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

    public const int DefaultGracefulStopTimeoutSeconds = 30;
    public const int DefaultForceStopTimeoutSeconds = 10;
    public const int CrashRestartMaxAttempts = 3;
    public const int CrashRestartWindowMinutes = 10;

    public const string WebAdminCookieName = "csc_session";
    public const string AuditActorSystem = "System";
}
