using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Updates;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Updates;

public sealed class ServerUpdateService : IServerUpdateService
{
    private readonly ISettingsService _settings;
    private readonly ISteamCmdService _steamCmd;
    private readonly IServerProcessManager _server;
    private readonly IBackupService _backups;
    private readonly IWorkshopModService _mods;
    private readonly IServerActionGate _gate;
    private readonly IActivityLog _activityLog;
    private readonly ILogger<ServerUpdateService> _logger;
    private readonly UpdatePipelineStateMachine _pipeline = new();

    public ServerUpdateService(
        ISettingsService settings,
        ISteamCmdService steamCmd,
        IServerProcessManager server,
        IBackupService backups,
        IWorkshopModService mods,
        IServerActionGate gate,
        IActivityLog activityLog,
        ILogger<ServerUpdateService> logger)
    {
        _settings = settings;
        _steamCmd = steamCmd;
        _server = server;
        _backups = backups;
        _mods = mods;
        _gate = gate;
        _activityLog = activityLog;
        _logger = logger;
    }

    public Task<ServerUpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var install = _settings.Current.ServerPaths.ServerInstallDirectory
                      ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        string? installed = null;
        if (!string.IsNullOrWhiteSpace(install))
        {
            var manifest = Path.Combine(install, "steamapps", $"appmanifest_{AppConstants.ConanDedicatedServerAppId}.acf");
            if (File.Exists(manifest))
            {
                installed = TryReadBuildId(File.ReadAllText(manifest));
            }
        }

        var summary = installed is null
            ? "Installed build is unknown. Use Update Server to let SteamCMD refresh files."
            : $"Installed dedicated server build: {installed}";

        _logger.LogInformation("Update check: {Summary}", summary);
        return Task.FromResult(new ServerUpdateCheckResult
        {
            InstalledBuild = installed,
            AvailableBuild = null,
            UpdateAvailable = false,
            Summary = summary + " Latest Steam build comparison is not fully wired yet (no Steam Web API key is used in Phase 1)."
        });
    }

    public async Task UpdateAsync(bool restartAfter, IProgress<PipelineProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!_gate.TryBegin("Update server", out var lease) || lease is null)
        {
            throw new UserFacingException(
                "Another action is already running",
                $"Current action: {_gate.CurrentAction}",
                "Wait for it to finish before starting an update.");
        }

        using (lease)
        {
            _pipeline.Begin();
            try
            {
                Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Checking, "Checking install directory..."));

                var install = _settings.Current.ServerPaths.ServerInstallDirectory
                              ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
                if (string.IsNullOrWhiteSpace(install))
                {
                    throw new UserFacingException(
                        "Dedicated server folder is not set",
                        "SteamCMD needs an install directory for app 443030.",
                        "Open Settings, set the server install folder, then try Update Server again.");
                }

                if (_settings.Current.Backups.BackupBeforeServerUpdate)
                {
                    Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Backup, "Creating backup..."));
                    await _backups.BackupNowAsync("pre-server-update", cancellationToken).ConfigureAwait(false);
                }

                if (_server.State.Status is not ServerStatus.Offline and not ServerStatus.Error)
                {
                    Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Stopping, "Stopping server..."));
                    await _server.StopAsync(force: false, cancellationToken).ConfigureAwait(false);
                }

                Report(progress, _pipeline.TransitionTo(UpdatePipelineState.UpdatingServer, "Downloading server files..."));
                await _steamCmd.InstallOrUpdateDedicatedServerAsync(
                        install,
                        _settings.Current.SteamCmd.ValidateAfterUpdate,
                        new Progress<string>(line => _logger.LogInformation("SteamCMD: {Line}", line)),
                        cancellationToken)
                    .ConfigureAwait(false);

                Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Validating, "Validating update..."));
                if (restartAfter)
                {
                    Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Starting, "Starting server..."));
                    await _server.StartAsync(cancellationToken).ConfigureAwait(false);
                    Report(progress, _pipeline.TransitionTo(UpdatePipelineState.HealthCheck, "Checking process..."));
                }

                Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Completed, "Update completed. Existing saves were not deleted."));
                await _activityLog.AddAsync("Updates", "Dedicated server update completed.", cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _pipeline.Fail(ex.Message);
                progress?.Report(_pipeline.Snapshot());
                _logger.LogError(ex, "Server update failed. Existing server files were not deleted.");
                throw new UserFacingException(
                    "Server update failed",
                    ex is UserFacingException ufe ? ufe.Message : ex.Message,
                    "The existing dedicated server and world saves were not deleted. Open the SteamCMD log, fix the error, then retry.",
                    ex);
            }
        }
    }

    private static void Report(IProgress<PipelineProgress>? progress, PipelineProgress snapshot) =>
        progress?.Report(snapshot);

    private static string? TryReadBuildId(string acf)
    {
        const string key = "\"buildid\"";
        var index = acf.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var rest = acf[(index + key.Length)..];
        var first = rest.IndexOf('"', StringComparison.Ordinal);
        var second = rest.IndexOf('"', first + 1);
        if (first < 0 || second < 0)
        {
            return null;
        }

        return rest.Substring(first + 1, second - first - 1);
    }
}

public sealed class DelayedRestartService : IDelayedRestartService
{
    private readonly IServerProcessManager _server;
    private readonly IRconService _rcon;
    private readonly IBackupService _backups;
    private readonly IActivityLog _activityLog;
    private readonly ILogger<DelayedRestartService> _logger;
    private CancellationTokenSource? _cts;
    private DateTimeOffset? _deadline;

    public DelayedRestartService(
        IServerProcessManager server,
        IRconService rcon,
        IBackupService backups,
        IActivityLog activityLog,
        ILogger<DelayedRestartService> logger)
    {
        _server = server;
        _rcon = rcon;
        _backups = backups;
        _activityLog = activityLog;
        _logger = logger;
    }

    public bool IsCountdownActive => _cts is { IsCancellationRequested: false } && _deadline is not null;

    public TimeSpan? Remaining => _deadline is null ? null : _deadline.Value - DateTimeOffset.UtcNow;

    public async Task StartAsync(DelayedRestartRequest request, CancellationToken cancellationToken = default)
    {
        await CancelAsync().ConfigureAwait(false);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;
        var delay = TimeSpan.FromMinutes((int)request.Delay);
        _deadline = DateTimeOffset.UtcNow + delay;
        await _activityLog.AddAsync("Server", $"Restart scheduled for {(int)request.Delay} minutes.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _ = Task.Run(() => RunCountdownAsync(request, token), token);
    }

    public Task CancelAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _deadline = null;
        return Task.CompletedTask;
    }

    private async Task RunCountdownAsync(DelayedRestartRequest request, CancellationToken token)
    {
        try
        {
            var totalMinutes = (int)request.Delay;
            var marks = new[] { 10, 5, 1 }
                .Where(m => m <= totalMinutes)
                .Select(m => TimeSpan.FromMinutes(m))
                .Concat([TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10)])
                .Distinct()
                .OrderByDescending(t => t)
                .ToArray();

            if (totalMinutes == 0)
            {
                await FinishAsync(request, token).ConfigureAwait(false);
                return;
            }

            var start = DateTimeOffset.UtcNow;
            var end = start + TimeSpan.FromMinutes(totalMinutes);
            foreach (var mark in marks)
            {
                var fireAt = end - mark;
                var wait = fireAt - DateTimeOffset.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, token).ConfigureAwait(false);
                }

                var text = mark.TotalMinutes >= 1
                    ? $"Server restart in {(int)mark.TotalMinutes} minutes."
                    : $"Server restart in {(int)mark.TotalSeconds} seconds.";
                try
                {
                    await _rcon.AnnounceAsync(text, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not announce restart warning via RCON.");
                }

                await _activityLog.AddAsync("Server", text, cancellationToken: token).ConfigureAwait(false);
            }

            var remaining = end - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, token).ConfigureAwait(false);
            }

            await FinishAsync(request, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _activityLog.AddAsync("Server", "Delayed restart cancelled.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delayed restart failed.");
        }
        finally
        {
            _deadline = null;
        }
    }

    private async Task FinishAsync(DelayedRestartRequest request, CancellationToken token)
    {
        if (request.BackupFirst)
        {
            await _backups.BackupNowAsync(request.Reason ?? "delayed-restart", token).ConfigureAwait(false);
        }

        await _server.RestartAsync(token).ConfigureAwait(false);
    }
}
