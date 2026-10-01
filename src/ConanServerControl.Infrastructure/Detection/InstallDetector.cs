using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;
using Microsoft.Win32;

namespace ConanServerControl.Infrastructure.Detection;

public sealed class InstallDetector : IInstallDetector
{
    public DetectedInstalls Detect()
    {
        var steam = DetectSteamDirectory();
        var steamCmd = DetectSteamCmd(steam);
        var dedicated = DetectDedicatedServer(steam);
        var client = DetectClient(steam);
        string? workshop = null;
        string? mods = null;
        string? modlist = null;
        string? config = null;

        if (!string.IsNullOrWhiteSpace(client))
        {
            workshop = Path.Combine(steam ?? string.Empty, "steamapps", "workshop", "content", AppConstants.ConanExilesAppId.ToString());
            if (!Directory.Exists(workshop))
            {
                workshop = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(dedicated))
        {
            mods = Path.Combine(dedicated, "ConanSandbox", "Mods");
            var list = Path.Combine(mods, AppConstants.ModListFileName);
            if (File.Exists(list))
            {
                modlist = list;
            }

            var cfg = Path.Combine(dedicated, AppConstants.ConfigRelative);
            if (Directory.Exists(cfg))
            {
                config = cfg;
            }
        }

        return new DetectedInstalls
        {
            SteamDirectory = steam,
            SteamCmdDirectory = steamCmd,
            DedicatedServerDirectory = dedicated,
            DedicatedServerExecutable = dedicated is null ? null : FindServerExecutable(dedicated),
            ClientDirectory = client,
            WorkshopDirectory = workshop,
            ModsDirectory = Directory.Exists(mods ?? "") ? mods : null,
            ExistingModListPath = modlist,
            ExistingServerConfigDirectory = config
        };
    }

    private static string? DetectSteamDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var path = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }
        catch
        {
            // ignored
        }

        var guesses = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam"
        };
        return guesses.FirstOrDefault(Directory.Exists);
    }

    private static string? DetectSteamCmd(string? steam)
    {
        var guesses = new List<string>();
        if (!string.IsNullOrWhiteSpace(steam))
        {
            guesses.Add(Path.Combine(steam, "steamcmd"));
        }

        guesses.Add(@"C:\ConanServerControl\steamcmd");
        guesses.Add(@"C:\steamcmd");
        return guesses.FirstOrDefault(dir => File.Exists(Path.Combine(dir, AppConstants.SteamCmdExecutableWindows)));
    }

    private static string? DetectDedicatedServer(string? steam)
    {
        var guesses = new List<string>();
        if (!string.IsNullOrWhiteSpace(steam))
        {
            guesses.Add(Path.Combine(steam, "steamapps", "common", "Conan Exiles Dedicated Server"));
        }

        guesses.Add(@"C:\ConanServer");
        guesses.Add(@"C:\Servers\ConanExiles");
        return guesses.FirstOrDefault(dir => FindServerExecutable(dir) is not null);
    }

    private static string? DetectClient(string? steam)
    {
        if (string.IsNullOrWhiteSpace(steam))
        {
            return null;
        }

        var dir = Path.Combine(steam, "steamapps", "common", "Conan Exiles");
        return Directory.Exists(dir) ? dir : null;
    }

    private static string? FindServerExecutable(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var candidates = new[]
        {
            Path.Combine(directory, AppConstants.DefaultServerSubPath),
            Path.Combine(directory, "ConanSandbox", "Binaries", "Win64", AppConstants.DedicatedServerShippingExecutable),
            Path.Combine(directory, AppConstants.DedicatedServerExecutable)
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
