using ConanServerControl.Core;
using ConanServerControl.Core.Backups;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Mods;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Workshop;

/// <summary>
/// Local mod source: an administrator-supplied .pak. No SteamCMD involved.
/// selected .pak → validate → COPY into isolated staging → SHA-256 → transactional commit
/// (rollback copy, verified replacement, rollback on failure) → modlist.txt.
/// The source file is only ever read: never moved, modified or deleted.
/// </summary>
public sealed partial class WorkshopModService
{
    private const string LocalStagingFolder = "local";

    public async Task<StagedLocalPak> StageLocalPakAsync(string sourcePakPath, CancellationToken cancellationToken = default)
    {
        var source = sourcePakPath?.Trim().Trim('"');
        if (!PathValidator.IsSafeAbsolutePath(source))
        {
            throw new UserFacingException(
                "Invalid local mod path",
                $"The selected path is not a safe absolute file path:{Environment.NewLine}{sourcePakPath}",
                "Choose the .pak file with Browse.");
        }

        if (Directory.Exists(source))
        {
            throw new UserFacingException("Local mod is a folder", source!, "Choose a single .pak file, not a folder.");
        }

        if (!File.Exists(source))
        {
            throw new UserFacingException("Local mod file not found", source!, "Choose an existing .pak file.");
        }

        var fileName = Path.GetFileName(source)!;
        if (!string.Equals(Path.GetExtension(fileName), ".pak", StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFacingException(
                "Not a .pak file",
                $"{fileName} is not a Conan mod package (.pak).",
                "Choose the mod's .pak file.");
        }

        if (!PathValidator.IsSafeRelativeName(fileName))
        {
            throw new UserFacingException("Unsafe mod file name", fileName, "Rename the file to a plain name, then import it again.");
        }

        var info = new FileInfo(source!);
        if (info.Length <= 0)
        {
            throw new UserFacingException("Local mod file is empty", $"{fileName} has 0 bytes.", "Choose a complete .pak file.");
        }

        var modsDir = ServerModsDirectoryOrNull();
        if (modsDir is not null && PathValidator.IsUnderRoot(source, modsDir))
        {
            throw new UserFacingException(
                "File is already in the server Mods folder",
                source!,
                "Import from a copy outside the server Mods folder. The manager never operates directly on a live mod file.");
        }

        if (PathValidator.IsUnderRoot(source, _paths.StagingDirectory))
        {
            throw new UserFacingException("Invalid local mod path", source!, "Choose a file outside the application staging folder.");
        }

        var stagingDirectory = Path.Combine(_paths.StagingDirectory, LocalStagingFolder, Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(stagingDirectory);
        var stagedPath = Path.Combine(stagingDirectory, fileName);
        try
        {
            var lengthBefore = info.Length;
            var writeBefore = info.LastWriteTimeUtc;

            await using (var input = new FileStream(source!, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan | FileOptions.Asynchronous))
            await using (var output = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            var stagedHash = BackupFileHasher.Sha256File(stagedPath);
            var sourceHash = BackupFileHasher.Sha256File(source!);
            info.Refresh();
            var stagedLength = new FileInfo(stagedPath).Length;
            if (!string.Equals(stagedHash, sourceHash, StringComparison.OrdinalIgnoreCase) ||
                stagedLength != lengthBefore ||
                info.Length != lengthBefore ||
                info.LastWriteTimeUtc != writeBefore)
            {
                throw new UserFacingException(
                    "Local mod changed while it was being copied",
                    $"{fileName} changed during staging, so the copy cannot be trusted.",
                    "Wait until the file is no longer being written, then import it again.");
            }

            _logger.LogInformation(
                "Staged local mod {File} ({Size} bytes, SHA-256 {Hash}) from {Source}.",
                fileName,
                stagedLength,
                stagedHash,
                source);
            return new StagedLocalPak(source!, fileName, stagedPath, stagingDirectory, stagedLength, stagedHash);
        }
        catch
        {
            TryDeleteStaging(stagingDirectory);
            throw;
        }
    }

    public async Task<WorkshopMod> CommitLocalPakAsync(
        StagedLocalPak staged,
        string? replaceModKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(staged);
        RevalidateStagedLocal(staged);
        var livePath = ResolveLivePakPath(staged.FileName);

        WorkshopMod? previous = null;
        if (replaceModKey is null)
        {
            var owner = _settings.Current.Mods.Mods.FirstOrDefault(m =>
                string.Equals(m.LocalFileName, staged.FileName, StringComparison.OrdinalIgnoreCase));
            if (owner is not null)
            {
                throw new UserFacingException(
                    "A mod already uses this file name",
                    $"{staged.FileName} already belongs to {ModKeys.Describe(owner)}.",
                    owner.SourceType == ModSourceType.Local
                        ? "Use Replace local .pak (manual update) to install a newer version of this mod."
                        : "Remove the other mod first. The existing file was not changed.");
            }

            if (File.Exists(livePath) &&
                !string.Equals(BackupFileHasher.Sha256File(livePath), staged.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFacingException(
                    "An unmanaged file with this name is already in the server Mods folder",
                    $"{livePath} exists, is not in the mod list, and differs from the selected file. It was not overwritten.",
                    "Move that file out of the server Mods folder yourself if it is not needed, then import again.");
            }
        }
        else
        {
            var existing = _settings.Current.Mods.Mods.FirstOrDefault(m => ModKeys.Matches(m, replaceModKey))
                           ?? throw NotFound(replaceModKey);
            if (existing.SourceType != ModSourceType.Local)
            {
                throw new UserFacingException(
                    "Only Local mods can be replaced from a local file",
                    $"{ModKeys.Describe(existing)} is a Workshop mod.",
                    "Workshop mods are updated with Update Selected (requires the optional SteamCMD integration).");
            }

            if (!string.Equals(existing.LocalFileName, staged.FileName, StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFacingException(
                    "Replacement has a different file name",
                    $"The installed mod is {existing.LocalFileName}; the selected file is {staged.FileName}.",
                    "Choose the newer file with the same name, or import it as a new Local mod and remove the old one.");
            }

            previous = Snapshot(existing);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var batch = ModBatchTransaction.Prepare(
            _paths.StagingDirectory,
            [new ModBatchTarget(0, livePath, staged.StagedPath, staged.FileName)],
            _logger);
        try
        {
            // Once live mutation begins, cancellation must not leave a half-applied change.
            Exception? commitFailure = null;
            var catalogChanged = false;
            WorkshopMod? result = null;
            try
            {
                var record = batch.Records[0];
                batch.ReplaceLive(record);
                AfterLiveReplacementForTests?.Invoke(new ModLiveReplacementContext(
                    record.WorkshopId,
                    record.LivePath,
                    record.RollbackPath,
                    batch.RollbackDirectory,
                    1));

                var liveHash = BackupFileHasher.Sha256File(livePath);
                if (!string.Equals(liveHash, staged.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"Installed {staged.FileName} does not match the staged copy (SHA-256 mismatch).");
                }

                catalogChanged = true;
                result = await PersistLocalAsync(staged, isNew: previous is null, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                commitFailure = ex;
            }

            if (commitFailure is not null)
            {
                var rollback = SafeRollback(batch, _logger);
                batch.KeepRollbackForRecovery = rollback.RecoveryRequired;
                if (catalogChanged)
                {
                    await TryRevertLocalCatalogAsync(staged.FileName, previous).ConfigureAwait(false);
                }

                throw ToUserFacing(ToBatchCommitException(commitFailure, rollback, "Local"), "Local");
            }

            return result!;
        }
        finally
        {
            batch.CleanupRollback();
        }
    }

    public void DiscardStagedLocalPak(StagedLocalPak staged)
    {
        ArgumentNullException.ThrowIfNull(staged);
        var localRoot = Path.Combine(_paths.StagingDirectory, LocalStagingFolder);
        if (PathValidator.IsUnderRoot(staged.StagingDirectory, localRoot) &&
            !PathValidator.PathsEqual(staged.StagingDirectory, localRoot))
        {
            TryDeleteStaging(staged.StagingDirectory);
        }
    }

    private static void RevalidateStagedLocal(StagedLocalPak staged)
    {
        if (!File.Exists(staged.StagedPath) || new FileInfo(staged.StagedPath).Length != staged.SizeBytes)
        {
            throw new UserFacingException(
                "Staged local mod is missing or changed",
                $"{staged.FileName} is no longer intact in staging.",
                "Import the file again. Nothing was installed.");
        }

        if (!string.Equals(BackupFileHasher.Sha256File(staged.StagedPath), staged.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFacingException(
                "Staged local mod failed verification",
                $"{staged.FileName} in staging no longer matches its SHA-256.",
                "Import the file again. Nothing was installed.");
        }
    }

    private async Task<WorkshopMod> PersistLocalAsync(StagedLocalPak staged, bool isNew, CancellationToken cancellationToken)
    {
        var installedAt = DateTimeOffset.UtcNow;
        WorkshopMod? saved = null;
        await _settings.UpdateAsync(s =>
        {
            var mod = s.Mods.Mods.FirstOrDefault(m =>
                m.SourceType == ModSourceType.Local &&
                string.Equals(m.LocalFileName, staged.FileName, StringComparison.OrdinalIgnoreCase));
            if (mod is null)
            {
                mod = new WorkshopMod
                {
                    SourceType = ModSourceType.Local,
                    WorkshopId = 0,
                    Name = Path.GetFileNameWithoutExtension(staged.FileName),
                    Enabled = true,
                    LoadOrder = s.Mods.Mods.Count + 1
                };
                s.Mods.Mods.Add(mod);
            }

            mod.LocalFileName = staged.FileName;
            mod.LocalSourcePath = staged.SourcePath;
            mod.Sha256 = staged.Sha256;
            mod.InstalledTimestamp = installedAt;
            mod.UpdateAvailable = false;
            mod.LatestWorkshopTimestamp = null;
            mod.Error = null;
            saved = mod;
        }, cancellationToken).ConfigureAwait(false);

        await WriteModListAsync(cancellationToken).ConfigureAwait(false);
        await _activityLog.AddAsync(
                "Mods",
                isNew
                    ? $"Imported local mod {staged.FileName} ({staged.SizeBytes} bytes, SHA-256 {staged.Sha256})."
                    : $"Manually updated local mod {staged.FileName} ({staged.SizeBytes} bytes, SHA-256 {staged.Sha256}).",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return saved!;
    }

    private async Task TryRevertLocalCatalogAsync(string fileName, WorkshopMod? previous)
    {
        try
        {
            await _settings.UpdateAsync(s =>
            {
                var index = s.Mods.Mods.FindIndex(m =>
                    m.SourceType == ModSourceType.Local &&
                    string.Equals(m.LocalFileName, fileName, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    return;
                }

                if (previous is null)
                {
                    s.Mods.Mods.RemoveAt(index);
                    ModListGenerator.ApplySequentialOrder(s.Mods.Mods.OrderBy(m => m.LoadOrder).ToList());
                }
                else
                {
                    s.Mods.Mods[index] = previous;
                }
            }, CancellationToken.None).ConfigureAwait(false);
            await WriteModListAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not revert the mod catalog after a failed local mod commit for {File}.", fileName);
        }
    }

    private static WorkshopMod Snapshot(WorkshopMod mod) => new()
    {
        SourceType = mod.SourceType,
        WorkshopId = mod.WorkshopId,
        LocalSourcePath = mod.LocalSourcePath,
        Sha256 = mod.Sha256,
        Name = mod.Name,
        LocalFileName = mod.LocalFileName,
        Enabled = mod.Enabled,
        LoadOrder = mod.LoadOrder,
        InstalledVersion = mod.InstalledVersion,
        InstalledTimestamp = mod.InstalledTimestamp,
        LatestWorkshopVersion = mod.LatestWorkshopVersion,
        LatestWorkshopTimestamp = mod.LatestWorkshopTimestamp,
        UpdateAvailable = mod.UpdateAvailable,
        LastChecked = mod.LastChecked,
        Error = mod.Error,
        IsPlaceholder = mod.IsPlaceholder
    };

    private string? ServerModsDirectoryOrNull()
    {
        var install = _settings.Current.ServerPaths.ServerInstallDirectory
                      ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        return string.IsNullOrWhiteSpace(install) ? null : Path.Combine(install, "ConanSandbox", "Mods");
    }
}
