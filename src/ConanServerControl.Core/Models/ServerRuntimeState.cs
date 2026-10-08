namespace ConanServerControl.Core.Models;

public sealed class ServerRuntimeState
{
    public ServerStatus Status { get; set; } = ServerStatus.Offline;

    public HealthCheckResult Health { get; set; } = HealthCheckResult.ServerOffline;

    public int? ProcessId { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? StoppedAt { get; set; }

    public int? LastExitCode { get; set; }

    public bool LastExitWasCrash { get; set; }

    public string? LastError { get; set; }

    public string? LastErrorGuidance { get; set; }

    public TimeSpan? Uptime =>
        StartedAt is null || Status is ServerStatus.Offline or ServerStatus.Error
            ? null
            : DateTimeOffset.UtcNow - StartedAt.Value;

    public int PlayerCount { get; set; }

    public int MaxPlayers { get; set; } = AppConstants.DefaultMaxPlayers;

    public IReadOnlyList<PlayerInfo> Players { get; set; } = Array.Empty<PlayerInfo>();

    public double? CpuUsagePercent { get; set; }

    public long? WorkingSetBytes { get; set; }

    public string? InstalledBuild { get; set; }

    public string? AvailableBuild { get; set; }

    public bool ServerUpdateAvailable { get; set; }

    public string? ClientInstalledBuild { get; set; }

    public string? ClientAvailableBuild { get; set; }

    public bool ClientUpdateAvailable { get; set; }

    public int InstalledModCount { get; set; }

    public int ModsRequiringUpdate { get; set; }

    public DateTimeOffset? LastBackupAt { get; set; }

    public DateTimeOffset? LastUpdateCheckAt { get; set; }

    public string? CurrentAction { get; set; }

    public bool ActionInProgress { get; set; }

    public int ConsecutiveCrashRestarts { get; set; }

    public bool CrashLoopDetected { get; set; }

    /// <summary>What the last stop actually did (null until a stop finishes or fails).</summary>
    public ServerStopReport? LastStop { get; set; }

    public ServerRuntimeState Clone()
    {
        return new ServerRuntimeState
        {
            Status = Status,
            Health = Health,
            ProcessId = ProcessId,
            StartedAt = StartedAt,
            StoppedAt = StoppedAt,
            LastExitCode = LastExitCode,
            LastExitWasCrash = LastExitWasCrash,
            LastError = LastError,
            LastErrorGuidance = LastErrorGuidance,
            PlayerCount = PlayerCount,
            MaxPlayers = MaxPlayers,
            Players = Players.ToArray(),
            CpuUsagePercent = CpuUsagePercent,
            WorkingSetBytes = WorkingSetBytes,
            InstalledBuild = InstalledBuild,
            AvailableBuild = AvailableBuild,
            ServerUpdateAvailable = ServerUpdateAvailable,
            ClientInstalledBuild = ClientInstalledBuild,
            ClientAvailableBuild = ClientAvailableBuild,
            ClientUpdateAvailable = ClientUpdateAvailable,
            InstalledModCount = InstalledModCount,
            ModsRequiringUpdate = ModsRequiringUpdate,
            LastBackupAt = LastBackupAt,
            LastUpdateCheckAt = LastUpdateCheckAt,
            CurrentAction = CurrentAction,
            ActionInProgress = ActionInProgress,
            ConsecutiveCrashRestarts = ConsecutiveCrashRestarts,
            CrashLoopDetected = CrashLoopDetected,
            LastStop = LastStop
        };
    }
}

/// <summary>Immutable record of one stop: what was sent, what was observed, and how it ended.</summary>
public sealed record ServerStopReport
{
    public DateTimeOffset RequestedAt { get; init; }

    public bool ForceRequested { get; init; }

    public string? ShutdownCommand { get; init; }

    public DateTimeOffset? ShutdownSentAt { get; init; }

    /// <summary>The server replied that the shutdown command executed.</summary>
    public bool ShutdownAcknowledged { get; init; }

    public string? ShutdownReply { get; init; }

    public DateTimeOffset? ShutdownProgressAt { get; init; }

    /// <summary>First server-log line showing the shutdown in progress, if any was seen.</summary>
    public string? ShutdownProgressEvidence { get; init; }

    /// <summary>True when the extended window applied (acknowledged or progress observed).</summary>
    public bool ExtendedWindowUsed { get; init; }

    public int GracefulWindowSeconds { get; init; }

    /// <summary>Hard ceiling that applied to this stop (0 when the short unacknowledged window applied).</summary>
    public int EmergencyCeilingSeconds { get; init; }

    /// <summary>The server was still exiting when the graceful window ran out, and the stop kept waiting.</summary>
    public bool GracefulWindowExceeded { get; init; }

    public bool ForcedKill { get; init; }

    /// <summary>When the whole managed process tree was gone. Null if it was still running.</summary>
    public DateTimeOffset? ProcessTreeExitedAt { get; init; }

    public int? ExitCode { get; init; }
}

public sealed class PlayerInfo
{
    public string Name { get; set; } = string.Empty;

    public string? SteamId { get; set; }

    public TimeSpan? SessionDuration { get; set; }
}

public sealed class DiagnosticsSnapshot
{
    public string ApplicationDataDirectory { get; set; } = string.Empty;

    public string LogsDirectory { get; set; } = string.Empty;

    public string BackupsDirectory { get; set; } = string.Empty;

    public string SettingsFilePath { get; set; } = string.Empty;

    public string SteamCmdPath { get; set; } = string.Empty;

    public bool SteamCmdExists { get; set; }

    public string ConanServerExecutablePath { get; set; } = string.Empty;

    public bool ConanServerExecutableExists { get; set; }

    public string ConanServerWorkingDirectory { get; set; } = string.Empty;

    public bool ConanServerWorkingDirectoryExists { get; set; }

    public ServerStatus ServerStatus { get; set; }

    public int? ProcessId { get; set; }

    public string OperatingSystem { get; set; } = string.Empty;

    public string Runtime { get; set; } = string.Empty;

    public string? TailscaleIPv4 { get; set; }

    public string WebAdminUrl { get; set; } = string.Empty;
}

public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; init; }

    public string Level { get; init; } = "Information";

    public string Source { get; init; } = "App";

    public string Message { get; init; } = string.Empty;
}

public sealed class ActivityLogEntry
{
    public int Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string Category { get; set; } = "General";

    public string Message { get; set; } = string.Empty;

    public string? Actor { get; set; }

    public string? Details { get; set; }
}

public sealed class ProcessExecutionResult
{
    public int ExitCode { get; init; }

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;

    public TimeSpan Duration { get; init; }

    public bool TimedOut { get; init; }

    public bool Succeeded => !TimedOut && ExitCode == 0;
}
