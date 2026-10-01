using ConanServerControl.Core.Backups;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;

namespace ConanServerControl.Core.Mods;

/// <summary>One required server mod inside a Client Mod Bundle.</summary>
public sealed record ClientModBundleEntry(
    int LoadOrder,
    string FileName,
    long SizeBytes,
    string Sha256,
    ModSourceType SourceType,
    long? WorkshopId,
    string Name);

/// <summary>
/// manifest.json of a Client Mod Bundle: the server's required mod set for standalone clients.
/// Contains mod files only, never Conan game files.
/// </summary>
public sealed record ClientModBundleManifest
{
    public const int CurrentVersion = 1;

    public int BundleVersion { get; init; } = CurrentVersion;

    public required DateTimeOffset CreatedAt { get; init; }

    public required string Generator { get; init; }

    public string ModListFileName { get; init; } = AppConstants.ModListFileName;

    public string ModsFolder { get; init; } = "Mods";

    public string Notice { get; init; } =
        "Mod files only. This bundle contains no Conan Exiles game files. Players need their own licensed Conan Exiles client.";

    public required IReadOnlyList<ClientModBundleEntry> Mods { get; init; }
}

public sealed record ClientModBundleExport(
    string BundleDirectory,
    string ManifestPath,
    string ModListPath,
    ClientModBundleManifest Manifest);

public enum ClientModSyncAction
{
    /// <summary>The client already has an identical file (same SHA-256).</summary>
    UpToDate = 0,

    /// <summary>The client lacks the file; a sync would copy it.</summary>
    Copy = 1,

    /// <summary>The client has a different file with the same name; a sync would replace it.</summary>
    Replace = 2
}

public sealed record ClientModSyncItem(
    int LoadOrder,
    string FileName,
    ClientModSyncAction Action,
    string TargetPath,
    string? CurrentSha256);

/// <summary>
/// What a guarded client synchronization WOULD do. Producing a plan never writes anything.
/// </summary>
public sealed record ClientModSyncPlan(
    string ClientRoot,
    string ClientModsDirectory,
    bool ClientExecutableFound,
    IReadOnlyList<ClientModSyncItem> Items,
    IReadOnlyList<string> ExtraClientPaks,
    string ExpectedModList,
    string? CurrentModList,
    bool ModListMatches)
{
    public bool IsInSync => ModListMatches && Items.All(i => i.Action == ClientModSyncAction.UpToDate);
}

/// <summary>
/// Read-only comparison between a bundle manifest and a standalone client installation.
/// Target layout (to be confirmed by the guarded live checkpoint): &lt;client&gt;\ConanSandbox\Mods.
/// </summary>
public static class ClientModSyncPlanner
{
    public static ClientModSyncPlan Plan(ClientModBundleManifest manifest, string clientRoot)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientRoot);

        var modsDir = Path.Combine(clientRoot, "ConanSandbox", "Mods");
        var items = new List<ClientModSyncItem>();
        foreach (var entry in manifest.Mods.OrderBy(m => m.LoadOrder))
        {
            if (!PathValidator.IsSafeRelativeName(entry.FileName) ||
                !entry.FileName.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Bundle manifest contains an unsafe mod file name: {entry.FileName}");
            }

            var target = Path.Combine(modsDir, entry.FileName);
            if (!File.Exists(target))
            {
                items.Add(new ClientModSyncItem(entry.LoadOrder, entry.FileName, ClientModSyncAction.Copy, target, null));
                continue;
            }

            var currentHash = BackupFileHasher.Sha256File(target);
            var action = string.Equals(currentHash, entry.Sha256, StringComparison.OrdinalIgnoreCase)
                ? ClientModSyncAction.UpToDate
                : ClientModSyncAction.Replace;
            items.Add(new ClientModSyncItem(entry.LoadOrder, entry.FileName, action, target, currentHash));
        }

        var required = manifest.Mods.Select(m => m.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var extras = Directory.Exists(modsDir)
            ? Directory.EnumerateFiles(modsDir, "*.pak").Select(Path.GetFileName).Where(n => n is not null && !required.Contains(n)).Cast<string>().ToArray()
            : Array.Empty<string>();

        var expected = string.Join(Environment.NewLine, manifest.Mods.OrderBy(m => m.LoadOrder).Select(m => m.FileName));
        var modListPath = Path.Combine(modsDir, manifest.ModListFileName);
        var current = File.Exists(modListPath) ? File.ReadAllText(modListPath) : null;
        var matches = current is not null &&
                      ModListGenerator.Parse(current).Select(FileNameOf)
                          .SequenceEqual(ModListGenerator.Parse(expected), StringComparer.OrdinalIgnoreCase);

        var exeFound = File.Exists(Path.Combine(clientRoot, "ConanSandbox.exe")) ||
                       File.Exists(Path.Combine(clientRoot, "ConanSandbox", "Binaries", "Win64", "ConanSandbox.exe"));

        return new ClientModSyncPlan(clientRoot, modsDir, exeFound, items, extras, expected, current, matches);
    }

    private static string FileNameOf(string line)
    {
        var trimmed = line.Trim().TrimStart('*').Trim().Trim('"');
        var index = trimmed.LastIndexOfAny(['\\', '/']);
        return index >= 0 ? trimmed[(index + 1)..] : trimmed;
    }
}

/// <summary>
/// Finds ConanSandboxServer.exe inside an existing Dedicated Server installation. Supports the
/// install root and ConanSandbox\Binaries\Win64 because the exact layout of a real install has
/// not been live-verified yet. Never returns the -Win64-Shipping binary (the start gate blocks it).
/// </summary>
public static class DedicatedServerLocator
{
    public static IReadOnlyList<string> CandidatePaths(string installRoot) =>
    [
        Path.Combine(installRoot, AppConstants.DedicatedServerExecutable),
        Path.Combine(installRoot, AppConstants.DefaultServerSubPath)
    ];

    public static string? Find(string? installRoot) =>
        string.IsNullOrWhiteSpace(installRoot) || !Directory.Exists(installRoot)
            ? null
            : CandidatePaths(installRoot).FirstOrDefault(File.Exists);
}
