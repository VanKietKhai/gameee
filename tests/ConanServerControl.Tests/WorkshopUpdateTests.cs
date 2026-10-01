using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Mods;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.Settings;
using ConanServerControl.Infrastructure.Workshop;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class WorkshopUpdateComparerTests
{
    [Fact]
    public void Missing_install_means_update_available()
    {
        Assert.True(WorkshopUpdateComparer.IsUpdateAvailable(null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Newer_remote_timestamp_is_an_update()
    {
        var installed = DateTimeOffset.UnixEpoch.AddSeconds(100);
        var remote = DateTimeOffset.UnixEpoch.AddSeconds(200);
        Assert.True(WorkshopUpdateComparer.IsUpdateAvailable(installed, remote));
        Assert.False(WorkshopUpdateComparer.IsUpdateAvailable(remote, installed));
    }
}

public class WorkshopModServiceCheckTests
{
    [Fact]
    public async Task CheckForUpdates_sets_flags_from_remote_metadata_without_restarting()
    {
        var data = Path.Combine(Path.GetTempPath(), "csc-tests", Guid.NewGuid().ToString("n"));
        var paths = new AppPaths(data);
        paths.EnsureCreated();
        var settings = new JsonSettingsService(paths, new PassthroughProtector(), NullLogger<JsonSettingsService>.Instance);
        await settings.LoadAsync();
        await settings.UpdateAsync(s =>
        {
            s.Mods.Mods.Add(new WorkshopMod
            {
                WorkshopId = 111,
                Name = "Old Name",
                InstalledTimestamp = DateTimeOffset.UnixEpoch.AddSeconds(10),
                LoadOrder = 1,
                Enabled = true
            });
        });

        var client = new FakeWorkshopClient(
        [
            new WorkshopPublishedFileDetails
            {
                WorkshopId = 111,
                Title = "Pippi",
                TimeUpdated = DateTimeOffset.UnixEpoch.AddSeconds(50),
                FileName = "Pippi.pak"
            }
        ]);

        var service = new WorkshopModService(
            settings,
            new NoOpSteamCmd(),
            new NoOpBackup(),
            new ConanServerControl.Infrastructure.Concurrency.ServerActionGate(),
            paths,
            client,
            new MemoryActivityLog(),
            NullLogger<WorkshopModService>.Instance);

        await service.CheckForUpdatesAsync();
        var mod = Assert.Single(service.Mods);
        Assert.Equal("Pippi", mod.Name);
        Assert.True(mod.UpdateAvailable);
        Assert.Equal("Pippi.pak", mod.LocalFileName);
        Assert.NotNull(mod.LastChecked);
    }
}

file sealed class FakeWorkshopClient : ISteamWorkshopClient
{
    private readonly IReadOnlyList<WorkshopPublishedFileDetails> _details;

    public FakeWorkshopClient(IReadOnlyList<WorkshopPublishedFileDetails> details) => _details = details;

    public Task<IReadOnlyList<WorkshopPublishedFileDetails>> GetPublishedFileDetailsAsync(
        IReadOnlyList<long> workshopIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_details);
}

file sealed class NoOpSteamCmd : ISteamCmdService
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
        Task.FromResult(new ProcessExecutionResult { ExitCode = 0 });

    public Task<ProcessExecutionResult> DownloadWorkshopItemAsync(
        long workshopId,
        string installDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProcessExecutionResult { ExitCode = 0 });
}

file sealed class NoOpBackup : IBackupService
{
    public Task<BackupRecord> BackupNowAsync(string reason, CancellationToken cancellationToken = default) =>
        Task.FromResult(new BackupRecord { Id = "t", CreatedAt = DateTimeOffset.UtcNow, Reason = reason });

    public Task RestoreAsync(string backupId, bool startAfter, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BackupRecord>>(Array.Empty<BackupRecord>());

    public Task ApplyRetentionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
