using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Concurrency;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.ProcessManagement;
using ConanServerControl.Infrastructure.Settings;
using ConanServerControl.Infrastructure.Updates;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class ServerUpdatePipelineTests
{
    [Fact]
    public async Task UpdateAsync_can_stop_and_start_while_holding_the_action_gate()
    {
        var data = Path.Combine(Path.GetTempPath(), "csc-tests", Guid.NewGuid().ToString("n"));
        var paths = new AppPaths(data);
        paths.EnsureCreated();
        var settings = new JsonSettingsService(paths, new PassthroughProtector(), NullLogger<JsonSettingsService>.Instance);
        await settings.LoadAsync();

        var temp = Path.Combine(data, "server");
        Directory.CreateDirectory(temp);
        var exe = Path.Combine(temp, "ConanSandboxServer.exe");
        await File.WriteAllTextAsync(exe, "fake");
        await settings.UpdateAsync(s =>
        {
            s.ServerPaths.ServerExecutablePath = exe;
            s.ServerPaths.ServerWorkingDirectory = temp;
            s.ServerPaths.ServerInstallDirectory = temp;
            s.Advanced.GracefulStopTimeoutSeconds = 1;
            s.Advanced.ForceStopTimeoutSeconds = 1;
            s.Backups.BackupBeforeServerUpdate = false;
        });

        var starter = new FakeProcessStarter { Settings = settings };
        var gate = new ServerActionGate();
        var activity = new MemoryActivityLog();
        var manager = new ServerProcessManager(
            settings,
            starter,
            gate,
            activity,
            new FakeRcon(),
            new ImmediateReadyProbe(),
            NullLogger<ServerProcessManager>.Instance);

        await manager.StartAsync();
        Assert.Equal(ServerStatus.Online, manager.State.Status);

        var updates = new ServerUpdateService(
            settings,
            new FakeSteamCmd(),
            manager,
            new FakeBackup(),
            new FakeWorkshop(),
            gate,
            activity,
            NullLogger<ServerUpdateService>.Instance);

        await updates.UpdateAsync(restartAfter: true);
        Assert.Equal(ServerStatus.Online, manager.State.Status);
    }
}

file sealed class FakeSteamCmd : ISteamCmdService
{
    public string? ExecutablePath => "steamcmd.exe";
    public bool IsInstalled => true;
    public Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<ProcessExecutionResult> InstallOrUpdateDedicatedServerAsync(
        string installDirectory,
        bool validate,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProcessExecutionResult { ExitCode = 0, Duration = TimeSpan.FromMilliseconds(1) });

    public Task<ProcessExecutionResult> DownloadWorkshopItemAsync(
        long workshopId,
        string installDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProcessExecutionResult { ExitCode = 0, Duration = TimeSpan.FromMilliseconds(1) });
}

file sealed class FakeBackup : IBackupService
{
    public Task<BackupRecord> BackupNowAsync(string reason, CancellationToken cancellationToken = default) =>
        Task.FromResult(new BackupRecord { Id = "t", DirectoryPath = "/tmp", CreatedAt = DateTimeOffset.UtcNow, Reason = reason });

    public Task RestoreAsync(string backupId, bool startAfter, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BackupRecord>>(Array.Empty<BackupRecord>());

    public Task ApplyRetentionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class FakeWorkshop : IWorkshopModService
{
    public IReadOnlyList<WorkshopMod> Mods => Array.Empty<WorkshopMod>();
    public Task AddAsync(long workshopId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAsync(long workshopId, bool confirmed, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetEnabledAsync(long workshopId, bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task MoveAsync(long workshopId, int newIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateAsync(long workshopId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ApplyUpdatesAsync(IReadOnlyList<long>? workshopIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CheckForUpdatesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public string GetShareableModList() => string.Empty;
}
