using System.Text.Json;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Backups;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Settings;
using ConanServerControl.Infrastructure.Backups;
using ConanServerControl.Infrastructure.Concurrency;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

/// <summary>
/// Restore must keep the selected backup intact for the whole copy and must not report success
/// when that source was removed or nothing was copied.
/// </summary>
public class QaRestoreSafetyTests
{
    [Fact]
    public async Task Restore_of_a_recent_backup_copies_world_and_keeps_the_source()
    {
        var harness = await RestoreHarness.CreateAsync(keepDays: 14);
        var source = harness.WriteBackup("2026-09-15_120000", DateTimeOffset.Now.AddDays(-1), "FROM-A");
        await File.WriteAllTextAsync(harness.LiveMarker, "CURRENT");

        await harness.Service.RestoreAsync("2026-09-15_120000", startAfter: false);

        Assert.True(Directory.Exists(source));
        Assert.Equal("FROM-A", await File.ReadAllTextAsync(harness.LiveMarker));
        Assert.Contains(harness.Activity.Messages, m => m.Contains("Restored backup 2026-09-15_120000", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-001")]
    public async Task Restore_must_not_delete_the_source_backup_or_report_success_when_it_is_gone()
    {
        // Defaults: keep latest 10 and 14 days. Backup A is older than 14 days and is the 11th
        // newest once the pre-restore safety backup is created, so retention can delete it
        // before the copy runs.
        var harness = await RestoreHarness.CreateAsync(keepDays: 14);
        var source = harness.WriteBackup("2026-08-01_000001", DateTimeOffset.Now.AddDays(-40), "FROM-A");
        for (var i = 0; i < 10; i++)
        {
            harness.WriteBackup(
                $"2026-09-{(i + 1):00}_120000",
                DateTimeOffset.Now.AddHours(-(i + 1)),
                marker: null);
        }

        await File.WriteAllTextAsync(harness.LiveMarker, "CURRENT");

        var error = await Record.ExceptionAsync(() => harness.Service.RestoreAsync("2026-08-01_000001", startAfter: false));
        var sourceSurvived = Directory.Exists(source);
        var live = File.Exists(harness.LiveMarker) ? await File.ReadAllTextAsync(harness.LiveMarker) : "<missing>";
        var saidRestored = harness.Activity.Messages.Any(m => m.Contains("Restored backup", StringComparison.Ordinal));

        Assert.True(
            error is null && sourceSurvived && live == "FROM-A",
            $"Restore source must stay on disk and the live world must become FROM-A. " +
            $"error={error?.GetType().Name}: {error?.Message}; sourceSurvived={sourceSurvived}; live={live}; saidRestored={saidRestored}; " +
            $"activity=[{string.Join(" | ", harness.Activity.Messages)}]");
    }

    [Fact]
    [Trait("Issue", "QA-001")]
    public async Task Restore_must_fail_and_not_log_success_when_copy_fails()
    {
        var harness = await RestoreHarness.CreateAsync(keepDays: 14);
        harness.WriteBackup("2026-09-15_120000", DateTimeOffset.Now.AddDays(-1), "FROM-A");
        if (File.Exists(harness.LiveMarker))
        {
            File.Delete(harness.LiveMarker);
        }

        Directory.CreateDirectory(harness.LiveMarker);

        var error = await Record.ExceptionAsync(() => harness.Service.RestoreAsync("2026-09-15_120000", startAfter: false));
        var saidRestored = harness.Activity.Messages.Any(m => m.Contains("Restored backup", StringComparison.Ordinal));

        Assert.True(
            error is ConanServerControl.Core.Exceptions.UserFacingException && !saidRestored,
            $"A copy failure must return an explicit error and must not log restore success. " +
            $"error={error?.GetType().Name}: {error?.Message}; saidRestored={saidRestored}; " +
            $"activity=[{string.Join(" | ", harness.Activity.Messages)}]");
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-012")]
    public async Task Restore_must_not_run_while_another_action_holds_the_gate()
    {
        var harness = await RestoreHarness.CreateAsync(keepDays: 14);
        harness.WriteBackup("2026-09-15_120000", DateTimeOffset.Now.AddDays(-1), "FROM-A");
        await File.WriteAllTextAsync(harness.LiveMarker, "CURRENT");

        // Share the same gate instance the service uses. A disconnected new() gate
        // cannot prove Restore participates in global operation coordination.
        Assert.True(harness.Gate.TryBegin("Update server", out var lease));
        try
        {
            var error = await Record.ExceptionAsync(() => harness.Service.RestoreAsync("2026-09-15_120000", startAfter: false));
            Assert.IsType<ConanServerControl.Core.Exceptions.UserFacingException>(error);
        }
        finally
        {
            lease!.Dispose();
        }
    }

    [Fact]
    public async Task Restore_refuses_while_status_is_online()
    {
        var harness = await RestoreHarness.CreateAsync(keepDays: 14);
        harness.Server.State.Status = ServerStatus.Online;
        harness.WriteBackup("2026-09-15_120000", DateTimeOffset.Now.AddDays(-1), "FROM-A");

        var error = await Record.ExceptionAsync(() => harness.Service.RestoreAsync("2026-09-15_120000", startAfter: false));

        var rejected = Assert.IsType<ConanServerControl.Core.Exceptions.UserFacingException>(error);
        Assert.Contains("running", rejected.FormatForDisplay(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Issue", "QA-012")]
    public async Task Failed_restore_releases_the_action_gate()
    {
        var harness = await RestoreHarness.CreateAsync(keepDays: 14);
        harness.WriteBackup("2026-09-15_120000", DateTimeOffset.Now.AddDays(-1), "FROM-A");
        if (File.Exists(harness.LiveMarker))
        {
            File.Delete(harness.LiveMarker);
        }

        Directory.CreateDirectory(harness.LiveMarker);

        var error = await Record.ExceptionAsync(() => harness.Service.RestoreAsync("2026-09-15_120000", startAfter: false));
        Assert.NotNull(error);
        Assert.True(
            harness.Gate.TryBegin("probe", out var lease),
            "A failed restore must release the action gate.");
        lease!.Dispose();
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-007")]
    public async Task Backup_that_copies_no_world_must_not_be_reported_as_success()
    {
        var (data, paths, settings) = QaTestSupport.CreateData();
        var install = QaTestSupport.InstallRoot(data);
        Directory.CreateDirectory(install);
        await settings.UpdateAsync(s => s.ServerPaths.ServerInstallDirectory = install);
        var activity = new RecordingActivityLog();
        var service = new BackupService(paths, settings, new StatusOnlyServer(), new ServerActionGate(), activity, NullLogger<BackupService>.Instance);

        var error = await Record.ExceptionAsync(() => service.BackupNowAsync("pre-server-update"));
        var completed = activity.Messages.Any(m => m.Contains("Backup completed", StringComparison.Ordinal));
        Assert.True(
            error is ConanServerControl.Core.Exceptions.UserFacingException && !completed,
            $"An empty pre-update backup must fail visibly. error={error?.GetType().Name}: {error?.Message}; activity=[{string.Join(" | ", activity.Messages)}]");
    }

    [Fact]
    [Trait("Issue", "QA-007")]
    public async Task Backup_of_a_valid_world_is_reported_as_success()
    {
        var (data, paths, settings) = QaTestSupport.CreateData();
        var install = QaTestSupport.InstallRoot(data);
        Directory.CreateDirectory(Path.Combine(install, "ConanSandbox", "Saved"));
        await File.WriteAllTextAsync(Path.Combine(install, "ConanSandbox", "Saved", "marker.txt"), "WORLD");
        await settings.UpdateAsync(s => s.ServerPaths.ServerInstallDirectory = install);
        var activity = new RecordingActivityLog();
        var service = new BackupService(paths, settings, new StatusOnlyServer(), new ServerActionGate(), activity, NullLogger<BackupService>.Instance);

        var record = await service.BackupNowAsync("manual");

        Assert.True(record.IncludesWorld);
        Assert.Contains(activity.Messages, m => m.Contains("Backup completed", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(record.DirectoryPath, "world", "marker.txt")));
    }

    [Fact]
    [Trait("Issue", "QA-007")]
    public async Task Backup_must_fail_when_world_copy_throws()
    {
        var (data, paths, settings) = QaTestSupport.CreateData();
        var install = QaTestSupport.InstallRoot(data);
        var saved = Path.Combine(install, "ConanSandbox", "Saved");
        Directory.CreateDirectory(saved);
        var blocked = Path.Combine(saved, "game.db");
        await File.WriteAllTextAsync(blocked, "WORLD");
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(blocked, UnixFileMode.None);
        }

        await settings.UpdateAsync(s => s.ServerPaths.ServerInstallDirectory = install);
        var activity = new RecordingActivityLog();
        var service = new BackupService(paths, settings, new StatusOnlyServer(), new ServerActionGate(), activity, NullLogger<BackupService>.Instance);

        var error = await Record.ExceptionAsync(() => service.BackupNowAsync("pre-server-update"));
        var completed = activity.Messages.Any(m => m.Contains("Backup completed", StringComparison.Ordinal));

        Assert.True(
            error is ConanServerControl.Core.Exceptions.UserFacingException && !completed,
            $"A throwing world copy must fail visibly. error={error?.GetType().Name}: {error?.Message}; activity=[{string.Join(" | ", activity.Messages)}]");
    }

    [Fact]
    [Trait("Issue", "QA-007")]
    public async Task Backup_must_fail_when_the_destination_cannot_be_written()
    {
        var (data, paths, settings) = QaTestSupport.CreateData();
        var install = QaTestSupport.InstallRoot(data);
        Directory.CreateDirectory(Path.Combine(install, "ConanSandbox", "Saved"));
        await File.WriteAllTextAsync(Path.Combine(install, "ConanSandbox", "Saved", "marker.txt"), "WORLD");
        await settings.UpdateAsync(s => s.ServerPaths.ServerInstallDirectory = install);
        Directory.CreateDirectory(paths.BackupsDirectory);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(paths.BackupsDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        var activity = new RecordingActivityLog();
        var service = new BackupService(paths, settings, new StatusOnlyServer(), new ServerActionGate(), activity, NullLogger<BackupService>.Instance);

        var error = await Record.ExceptionAsync(() => service.BackupNowAsync("pre-server-update"));
        var completed = activity.Messages.Any(m => m.Contains("Backup completed", StringComparison.Ordinal));

        Assert.True(
            error is not null && !completed,
            $"An unwritable backup destination must fail. error={error?.GetType().Name}: {error?.Message}; activity=[{string.Join(" | ", activity.Messages)}]");

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(
                paths.BackupsDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private sealed class RestoreHarness
    {
        public required BackupService Service { get; init; }

        public required IServerActionGate Gate { get; init; }

        public required StatusOnlyServer Server { get; init; }

        public required RecordingActivityLog Activity { get; init; }

        public required string BackupRoot { get; init; }

        public required string LiveMarker { get; init; }

        public static async Task<RestoreHarness> CreateAsync(int? keepDays)
        {
            var (data, paths, settings) = QaTestSupport.CreateData();
            var install = QaTestSupport.InstallRoot(data);
            Directory.CreateDirectory(Path.Combine(install, "ConanSandbox", "Saved"));
            await settings.UpdateAsync(s =>
            {
                s.ServerPaths.ServerInstallDirectory = install;
                s.ServerPaths.ServerWorkingDirectory = install;
                s.Backups.KeepLatest = BackupKeepLatest.Ten;
                s.Backups.KeepDays = keepDays;
            });

            var server = new StatusOnlyServer();
            var activity = new RecordingActivityLog();
            var gate = new ServerActionGate();
            return new RestoreHarness
            {
                Service = new BackupService(paths, settings, server, gate, activity, NullLogger<BackupService>.Instance),
                Server = server,
                Gate = gate,
                Activity = activity,
                BackupRoot = paths.BackupsDirectory,
                LiveMarker = Path.Combine(install, "ConanSandbox", "Saved", "marker.txt")
            };
        }

        public string WriteBackup(string id, DateTimeOffset created, string? marker)
        {
            var dir = Path.Combine(BackupRoot, id);
            var world = Path.Combine(dir, "world");
            Directory.CreateDirectory(world);
            if (marker is not null)
            {
                File.WriteAllText(Path.Combine(world, "marker.txt"), marker);
            }

            var record = new BackupRecord
            {
                Id = id,
                DirectoryPath = dir,
                CreatedAt = created,
                Reason = "fixture",
                IncludesWorld = marker is not null
            };
            File.WriteAllText(
                Path.Combine(dir, "metadata.json"),
                JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
            return dir;
        }
    }
}
