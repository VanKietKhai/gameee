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

    public async Task<ServerUpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
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

        await _mods.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
        var modsNeeding = _settings.Current.Mods.Mods.Count(m => m.UpdateAvailable);
        _server.State.InstalledBuild = installed;
        _server.State.LastUpdateCheckAt = DateTimeOffset.UtcNow;
        _server.State.ModsRequiringUpdate = modsNeeding;
        _server.State.InstalledModCount = _settings.Current.Mods.Mods.Count;

        var serverLine = installed is null
            ? "Installed dedicated server build is unknown. Use Update Server to let SteamCMD refresh files."
            : $"Installed dedicated server build: {installed}. Latest Steam depot comparison is not available without a Steam Web API key; Update Server still runs SteamCMD app_update.";
        var modsLine = modsNeeding == 0
            ? "No Workshop mod updates were flagged."
            : $"{modsNeeding} Workshop mod(s) have a newer Steam timestamp than the last successful install.";

        var summary = serverLine + " " + modsLine;
        _logger.LogInformation("Update check: {Summary}", summary);
        await _activityLog.AddAsync("Updates", summary, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new ServerUpdateCheckResult
        {
            InstalledBuild = installed,
            AvailableBuild = null,
            UpdateAvailable = modsNeeding > 0,
            Summary = summary
        };
    }

    public Task UpdateAsync(bool restartAfter, IProgress<PipelineProgress>? progress = null, CancellationToken cancellationToken = default) =>
        RunLockedAsync("Update server", restartAfter, updateServer: true, updateMods: false, "pre-server-update", progress, cancellationToken);

    public Task UpdateModsAsync(bool restartAfter, IProgress<PipelineProgress>? progress = null, CancellationToken cancellationToken = default) =>
        RunLockedAsync("Update mods", restartAfter, updateServer: false, updateMods: true, "pre-mod-update", progress, cancellationToken);

    public Task UpdateSelectedModsAsync(long workshopId, IProgress<PipelineProgress>? progress = null, CancellationToken cancellationToken = default) =>
        RunLockedAsync("Update selected mods", restartAfter: false, updateServer: false, updateMods: true, "pre-mod-update", progress, cancellationToken, new[] { workshopId });

    public Task UpdateEverythingAsync(IProgress<PipelineProgress>? progress = null, CancellationToken cancellationToken = default) =>
        RunLockedAsync("Update everything", restartAfter: false, updateServer: true, updateMods: true, "pre-update-everything", progress, cancellationToken);

    public Task ImportLocalModAsync(string sourcePakPath, IProgress<PipelineProgress>? progress = null, CancellationToken cancellationToken = default) =>
        RunLocalModAsync("Import local mod", "pre-local-mod-import", sourcePakPath, replaceModKey: null, progress, cancellationToken);

    public Task ReplaceLocalModAsync(string modKey, string sourcePakPath, IProgress<PipelineProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modKey);
        return RunLocalModAsync("Replace local mod", "pre-local-mod-update", sourcePakPath, modKey, progress, cancellationToken);
    }

    /// <summary>
    /// Local mod import/manual update. The .pak is validated, copied and hashed BEFORE the
    /// server is touched, so a bad file never stops the server. The commit then runs inside the
    /// same locked pipeline as Workshop updates (stop if running, verified cold backup,
    /// transactional commit, restart only if it was running). SteamCMD is never used.
    /// </summary>
    private async Task RunLocalModAsync(
        string actionName,
        string backupReason,
        string sourcePakPath,
        string? replaceModKey,
        IProgress<PipelineProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (_mods is not IModCatalogService catalog)
        {
            throw new UserFacingException(
                "Local mods are not available",
                "The configured mod service does not support Local mods.",
                "Restart Conan Server Control.");
        }

        var staged = await catalog.StageLocalPakAsync(sourcePakPath, cancellationToken).ConfigureAwait(false);
        try
        {
            await RunLockedAsync(
                    actionName,
                    restartAfter: false,
                    updateServer: false,
                    updateMods: true,
                    backupReason,
                    progress,
                    cancellationToken,
                    workshopIds: null,
                    modStep: ct => catalog.CommitLocalPakAsync(staged, replaceModKey, ct),
                    modStepDescription: $"Installing local mod {staged.FileName}...")
                .ConfigureAwait(false);
        }
        finally
        {
            catalog.DiscardStagedLocalPak(staged);
        }
    }

    private async Task RunLockedAsync(
        string actionName,
        bool restartAfter,
        bool updateServer,
        bool updateMods,
        string backupReason,
        IProgress<PipelineProgress>? progress,
        CancellationToken cancellationToken,
        IReadOnlyList<long>? workshopIds = null,
        Func<CancellationToken, Task>? modStep = null,
        string? modStepDescription = null)
    {
        if (!_gate.TryBegin(actionName, out var lease) || lease is null)
        {
            throw new UserFacingException(
                "Another action is already running",
                $"Current action: {_gate.CurrentAction}",
                "Wait for it to finish before starting an update.");
        }

        using (lease)
        {
            _pipeline.Begin();
            var wasRunning = _server.State.Status is not ServerStatus.Offline and not ServerStatus.Error;
            var mutationStarted = false;
            var safetyBackupFailed = false;
            var startedAfterMutation = false;
            try
            {
                Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Checking, "Preparing update..."));
                if (updateServer)
                {
                    var install = _settings.Current.ServerPaths.ServerInstallDirectory
                                  ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
                    if (string.IsNullOrWhiteSpace(install))
                    {
                        throw new UserFacingException(
                            "Dedicated server folder is not set",
                            "SteamCMD needs an install directory for app 443030.",
                            "Open Settings, set the server install folder, then try Update Server again.");
                    }
                }

                if (wasRunning)
                {
                    Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Stopping, "Stopping server..."));
                    await _server.StopUnderLockAsync(lease, force: false, cancellationToken).ConfigureAwait(false);
                    if (_server.State.Status is not ServerStatus.Offline and not ServerStatus.Error)
                    {
                        throw new UserFacingException(
                            "Could not stop the dedicated server",
                            $"The server is still {_server.State.Status}. The update was aborted. No backup or update was performed.",
                            "Stop the server manually, then retry.");
                    }
                }

                var shouldBackup = (updateServer && _settings.Current.Backups.BackupBeforeServerUpdate)
                                   || (updateMods && _settings.Current.Backups.BackupBeforeModUpdate);
                if (shouldBackup)
                {
                    Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Backup, "Creating verified cold backup..."));
                    try
                    {
                        await _backups.BackupNowAsync(backupReason, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        safetyBackupFailed = true;
                        _logger.LogError(ex, "Update aborted because safety backup failed.");
                        throw;
                    }
                }

                if (updateServer)
                {
                    var install = _settings.Current.ServerPaths.ServerInstallDirectory
                                  ?? _settings.Current.ServerPaths.ServerWorkingDirectory!;
                    mutationStarted = true;
                    Report(progress, _pipeline.TransitionTo(UpdatePipelineState.UpdatingServer, "Downloading server files..."));
                    await _steamCmd.InstallOrUpdateDedicatedServerAsync(
                            install,
                            _settings.Current.SteamCmd.ValidateAfterUpdate,
                            new Progress<string>(line => _logger.LogInformation("SteamCMD: {Line}", line)),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                if (updateMods)
                {
                    mutationStarted = true;
                    Report(progress, _pipeline.TransitionTo(UpdatePipelineState.UpdatingMods, modStepDescription ?? "Updating Steam Workshop mods..."));
                    if (modStep is not null)
                    {
                        await modStep(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _mods.ApplyUpdatesAsync(workshopIds, cancellationToken).ConfigureAwait(false);
                    }
                }

                Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Validating, "Validating update..."));
                // Preserve the original process state. Do not treat the generic
                // restartAfter flag as an explicit StartAfterwards request.
                if (wasRunning)
                {
                    Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Starting, "Starting server..."));
                    startedAfterMutation = true;
                    await _server.StartUnderLockAsync(lease, cancellationToken).ConfigureAwait(false);
                    if (_server.State.Status is not ServerStatus.Online)
                    {
                        throw new UserFacingException(
                            $"{actionName} failed",
                            "The dedicated server process started but did not become ready.",
                            "The update may have been applied. The server is not Online. Inspect the dedicated-server log.");
                    }

                    Report(progress, _pipeline.TransitionTo(UpdatePipelineState.HealthCheck, "Waiting for server readiness..."));
                }

                Report(progress, _pipeline.TransitionTo(UpdatePipelineState.Completed, "Update completed. Existing saves were not deleted."));
                await _activityLog.AddAsync("Updates", $"{actionName} completed.", cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _pipeline.Fail(ex.Message);
                progress?.Report(_pipeline.Snapshot());
                _logger.LogError(
                    ex,
                    "{Action} failed after wasRunning={WasRunning} mutationStarted={MutationStarted} safetyBackupFailed={BackupFailed}. Server files were not deleted. Current status={Status}.",
                    actionName,
                    wasRunning,
                    mutationStarted,
                    safetyBackupFailed,
                    _server.State.Status);

                if (safetyBackupFailed)
                {
                    await _activityLog.AddAsync(
                            "Updates",
                            "Update aborted because safety backup failed.",
                            cancellationToken: CancellationToken.None)
                        .ConfigureAwait(false);
                }

                var batchFailure = FindModBatchFailure(ex);
                var verifiedRestore = batchFailure is { IsSafeToRestart: true };
                var recoveryRequired = batchFailure is not null && !verifiedRestore;
                var detail = ex is UserFacingException facing ? facing.Message : ex.Message;

                if (recoveryRequired)
                {
                    _logger.LogError(
                        ex,
                        "{Message}",
                        ModBatchCommitException.UnverifiedRollbackGuidance);
                    await _activityLog.AddAsync(
                            "Updates",
                            ModBatchCommitException.UnverifiedRollbackGuidance,
                            cancellationToken: CancellationToken.None)
                        .ConfigureAwait(false);
                    throw new UserFacingException(
                        ModBatchCommitException.RecoveryRequiredTitle,
                        detail,
                        ModBatchCommitException.UnverifiedRollbackGuidance,
                        ex);
                }

                if (updateServer && batchFailure is not null)
                {
                    _logger.LogWarning(
                        "Server binary update may have succeeded, but the Workshop mod live commit failed. The operation is FAILED. Previous mod set was restored.");
                }

                var restoredOnline = false;
                var mayRestart = wasRunning
                    && !startedAfterMutation
                    && (batchFailure is null || verifiedRestore)
                    && _server.State.Status is ServerStatus.Offline or ServerStatus.Error;
                if (mayRestart)
                {
                    if (verifiedRestore)
                    {
                        const string restoredMessage =
                            "Mod update failed. Previous mod set restored. Restarting server with previous versions.";
                        _logger.LogWarning(restoredMessage);
                        await _activityLog.AddAsync("Updates", restoredMessage, cancellationToken: CancellationToken.None)
                            .ConfigureAwait(false);
                    }

                    try
                    {
                        await _server.StartUnderLockAsync(lease, cancellationToken).ConfigureAwait(false);
                        restoredOnline = _server.State.Status is ServerStatus.Online;
                    }
                    catch (Exception startEx)
                    {
                        _logger.LogError(startEx, "Could not restore the previously running server after {Action} failed.", actionName);
                    }
                }

                var guidance = safetyBackupFailed && restoredOnline
                    ? "Update aborted because safety backup failed. The live world was not mutated. The previously running server was started again."
                    : safetyBackupFailed && wasRunning && !restoredOnline
                        ? "Update aborted because safety backup failed. The live world was not mutated. The server was stopped for the backup and was not restarted."
                    : verifiedRestore && restoredOnline
                        ? "Mod update failed. Previous mod set restored. Restarting server with previous versions."
                        : restoredOnline
                            ? "The existing dedicated server and world saves were not deleted. A safety backup was kept if backup was enabled. Open the SteamCMD log, fix the error, then retry."
                            : wasRunning
                                ? "The server was stopped for this update and was not restarted. A safety backup was kept if backup was enabled. Start the server manually after you inspect the failure."
                                : "The existing dedicated server and world saves were not deleted. Open the SteamCMD log, fix the error, then retry.";

                throw new UserFacingException(
                    safetyBackupFailed ? $"{actionName} aborted" : $"{actionName} failed",
                    detail,
                    guidance,
                    ex);
            }
        }
    }

    private static ModBatchCommitException? FindModBatchFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is ModBatchCommitException batch)
            {
                return batch;
            }
        }

        return null;
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
