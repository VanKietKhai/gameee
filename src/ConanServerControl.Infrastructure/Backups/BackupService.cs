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
    private readonly IServerActionGate _actionGate;
    private readonly IActivityLog _activityLog;
    private readonly IBackupVerifier _verifier;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IAppPaths paths,
        ISettingsService settings,
        IServerProcessManager server,
        IServerActionGate actionGate,
        IActivityLog activityLog,
        IBackupVerifier verifier,
        ILogger<BackupService> logger)
    {
        _paths = paths;
        _settings = settings;
        _server = server;
        _actionGate = actionGate;
        _activityLog = activityLog;
        _verifier = verifier;
        _logger = logger;
    }

    public async Task<BackupRecord> BackupNowAsync(string reason, CancellationToken cancellationToken = default)
    {
        var wasRunning = _server.State.Status is not ServerStatus.Offline and not ServerStatus.Error;
        if (!wasRunning)
        {
            return await BackupNowCoreAsync(reason, extraProtectedBackupIdsOrPaths: null, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!_actionGate.TryBegin("Backup now", out var lease) || lease is null)
        {
            throw new UserFacingException(
                "Server action already running",
                $"Cannot create a backup because another action is in progress: {_actionGate.CurrentAction}.",
                "Wait for the current action to finish, then try again.");
        }

        using (lease)
        {
            await _server.StopUnderLockAsync(lease, force: false, cancellationToken).ConfigureAwait(false);
            EnsureServerStoppedForColdBackup("backup");

            BackupRecord record;
            try
            {
                record = await BackupNowCoreAsync(reason, extraProtectedBackupIdsOrPaths: null, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                await TryRestartAfterFailedColdBackupAsync(lease, cancellationToken).ConfigureAwait(false);
                throw;
            }

            try
            {
                await _server.StartUnderLockAsync(lease, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception startEx)
            {
                throw new UserFacingException(
                    "Backup created, but the server did not return online",
                    startEx is UserFacingException facing ? facing.Message : startEx.Message,
                    $"A verified cold backup was created ({record.Id}) after the server was stopped, but the dedicated server did not become ready again.",
                    startEx);
            }

            return record;
        }
    }

    private async Task TryRestartAfterFailedColdBackupAsync(IServerOperationLease lease, CancellationToken cancellationToken)
    {
        if (_server.State.Status is not ServerStatus.Offline and not ServerStatus.Error)
        {
            return;
        }

        try
        {
            await _server.StartUnderLockAsync(lease, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception startEx)
        {
            _logger.LogError(startEx, "Could not restart the previously running server after a cold backup failure.");
        }
    }

    private void EnsureServerStoppedForColdBackup(string operation)
    {
        if (_server.State.Status is ServerStatus.Offline or ServerStatus.Error)
        {
            return;
        }

        throw new UserFacingException(
            "Cannot create a cold world backup while the server is running",
            $"The dedicated server is still {_server.State.Status}. A live SQLite copy is not a valid safety backup.",
            $"Stop the server, then retry the {operation}. The live world was not copied.");
    }

    private async Task<BackupRecord> BackupNowCoreAsync(
        string reason,
        IReadOnlyCollection<string>? extraProtectedBackupIdsOrPaths,
        CancellationToken cancellationToken)
    {
        EnsureServerStoppedForColdBackup("backup");

        var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var dest = Path.Combine(_paths.BackupsDirectory, stamp);
        EnsureDestinationIsUnderBackupRoot(dest, _paths.BackupsDirectory);

        var install = _settings.Current.ServerPaths.ServerInstallDirectory
                      ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        if (string.IsNullOrWhiteSpace(install) || !Directory.Exists(install))
        {
            throw new UserFacingException(
                "Backup failed",
                "The dedicated server install directory is not configured or does not exist.",
                "Set the dedicated server folder in Settings before creating a backup.");
        }

        var saved = Path.Combine(install, "ConanSandbox", "Saved");
        if (!Directory.Exists(saved))
        {
            throw new UserFacingException(
                "Backup failed",
                "The world/save folder was not found. No backup was completed.",
                $"Expected:{Environment.NewLine}{saved}{Environment.NewLine}{Environment.NewLine}The live world was not copied.");
        }

        var worldDir = Path.Combine(dest, "world");
        var configDir = Path.Combine(dest, "config");
        var modlistDir = Path.Combine(dest, "modlist");
        try
        {
            Directory.CreateDirectory(dest);
            Directory.CreateDirectory(worldDir);
            Directory.CreateDirectory(configDir);
            Directory.CreateDirectory(modlistDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteBackupFolder(dest);
            throw new UserFacingException(
                "Backup failed",
                ex.Message,
                "The backup destination could not be written. No backup was completed.",
                ex);
        }

        var includesWorld = false;
        var includesConfig = false;
        var includesModList = false;

        try
        {
            includesWorld = CopyIfExists(saved, worldDir);
            includesConfig = CopyIfExists(Path.Combine(install, "ConanSandbox", "Saved", "Config"), configDir);
            includesModList = CopyIfExists(
                Path.Combine(install, "ConanSandbox", "Mods", "modlist.txt"),
                Path.Combine(modlistDir, "modlist.txt"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteBackupFolder(dest);
            throw new UserFacingException(
                "Backup failed",
                ex.Message,
                "World/save data was not copied. The incomplete backup folder was discarded.",
                ex);
        }

        if (!includesWorld)
        {
            TryDeleteBackupFolder(dest);
            throw new UserFacingException(
                "Backup failed",
                "World/save data was not copied. The backup is incomplete and was not recorded as successful.",
                $"Expected world files under:{Environment.NewLine}{saved}");
        }

        var copiedWorldFiles = ConanWorldFiles.Present(worldDir);
        var mainDb = ConanWorldFiles.MainDatabaseFileName(copiedWorldFiles);
        var worldType = ConanWorldFiles.DetectWorldType(copiedWorldFiles);
        var worldFileRecords = new List<BackupWorldFileRecord>();
        var record = new BackupRecord
        {
            Id = stamp,
            DirectoryPath = dest,
            CreatedAt = DateTimeOffset.Now,
            Reason = reason,
            IncludesWorld = includesWorld,
            IncludesConfig = includesConfig,
            IncludesModList = includesModList,
            WorldType = worldType,
            MainDbFileName = mainDb,
            WorldFiles = worldFileRecords
        };

        if (string.IsNullOrWhiteSpace(mainDb))
        {
            record.Succeeded = false;
            record.VerificationDetail = "Expected main world database (game_0.db or game.db) is missing from the backup copy.";
            record.VerifiedAt = DateTimeOffset.UtcNow;
            await TryWriteMetadataAsync(dest, record, cancellationToken).ConfigureAwait(false);
            throw new UserFacingException(
                "Backup failed",
                record.VerificationDetail,
                "A verified world backup requires the main SQLite database. Unrelated .db files are ignored. The live world was not treated as backed up.");
        }

        try
        {
            foreach (var name in copiedWorldFiles)
            {
                var copyPath = Path.Combine(worldDir, name);
                var hash = BackupFileHasher.Sha256File(copyPath);
                worldFileRecords.Add(new BackupWorldFileRecord
                {
                    LogicalName = name,
                    FileName = name,
                    SizeBytes = new FileInfo(copyPath).Length,
                    Sha256 = hash
                });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            record.Succeeded = false;
            record.VerificationDetail = $"Could not hash copied world files: {ex.Message}";
            record.VerifiedAt = DateTimeOffset.UtcNow;
            await TryWriteMetadataAsync(dest, record, cancellationToken).ConfigureAwait(false);
            throw new UserFacingException(
                "Backup failed",
                record.VerificationDetail,
                "The backup copy could not be hashed. The backup is not verified and must not be used as a safety backup.",
                ex);
        }

        BackupVerificationResult verification;
        try
        {
            verification = await _verifier.VerifyAsync(worldDir, record, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            record.Succeeded = false;
            record.HashesVerified = false;
            record.SqliteVerified = false;
            record.VerificationDetail = ex.Message;
            record.VerifiedAt = DateTimeOffset.UtcNow;
            await TryWriteMetadataAsync(dest, record, cancellationToken).ConfigureAwait(false);
            throw new UserFacingException(
                "Backup failed",
                ex.Message,
                "Backup verification threw. The backup is not verified and must not be used as a safety backup.",
                ex);
        }

        record.HashesVerified = verification.HashesVerified;
        record.SqliteVerified = verification.SqliteVerified;
        record.VerificationDetail = verification.Detail;
        record.VerifiedAt = verification.VerifiedAt;
        record.Succeeded = verification.Succeeded;

        if (!verification.Succeeded)
        {
            await TryWriteMetadataAsync(dest, record, cancellationToken).ConfigureAwait(false);
            throw new UserFacingException(
                "Backup failed",
                verification.Detail,
                "The copied world failed hash or SQLite verification. The backup is not verified. No update or other mutation should proceed.");
        }

        try
        {
            record.SizeBytes = GetDirectorySize(dest);
            record.ManifestWritten = true;
            var metadataPath = Path.Combine(dest, "metadata.json");
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(record, JsonOptions), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            record.Succeeded = false;
            record.ManifestWritten = false;
            TryDeleteBackupFolder(dest);
            throw new UserFacingException(
                "Backup failed",
                ex.Message,
                "The backup metadata could not be written. The incomplete backup folder was discarded.",
                ex);
        }

        if (!record.ManifestWritten || !record.HashesVerified || !record.SqliteVerified || !record.Succeeded)
        {
            throw new UserFacingException(
                "Backup failed",
                record.VerificationDetail ?? "Backup verification did not complete.",
                "A completed backup requires copied world files, a written manifest, hashes, and SQLite verification.");
        }

        await _settings.UpdateAsync(s => { }, cancellationToken).ConfigureAwait(false);
        await _activityLog.AddAsync("Backup", $"Backup completed ({stamp}).", details: reason, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("Backup {Id} created at {Path}", stamp, dest);

        var protectedSet = new List<string> { stamp, dest };
        if (extraProtectedBackupIdsOrPaths is not null)
        {
            protectedSet.AddRange(extraProtectedBackupIdsOrPaths);
        }

        await ApplyRetentionAsync(protectedSet, cancellationToken).ConfigureAwait(false);
        return record;
    }

    public async Task RestoreAsync(string backupId, bool startAfter, CancellationToken cancellationToken = default)
    {
        if (!_actionGate.TryBegin("Restore backup", out var lease) || lease is null)
        {
            throw new UserFacingException(
                "Server action already running",
                $"Cannot restore because another action is in progress: {_actionGate.CurrentAction}.",
                "Wait for the current action to finish, then try again.");
        }

        try
        {
            await RestoreCoreAsync(lease, backupId, startAfter, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lease.Dispose();
        }
    }

    private async Task RestoreCoreAsync(
        IServerOperationLease lease,
        string backupId,
        bool startAfter,
        CancellationToken cancellationToken)
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

        var sourceFullPath = Path.GetFullPath(source);
        await BackupNowCoreAsync(
                "pre-restore",
                new[] { backupId, source, sourceFullPath },
                cancellationToken)
            .ConfigureAwait(false);

        if (!Directory.Exists(source))
        {
            throw new UserFacingException(
                "Restore source was deleted during the operation",
                source,
                "The selected backup is no longer on disk. Restore did not change the live world.");
        }

        var install = _settings.Current.ServerPaths.ServerInstallDirectory
                      ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        if (string.IsNullOrWhiteSpace(install) || !Directory.Exists(install))
        {
            throw new UserFacingException(
                "Server install directory is not configured",
                install ?? "(not set)",
                "Set the dedicated server folder in Settings before restoring.");
        }

        try
        {
            var worldSource = Path.Combine(source, "world");
            var worldDest = Path.Combine(install, "ConanSandbox", "Saved");
            if (!Directory.Exists(worldSource))
            {
                throw new UserFacingException(
                    "Restore failed",
                    "The selected backup does not contain world/save data.",
                    "Choose a backup that includes the world folder, or copy the files manually.");
            }

            CopyDirectory(worldSource, worldDest);

            var configSource = Path.Combine(source, "config");
            if (Directory.Exists(configSource))
            {
                CopyDirectory(configSource, Path.Combine(install, "ConanSandbox", "Saved", "Config"));
            }

            var modlistSource = Path.Combine(source, "modlist", "modlist.txt");
            if (File.Exists(modlistSource))
            {
                var dest = Path.Combine(install, "ConanSandbox", "Mods", "modlist.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(modlistSource, dest, overwrite: true);
            }
        }
        catch (UserFacingException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            throw new UserFacingException(
                "Restore failed",
                ex.Message,
                "The live world was not fully restored. Check disk space and file permissions, then retry.",
                ex);
        }

        if (!Directory.Exists(source))
        {
            throw new UserFacingException(
                "Restore source was deleted during the operation",
                source,
                "The selected backup is no longer on disk. Do not treat this restore as successful.");
        }

        await _activityLog.AddAsync("Backup", $"Restored backup {backupId}.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (startAfter)
        {
            await _server.StartUnderLockAsync(lease, cancellationToken).ConfigureAwait(false);
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

    public Task ApplyRetentionAsync(CancellationToken cancellationToken = default) =>
        ApplyRetentionAsync(protectedBackupIdsOrPaths: null, cancellationToken);

    public async Task ApplyRetentionAsync(
        IReadOnlyCollection<string>? protectedBackupIdsOrPaths,
        CancellationToken cancellationToken = default)
    {
        var backups = await ListAsync(cancellationToken).ConfigureAwait(false);
        var doomed = BackupRetentionPolicy.SelectForDeletion(
            backups,
            _settings.Current.Backups,
            DateTimeOffset.Now,
            protectedBackupIdsOrPaths);
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

    private async Task TryWriteMetadataAsync(string dest, BackupRecord record, CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(dest) || !PathValidator.IsUnderRoot(dest, _paths.BackupsDirectory))
            {
                return;
            }

            var metadataPath = Path.Combine(dest, "metadata.json");
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(record, JsonOptions), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write backup metadata for {Id}", record.Id);
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
            CopyDirectory(source, destination);
            return true;
        }

        return false;
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException(source);
        }

        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, dir));
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.Copy(file, target, overwrite: true);
        }
    }

    private void TryDeleteBackupFolder(string dest)
    {
        try
        {
            if (Directory.Exists(dest) && PathValidator.IsUnderRoot(dest, _paths.BackupsDirectory))
            {
                Directory.Delete(dest, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discard incomplete backup folder {Path}", dest);
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

    internal static void EnsureDestinationIsUnderBackupRoot(string destination, string backupRoot)
    {
        if (!PathValidator.IsUnderRoot(destination, backupRoot))
        {
            throw new UserFacingException(
                "Invalid backup path",
                destination,
                "Backup folders are created under the application backups directory only.");
        }
    }
}
