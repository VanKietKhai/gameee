using System.IO.Compression;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Health;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.ProcessManagement;

/// <summary>
/// Restarts the dedicated server every <see cref="Core.Settings.AdvancedSettings.ScheduledRestartHours"/>
/// hours of uptime so the server log starts fresh. With players online a 30-minute countdown
/// (in-game warnings at 30/10/5/1 minutes) starts 30 minutes before the mark; with nobody online
/// it restarts at the mark straight away. After every server start the previous server logs are
/// archived (gzip) under the app data folder, because Unreal purges its backup logs after a few days.
/// </summary>
public sealed class ScheduledRestartService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan WarningLead = TimeSpan.FromMinutes(30);

    private readonly IServerProcessManager _server;
    private readonly IDelayedRestartService _countdown;
    private readonly IServerActionGate _gate;
    private readonly ISettingsService _settings;
    private readonly IActivityLog _activity;
    private readonly IAppPaths _paths;
    private readonly ILogger<ScheduledRestartService> _logger;
    private DateTimeOffset? _handledStart;
    private DateTimeOffset? _archivedStart;

    public ScheduledRestartService(
        IServerProcessManager server,
        IDelayedRestartService countdown,
        IServerActionGate gate,
        ISettingsService settings,
        IActivityLog activity,
        IAppPaths paths,
        ILogger<ScheduledRestartService> logger)
    {
        _server = server;
        _countdown = countdown;
        _gate = gate;
        _settings = settings;
        _activity = activity;
        _paths = paths;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Scheduled restart check failed.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        var state = _server.State;
        if (state.Status is not ServerStatus.Online || state.StartedAt is not { } startedAt)
        {
            return;
        }

        if (_archivedStart != startedAt)
        {
            _archivedStart = startedAt;
            ArchiveServerLogs();
        }

        var hours = _settings.Current.Advanced.ScheduledRestartHours;
        var decision = Decide(hours, DateTimeOffset.UtcNow - startedAt, state.PlayerCount);
        if (decision == RestartDecision.None
            || _handledStart == startedAt          // already scheduled (or cancelled) for this run
            || _countdown.IsCountdownActive
            || _gate.IsBusy)
        {
            return;
        }

        _handledStart = startedAt;
        if (decision == RestartDecision.Countdown)
        {
            await _activity.AddAsync("Server", $"Scheduled {hours} h restart: 30-minute countdown started.", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await _countdown.StartAsync(new DelayedRestartRequest
            {
                Delay = RestartDelay.Minutes30,
                BackupFirst = false,
                Reason = "scheduled-restart"
            }, CancellationToken.None).ConfigureAwait(false);
        }
        else
        {
            await _activity.AddAsync("Server", $"Scheduled {hours} h restart (no players online).", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await _server.RestartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public enum RestartDecision
    {
        None,
        Countdown,
        Now
    }

    /// <summary>Countdown 30 minutes before the mark when players are online; otherwise restart at the mark.</summary>
    public static RestartDecision Decide(int intervalHours, TimeSpan uptime, int playerCount)
    {
        if (intervalHours <= 0)
        {
            return RestartDecision.None;
        }

        var interval = TimeSpan.FromHours(intervalHours);
        if (playerCount > 0)
        {
            return uptime >= interval - WarningLead ? RestartDecision.Countdown : RestartDecision.None;
        }

        return uptime >= interval ? RestartDecision.Now : RestartDecision.None;
    }

    /// <summary>Gzip-copies the dedicated server's rotated logs that are not archived yet.</summary>
    private void ArchiveServerLogs()
    {
        try
        {
            var log = ConanLogShutdownProbe.LogPathFor(_settings.Current.ServerPaths);
            var logDir = log is null ? null : Path.GetDirectoryName(log);
            if (logDir is null || !Directory.Exists(logDir))
            {
                return;
            }

            var archive = Directory.CreateDirectory(Path.Combine(_paths.DataDirectory, "server-log-archive")).FullName;
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(logDir, "*-backup-*.log"))
            {
                var target = Path.Combine(archive, Path.GetFileName(file) + ".gz");
                if (File.Exists(target))
                {
                    continue;
                }

                using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var output = File.Create(target);
                using var gzip = new GZipStream(output, CompressionLevel.SmallestSize);
                source.CopyTo(gzip);
                count++;
            }

            if (count > 0)
            {
                _logger.LogInformation("Archived {Count} server log(s) to {Dir}.", count, archive);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not archive server logs.");
        }
    }
}
