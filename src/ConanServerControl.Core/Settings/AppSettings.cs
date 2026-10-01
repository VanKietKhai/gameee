using ConanServerControl.Core.Models;

namespace ConanServerControl.Core.Settings;

public sealed class AppSettings
{
    public const int CurrentVersion = 1;

    public int SettingsVersion { get; set; } = CurrentVersion;

    public bool IsSetupComplete { get; set; }

    public GeneralSettings General { get; set; } = new();

    public ServerPathSettings ServerPaths { get; set; } = new();

    public SteamCmdSettings SteamCmd { get; set; } = new();

    public ServerConfigSettings Server { get; set; } = new();

    public ModSettings Mods { get; set; } = new();

    public UpdateSettings Updates { get; set; } = new();

    public BackupSettings Backups { get; set; } = new();

    public WebAdminSettings WebAdmin { get; set; } = new();

    public RconSettings Rcon { get; set; } = new();

    public AdvancedSettings Advanced { get; set; } = new();
}

public sealed class GeneralSettings
{
    public string Language { get; set; } = "en";

    public bool StartManagerWithWindows { get; set; }

    public bool StartServerWhenManagerLaunches { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public bool CheckForManagerUpdates { get; set; }
}

public sealed class ServerPathSettings
{
    public string? ServerExecutablePath { get; set; }

    public string? ServerWorkingDirectory { get; set; }

    public string? ServerInstallDirectory { get; set; }

    public string AdditionalArguments { get; set; } = "-log";
}

public sealed class SteamCmdSettings
{
    public string? InstallDirectory { get; set; }

    public bool UseAnonymousLogin { get; set; } = true;

    public string? SteamUsername { get; set; }

    public bool ValidateAfterUpdate { get; set; } = true;
}

public sealed class ServerConfigSettings
{
    public string ServerName { get; set; } = "Conan Dedicated Server";

    public int MaxPlayers { get; set; } = AppConstants.DefaultMaxPlayers;

    public int GamePort { get; set; } = AppConstants.DefaultGamePort;

    public int QueryPort { get; set; } = AppConstants.DefaultQueryPort;

    /// <summary>
    /// Legacy RCON port copied from older settings JSON. Runtime code must use
    /// <see cref="RconSettings.Port"/>. Kept only so existing files still deserialize.
    /// </summary>
    [Obsolete("Use AppSettings.Rcon.Port. Retained for backward-compatible JSON deserialization.")]
    public int RconPort { get; set; } = AppConstants.DefaultRconPort;

    public bool HasServerPassword { get; set; }
}

public sealed class ModSettings
{
    public List<WorkshopMod> Mods { get; set; } = new();

    public bool WarnOnLoadOrderChange { get; set; } = true;

    public bool BackupBeforeModChanges { get; set; } = true;
}

public sealed class UpdateSettings
{
    public UpdateCheckInterval CheckInterval { get; set; } = UpdateCheckInterval.Minutes30;

    public AutomationMode AutomationMode { get; set; } = AutomationMode.Manual;

    public bool WarnPlayersBeforeRestart { get; set; } = true;

    public int WarningLeadMinutes { get; set; } = 10;

    public string? MaintenanceWindowStart { get; set; }

    public string? MaintenanceWindowEnd { get; set; }

    public bool WaitUntilEmptyWhenPlayersOnline { get; set; } = true;
}

public sealed class BackupSettings
{
    public BackupInterval Interval { get; set; } = BackupInterval.Hours6;

    public BackupKeepLatest KeepLatest { get; set; } = BackupKeepLatest.Ten;

    public int? KeepDays { get; set; } = 14;

    public bool BackupBeforeServerUpdate { get; set; } = true;

    public bool BackupBeforeModUpdate { get; set; } = true;

    public bool BackupBeforeModRemoval { get; set; } = true;

    public bool BackupBeforeLoadOrderChange { get; set; } = true;
}

public sealed class WebAdminSettings
{
    public bool Enabled { get; set; }

    public int Port { get; set; } = AppConstants.DefaultWebAdminPort;

    public WebBindMode BindMode { get; set; } = WebBindMode.LocalhostOnly;

    public string? CustomBindAddress { get; set; }

    public string Username { get; set; } = "admin";

    public int SessionMinutes { get; set; } = 30;

    public int MaxLoginAttempts { get; set; } = 5;

    public int LoginLockoutMinutes { get; set; } = 15;
}

public sealed class RconSettings
{
    public bool Enabled { get; set; } = true;

    public int Port { get; set; } = AppConstants.DefaultRconPort;

    public int TimeoutSeconds { get; set; } = 5;
}

public sealed class AdvancedSettings
{
    public int GracefulStopTimeoutSeconds { get; set; } = AppConstants.DefaultGracefulStopTimeoutSeconds;

    public int ForceStopTimeoutSeconds { get; set; } = AppConstants.DefaultForceStopTimeoutSeconds;

    public bool RestartAfterCrash { get; set; } = true;

    public int CrashRestartMaxAttempts { get; set; } = AppConstants.CrashRestartMaxAttempts;

    public int CrashRestartWindowMinutes { get; set; } = AppConstants.CrashRestartWindowMinutes;

    public int HealthCheckIntervalSeconds { get; set; } = 5;

    public bool QueryEnabled { get; set; }

    /// <summary>
    /// Maximum time to wait after process launch for the readiness probe.
    /// Timeout must not report the server Online.
    /// </summary>
    public int StartupReadyTimeoutSeconds { get; set; } = AppConstants.DefaultStartupReadyTimeoutSeconds;

    public int ReadinessPollIntervalMilliseconds { get; set; } = AppConstants.DefaultReadinessPollIntervalMilliseconds;
}

/// <summary>
/// Secrets stored separately and protected with DPAPI on Windows.
/// </summary>
public sealed class ProtectedSecrets
{
    public string? WebAdminPasswordHash { get; set; }

    public string? RconPassword { get; set; }

    public string? ServerPassword { get; set; }

    public string? AdminPassword { get; set; }

    public string? SteamPassword { get; set; }
}
