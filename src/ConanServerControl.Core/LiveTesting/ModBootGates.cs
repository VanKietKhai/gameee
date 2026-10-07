using System.Text.RegularExpressions;

namespace ConanServerControl.Core.LiveTesting;

/// <summary>
/// A boot-log line accepted as a known, non-blocking warning. It matches only the exact message
/// (the line without its <c>[timestamp][frame]</c> prefix) and only while the installed mod file
/// still has the SHA-256 it was validated with, so a new mod version must be validated again.
/// </summary>
public sealed record KnownBootWarning(
    string Id,
    string ModPakFileName,
    string ValidatedPakSha256,
    string ExactMessage,
    string Reason)
{
    /// <summary>Occurrences per boot beyond which the extra lines fail the boot (unlimited unless set).</summary>
    public int MaxOccurrences { get; init; } = int.MaxValue;
}

/// <summary>
/// A set of identical warnings accepted only together: bound to one mod file's SHA-256, one exact message pattern
/// (matched against the message without its <c>[timestamp][frame]</c> prefix), one phase (after the main-world
/// teardown begins) and an exact occurrence count. See <see cref="ModBootGates.PhaseBoundWarnings"/>.
/// </summary>
public sealed record PhaseBoundWarning(
    string Id,
    string ModPakFileName,
    string ValidatedPakSha256,
    string MessagePattern,
    int ExactCount,
    string Reason);

/// <summary>Result of one live-harness gate.</summary>
public sealed record BootGateResult(bool Pass, string Detail);

/// <summary>One <c>LoadErrors</c> line: the package being loaded and the missing dependency's package id.</summary>
public sealed record LoadErrorEntry(string Package, string MissingPackageId)
{
    public string? RawUnparsedLine { get; init; }

    /// <summary>The line without its <c>[timestamp][frame]</c> prefix, as logged (set by <see cref="ModBootGates.ParseLoadErrors"/>).</summary>
    public string? Message { get; init; }

    /// <summary>The next log line (the engine's explanatory line), without its line ending; null at the end of the log.</summary>
    public string? DetailLine { get; init; }

    /// <summary>The engine frame from the line's prefix; null when the line has no prefix.</summary>
    public int? Frame { get; init; }

    public override string ToString() => $"{Package} -> {MissingPackageId}";
}

/// <summary>
/// The exact two-line form one validated <c>LoadErrors</c> entry must have: the <c>LoadErrors:</c> message (without
/// its prefix), the explanatory line that follows it, and, when set, the engine frame it must be logged in.
/// All comparisons are ordinal and exact.
/// </summary>
public sealed record LoadErrorSignature(string ExactMessage, string ExactDetailLine, int? RequiredFrame);

public enum LoadErrorBaselineStatus
{
    /// <summary>Operator-accepted known non-blocking warning.</summary>
    KnownNonBlocking,

    /// <summary>Recorded but not yet classified by the operator; monitored for any change.</summary>
    PendingOperatorClassification
}

/// <summary>
/// The exact multiset of <c>LoadErrors</c> a validated mod file produces on every boot, keyed by
/// <see cref="LoadErrorEntry.ToString"/>. It applies only while the installed file has <see cref="ValidatedPakSha256"/>.
/// </summary>
public sealed record LoadErrorBaseline(
    string ModPakFileName,
    string ValidatedPakSha256,
    LoadErrorBaselineStatus Status,
    IReadOnlyDictionary<string, int> Expected,
    string Reason)
{
    /// <summary>
    /// Optional, per <see cref="Expected"/> key: the exact two-line form (and frame) every occurrence of that entry
    /// must have. Null (the default) keeps the key-and-count comparison only, as for the earlier baselines; it is not
    /// written to JSON, so catalogs without signatures keep their recorded SHA-256.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, LoadErrorSignature>? Signatures { get; init; }
}

/// <summary>A mod object that must exist exactly once in a stopped world while its mod is installed.</summary>
public sealed record SingletonActor(string Label, string ModPakFileName, string ClassPath);

/// <summary>
/// Batch gates for the live harness mod boots: the known-warning rules, the validated <c>LoadErrors</c>
/// sets, the singleton persistence objects, and the shutdown-duration risk metric.
/// </summary>
public static class ModBootGates
{
    public const string ItqolPakFileName = "ImprovedThrallsAndQoL.pak";

    public const string ItqolValidatedSha256 = "F35D9D927B4E76869D57DC7073B61FCF2215039B628343B28D6B0989A0B48272";

    /// <summary>The hidden server mailbox ITQoL keeps in the world (one per world).</summary>
    public const string ItqolMailboxClassPath =
        "/Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_PL_ServerMailContainer.BP_PL_ServerMailContainer_C";

    /// <summary>The ITQoL mod controller (one per world).</summary>
    public const string ItqolControllerClassPath =
        "/Game/Mods/ImprovedThrallsAndQoL/MC_ImprovedThrallsAndQoL.MC_ImprovedThrallsAndQoL_C";

    public const string AncientRealmsPakFileName = "Ancient_Realms.pak";

    /// <summary>The Ancient Realms file validated in Batch C (6 boots, 3 of them on a quiet host).</summary>
    public const string AncientRealmsValidatedSha256 = "12F7E7192043270FC5F2290C5989F8B8285494BA47A054646790B4A042D1FD1A";

    /// <summary>The Ancient Realms mod controller (one per world).</summary>
    public const string AncientRealmsControllerClassPath =
        "/Game/Mods/Ancient_Realms/AR_BP_ModController.AR_BP_ModController_C";

    /// <summary>A stop at or above this many seconds is WARNING (investigate host load).</summary>
    public const int ShutdownWarningSeconds = 240;

    /// <summary>A stop at or above this many seconds is DEGRADED (no new mod batch until reviewed).</summary>
    public const int ShutdownDegradedSeconds = 300;

    /// <summary>A stop at or above this many seconds reached the EMERGENCY force-kill ceiling.</summary>
    public const int ShutdownEmergencySeconds = 600;

    private const string AncientRealmsReason =
        "Batch C (2026-10-03): dangling package reference (the missing package exists in no mod or vanilla container); " +
        "identical on all six Ancient Realms boots; operator-accepted as a known non-blocking warning for this file only.";

    /// <summary>
    /// The ITQoL mailbox line and the seven validated Ancient Realms lines, each bound to its file's SHA-256.
    /// Everything else (any other ITQoL or Ancient Realms line, any other BP_PL line, any other mod error)
    /// still fails the boot.
    /// </summary>
    public static readonly IReadOnlyList<KnownBootWarning> KnownWarnings =
    [
        new KnownBootWarning(
            "ITQOL-MAILBOX-HEALTHPOOL",
            ItqolPakFileName,
            ItqolValidatedSha256,
            "Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool - DefaultObject not loaded: " +
            ItqolMailboxClassPath,
            "Batch B (2026-10-02): logged on every restart while the mailbox stays a single, unchanged object " +
            "over three restart cycles. Accepted only together with a mailbox count of exactly 1."),
        AncientRealmsLoadError(1, "/Game/Mods/Ancient_Realms/AR_BP_ModController", "35924C262CEDD960"),
        AncientRealmsLoadError(2, "/Game/Mods/Ancient_Realms/AR_BP_ModController", "CFFAB4A08E3DB146"),
        AncientRealmsLoadError(3, "/Game/Mods/Ancient_Realms/Buildings/_MASTER/BP_PL_Decal_Floor_Master", "D0B5ED96C9B95112"),
        AncientRealmsLoadError(4, "/Game/Mods/Ancient_Realms/Buildings/Brick/brick_03/BP_PL_Water_Well_Fountain", "FE8C96EB21683A73"),
        AncientRealmsLoadError(5, "/Game/Mods/Ancient_Realms/Buildings/Ceramic/ceramic_02_white/BP_PL_Water_Well_Fountain_gold", "FE8C96EB21683A73"),
        AncientRealmsLoadError(6, "/Game/Mods/Ancient_Realms/Buildings/Concrete/concrete_04/BP_PL_Water_Well_Fountain", "FE8C96EB21683A73"),
        AncientRealmsLoadError(7, "/Game/Mods/Ancient_Realms/Buildings/Stone/stone_05/BP_PL_Water_Well_Fountain", "FE8C96EB21683A73"),
        new KnownBootWarning(
            "ANCIENT-REALMS-MERGE-DATATABLE-NULL",
            AncientRealmsPakFileName,
            AncientRealmsValidatedSha256,
            "LogModController: Error: AModController::MergeDataTables - ToBeAddedDataTable is null",
            "Surfaced 2026-10-03 when error scanning stopped depending on a mod name (never part of the Batch C set). " +
            "Exactly 2 lines, printed right after the Ancient Realms controller registers, in every boot that mounts " +
            "Ancient_Realms.pak (3 of 3) and in none of the 8 boots without it. Passes only at most twice for this exact " +
            "file; accepted by the operator 2026-10-03 as known non-blocking (not broadened).")
        {
            MaxOccurrences = 2
        }
    ];

    /// <summary>
    /// The exact <c>LoadErrors</c> each validated file produces per boot. A boot fails when a mod's attributed
    /// <c>LoadErrors</c> differ in any way, when a mod without a baseline has any, or when one is attributed to
    /// no installed mod. Both current sets (ITQoL, Batch B; Ancient Realms, Batch C) are operator-accepted known
    /// non-blocking warnings for their exact file versions.
    /// </summary>
    public static readonly IReadOnlyList<LoadErrorBaseline> LoadErrorBaselines =
    [
        new LoadErrorBaseline(
            AncientRealmsPakFileName,
            AncientRealmsValidatedSha256,
            LoadErrorBaselineStatus.KnownNonBlocking,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["/Game/Mods/Ancient_Realms/AR_BP_ModController -> 35924C262CEDD960"] = 1,
                ["/Game/Mods/Ancient_Realms/AR_BP_ModController -> CFFAB4A08E3DB146"] = 1,
                ["/Game/Mods/Ancient_Realms/Buildings/_MASTER/BP_PL_Decal_Floor_Master -> D0B5ED96C9B95112"] = 1,
                ["/Game/Mods/Ancient_Realms/Buildings/Brick/brick_03/BP_PL_Water_Well_Fountain -> FE8C96EB21683A73"] = 1,
                ["/Game/Mods/Ancient_Realms/Buildings/Ceramic/ceramic_02_white/BP_PL_Water_Well_Fountain_gold -> FE8C96EB21683A73"] = 1,
                ["/Game/Mods/Ancient_Realms/Buildings/Concrete/concrete_04/BP_PL_Water_Well_Fountain -> FE8C96EB21683A73"] = 1,
                ["/Game/Mods/Ancient_Realms/Buildings/Stone/stone_05/BP_PL_Water_Well_Fountain -> FE8C96EB21683A73"] = 1,
                ["None -> 1B6F0F85A6FACB01"] = 4,
                ["None -> 1FCB1FA801D75583"] = 2,
                ["None -> 5A19E15D92AF952"] = 9,
                ["None -> 7C3C9C3D45215971"] = 4,
                ["None -> 9F5919676AD734DE"] = 10,
                ["None -> F6DA87602985C3AA"] = 1
            },
            AncientRealmsReason),
        new LoadErrorBaseline(
            ItqolPakFileName,
            ItqolValidatedSha256,
            LoadErrorBaselineStatus.KnownNonBlocking,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["None -> 2876A0A7B1DD1AD8"] = 1,
                ["None -> 37820E3E16D8E6F6"] = 1,
                ["None -> 3C7F626B1C9B9D8C"] = 2,
                ["None -> 3DF389CD30D56D3C"] = 1,
                ["None -> 4039BB7082018681"] = 1,
                ["None -> 6C7B432AC49D7142"] = 1,
                ["None -> 8040D31408A2BA0E"] = 1,
                ["None -> 86A2CF31019311D9"] = 3,
                ["None -> 89B52F3293E329D7"] = 1,
                ["None -> 942F6E15566BC1E4"] = 1,
                ["None -> 9CB1BD6A314F9F36"] = 3,
                ["None -> 9D4E774A46354F2E"] = 1,
                ["None -> A0EB4DCCFC9C56F"] = 2,
                ["None -> B49E9D60D2FBA081"] = 1,
                ["None -> F3F9C875CC712121"] = 1,
                ["None -> FBEAD0918ACBBB39"] = 2
            },
            "Batch B (2026-10-02): 23 dangling-reference LoadErrors per boot, found after acceptance (cc13afa). Attributed " +
            "(2026-10-03) to 17 ITQoL packages (realm templates, a material instance, FollowerDNA altar and door, a mailbox UI " +
            "widget, the humanoid-NPC component); identical in all 12 boots; operator-accepted as a known non-blocking warning " +
            "for this file only."),
        new LoadErrorBaseline(
            SimpleMinimapPakFileName,
            SimpleMinimapValidatedSha256,
            LoadErrorBaselineStatus.KnownNonBlocking,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["None -> C88E5FE76A79516D"] = 1
            },
            "Phase 2 (2026-10-05): one dangling dependent-package reference at world init (frame 0), byte-identical in " +
            "p12-minimap-1, minimap-retest-1 and minimap-retest-2, with no integrity, persistence or controller effect; " +
            "operator-accepted 2026-10-05 as a known non-blocking warning for this exact file, message and frame only.")
        {
            Signatures = new Dictionary<string, LoadErrorSignature>(StringComparer.Ordinal)
            {
                ["None -> C88E5FE76A79516D"] = new LoadErrorSignature(
                    "LoadErrors: While trying to load package None, a dependent package None (C88E5FE76A79516D) was not available. " +
                    "Additional explanatory information follows:",
                    "FPackageName: Unable to identify a valid mount point associated with skipped package None. The package root is unknown.",
                    0)
            }
        }
    ];

    public const string SimpleMinimapPakFileName = "Simple_Minimap.pak";

    /// <summary>The Simple Minimap file validated in Phase 2 (Workshop 3719513784; three boots).</summary>
    public const string SimpleMinimapValidatedSha256 = "04F31A75559665C1A949D7A9A632F9A777CE7D79E926032CFC9FD101B1626AC9";

    /// <summary>Mod objects that must exist exactly once in a stopped world while their mod is installed.</summary>
    public static readonly IReadOnlyList<SingletonActor> SingletonActors =
    [
        new SingletonActor("ITQoL server mailbox", ItqolPakFileName, ItqolMailboxClassPath),
        new SingletonActor("ITQoL mod controller", ItqolPakFileName, ItqolControllerClassPath),
        new SingletonActor("Ancient Realms mod controller", AncientRealmsPakFileName, AncientRealmsControllerClassPath)
    ];

    /// <summary>
    /// Log markers that fail a boot whichever mod (if any) the line names: crash, assertion and persistence/save
    /// errors. Only an exact, version-bound <see cref="KnownWarnings"/> entry is exempt.
    /// </summary>
    public static readonly IReadOnlyList<string> SevereLogMarkers =
    [
        "Persistence: Error",
        "Fatal error",
        "Assertion failed",
        "Ensure condition failed",
        "Unhandled Exception",
        "Critical error"
    ];

    private static readonly Regex LoadErrorLine = new(
        @"^LoadErrors: While trying to load package (?<pkg>\S+), a dependent package None \((?<id>[0-9A-Fa-f]{1,16})\) was not available\. Additional explanatory information follows:$",
        RegexOptions.Compiled);

    private static readonly Regex ErrorSeverity = new(@"(?:^|:)\s*(?:Error|Fatal)\s*:",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Log categories whose warnings can mean a mod broke NPC, spawn, stat, save or world data. Warnings elsewhere
    /// (script, data-table lookups, actor attach, streaming) are base-game volume (1,400+ per boot) and are not
    /// scanned unless they name a mod; every Error and Fatal line of any category is scanned (see below).
    /// </summary>
    private static readonly Regex RelevantWarningFamily = new(
        @"^(?:Log)?(?:NPC\w*|Spawn\w*|Stat\w*|Persistence\w*|Save\w*|Database\w*|SQLite\w*|ModController|ModManager|World\w*|Map\w*)\s*:",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] ProblemMarkers =
        ["Error", "Warning", "Fatal", "Failed", "missing", "not found", "Could not", "Unable"];

    /// <summary>One base-game error kind recorded in every healthy boot before Batch D.</summary>
    public sealed record BaseGameNoiseKind(string Id, string Pattern)
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Regex> Cache = new();

        [System.Text.Json.Serialization.JsonIgnore]
        public Regex Compiled => Cache.GetOrAdd(Pattern, p => new Regex(p, RegexOptions.CultureInvariant));
    }

    private static BaseGameNoiseKind Noise(string id, string pattern) => new(id, pattern);

    /// <summary>
    /// Error messages the unmodified base game logs on every boot. Derived 2026-10-03 from 11 healthy boot logs
    /// (no mods through the seven-mod baseline): each kind below appears in at least 10 of the 11, in the same form
    /// with and without mods. Matching is by message kind, so a changed or new message is never covered; a new kind
    /// of error from any source (including a mod whose line names no mod) stays an unknown problem and fails the boot.
    /// </summary>
    public static readonly IReadOnlyList<BaseGameNoiseKind> BaseGameNoise =
    [
        Noise("ITEMINVENTORY-DLC-MISMATCH", @"^ItemInventory: Error: Data: Mismatch \w+ DLCPackage \S+ for item .+$"),
        Noise("ITEMINVENTORY-FEAT-BLACKLIST", @"^ItemInventory: Error: Data: Map feat blacklist table not found, blacklist will not function\.$"),
        Noise("BINK-TITLE-MOVIE", @"^LogBinkMoviePlayer: Error: UBinkMediaPlayer::Open: Failed! BinkHLOpen failed\. URL: .+$"),
        Noise("DEV-ASSETGROUP-STREAMING", @"^LogLevelStreaming: Error: Couldn't find file for package /Game/Developers/MasonRoy/AssetGroups/AG_TEMP_\w+\.$"),
        Noise("WILDLIFE-AILOD3-MOVEMENT", @"^LogTemp: Error: Character 'BP_NPC_Wildlife_\w+' is in an unsafe movement move \(currently \d+\) while in AILOD3$"),
        Noise("NPC-SPAWNER-DESPAWN-ENTRY", @"^NPC: Error: Code: UNpcSpawnerComponent::Despawned - Invalid spawn entry supplied, Type: \d+, Index: \d+$"),
        // Exactly the two ids every healthy boot logs (once each). A different id means new spawn content references a
        // missing table, so it must not be hidden here (found in the Shemite boot: Catacomb_Wretch x5 and
        // Wildlife_SiptahTwoHornedRhino_Baby x1, absent from all 14 earlier logs).
        Noise("SPAWNTABLE-WEIGHTED-TABLE", @"^SpawnTable: Error: Data: USpawnTableLibrary::SpawnNPCFromWeightedTable - could not find weighted table with id: (?:WarTestLongLeash|Wildlife_Siptah_Firstman_Warrior4)$"),
        // Vanilla priest variants (operator decision A, 2026-10-07): the game picks a random priest variant per boot and
        // some variants have no weighted table. Proven with no mods installed (vanilla 2.2.3: Exile_Priest_4_Nordheimer;
        // vanilla 3.0.0 CL-378787: Exile_OrchidPriest_4_Nordheimer); Exile_Priest_4_Hyrkanian seen in modded boots,
        // including before Mod #14 existed. Only these two id families; any other id stays unknown.
        Noise("SPAWNTABLE-PRIEST-VARIANT", @"^SpawnTable: Error: Data: USpawnTableLibrary::SpawnNPCFromWeightedTable - could not find weighted table with id: Exile_(?:Orchid)?Priest_4_[A-Za-z]+$"),
        Noise("BASESPAWNER-MODULE", @"^LogBaseSpawner: Error: ABaseSpawner::TickSpawnBase - Failed to spawn module from BP_HL_Build\w+_T2_C\.$"),
        Noise("BUILDING-STABILITY", @"^building: Error: Code: ABuildingBase::AddModule_Internal - Removing placed module that did not manage to find stability\.$")
    ];

    /// <summary>The base-game noise kind a log line matches, or null.</summary>
    public static string? MatchBaseGameNoise(string line) => MatchBaseGameNoise(line, BaseGameNoise);

    /// <summary>The kind in <paramref name="noise"/> a log line matches, or null.</summary>
    public static string? MatchBaseGameNoise(string line, IEnumerable<BaseGameNoiseKind> noise)
    {
        var message = StripLogPrefix(line).TrimEnd();
        return noise.FirstOrDefault(k => k.Compiled.IsMatch(message))?.Id;
    }

    /// <summary>
    /// Selects every error/fatal line of any category, every severe diagnostic, relevant-category warnings and
    /// mod-related problems, minus the recorded <see cref="BaseGameNoise"/> kinds. Keeps duplicate occurrences.
    /// LoadErrors have their own mandatory exact-multiset gate. Selection never depends on a generic error naming
    /// the responsible mod.
    /// </summary>
    public static IReadOnlyList<string> SelectProblemLines(IEnumerable<string> lines, IEnumerable<string> modStems) =>
        SelectProblemLines(lines, modStems, ValidatedCatalog.Current);

    /// <summary>As above, against an explicit (for example recorded) validated catalog.</summary>
    public static IReadOnlyList<string> SelectProblemLines(IEnumerable<string> lines, IEnumerable<string> modStems, ValidatedCatalog catalog)
    {
        var stems = modStems.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        return lines.Where(line => IsProblemLine(line, stems, catalog)).ToList();
    }

    /// <summary>Whether one log line is selected by <see cref="SelectProblemLines(IEnumerable{string}, IEnumerable{string}, ValidatedCatalog)"/>.</summary>
    public static bool IsProblemLine(string line, IReadOnlyCollection<string> modStems, ValidatedCatalog catalog)
    {
        var message = StripLogPrefix(line);
        if (message.Contains("LoadErrors:", StringComparison.OrdinalIgnoreCase))
            return false; // ParseLoadErrors retains malformed lines and the caller gates all entries.
        if (MatchBaseGameNoise(line, catalog.BaseGameNoise) is not null)
            return false;
        if (ErrorSeverity.IsMatch(message) ||
            catalog.SevereLogMarkers.Any(m => message.Contains(m, StringComparison.OrdinalIgnoreCase)))
            return true;
        // The problem-word test must not see a mod's own name: "NightTerrors" contains "error", so every ordinary
        // mount line for that mod looked like a problem. Mask each mod stem out of the text used for that test only;
        // an Error/Fatal severity, a severe marker or a real problem word elsewhere in the line is still caught above
        // and here, and "names a mod" below still uses the original message.
        var markerText = message;
        foreach (var stem in modStems)
        {
            if (!string.IsNullOrWhiteSpace(stem))
                markerText = markerText.Replace(stem, " ", StringComparison.OrdinalIgnoreCase);
        }

        return ProblemMarkers.Any(m => markerText.Contains(m, StringComparison.OrdinalIgnoreCase)) &&
            (RelevantWarningFamily.IsMatch(message) ||
             modStems.Any(s => !string.IsNullOrWhiteSpace(s) && message.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
             message.Contains("modlist", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("Failed to mount", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The log message at which the main world's teardown begins (its <c>BeginTearingDown</c> line). A phase-bound
    /// warning is judged against the first such line that follows the engine exit request.
    /// </summary>
    public const string MainWorldTeardownMessage = "LogWorld: BeginTearingDown for /Game/Maps/ConanSandbox/ConanSandbox";

    public const string EngineExitRequestMarker = "Engine exit requested";

    /// <summary>
    /// Index of the main-world teardown line that follows the first engine exit request, or null when the log shows
    /// no such teardown (so nothing can be proven to belong to it).
    /// </summary>
    public static int? FindTeardownStart(IReadOnlyList<string> lines)
    {
        var exitRequested = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var message = StripLogPrefix(lines[i]).TrimEnd();
            if (exitRequested < 0)
            {
                if (message.Contains(EngineExitRequestMarker, StringComparison.Ordinal))
                    exitRequested = i;
            }
            else if (string.Equals(message, MainWorldTeardownMessage, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>The Cannibal Captivity file validated for <see cref="PhaseBoundWarnings"/> (2026-10-03/04, two runs).</summary>
    public const string CannibalCaptivityPakFileName = "Cannibal_Captivity.pak";

    public const string CannibalCaptivityValidatedSha256 = "DB6E3C299912E48E4DEC8293A58C8DEF1D881FDBDC44F1E67CE99A9BF348F04F";

    /// <summary>
    /// Warnings accepted only as a complete set, in one phase, for one exact file. Unlike <see cref="KnownWarnings"/>
    /// these are judged on the whole boot log: the set is accepted only when the file hash matches, every matching
    /// line is in the required phase (after the main-world teardown begins), and the count is exactly
    /// <see cref="PhaseBoundWarning.ExactCount"/>. A 100th line, a line before teardown, a changed path or signature
    /// or a changed hash leaves every matching line an unknown problem.
    /// </summary>
    public static readonly IReadOnlyList<PhaseBoundWarning> PhaseBoundWarnings =
    [
        new PhaseBoundWarning(
            "CANNIBAL-CAPTIVITY-TEARDOWN-NO-WORLD",
            CannibalCaptivityPakFileName,
            CannibalCaptivityValidatedSha256,
            @"^LogScript: Warning: Script Msg: No world was found for object \(/Game/Mods/Cannibal_Captivity/Base/CannibalCaptivityLevel\.CannibalCaptivityLevel:PersistentLevel\.[^()\s]+\) passed in to UEngine::GetWorldFromContextObject\(\)\.$",
            99,
            "Cannibal Captivity (2026-10-03/04): 99 identical LogScript warnings from the mod's level instance while the world is torn down " +
            "after shutdown; zero while online. Observed in two controlled runs (same count, same object-path family, same position in the " +
            "log, 84-95 ms burst) with quick_check ok, singleton objects intact and NORMAL clean stops. Operator-accepted as a known " +
            "non-blocking server-side teardown warning for this exact file; in-game behavior is not verified.")
    ];

    private static KnownBootWarning AncientRealmsLoadError(int number, string package, string missingPackageId) =>
        new($"ANCIENT-REALMS-DANGLING-REF-{number}",
            AncientRealmsPakFileName,
            AncientRealmsValidatedSha256,
            $"LoadErrors: While trying to load package {package}, a dependent package None ({missingPackageId}) was not available. " +
            "Additional explanatory information follows:",
            AncientRealmsReason);

    /// <summary>Every line containing a <c>LoadErrors:</c> marker, including unparsed lines, in log order.</summary>
    public static IReadOnlyList<LoadErrorEntry> ParseLoadErrors(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var all = lines as IReadOnlyList<string> ?? lines.ToList();
        var entries = new List<LoadErrorEntry>();
        for (var i = 0; i < all.Count; i++)
        {
            var l = all[i];
            if (!l.Contains("LoadErrors:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var message = StripLogPrefix(l);
            var match = LoadErrorLine.Match(message);
            entries.Add(match.Success
                ? new LoadErrorEntry(match.Groups["pkg"].Value, match.Groups["id"].Value.ToUpperInvariant())
                {
                    Message = message,
                    DetailLine = i + 1 < all.Count ? all[i + 1].TrimEnd('\r', '\n') : null,
                    Frame = LogFrame(l)
                }
                : new LoadErrorEntry("<unparsed>", "") { RawUnparsedLine = l });
        }

        return entries;
    }

    private static int? LogFrame(string line)
    {
        var match = LogPrefix.Match(line);
        return match.Success ? int.Parse(match.Groups["frame"].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// Compares the <c>LoadErrors</c> attributed to one installed mod with its validated baseline. Passes only on
    /// an exact multiset match with the validated file, or when the mod produced none and has no baseline.
    /// </summary>
    public static BootGateResult EvaluateLoadErrors(string modPakFileName, string? installedSha256, IReadOnlyList<LoadErrorEntry> attributed) =>
        EvaluateLoadErrors(modPakFileName, installedSha256, attributed, ValidatedCatalog.Current);

    /// <summary>As above, against an explicit (for example recorded) validated catalog.</summary>
    public static BootGateResult EvaluateLoadErrors(string modPakFileName, string? installedSha256, IReadOnlyList<LoadErrorEntry> attributed, ValidatedCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(attributed);
        if (attributed.Any(e => e.RawUnparsedLine is not null))
            return new BootGateResult(false, "UNKNOWN: unparsed LoadErrors line");
        var baseline = catalog.LoadErrorBaselines.FirstOrDefault(b =>
            string.Equals(b.ModPakFileName, modPakFileName, StringComparison.OrdinalIgnoreCase));
        var hashMatches = baseline is not null &&
                          string.Equals(installedSha256, baseline.ValidatedPakSha256, StringComparison.OrdinalIgnoreCase);

        if (baseline is null || !hashMatches)
        {
            return attributed.Count == 0
                ? new BootGateResult(true, "no LoadErrors")
                : new BootGateResult(false, baseline is null
                    ? $"{attributed.Count} LoadErrors and no validated set for this mod"
                    : $"{attributed.Count} LoadErrors; the file is not the validated version (SHA-256 {baseline.ValidatedPakSha256[..8]}...), so its validated set does not apply");
        }

        var observed = attributed.GroupBy(e => e.ToString(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var extra = observed
            .Where(kv => kv.Value > baseline.Expected.GetValueOrDefault(kv.Key))
            .Select(kv => $"{kv.Key} x{kv.Value - baseline.Expected.GetValueOrDefault(kv.Key)}")
            .ToList();
        var missing = baseline.Expected
            .Where(kv => kv.Value > observed.GetValueOrDefault(kv.Key))
            .Select(kv => $"{kv.Key} x{kv.Value - observed.GetValueOrDefault(kv.Key)}")
            .ToList();
        var status = baseline.Status == LoadErrorBaselineStatus.KnownNonBlocking
            ? "KNOWN NON-BLOCKING"
            : "PENDING OPERATOR CLASSIFICATION (monitored, not accepted)";
        if (extra.Count != 0 || missing.Count != 0)
        {
            return new BootGateResult(false,
                $"differs from the validated set: new/extra [{string.Join("; ", extra)}] missing [{string.Join("; ", missing)}]");
        }

        // Signature-bound entries must also match their exact two-line form and frame (fail closed when unknown).
        var drift = baseline.Signatures is null
            ? []
            : attributed.Where(e => baseline.Signatures.TryGetValue(e.ToString(), out var sig) &&
                                    !(string.Equals(e.Message, sig.ExactMessage, StringComparison.Ordinal) &&
                                      string.Equals(e.DetailLine, sig.ExactDetailLine, StringComparison.Ordinal) &&
                                      (sig.RequiredFrame is null || e.Frame == sig.RequiredFrame)))
                .Select(e => $"{e} (frame {e.Frame?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"})")
                .ToList();
        return drift.Count == 0
            ? new BootGateResult(true, $"{status}: {attributed.Count} LoadErrors = validated set (exact)" +
                                       (baseline.Signatures is null ? "" : $"; {baseline.Signatures.Count} exact signature(s) and frame(s) match"))
            : new BootGateResult(false, $"validated set matches but the exact message, explanatory line or frame drifted: [{string.Join("; ", drift)}]");
    }

    /// <summary>
    /// With its mod installed, the stopped world must hold exactly one of the object: 0 means it disappeared,
    /// more than 1 means it was duplicated, null means it could not be read.
    /// </summary>
    public static BootGateResult EvaluateSingletonActor(SingletonActor actor, bool installed, int? count)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!installed)
        {
            return new BootGateResult(true, $"not applicable ({actor.ModPakFileName} not installed)");
        }

        return count switch
        {
            null => new BootGateResult(false, $"{actor.Label} count unavailable (world database could not be read)"),
            1 => new BootGateResult(true, $"1 {actor.Label} (expected exactly 1)"),
            0 => new BootGateResult(false, $"MISSING: 0 {actor.Label} (expected exactly 1)"),
            _ => new BootGateResult(false, $"DUPLICATE: {count} {actor.Label} (expected exactly 1)")
        };
    }

    private static readonly Regex LogPrefix = new(@"^\[[^\]]*\]\[\s*(?<frame>\d+)\]", RegexOptions.Compiled);

    /// <summary>The log line without its <c>[timestamp][frame]</c> prefix.</summary>
    public static string StripLogPrefix(string line) =>
        LogPrefix.Replace(line.TrimEnd('\r', '\n'), string.Empty, 1);

    /// <summary>
    /// Splits mod-related problem lines into real problems and known non-blocking warnings.
    /// <paramref name="installedPakSha256"/> maps installed <c>.pak</c> file names to their SHA-256.
    /// </summary>
    public static (IReadOnlyList<string> Problems, IReadOnlyList<(KnownBootWarning Warning, string Line)> Known) ClassifyProblems(
        IEnumerable<string> problemLines,
        IReadOnlyDictionary<string, string> installedPakSha256) =>
        ClassifyProblems(problemLines, installedPakSha256, ValidatedCatalog.Current);

    /// <summary>As above, against an explicit (for example recorded) validated catalog.</summary>
    public static (IReadOnlyList<string> Problems, IReadOnlyList<(KnownBootWarning Warning, string Line)> Known) ClassifyProblems(
        IEnumerable<string> problemLines,
        IReadOnlyDictionary<string, string> installedPakSha256,
        ValidatedCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(problemLines);
        ArgumentNullException.ThrowIfNull(installedPakSha256);
        ArgumentNullException.ThrowIfNull(catalog);

        var problems = new List<string>();
        var known = new List<(KnownBootWarning, string)>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in problemLines)
        {
            var message = StripLogPrefix(line);
            var rule = catalog.KnownWarnings.FirstOrDefault(w =>
                string.Equals(message, w.ExactMessage, StringComparison.Ordinal) &&
                installedPakSha256.TryGetValue(w.ModPakFileName, out var sha256) &&
                string.Equals(sha256, w.ValidatedPakSha256, StringComparison.OrdinalIgnoreCase));
            if (rule is not null)
            {
                var count = seen[rule.Id] = seen.GetValueOrDefault(rule.Id) + 1;
                if (count > rule.MaxOccurrences)
                {
                    rule = null; // more occurrences than were validated: the extra lines are unknown problems
                }
            }

            if (rule is null)
            {
                problems.Add(line);
            }
            else
            {
                known.Add((rule, line));
            }
        }

        return (problems, known);
    }

    /// <summary>
    /// With ITQoL installed the world must hold exactly one server mailbox after a boot:
    /// 0 means it disappeared, more than 1 means it was duplicated, null means it could not be read.
    /// </summary>
    public static BootGateResult EvaluateItqolMailbox(bool itqolInstalled, int? mailboxCount) =>
        EvaluateSingletonActor(SingletonActors[0], itqolInstalled, mailboxCount);

    public static ShutdownSeverity ClassifyShutdownDuration(TimeSpan duration) => duration.TotalSeconds switch
    {
        >= ShutdownEmergencySeconds => ShutdownSeverity.Emergency,
        >= ShutdownDegradedSeconds => ShutdownSeverity.Degraded,
        >= ShutdownWarningSeconds => ShutdownSeverity.Warning,
        _ => ShutdownSeverity.Normal
    };

    /// <summary>
    /// Shutdown duration metric (operator policy, 2026-10-03). The long phase is base-game, single-threaded
    /// work that host CPU load stretches, so duration is never a mod compatibility failure. <c>Pass</c> means
    /// another mod batch may be added: NORMAL and WARNING pass, DEGRADED and EMERGENCY block it until reviewed.
    /// The force-kill decision belongs to the stop policy, not to this metric.
    /// </summary>
    public static BootGateResult EvaluateShutdownDuration(TimeSpan duration, int gracefulWindowSeconds, int? emergencyCeilingSeconds = null)
    {
        var seconds = duration.TotalSeconds;
        var limits = emergencyCeilingSeconds is int ceiling
            ? $"graceful window {gracefulWindowSeconds} s, emergency ceiling {ceiling} s"
            : $"graceful window {gracefulWindowSeconds} s";
        return ClassifyShutdownDuration(duration) switch
        {
            ShutdownSeverity.Normal => new BootGateResult(true,
                $"NORMAL: {seconds:0.0} s < {ShutdownWarningSeconds} s ({limits})"),
            ShutdownSeverity.Warning => new BootGateResult(true,
                $"WARNING: {seconds:0.0} s in {ShutdownWarningSeconds}-{ShutdownDegradedSeconds} s ({limits}). " +
                "Investigate host load; not a mod compatibility failure."),
            ShutdownSeverity.Degraded => new BootGateResult(false,
                $"DEGRADED: {seconds:0.0} s in {ShutdownDegradedSeconds}-{ShutdownEmergencySeconds} s ({limits}). " +
                "Do not add another mod batch until reviewed; not a mod compatibility failure."),
            _ => new BootGateResult(false,
                $"EMERGENCY: {seconds:0.0} s >= {ShutdownEmergencySeconds} s ({limits}). " +
                "The force-kill ceiling was reached; review before any further batch.")
        };
    }
}

/// <summary>Shutdown duration classes (operator policy, 2026-10-03).</summary>
public enum ShutdownSeverity
{
    /// <summary>Below 240 s.</summary>
    Normal,

    /// <summary>240-300 s: investigate host load.</summary>
    Warning,

    /// <summary>300-600 s: no new mod batch until reviewed.</summary>
    Degraded,

    /// <summary>600 s or more: the emergency force-kill ceiling.</summary>
    Emergency
}
