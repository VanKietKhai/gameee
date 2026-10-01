using System.Diagnostics;
using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;
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
    private readonly object _sync = new();

    private IManagedProcess? _process;
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
        ILogger<ServerProcessManager> logger)
    {
        _settings = settings;
        _processStarter = processStarter;
        _actionGate = actionGate;
        _activityLog = activityLog;
        _rcon = rcon;
        _logger = logger;
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

    public Task StartUnderLockAsync(CancellationToken cancellationToken = default)
    {
        EnsureLockHeld("start");
        return StartCoreAsync(cancellationToken);
    }

    public Task StopUnderLockAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        EnsureLockHeld("stop");
        return StopCoreAsync(force, expected: true, cancellationToken);
    }

    public void Dispose()
    {
        _monitorCts?.Cancel();
        _process?.Dispose();
        _monitorCts?.Dispose();
    }

    private void EnsureLockHeld(string operation)
    {
        if (_actionGate.IsBusy)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Cannot {operation} under lock because no server action lease is held. Use the public Start/Stop methods instead.");
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var paths = _settings.Current.ServerPaths;
        var exe = paths.ServerExecutablePath;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
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

        var fileName = Path.GetFileName(exe);
        if (!IsConanServerProcess(fileName))
        {
            _logger.LogWarning("Starting {Exe} which does not match the expected Conan dedicated server process names.", exe);
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

        await _activityLog.AddAsync("Server", $"Server started (PID {process.Id}).", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Dedicated servers typically take time to bind ports. Mark Online once the process stays alive briefly.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        lock (_sync)
        {
            if (_process is { HasExited: false })
            {
                State.Status = ServerStatus.Online;
                State.Health = HealthCheckResult.ServerStarting;
                PublishLocked();
            }
        }

        await _activityLog.AddAsync("Server", "Server process is running.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task StopCoreAsync(bool force, bool expected, CancellationToken cancellationToken)
    {
        IManagedProcess? process;
        lock (_sync)
        {
            process = _process;
            if (process is null || process.HasExited)
            {
                State.Status = ServerStatus.Offline;
                State.Health = HealthCheckResult.ServerOffline;
                State.ProcessId = null;
                PublishLocked();
                return;
            }

            State.Status = ServerStatus.Stopping;
            PublishLocked();
        }

        await _activityLog.AddAsync("Server", force ? "Force stop requested." : "Graceful stop requested.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var gracefulTimeout = TimeSpan.FromSeconds(Math.Max(5, _settings.Current.Advanced.GracefulStopTimeoutSeconds));
        var forceTimeout = TimeSpan.FromSeconds(Math.Max(3, _settings.Current.Advanced.ForceStopTimeoutSeconds));

        if (!force)
        {
            await TryGracefulShutdownAsync(cancellationToken).ConfigureAwait(false);
            if (await WaitForExitAsync(process, gracefulTimeout, cancellationToken).ConfigureAwait(false))
            {
                FinalizeStop(process, expected);
                return;
            }

            _logger.LogWarning("Graceful stop timed out after {Timeout}. Attempting CloseMainWindow.", gracefulTimeout);
            process.CloseMainWindow();
            if (await WaitForExitAsync(process, TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false))
            {
                FinalizeStop(process, expected);
                return;
            }
        }

        _logger.LogWarning("Forcing Conan dedicated server process {Pid} to terminate.", process.Id);
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Force terminate failed for PID {Pid}.", process.Id);
        }

        await WaitForExitAsync(process, forceTimeout, cancellationToken).ConfigureAwait(false);
        FinalizeStop(process, expected);
    }

    private async Task TryGracefulShutdownAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_settings.Current.Rcon.Enabled || string.IsNullOrWhiteSpace(_settings.Secrets.RconPassword))
            {
                return;
            }

            await _rcon.AnnounceAsync("Server is shutting down.", cancellationToken).ConfigureAwait(false);
            await _rcon.SendCommandAsync("DoExit", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "RCON graceful shutdown is unavailable; falling back to process stop.");
        }
    }

    private void FinalizeStop(IManagedProcess process, bool expected)
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
        var expected = State.Status is ServerStatus.Stopping or ServerStatus.Restarting or ServerStatus.Updating;
        IManagedProcess? process;
        lock (_sync)
        {
            process = _process;
        }

        if (process is not null)
        {
            FinalizeStop(process, expected);
        }

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

    private static async Task<bool> WaitForExitAsync(IManagedProcess process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                return true;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return process.HasExited;
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

            if (status == ServerStatus.Error)
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
        StateChanged?.Invoke(this, snapshot);
    }
}
