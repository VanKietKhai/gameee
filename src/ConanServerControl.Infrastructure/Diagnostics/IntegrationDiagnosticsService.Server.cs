using System.Text.RegularExpressions;
using ConanServerControl.Core;
using ConanServerControl.Core.Diagnostics;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;

namespace ConanServerControl.Infrastructure.Diagnostics;

public sealed partial class IntegrationDiagnosticsService
{
    private static readonly Regex AcfValue = new(
        "\"(?<key>buildid|StateFlags|installdir)\"\\s+\"(?<value>[^\"]*)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private DiagnosticCheckResult CheckServerExecutable(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ServerExecutable;
        const string name = "Dedicated server executable";
        var exe = NullIfBlank(context.Settings.ServerPaths.ServerExecutablePath);
        var kind = ConanExecutableClassifier.Classify(exe);
        var facts = new Dictionary<string, string>
        {
            ["ConfiguredPath"] = exe ?? string.Empty,
            ["Classification"] = kind.ToString(),
            ["Started"] = No
        };

        if (exe is null)
        {
            var expected = context.InstallDirectory is null
                ? null
                : Path.Combine(context.InstallDirectory, AppConstants.DefaultServerSubPath);
            if (expected is not null)
            {
                facts["ExpectedPath"] = expected;
                facts["ExpectedPathExists"] = File.Exists(expected) ? Yes : No;
            }

            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.NotConfigured,
                "Dedicated server executable is not configured.",
                details: expected is null ? null : $"Expected after install: {expected}",
                action: "Set the ConanSandboxServer.exe path in Settings, or configure an install directory for the live install.",
                facts: facts);
        }

        if (kind == ConanExecutableKind.ClientLauncherBatch)
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "Run Me!.bat is the standalone CLIENT launcher, not the dedicated server.",
                details: exe,
                action: "Select ConanSandboxServer.exe from the dedicated server install (SteamCMD app 443030).",
                facts: facts);
        }

        if (kind == ConanExecutableKind.StandaloneClient)
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "ConanSandbox.exe is the Conan game CLIENT, not the dedicated server.",
                details: exe,
                action: "Select ConanSandboxServer.exe from the dedicated server install (SteamCMD app 443030).",
                facts: facts);
        }

        if (kind == ConanExecutableKind.BatchOrScript)
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "A script/shortcut is configured as the dedicated server executable.",
                details: exe,
                action: "Select ConanSandboxServer.exe directly; scripts cannot be tracked or stopped safely.",
                facts: facts);
        }

        if (!PathValidator.IsSafeAbsolutePath(exe))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "Dedicated server executable path is not a safe absolute path.",
                details: exe, action: "Choose the executable with Browse in Settings.", facts: facts);
        }

        if (context.ClientRoot is not null && IsSameOrUnder(exe, context.ClientRoot))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "Dedicated server executable is inside the standalone client folder.",
                details: $"Executable: {exe}{Environment.NewLine}Client root: {context.ClientRoot}",
                action: "Keep the dedicated server install separate from the standalone client.",
                facts: facts);
        }

        if (!ConanExecutableClassifier.IsDedicatedServer(kind))
        {
            facts["Exists"] = File.Exists(exe) ? Yes : No;
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Warning,
                $"Unexpected executable name '{Path.GetFileName(exe)}'. Expected {AppConstants.DedicatedServerExecutable}.",
                DiagnosticEvidence.FilesystemInspected,
                details: exe,
                action: "Confirm this is the Conan dedicated server executable.",
                facts: facts);
        }

        if (!File.Exists(exe))
        {
            facts["Exists"] = No;
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Warning,
                "Dedicated server is not installed at the configured path yet.",
                DiagnosticEvidence.FilesystemInspected,
                details: exe,
                action: "Install the dedicated server (Update Server) or correct the path in Settings.",
                facts: facts);
        }

        facts["Exists"] = Yes;
        facts["SizeBytes"] = new FileInfo(exe).Length.ToString();
        return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Pass,
            "Dedicated server executable found (not started by diagnostics).",
            DiagnosticEvidence.FilesystemInspected, details: exe, facts: facts);
    }

    private DiagnosticCheckResult CheckServerWorkingDirectory(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ServerWorkingDirectory;
        const string name = "Server working directory";
        var cwd = NullIfBlank(context.Settings.ServerPaths.ServerWorkingDirectory);
        var exe = NullIfBlank(context.Settings.ServerPaths.ServerExecutablePath);

        if (cwd is null)
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Pass,
                "Not set; the executable's folder is used as the working directory.",
                details: exe is null ? null : Path.GetDirectoryName(exe));
        }

        var facts = new Dictionary<string, string> { ["WorkingDirectory"] = cwd };
        if (!PathValidator.IsSafeAbsolutePath(cwd))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "Working directory is not a safe absolute path.", details: cwd,
                action: "Choose the working directory with Browse in Settings.", facts: facts);
        }

        if (context.ClientRoot is not null && IsSameOrUnder(cwd, context.ClientRoot))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "Working directory is inside the standalone client folder.",
                details: cwd, action: "Use the dedicated server install folder.", facts: facts);
        }

        if (Directory.Exists(cwd))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Pass,
                "Working directory exists.", DiagnosticEvidence.FilesystemInspected, details: cwd, facts: facts);
        }

        var serverInstalled = exe is not null && File.Exists(exe);
        return Result(id, DiagnosticCategories.DedicatedServer, name,
            serverInstalled ? DiagnosticStatus.Fail : DiagnosticStatus.Warning,
            serverInstalled
                ? "Working directory does not exist, but the server executable does."
                : "Working directory does not exist yet (server not installed).",
            DiagnosticEvidence.FilesystemInspected,
            details: cwd,
            action: "Correct the working directory in Settings, or leave it empty to use the executable's folder.",
            facts: facts);
    }

    private DiagnosticCheckResult CheckServerInstallDirectory(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ServerInstallDirectory;
        const string name = "Server install directory";
        var install = context.InstallDirectory;
        if (install is null)
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.NotConfigured,
                "Dedicated server install directory is not configured.",
                action: "Set the install directory (SteamCMD app 443030) in Settings.");
        }

        var facts = new Dictionary<string, string> { ["InstallDirectory"] = install };
        if (!PathValidator.IsSafeAbsolutePath(install))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "Install directory is not a safe absolute path.", details: install,
                action: "Choose an absolute folder such as C:\\ConanServer.", facts: facts);
        }

        if (!Directory.Exists(install))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Warning,
                "Install directory does not exist yet; the live install will create it.",
                DiagnosticEvidence.FilesystemInspected, details: install, facts: facts);
        }

        var conanSandbox = Path.Combine(install, "ConanSandbox");
        var binaries = Path.Combine(install, "ConanSandbox", "Binaries", "Win64");
        var serverExe = Path.Combine(install, AppConstants.DefaultServerSubPath);
        var clientExe = Path.Combine(install, ConanExecutableClassifier.ClientExecutable);
        facts["ConanSandboxDirectory"] = Directory.Exists(conanSandbox) ? Yes : No;
        facts["BinariesWin64"] = Directory.Exists(binaries) ? Yes : No;
        facts["ServerExecutableAtDefaultPath"] = File.Exists(serverExe) ? Yes : No;
        ReadAppManifest(install, facts);

        if (File.Exists(clientExe) && !File.Exists(serverExe))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "This folder contains the Conan game CLIENT (ConanSandbox.exe), not the dedicated server.",
                DiagnosticEvidence.FilesystemInspected,
                details: install,
                action: "Use a separate folder for the dedicated server. The standalone client belongs in 'Standalone client root'.",
                facts: facts);
        }

        if (!Directory.Exists(conanSandbox) || !Directory.Exists(binaries))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Warning,
                "Install directory exists, but the Conan server folders are not present yet.",
                DiagnosticEvidence.FilesystemInspected,
                details: install,
                action: "Install the dedicated server into this folder (Update Server) during the live test.",
                facts: facts);
        }

        return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Pass,
            "Conan dedicated server folder structure found.",
            DiagnosticEvidence.FilesystemInspected, details: install, facts: facts);
    }

    private DiagnosticCheckResult CheckServerWorkspace(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ServerWorkspace;
        const string name = "Server workspace safety";
        var install = context.InstallDirectory;
        if (install is null)
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.NotConfigured,
                "No dedicated server workspace is configured.",
                action: "Set a dedicated server install directory in Settings.");
        }

        var facts = new Dictionary<string, string> { ["Workspace"] = install };
        if (!PathValidator.IsSafeAbsolutePath(install))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "Workspace is not a safe absolute path.", details: install, facts: facts);
        }

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(install));
        var root = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(full) ?? string.Empty);
        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "Workspace is a drive root.", details: install,
                action: "Use a dedicated sub-folder such as D:\\ConanServer.", facts: facts);
        }

        if (context.ClientRoot is not null &&
            (IsSameOrUnder(install, context.ClientRoot) || IsSameOrUnder(context.ClientRoot, install)))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "Workspace overlaps the standalone client folder.",
                details: $"Workspace: {install}{Environment.NewLine}Client root: {context.ClientRoot}",
                action: "Install the dedicated server in its own folder, separate from the game client.",
                facts: facts);
        }

        if (File.Exists(Path.Combine(install, ConanExecutableClassifier.ClientLauncherBatch)))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "Workspace contains 'Run Me!.bat' (standalone client launcher).",
                DiagnosticEvidence.FilesystemInspected,
                details: install,
                action: "Install the dedicated server in its own folder, separate from the game client.",
                facts: facts);
        }

        if (IsSameOrUnder(_paths.DataDirectory, install))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "The application data directory is inside the server workspace.",
                details: $"Workspace: {install}{Environment.NewLine}Data: {_paths.DataDirectory}",
                action: "Keep Conan Server Control's data folder outside the dedicated server install.",
                facts: facts);
        }

        var steamDir = _steamCmd.ExecutablePath is null ? null : Path.GetDirectoryName(_steamCmd.ExecutablePath);
        if (steamDir is not null && IsSameOrUnder(install, steamDir))
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Fail,
                "Workspace is inside the SteamCMD folder.",
                details: install, action: "Use a separate folder for the dedicated server.", facts: facts);
        }

        var warnings = new List<string>();
        if (IsSameOrUnder(install, _paths.DataDirectory))
        {
            warnings.Add("Workspace is inside the application data directory; prefer a separate folder.");
        }

        foreach (var protectedRoot in ProtectedWindowsRoots())
        {
            if (IsSameOrUnder(install, protectedRoot))
            {
                warnings.Add($"Workspace is under {protectedRoot}; writing there may require Administrator rights.");
            }
        }

        var parent = Path.GetDirectoryName(full);
        if (!Directory.Exists(install) && (parent is null || !Directory.Exists(parent)))
        {
            warnings.Add("Neither the workspace nor its parent folder exists yet.");
        }

        return warnings.Count > 0
            ? Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Warning,
                "Workspace is usable with warnings.", DiagnosticEvidence.FilesystemInspected,
                details: string.Join(Environment.NewLine, warnings), facts: facts)
            : Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Pass,
                "Workspace is separate from the client, app data and SteamCMD.",
                DiagnosticEvidence.FilesystemInspected, details: install, facts: facts);
    }

    private DiagnosticCheckResult CheckServerSaveLocation(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ServerSaveLocation;
        const string name = "Server save location";
        if (context.InstallDirectory is null)
        {
            return Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.NotConfigured,
                "Save location cannot be resolved without an install directory.");
        }

        var saved = Path.Combine(context.InstallDirectory, "ConanSandbox", "Saved");
        var facts = new Dictionary<string, string> { ["SavedDirectory"] = saved };
        return Directory.Exists(saved)
            ? Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Pass,
                "Server Saved folder resolved.", DiagnosticEvidence.FilesystemInspected, details: saved, facts: facts)
            : Result(id, DiagnosticCategories.DedicatedServer, name, DiagnosticStatus.Warning,
                "Server Saved folder does not exist yet (created on first server boot).",
                DiagnosticEvidence.FilesystemInspected, details: saved, facts: facts);
    }

    private static DiagnosticCheckResult CheckServerLive(CheckContext context) =>
        Result(DiagnosticCheckIds.ServerLive, DiagnosticCategories.DedicatedServer, "Dedicated server live boot",
            DiagnosticStatus.NotTested,
            "A real Conan dedicated server boot has not been verified by a live test.",
            DiagnosticEvidence.NotExercised,
            details: context.ServerStatus == ServerStatus.Online
                ? "The process manager currently reports Online (process + port/RCON readiness). That is not proof that a client can join."
                : $"Current status: {context.ServerStatus}. Diagnostics never start the server.",
            action: "Exercised by the guarded M3 Task 4 live test.",
            facts: new Dictionary<string, string> { ["CurrentStatus"] = context.ServerStatus.ToString() });

    // ---------------------------------------------------------------- Standalone client

    private DiagnosticCheckResult CheckClientRoot(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ClientRoot;
        const string name = "Standalone client root";
        var root = context.ClientRoot;
        if (root is null)
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.NotConfigured,
                "Standalone client root is not configured (optional; not required for server tests).",
                action: "Optionally set the folder that contains the client ConanSandbox.exe in Settings to prepare client compatibility tests.");
        }

        var facts = new Dictionary<string, string> { ["ClientRoot"] = root };
        if (!PathValidator.IsSafeAbsolutePath(root))
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Fail,
                "Standalone client root is not a safe absolute path.", details: root, facts: facts);
        }

        if (!Directory.Exists(root))
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Fail,
                "Standalone client root does not exist.", DiagnosticEvidence.FilesystemInspected,
                details: root, action: "Correct the standalone client root in Settings.", facts: facts);
        }

        if (context.InstallDirectory is not null &&
            PathValidator.IsSafeAbsolutePath(context.InstallDirectory) &&
            IsSameOrUnder(root, context.InstallDirectory))
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Fail,
                "Standalone client root is inside the dedicated server install.",
                details: root, action: "Point this at the game client folder, not the dedicated server.", facts: facts);
        }

        if (!File.Exists(Path.Combine(root, ConanExecutableClassifier.ClientExecutable)) &&
            File.Exists(Path.Combine(root, ConanExecutableClassifier.ClientLauncherBatch)))
        {
            var suggestion = Directory.EnumerateDirectories(root)
                .FirstOrDefault(d => File.Exists(Path.Combine(d, ConanExecutableClassifier.ClientExecutable)));
            if (suggestion is not null)
            {
                facts["SuggestedRoot"] = suggestion;
            }

            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Warning,
                "This is the launcher folder (contains Run Me!.bat); the client executable is in a sub-folder.",
                DiagnosticEvidence.FilesystemInspected,
                details: root,
                action: suggestion is null
                    ? "Set the root to the folder that contains ConanSandbox.exe."
                    : $"Set the root to: {suggestion}",
                facts: facts);
        }

        return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Pass,
            "Standalone client root exists.", DiagnosticEvidence.FilesystemInspected, details: root, facts: facts);
    }

    private DiagnosticCheckResult CheckClientExecutable(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ClientExecutable;
        const string name = "Standalone client executable";
        var root = context.ClientRoot;
        if (root is null || !Directory.Exists(root))
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.NotConfigured,
                "Standalone client not configured or not found (optional).");
        }

        if (File.Exists(Path.Combine(root, AppConstants.DedicatedServerExecutable)) ||
            File.Exists(Path.Combine(root, AppConstants.DefaultServerSubPath)))
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Fail,
                "This folder contains the DEDICATED SERVER, not the game client.",
                DiagnosticEvidence.FilesystemInspected,
                details: root, action: "Point the standalone client root at the game client folder.");
        }

        var candidates = new[]
        {
            Path.Combine(root, ConanExecutableClassifier.ClientExecutable),
            Path.Combine(root, "ConanSandbox", "Binaries", "Win64", ConanExecutableClassifier.ClientExecutable),
            Path.Combine(root, "ConanSandbox", "Binaries", "Win64", ConanExecutableClassifier.ClientShippingExecutable)
        };
        var found = candidates.FirstOrDefault(File.Exists);
        if (found is null)
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Fail,
                "ConanSandbox.exe was not found under the standalone client root.",
                DiagnosticEvidence.FilesystemInspected,
                details: root, action: "Set the root to the folder that contains the client ConanSandbox.exe.");
        }

        return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Pass,
            "Standalone client executable found (not launched by diagnostics).",
            DiagnosticEvidence.FilesystemInspected,
            details: found,
            facts: new Dictionary<string, string> { ["ClientExecutable"] = found, ["Launched"] = No });
    }

    private DiagnosticCheckResult CheckClientMods(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ClientMods;
        const string name = "Standalone client mods";
        var root = context.ClientRoot;
        if (root is null || !Directory.Exists(root))
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.NotConfigured,
                "Standalone client not configured (optional).");
        }

        var modsDir = Path.Combine(root, "ConanSandbox", "Mods");
        var modList = Path.Combine(modsDir, AppConstants.ModListFileName);
        var facts = new Dictionary<string, string>
        {
            ["ClientModsDirectory"] = modsDir,
            ["ClientModsDirectoryExists"] = Directory.Exists(modsDir) ? Yes : No,
            ["ClientModListPresent"] = File.Exists(modList) ? Yes : No
        };

        if (!Directory.Exists(modsDir))
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Warning,
                "Client has no ConanSandbox\\Mods folder (no local client mods).",
                DiagnosticEvidence.FilesystemInspected, details: modsDir,
                action: "If the server uses mods, the same .pak files must be copied to the client manually.",
                facts: facts);
        }

        if (!File.Exists(modList))
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Warning,
                "Client Mods folder exists but has no modlist.txt.",
                DiagnosticEvidence.FilesystemInspected, details: modsDir, facts: facts);
        }

        var entries = ReadModList(modList, modsDir);
        var missing = entries.Where(e => !e.Exists).Select(e => e.Line).ToArray();
        var zeroByte = entries.Where(e => e is { Exists: true, SizeBytes: 0 }).Select(e => e.Line).ToArray();
        facts["ClientModListEntries"] = entries.Count.ToString();
        facts["ClientMissingPaks"] = missing.Length.ToString();
        facts["ClientZeroBytePaks"] = zeroByte.Length.ToString();

        if (missing.Length > 0 || zeroByte.Length > 0)
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Warning,
                $"Client modlist.txt has {missing.Length} missing and {zeroByte.Length} zero-byte .pak entries.",
                DiagnosticEvidence.FilesystemInspected,
                details: string.Join(Environment.NewLine, missing.Select(m => "missing: " + m).Concat(zeroByte.Select(z => "zero-byte: " + z))),
                action: "Copy the correct .pak files into the client manually; client Workshop sync is not automatic.",
                facts: facts);
        }

        return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Pass,
            $"Client modlist.txt lists {entries.Count} .pak file(s) that exist (client load not verified).",
            DiagnosticEvidence.FilesystemInspected, details: modList, facts: facts);
    }

    private DiagnosticCheckResult CheckClientConfig(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ClientConfig;
        const string name = "Standalone client Saved/Config";
        var root = context.ClientRoot;
        if (root is null || !Directory.Exists(root))
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.NotConfigured,
                "Standalone client not configured (optional).");
        }

        var saved = Path.Combine(root, "ConanSandbox", "Saved");
        var config = Path.Combine(saved, "Config");
        var facts = new Dictionary<string, string>
        {
            ["ClientSavedDirectory"] = saved,
            ["ClientSavedExists"] = Directory.Exists(saved) ? Yes : No,
            ["ClientConfigExists"] = Directory.Exists(config) ? Yes : No
        };

        if (Directory.Exists(config))
        {
            facts["ClientConfigPlatforms"] = string.Join(", ",
                Directory.EnumerateDirectories(config).Select(Path.GetFileName));
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Pass,
                "Client Saved/Config folders found (read-only; not modified).",
                DiagnosticEvidence.FilesystemInspected, details: config, facts: facts);
        }

        return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Warning,
            "Client Saved/Config folders not found (the client may not have been run yet).",
            DiagnosticEvidence.FilesystemInspected, details: saved, facts: facts);
    }

    private DiagnosticCheckResult CheckClientModParity(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ClientModParity;
        const string name = "Client vs server mod list";
        var root = context.ClientRoot;
        if (root is null || !Directory.Exists(root))
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.NotTested,
                "Standalone client not configured; client/server mod parity not compared.");
        }

        var serverMods = ExpectedServerModFileNames(context);
        if (serverMods.Count == 0)
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Pass,
                "The server has no enabled mods; the client needs none.");
        }

        var clientModsDir = Path.Combine(root, "ConanSandbox", "Mods");
        var clientModList = Path.Combine(clientModsDir, AppConstants.ModListFileName);
        var clientNames = File.Exists(clientModList)
            ? ReadModList(clientModList, clientModsDir).Where(e => e.Exists).Select(e => e.FileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = serverMods.Where(m => !clientNames.Contains(m)).ToArray();
        var facts = new Dictionary<string, string>
        {
            ["ServerEnabledMods"] = serverMods.Count.ToString(),
            ["MissingOnClient"] = missing.Length.ToString()
        };

        if (missing.Length > 0)
        {
            return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Warning,
                $"Standalone client is missing {missing.Length} of {serverMods.Count} server mod(s). Client Workshop sync is not automatic.",
                DiagnosticEvidence.FilesystemInspected,
                details: string.Join(Environment.NewLine, missing),
                action: "Copy these .pak files into the client ConanSandbox\\Mods and add them to the client modlist.txt manually.",
                facts: facts);
        }

        return Result(id, DiagnosticCategories.StandaloneClient, name, DiagnosticStatus.Pass,
            "Every enabled server mod is listed and present in the client modlist (client load not verified).",
            DiagnosticEvidence.FilesystemInspected, facts: facts);
    }

    private static DiagnosticCheckResult CheckClientJoinLive() =>
        Result(DiagnosticCheckIds.ClientJoinLive, DiagnosticCategories.StandaloneClient, "Client join live test",
            DiagnosticStatus.NotTested,
            "A real client has not joined the dedicated server in a verified live test.",
            DiagnosticEvidence.NotExercised,
            details: "Diagnostics never launch Run Me!.bat or ConanSandbox.exe.",
            action: "Exercised by the guarded M3 Task 4 client compatibility test.");

    // ---------------------------------------------------------------- Helpers

    private static void ReadAppManifest(string install, IDictionary<string, string> facts)
    {
        var manifest = Path.Combine(install, "steamapps", $"appmanifest_{AppConstants.ConanDedicatedServerAppId}.acf");
        facts["AppManifestPresent"] = File.Exists(manifest) ? Yes : No;
        if (!File.Exists(manifest))
        {
            return;
        }

        foreach (Match match in AcfValue.Matches(File.ReadAllText(manifest)))
        {
            facts["AppManifest." + match.Groups["key"].Value] = match.Groups["value"].Value;
        }
    }

    private static bool IsSameOrUnder(string path, string root) =>
        PathValidator.IsSafeAbsolutePath(path) &&
        PathValidator.IsSafeAbsolutePath(root) &&
        PathValidator.IsUnderRoot(path, root);

    private static IEnumerable<string> ProtectedWindowsRoots()
    {
        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.Windows,
                     Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.ProgramFilesX86
                 })
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }
}
