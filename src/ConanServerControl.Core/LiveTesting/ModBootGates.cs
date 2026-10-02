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
    string Reason);

/// <summary>Result of one live-harness gate.</summary>
public sealed record BootGateResult(bool Pass, string Detail);

/// <summary>One <c>LoadErrors</c> line: the package being loaded and the missing dependency's package id.</summary>
public sealed record LoadErrorEntry(string Package, string MissingPackageId)
{
    public override string ToString() => $"{Package} -> {MissingPackageId}";
}

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
    string Reason);

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

    /// <summary>A stop at or above this many seconds is HIGH RISK (the graceful window is 300 s).</summary>
    public const int ShutdownHighRiskSeconds = 240;

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
        AncientRealmsLoadError(7, "/Game/Mods/Ancient_Realms/Buildings/Stone/stone_05/BP_PL_Water_Well_Fountain", "FE8C96EB21683A73")
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
            "for this file only.")
    ];

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
        @"LoadErrors: While trying to load package (?<pkg>\S+), a dependent package None \((?<id>[0-9A-Fa-f]+)\) was not available",
        RegexOptions.Compiled);

    private static KnownBootWarning AncientRealmsLoadError(int number, string package, string missingPackageId) =>
        new($"ANCIENT-REALMS-DANGLING-REF-{number}",
            AncientRealmsPakFileName,
            AncientRealmsValidatedSha256,
            $"LoadErrors: While trying to load package {package}, a dependent package None ({missingPackageId}) was not available. " +
            "Additional explanatory information follows:",
            AncientRealmsReason);

    /// <summary>Every <c>LoadErrors: … dependent package None (id) was not available</c> line, in log order.</summary>
    public static IReadOnlyList<LoadErrorEntry> ParseLoadErrors(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.Select(l => LoadErrorLine.Match(l))
            .Where(m => m.Success)
            .Select(m => new LoadErrorEntry(m.Groups["pkg"].Value, m.Groups["id"].Value.ToUpperInvariant()))
            .ToList();
    }

    /// <summary>
    /// Compares the <c>LoadErrors</c> attributed to one installed mod with its validated baseline. Passes only on
    /// an exact multiset match with the validated file, or when the mod produced none and has no baseline.
    /// </summary>
    public static BootGateResult EvaluateLoadErrors(string modPakFileName, string? installedSha256, IReadOnlyList<LoadErrorEntry> attributed)
    {
        ArgumentNullException.ThrowIfNull(attributed);
        var baseline = LoadErrorBaselines.FirstOrDefault(b =>
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
        return extra.Count == 0 && missing.Count == 0
            ? new BootGateResult(true, $"{status}: {attributed.Count} LoadErrors = validated set (exact)")
            : new BootGateResult(false,
                $"differs from the validated set: new/extra [{string.Join("; ", extra)}] missing [{string.Join("; ", missing)}]");
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

    private static readonly Regex LogPrefix = new(@"^\[[^\]]*\]\[\s*\d+\]", RegexOptions.Compiled);

    /// <summary>The log line without its <c>[timestamp][frame]</c> prefix.</summary>
    public static string StripLogPrefix(string line) =>
        LogPrefix.Replace(line.TrimEnd('\r', '\n'), string.Empty, 1);

    /// <summary>
    /// Splits mod-related problem lines into real problems and known non-blocking warnings.
    /// <paramref name="installedPakSha256"/> maps installed <c>.pak</c> file names to their SHA-256.
    /// </summary>
    public static (IReadOnlyList<string> Problems, IReadOnlyList<(KnownBootWarning Warning, string Line)> Known) ClassifyProblems(
        IEnumerable<string> problemLines,
        IReadOnlyDictionary<string, string> installedPakSha256)
    {
        ArgumentNullException.ThrowIfNull(problemLines);
        ArgumentNullException.ThrowIfNull(installedPakSha256);

        var problems = new List<string>();
        var known = new List<(KnownBootWarning, string)>();
        foreach (var line in problemLines)
        {
            var message = StripLogPrefix(line);
            var rule = KnownWarnings.FirstOrDefault(w =>
                string.Equals(message, w.ExactMessage, StringComparison.Ordinal) &&
                installedPakSha256.TryGetValue(w.ModPakFileName, out var sha256) &&
                string.Equals(sha256, w.ValidatedPakSha256, StringComparison.OrdinalIgnoreCase));
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

    /// <summary>
    /// Shutdown duration metric: at or above <see cref="ShutdownHighRiskSeconds"/> is HIGH RISK, so the batch gate
    /// fails. This is a compatibility judgement only; the force-kill decision belongs to the stop policy
    /// (graceful window, then the emergency ceiling for a proven shutdown).
    /// </summary>
    public static BootGateResult EvaluateShutdownDuration(TimeSpan duration, int gracefulWindowSeconds, int? emergencyCeilingSeconds = null)
    {
        var seconds = duration.TotalSeconds;
        var limits = emergencyCeilingSeconds is int ceiling
            ? $"graceful window {gracefulWindowSeconds} s, emergency ceiling {ceiling} s"
            : $"graceful window {gracefulWindowSeconds} s";
        return seconds >= ShutdownHighRiskSeconds
            ? new BootGateResult(false,
                $"HIGH RISK: {seconds:0.0} s >= {ShutdownHighRiskSeconds} s ({limits}). " +
                "Stop before adding another batch.")
            : new BootGateResult(true,
                $"OK: {seconds:0.0} s < {ShutdownHighRiskSeconds} s ({limits})");
    }
}
