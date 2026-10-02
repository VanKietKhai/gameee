using ConanServerControl.Core.LiveTesting;

namespace ConanServerControl.Tests;

/// <summary>
/// Live harness batch gates: the exact, version-bound known warnings (ITQoL mailbox, Batch B; the seven Ancient
/// Realms dangling references, Batch C), the validated LoadErrors sets, the singleton persistence objects, and
/// the shutdown-duration risk metric. Fully offline.
/// </summary>
public sealed class M3ModBootGateTests
{
    private const string ValidatedItqolSha256 = "F35D9D927B4E76869D57DC7073B61FCF2215039B628343B28D6B0989A0B48272";

    private const string ValidatedAncientRealmsSha256 = "12F7E7192043270FC5F2290C5989F8B8285494BA47A054646790B4A042D1FD1A";

    private const string KnownLine =
        "[2026.10.02-07.43.33:923][  0]Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool - " +
        "DefaultObject not loaded: /Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_PL_ServerMailContainer.BP_PL_ServerMailContainer_C";

    /// <summary>The seven Ancient Realms lines exactly as logged on every validated boot (2026-10-02/03).</summary>
    private static readonly string[] AncientRealmsLines =
    [
        "LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/AR_BP_ModController, a dependent package None (35924C262CEDD960) was not available. Additional explanatory information follows:",
        "LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/AR_BP_ModController, a dependent package None (CFFAB4A08E3DB146) was not available. Additional explanatory information follows:",
        "LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/_MASTER/BP_PL_Decal_Floor_Master, a dependent package None (D0B5ED96C9B95112) was not available. Additional explanatory information follows:",
        "LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Brick/brick_03/BP_PL_Water_Well_Fountain, a dependent package None (FE8C96EB21683A73) was not available. Additional explanatory information follows:",
        "LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Ceramic/ceramic_02_white/BP_PL_Water_Well_Fountain_gold, a dependent package None (FE8C96EB21683A73) was not available. Additional explanatory information follows:",
        "LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Concrete/concrete_04/BP_PL_Water_Well_Fountain, a dependent package None (FE8C96EB21683A73) was not available. Additional explanatory information follows:",
        "LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Stone/stone_05/BP_PL_Water_Well_Fountain, a dependent package None (FE8C96EB21683A73) was not available. Additional explanatory information follows:"
    ];

    private static Dictionary<string, string> Installed(string? itqolSha256 = ValidatedItqolSha256, string? arSha256 = null)
    {
        var installed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (itqolSha256 is not null)
        {
            installed["ImprovedThrallsAndQoL.pak"] = itqolSha256;
        }

        if (arSha256 is not null)
        {
            installed["Ancient_Realms.pak"] = arSha256;
        }

        return installed;
    }

    private static string Logged(string message) => "[2026.10.03-18.16.43:113][  0]" + message;

    /// <summary>The validated Ancient Realms LoadErrors multiset as parsed entries (37 per boot).</summary>
    private static List<LoadErrorEntry> ValidatedAncientRealmsLoadErrors() =>
        ModBootGates.LoadErrorBaselines.Single(b => b.ModPakFileName == "Ancient_Realms.pak").Expected
            .SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value))
            .Select(key => key.Split(" -> "))
            .Select(parts => new LoadErrorEntry(parts[0], parts[1]))
            .ToList();

    // ------------------------------------------------------------ Known warning rules

    [Fact]
    public void Known_warning_rules_are_exactly_the_itqol_mailbox_line_and_the_seven_ancient_realms_lines()
    {
        Assert.Equal(8, ModBootGates.KnownWarnings.Count);

        var mailbox = Assert.Single(ModBootGates.KnownWarnings, w => w.ModPakFileName == "ImprovedThrallsAndQoL.pak");
        Assert.Equal("ITQOL-MAILBOX-HEALTHPOOL", mailbox.Id);
        Assert.Equal(ValidatedItqolSha256, mailbox.ValidatedPakSha256);
        Assert.EndsWith(ModBootGates.ItqolMailboxClassPath, mailbox.ExactMessage, StringComparison.Ordinal);
        Assert.StartsWith("Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool", mailbox.ExactMessage, StringComparison.Ordinal);

        var ancientRealms = ModBootGates.KnownWarnings.Where(w => w.ModPakFileName == "Ancient_Realms.pak").ToList();
        Assert.Equal(7, ancientRealms.Count);
        Assert.All(ancientRealms, w => Assert.Equal(ValidatedAncientRealmsSha256, w.ValidatedPakSha256));
        Assert.Equal(AncientRealmsLines.OrderBy(l => l, StringComparer.Ordinal),
            ancientRealms.Select(w => w.ExactMessage).OrderBy(l => l, StringComparer.Ordinal));
    }

    [Fact]
    public void All_seven_ancient_realms_lines_with_the_validated_file_are_known_non_blocking()
    {
        var (problems, known) = ModBootGates.ClassifyProblems(AncientRealmsLines.Select(Logged), Installed(arSha256: ValidatedAncientRealmsSha256));

        Assert.Empty(problems);
        Assert.Equal(7, known.Count);
        Assert.All(known, k => Assert.StartsWith("ANCIENT-REALMS-DANGLING-REF-", k.Warning.Id, StringComparison.Ordinal));
    }

    [Fact]
    public void Ancient_realms_lines_fail_when_the_installed_file_hash_changed()
    {
        var (problems, known) = ModBootGates.ClassifyProblems(AncientRealmsLines.Select(Logged), Installed(arSha256: new string('B', 64)));

        Assert.Equal(7, problems.Count);
        Assert.Empty(known);
    }

    [Theory]
    // a new dangling reference from a validated package
    [InlineData("LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/AR_BP_ModController, a dependent package None (0123456789ABCDEF) was not available. Additional explanatory information follows:")]
    // the same missing id from a different Ancient Realms package
    [InlineData("LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Wood/wood_01/BP_PL_Water_Well_Fountain, a dependent package None (FE8C96EB21683A73) was not available. Additional explanatory information follows:")]
    // an Ancient Realms persistence error
    [InlineData("Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool - DefaultObject not loaded: /Game/Mods/Ancient_Realms/Buildings/Stone/stone_05/BP_PL_Water_Well_Fountain.BP_PL_Water_Well_Fountain_C")]
    // an Ancient Realms script error
    [InlineData("LogScript: Error: Script Msg: Accessed None trying to read property in /Game/Mods/Ancient_Realms/AR_BP_ModController.AR_BP_ModController_C")]
    public void Any_other_ancient_realms_error_still_fails(string message)
    {
        var (problems, known) = ModBootGates.ClassifyProblems(
            AncientRealmsLines.Select(Logged).Append(Logged(message)), Installed(arSha256: ValidatedAncientRealmsSha256));

        Assert.Equal(Logged(message), Assert.Single(problems));
        Assert.Equal(7, known.Count);
    }

    // ------------------------------------------------------------ Validated LoadErrors sets

    [Fact]
    public void Load_errors_are_parsed_for_named_and_none_packages()
    {
        var entries = ModBootGates.ParseLoadErrors([
            Logged(AncientRealmsLines[0]),
            Logged("LoadErrors: While trying to load package None, a dependent package None (5A19E15D92AF952) was not available. Additional explanatory information follows:"),
            Logged("FPackageName: Unable to identify a valid mount point associated with skipped package None. The package root is unknown."),
            Logged("LogTemp: Display: unrelated")
        ]);

        Assert.Equal(2, entries.Count);
        Assert.Equal("/Game/Mods/Ancient_Realms/AR_BP_ModController -> 35924C262CEDD960", entries[0].ToString());
        Assert.Equal("None -> 5A19E15D92AF952", entries[1].ToString());
    }

    [Fact]
    public void Ancient_realms_validated_set_has_the_37_lines_seen_on_every_boot()
    {
        var baseline = ModBootGates.LoadErrorBaselines.Single(b => b.ModPakFileName == "Ancient_Realms.pak");

        Assert.Equal(LoadErrorBaselineStatus.KnownNonBlocking, baseline.Status);
        Assert.Equal(ValidatedAncientRealmsSha256, baseline.ValidatedPakSha256);
        Assert.Equal(37, baseline.Expected.Values.Sum());
        Assert.Equal(13, baseline.Expected.Count);
    }

    [Fact]
    public void Exact_validated_ancient_realms_load_errors_pass()
    {
        var gate = ModBootGates.EvaluateLoadErrors("Ancient_Realms.pak", ValidatedAncientRealmsSha256, ValidatedAncientRealmsLoadErrors());

        Assert.True(gate.Pass);
        Assert.StartsWith("KNOWN NON-BLOCKING: 37 LoadErrors", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_new_ancient_realms_load_error_signature_fails()
    {
        var observed = ValidatedAncientRealmsLoadErrors();
        observed.Add(new LoadErrorEntry("None", "0123456789ABCDEF"));

        var gate = ModBootGates.EvaluateLoadErrors("Ancient_Realms.pak", ValidatedAncientRealmsSha256, observed);

        Assert.False(gate.Pass);
        Assert.Contains("None -> 0123456789ABCDEF x1", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_extra_occurrence_or_a_missing_one_fails()
    {
        var extra = ValidatedAncientRealmsLoadErrors();
        extra.Add(new LoadErrorEntry("None", "9F5919676AD734DE"));
        var missing = ValidatedAncientRealmsLoadErrors();
        missing.RemoveAt(missing.Count - 1);

        Assert.False(ModBootGates.EvaluateLoadErrors("Ancient_Realms.pak", ValidatedAncientRealmsSha256, extra).Pass);
        Assert.False(ModBootGates.EvaluateLoadErrors("Ancient_Realms.pak", ValidatedAncientRealmsSha256, missing).Pass);
    }

    [Fact]
    public void A_changed_ancient_realms_file_invalidates_its_validated_set()
    {
        var gate = ModBootGates.EvaluateLoadErrors("Ancient_Realms.pak", new string('C', 64), ValidatedAncientRealmsLoadErrors());

        Assert.False(gate.Pass);
        Assert.Contains("not the validated version", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_mod_without_a_validated_set_fails_on_any_load_error_and_passes_without_any()
    {
        Assert.False(ModBootGates.EvaluateLoadErrors("Shemite_City_State.pak", new string('D', 64),
            [new LoadErrorEntry("None", "0123456789ABCDEF")]).Pass);
        Assert.True(ModBootGates.EvaluateLoadErrors("Shemite_City_State.pak", new string('D', 64), []).Pass);
    }

    [Fact]
    public void Itqol_exact_23_load_errors_are_known_non_blocking_for_the_validated_file_only()
    {
        var baseline = ModBootGates.LoadErrorBaselines.Single(b => b.ModPakFileName == "ImprovedThrallsAndQoL.pak");
        var observed = baseline.Expected.SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value))
            .Select(k => new LoadErrorEntry("None", k.Split(" -> ")[1])).ToList();

        var gate = ModBootGates.EvaluateLoadErrors("ImprovedThrallsAndQoL.pak", ValidatedItqolSha256, observed);

        Assert.Equal(LoadErrorBaselineStatus.KnownNonBlocking, baseline.Status);
        Assert.Equal(ValidatedItqolSha256, baseline.ValidatedPakSha256);
        Assert.Equal(23, observed.Count);
        Assert.Equal(16, baseline.Expected.Count);
        Assert.True(gate.Pass);
        Assert.StartsWith("KNOWN NON-BLOCKING: 23 LoadErrors", gate.Detail, StringComparison.Ordinal);
        Assert.False(ModBootGates.EvaluateLoadErrors("ImprovedThrallsAndQoL.pak", new string('E', 64), observed).Pass);
    }

    [Fact]
    public void Itqol_load_error_set_changes_still_fail()
    {
        var baseline = ModBootGates.LoadErrorBaselines.Single(b => b.ModPakFileName == "ImprovedThrallsAndQoL.pak");
        List<LoadErrorEntry> Exact() => baseline.Expected.SelectMany(kv => Enumerable.Repeat(kv.Key, kv.Value))
            .Select(k => new LoadErrorEntry("None", k.Split(" -> ")[1])).ToList();
        var added = Exact();
        added.Add(new LoadErrorEntry("None", "0123456789ABCDEF"));
        var removed = Exact();
        removed.RemoveAt(0);
        var changed = Exact();
        changed[0] = new LoadErrorEntry("/Game/Mods/ImprovedThrallsAndQoL/MC_ImprovedThrallsAndQoL", changed[0].MissingPackageId);

        Assert.False(ModBootGates.EvaluateLoadErrors("ImprovedThrallsAndQoL.pak", ValidatedItqolSha256, added).Pass);
        Assert.False(ModBootGates.EvaluateLoadErrors("ImprovedThrallsAndQoL.pak", ValidatedItqolSha256, removed).Pass);
        Assert.False(ModBootGates.EvaluateLoadErrors("ImprovedThrallsAndQoL.pak", ValidatedItqolSha256, changed).Pass);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(2, false)]
    public void Itqol_controller_must_exist_exactly_once(int count, bool pass)
    {
        var controller = Assert.Single(ModBootGates.SingletonActors, a => a.Label == "ITQoL mod controller");

        Assert.Equal(pass, ModBootGates.EvaluateSingletonActor(controller, installed: true, count).Pass);
        Assert.Equal("/Game/Mods/ImprovedThrallsAndQoL/MC_ImprovedThrallsAndQoL.MC_ImprovedThrallsAndQoL_C", controller.ClassPath);
    }

    // ------------------------------------------------------------ Singleton persistence objects

    [Theory]
    [InlineData(1, true, "1 Ancient Realms mod controller")]
    [InlineData(0, false, "MISSING")]
    [InlineData(2, false, "DUPLICATE")]
    public void Ancient_realms_controller_must_exist_exactly_once(int count, bool pass, string detailStart)
    {
        var controller = Assert.Single(ModBootGates.SingletonActors, a => a.ModPakFileName == "Ancient_Realms.pak");

        var gate = ModBootGates.EvaluateSingletonActor(controller, installed: true, count);

        Assert.Equal(pass, gate.Pass);
        Assert.StartsWith(detailStart, gate.Detail, StringComparison.Ordinal);
        Assert.Equal("/Game/Mods/Ancient_Realms/AR_BP_ModController.AR_BP_ModController_C", controller.ClassPath);
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
    [InlineData(301.8)]
    [InlineData(450.0)]
    public void Graceful_stops_past_240_seconds_still_fail_the_batch_gate_even_with_the_600_s_ceiling(double seconds)
    {
        // The stop policy may let a proven shutdown finish (no kill at 300 s); the compatibility gate still fails.
        var gate = ModBootGates.EvaluateShutdownDuration(TimeSpan.FromSeconds(seconds), 300, 600);

        Assert.False(gate.Pass);
        Assert.StartsWith("HIGH RISK", gate.Detail, StringComparison.Ordinal);
        Assert.Contains("emergency ceiling 600 s", gate.Detail, StringComparison.Ordinal);
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
