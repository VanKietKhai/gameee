using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Updates;

namespace ConanServerControl.Tests;

public class M3ColdBackupPipelineTests
{
    [Fact]
    public async Task Online_update_stops_before_backup_and_backup_before_mutation()
    {
        var fx = CreateOnlineFixture();
        await fx.Updates.UpdateAsync(restartAfter: true);

        Assert.Equal(new[] { "stop", "backup", "update", "start" }, fx.Timeline);
        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
    }

    [Fact]
    public async Task Online_update_everything_is_one_stop_one_backup_server_then_mods_one_start()
    {
        var fx = CreateOnlineFixture();
        await fx.Updates.UpdateEverythingAsync();

        Assert.Equal(new[] { "stop", "backup", "update", "mods", "start" }, fx.Timeline);
        Assert.Equal(1, fx.Backup.Count);
        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
    }

    [Fact]
    public async Task Offline_update_does_not_stop_or_start()
    {
        var fx = CreateOfflineFixture();
        await fx.Updates.UpdateAsync(restartAfter: true);

        Assert.DoesNotContain("stop", fx.Timeline);
        Assert.DoesNotContain("start", fx.Timeline);
        Assert.Equal(new[] { "backup", "update" }, fx.Timeline);
        Assert.Equal(ServerStatus.Offline, fx.Server.State.Status);
    }

    [Fact]
    public async Task Backup_failure_blocks_mutation_and_restarts_previously_online_server()
    {
        var fx = CreateOnlineFixture();
        fx.Backup.FailWith = new UserFacingException("Backup failed", "hash failed");

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateAsync(restartAfter: true));

        Assert.IsType<UserFacingException>(error);
        Assert.Contains("safety backup failed", error!.ToString() + ((UserFacingException)error).FormatForDisplay(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fx.Steam.Events);
        Assert.Equal(0, fx.Mods.UpdateAllCount);
        Assert.DoesNotContain("update", fx.Timeline);
        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
        Assert.Contains(fx.Activity.Messages, m => m.Contains("safety backup failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Backup_failure_while_offline_stays_offline_and_does_not_mutate()
    {
        var fx = CreateOfflineFixture();
        fx.Backup.FailWith = new UserFacingException("Backup failed", "sqlite invalid");

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateAsync(restartAfter: true));

        Assert.IsType<UserFacingException>(error);
        Assert.Empty(fx.Steam.Events);
        Assert.Equal(ServerStatus.Offline, fx.Server.State.Status);
        Assert.DoesNotContain("start", fx.Timeline);
    }

    [Fact]
    public async Task Server_that_cannot_stop_aborts_without_backup_or_update()
    {
        var fx = CreateOnlineFixture();
        fx.Server.StopLeavesRunning = true;

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateAsync(restartAfter: true));

        Assert.IsType<UserFacingException>(error);
        Assert.Equal(0, fx.Backup.Count);
        Assert.Empty(fx.Steam.Events);
        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
        Assert.DoesNotContain("backup", fx.Timeline);
        Assert.DoesNotContain("update", fx.Timeline);
    }

    [Fact]
    public async Task Hash_or_sqlite_failure_is_backup_failure_and_blocks_mutation()
    {
        var fx = CreateOnlineFixture();
        fx.Backup.FailWith = new UserFacingException("Backup failed", "PRAGMA quick_check reported corruption");

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateEverythingAsync());

        Assert.IsType<UserFacingException>(error);
        Assert.Empty(fx.Steam.Events);
        Assert.Equal(0, fx.Mods.UpdateAllCount);
        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
    }

    private static PipelineFx CreateOnlineFixture() => PipelineFx.Create(ServerStatus.Online);

    private static PipelineFx CreateOfflineFixture() => PipelineFx.Create(ServerStatus.Offline);

    private sealed class PipelineFx
    {
        public required RecordingServer Server { get; init; }

        public required CountingBackup Backup { get; init; }

        public required ScriptedSteamCmd Steam { get; init; }

        public required ScriptedWorkshop Mods { get; init; }

        public required ServerUpdateService Updates { get; init; }

        public required List<string> Timeline { get; init; }

        public required RecordingActivityLog Activity { get; init; }

        public static PipelineFx Create(ServerStatus status)
        {
            var (data, _, settings) = QaTestSupport.CreateData();
            var install = QaTestSupport.InstallRoot(data);
            QaTestSupport.ConfigureInstallAsync(settings, install).GetAwaiter().GetResult();
            var gate = new ConanServerControl.Infrastructure.Concurrency.ServerActionGate();
            var timeline = new List<string>();
            var server = new RecordingServer(gate) { Timeline = timeline };
            server.State.Status = status;
            var backup = new CountingBackup { Timeline = timeline };
            var steam = new ScriptedSteamCmd { Timeline = timeline };
            var mods = new ScriptedWorkshop { Timeline = timeline };
            var activity = new RecordingActivityLog();
            var updates = new ServerUpdateService(
                settings,
                steam,
                server,
                backup,
                mods,
                gate,
                activity,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ServerUpdateService>.Instance);
            return new PipelineFx
            {
                Server = server,
                Backup = backup,
                Steam = steam,
                Mods = mods,
                Updates = updates,
                Timeline = timeline,
                Activity = activity
            };
        }
    }
}
