namespace ConanServerControl.Core;

/// <summary>
/// Steam and Conan Exiles identifiers. Keep App IDs out of UI code.
/// </summary>
public static class AppConstants
{
    public const string ApplicationName = "Conan Server Control";
    public const string ApplicationFolderName = "ConanServerControl";
    public const string ExecutableName = "ConanServerControl.exe";

    /// <summary>Conan Exiles client (Workshop host app).</summary>
    public const int ConanExilesAppId = 440900;

    /// <summary>Conan Exiles Dedicated Server.</summary>
    public const int ConanDedicatedServerAppId = 443030;

    public const string DedicatedServerProcessName = "ConanSandboxServer";
    public const string DedicatedServerShippingProcessName = "ConanSandboxServer-Win64-Shipping";
    public const string DedicatedServerExecutable = "ConanSandboxServer.exe";
    public const string DedicatedServerShippingExecutable = "ConanSandboxServer-Win64-Shipping.exe";

    public const string DefaultServerSubPath = @"ConanSandbox\Binaries\Win64\ConanSandboxServer.exe";
    public const string ModsDirectoryRelative = @"ConanSandbox\Mods";
    public const string ModListFileName = "modlist.txt";
    public const string SavedRelative = @"ConanSandbox\Saved";
    public const string ConfigRelative = @"ConanSandbox\Saved\Config\WindowsServer";

    /// <summary>
    /// Legacy single-file relative path. Prefer <see cref="Backups.ConanWorldFiles"/>
    /// which also covers Enhanced <c>game_0.db</c> and WAL/SHM siblings.
    /// </summary>
    public const string GameDbRelative = @"ConanSandbox\Saved\game.db";

    public const string SteamCmdExecutableWindows = "steamcmd.exe";
    public const string SteamCmdZipUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
    public const string SteamPublishedFileDetailsUrl = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";

    public const int DefaultGamePort = 7777;
    public const int DefaultQueryPort = 27015;
    public const int DefaultRconPort = 25575;
    public const int DefaultWebAdminPort = 8080;
    public const int DefaultMaxPlayers = 10;

    /// <summary>
    /// Extended graceful-stop window, used only once the shutdown is acknowledged by RCON or
    /// shutdown progress is seen in the server log. M3 live: a clean RCON shutdown of the Enhanced
    /// dedicated server took ~57-65 s after short runs, but 125-170 s after ~11 minutes online
    /// (teardown starts immediately; the exit sequence then goes quiet for minutes). 30 s and 120 s
    /// both force-killed it mid-exit and left game_0.db-wal behind.
    /// </summary>
    public const int DefaultGracefulStopTimeoutSeconds = 300;

    /// <summary>
    /// Hard emergency ceiling for a shutdown that is proven to be progressing (acknowledged by RCON or
    /// visible in the server log). Passing the graceful window alone no longer force-kills such a stop:
    /// on 2026-10-03, with a heavily loaded host, the exit sequence went quiet for ~288 s and two clean
    /// shutdowns were killed at 300 s, leaving game_0.db-wal behind. The process tree is killed only
    /// at this ceiling. Batch timing is judged separately (240 s = HIGH RISK in the live harness).
    /// </summary>
    public const int DefaultEmergencyStopCeilingSeconds = 600;

    /// <summary>
    /// Graceful-stop window when the shutdown was not acknowledged and no shutdown progress is seen
    /// (RCON unavailable, command rejected, server hung). Waiting the extended window here would only
    /// delay the force-kill fallback.
    /// </summary>
    public const int DefaultUnacknowledgedStopTimeoutSeconds = 30;

    /// <summary>
    /// RCON command that shuts the dedicated server down cleanly. Live-verified on
    /// build ++exiles+release-beta-CL-378132: listed by the server's RCON "help",
    /// replies "Successfully executed: shutdown", exits with code 0 and leaves no
    /// game_0.db-wal/-shm. "DoExit" and "exit" are not recognised by that build.
    /// </summary>
    public const string DefaultRconShutdownCommand = "shutdown";
    public const int DefaultForceStopTimeoutSeconds = 10;
    public const int CrashRestartMaxAttempts = 3;
    public const int CrashRestartWindowMinutes = 10;

    public const int DefaultStartupReadyTimeoutSeconds = 600;
    public const int DefaultReadinessPollIntervalMilliseconds = 1000;

    public const string WebAdminCookieName = "csc_session";
    public const string AuditActorSystem = "System";
}
