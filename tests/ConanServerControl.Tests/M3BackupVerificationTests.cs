using ConanServerControl.Core.Backups;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Backups;
using ConanServerControl.Infrastructure.Concurrency;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class M3BackupVerificationTests
{
    [Fact]
    public async Task Enhanced_main_db_valid_backup_is_verified()
    {
        var harness = await WorldBackupHarness.CreateAsync();
        SqliteTestDb.WriteValid(Path.Combine(harness.Saved, "game_0.db"));
        File.WriteAllText(Path.Combine(harness.Saved, "unrelated.db"), "ignore-me");

        var record = await harness.Service.BackupNowAsync("manual");

        Assert.True(record.Succeeded);
        Assert.True(record.ManifestWritten);
        Assert.True(record.HashesVerified);
        Assert.True(record.SqliteVerified);
        Assert.Equal(ConanWorldFiles.WorldTypeEnhanced, record.WorldType);
        Assert.Equal("game_0.db", record.MainDbFileName);
        Assert.Contains(record.WorldFiles, f => f.FileName == "game_0.db" && f.Sha256.Length == 64 && f.SizeBytes > 0);
        Assert.DoesNotContain(record.WorldFiles, f => f.FileName.Contains("unrelated", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(Path.Combine(record.DirectoryPath, "world", "unrelated.db")));
    }

    [Fact]
    public async Task Enhanced_db_with_wal_and_shm_verifies_when_sqlite_set_is_valid()
    {
        var harness = await WorldBackupHarness.CreateAsync();
        var liveMain = Path.Combine(harness.Saved, "game_0.db");
        SqliteTestDb.WriteValidWithWal(liveMain);
        var wal = liveMain + "-wal";
        var shm = liveMain + "-shm";

        var record = await harness.Service.BackupNowAsync("pre-server-update");

        Assert.True(record.Succeeded, record.VerificationDetail);
        Assert.True(record.SqliteVerified);
        Assert.Contains(record.WorldFiles, f => f.FileName == "game_0.db");
        if (File.Exists(wal))
        {
            Assert.Contains(record.WorldFiles, f => f.FileName == "game_0.db-wal");
            Assert.True(File.Exists(wal));
        }

        if (File.Exists(shm))
        {
            Assert.Contains(record.WorldFiles, f => f.FileName == "game_0.db-shm");
            Assert.True(File.Exists(shm));
        }
    }

    [Fact]
    public async Task Legacy_main_db_valid_backup_is_verified()
    {
        var harness = await WorldBackupHarness.CreateAsync();
        SqliteTestDb.WriteValid(Path.Combine(harness.Saved, "game.db"));

        var record = await harness.Service.BackupNowAsync("manual");

        Assert.True(record.Succeeded);
        Assert.Equal(ConanWorldFiles.WorldTypeLegacy, record.WorldType);
        Assert.Equal("game.db", record.MainDbFileName);
        var copied = Path.Combine(record.DirectoryPath, "world", "game.db");
        Assert.Equal(BackupFileHasher.Sha256File(copied), Assert.Single(record.WorldFiles).Sha256);
    }

    [Fact]
    public async Task Main_db_missing_is_invalid()
    {
        var harness = await WorldBackupHarness.CreateAsync();
        File.WriteAllText(Path.Combine(harness.Saved, "marker.txt"), "WORLD");
        File.WriteAllText(Path.Combine(harness.Saved, "foo.db"), "not-the-world");

        var error = await Record.ExceptionAsync(() => harness.Service.BackupNowAsync("pre-server-update"));

        Assert.IsType<UserFacingException>(error);
        Assert.DoesNotContain(harness.Activity.Messages, m => m.Contains("Backup completed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Non_sqlite_corrupt_db_is_invalid()
    {
        var harness = await WorldBackupHarness.CreateAsync();
        SqliteTestDb.WriteCorrupt(Path.Combine(harness.Saved, "game_0.db"));

        var error = await Record.ExceptionAsync(() => harness.Service.BackupNowAsync("pre-server-update"));

        Assert.IsType<UserFacingException>(error);
        Assert.DoesNotContain(harness.Activity.Messages, m => m.Contains("Backup completed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Quick_check_error_is_invalid()
    {
        var temp = Path.Combine(Path.GetTempPath(), "csc-m3-verify", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(temp);
        var main = Path.Combine(temp, "game.db");
        SqliteTestDb.WriteValid(main);
        var bytes = File.ReadAllBytes(main);
        if (bytes.Length > 80)
        {
            for (var i = 80; i < Math.Min(bytes.Length, 200); i++)
            {
                bytes[i] ^= 0x5A;
            }

            File.WriteAllBytes(main, bytes);
        }

        var record = new BackupRecord
        {
            MainDbFileName = "game.db",
            WorldFiles =
            [
                new BackupWorldFileRecord
                {
                    LogicalName = "game.db",
                    FileName = "game.db",
                    SizeBytes = new FileInfo(main).Length,
                    Sha256 = BackupFileHasher.Sha256File(main)
                }
            ]
        };

        var result = await new SqliteBackupVerifier(NullLogger<SqliteBackupVerifier>.Instance)
            .VerifyAsync(temp, record);

        Assert.False(result.Succeeded);
        Assert.False(result.SqliteVerified);
    }

    [Fact]
    public async Task Hash_manifest_matches_copied_file_and_mismatch_revalidation_fails()
    {
        var harness = await WorldBackupHarness.CreateAsync();
        SqliteTestDb.WriteValid(Path.Combine(harness.Saved, "game.db"));
        var record = await harness.Service.BackupNowAsync("manual");
        var copied = Path.Combine(record.DirectoryPath, "world", "game.db");
        Assert.Equal(BackupFileHasher.Sha256File(copied), record.WorldFiles[0].Sha256);

        await File.AppendAllTextAsync(copied, "tamper");
        var verifier = new SqliteBackupVerifier(NullLogger<SqliteBackupVerifier>.Instance);
        var recheck = await verifier.VerifyAsync(Path.Combine(record.DirectoryPath, "world"), record);

        Assert.False(recheck.Succeeded);
        Assert.False(recheck.HashesVerified);
        Assert.Contains("mismatch", recheck.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unrelated_db_is_ignored_in_the_world_manifest()
    {
        var harness = await WorldBackupHarness.CreateAsync();
        SqliteTestDb.WriteValid(Path.Combine(harness.Saved, "game.db"));
        File.WriteAllText(Path.Combine(harness.Saved, "player.db"), "nope");
        File.WriteAllText(Path.Combine(harness.Saved, "game.bak.db"), "nope");

        var record = await harness.Service.BackupNowAsync("manual");

        Assert.Equal(["game.db"], record.WorldFiles.Select(f => f.FileName).ToArray());
        Assert.True(record.Succeeded);
    }

    [Fact]
    public async Task Missing_copied_main_db_fails_verifier()
    {
        var temp = Path.Combine(Path.GetTempPath(), "csc-m3-verify", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(temp);
        var record = new BackupRecord { MainDbFileName = "game_0.db" };
        var result = await new SqliteBackupVerifier(NullLogger<SqliteBackupVerifier>.Instance)
            .VerifyAsync(temp, record);
        Assert.False(result.Succeeded);
        Assert.Contains("missing", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manual_backup_while_online_stops_then_backups_then_restarts()
    {
        var harness = await WorldBackupHarness.CreateAsync();
        SqliteTestDb.WriteValid(Path.Combine(harness.Saved, "game.db"));
        harness.Server.State.Status = ServerStatus.Online;

        var record = await harness.Service.BackupNowAsync("dashboard");

        Assert.True(record.Succeeded);
        Assert.Equal(new[] { "stop-under-lock", "start-under-lock" }, harness.Server.Calls);
        Assert.Equal(ServerStatus.Online, harness.Server.State.Status);
    }

    private sealed class WorldBackupHarness
    {
        public required BackupService Service { get; init; }

        public required RecordingServer Server { get; init; }

        public required RecordingActivityLog Activity { get; init; }

        public required string Saved { get; init; }

        public static async Task<WorldBackupHarness> CreateAsync()
        {
            var (data, paths, settings) = QaTestSupport.CreateData();
            var install = QaTestSupport.InstallRoot(data);
            var saved = Path.Combine(install, "ConanSandbox", "Saved");
            Directory.CreateDirectory(saved);
            await settings.UpdateAsync(s =>
            {
                s.ServerPaths.ServerInstallDirectory = install;
                s.ServerPaths.ServerWorkingDirectory = install;
            });
            var gate = new ServerActionGate();
            var server = new RecordingServer(gate);
            var activity = new RecordingActivityLog();
            return new WorldBackupHarness
            {
                Service = new BackupService(
                    paths,
                    settings,
                    server,
                    gate,
                    activity,
                    new SqliteBackupVerifier(NullLogger<SqliteBackupVerifier>.Instance),
                    NullLogger<BackupService>.Instance),
                Server = server,
                Activity = activity,
                Saved = saved
            };
        }
    }
}
