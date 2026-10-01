using ConanServerControl.Core.Models;

namespace ConanServerControl.Core.Abstractions;

public interface IServerProcessManager
{
    ServerRuntimeState State { get; }

    event EventHandler<ServerRuntimeState>? StateChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(bool force = false, CancellationToken cancellationToken = default);

    Task RestartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the process when the caller already holds <see cref="IServerActionGate"/>.
    /// </summary>
    Task StartUnderLockAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the process when the caller already holds <see cref="IServerActionGate"/>.
    /// </summary>
    Task StopUnderLockAsync(bool force = false, CancellationToken cancellationToken = default);

    bool IsConanServerProcess(string processName);

    Task RefreshAsync(CancellationToken cancellationToken = default);
}

public interface ISteamCmdService
{
    string? ExecutablePath { get; }

    bool IsInstalled { get; }

    Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    Task UpdateAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    Task<bool> ValidateAsync(CancellationToken cancellationToken = default);

    Task<ProcessExecutionResult> InstallOrUpdateDedicatedServerAsync(
        string installDirectory,
        bool validate,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ProcessExecutionResult> DownloadWorkshopItemAsync(
        long workshopId,
        string installDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IServerUpdateService
{
    Task<ServerUpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(bool restartAfter, IProgress<PipelineProgress>? progress = null, CancellationToken cancellationToken = default);

    Task UpdateModsAsync(bool restartAfter, IProgress<PipelineProgress>? progress = null, CancellationToken cancellationToken = default);

    Task UpdateEverythingAsync(IProgress<PipelineProgress>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class ServerUpdateCheckResult
{
    public string? InstalledBuild { get; init; }

    public string? AvailableBuild { get; init; }

    public bool UpdateAvailable { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public interface IWorkshopModService
{
    IReadOnlyList<WorkshopMod> Mods { get; }

    Task AddAsync(long workshopId, CancellationToken cancellationToken = default);

    Task RemoveAsync(long workshopId, bool confirmed, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(long workshopId, bool enabled, CancellationToken cancellationToken = default);

    Task MoveAsync(long workshopId, int newIndex, CancellationToken cancellationToken = default);

    Task UpdateAsync(long workshopId, CancellationToken cancellationToken = default);

    Task UpdateAllAsync(CancellationToken cancellationToken = default);

    Task CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    string GetShareableModList();
}

public sealed class WorkshopPublishedFileDetails
{
    public long WorkshopId { get; init; }

    public string Title { get; init; } = string.Empty;

    public DateTimeOffset TimeUpdated { get; init; }

    public string? FileName { get; init; }
}

public interface ISteamWorkshopClient
{
    Task<IReadOnlyList<WorkshopPublishedFileDetails>> GetPublishedFileDetailsAsync(
        IReadOnlyList<long> workshopIds,
        CancellationToken cancellationToken = default);
}

public interface IBackupService
{
    Task<BackupRecord> BackupNowAsync(string reason, CancellationToken cancellationToken = default);

    Task RestoreAsync(string backupId, bool startAfter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task ApplyRetentionAsync(CancellationToken cancellationToken = default);
}

public interface IRconService
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync();

    Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default);

    Task AnnounceAsync(string message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerInfo>> GetPlayersAsync(CancellationToken cancellationToken = default);
}

public interface IServerHealthService
{
    Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}

public interface IDelayedRestartService
{
    bool IsCountdownActive { get; }

    TimeSpan? Remaining { get; }

    Task StartAsync(DelayedRestartRequest request, CancellationToken cancellationToken = default);

    Task CancelAsync();
}

public interface IInstallDetector
{
    DetectedInstalls Detect();
}

public sealed class DetectedInstalls
{
    public string? SteamDirectory { get; init; }

    public string? SteamCmdDirectory { get; init; }

    public string? DedicatedServerDirectory { get; init; }

    public string? DedicatedServerExecutable { get; init; }

    public string? ClientDirectory { get; init; }

    public string? WorkshopDirectory { get; init; }

    public string? ModsDirectory { get; init; }

    public string? ExistingModListPath { get; init; }

    public string? ExistingServerConfigDirectory { get; init; }
}

public interface INetworkInfoService
{
    string? GetLanIPv4();

    string? GetTailscaleIPv4();

    string GetWebAdminUrl(string bindAddress, int port);
}

public interface IFeatureStatus
{
    bool IsImplemented { get; }

    string FeatureName { get; }

    string StatusMessage { get; }
}
