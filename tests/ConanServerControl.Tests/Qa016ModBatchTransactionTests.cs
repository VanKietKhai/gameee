using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Concurrency;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.Updates;
using ConanServerControl.Infrastructure.Workshop;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class Qa016ModBatchTransactionTests
{
    [Fact]
    [Trait("Issue", "QA-016")]
    public async Task TestA_two_mods_second_replace_fails_restores_both_old_and_fails()
    {
        var fx = await BatchFixture.CreateAsync(modCount: 2, lastDestIsDirectory: true, ServerStatus.Online);

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateEverythingAsync());

        Assert.IsType<UserFacingException>(error);
        Assert.False(fx.ClaimedSuccess);
        Assert.Equal("MOD1-OLD", await File.ReadAllTextAsync(fx.LivePaths[0]));
        Assert.True(Directory.Exists(fx.LivePaths[1]), "Mod2 original (directory) must remain.");
        Assert.False(File.Exists(fx.LivePaths[1]));
        AssertBatchFailed(error);
    }

    [Fact]
    [Trait("Issue", "QA-016")]
    public async Task TestB_three_mods_third_replace_fails_restores_all_old_and_fails()
    {
        var fx = await BatchFixture.CreateAsync(modCount: 3, lastDestIsDirectory: true, ServerStatus.Online);

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateEverythingAsync());

        Assert.IsType<UserFacingException>(error);
        Assert.False(fx.ClaimedSuccess);
        Assert.Equal("MOD1-OLD", await File.ReadAllTextAsync(fx.LivePaths[0]));
        Assert.Equal("MOD2-OLD", await File.ReadAllTextAsync(fx.LivePaths[1]));
        Assert.True(Directory.Exists(fx.LivePaths[2]));
        Assert.False(File.Exists(fx.LivePaths[2]));
        AssertBatchFailed(error);
    }

    [Fact]
    [Trait("Issue", "QA-016")]
    public async Task TestC_replace_fails_rollback_succeeds_previously_online_server_may_restart_but_operation_failed()
    {
        var fx = await BatchFixture.CreateAsync(modCount: 2, lastDestIsDirectory: true, ServerStatus.Online);

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateEverythingAsync());

        Assert.IsType<UserFacingException>(error);
        Assert.False(fx.ClaimedSuccess);
        Assert.Equal("MOD1-OLD", await File.ReadAllTextAsync(fx.LivePaths[0]));
        Assert.True(Directory.Exists(fx.LivePaths[1]));
        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
        Assert.Contains("start-under-lock", fx.Server.Calls);
        Assert.Contains(
            fx.Activity.Messages,
            m => m.Contains("Previous mod set restored", StringComparison.OrdinalIgnoreCase)
                 && m.Contains("Restarting server with previous versions", StringComparison.OrdinalIgnoreCase));
        var display = ((UserFacingException)error!).FormatForDisplay();
        Assert.Contains("Previous mod set restored", display, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recovery required", display, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Issue", "QA-016")]
    public async Task TestD_replace_fails_and_rollback_fails_leaves_previously_online_server_offline()
    {
        var fx = await BatchFixture.CreateAsync(modCount: 2, lastDestIsDirectory: true, ServerStatus.Online);
        fx.Mods.AfterLiveReplacementForTests = ctx =>
        {
            if (Directory.Exists(ctx.RollbackDirectory))
            {
                Directory.Delete(ctx.RollbackDirectory, recursive: true);
            }
        };

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateEverythingAsync());

        Assert.IsType<UserFacingException>(error);
        Assert.False(fx.ClaimedSuccess);
        Assert.Equal(ServerStatus.Offline, fx.Server.State.Status);
        Assert.DoesNotContain("start-under-lock", fx.Server.Calls);
        var ufe = (UserFacingException)error!;
        Assert.Contains("recovery required", ufe.Title, StringComparison.OrdinalIgnoreCase);
        var display = ufe.FormatForDisplay();
        Assert.Contains("rollback was incomplete", display, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("left offline", display, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            fx.Activity.Messages,
            m => m.Contains("rollback was incomplete", StringComparison.OrdinalIgnoreCase)
                 && m.Contains("left offline", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            fx.Activity.Messages,
            m => m.Contains("Update everything completed", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Issue", "QA-016")]
    public async Task Rollback_verify_throw_leaves_previously_online_server_offline()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            // POSIX mode bits are what make the rollback copy unreadable here.
            // TestD covers the missing-copy path on every OS.
            return;
        }

        var fx = await BatchFixture.CreateAsync(modCount: 2, lastDestIsDirectory: true, ServerStatus.Online);
        string? rollbackCopy = null;
        fx.Mods.AfterLiveReplacementForTests = ctx =>
        {
            if (ctx.RollbackPath is null || !File.Exists(ctx.RollbackPath))
            {
                return;
            }

            rollbackCopy = ctx.RollbackPath;
            File.SetUnixFileMode(ctx.RollbackPath, UnixFileMode.None);
        };

        try
        {
            var error = await Record.ExceptionAsync(() => fx.Updates.UpdateEverythingAsync());

            Assert.IsType<UserFacingException>(error);
            Assert.False(fx.ClaimedSuccess);
            Assert.Equal(ServerStatus.Offline, fx.Server.State.Status);
            Assert.DoesNotContain("start-under-lock", fx.Server.Calls);
            var display = ((UserFacingException)error!).FormatForDisplay();
            Assert.Contains("recovery required", display, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (rollbackCopy is not null && File.Exists(rollbackCopy))
            {
                File.SetUnixFileMode(
                    rollbackCopy,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
    }

    [Fact]
    [Trait("Issue", "QA-016")]
    public async Task TestE_all_replacements_succeed_live_mods_are_new_and_rollback_is_cleaned()
    {
        var fx = await BatchFixture.CreateAsync(modCount: 2, lastDestIsDirectory: false, ServerStatus.Online);

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateEverythingAsync());

        Assert.Null(error);
        Assert.True(fx.ClaimedSuccess);
        Assert.Equal("MOD1-NEW", await File.ReadAllTextAsync(fx.LivePaths[0]));
        Assert.Equal("MOD2-NEW", await File.ReadAllTextAsync(fx.LivePaths[1]));
        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
        Assert.False(HasLeftoverRollback(fx.Paths.StagingDirectory));
        Assert.Contains(fx.Activity.Messages, m => m.Contains("Installed/updated Workshop mod 1.", StringComparison.Ordinal));
        Assert.Contains(fx.Activity.Messages, m => m.Contains("Installed/updated Workshop mod 2.", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Issue", "QA-016")]
    public async Task TestF_originally_offline_commit_failure_with_successful_rollback_stays_offline()
    {
        var fx = await BatchFixture.CreateAsync(modCount: 2, lastDestIsDirectory: true, ServerStatus.Offline);

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateEverythingAsync());

        Assert.IsType<UserFacingException>(error);
        Assert.False(fx.ClaimedSuccess);
        Assert.Equal("MOD1-OLD", await File.ReadAllTextAsync(fx.LivePaths[0]));
        Assert.True(Directory.Exists(fx.LivePaths[1]));
        Assert.Equal(ServerStatus.Offline, fx.Server.State.Status);
        Assert.DoesNotContain("start-under-lock", fx.Server.Calls);
        Assert.DoesNotContain("stop-under-lock", fx.Server.Calls);
    }

    private static void AssertBatchFailed(Exception? error)
    {
        Assert.IsType<UserFacingException>(error);
        Assert.NotNull(FindBatch(error));
        Assert.True(FindBatch(error)!.RollbackCompleted);
        Assert.False(FindBatch(error)!.RecoveryRequired);
    }

    private static ModBatchCommitException? FindBatch(Exception? error)
    {
        for (var current = error; current is not null; current = current.InnerException)
        {
            if (current is ModBatchCommitException batch)
            {
                return batch;
            }
        }

        return null;
    }

    private static bool HasLeftoverRollback(string stagingDirectory)
    {
        var root = Path.Combine(stagingDirectory, "mod-update");
        if (!Directory.Exists(root))
        {
            return false;
        }

        return Directory.EnumerateDirectories(root).Any(dir =>
            Directory.Exists(Path.Combine(dir, "rollback"))
            && Directory.EnumerateFileSystemEntries(Path.Combine(dir, "rollback")).Any());
    }

    private sealed class BatchFixture
    {
        public required AppPaths Paths { get; init; }

        public required List<string> LivePaths { get; init; }

        public required RecordingServer Server { get; init; }

        public required RecordingActivityLog Activity { get; init; }

        public required WorkshopModService Mods { get; init; }

        public required ServerUpdateService Updates { get; init; }

        public bool ClaimedSuccess =>
            Activity.Messages.Any(m => m.Contains("Update everything completed", StringComparison.Ordinal));

        public static async Task<BatchFixture> CreateAsync(int modCount, bool lastDestIsDirectory, ServerStatus status)
        {
            var (data, paths, settings) = QaTestSupport.CreateData();
            var install = QaTestSupport.InstallRoot(data);
            await QaTestSupport.ConfigureInstallAsync(settings, install);
            var modsDir = Path.Combine(install, "ConanSandbox", "Mods");
            Directory.CreateDirectory(modsDir);
            var livePaths = new List<string>(modCount);
            await settings.UpdateAsync(s =>
            {
                s.Backups.BackupBeforeServerUpdate = true;
                s.Backups.BackupBeforeModUpdate = true;
            });

            for (var i = 1; i <= modCount; i++)
            {
                var fileName = $"Mod{i}.pak";
                var live = Path.Combine(modsDir, fileName);
                livePaths.Add(live);
                var isLast = i == modCount;
                if (lastDestIsDirectory && isLast)
                {
                    Directory.CreateDirectory(live);
                }
                else
                {
                    await File.WriteAllTextAsync(live, $"MOD{i}-OLD");
                }

                var index = i;
                await settings.UpdateAsync(s =>
                {
                    s.Mods.Mods.Add(new WorkshopMod
                    {
                        WorkshopId = index,
                        Name = $"Mod{index}",
                        Enabled = true,
                        LoadOrder = index,
                        LocalFileName = fileName
                    });
                });
            }

            var gate = new ServerActionGate();
            var server = new RecordingServer(gate);
            server.State.Status = status;
            var steam = new ScriptedSteamCmd
            {
                OnWorkshop = (id, dir, _) =>
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, $"Mod{id}.pak"), $"MOD{id}-NEW");
                    return Task.CompletedTask;
                }
            };
            var activity = new RecordingActivityLog();
            var mods = new WorkshopModService(
                settings,
                steam,
                new CountingBackup(),
                gate,
                paths,
                new EmptyWorkshopClient(),
                activity,
                NullLogger<WorkshopModService>.Instance);
            var updates = new ServerUpdateService(
                settings,
                steam,
                server,
                new CountingBackup(),
                mods,
                gate,
                activity,
                NullLogger<ServerUpdateService>.Instance);

            return new BatchFixture
            {
                Paths = paths,
                LivePaths = livePaths,
                Server = server,
                Activity = activity,
                Mods = mods,
                Updates = updates
            };
        }
    }
}
