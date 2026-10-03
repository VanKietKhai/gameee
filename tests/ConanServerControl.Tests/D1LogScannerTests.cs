using ConanServerControl.Core.LiveTesting;

namespace ConanServerControl.Tests;

public sealed class D1LogScannerTests
{
    // Exact preserved boot AC751A4E...1522B, lines 7236-7257 (timestamps are UTC).
    private static readonly string[] D1Errors =
    [
        "[2026.10.03-07.24.54:111][  8]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.54:116][  8]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.54:210][  9]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.54:406][ 13]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.54:437][ 14]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.54:606][ 19]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.55:116][ 23]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.55:491][ 24]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.55:496][ 24]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.55:733][ 25]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.56:450][ 31]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.57:283][ 35]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.57:541][ 36]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.58:018][ 39]NPC: Error: Data: No stat templates found for StatModifier template None.",
        "[2026.10.03-07.24.58:330][ 40]NPC: Error: Data: No stat templates found for StatModifier template None.",
    ];

    [Fact]
    public void All_fifteen_D1_errors_fail_without_a_mod_name_or_attribution()
    {
        var selected = ModBootGates.SelectProblemLines(D1Errors, ["FantasyRacesOfExiles"]);
        var (unknown, known) = ModBootGates.ClassifyProblems(selected, new Dictionary<string, string>());
        Assert.Equal(15, unknown.Count);
        Assert.Equal(D1Errors, unknown);
        Assert.Empty(known);
    }

    [Fact]
    public void Completed_run_detects_errors_after_readiness_and_during_shutdown()
    {
        string[] readiness = ["[2026.10.03-07.24.51:919][  2]LogTemp: Display: World is ticking"];
        Assert.Empty(ModBootGates.SelectProblemLines(readiness, []));
        var completed = readiness.Concat(D1Errors)
            .Append("[2026.10.03-07.24.59:000][ 41]SaveGame: Error: save failed");
        Assert.Equal(16, ModBootGates.SelectProblemLines(completed, []).Count);
    }

    [Theory]
    [InlineData("NPC: Error: Data: No stat templates found for StatModifier template Changed.")]
    [InlineData("NPC: Warning: Unknown stat template")]
    [InlineData("LogSpawn: Warning: Missing spawn row")]
    [InlineData("Persistence: Error: Failed to save actor")]
    [InlineData("LogSQLite: Error: disk I/O error")]
    [InlineData("LogLevelStreaming: Error: Couldn't find file for package /Game/Map")]
    [InlineData("LogWorld: Warning: Failed to load world")]
    [InlineData("LogModController: Error: AModController::MergeDataTables - ToBeAddedDataTable is null")]
    [InlineData("UnfamiliarFamily: Error: undocumented failure")]
    [InlineData("LogTemp: Fatal: crash")]
    [InlineData("Assertion failed: something")]
    [InlineData("Unhandled Exception: EXCEPTION_ACCESS_VIOLATION")]
    public void Relevant_errors_fail_closed_independently_of_mod_names(string line)
    {
        var selected = ModBootGates.SelectProblemLines([line], []);
        Assert.Equal(line, Assert.Single(ModBootGates.ClassifyProblems(selected, new Dictionary<string, string>()).Problems));
    }

    [Fact]
    public void Exact_mailbox_exception_still_requires_its_validated_hash()
    {
        var warning = ModBootGates.KnownWarnings[0];
        var selected = ModBootGates.SelectProblemLines([warning.ExactMessage], []);
        Assert.Single(selected);
        var hashes = new Dictionary<string, string> { [warning.ModPakFileName] = warning.ValidatedPakSha256 };
        Assert.Single(ModBootGates.ClassifyProblems(selected, hashes).Known);
        hashes[warning.ModPakFileName] = new string('0', 64);
        Assert.Single(ModBootGates.ClassifyProblems(selected, hashes).Problems);
    }

    // Real base-game lines present in every healthy boot (taken from the seven-mod baseline log).
    [Theory]
    [InlineData("[2026.10.02-20.29.34:001][  0]ItemInventory: Error: Data: Mismatch BuildingClass DLCPackage XX_DevOnly for item XX_DEV Foundation", "ITEMINVENTORY-DLC-MISMATCH")]
    [InlineData("[2026.10.02-20.29.34:001][  0]ItemInventory: Error: Data: Mismatch Icon DLCPackage None for item XX_AkhametLeggings", "ITEMINVENTORY-DLC-MISMATCH")]
    [InlineData("[2026.10.02-20.29.34:001][  0]ItemInventory: Error: Data: Map feat blacklist table not found, blacklist will not function.", "ITEMINVENTORY-FEAT-BLACKLIST")]
    [InlineData("[2026.10.02-20.29.34:001][  0]LogBinkMoviePlayer: Error: UBinkMediaPlayer::Open: Failed! BinkHLOpen failed. URL: D:/x/Movies/TitleScreen_Claws.bk2 Error: ", "BINK-TITLE-MOVIE")]
    [InlineData("[2026.10.02-20.29.34:001][  0]LogLevelStreaming: Error: Couldn't find file for package /Game/Developers/MasonRoy/AssetGroups/AG_TEMP_BoneBowl.", "DEV-ASSETGROUP-STREAMING")]
    [InlineData("[2026.10.02-20.29.34:001][  0]LogTemp: Error: Character 'BP_NPC_Wildlife_Rabbit_C_2147405417' is in an unsafe movement move (currently 4) while in AILOD3", "WILDLIFE-AILOD3-MOVEMENT")]
    [InlineData("[2026.10.02-20.29.34:001][  0]NPC: Error: Code: UNpcSpawnerComponent::Despawned - Invalid spawn entry supplied, Type: 1, Index: 0", "NPC-SPAWNER-DESPAWN-ENTRY")]
    [InlineData("[2026.10.02-20.29.34:001][  0]SpawnTable: Error: Data: USpawnTableLibrary::SpawnNPCFromWeightedTable - could not find weighted table with id: WarTestLongLeash", "SPAWNTABLE-WEIGHTED-TABLE")]
    [InlineData("[2026.10.02-20.29.34:001][  0]LogBaseSpawner: Error: ABaseSpawner::TickSpawnBase - Failed to spawn module from BP_HL_BuildPillar_T2_C.", "BASESPAWNER-MODULE")]
    [InlineData("[2026.10.02-20.29.34:001][  0]building: Error: Code: ABuildingBase::AddModule_Internal - Removing placed module that did not manage to find stability.", "BUILDING-STABILITY")]
    public void Recorded_base_game_error_kinds_are_not_flagged(string line, string kind)
    {
        Assert.Equal(kind, ModBootGates.MatchBaseGameNoise(line));
        Assert.Empty(ModBootGates.SelectProblemLines([line], []));
    }

    [Fact]
    public void The_new_D1_message_and_near_misses_of_baseline_kinds_are_never_baseline()
    {
        Assert.All(D1Errors, line => Assert.Null(ModBootGates.MatchBaseGameNoise(line)));
        string[] nearMisses =
        [
            "NPC: Error: Code: UNpcSpawnerComponent::Despawned - Invalid spawn entry supplied, Type: 1", // changed text
            "NPC: Error: Data: No stat templates found for StatModifier template Changed.",
            "LogLevelStreaming: Error: Couldn't find file for package /Game/Mods/Anything/Map", // not a developer asset group
            "building: Error: Code: ABuildingBase::AddModule_Internal - something else failed.",
            "LogBinkMoviePlayer: Error: UBinkMediaPlayer::Open: Failed to read the stream"
        ];
        Assert.Equal(nearMisses, ModBootGates.SelectProblemLines(nearMisses, []));
    }

    [Fact]
    public void Base_game_script_and_data_table_warning_volume_is_not_scanned_but_mod_family_warnings_are()
    {
        string[] baseVolume =
        [
            "LogScript: Warning: Script Msg: A null object was passed as a world context object to UEngine::GetWorldFromContextObject().",
            "LogDataTable: Warning: UDataTable::FindRow : 'AIDataTable' requested row 'WarTestShortLeashShortSightWithdraw' not in DataTable '/Game/Systems/AI/NewAI/AIDataTable.AIDataTable'.",
            "LogActor: Warning: UAILODComponent::OnChildAttachedToOwner - Attached (X) to ParentActor (Y) but bNetUseOwnerRelevancy is false, and has no AILODComponent"
        ];
        Assert.Empty(ModBootGates.SelectProblemLines(baseVolume, []));
        Assert.Single(ModBootGates.SelectProblemLines(["NPC: Warning: Missing stat template row"], []));
        // A scripted warning that names a mod is still scanned.
        Assert.Single(ModBootGates.SelectProblemLines(
            ["LogScript: Warning: Script Msg: Failed to load FantasyRacesOfExiles asset"], ["FantasyRacesOfExiles"]));
    }

    [Fact]
    public void Ancient_Realms_merge_error_is_known_only_twice_and_only_for_the_validated_file()
    {
        var warning = ModBootGates.KnownWarnings.Single(w => w.Id == "ANCIENT-REALMS-MERGE-DATATABLE-NULL");
        var hashes = new Dictionary<string, string> { [warning.ModPakFileName] = warning.ValidatedPakSha256 };
        var line = "[2026.10.02-20.29.01:885][  0]" + warning.ExactMessage;

        var two = ModBootGates.ClassifyProblems([line, line], hashes);
        Assert.Empty(two.Problems);
        Assert.Equal(2, two.Known.Count);

        var three = ModBootGates.ClassifyProblems([line, line, line], hashes);
        Assert.Single(three.Problems);
        Assert.Equal(2, three.Known.Count);

        hashes[warning.ModPakFileName] = new string('0', 64);
        Assert.Equal(2, ModBootGates.ClassifyProblems([line, line], hashes).Problems.Count);
        Assert.Equal(2, ModBootGates.ClassifyProblems([line, line], new Dictionary<string, string>()).Problems.Count);
    }

    [Fact]
    public void Duplicate_lines_are_not_collapsed()
    {
        Assert.Equal(2, ModBootGates.SelectProblemLines([D1Errors[0], D1Errors[0]], []).Count);
    }

    [Fact]
    public void LoadErrors_remain_accounted_for_by_the_separate_strict_gate()
    {
        const string malformed = "LoadErrors: unknown";
        Assert.Empty(ModBootGates.SelectProblemLines([malformed], []));
        Assert.False(ModBootGates.EvaluateLoadErrors("Unknown.pak", null,
            ModBootGates.ParseLoadErrors([malformed])).Pass);
    }
}
