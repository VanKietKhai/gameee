using System.Diagnostics;
using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Diagnostics;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.ProcessManagement;

public sealed class ServerProcessManager : IServerProcessManager, IDisposable
{
    private static readonly string[] KnownProcessNames =
    [
        AppConstants.DedicatedServerProcessName,
        AppConstants.DedicatedServerShippingProcessName
    ];

    private readonly ISettingsService _settings;
    private readonly IProcessStarter _processStarter;
    private readonly IServerActionGate _actionGate;
    private readonly IActivityLog _activityLog;
    private readonly ILogger<ServerProcessManager> _logger;
    private readonly IRconService _rcon;
    private readonly IServerReadinessProbe _readiness;
    private readonly IServerShutdownProbe? _shutdownProbe;
    private readonly object _sync = new();

    private IManagedProcess? _process;
    private bool _stopInProgress;
    private CancellationTokenSource? _monitorCts;
    private Task? _outputPump;
    private DateTimeOffset _cpuSampleAt = DateTimeOffset.MinValue;
    private TimeSpan _cpuSampleValue = TimeSpan.Zero;
    private readonly Queue<DateTimeOffset> _crashTimes = new();

    public ServerProcessManager(
        ISettingsService settings,
        IProcessStarter processStarter,
        IServerActionGate actionGate,
        IActivityLog activityLog,
        IRconService rcon,
        IServerReadinessProbe readiness,
        ILogger<ServerProcessManager> logger,
        IServerShutdownProbe? shutdownProbe = null)
    {
        _settings = settings;
        _processStarter = processStarter;
        _actionGate = actionGate;
        _activityLog = activityLog;
        _rcon = rcon;
        _readiness = readiness;
        _logger = logger;
        _shutdownProbe = shutdownProbe;
        State = new ServerRuntimeState();
    }

    public ServerRuntimeState State { get; private set; }

    public event EventHandler<ServerRuntimeState>? StateChanged;

    public bool IsConanServerProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        return KnownProcessNames.Any(known => string.Equals(known, name, StringComparison.OrdinalIgnoreCase));
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (_process is { HasExited: false })
            {
                SampleResourceUsage(_process);
                PublishLocked();
                return Task.CompletedTask;
            }
        }

        TryAttachToExistingProcess();
        return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_actionGate.TryBegin("Start server", out var lease) || lease is null)
        {
            throw new UserFacingException(
                "Server action already running",
                $"Cannot start the server because another action is in progress: {_actionGate.CurrentAction}.",
                "Wait for the current action to finish, then try again.");
        }

        using (lease)
        {
            await StartCoreAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!_actionGate.TryBegin(force ? "Force stop server" : "Stop server", out var lease) || lease is null)
        {
            throw new UserFacingException(
                "Server action already running",
                $"Cannot stop the server because another action is in progress: {_actionGate.CurrentAction}.",
                "Wait for the current action to finish, or cancel it if it is a delayed restart.");
        }

        using (lease)
        {
            await StopCoreAsync(force, expected: true, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        if (!_actionGate.TryBegin("Restart server", out var lease) || lease is null)
        {
            throw new UserFacingException(
                "Server action already running",
                $"Cannot restart the server because another action is in progress: {_actionGate.CurrentAction}.",
                "Wait for the current action to finish.");
        }

        using (lease)
        {
            SetStatus(ServerStatus.Restarting, "Restarting the Conan dedicated server.");
            await _activityLog.AddAsync("Server", "Restart requested.", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (State.Status != ServerStatus.Offline)
            {
                await StopCoreAsync(force: false, expected: true, cancellationToken).ConfigureAwait(false);
            }

            await StartCoreAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StartUnderLockAsync(IServerOperationLease lease, CancellationToken cancellationToken = default)
    {
        EnsureOwns(lease, "start");
        return StartCoreAsync(cancellationToken);
    }

    public Task StopUnderLockAsync(IServerOperationLease lease, bool force = false, CancellationToken cancellationToken = default)
    {
        EnsureOwns(lease, "stop");
        return StopCoreAsync(force, expected: true, cancellationToken);
    }

    public void Dispose()
    {
        _monitorCts?.Cancel();
        _process?.Dispose();
        _monitorCts?.Dispose();
    }

    private void EnsureOwns(IServerOperationLease lease, string operation)
    {
        if (_actionGate.Owns(lease))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Cannot {operation} under lock because the caller does not own the current server action lease.");
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var paths = _settings.Current.ServerPaths;
        var exe = paths.ServerExecutablePath;

        // Hard safety gate (M3 Task 4): only ConanSandboxServer.exe outside the standalone
        // client folder may be launched. Blocks before any status change or process start.
        var gate = ServerExecutableGate.Evaluate(exe, _settings.Current.Client.RootDirectory);
        if (!gate.Allowed)
        {
            _logger.LogError("Server start blocked: {Reason} ({Exe})", gate.Reason, exe);
            throw new UserFacingException(
                "Server start blocked",
                $"{gate.Reason}{Environment.NewLine}Configured path:{Environment.NewLine}{exe ?? "(not set)"}",
                $"Choose {AppConstants.DedicatedServerExecutable} from the dedicated server install (SteamCMD app {AppConstants.ConanDedicatedServerAppId}) in Settings.");
        }

        if (!File.Exists(exe))
        {
            throw new UserFacingException(
                "Conan server executable was not found",
                $"Configured path:{Environment.NewLine}{exe ?? "(not set)"}",
                "Open Settings and choose the ConanSandboxServer.exe that SteamCMD installed, or install the dedicated server first.");
        }

        if (!PathValidator.IsSafeAbsolutePath(exe))
        {
            throw new UserFacingException(
                "Invalid server path",
                $"The configured Conan server path is not a safe absolute path:{Environment.NewLine}{exe}",
                "Choose the executable again from Settings.");
        }

        lock (_sync)
        {
            if (_process is { HasExited: false })
            {
                throw new UserFacingException(
                    "Server already running",
                    $"The Conan dedicated server is already running (PID {_process.Id}).",
                    "Use Restart if you need to apply configuration changes.");
            }
        }

        var workingDirectory = string.IsNullOrWhiteSpace(paths.ServerWorkingDirectory)
            ? Path.GetDirectoryName(exe)!
            : paths.ServerWorkingDirectory;

        if (!Directory.Exists(workingDirectory))
        {
            throw new UserFacingException(
                "Server working directory was not found",
                $"Configured directory:{Environment.NewLine}{workingDirectory}",
                "Open Settings and choose the dedicated server install folder.");
        }

        var arguments = BuildLaunchArguments();
        _logger.LogInformation("Starting Conan dedicated server: {Exe} {Args}", exe, arguments);
        SetStatus(ServerStatus.Starting, null);

        IManagedProcess process;
        try
        {
            process = _processStarter.Start(new ProcessStartRequest
            {
                FileName = exe,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardIO = true,
                CreateNoWindow = true
            });
        }
        catch (UserFacingException)
        {
            SetStatus(ServerStatus.Error, "The Conan dedicated server process failed to start.");
            throw;
        }
        catch (Exception ex)
        {
            SetStatus(ServerStatus.Error, ex.Message);
            throw new UserFacingException(
                "Failed to start Conan dedicated server",
                ex.Message,
                "Confirm the executable path and that Visual C++ runtimes required by Unreal are installed.",
                ex);
        }

        lock (_sync)
        {
            _process?.Dispose();
            _process = process;
            State.ProcessId = process.Id;
            State.StartedAt = DateTimeOffset.UtcNow;
            State.StoppedAt = null;
            State.LastError = null;
            State.LastErrorGuidance = null;
            State.LastExitWasCrash = false;
            State.CrashLoopDetected = false;
            State.Status = ServerStatus.Starting;
            PublishLocked();
        }

        process.Exited += OnProcessExited;
        _monitorCts?.Cancel();
        _monitorCts = new CancellationTokenSource();
        _outputPump = PumpOutputAsync(process, _monitorCts.Token);

        await _activityLog.AddAsync("Server", $"Server process launched (PID {process.Id}). Waiting for readiness.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await WaitUntilReadyAsync(process, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            SetStatus(ServerStatus.Error, "Server start was cancelled before the dedicated server became ready.");
            throw;
        }

        await _activityLog.AddAsync("Server", "Server is ready.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task StopCoreAsync(bool force, bool expected, CancellationToken cancellationToken)
    {
        IManagedProcess? process;
        lock (_sync)
        {
            process = _process;
            if (process is null || process.TreeHasExited)
            {
                State.Status = ServerStatus.Offline;
                State.Health = HealthCheckResult.ServerOffline;
                State.ProcessId = null;
                PublishLocked();
                return;
            }

            _stopInProgress = true;
            State.Status = ServerStatus.Stopping;
            PublishLocked();
        }

        try
        {
            await StopTreeAsync(process, force, expected, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                _stopInProgress = false;
            }
        }
    }

    /// <summary>
    /// Graceful first, force-kill last. A shutdown the server acknowledged, or whose progress its log
    /// shows, may outlast the graceful window and is killed only at the emergency ceiling; otherwise a
    /// short window keeps an unresponsive server from delaying the kill. Offline is reported only once
    /// the whole process tree (launcher and the -Shipping server child) is gone.
    /// </summary>
    private async Task StopTreeAsync(IManagedProcess process, bool force, bool expected, CancellationToken cancellationToken)
    {
        var report = new ServerStopReport { RequestedAt = DateTimeOffset.UtcNow, ForceRequested = force };
        await _activityLog.AddAsync("Server", force ? "Force stop requested." : "Graceful stop requested.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        process.TrackDescendants();
        var advanced = _settings.Current.Advanced;
        var extendedWindow = TimeSpan.FromSeconds(Math.Max(5, advanced.GracefulStopTimeoutSeconds));
        var shortWindow = TimeSpan.FromSeconds(Math.Clamp(advanced.UnacknowledgedStopTimeoutSeconds, 1, extendedWindow.TotalSeconds));
        var emergencyCeiling = TimeSpan.FromSeconds(Math.Max(extendedWindow.TotalSeconds, advanced.EmergencyStopCeilingSeconds));
        var forceTimeout = TimeSpan.FromSeconds(Math.Max(3, advanced.ForceStopTimeoutSeconds));

        if (!force)
        {
            var mark = _shutdownProbe?.Mark() ?? 0;
            report = await TryGracefulShutdownAsync(report, cancellationToken).ConfigureAwait(false);
            report = await WaitForGracefulExitAsync(process, report, mark, shortWindow, extendedWindow, emergencyCeiling, cancellationToken)
                .ConfigureAwait(false);
            if (process.TreeHasExited)
            {
                FinalizeStop(process, expected, report);
                return;
            }

            _logger.LogWarning(
                "Graceful stop did not finish within {Limit} (acknowledged: {Acknowledged}, progress: {Progress}). Attempting CloseMainWindow.",
                TimeSpan.FromSeconds(report.ExtendedWindowUsed ? report.EmergencyCeilingSeconds : report.GracefulWindowSeconds),
                report.ShutdownAcknowledged, report.ShutdownProgressEvidence ?? "none");
            // A console server started without a window has no main window; only wait when one was asked to close.
            if (process.CloseMainWindow() &&
                await WaitForTreeExitAsync(process, TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false))
            {
                FinalizeStop(process, expected, report);
                return;
            }
        }

        _logger.LogWarning("Forcing Conan dedicated server process tree {Pid} to terminate.", process.Id);
        report = report with { ForcedKill = true };
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Force terminate failed for PID {Pid}.", process.Id);
        }

        await WaitForTreeExitAsync(process, forceTimeout, cancellationToken).ConfigureAwait(false);
        if (!process.TreeHasExited)
        {
            lock (_sync)
            {
                State.LastStop = report;
            }

            SetStatus(ServerStatus.Error, "The dedicated server process tree did not exit after stop was requested.");
            throw new UserFacingException(
                "Server did not stop",
                $"PID {process.Id} or one of its child processes is still running after the stop timeout.",
                "Stop the process manually, then retry. No backup or update was performed.");
        }

        FinalizeStop(process, expected, report);
    }

    private async Task<ServerStopReport> TryGracefulShutdownAsync(ServerStopReport report, CancellationToken cancellationToken)
    {
        if (!_settings.Current.Rcon.Enabled || string.IsNullOrWhiteSpace(_settings.Secrets.RconPassword))
        {
            _logger.LogInformation("RCON is not configured; no graceful shutdown command can be sent.");
            return report;
        }

        try
        {
            await _rcon.AnnounceAsync(Core.Notifications.RconMessages.ShuttingDown, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "RCON shutdown announcement failed; sending the shutdown command anyway.");
        }

        var command = string.IsNullOrWhiteSpace(_settings.Current.Rcon.ShutdownCommand)
            ? AppConstants.DefaultRconShutdownCommand
            : _settings.Current.Rcon.ShutdownCommand.Trim();
        report = report with { ShutdownCommand = command, ShutdownSentAt = DateTimeOffset.UtcNow };
        try
        {
            var reply = await _rcon.SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
            var acknowledged = IsShutdownAcknowledged(reply);
            _logger.LogInformation("RCON graceful shutdown '{Command}' replied: {Reply} (acknowledged: {Acknowledged})",
                command, reply, acknowledged);
            return report with { ShutdownAcknowledged = acknowledged, ShutdownReply = reply };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(ex, "RCON graceful shutdown got no reply; watching the server log for shutdown progress.");
            return report with { ShutdownReply = "no reply: " + ex.Message };
        }
    }

    /// <summary>Conan replies "Successfully executed: shutdown"; unknown commands get "Couldn't find the command".</summary>
    internal static bool IsShutdownAcknowledged(string? reply) =>
        reply is not null && reply.Contains("Successfully executed", StringComparison.OrdinalIgnoreCase);

    private async Task<ServerStopReport> WaitForGracefulExitAsync(
        IManagedProcess process,
        ServerStopReport report,
        long mark,
        TimeSpan shortWindow,
        TimeSpan extendedWindow,
        TimeSpan emergencyCeiling,
        CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var nextProbe = DateTime.MinValue;
        var exceeded = false;
        while (true)
        {
            if (process.TreeHasExited)
            {
                break;
            }

            var now = DateTime.UtcNow;
            if (report.ShutdownProgressEvidence is null && _shutdownProbe is not null && now >= nextProbe)
            {
                nextProbe = now.AddSeconds(1);
                if (_shutdownProbe.FindShutdownEvidence(mark) is { } evidence)
                {
                    _logger.LogInformation("Server shutdown in progress: {Evidence}", evidence);
                    report = report with { ShutdownProgressAt = DateTimeOffset.UtcNow, ShutdownProgressEvidence = evidence };
                }
            }

            // A proven shutdown (acknowledged or progressing) is killed only at the emergency ceiling;
            // anything else gets the short window.
            var extended = report.ShutdownAcknowledged || report.ShutdownProgressEvidence is not null;
            var elapsed = now - started;
            if (extended && !exceeded && elapsed >= extendedWindow)
            {
                exceeded = true;
                _logger.LogWarning(
                    "Graceful window {Window} exceeded while the shutdown is acknowledged ({Acknowledged}) or progressing ({Progress}); waiting up to the emergency ceiling {Ceiling}.",
                    extendedWindow, report.ShutdownAcknowledged, report.ShutdownProgressEvidence ?? "none", emergencyCeiling);
            }

            if (elapsed >= (extended ? emergencyCeiling : shortWindow))
            {
                break;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        var used = report.ShutdownAcknowledged || report.ShutdownProgressEvidence is not null;
        return report with
        {
            ExtendedWindowUsed = used,
            GracefulWindowSeconds = (int)(used ? extendedWindow : shortWindow).TotalSeconds,
            EmergencyCeilingSeconds = used ? (int)emergencyCeiling.TotalSeconds : 0,
            GracefulWindowExceeded = exceeded
        };
    }

    private void FinalizeStop(IManagedProcess process, bool expected, ServerStopReport? report = null)
    {
        lock (_sync)
        {
            var exitCode = 0;
            try
            {
                exitCode = process.ExitCode;
            }
            catch
            {
                // ignored
            }

            State.LastExitCode = exitCode;
            State.StoppedAt = DateTimeOffset.UtcNow;
            State.ProcessId = null;
            State.Status = ServerStatus.Offline;
            State.Health = HealthCheckResult.ServerOffline;
            State.CpuUsagePercent = null;
            State.WorkingSetBytes = null;
            if (!expected && exitCode != 0)
            {
                State.LastExitWasCrash = true;
            }

            if (report is not null)
            {
                State.LastStop = report with { ProcessTreeExitedAt = State.StoppedAt, ExitCode = exitCode };
            }

            if (ReferenceEquals(_process, process))
            {
                _process = null;
            }

            PublishLocked();
        }

        _ = _activityLog.AddAsync("Server", expected ? "Server stopped." : "Server process exited unexpectedly.");
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        IManagedProcess? process;
        lock (_sync)
        {
            // A requested stop is finalized by StopCoreAsync once the whole process tree is gone.
            if (_stopInProgress)
            {
                return;
            }

            process = _process;
        }

        // Already finalized, or the event belongs to a process from an earlier run.
        if (process is null || (sender is IManagedProcess source && !ReferenceEquals(source, process)))
        {
            return;
        }

        var expected = State.Status is ServerStatus.Stopping or ServerStatus.Restarting or ServerStatus.Updating;
        if (!process.TreeHasExited)
        {
            _logger.LogWarning("Server launcher PID {Pid} exited while its dedicated server child process is still running.", process.Id);
            SetStatus(ServerStatus.Error, "The server launcher exited while the dedicated server child process is still running.");
            return;
        }

        FinalizeStop(process, expected);

        if (expected || !_settings.Current.Advanced.RestartAfterCrash)
        {
            return;
        }

        HandleCrashRestart();
    }

    private void HandleCrashRestart()
    {
        var now = DateTimeOffset.UtcNow;
        var window = TimeSpan.FromMinutes(Math.Max(1, _settings.Current.Advanced.CrashRestartWindowMinutes));
        var maxAttempts = Math.Max(1, _settings.Current.Advanced.CrashRestartMaxAttempts);

        lock (_sync)
        {
            _crashTimes.Enqueue(now);
            while (_crashTimes.Count > 0 && now - _crashTimes.Peek() > window)
            {
                _crashTimes.Dequeue();
            }

            State.ConsecutiveCrashRestarts = _crashTimes.Count;
            if (_crashTimes.Count > maxAttempts)
            {
                State.CrashLoopDetected = true;
                State.Status = ServerStatus.Error;
                State.LastError = "SERVER CRASH LOOP DETECTED";
                State.LastErrorGuidance =
                    $"The dedicated server crashed {_crashTimes.Count} times in {window.TotalMinutes:0} minutes. Automatic restart is paused until you intervene.";
                PublishLocked();
                _logger.LogError("Crash loop detected. Automatic restart disabled until an administrator intervenes.");
                _ = _activityLog.AddAsync("Server", "SERVER CRASH LOOP DETECTED. Automatic restart paused.");
                return;
            }
        }

        _ = _activityLog.AddAsync("Server", "Unexpected crash detected. Attempting automatic restart.");
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                await StartAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automatic crash restart failed.");
                SetStatus(ServerStatus.Error, ex.Message);
            }
        });
    }

    private void TryAttachToExistingProcess()
    {
        try
        {
            foreach (var name in KnownProcessNames)
            {
                var processes = Process.GetProcessesByName(name);
                var existing = processes.FirstOrDefault();
                foreach (var extra in processes.Where(p => p != existing))
                {
                    extra.Dispose();
                }

                if (existing is null)
                {
                    continue;
                }

                lock (_sync)
                {
                    if (_process is { HasExited: false })
                    {
                        existing.Dispose();
                        return;
                    }

                    _process = new SystemManagedProcess(existing);
                    _process.Exited += OnProcessExited;
                    State.ProcessId = existing.Id;
                    State.Status = ServerStatus.Online;
                    State.Health = HealthCheckResult.ProcessRunning;
                    State.StartedAt ??= new DateTimeOffset(existing.StartTime);
                    PublishLocked();
                }

                _logger.LogInformation("Attached to existing Conan dedicated server process PID {Pid}.", existing.Id);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not scan for an existing Conan dedicated server process.");
        }
    }

    private string BuildLaunchArguments()
    {
        var configured = _settings.Current.ServerPaths.AdditionalArguments ?? string.Empty;
        var server = _settings.Current.Server;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            parts.Add(configured.Trim());
        }

        if (!configured.Contains("-port=", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"-port={server.GamePort}");
        }

        if (!configured.Contains("-queryport=", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"-QueryPort={server.QueryPort}");
        }

        return string.Join(' ', parts);
    }

    private async Task PumpOutputAsync(IManagedProcess process, CancellationToken cancellationToken)
    {
        try
        {
            var tasks = new List<Task>();
            if (process.StandardOutput is not null)
            {
                tasks.Add(ReadLoopAsync(process.StandardOutput, "server-out", cancellationToken));
            }

            if (process.StandardError is not null)
            {
                tasks.Add(ReadLoopAsync(process.StandardError, "server-err", cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected on stop
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Server output pump ended.");
        }
    }

    private async Task ReadLoopAsync(StreamReader reader, string source, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            _logger.LogInformation("[{Source}] {Line}", source, line);
        }
    }

    private static async Task<bool> WaitForTreeExitAsync(IManagedProcess process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.TreeHasExited)
            {
                return true;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return process.TreeHasExited;
    }

    private async Task WaitUntilReadyAsync(IManagedProcess process, CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Max(1, _settings.Current.Advanced.StartupReadyTimeoutSeconds);
        var pollMs = Math.Clamp(_settings.Current.Advanced.ReadinessPollIntervalMilliseconds, 20, 30_000);
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        string? lastError = "The dedicated server has not become ready yet.";

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                FailStartupBecauseProcessExited();
            }

            ServerReadinessResult result;
            try
            {
                result = await _readiness.ProbeAsync(
                        new ServerReadinessContext
                        {
                            ProcessId = process.Id,
                            GamePort = _settings.Current.Server.GamePort,
                            StartedAt = State.StartedAt ?? DateTimeOffset.UtcNow
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Readiness probe threw; treating as not ready and retrying.");
                result = new ServerReadinessResult
                {
                    IsReady = false,
                    Reason = "probe-exception",
                    LastError = ex.Message,
                    Health = HealthCheckResult.ServerStarting
                };
            }

            if (process.HasExited)
            {
                FailStartupBecauseProcessExited();
            }

            // Remember the -Shipping child while the launcher is alive; stop waits for (and kills) it too.
            process.TrackDescendants();
            if (result.IsReady)
            {
                lock (_sync)
                {
                    State.Status = ServerStatus.Online;
                    State.Health = HealthCheckResult.ServerOnline;
                    State.LastError = null;
                    State.LastErrorGuidance = null;
                    PublishLocked();
                }

                _logger.LogInformation("Conan dedicated server is ready: {Reason}", result.Reason);
                return;
            }

            lastError = result.LastError ?? result.Reason;
            if (DateTimeOffset.UtcNow >= deadline)
            {
                lock (_sync)
                {
                    State.Status = ServerStatus.Unresponsive;
                    State.Health = HealthCheckResult.ServerUnresponsive;
                    State.LastError = "The dedicated server process started but did not become ready in time.";
                    State.LastErrorGuidance =
                        $"Startup readiness timed out after {timeoutSeconds} seconds. The process may still be running, but it is not Online.";
                    PublishLocked();
                }

                throw new UserFacingException(
                    "Server did not become ready",
                    lastError ?? "The dedicated server did not become ready before the startup timeout.",
                    $"Waited {timeoutSeconds} seconds after process launch. The server is not Online. Inspect the dedicated-server log, then stop the process if it is stuck.");
            }

            await Task.Delay(pollMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private void FailStartupBecauseProcessExited()
    {
        SetStatus(ServerStatus.Error, "The Conan dedicated server process exited before it became ready.");
        throw new UserFacingException(
            "Server start failed",
            "The dedicated server process exited during startup.",
            "Open the server log, fix the configuration error, then start again.");
    }

    private void SampleResourceUsage(IManagedProcess process)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var cpu = process.TotalProcessorTime;
            if (_cpuSampleAt != DateTimeOffset.MinValue)
            {
                var wall = now - _cpuSampleAt;
                var cpuDelta = cpu - _cpuSampleValue;
                if (wall.TotalMilliseconds > 200)
                {
                    var percent = cpuDelta.TotalMilliseconds / (wall.TotalMilliseconds * Environment.ProcessorCount) * 100.0;
                    State.CpuUsagePercent = Math.Clamp(percent, 0, 100);
                }
            }

            _cpuSampleAt = now;
            _cpuSampleValue = cpu;
            State.WorkingSetBytes = process.WorkingSet64;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not sample process resource usage.");
        }
    }

    private void SetStatus(ServerStatus status, string? error)
    {
        lock (_sync)
        {
            State.Status = status;
            if (error is not null)
            {
                State.LastError = error;
            }

            if (status is ServerStatus.Error or ServerStatus.Unresponsive)
            {
                State.Health = HealthCheckResult.ServerUnresponsive;
            }

            PublishLocked();
        }
    }

    private void PublishLocked()
    {
        State.MaxPlayers = _settings.Current.Server.MaxPlayers;
        State.ActionInProgress = _actionGate.IsBusy;
        State.CurrentAction = _actionGate.CurrentAction;
        var snapshot = State.Clone();
        var handlers = StateChanged;
        if (handlers is null)
        {
            return;
        }

        // An observer (UI, Web, harness) must never abort a lifecycle operation such as Stop.
        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<ServerRuntimeState>>())
        {
            try
            {
                handler(this, snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A server state subscriber threw; the server operation continues.");
            }
        }
    }
}
