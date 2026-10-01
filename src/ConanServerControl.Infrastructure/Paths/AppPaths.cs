using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;

namespace ConanServerControl.Infrastructure.Paths;

public sealed class AppPaths : IAppPaths
{
    public const string DataDirectoryOverrideVariable = "CONAN_SERVER_CONTROL_DATA";

    public AppPaths(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? ResolveDefaultDataDirectory();
        LogsDirectory = Path.Combine(DataDirectory, "logs");
        BackupsDirectory = Path.Combine(DataDirectory, "backups");
        SettingsFilePath = Path.Combine(DataDirectory, "settings.json");
        SecretsFilePath = Path.Combine(DataDirectory, "secrets.bin");
        DatabaseFilePath = Path.Combine(DataDirectory, "control.db");
        SteamCmdDefaultDirectory = Path.Combine(DataDirectory, "steamcmd");
        StagingDirectory = Path.Combine(DataDirectory, "staging");
    }

    public string DataDirectory { get; }

    public string LogsDirectory { get; }

    public string BackupsDirectory { get; }

    public string SettingsFilePath { get; }

    public string SecretsFilePath { get; }

    public string DatabaseFilePath { get; }

    public string SteamCmdDefaultDirectory { get; }

    public string StagingDirectory { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(SteamCmdDefaultDirectory);
        Directory.CreateDirectory(StagingDirectory);
        Directory.CreateDirectory(Path.Combine(DataDirectory, "mods-cache"));
    }

    public static string ResolveDefaultDataDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable(DataDirectoryOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        if (OperatingSystem.IsWindows())
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrWhiteSpace(programData))
            {
                programData = @"C:\ProgramData";
            }

            return Path.Combine(programData, AppConstants.ApplicationFolderName);
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
        {
            local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Path.Combine(local, AppConstants.ApplicationFolderName);
    }
}
