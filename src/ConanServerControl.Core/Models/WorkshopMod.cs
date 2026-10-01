namespace ConanServerControl.Core.Models;

public sealed class WorkshopMod
{
    public long WorkshopId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? LocalFileName { get; set; }

    public bool Enabled { get; set; } = true;

    public int LoadOrder { get; set; }

    public string? InstalledVersion { get; set; }

    public DateTimeOffset? InstalledTimestamp { get; set; }

    public string? LatestWorkshopVersion { get; set; }

    public DateTimeOffset? LatestWorkshopTimestamp { get; set; }

    public bool UpdateAvailable { get; set; }

    public DateTimeOffset? LastChecked { get; set; }

    public string? Error { get; set; }

    public bool IsPlaceholder { get; set; }
}

public sealed class BackupRecord
{
    public string Id { get; set; } = string.Empty;

    public string DirectoryPath { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string Reason { get; set; } = "manual";

    public long SizeBytes { get; set; }

    public string? Notes { get; set; }

    public bool IncludesWorld { get; set; }

    public bool IncludesConfig { get; set; }

    public bool IncludesModList { get; set; }

    /// <summary>True only when copy, manifest, hashes, and SQLite verification all succeeded.</summary>
    public bool Succeeded { get; set; }

    public bool ManifestWritten { get; set; }

    public bool HashesVerified { get; set; }

    public bool SqliteVerified { get; set; }

    /// <summary><c>Enhanced</c> or <c>Legacy</c> when a known main DB was captured.</summary>
    public string? WorldType { get; set; }

    public string? MainDbFileName { get; set; }

    public List<BackupWorldFileRecord> WorldFiles { get; set; } = new();

    public string? VerificationDetail { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }
}

public sealed class BackupWorldFileRecord
{
    public string LogicalName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;
}

public sealed class PipelineProgress
{
    public UpdatePipelineState State { get; set; } = UpdatePipelineState.Idle;

    public int CurrentStep { get; set; }

    public int TotalSteps { get; set; }

    public string? StepDescription { get; set; }

    public string? Error { get; set; }

    public bool IsActive => State is not UpdatePipelineState.Idle
        and not UpdatePipelineState.Completed
        and not UpdatePipelineState.Failed
        and not UpdatePipelineState.Cancelled;
}

public enum UpdatePipelineState
{
    Idle = 0,
    Checking = 1,
    Backup = 2,
    NotifyingPlayers = 3,
    Stopping = 4,
    UpdatingServer = 5,
    UpdatingMods = 6,
    Validating = 7,
    Starting = 8,
    HealthCheck = 9,
    Completed = 10,
    Failed = 11,
    Cancelled = 12
}

public enum RestartDelay
{
    Immediate = 0,
    Minutes5 = 5,
    Minutes10 = 10,
    Minutes15 = 15,
    Minutes30 = 30,
    Minutes60 = 60
}

public sealed class DelayedRestartRequest
{
    public RestartDelay Delay { get; set; }

    public bool BackupFirst { get; set; } = true;

    public bool UpdateServer { get; set; }

    public bool UpdateMods { get; set; }

    public string? Reason { get; set; }
}
