using System.Globalization;
using System.Text.Json;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Updates;

/// <summary>
/// Periodic Steam update check (server build, game client build, Workshop mods) at
/// <see cref="Core.Settings.UpdateSettings.CheckInterval"/>. Each new finding is announced once.
/// In Automatic mode (or Scheduled, inside the maintenance window) the existing backup-first
/// update pipeline is run, waiting for the server to be empty when configured.
/// The game client is never updated by the app; Steam does that.
/// </summary>
public sealed class UpdateWatchService : BackgroundService
{
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan BusyRetry = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan WaitForEmptyRetry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromHours(6);

    private readonly ISettingsService _settings;
    private readonly IServerUpdateService _updates;
    private readonly IServerProcessManager _server;
    private readonly IServerActionGate _gate;
    private readonly IRconService _rcon;
    private readonly IServerEventBus _events;
    private readonly IActivityLog _activity;
    private readonly ILogger<UpdateWatchService> _logger;
    private readonly string _statePath;
    private DateTimeOffset? _lastAutomaticFailure;

    public UpdateWatchService(
        ISettingsService settings,
        IServerUpdateService updates,
        IServerProcessManager server,
        IServerActionGate gate,
        IRconService rcon,
        IServerEventBus events,
        IActivityLog activity,
        IAppPaths paths,
        ILogger<UpdateWatchService> logger)
    {
        _settings = settings;
        _updates = updates;
        _server = server;
        _gate = gate;
        _rcon = rcon;
        _events = events;
        _activity = activity;
        _logger = logger;
        _statePath = Path.Combine(paths.DataDirectory, "update-watch.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(FirstCheckDelay, stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = _settings.Current.Updates.CheckInterval;
            if (interval == UpdateCheckInterval.Disabled)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
                continue;
            }

            TimeSpan next;
            try
            {
                next = await RunOnceAsync(stoppingToken).ConfigureAwait(false) ?? TimeSpan.FromMinutes((int)interval);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Scheduled update check failed.");
                next = TimeSpan.FromMinutes((int)interval);
            }

            await Task.Delay(next, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>One check. Returns a shorter delay when it should look again soon.</summary>
    private async Task<TimeSpan?> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (_gate.IsBusy)
        {
            return BusyRetry;
        }

        var result = await _updates.CheckAsync(cancellationToken).ConfigureAwait(false);
        AnnounceNewFindings(result);

        var mode = _settings.Current.Updates.AutomationMode;
        var modsPending = result.ModsNeedingUpdate.Count > 0;
        if (mode == AutomationMode.Manual || (!result.ServerUpdateAvailable && !modsPending))
        {
            return null;
        }

        if (mode == AutomationMode.Scheduled && !InMaintenanceWindow(DateTime.Now))
        {
            return null;
        }

        if (_lastAutomaticFailure is { } failedAt && DateTimeOffset.UtcNow - failedAt < FailureCooldown)
        {
            // Do not hammer Steam or repeat the failure notice every check.
            return null;
        }

        var online = _server.State.Status is ServerStatus.Online;
        if (online && _server.State.PlayerCount > 0)
        {
            if (_settings.Current.Updates.WaitUntilEmptyWhenPlayersOnline)
            {
                _logger.LogInformation("Automatic update is waiting: {Count} player(s) online.", _server.State.PlayerCount);
                return WaitForEmptyRetry;
            }

            if (_settings.Current.Updates.WarnPlayersBeforeRestart)
            {
                await WarnPlayersAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await _activity.AddAsync("Updates", "Automatic update started.", cancellationToken: cancellationToken).ConfigureAwait(false);
        try
        {
            if (result.ServerUpdateAvailable && modsPending)
            {
                await _updates.UpdateEverythingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else if (result.ServerUpdateAvailable)
            {
                await _updates.UpdateAsync(restartAfter: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _updates.UpdateModsAsync(restartAfter: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The pipeline already logged it and published UpdateFailed. Retry after the cooldown.
            _lastAutomaticFailure = DateTimeOffset.UtcNow;
            _logger.LogError(ex, "Automatic update failed.");
        }

        return null;
    }

    private void AnnounceNewFindings(ServerUpdateCheckResult result)
    {
        var announced = LoadAnnounced();
        var changed = false;
        if (result.ServerUpdateAvailable && result.AvailableBuild is not null && announced.Add($"server:{result.AvailableBuild}"))
        {
            changed = true;
            _events.Publish(new ServerEvent
            {
                Kind = ServerEventKind.ServerUpdateAvailable,
                InstalledBuild = result.InstalledBuild,
                AvailableBuild = result.AvailableBuild
            });
        }

        if (result.ClientUpdateAvailable && result.ClientAvailableBuild is not null && announced.Add($"client:{result.ClientAvailableBuild}"))
        {
            changed = true;
            _events.Publish(new ServerEvent
            {
                Kind = ServerEventKind.ClientUpdateAvailable,
                InstalledBuild = result.ClientInstalledBuild,
                AvailableBuild = result.ClientAvailableBuild
            });
        }

        var newMods = result.ModsNeedingUpdate
            .Where(m => announced.Add($"mod:{m.WorkshopId}:{m.LatestWorkshopTimestamp?.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) ?? m.LatestWorkshopVersion ?? "?"}"))
            .Select(m => m.Name)
            .ToArray();
        if (newMods.Length > 0)
        {
            changed = true;
            _events.Publish(new ServerEvent { Kind = ServerEventKind.ModUpdateAvailable, Items = newMods });
        }

        if (changed)
        {
            SaveAnnounced(announced);
        }
    }

    private async Task WarnPlayersAsync(CancellationToken cancellationToken)
    {
        var lead = Math.Clamp(_settings.Current.Updates.WarningLeadMinutes, 1, 60);
        var marks = new[] { lead, 5, 1 }.Where(m => m <= lead).Distinct().OrderByDescending(m => m).ToArray();
        for (var i = 0; i < marks.Length; i++)
        {
            try
            {
                await _rcon.AnnounceAsync($"[SERVER] Server se tat sau {marks[i]} phut de cap nhat. Hay ve noi an toan va thoat game.", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Could not announce the update warning via RCON.");
            }

            var wait = i + 1 < marks.Length ? marks[i] - marks[i + 1] : marks[i];
            await Task.Delay(TimeSpan.FromMinutes(wait), cancellationToken).ConfigureAwait(false);
        }
    }

    private bool InMaintenanceWindow(DateTime localNow)
    {
        var updates = _settings.Current.Updates;
        if (!TimeOnly.TryParse(updates.MaintenanceWindowStart, CultureInfo.InvariantCulture, out var start)
            || !TimeOnly.TryParse(updates.MaintenanceWindowEnd, CultureInfo.InvariantCulture, out var end))
        {
            return false;
        }

        var now = TimeOnly.FromDateTime(localNow);
        return start <= end ? now >= start && now < end : now >= start || now < end;
    }

    private HashSet<string> LoadAnnounced()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(_statePath)) ?? new HashSet<string>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read {Path}.", _statePath);
        }

        return new HashSet<string>();
    }

    private void SaveAnnounced(HashSet<string> announced)
    {
        try
        {
            File.WriteAllText(_statePath, JsonSerializer.Serialize(announced.Order(StringComparer.Ordinal).ToArray()));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not write {Path}.", _statePath);
        }
    }
}
