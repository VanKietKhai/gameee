using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConanServerControl.Core.LiveTesting;

/// <summary>
/// The validated-warning catalog a batch was judged against: known warnings, validated LoadErrors sets, base-game
/// noise kinds, severe markers and singleton persistence objects. <see cref="Current"/> is the committed code;
/// a snapshot records the catalog in force when its batch ran, so a historical analysis does not change when the
/// catalog later does.
/// </summary>
public sealed record ValidatedCatalog(
    IReadOnlyList<KnownBootWarning> KnownWarnings,
    IReadOnlyList<LoadErrorBaseline> LoadErrorBaselines,
    IReadOnlyList<ModBootGates.BaseGameNoiseKind> BaseGameNoise,
    IReadOnlyList<string> SevereLogMarkers,
    IReadOnlyList<SingletonActor> SingletonActors)
{
    public static ValidatedCatalog Current => new(
        ModBootGates.KnownWarnings, ModBootGates.LoadErrorBaselines, ModBootGates.BaseGameNoise,
        ModBootGates.SevereLogMarkers, ModBootGates.SingletonActors);
}

/// <summary>One mod file of a batch: exact name, SHA-256 and size.</summary>
public sealed record SnapshotMod(string FileName, string Sha256, long SizeBytes);

/// <summary>
/// Identity of the analysed boot log: the immutable copy kept beside the snapshot, its SHA-256 and size, and the
/// path it was taken from.
/// </summary>
public sealed record SnapshotLog(string FileName, string Sha256, long Bytes, string SourcePath);

/// <summary>
/// Immutable record of everything a boot-log analysis depends on, taken when a live batch runs: the active modlist
/// order, each mod's exact file name and SHA-256, the validated catalog, and the boot log's identity and hash.
/// Analysing from a snapshot (instead of the live catalog) gives the same result after the mod was removed or the
/// server rolled back.
/// </summary>
public sealed record BatchAnalysisSnapshot(
    string BatchId,
    DateTime CreatedUtc,
    IReadOnlyList<string> ModList,
    IReadOnlyList<SnapshotMod> Mods,
    IReadOnlyList<string> ExpectAbsent,
    ValidatedCatalog Catalog,
    SnapshotLog? Log = null,
    int SchemaVersion = 1)
{
    public IReadOnlyDictionary<string, string> Sha256ByPak =>
        Mods.ToDictionary(m => m.FileName, m => m.Sha256, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Writes snapshots once (create-new, read-only) and loads them with the log hash verified.</summary>
public static class BatchAnalysisSnapshotStore
{
    public const string SnapshotFileName = "snapshot.json";
    public const string LogFileName = "boot.log";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Saves <paramref name="snapshot"/> under <c>&lt;root&gt;\&lt;BatchId&gt;</c> with a copy of the boot log taken
    /// from <paramref name="logSourcePath"/> (bytes from <paramref name="logOffset"/>). Refuses to overwrite an
    /// existing batch; both files are made read-only. Returns the snapshot directory.
    /// </summary>
    public static string Save(string root, BatchAnalysisSnapshot snapshot, string logSourcePath, long logOffset = 0)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.BatchId) || snapshot.BatchId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) || snapshot.BatchId.StartsWith('.'))
        {
            throw new ArgumentException("BatchId may contain only letters, digits, '-', '_' and '.'.", nameof(snapshot));
        }

        var dir = Path.Combine(root, snapshot.BatchId);
        if (Directory.Exists(dir))
        {
            throw new IOException($"Batch snapshot '{snapshot.BatchId}' already exists and is immutable: {dir}");
        }

        Directory.CreateDirectory(dir);
        var logCopy = Path.Combine(dir, LogFileName);
        byte[] bytes;
        using (var source = new FileStream(logSourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            source.Seek(logOffset, SeekOrigin.Begin);
            using var ms = new MemoryStream();
            source.CopyTo(ms);
            bytes = ms.ToArray();
        }

        using (var target = new FileStream(logCopy, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            target.Write(bytes);
        }

        var saved = snapshot with
        {
            Log = new SnapshotLog(LogFileName, Convert.ToHexString(SHA256.HashData(bytes)), bytes.LongLength, logSourcePath)
        };
        var path = Path.Combine(dir, SnapshotFileName);
        using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(target, saved, Json);
        }

        File.SetAttributes(logCopy, FileAttributes.ReadOnly);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        return dir;
    }

    /// <summary>
    /// Loads a snapshot (directory or <c>snapshot.json</c> path) and verifies that its log copy still has the
    /// recorded SHA-256 and size; otherwise the snapshot is rejected.
    /// </summary>
    public static (BatchAnalysisSnapshot Snapshot, string LogPath) Load(string directoryOrJson)
    {
        var json = Directory.Exists(directoryOrJson) ? Path.Combine(directoryOrJson, SnapshotFileName) : directoryOrJson;
        var snapshot = JsonSerializer.Deserialize<BatchAnalysisSnapshot>(File.ReadAllText(json), Json)
                       ?? throw new InvalidDataException("Empty batch snapshot: " + json);
        if (snapshot.Log is null)
        {
            throw new InvalidDataException("Batch snapshot has no boot-log identity: " + json);
        }

        var logPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(json))!, snapshot.Log.FileName);
        var actual = File.ReadAllBytes(logPath);
        if (actual.LongLength != snapshot.Log.Bytes ||
            !string.Equals(Convert.ToHexString(SHA256.HashData(actual)), snapshot.Log.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Boot log copy does not match the snapshot's recorded SHA-256/size: {logPath}");
        }

        return (snapshot, logPath);
    }
}

/// <summary>Everything the boot-log problem analysis produced for one batch.</summary>
public sealed record BootLogAnalysisResult(
    IReadOnlyList<string> Unknown,
    IReadOnlyList<(KnownBootWarning Warning, string Line)> Known,
    IReadOnlyDictionary<string, int> BaseGameNoiseCounts,
    IReadOnlyDictionary<string, BootGateResult> LoadErrorGates,
    IReadOnlyDictionary<string, int> LoadErrorCountsByMod,
    IReadOnlyList<LoadErrorEntry> UnattributedLoadErrors,
    IReadOnlyList<string> LoadErrorProblems)
{
    public bool Clean => Unknown.Count == 0 && LoadErrorProblems.Count == 0;
}

/// <summary>
/// The problem analysis of one boot log against a <see cref="BatchAnalysisSnapshot"/> (mod set, hashes, recorded
/// catalog). It reads nothing from the live server except, through <paramref name="readExtractedContainer"/>, a
/// mod's extracted container bytes, which attribute "package None" LoadErrors to a mod.
/// </summary>
public static class BootLogAnalyzer
{
    public static BootLogAnalysisResult Analyze(
        IReadOnlyList<string> lines,
        BatchAnalysisSnapshot context,
        Func<string, byte[]?> readExtractedContainer)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(readExtractedContainer);

        var stems = context.ModList.Concat(context.ExpectAbsent)
            .Select(Path.GetFileNameWithoutExtension).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToArray();
        var flagged = ModBootGates.SelectProblemLines(lines, stems, context.Catalog);
        var sha = context.Sha256ByPak;
        var (problems, known) = ModBootGates.ClassifyProblems(flagged, sha, context.Catalog);

        var noise = lines.Select(l => ModBootGates.MatchBaseGameNoise(l, context.Catalog.BaseGameNoise))
            .Where(id => id is not null).GroupBy(id => id!).ToDictionary(g => g.Key, g => g.Count());

        var parsed = ModBootGates.ParseLoadErrors(lines);
        var (byMod, unattributed) = AttributeLoadErrors(parsed.Where(e => e.RawUnparsedLine is null).ToList(), context.ModList, readExtractedContainer);
        var gates = context.ModList.ToDictionary(
            pak => pak,
            pak => ModBootGates.EvaluateLoadErrors(pak, sha.GetValueOrDefault(pak), byMod.GetValueOrDefault(pak) ?? [], context.Catalog),
            StringComparer.OrdinalIgnoreCase);
        var loadErrorProblems = gates.Where(g => !g.Value.Pass).Select(g => $"{g.Key}: {g.Value.Detail}")
            .Concat(unattributed.Select(e => $"unattributed LoadErrors: {e}"))
            .Concat(parsed.Where(e => e.RawUnparsedLine is not null).Select(e => $"unparsed LoadErrors: {e.RawUnparsedLine}"))
            .ToList();

        return new BootLogAnalysisResult(problems, known, noise, gates,
            byMod.ToDictionary(kv => kv.Key, kv => kv.Value.Count, StringComparer.OrdinalIgnoreCase), unattributed, loadErrorProblems);
    }

    /// <summary>
    /// Assigns each LoadErrors entry to an installed mod: by its <c>/Game/Mods/&lt;mod&gt;/</c> package path, or, for
    /// "package None", by the missing package id's 8 little-endian bytes appearing in that mod's extracted
    /// <c>-WindowsServer.ucas</c>. Entries that match no installed mod are returned as unattributed.
    /// </summary>
    public static (Dictionary<string, List<LoadErrorEntry>> ByMod, List<LoadErrorEntry> Unattributed) AttributeLoadErrors(
        IReadOnlyList<LoadErrorEntry> entries, IReadOnlyList<string> installedPaks, Func<string, byte[]?> readExtractedContainer)
    {
        var byMod = new Dictionary<string, List<LoadErrorEntry>>(StringComparer.OrdinalIgnoreCase);
        var unattributed = new List<LoadErrorEntry>();
        var containers = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        byte[]? Container(string stem)
        {
            if (!containers.TryGetValue(stem, out var bytes))
            {
                bytes = readExtractedContainer(stem);
                containers[stem] = bytes;
            }

            return bytes;
        }

        foreach (var entry in entries)
        {
            var owner = installedPaks.FirstOrDefault(pak =>
            {
                var stem = Path.GetFileNameWithoutExtension(pak);
                if (!string.Equals(entry.Package, "None", StringComparison.Ordinal))
                {
                    return entry.Package.StartsWith($"/Game/Mods/{stem}/", StringComparison.OrdinalIgnoreCase);
                }

                var idBytes = new byte[8];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(idBytes, Convert.ToUInt64(entry.MissingPackageId, 16));
                return Container(stem) is { } bytes && bytes.AsSpan().IndexOf(idBytes) >= 0;
            });
            if (owner is null)
            {
                unattributed.Add(entry);
            }
            else
            {
                if (!byMod.TryGetValue(owner, out var list))
                {
                    list = [];
                    byMod[owner] = list;
                }

                list.Add(entry);
            }
        }

        return (byMod, unattributed);
    }
}
