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

/// <summary>
/// Batch gates for the live harness mod boots: the known-warning rule, the Improved Thralls &amp; QoL
/// server mailbox count, and the shutdown-duration risk metric.
/// </summary>
public static class ModBootGates
{
    public const string ItqolPakFileName = "ImprovedThrallsAndQoL.pak";

    /// <summary>The hidden server mailbox ITQoL keeps in the world (one per world).</summary>
    public const string ItqolMailboxClassPath =
        "/Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_PL_ServerMailContainer.BP_PL_ServerMailContainer_C";

    /// <summary>A stop at or above this many seconds is HIGH RISK (the graceful window is 300 s).</summary>
    public const int ShutdownHighRiskSeconds = 240;

    /// <summary>
    /// Exactly one rule. Everything else (any other ITQoL line, any other BP_PL line, any other mod
    /// error) still fails the boot.
    /// </summary>
    public static readonly IReadOnlyList<KnownBootWarning> KnownWarnings =
    [
        new KnownBootWarning(
            "ITQOL-MAILBOX-HEALTHPOOL",
            ItqolPakFileName,
            "F35D9D927B4E76869D57DC7073B61FCF2215039B628343B28D6B0989A0B48272",
            "Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool - DefaultObject not loaded: " +
            ItqolMailboxClassPath,
            "Batch B (2026-10-02): logged on every restart while the mailbox stays a single, unchanged object " +
            "over three restart cycles. Accepted only together with a mailbox count of exactly 1.")
    ];

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
    public static BootGateResult EvaluateItqolMailbox(bool itqolInstalled, int? mailboxCount)
    {
        if (!itqolInstalled)
        {
            return new BootGateResult(true, $"not applicable ({ItqolPakFileName} not installed)");
        }

        return mailboxCount switch
        {
            null => new BootGateResult(false, "mailbox count unavailable (world database could not be read)"),
            1 => new BootGateResult(true, "1 mailbox (expected exactly 1)"),
            0 => new BootGateResult(false, "MISSING: 0 mailboxes (expected exactly 1)"),
            _ => new BootGateResult(false, $"DUPLICATE: {mailboxCount} mailboxes (expected exactly 1)")
        };
    }

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
