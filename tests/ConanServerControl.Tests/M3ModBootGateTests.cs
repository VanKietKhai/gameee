using ConanServerControl.Core.LiveTesting;

namespace ConanServerControl.Tests;

/// <summary>
/// Batch B gates for the live harness: the single exact, version-bound ITQoL mailbox known warning,
/// the mailbox count gate, and the shutdown-duration risk metric. Fully offline.
/// </summary>
public sealed class M3ModBootGateTests
{
    private const string ValidatedItqolSha256 = "F35D9D927B4E76869D57DC7073B61FCF2215039B628343B28D6B0989A0B48272";

    private const string KnownLine =
        "[2026.10.02-07.43.33:923][  0]Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool - " +
        "DefaultObject not loaded: /Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_PL_ServerMailContainer.BP_PL_ServerMailContainer_C";

    private static Dictionary<string, string> Installed(string? itqolSha256 = ValidatedItqolSha256) =>
        itqolSha256 is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ImprovedThrallsAndQoL.pak"] = itqolSha256 };

    // ------------------------------------------------------------ Known warning rule

    [Fact]
    public void Exactly_one_known_warning_rule_exists_and_it_names_the_full_mailbox_line()
    {
        var rule = Assert.Single(ModBootGates.KnownWarnings);
        Assert.Equal("ITQOL-MAILBOX-HEALTHPOOL", rule.Id);
        Assert.Equal("ImprovedThrallsAndQoL.pak", rule.ModPakFileName);
        Assert.Equal(ValidatedItqolSha256, rule.ValidatedPakSha256);
        Assert.EndsWith(ModBootGates.ItqolMailboxClassPath, rule.ExactMessage, StringComparison.Ordinal);
        Assert.StartsWith("Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool", rule.ExactMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(KnownLine)]
    [InlineData(KnownLine + "\r")]
    [InlineData("[2026.10.02-07.29.58:596][  0]Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool - DefaultObject not loaded: /Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_PL_ServerMailContainer.BP_PL_ServerMailContainer_C")]
    public void Exact_mailbox_line_with_the_validated_itqol_version_is_known_non_blocking(string line)
    {
        var (problems, known) = ModBootGates.ClassifyProblems([line], Installed());

        Assert.Empty(problems);
        Assert.Equal("ITQOL-MAILBOX-HEALTHPOOL", Assert.Single(known).Warning.Id);
    }

    [Fact]
    public void Validated_hash_comparison_ignores_hex_case()
    {
        var (problems, known) = ModBootGates.ClassifyProblems([KnownLine], Installed(ValidatedItqolSha256.ToLowerInvariant()));

        Assert.Empty(problems);
        Assert.Single(known);
    }

    [Fact]
    public void Mailbox_line_fails_when_the_installed_itqol_is_a_different_version()
    {
        var (problems, known) = ModBootGates.ClassifyProblems([KnownLine], Installed(new string('A', 64)));

        Assert.Equal(KnownLine, Assert.Single(problems));
        Assert.Empty(known);
    }

    [Fact]
    public void Mailbox_line_fails_when_itqol_is_not_installed()
    {
        var (problems, known) = ModBootGates.ClassifyProblems([KnownLine], Installed(itqolSha256: null));

        Assert.Single(problems);
        Assert.Empty(known);
    }

    [Theory]
    // a different ITQoL persistence error
    [InlineData("[2026.10.02-07.43.33:923][  0]Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool - DefaultObject not loaded: /Game/Mods/ImprovedThrallsAndQoL/Altar/BP_PL_AltarOfTheFallen.BP_PL_AltarOfTheFallen_C")]
    // any other ITQoL warning
    [InlineData("[2026.10.02-07.43.33:923][  0]LogActor: Warning: Lamplighter_Sphere attached from /Game/Mods/ImprovedThrallsAndQoL/Thralls/BP_Lamplighter.BP_Lamplighter_C")]
    // the same mailbox class with a different message
    [InlineData("[2026.10.02-07.43.33:923][  0]Persistence: Error: Code: Failed to load /Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_PL_ServerMailContainer.BP_PL_ServerMailContainer_C")]
    // the known message as a warning instead of an error
    [InlineData("[2026.10.02-07.43.33:923][  0]Persistence: Warning: Code: UConanBuildingPersistenceComponent::CreateHealthPool - DefaultObject not loaded: /Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_PL_ServerMailContainer.BP_PL_ServerMailContainer_C")]
    // the known message with extra text appended
    [InlineData(KnownLine + " (retry 2)")]
    // the same CreateHealthPool failure for another mod's BP_PL placeable
    [InlineData("[2026.10.02-07.43.33:923][  0]Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool - DefaultObject not loaded: /Game/Mods/WO_RidingThralls/BP_PL_Registrar.BP_PL_Registrar_C")]
    // any other mod error
    [InlineData("[2026.10.02-07.43.33:923][  0]LogModManager: Error: Failed to mount ThrallReputation.pak")]
    public void Any_other_line_still_fails(string line)
    {
        var (problems, known) = ModBootGates.ClassifyProblems([line], Installed());

        Assert.Equal(line, Assert.Single(problems));
        Assert.Empty(known);
    }

    [Fact]
    public void Known_warning_does_not_hide_other_problems_in_the_same_boot()
    {
        const string other = "[2026.10.02-07.43.34:001][  1]Persistence: Error: Code: something else in /Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_Mailbox.BP_Mailbox_C";

        var (problems, known) = ModBootGates.ClassifyProblems([KnownLine, other], Installed());

        Assert.Equal(other, Assert.Single(problems));
        Assert.Single(known);
    }

    [Fact]
    public void Log_prefix_is_stripped_only_once_at_the_start()
    {
        Assert.Equal("LogTemp: Error: [x][1] inside", ModBootGates.StripLogPrefix("[2026.10.02-07.43.33:923][ 12]LogTemp: Error: [x][1] inside\r"));
        Assert.Equal("no prefix", ModBootGates.StripLogPrefix("no prefix"));
    }

    // ------------------------------------------------------------ Mailbox count gate

    [Fact]
    public void Mailbox_gate_passes_with_exactly_one_mailbox()
    {
        Assert.True(ModBootGates.EvaluateItqolMailbox(itqolInstalled: true, mailboxCount: 1).Pass);
    }

    [Theory]
    [InlineData(0, "MISSING")]
    [InlineData(2, "DUPLICATE")]
    [InlineData(3, "DUPLICATE")]
    public void Mailbox_gate_fails_when_the_mailbox_disappears_or_is_duplicated(int count, string expected)
    {
        var gate = ModBootGates.EvaluateItqolMailbox(itqolInstalled: true, mailboxCount: count);

        Assert.False(gate.Pass);
        Assert.StartsWith(expected, gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Mailbox_gate_fails_when_the_world_cannot_be_read()
    {
        Assert.False(ModBootGates.EvaluateItqolMailbox(itqolInstalled: true, mailboxCount: null).Pass);
    }

    [Fact]
    public void Mailbox_gate_is_not_applicable_without_itqol()
    {
        var gate = ModBootGates.EvaluateItqolMailbox(itqolInstalled: false, mailboxCount: null);

        Assert.True(gate.Pass);
        Assert.StartsWith("not applicable", gate.Detail, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ Shutdown duration metric

    [Theory]
    [InlineData(70.0)]
    [InlineData(177.0)]
    [InlineData(183.7)]
    [InlineData(239.9)]
    public void Shutdown_below_240_seconds_passes(double seconds)
    {
        Assert.True(ModBootGates.EvaluateShutdownDuration(TimeSpan.FromSeconds(seconds), 300).Pass);
    }

    [Theory]
    [InlineData(240.0)]
    [InlineData(275.5)]
    [InlineData(300.0)]
    public void Shutdown_at_or_above_240_seconds_is_high_risk(double seconds)
    {
        var gate = ModBootGates.EvaluateShutdownDuration(TimeSpan.FromSeconds(seconds), 300);

        Assert.False(gate.Pass);
        Assert.StartsWith("HIGH RISK", gate.Detail, StringComparison.Ordinal);
    }
}
