using System.Security.Cryptography;
using System.Text;
using ConanServerControl.Core.LiveTesting;

namespace ConanServerControl.Tests;

public sealed class BatchAnalysisSnapshotTests : IDisposable
{
    private const string CannibalSha = "DB6E3C299912E48E4DEC8293A58C8DEF1D881FDBDC44F1E67CE99A9BF348F04F";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "csc-snapshot-tests-" + Guid.NewGuid().ToString("N"));

    public BatchAnalysisSnapshotTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, recursive: true);
    }

    private static readonly string[] SevenMods =
    [
        "StackMe10K.pak", "SavageParagon.pak", "GritandGrease.pak", "ThrallReputation.pak",
        "ImprovedThrallsAndQoL.pak", "WO_RidingThralls.pak", "Ancient_Realms.pak"
    ];

    private static string Fake(string name) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)));

    private static BatchAnalysisSnapshot Context(string batchId, IEnumerable<string> mods, ValidatedCatalog? catalog = null)
    {
        var list = mods.ToArray();
        var entries = list.Select(m => new SnapshotMod(m,
            m == "Cannibal_Captivity.pak" ? CannibalSha : m == "Ancient_Realms.pak" ? ModBootGates.AncientRealmsValidatedSha256 : Fake(m), 1000)).ToArray();
        return new BatchAnalysisSnapshot(batchId, new DateTime(2026, 10, 3, 16, 0, 0, DateTimeKind.Utc), list, entries, [],
            catalog ?? ValidatedCatalog.Current);
    }

    // 99 teardown warnings in the exact shape recorded in the Cannibal Captivity log.
    private static IReadOnlyList<string> CannibalTeardownWarnings()
    {
        string[] parts =
        [
            "BP_BuildTriangleFoundation_C_2147419141.InstancedBuildingMesh", "BP_BuildTriangleFoundation_C_2147419141",
            "BP_BuildWall_C_2147414744.InstancedBuildingComponent_2147414201", "BP_BuildStairs_C_2147414191"
        ];
        return Enumerable.Range(0, 99).Select(i =>
            $"[2026.10.03-16.37.54:{735 + i:000}][151]LogScript: Warning: Script Msg: No world was found for object " +
            $"(/Game/Mods/Cannibal_Captivity/Base/CannibalCaptivityLevel.CannibalCaptivityLevel:PersistentLevel.{parts[i % parts.Length]}) " +
            "passed in to UEngine::GetWorldFromContextObject().").ToList();
    }

    private static IReadOnlyList<string> CannibalBootLog()
    {
        var lines = new List<string>
        {
            "[2026.10.03-16.24.16:242][  0]LogTemp: Display: boot",
            "[2026.10.03-16.34.51:838][150]LogCore: Engine exit requested (reason: GenericPlatform RequestExit)",
            "[2026.10.03-16.34.51:912][151]LogWorld: BeginTearingDown for /Game/Maps/ConanSandbox/ConanSandbox",
            // The same message without a mod path is base-game volume and must stay unflagged.
            "[2026.10.03-16.37.50:001][151]LogScript: Warning: Script Msg: No world was found for object (/Game/Systems/Buildings/BP_Foo.BP_Foo) passed in to UEngine::GetWorldFromContextObject()."
        };
        lines.AddRange(CannibalTeardownWarnings());
        return lines;
    }

    [Fact]
    public void Regression_historical_Cannibal_boot_fails_the_same_way_from_the_snapshot_after_the_mod_is_removed()
    {
        var log = CannibalBootLog();

        // 1. Analyse while Cannibal is installed -> FAIL on the 99 warnings. The catalog is the one in force when
        // that boot ran: the phase-bound Cannibal acceptance was only added after the controlled rerun.
        var catalogOfThatRun = ValidatedCatalog.Current with { PhaseBoundWarnings = null };
        var installed = Context("cannibal-run-1", [.. SevenMods, "Cannibal_Captivity.pak"], catalogOfThatRun);
        var whileInstalled = BootLogAnalyzer.Analyze(log, installed, _ => null);
        Assert.Equal(99, whileInstalled.Unknown.Count);
        Assert.Equal(CannibalTeardownWarnings(), whileInstalled.Unknown);

        // Record the batch while it is current: immutable snapshot plus a copy of the boot log.
        var logFile = Path.Combine(_root, "ConanSandbox.log");
        File.WriteAllLines(logFile, log);
        var dir = BatchAnalysisSnapshotStore.Save(Path.Combine(_root, "snapshots"), installed, logFile);

        // 2. Cannibal leaves the current catalog (rollback). Analysing against the live view now passes: the gap.
        var rolledBack = Context("live", SevenMods, catalogOfThatRun);
        Assert.Empty(BootLogAnalyzer.Analyze(log, rolledBack, _ => null).Unknown);

        // 3. The preserved batch snapshot gives the SAME failure, from its own copy of the log.
        var (snapshot, logPath) = BatchAnalysisSnapshotStore.Load(dir);
        var fromSnapshot = BootLogAnalyzer.Analyze(File.ReadAllLines(logPath), snapshot, _ => null);
        Assert.Equal(99, fromSnapshot.Unknown.Count);
        Assert.Equal(whileInstalled.Unknown, fromSnapshot.Unknown);
        Assert.False(fromSnapshot.Clean);
    }

    [Fact]
    public void Snapshot_round_trips_modlist_hashes_catalog_and_log_identity()
    {
        var context = Context("batch-a", [.. SevenMods, "Cannibal_Captivity.pak"]);
        var logFile = Path.Combine(_root, "ConanSandbox.log");
        File.WriteAllLines(logFile, CannibalBootLog());

        var dir = BatchAnalysisSnapshotStore.Save(Path.Combine(_root, "snapshots"), context, logFile);
        var (loaded, logPath) = BatchAnalysisSnapshotStore.Load(dir);

        Assert.Equal(context.ModList, loaded.ModList);
        Assert.Equal(context.Mods, loaded.Mods);
        Assert.Equal(CannibalSha, loaded.Sha256ByPak["Cannibal_Captivity.pak"]);
        Assert.Equal(context.Catalog.KnownWarnings.Select(w => (w.Id, w.ModPakFileName, w.ValidatedPakSha256, w.ExactMessage, w.MaxOccurrences)),
            loaded.Catalog.KnownWarnings.Select(w => (w.Id, w.ModPakFileName, w.ValidatedPakSha256, w.ExactMessage, w.MaxOccurrences)));
        Assert.Equal(context.Catalog.LoadErrorBaselines.Select(b => (b.ModPakFileName, b.ValidatedPakSha256, b.Status, b.Expected.Count)),
            loaded.Catalog.LoadErrorBaselines.Select(b => (b.ModPakFileName, b.ValidatedPakSha256, b.Status, b.Expected.Count)));
        Assert.Equal(context.Catalog.BaseGameNoise, loaded.Catalog.BaseGameNoise);
        Assert.Equal(context.Catalog.SingletonActors, loaded.Catalog.SingletonActors);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(logFile))), loaded.Log!.Sha256);
        Assert.Equal(new FileInfo(logFile).Length, loaded.Log.Bytes);
        Assert.Equal(logFile, loaded.Log.SourcePath);
        Assert.Equal(File.ReadAllBytes(logFile), File.ReadAllBytes(logPath));
    }

    [Fact]
    public void Snapshot_and_log_copy_are_read_only_and_a_batch_id_cannot_be_reused()
    {
        var context = Context("batch-b", SevenMods);
        var logFile = Path.Combine(_root, "ConanSandbox.log");
        File.WriteAllLines(logFile, ["[2026.10.03-16.24.16:242][  0]LogTemp: Display: boot"]);
        var snapshots = Path.Combine(_root, "snapshots");

        var dir = BatchAnalysisSnapshotStore.Save(snapshots, context, logFile);

        Assert.True(File.GetAttributes(Path.Combine(dir, BatchAnalysisSnapshotStore.SnapshotFileName)).HasFlag(FileAttributes.ReadOnly));
        Assert.True(File.GetAttributes(Path.Combine(dir, BatchAnalysisSnapshotStore.LogFileName)).HasFlag(FileAttributes.ReadOnly));
        Assert.Throws<IOException>(() => BatchAnalysisSnapshotStore.Save(snapshots, context, logFile));
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData(".hidden")]
    public void Unsafe_batch_ids_are_rejected(string batchId)
    {
        var logFile = Path.Combine(_root, "ConanSandbox.log");
        File.WriteAllText(logFile, "x");
        Assert.Throws<ArgumentException>(() =>
            BatchAnalysisSnapshotStore.Save(Path.Combine(_root, "snapshots"), Context(batchId, SevenMods), logFile));
    }

    [Fact]
    public void A_log_copy_that_no_longer_matches_its_recorded_hash_is_rejected()
    {
        var logFile = Path.Combine(_root, "ConanSandbox.log");
        File.WriteAllLines(logFile, CannibalBootLog());
        var dir = BatchAnalysisSnapshotStore.Save(Path.Combine(_root, "snapshots"), Context("batch-c", SevenMods), logFile);

        var copy = Path.Combine(dir, BatchAnalysisSnapshotStore.LogFileName);
        File.SetAttributes(copy, FileAttributes.Normal);
        File.AppendAllText(copy, "tampered");

        Assert.Throws<InvalidDataException>(() => BatchAnalysisSnapshotStore.Load(dir));
    }

    [Fact]
    public void A_snapshot_judges_a_boot_by_its_recorded_catalog_not_by_the_catalog_that_is_current_later()
    {
        // The Ancient Realms merge warning is a known warning in the current catalog. A snapshot recorded before
        // it existed must still treat the two lines as unknown, however much the code accepts later.
        var warning = ModBootGates.KnownWarnings.Single(w => w.Id == "ANCIENT-REALMS-MERGE-DATATABLE-NULL");
        var line = "[2026.10.02-20.29.01:885][  0]" + warning.ExactMessage;
        string[] log = [line, line];
        var older = ValidatedCatalog.Current with
        {
            KnownWarnings = ValidatedCatalog.Current.KnownWarnings.Where(w => w.Id != warning.Id).ToList()
        };

        var recorded = Context("older-batch", SevenMods, older);
        Assert.Equal(2, BootLogAnalyzer.Analyze(log, recorded, _ => null).Unknown.Count);

        var logFile = Path.Combine(_root, "ConanSandbox.log");
        File.WriteAllLines(logFile, log);
        var (reloaded, _) = BatchAnalysisSnapshotStore.Load(BatchAnalysisSnapshotStore.Save(Path.Combine(_root, "snapshots"), recorded, logFile));
        Assert.Equal(2, BootLogAnalyzer.Analyze(log, reloaded, _ => null).Unknown.Count);

        var today = Context("today", SevenMods);
        var current = BootLogAnalyzer.Analyze(log, today, _ => null);
        Assert.Empty(current.Unknown);
        Assert.Equal(2, current.Known.Count);
    }

    [Fact]
    public void Known_warnings_use_the_recorded_pak_hash_not_the_file_on_disk_now()
    {
        var warning = ModBootGates.KnownWarnings.Single(w => w.Id == "ANCIENT-REALMS-MERGE-DATATABLE-NULL");
        var line = "[2026.10.02-20.29.01:885][  0]" + warning.ExactMessage;
        var changedFile = Context("changed-hash", SevenMods) with
        {
            Mods = SevenMods.Select(m => new SnapshotMod(m, m == "Ancient_Realms.pak" ? new string('0', 64) : Fake(m), 1)).ToArray()
        };

        Assert.Equal(2, BootLogAnalyzer.Analyze([line, line], changedFile, _ => null).Unknown.Count);
    }

    private static PreBatchSnapshot Plan(string batchId, ValidatedCatalog? catalog = null)
    {
        var cat = catalog ?? ValidatedCatalog.Current;
        var baseline = Context("baseline", SevenMods);
        var added = new SnapshotMod("Shemite_City_State.pak", Fake("Shemite_City_State.pak"), 2098034644);
        return new PreBatchSnapshot(batchId, new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc), "2026-10-04_001102", "d1c31ff",
            BatchAnalysisSnapshotStore.CatalogSha256(cat), SevenMods, baseline.Mods, added, @"C:\mods\Shemite_City_State.pak",
            [.. SevenMods, added.FileName], cat);
    }

    [Fact]
    public void A_pre_batch_plan_round_trips_and_is_read_only_and_cannot_be_reused()
    {
        var plan = Plan("shemite-pre");
        var root = Path.Combine(_root, "snapshots");

        var dir = BatchAnalysisSnapshotStore.SavePreBatch(root, plan);
        var loaded = BatchAnalysisSnapshotStore.LoadPreBatch(dir);

        Assert.Equal(plan.PreBatchBackupId, loaded.PreBatchBackupId);
        Assert.Equal(plan.CodeHead, loaded.CodeHead);
        Assert.Equal(plan.CatalogSha256, loaded.CatalogSha256);
        Assert.Equal(plan.BaselineModList, loaded.BaselineModList);
        Assert.Equal(plan.BaselineMods, loaded.BaselineMods);
        Assert.Equal(plan.ExpectedNewMod, loaded.ExpectedNewMod);
        Assert.Equal([.. SevenMods, "Shemite_City_State.pak"], loaded.ExpectedModList);
        Assert.Equal(loaded.CatalogSha256, BatchAnalysisSnapshotStore.CatalogSha256(loaded.Catalog));
        Assert.True(File.GetAttributes(Path.Combine(dir, BatchAnalysisSnapshotStore.PreBatchFileName)).HasFlag(FileAttributes.ReadOnly));
        Assert.Throws<IOException>(() => BatchAnalysisSnapshotStore.SavePreBatch(root, plan));
        Assert.Throws<ArgumentException>(() => BatchAnalysisSnapshotStore.SavePreBatch(root, Plan("..")));
    }

    [Fact]
    public void A_pre_batch_plan_can_validate_the_baseline_itself_with_no_new_mod()
    {
        var plan = Plan("core-final-pre") with { ExpectedNewMod = null, ExpectedNewModSourcePath = null, ExpectedModList = SevenMods };

        var loaded = BatchAnalysisSnapshotStore.LoadPreBatch(BatchAnalysisSnapshotStore.SavePreBatch(Path.Combine(_root, "snapshots"), plan));

        Assert.Null(loaded.ExpectedNewMod);
        Assert.Null(loaded.ExpectedNewModSourcePath);
        Assert.Equal(loaded.BaselineModList, loaded.ExpectedModList);
    }

    [Fact]
    public void The_catalog_hash_identifies_the_exact_rule_set()
    {
        var current = BatchAnalysisSnapshotStore.CatalogSha256(ValidatedCatalog.Current);

        Assert.Equal(current, BatchAnalysisSnapshotStore.CatalogSha256(ValidatedCatalog.Current));
        Assert.NotEqual(current, BatchAnalysisSnapshotStore.CatalogSha256(ValidatedCatalog.Current with { PhaseBoundWarnings = null }));
        Assert.NotEqual(current, BatchAnalysisSnapshotStore.CatalogSha256(ValidatedCatalog.Current with
        {
            KnownWarnings = ValidatedCatalog.Current.KnownWarnings.Skip(1).ToList()
        }));
    }
}
