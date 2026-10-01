namespace ConanServerControl.Core.Models;

public enum ServerStatus
{
    Offline = 0,
    Starting = 1,
    Online = 2,
    Stopping = 3,
    Updating = 4,
    Restarting = 5,
    Error = 6,
    Unresponsive = 7
}

public enum HealthCheckResult
{
    ProcessRunning,
    ServerStarting,
    ServerOnline,
    ServerUnresponsive,
    ServerOffline
}

public enum AutomationMode
{
    Manual = 0,
    Scheduled = 1,
    Automatic = 2
}

public enum WebBindMode
{
    LocalhostOnly = 0,
    Lan = 1,
    Custom = 2
}

public enum UpdateCheckInterval
{
    Disabled = 0,
    Minutes15 = 15,
    Minutes30 = 30,
    Hours1 = 60,
    Hours3 = 180,
    Hours6 = 360,
    Hours12 = 720,
    Hours24 = 1440
}

public enum BackupInterval
{
    Disabled = 0,
    Hours1 = 60,
    Hours3 = 180,
    Hours6 = 360,
    Hours12 = 720,
    Hours24 = 1440
}

public enum BackupKeepLatest
{
    Five = 5,
    Ten = 10,
    Twenty = 20,
    Fifty = 50,
    Hundred = 100
}
