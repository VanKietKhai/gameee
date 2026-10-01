using ConanServerControl.Core;
using ConanServerControl.Core.Backups;
using ConanServerControl.Core.Diagnostics;
using ConanServerControl.Core.Mods;

namespace ConanServerControl.Infrastructure.Diagnostics;

public sealed partial class IntegrationDiagnosticsService
{
    private sealed record ModListEntry(string Line, string FileName, string FullPath, bool Exists, long SizeBytes);

    // ---------------------------------------------------------------- Mods (server)

    private DiagnosticCheckResult CheckModsDirectory(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ModsDirectory;
        const string name = "Server Mods directory";
        var modsDir = ServerModsDirectory(context);
        var enabled = EnabledMods(context);
        var facts = new Dictionary<string, string>
        {
            ["CatalogMods"] = context.Settings.Mods.Mods.Count.ToString(),
            ["EnabledMods"] = enabled.Count.ToString()
        };

        if (modsDir is null)
        {
            return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.NotConfigured,
                "Server Mods directory cannot be resolved without an install directory.", facts: facts);
        }

        facts["ModsDirectory"] = modsDir;
        if (Directory.Exists(modsDir))
        {
            return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Pass,
                $"Server Mods directory exists; {enabled.Count} mod(s) enabled in the application.",
                DiagnosticEvidence.FilesystemInspected, details: modsDir, facts: facts);
        }

        return enabled.Count > 0
            ? Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Fail,
                $"{enabled.Count} mod(s) are enabled, but the server Mods directory does not exist.",
                DiagnosticEvidence.FilesystemInspected, details: modsDir,
                action: "Run Update Mods during the live test, or disable the mods.", facts: facts)
            : Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.NotConfigured,
                "No enabled mods and no server Mods directory.",
                DiagnosticEvidence.FilesystemInspected, details: modsDir, facts: facts);
    }

    private DiagnosticCheckResult CheckModList(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ModsModList;
        const string name = "Server modlist.txt";
        var modsDir = ServerModsDirectory(context);
        if (modsDir is null)
        {
            return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.NotConfigured,
                "modlist.txt cannot be resolved without an install directory.");
        }

        var path = Path.Combine(modsDir, AppConstants.ModListFileName);
        var enabled = EnabledMods(context);
        var facts = new Dictionary<string, string> { ["ModListPath"] = path };
        if (!File.Exists(path))
        {
            return enabled.Count > 0
                ? Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Fail,
                    $"{enabled.Count} mod(s) are enabled, but modlist.txt does not exist.",
                    DiagnosticEvidence.FilesystemInspected, details: path,
                    action: "Run Update Mods during the live test so the application writes modlist.txt.", facts: facts)
                : Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.NotConfigured,
                    "No modlist.txt and no enabled mods.", DiagnosticEvidence.FilesystemInspected,
                    details: path, facts: facts);
        }

        var entries = ReadModList(path, modsDir);
        facts["Entries"] = entries.Count.ToString();
        return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Pass,
            $"modlist.txt exists with {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")} (read-only; not modified).",
            DiagnosticEvidence.FilesystemInspected, details: path, facts: facts);
    }

    private DiagnosticCheckResult CheckPakFiles(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ModsPakFiles;
        const string name = "Server .pak files";
        var modsDir = ServerModsDirectory(context);
        if (modsDir is null)
        {
            return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.NotConfigured,
                "Mods cannot be inspected without an install directory.");
        }

        var modList = Path.Combine(modsDir, AppConstants.ModListFileName);
        var entries = File.Exists(modList) ? ReadModList(modList, modsDir) : new List<ModListEntry>();

        // Catalog mods with a known local file must exist too, even if modlist.txt drifted.
        var listed = entries.Select(e => e.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in EnabledMods(context).Where(m => !string.IsNullOrWhiteSpace(m.LocalFileName)))
        {
            var fileName = Path.GetFileName(mod.LocalFileName!);
            if (listed.Add(fileName))
            {
                entries.Add(ToEntry(fileName, modsDir));
            }
        }

        if (entries.Count == 0)
        {
            return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.NotConfigured,
                "No .pak files are expected (no modlist entries and no enabled mods).");
        }

        var missing = entries.Where(e => !e.Exists).Select(e => e.FullPath).ToArray();
        var zeroByte = entries.Where(e => e is { Exists: true, SizeBytes: 0 }).Select(e => e.FullPath).ToArray();
        var facts = new Dictionary<string, string>
        {
            ["ExpectedPaks"] = entries.Count.ToString(),
            ["MissingPaks"] = missing.Length.ToString(),
            ["ZeroBytePaks"] = zeroByte.Length.ToString()
        };

        if (missing.Length > 0 || zeroByte.Length > 0)
        {
            return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Fail,
                $"{missing.Length} missing and {zeroByte.Length} zero-byte .pak file(s).",
                DiagnosticEvidence.FilesystemInspected,
                details: string.Join(Environment.NewLine,
                    missing.Select(m => "missing: " + m).Concat(zeroByte.Select(z => "zero-byte: " + z))),
                action: "Re-download the affected mods with Update Mods (server stopped) during the live test.",
                facts: facts);
        }

        return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Pass,
            $"All {entries.Count} expected .pak file(s) exist and are non-empty. Conan has not been observed loading them.",
            DiagnosticEvidence.FilesystemInspected, facts: facts);
    }

    private DiagnosticCheckResult CheckModOrdering(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ModsOrdering;
        const string name = "modlist.txt matches application state";
        var modsDir = ServerModsDirectory(context);
        if (modsDir is null)
        {
            return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.NotConfigured,
                "Mod order cannot be compared without an install directory.");
        }

        var expected = ExpectedServerModFileNames(context);
        var modList = Path.Combine(modsDir, AppConstants.ModListFileName);
        if (!File.Exists(modList))
        {
            return expected.Count == 0
                ? Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Pass,
                    "No enabled mods and no modlist.txt; nothing to compare.")
                : Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.NotTested,
                    "modlist.txt is missing; order cannot be compared.");
        }

        var actual = ReadModList(modList, modsDir).Select(e => e.FileName).ToArray();
        var facts = new Dictionary<string, string>
        {
            ["ExpectedOrder"] = string.Join(" > ", expected),
            ["ModListOrder"] = string.Join(" > ", actual)
        };

        if (expected.SequenceEqual(actual, StringComparer.OrdinalIgnoreCase))
        {
            return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Pass,
                "modlist.txt matches the application's enabled mods and load order.",
                DiagnosticEvidence.FilesystemInspected, facts: facts);
        }

        var onlyInFile = actual.Except(expected, StringComparer.OrdinalIgnoreCase).ToArray();
        var onlyInApp = expected.Except(actual, StringComparer.OrdinalIgnoreCase).ToArray();
        if (onlyInFile.Length == 0 && onlyInApp.Length == 0)
        {
            return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Warning,
                "modlist.txt has the same mods as the application, but in a different load order.",
                DiagnosticEvidence.FilesystemInspected,
                action: "Saving the mod order in the Mods page rewrites modlist.txt (diagnostics never modify it).",
                facts: facts);
        }

        return Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Warning,
            $"modlist.txt differs from the application: {onlyInFile.Length} extra, {onlyInApp.Length} missing.",
            DiagnosticEvidence.FilesystemInspected,
            details: string.Join(Environment.NewLine,
                onlyInFile.Select(f => "only in modlist.txt: " + f).Concat(onlyInApp.Select(a => "only in application: " + a))),
            action: "Review the Mods page; the next mod commit rewrites modlist.txt from application state.",
            facts: facts);
    }

    private DiagnosticCheckResult CheckModDuplicates(CheckContext context)
    {
        const string id = DiagnosticCheckIds.ModsDuplicates;
        const string name = "Duplicate / conflicting mod entries";
        var problems = new List<string>();
        var catalog = context.Settings.Mods.Mods;

        problems.AddRange(catalog.GroupBy(m => m.WorkshopId).Where(g => g.Count() > 1)
            .Select(g => $"Workshop ID {g.Key} appears {g.Count()} times in the application catalog."));
        problems.AddRange(catalog.Where(m => !string.IsNullOrWhiteSpace(m.LocalFileName))
            .GroupBy(m => Path.GetFileName(m.LocalFileName!), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(m => m.WorkshopId).Distinct().Count() > 1)
            .Select(g => $"{g.Key} is claimed by Workshop IDs {string.Join(", ", g.Select(m => m.WorkshopId).Distinct())}."));

        var modsDir = ServerModsDirectory(context);
        if (modsDir is not null)
        {
            var modList = Path.Combine(modsDir, AppConstants.ModListFileName);
            if (File.Exists(modList))
            {
                problems.AddRange(ReadModList(modList, modsDir)
                    .GroupBy(e => e.FileName, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => $"{g.Key} appears {g.Count()} times in modlist.txt."));
            }
        }

        return problems.Count > 0
            ? Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Warning,
                $"{problems.Count} duplicate/conflicting mod entr{(problems.Count == 1 ? "y" : "ies")}.",
                DiagnosticEvidence.FilesystemInspected,
                details: string.Join(Environment.NewLine, problems),
                action: "Remove the duplicates in the Mods page.")
            : Result(id, DiagnosticCategories.Mods, name, DiagnosticStatus.Pass,
                "No duplicate or conflicting mod entries.", DiagnosticEvidence.FilesystemInspected);
    }

    private static DiagnosticCheckResult CheckWorkshopLive() =>
        Result(DiagnosticCheckIds.ModsWorkshopLive, DiagnosticCategories.Mods, "Workshop download / mod load",
            DiagnosticStatus.NotTested,
            "Real Workshop download and Conan mod loading have not been exercised.",
            DiagnosticEvidence.NotExercised,
            details: "Diagnostics never download Workshop items or touch .pak files. A .pak existing does not mean Conan loaded it.",
            action: "Exercised by the guarded M3 Task 4 live test.");

    // ---------------------------------------------------------------- World

    private static DiagnosticCheckResult CheckWorldFiles(CheckContext context)
    {
        const string id = DiagnosticCheckIds.WorldFiles;
        const string name = "World database files";
        if (context.InstallDirectory is null)
        {
            return Result(id, DiagnosticCategories.World, name, DiagnosticStatus.NotConfigured,
                "World files cannot be located without an install directory.");
        }

        var saved = Path.Combine(context.InstallDirectory, "ConanSandbox", "Saved");
        var present = ConanWorldFiles.Present(saved);
        var worldType = ConanWorldFiles.DetectWorldType(present);
        var mainDb = ConanWorldFiles.MainDatabaseFileName(present);
        var facts = new Dictionary<string, string>
        {
            ["SavedDirectory"] = saved,
            ["WorldType"] = worldType ?? "none",
            ["MainDb"] = mainDb ?? "none",
            ["EnhancedMainPresent"] = Has(present, ConanWorldFiles.EnhancedMain) ? Yes : No,
            ["LegacyMainPresent"] = Has(present, ConanWorldFiles.LegacyMain) ? Yes : No,
            ["WalPresent"] = mainDb is not null && Has(present, mainDb + "-wal") ? Yes : No,
            ["ShmPresent"] = mainDb is not null && Has(present, mainDb + "-shm") ? Yes : No,
            ["LiveDbOpened"] = No
        };
        const string readOnlyNote = "The live database was not opened. SQLite quick_check runs only on backup copies.";

        if (!Directory.Exists(saved))
        {
            return Result(id, DiagnosticCategories.World, name, DiagnosticStatus.Warning,
                "No known world detected: the server Saved folder does not exist yet.",
                DiagnosticEvidence.FilesystemInspected, details: saved, facts: facts);
        }

        if (mainDb is null)
        {
            if (present.Count > 0)
            {
                return Result(id, DiagnosticCategories.World, name, DiagnosticStatus.Fail,
                    "WAL/SHM files exist but the main world database is missing.",
                    DiagnosticEvidence.FilesystemInspected,
                    details: $"{string.Join(", ", present)} in {saved}. {readOnlyNote}",
                    action: "Do not start the server or run live tests against this world until it is investigated; restore from a verified backup if needed.",
                    facts: facts);
            }

            return Result(id, DiagnosticCategories.World, name, DiagnosticStatus.Warning,
                "No known world detected (no game_0.db or game.db). A fresh server creates one on first boot.",
                DiagnosticEvidence.FilesystemInspected, details: saved, facts: facts);
        }

        var info = new FileInfo(Path.Combine(saved, mainDb));
        facts["MainDbSizeBytes"] = info.Length.ToString();
        facts["MainDbModifiedUtc"] = info.LastWriteTimeUtc.ToString("u");

        if (info.Length == 0)
        {
            return Result(id, DiagnosticCategories.World, name, DiagnosticStatus.Warning,
                $"{worldType} world database {mainDb} is zero bytes.",
                DiagnosticEvidence.FilesystemInspected, details: readOnlyNote,
                action: "Investigate before live testing; restore from a verified backup if needed.", facts: facts);
        }

        if (Has(present, ConanWorldFiles.EnhancedMain) && Has(present, ConanWorldFiles.LegacyMain))
        {
            return Result(id, DiagnosticCategories.World, name, DiagnosticStatus.Warning,
                "Both Enhanced (game_0.db) and Legacy (game.db) worlds exist; backups capture the Enhanced world.",
                DiagnosticEvidence.FilesystemInspected, details: readOnlyNote, facts: facts);
        }

        return Result(id, DiagnosticCategories.World, name, DiagnosticStatus.Pass,
            $"{worldType} world detected ({mainDb}).",
            DiagnosticEvidence.FilesystemInspected, details: readOnlyNote, facts: facts);
    }

    // ---------------------------------------------------------------- Helpers

    private static string? ServerModsDirectory(CheckContext context) =>
        context.InstallDirectory is null ? null : Path.Combine(context.InstallDirectory, "ConanSandbox", "Mods");

    private static IReadOnlyList<Core.Models.WorkshopMod> EnabledMods(CheckContext context) =>
        context.Settings.Mods.Mods.Where(m => m.Enabled).ToArray();

    /// <summary>The modlist.txt file names the application would write, in load order.</summary>
    private static IReadOnlyList<string> ExpectedServerModFileNames(CheckContext context) =>
        ModListGenerator.Parse(ModListGenerator.Generate(context.Settings.Mods.Mods))
            .Select(FileNameOf)
            .ToArray();

    private static List<ModListEntry> ReadModList(string modListPath, string modsDirectory) =>
        ModListGenerator.Parse(File.ReadAllText(modListPath))
            .Select(line => ToEntry(line, modsDirectory))
            .ToList();

    private static ModListEntry ToEntry(string line, string modsDirectory)
    {
        var cleaned = line.Trim().TrimStart('*').Trim().Trim('"');
        var full = Path.IsPathRooted(cleaned)
            ? cleaned
            : Path.Combine(modsDirectory, cleaned.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar));
        var info = new FileInfo(full);
        return new ModListEntry(line, FileNameOf(cleaned), full, info.Exists, info.Exists ? info.Length : 0);
    }

    private static string FileNameOf(string line)
    {
        var trimmed = line.Trim().TrimStart('*').Trim().Trim('"');
        var index = trimmed.LastIndexOfAny(['\\', '/']);
        return index >= 0 ? trimmed[(index + 1)..] : trimmed;
    }

    private static bool Has(IEnumerable<string> names, string expected) =>
        names.Contains(expected, StringComparer.OrdinalIgnoreCase);
}
