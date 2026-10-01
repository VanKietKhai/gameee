using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Updates;
using ConanServerControl.Infrastructure.Concurrency;
using ConanServerControl.Infrastructure.ProcessManagement;
using ConanServerControl.Infrastructure.Updates;
using ConanServerControl.Infrastructure.Workshop;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class QaActionGateAndPipelineTests
{
    [Fact]
    public async Task Update_server_while_online_is_one_backup_one_stop_one_steamcmd_one_start()
    {
        var fx = PipelineFixture.Create(ServerStatus.Online);
        await fx.Updates.UpdateAsync(restartAfter: true);

        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
        Assert.Equal(new[] { "pre-server-update" }, fx.Backup.Reasons);
        Assert.Equal(new[] { "stop-under-lock", "start-under-lock" }, fx.Server.Calls);
        Assert.Equal(new[] { "server-update" }, fx.Steam.Events);
        Assert.Equal(0, fx.Mods.UpdateAllCount);
    }

    [Fact]
    public async Task Update_mods_while_online_is_one_backup_one_stop_one_mod_pass_one_start()
    {
        var fx = PipelineFixture.Create(ServerStatus.Online);
        await fx.Updates.UpdateModsAsync(restartAfter: true);

        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
        Assert.Equal(new[] { "pre-mod-update" }, fx.Backup.Reasons);
        Assert.Equal(new[] { "stop-under-lock", "start-under-lock" }, fx.Server.Calls);
        Assert.Empty(fx.Steam.Events);
        Assert.Equal(1, fx.Mods.UpdateAllCount);
    }

    [Fact]
    public async Task Update_everything_while_online_is_a_single_cycle()
    {
        var fx = PipelineFixture.Create(ServerStatus.Online);
        await fx.Updates.UpdateEverythingAsync();

        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
        Assert.Equal(1, fx.Backup.Count);
        Assert.Equal("pre-update-everything", Assert.Single(fx.Backup.Reasons));
        Assert.Equal(new[] { "stop-under-lock", "start-under-lock" }, fx.Server.Calls);
        Assert.Equal(new[] { "server-update" }, fx.Steam.Events);
        Assert.Equal(1, fx.Mods.UpdateAllCount);
    }

    [Fact]
    [Trait("Issue", "QA-005")]
    public async Task Update_selected_is_rejected_while_restart_holds_the_gate()
    {
        var fx = PipelineFixture.Create(ServerStatus.Online);
        Assert.True(fx.Gate.TryBegin("Restart server", out var lease));
        try
        {
            var error = await Record.ExceptionAsync(() => fx.Updates.UpdateSelectedModsAsync(1));
            Assert.IsType<UserFacingException>(error);
            Assert.Equal(0, fx.Mods.UpdateAllCount);
        }
        finally
        {
            lease!.Dispose();
        }
    }

    [Fact]
    [Trait("Issue", "QA-005")]
    public async Task Update_all_is_rejected_while_update_server_holds_the_gate()
    {
        var fx = PipelineFixture.Create(ServerStatus.Online);
        Assert.True(fx.Gate.TryBegin("Update server", out var lease));
        try
        {
            var selected = await Record.ExceptionAsync(() => fx.Updates.UpdateModsAsync(false));
            Assert.IsType<UserFacingException>(selected);
        }
        finally
        {
            lease!.Dispose();
        }

        await fx.Updates.UpdateModsAsync(false);
        Assert.Equal(1, fx.Mods.UpdateAllCount);
    }

    [Fact]
    public async Task Real_process_manager_update_while_online_does_not_deadlock_on_the_gate()
    {
        var (data, _, settings) = QaTestSupport.CreateData();
        var install = QaTestSupport.InstallRoot(data);
        await QaTestSupport.ConfigureInstallAsync(settings, install);
        await settings.UpdateAsync(s => s.Backups.BackupBeforeServerUpdate = false);
        var starter = new FakeProcessStarter { Settings = settings };
        var gate = new ServerActionGate();
        var manager = new ServerProcessManager(
            settings,
            starter,
            gate,
            new MemoryActivityLog(),
            new FakeRcon(),
            NullLogger<ServerProcessManager>.Instance);
        var steam = new ScriptedSteamCmd();
        string? statusDuringSteam = null;
        steam.OnServer = _ =>
        {
            statusDuringSteam = manager.State.Status.ToString();
            return Task.CompletedTask;
        };
        var updates = new ServerUpdateService(
            settings,
            steam,
            manager,
            new CountingBackup(),
            new ScriptedWorkshop(),
            gate,
            new MemoryActivityLog(),
            NullLogger<ServerUpdateService>.Instance);

        await manager.StartAsync();
        Assert.Equal(ServerStatus.Online, manager.State.Status);
        starter.Last.HasExited = true;

        await updates.UpdateAsync(restartAfter: true);

        Assert.Equal(nameof(ServerStatus.Offline), statusDuringSteam);
        Assert.Equal(ServerStatus.Online, manager.State.Status);
        Assert.False(gate.IsBusy);
    }

    [Fact]
    public async Task Concurrent_restart_and_stop_are_rejected_while_an_update_holds_the_gate()
    {
        var (data, _, settings) = QaTestSupport.CreateData();
        var install = QaTestSupport.InstallRoot(data);
        await QaTestSupport.ConfigureInstallAsync(settings, install);
        var starter = new FakeProcessStarter { Settings = settings };
        var gate = new ServerActionGate();
        var manager = new ServerProcessManager(
            settings,
            starter,
            gate,
            new MemoryActivityLog(),
            new FakeRcon(),
            NullLogger<ServerProcessManager>.Instance);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backup = new CountingBackup
        {
            OnBackup = async (_, _) =>
            {
                entered.TrySetResult(true);
                await release.Task;
            }
        };
        var updates = new ServerUpdateService(
            settings,
            new ScriptedSteamCmd(),
            manager,
            backup,
            new ScriptedWorkshop(),
            gate,
            new MemoryActivityLog(),
            NullLogger<ServerUpdateService>.Instance);

        await manager.StartAsync();
        starter.Last.HasExited = true;
        var updateTask = updates.UpdateAsync(restartAfter: true);
        await entered.Task;

        var restart = await Record.ExceptionAsync(() => manager.RestartAsync());
        var stop = await Record.ExceptionAsync(() => manager.StopAsync());
        release.TrySetResult(true);
        await updateTask;

        var restartError = Assert.IsType<UserFacingException>(restart);
        var stopError = Assert.IsType<UserFacingException>(stop);
        Assert.Contains("already running", restartError.FormatForDisplay(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("already running", stopError.FormatForDisplay(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ServerStatus.Online, manager.State.Status);
        Assert.False(gate.IsBusy);
    }

    [Fact]
    public async Task Exception_while_the_gate_is_held_releases_it_for_a_later_action()
    {
        var fx = PipelineFixture.Create(ServerStatus.Online);
        fx.Steam.ServerException = new InvalidOperationException("steam exploded");

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateAsync(restartAfter: true));

        Assert.IsType<UserFacingException>(error);
        Assert.False(fx.Gate.IsBusy);
        fx.Steam.ServerException = null;
        await fx.Updates.UpdateAsync(restartAfter: true);
        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
        Assert.False(fx.Gate.IsBusy);
    }

    [Fact]
    public async Task StartUnderLock_without_any_lease_is_rejected()
    {
        var manager = await CreateRealManagerAsync();
        var error = await Record.ExceptionAsync(() => manager.StartUnderLockAsync(null!));
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(ServerStatus.Offline, manager.State.Status);
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-008")]
    public async Task UnderLock_start_must_require_the_caller_to_own_the_lease()
    {
        var (manager, gate) = await CreateRealManagerWithGateAsync();
        Assert.True(gate.TryBegin("Update server", out var lease));
        try
        {
            var error = await Record.ExceptionAsync(() => manager.StartUnderLockAsync(new ForeignLease()));
            Assert.True(
                error is InvalidOperationException,
                $"A caller that does not own the lease must not start the server. error={error?.GetType().Name}: {error?.Message}; status={manager.State.Status}");
            Assert.Equal(ServerStatus.Offline, manager.State.Status);
        }
        finally
        {
            lease!.Dispose();
        }
    }

    [Fact]
    [Trait("Issue", "QA-008")]
    public async Task UnderLock_start_with_the_owning_lease_starts_the_server()
    {
        var (manager, gate) = await CreateRealManagerWithGateAsync();
        Assert.True(gate.TryBegin("Update server", out var lease));
        try
        {
            await manager.StartUnderLockAsync(lease!);
            Assert.Equal(ServerStatus.Online, manager.State.Status);
        }
        finally
        {
            lease!.Dispose();
        }
    }

    private sealed class ForeignLease : IServerOperationLease
    {
        public Guid Id { get; } = Guid.NewGuid();

        public string Action => "foreign";

        public bool IsDisposed => false;

        public void Dispose()
        {
        }
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-003")]
    public async Task Update_server_while_offline_stays_offline_when_start_afterwards_is_not_requested()
    {
        await AssertStaysOfflineAsync(fx => fx.Updates.UpdateAsync(restartAfter: false));
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-003")]
    public async Task Update_mods_while_offline_stays_offline_when_start_afterwards_is_not_requested()
    {
        await AssertStaysOfflineAsync(fx => fx.Updates.UpdateModsAsync(restartAfter: false));
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-003")]
    public async Task Shipped_update_server_call_starts_a_server_that_was_offline()
    {
        var fx = PipelineFixture.Create(ServerStatus.Offline);
        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateAsync(restartAfter: true));
        Assert.True(
            error is null && fx.Server.State.Status == ServerStatus.Offline && fx.Server.Calls.Contains("start-under-lock") == false,
            $"UPDATE SERVER while offline must not start the server. error={error?.Message}; status={fx.Server.State.Status}; calls=[{string.Join(", ", fx.Server.Calls)}]");
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-003")]
    public async Task Shipped_update_mods_call_starts_a_server_that_was_offline()
    {
        var fx = PipelineFixture.Create(ServerStatus.Offline);
        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateModsAsync(restartAfter: true));
        Assert.True(
            error is null && fx.Server.State.Status == ServerStatus.Offline && !fx.Server.Calls.Contains("start-under-lock"),
            $"UPDATE MODS while offline must not start the server. error={error?.Message}; status={fx.Server.State.Status}; calls=[{string.Join(", ", fx.Server.Calls)}]");
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-003")]
    public async Task Update_everything_while_offline_stays_offline()
    {
        var fx = PipelineFixture.Create(ServerStatus.Offline);
        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateEverythingAsync());
        Assert.True(
            error is null && fx.Server.State.Status == ServerStatus.Offline && !fx.Server.Calls.Contains("start-under-lock"),
            $"UPDATE EVERYTHING while offline must not start the server. error={error?.Message}; status={fx.Server.State.Status}; calls=[{string.Join(", ", fx.Server.Calls)}]");
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-004")]
    public void Pipeline_can_complete_after_validation_when_the_server_is_not_started()
    {
        var machine = new UpdatePipelineStateMachine();
        machine.Begin();
        machine.TransitionTo(UpdatePipelineState.Checking);
        machine.TransitionTo(UpdatePipelineState.UpdatingMods);
        machine.TransitionTo(UpdatePipelineState.Validating);
        var done = machine.TransitionTo(UpdatePipelineState.Completed, "Update completed.");
        Assert.Equal(UpdatePipelineState.Completed, done.State);
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-006")]
    public async Task Server_update_success_plus_mod_update_failure_must_not_leave_a_running_server_down_or_a_partial_mod_set()
    {
        var (data, paths, settings) = QaTestSupport.CreateData();
        var install = QaTestSupport.InstallRoot(data);
        await QaTestSupport.ConfigureInstallAsync(settings, install);
        var modsDir = Path.Combine(install, "ConanSandbox", "Mods");
        Directory.CreateDirectory(modsDir);
        var firstPak = Path.Combine(modsDir, "Mod1.pak");
        var secondPak = Path.Combine(modsDir, "Mod2.pak");
        await File.WriteAllTextAsync(firstPak, "MOD1-OLD");
        await File.WriteAllTextAsync(secondPak, "MOD2-OLD");
        await settings.UpdateAsync(s =>
        {
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 1, Name = "One", Enabled = true, LoadOrder = 1, LocalFileName = "Mod1.pak" });
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 2, Name = "Two", Enabled = true, LoadOrder = 2, LocalFileName = "Mod2.pak" });
        });

        var gate = new ServerActionGate();
        var server = new RecordingServer(gate) { };
        server.State.Status = ServerStatus.Online;
        var steam = new ScriptedSteamCmd
        {
            OnWorkshop = (id, dir, _) =>
            {
                if (id == 2)
                {
                    throw new InvalidOperationException("mod download failed");
                }

                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "Mod1.pak"), "MOD1-NEW");
                return Task.CompletedTask;
            }
        };
        var backup = new CountingBackup();
        var mods = new WorkshopModService(
            settings,
            steam,
            backup,
            gate,
            paths,
            new EmptyWorkshopClient(),
            new RecordingActivityLog(),
            NullLogger<WorkshopModService>.Instance);
        var updates = new ServerUpdateService(
            settings,
            steam,
            server,
            backup,
            mods,
            gate,
            new RecordingActivityLog(),
            NullLogger<ServerUpdateService>.Instance);

        var error = await Record.ExceptionAsync(() => updates.UpdateEverythingAsync());
        var mod1 = await File.ReadAllTextAsync(firstPak);
        var mod2 = await File.ReadAllTextAsync(secondPak);
        var toldStopped = error is UserFacingException ufe
            && ufe.FormatForDisplay().Contains("stopped", StringComparison.OrdinalIgnoreCase);

        Assert.True(
            error is UserFacingException
            && server.State.Status == ServerStatus.Online
            && mod1 == "MOD1-OLD"
            && mod2 == "MOD2-OLD"
            && backup.Count == 1
            && server.Calls.Count(c => c == "stop-under-lock") == 1
            && !server.Calls.Contains("start-under-lock") == false,
            $"Failure after a successful server update must roll back or restore the previous online state and leave both mods intact. " +
            $"error={error?.GetType().Name}: {error?.Message}; toldStopped={toldStopped}; status={server.State.Status}; " +
            $"calls=[{string.Join(", ", server.Calls)}]; backups={backup.Count} [{string.Join(", ", backup.Reasons)}]; " +
            $"steam=[{string.Join(", ", steam.Events)}]; mod1={mod1}; mod2={mod2}");
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-016")]
    public async Task Partial_live_commit_must_not_restart_onto_a_mixed_mod_set()
    {
        var (data, paths, settings) = QaTestSupport.CreateData();
        var install = QaTestSupport.InstallRoot(data);
        await QaTestSupport.ConfigureInstallAsync(settings, install);
        var modsDir = Path.Combine(install, "ConanSandbox", "Mods");
        Directory.CreateDirectory(modsDir);
        var firstPak = Path.Combine(modsDir, "Mod1.pak");
        var secondPak = Path.Combine(modsDir, "Mod2.pak");
        await File.WriteAllTextAsync(firstPak, "MOD1-OLD");
        Directory.CreateDirectory(secondPak);
        await settings.UpdateAsync(s =>
        {
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 1, Name = "One", Enabled = true, LoadOrder = 1, LocalFileName = "Mod1.pak" });
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 2, Name = "Two", Enabled = true, LoadOrder = 2, LocalFileName = "Mod2.pak" });
            s.Backups.BackupBeforeServerUpdate = true;
            s.Backups.BackupBeforeModUpdate = true;
        });

        var gate = new ServerActionGate();
        var server = new RecordingServer(gate);
        server.State.Status = ServerStatus.Online;
        var steam = new ScriptedSteamCmd
        {
            OnWorkshop = (id, dir, _) =>
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, id == 2 ? "Mod2.pak" : "Mod1.pak"), id == 2 ? "MOD2-NEW" : "MOD1-NEW");
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

        var error = await Record.ExceptionAsync(() => updates.UpdateEverythingAsync());
        var mod1 = await File.ReadAllTextAsync(firstPak);
        var partial = mod1 == "MOD1-NEW";
        var restarted = server.Calls.Contains("start-under-lock");
        var claimedSuccess = activity.Messages.Any(m => m.Contains("Update everything completed", StringComparison.Ordinal));

        Assert.True(
            error is UserFacingException
            && !claimedSuccess
            && !(partial && restarted),
            $"A failed live commit must not start a previously online server on a mixed mod set. " +
            $"Either roll the replaced pak back before start, or leave the server stopped. " +
            $"error={error?.GetType().Name}: {error?.Message}; partial={partial}; restarted={restarted}; " +
            $"status={server.State.Status}; calls=[{string.Join(", ", server.Calls)}]; mod1={mod1}; " +
            $"mod2IsDir={Directory.Exists(secondPak)}; activity=[{string.Join(" | ", activity.Messages)}]");
    }

    [Fact]
    [Trait("Issue", "QA-003")]
    public async Task Update_server_failure_while_offline_stays_offline()
    {
        var fx = PipelineFixture.Create(ServerStatus.Offline);
        fx.Steam.ServerException = new InvalidOperationException("steam exploded");

        var error = await Record.ExceptionAsync(() => fx.Updates.UpdateAsync(restartAfter: true));

        Assert.IsType<UserFacingException>(error);
        Assert.Equal(ServerStatus.Offline, fx.Server.State.Status);
        Assert.DoesNotContain("start-under-lock", fx.Server.Calls);
        Assert.DoesNotContain("stop-under-lock", fx.Server.Calls);
        Assert.False(fx.Gate.IsBusy);
    }

    [Fact]
    [Trait("Issue", "QA-008")]
    public async Task Disposed_lease_and_a_lease_from_another_gate_cannot_start_under_lock()
    {
        var (manager, gate) = await CreateRealManagerWithGateAsync();
        Assert.True(gate.TryBegin("Update server", out var lease));
        var otherGate = new ServerActionGate();
        Assert.True(otherGate.TryBegin("other", out var foreign));
        try
        {
            var wrongGate = await Record.ExceptionAsync(() => manager.StartUnderLockAsync(foreign!));
            Assert.IsType<InvalidOperationException>(wrongGate);

            lease!.Dispose();
            var disposed = await Record.ExceptionAsync(() => manager.StartUnderLockAsync(lease));
            Assert.IsType<InvalidOperationException>(disposed);
            Assert.Equal(ServerStatus.Offline, manager.State.Status);
            Assert.False(gate.IsBusy);
        }
        finally
        {
            foreign!.Dispose();
            if (!lease!.IsDisposed)
            {
                lease.Dispose();
            }
        }
    }

    [Fact]
    public async Task Update_all_downloads_every_enabled_mod_including_ones_without_update_available()
    {
        var (data, paths, settings) = QaTestSupport.CreateData();
        var install = QaTestSupport.InstallRoot(data);
        await QaTestSupport.ConfigureInstallAsync(settings, install);
        var modsDir = Path.Combine(install, "ConanSandbox", "Mods");
        Directory.CreateDirectory(modsDir);
        await settings.UpdateAsync(s =>
        {
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 1, Name = "Current", Enabled = true, LoadOrder = 1, UpdateAvailable = false, LocalFileName = "A.pak" });
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 2, Name = "Needed", Enabled = true, LoadOrder = 2, UpdateAvailable = true, LocalFileName = "B.pak" });
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 3, Name = "Off", Enabled = false, LoadOrder = 3, UpdateAvailable = true, LocalFileName = "C.pak" });
        });
        var steam = new ScriptedSteamCmd
        {
            OnWorkshop = (id, dir, _) =>
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, $"M{id}.pak"), "x");
                return Task.CompletedTask;
            }
        };
        var mods = new WorkshopModService(
            settings, steam, new CountingBackup(), new ServerActionGate(), paths, new EmptyWorkshopClient(), new RecordingActivityLog(), NullLogger<WorkshopModService>.Instance);

        await mods.UpdateAllAsync();

        Assert.Equal(new long[] { 1, 2 }, steam.WorkshopIds);
    }

    private static async Task AssertStaysOfflineAsync(Func<PipelineFixture, Task> act)
    {
        var fx = PipelineFixture.Create(ServerStatus.Offline);
        var error = await Record.ExceptionAsync(() => act(fx));
        Assert.True(
            error is null && fx.Server.State.Status == ServerStatus.Offline && !fx.Server.Calls.Contains("start-under-lock"),
            $"Offline update without start-afterwards must complete and stay offline. error={error?.GetType().Name}: {error?.Message}; status={fx.Server.State.Status}; calls=[{string.Join(", ", fx.Server.Calls)}]");
    }

    private static async Task<ServerProcessManager> CreateRealManagerAsync()
    {
        var (manager, _) = await CreateRealManagerWithGateAsync();
        return manager;
    }

    private static async Task<(ServerProcessManager Manager, ServerActionGate Gate)> CreateRealManagerWithGateAsync()
    {
        var (data, _, settings) = QaTestSupport.CreateData();
        var install = QaTestSupport.InstallRoot(data);
        await QaTestSupport.ConfigureInstallAsync(settings, install);
        var gate = new ServerActionGate();
        var manager = new ServerProcessManager(
            settings,
            new FakeProcessStarter { Settings = settings },
            gate,
            new MemoryActivityLog(),
            new FakeRcon(),
            NullLogger<ServerProcessManager>.Instance);
        return (manager, gate);
    }

    private sealed class PipelineFixture
    {
        public required ServerActionGate Gate { get; init; }

        public required RecordingServer Server { get; init; }

        public required CountingBackup Backup { get; init; }

        public required ScriptedSteamCmd Steam { get; init; }

        public required ScriptedWorkshop Mods { get; init; }

        public required ServerUpdateService Updates { get; init; }

        public static PipelineFixture Create(ServerStatus status)
        {
            var (data, _, settings) = QaTestSupport.CreateData();
            var install = QaTestSupport.InstallRoot(data);
            QaTestSupport.ConfigureInstallAsync(settings, install).GetAwaiter().GetResult();
            var gate = new ServerActionGate();
            var server = new RecordingServer(gate);
            server.State.Status = status;
            var backup = new CountingBackup();
            var steam = new ScriptedSteamCmd();
            var mods = new ScriptedWorkshop();
            var updates = new ServerUpdateService(
                settings,
                steam,
                server,
                backup,
                mods,
                gate,
                new RecordingActivityLog(),
                NullLogger<ServerUpdateService>.Instance);
            return new PipelineFixture
            {
                Gate = gate,
                Server = server,
                Backup = backup,
                Steam = steam,
                Mods = mods,
                Updates = updates
            };
        }
    }
}
