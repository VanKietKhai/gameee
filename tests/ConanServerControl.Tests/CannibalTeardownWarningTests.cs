using System.Security.Cryptography;
using System.Text;
using ConanServerControl.Core.LiveTesting;

namespace ConanServerControl.Tests;

/// <summary>
/// The Cannibal Captivity teardown warning set is accepted only as a whole: exact file hash, exact message and
/// object-path family, only after the main-world teardown begins, exactly 99 times. Anything else stays unknown.
/// </summary>
public sealed class CannibalTeardownWarningTests
{
    private const string Id = "CANNIBAL-CAPTIVITY-TEARDOWN-NO-WORLD";

    private static readonly string[] Mods =
    [
        "StackMe10K.pak", "SavageParagon.pak", "GritandGrease.pak", "ThrallReputation.pak",
        "ImprovedThrallsAndQoL.pak", "WO_RidingThralls.pak", "Ancient_Realms.pak", "Cannibal_Captivity.pak"
    ];

    private static BatchAnalysisSnapshot Context(string cannibalSha = ModBootGates.CannibalCaptivityValidatedSha256, ValidatedCatalog? catalog = null) =>
        new("batch", new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc), Mods,
            Mods.Select(m => new SnapshotMod(m,
                m == "Cannibal_Captivity.pak" ? cannibalSha :
                m == "Ancient_Realms.pak" ? ModBootGates.AncientRealmsValidatedSha256 :
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(m))), 1)).ToArray(),
            [], catalog ?? ValidatedCatalog.Current);

    private static string Warning(int n, string objectPath = "BP_BuildWall_C_2147414744.InstancedBuildingMesh") =>
        $"[2026.10.04-17.05.05:{n % 1000:000}][796]LogScript: Warning: Script Msg: No world was found for object " +
        $"(/Game/Mods/Cannibal_Captivity/Base/CannibalCaptivityLevel.CannibalCaptivityLevel:PersistentLevel.{objectPath}) " +
        "passed in to UEngine::GetWorldFromContextObject().";

    private static string[] Online =>
    [
        "[2026.10.04-16.54.00:001][  0]LogTemp: Display: World is ticking",
        "[2026.10.04-17.02.00:001][700]LogTemp: Display: online"
    ];

    private static string[] ShutdownStart =>
    [
        "[2026.10.04-17.02.05:837][790]LogRcon: Warning: Received Rcon: shutdown, from PeerAddr: 127.0.0.1:55578",
        "[2026.10.04-17.02.05:838][790]LogCore: Engine exit requested (reason: GenericPlatform RequestExit)",
        "[2026.10.04-17.02.05:912][791]LogInit: Display: PreExit Game.",
        "[2026.10.04-17.02.05:913][791]LogWorld: BeginTearingDown for /Game/Maps/ConanSandbox/ConanSandbox"
    ];

    private static string[] Log(IEnumerable<string> before, IEnumerable<string> teardown) =>
        [.. Online, .. before, .. ShutdownStart, .. teardown, "[2026.10.04-17.05.08:000][796]Log file closed"];

    private static IEnumerable<string> Ninety9() => Enumerable.Range(0, 99).Select(i => Warning(i));

    [Fact]
    public void The_exact_99_after_teardown_started_are_known_non_blocking_for_the_validated_file()
    {
        var result = BootLogAnalyzer.Analyze(Log([], Ninety9()), Context(), _ => null);

        Assert.Empty(result.Unknown);
        Assert.Equal(99, result.PhaseBoundAccepted!.Count);
        Assert.All(result.PhaseBoundAccepted, a => Assert.Equal(Id, a.Warning.Id));
        Assert.DoesNotContain(result.LoadErrorProblems, p => p.Contains("Cannibal", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("ACCEPTED 99 of exactly 99", Assert.Single(result.PhaseBoundNotes!));
    }

    [Fact]
    public void A_100th_occurrence_fails_every_matching_line()
    {
        var result = BootLogAnalyzer.Analyze(Log([], Ninety9().Append(Warning(500))), Context(), _ => null);

        Assert.Equal(100, result.Unknown.Count);
        Assert.Empty(result.PhaseBoundAccepted!);
        Assert.False(result.Clean);
    }

    [Fact]
    public void Fewer_than_99_also_fails()
    {
        var result = BootLogAnalyzer.Analyze(Log([], Ninety9().Take(98)), Context(), _ => null);

        Assert.Equal(98, result.Unknown.Count);
        Assert.Empty(result.PhaseBoundAccepted!);
    }

    [Fact]
    public void One_occurrence_before_shutdown_fails_the_whole_set()
    {
        var result = BootLogAnalyzer.Analyze(Log([Warning(900)], Ninety9().Take(98)), Context(), _ => null);

        Assert.Equal(99, result.Unknown.Count);
        Assert.Empty(result.PhaseBoundAccepted!);
        Assert.Contains("1 not after the teardown start", Assert.Single(result.PhaseBoundNotes!));
    }

    [Fact]
    public void An_occurrence_between_the_shutdown_command_and_the_teardown_start_is_not_teardown()
    {
        string[] shutdownBeforeTeardown =
        [
            .. Online,
            "[2026.10.04-17.02.05:837][790]LogRcon: Warning: Received Rcon: shutdown, from PeerAddr: 127.0.0.1:55578",
            "[2026.10.04-17.02.05:838][790]LogCore: Engine exit requested (reason: GenericPlatform RequestExit)",
            Warning(901),
            "[2026.10.04-17.02.05:913][791]LogWorld: BeginTearingDown for /Game/Maps/ConanSandbox/ConanSandbox",
            .. Ninety9().Take(98)
        ];

        var result = BootLogAnalyzer.Analyze(shutdownBeforeTeardown, Context(), _ => null);

        Assert.Equal(99, result.Unknown.Count);
        Assert.Empty(result.PhaseBoundAccepted!);
    }

    [Fact]
    public void Without_a_main_world_teardown_in_the_log_nothing_is_accepted()
    {
        string[] noTeardown = [.. Online, .. Ninety9()];

        var result = BootLogAnalyzer.Analyze(noTeardown, Context(), _ => null);

        Assert.Equal(99, result.Unknown.Count);
        Assert.Contains("no main-world teardown", Assert.Single(result.PhaseBoundNotes!));
    }

    [Fact]
    public void A_teardown_of_another_world_before_the_exit_request_does_not_count()
    {
        string[] logs =
        [
            .. Online,
            "[2026.10.04-16.54.01:000][  0]LogWorld: BeginTearingDown for /Game/Maps/ConanSandbox/ConanSandbox",
            .. Ninety9()
        ];

        Assert.Equal(99, BootLogAnalyzer.Analyze(logs, Context(), _ => null).Unknown.Count);
    }

    [Fact]
    public void A_changed_pak_hash_accepts_nothing()
    {
        var result = BootLogAnalyzer.Analyze(Log([], Ninety9()), Context(new string('A', 64)), _ => null);

        Assert.Equal(99, result.Unknown.Count);
        Assert.Empty(result.PhaseBoundAccepted!);
    }

    [Theory]
    [InlineData("/Game/Mods/Cannibal_Captivity/Other/Level.Level:PersistentLevel.BP_X_C_1")]
    [InlineData("/Game/Mods/Cannibal_Captivity/Base/CannibalCaptivityLevelB.CannibalCaptivityLevelB:PersistentLevel.BP_X_C_1")]
    [InlineData("/Game/Mods/Cannibal_Captivity/Base/CannibalCaptivityLevel.CannibalCaptivityLevel:PersistentLevel.BP X")]
    [InlineData("/Game/Mods/Other_Mod/Base/CannibalCaptivityLevel.CannibalCaptivityLevel:PersistentLevel.BP_X_C_1")]
    public void A_changed_object_path_fails_the_set(string path)
    {
        var drifted = $"[2026.10.04-17.05.05:700][796]LogScript: Warning: Script Msg: No world was found for object ({path}) passed in to UEngine::GetWorldFromContextObject().";

        // 98 exact lines + 1 drifted line = 99 lines that name the mod, but only 98 are the validated signature.
        var result = BootLogAnalyzer.Analyze(Log([], Ninety9().Take(98).Append(drifted)), Context(), _ => null);

        Assert.Empty(result.PhaseBoundAccepted!);
        Assert.True(result.Unknown.Count >= 98);
        Assert.False(result.Clean);
    }

    [Theory]
    [InlineData("LogScript: Error: Script Msg: No world was found for object ({0}) passed in to UEngine::GetWorldFromContextObject().")]
    [InlineData("LogScript: Warning: Script Msg: No world was found for object ({0}) passed in to UEngine::GetWorldFromContextObject()")]
    [InlineData("LogTemp: Warning: Script Msg: No world was found for object ({0}) passed in to UEngine::GetWorldFromContextObject().")]
    [InlineData("LogScript: Warning: Script Msg: A world was not found for object ({0}) passed in to UEngine::GetWorldFromContextObject().")]
    public void A_changed_message_or_severity_is_never_covered(string template)
    {
        const string path = "/Game/Mods/Cannibal_Captivity/Base/CannibalCaptivityLevel.CannibalCaptivityLevel:PersistentLevel.BP_X_C_1";
        var line = "[2026.10.04-17.05.05:700][796]" + string.Format(template, path);

        // The validated 99 plus one changed line: the 99 are accepted, the changed line is still an unknown problem.
        var result = BootLogAnalyzer.Analyze(Log([], Ninety9().Append(line)), Context(), _ => null);

        Assert.Equal(line, Assert.Single(result.Unknown));
        Assert.Equal(99, result.PhaseBoundAccepted!.Count);
    }

    [Fact]
    public void Other_lines_naming_the_mod_are_not_swept_up_there_is_no_broad_contains_rule()
    {
        var other = "[2026.10.04-17.05.05:800][796]LogScript: Warning: Script Msg: Failed to spawn /Game/Mods/Cannibal_Captivity/Base/BP_Camp.";
        var crash = "[2026.10.04-17.05.05:801][796]LogWindows: Error: Assertion failed: Cannibal_Captivity";

        var result = BootLogAnalyzer.Analyze(Log([], Ninety9().Append(other).Append(crash)), Context(), _ => null);

        Assert.Equal([other, crash], result.Unknown);
        Assert.Equal(99, result.PhaseBoundAccepted!.Count);
    }

    [Fact]
    public void A_boot_recorded_before_the_acceptance_still_fails_from_its_recorded_catalog()
    {
        var recorded = ValidatedCatalog.Current with { PhaseBoundWarnings = null };
        var log = Log([], Ninety9());

        Assert.Equal(99, BootLogAnalyzer.Analyze(log, Context(catalog: recorded), _ => null).Unknown.Count);
        Assert.Empty(BootLogAnalyzer.Analyze(log, Context(), _ => null).Unknown);
    }

    [Fact]
    public void The_catalog_entry_is_exactly_the_validated_hash_pattern_and_count()
    {
        var warning = Assert.Single(ModBootGates.PhaseBoundWarnings);

        Assert.Equal(Id, warning.Id);
        Assert.Equal("Cannibal_Captivity.pak", warning.ModPakFileName);
        Assert.Equal("DB6E3C299912E48E4DEC8293A58C8DEF1D881FDBDC44F1E67CE99A9BF348F04F", warning.ValidatedPakSha256);
        Assert.Equal(99, warning.ExactCount);
        Assert.StartsWith("^LogScript: Warning: Script Msg: No world was found for object", warning.MessagePattern);
        Assert.DoesNotContain("Contains", warning.MessagePattern);
    }
}
