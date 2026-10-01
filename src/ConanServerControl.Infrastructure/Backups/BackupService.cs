using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Backups;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ConanServerControl.Infrastructure.Backups;

public sealed class BackupService : IBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IAppPaths _paths;
    private readonly ISettingsService _settings;
    private readonly IServerProcessManager _server;
    private readonly IActivityLog _activityLog;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IAppPaths paths,
        ISettingsService settings,
        IServerProcessManager server,
        IActivityLog activityLog,
        ILogger<BackupService> logger)
    {
        _paths = paths;
        _settings = settings;
        _server = server;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<BackupRecord> BackupNowAsync(string reason, CancellationToken cancellationToken = default)
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var dest = Path.Combine(_paths.BackupsDirectory, stamp);
        if (!PathValidator.IsUnderRoot(dest, _paths.BackupsDirectory) &&
            !string.Equals(Path.GetFullPath(dest), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFacingException("Invalid backup path", dest, "Backup folders are created under the application backups directory only.");
        }

        Directory.CreateDirectory(dest);
        var worldDir = Path.Combine(dest, "world");
        var configDir = Path.Combine(dest, "config");
        var modlistDir = Path.Combine(dest, "modlist");
        Directory.CreateDirectory(worldDir);
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(modlistDir);

        var install = _settings.Current.ServerPaths.ServerInstallDirectory
                      ?? _settings.Current.ServerPaths.ServerWorkingDirectory;

        var includesWorld = false;
        var includesConfig = false;
        var includesModList = false;

        if (!string.IsNullOrWhiteSpace(install) && Directory.Exists(install))
        {
            includesWorld = CopyIfExists(Path.Combine(install, "ConanSandbox", "Saved"), worldDir);
            includesConfig = CopyIfExists(Path.Combine(install, "ConanSandbox", "Saved", "Config"), configDir);
            includesModList = CopyIfExists(
                Path.Combine(install, "ConanSandbox", "Mods", "modlist.txt"),
                Path.Combine(modlistDir, "modlist.txt"));
        }

        var record = new BackupRecord
        {
            Id = stamp,
            DirectoryPath = dest,
            CreatedAt = DateTimeOffset.Now,
            Reason = reason,
            IncludesWorld = includesWorld,
            IncludesConfig = includesConfig,
            IncludesModList = includesModList,
            Notes = string.IsNullOrWhiteSpace(install)
                ? "No dedicated server install directory is configured. An empty backup folder with metadata was created."
                : null
        };

        record.SizeBytes = GetDirectorySize(dest);
        var metadataPath = Path.Combine(dest, "metadata.json");
        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(record, JsonOptions), cancellationToken)
            .ConfigureAwait(false);

        await _settings.UpdateAsync(s => { }, cancellationToken).ConfigureAwait(false);
        await _activityLog.AddAsync("Backup", $"Backup completed ({stamp}).", details: reason, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("Backup {Id} created at {Path}", stamp, dest);

        await ApplyRetentionAsync(cancellationToken).ConfigureAwait(false);
        return record;
    }

    public async Task RestoreAsync(string backupId, bool startAfter, CancellationToken cancellationToken = default)
    {
        if (!PathValidator.IsSafeRelativeName(backupId))
        {
            throw new UserFacingException(
                "Invalid backup id",
                backupId,
                "Select a backup from the Backups list. Manual path entry is not allowed.");
        }

        if (_server.State.Status is not ServerStatus.Offline and not ServerStatus.Error)
        {
            throw new UserFacingException(
                "Cannot restore while the server is running",
                "Restore must stop the live world first so files are not overwritten mid-write.",
                "Stop the server, then restore. A safety backup of the current world is created automatically.");
        }

        var source = Path.Combine(_paths.BackupsDirectory, backupId);
        if (!Directory.Exists(source) || !PathValidator.IsUnderRoot(source, _paths.BackupsDirectory))
        {
            throw new UserFacingException(
                "Backup not found",
                source,
                "Refresh the Backups list and choose an existing backup.");
        }

        await BackupNowAsync("pre-restore", cancellationToken).ConfigureAwait(false);

        var install = _settings.Current.ServerPaths.ServerInstallDirectory
                      ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        if (string.IsNullOrWhiteSpace(install) || !Directory.Exists(install))
        {
            throw new UserFacingException(
                "Server install directory is not configured",
                install ?? "(not set)",
                "Set the dedicated server folder in Settings before restoring.");
        }

        CopyDirectoryIfExists(Path.Combine(source, "world"), Path.Combine(install, "ConanSandbox", "Saved"));
        CopyDirectoryIfExists(Path.Combine(source, "config"), Path.Combine(install, "ConanSandbox", "Saved", "Config"));
        var modlistSource = Path.Combine(source, "modlist", "modlist.txt");
        if (File.Exists(modlistSource))
        {
            var dest = Path.Combine(install, "ConanSandbox", "Mods", "modlist.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(modlistSource, dest, overwrite: true);
        }

        await _activityLog.AddAsync("Backup", $"Restored backup {backupId}.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (startAfter)
        {
            await _server.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_paths.BackupsDirectory);
        var records = new List<BackupRecord>();
        foreach (var dir in Directory.GetDirectories(_paths.BackupsDirectory))
        {
            var metadata = Path.Combine(dir, "metadata.json");
            if (File.Exists(metadata))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<BackupRecord>(File.ReadAllText(metadata));
                    if (parsed is not null)
                    {
                        records.Add(parsed);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read backup metadata in {Dir}", dir);
                }
            }

            var info = new DirectoryInfo(dir);
            records.Add(new BackupRecord
            {
                Id = info.Name,
                DirectoryPath = dir,
                CreatedAt = info.CreationTime,
                Reason = "unknown"
            });
        }

        return Task.FromResult<IReadOnlyList<BackupRecord>>(records.OrderByDescending(r => r.CreatedAt).ToArray());
    }

    public async Task ApplyRetentionAsync(CancellationToken cancellationToken = default)
    {
        var backups = await ListAsync(cancellationToken).ConfigureAwait(false);
        var doomed = BackupRetentionPolicy.SelectForDeletion(backups, _settings.Current.Backups, DateTimeOffset.Now);
        foreach (var backup in doomed)
        {
            try
            {
                if (Directory.Exists(backup.DirectoryPath) &&
                    PathValidator.IsUnderRoot(backup.DirectoryPath, _paths.BackupsDirectory))
                {
                    Directory.Delete(backup.DirectoryPath, recursive: true);
                    _logger.LogInformation("Removed expired backup {Id}", backup.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete expired backup {Id}", backup.Id);
            }
        }
    }

    private static bool CopyIfExists(string source, string destination)
    {
        if (File.Exists(source))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
            return true;
        }

        if (Directory.Exists(source))
        {
            CopyDirectoryIfExists(source, destination);
            return true;
        }

        return false;
    }

    private static void CopyDirectoryIfExists(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var target = dir.Replace(source, destination);
            Directory.CreateDirectory(target);
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = file.Replace(source, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
    }
}
