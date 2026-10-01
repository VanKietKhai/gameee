using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Workshop;

/// <summary>
/// Isolated rollback copies plus live replacement tracking for one
/// multi-mod commit. A new operation directory is created every time;
/// stale rollback folders are never reused.
/// </summary>
internal sealed class ModBatchTransaction
{
    private ModBatchTransaction(string operationId, string operationRoot, string rollbackDirectory, List<ModBatchRecord> records)
    {
        OperationId = operationId;
        OperationRoot = operationRoot;
        RollbackDirectory = rollbackDirectory;
        Records = records;
    }

    public string OperationId { get; }

    public string OperationRoot { get; }

    public string RollbackDirectory { get; }

    public IReadOnlyList<ModBatchRecord> Records { get; }

    public bool KeepRollbackForRecovery { get; set; }

    public static ModBatchTransaction Prepare(
        string stagingDirectory,
        IReadOnlyList<ModBatchTarget> targets,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        ArgumentNullException.ThrowIfNull(targets);

        var operationId = Guid.NewGuid().ToString("n");
        var operationRoot = Path.Combine(stagingDirectory, "mod-update", operationId);
        if (Directory.Exists(operationRoot))
        {
            Directory.Delete(operationRoot, recursive: true);
        }

        var rollbackDirectory = Path.Combine(operationRoot, "rollback");
        Directory.CreateDirectory(rollbackDirectory);

        var records = new List<ModBatchRecord>(targets.Count);
        try
        {
            foreach (var target in targets)
            {
                string? rollbackPath = null;
                var hadOriginal = File.Exists(target.LivePath);
                if (hadOriginal)
                {
                    rollbackPath = Path.Combine(
                        rollbackDirectory,
                        target.WorkshopId.ToString(),
                        target.FileName);
                    var rollbackParent = Path.GetDirectoryName(rollbackPath);
                    if (!string.IsNullOrWhiteSpace(rollbackParent))
                    {
                        Directory.CreateDirectory(rollbackParent);
                    }

                    File.Copy(target.LivePath, rollbackPath, overwrite: true);
                    if (!File.Exists(rollbackPath) || new FileInfo(rollbackPath).Length <= 0)
                    {
                        throw new IOException($"Rollback copy for Workshop mod {target.WorkshopId} was not created at {rollbackPath}.");
                    }
                }

                records.Add(new ModBatchRecord
                {
                    WorkshopId = target.WorkshopId,
                    LivePath = target.LivePath,
                    StagedPath = target.StagedPath,
                    FileName = target.FileName,
                    RollbackPath = rollbackPath,
                    HadOriginalLiveFile = hadOriginal
                });
            }
        }
        catch
        {
            TryDeleteDirectory(operationRoot, logger);
            throw;
        }

        return new ModBatchTransaction(operationId, operationRoot, rollbackDirectory, records);
    }

    public void ReplaceLive(ModBatchRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ReplaceLiveFile(record.StagedPath, record.LivePath);
        record.Replaced = true;
    }

    public bool TryRollback(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var completed = true;

        foreach (var record in Records)
        {
            if (!record.Replaced)
            {
                continue;
            }

            try
            {
                if (record.HadOriginalLiveFile)
                {
                    if (string.IsNullOrWhiteSpace(record.RollbackPath) || !File.Exists(record.RollbackPath))
                    {
                        logger.LogError(
                            "Rollback copy for Workshop mod {WorkshopId} is missing. Live path {LivePath} cannot be restored.",
                            record.WorkshopId,
                            record.LivePath);
                        completed = false;
                        continue;
                    }

                    ReplaceLiveFile(record.RollbackPath, record.LivePath);
                }
                else if (File.Exists(record.LivePath))
                {
                    File.Delete(record.LivePath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.LogError(
                    ex,
                    "Failed to restore Workshop mod {WorkshopId} from rollback copy {RollbackPath} to {LivePath}.",
                    record.WorkshopId,
                    record.RollbackPath,
                    record.LivePath);
                completed = false;
            }
        }

        if (!VerifyOriginals(logger))
        {
            completed = false;
        }

        if (completed)
        {
            logger.LogWarning(
                "Rolled back {Count} live Workshop mod replacement(s) to the previous versions.",
                Records.Count(r => r.Replaced));
        }

        return completed;
    }

    public void CleanupRollback()
    {
        if (KeepRollbackForRecovery)
        {
            return;
        }

        TryDeleteDirectory(OperationRoot, logger: null);
    }

    private bool VerifyOriginals(ILogger logger)
    {
        var ok = true;
        foreach (var record in Records)
        {
            if (!record.HadOriginalLiveFile)
            {
                if (record.Replaced && File.Exists(record.LivePath))
                {
                    logger.LogError(
                        "Workshop mod {WorkshopId} was newly installed during this batch and was not removed during rollback. Live path {LivePath}.",
                        record.WorkshopId,
                        record.LivePath);
                    ok = false;
                }

                continue;
            }

            if (!File.Exists(record.LivePath))
            {
                logger.LogError(
                    "Expected original live file for Workshop mod {WorkshopId} is missing at {LivePath} after rollback.",
                    record.WorkshopId,
                    record.LivePath);
                ok = false;
                continue;
            }

            if (record.Replaced)
            {
                if (string.IsNullOrWhiteSpace(record.RollbackPath) || !File.Exists(record.RollbackPath))
                {
                    logger.LogError(
                        "Cannot verify restored Workshop mod {WorkshopId}; rollback copy is missing.",
                        record.WorkshopId);
                    ok = false;
                    continue;
                }

                if (!FilesEqual(record.LivePath, record.RollbackPath))
                {
                    logger.LogError(
                        "Restored live file for Workshop mod {WorkshopId} does not match the rollback copy.",
                        record.WorkshopId);
                    ok = false;
                }
            }
        }

        return ok;
    }

    internal static void ReplaceLiveFile(string sourceFile, string destinationFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
        {
            throw new FileNotFoundException($"Source file is missing: {sourceFile}", sourceFile);
        }

        var destDir = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrWhiteSpace(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        var tempDest = destinationFile + ".new";
        File.Copy(sourceFile, tempDest, overwrite: true);
        try
        {
            if (File.Exists(destinationFile))
            {
                File.Replace(tempDest, destinationFile, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempDest, destinationFile, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteFile(tempDest);
            throw;
        }
        catch
        {
            TryDeleteFile(tempDest);
            throw;
        }
    }

    private static bool FilesEqual(string left, string right)
    {
        var leftInfo = new FileInfo(left);
        var rightInfo = new FileInfo(right);
        if (!leftInfo.Exists || !rightInfo.Exists || leftInfo.Length != rightInfo.Length)
        {
            return false;
        }

        using var leftStream = File.OpenRead(left);
        using var rightStream = File.OpenRead(right);
        var leftBuffer = new byte[8192];
        var rightBuffer = new byte[8192];
        while (true)
        {
            var leftRead = leftStream.Read(leftBuffer, 0, leftBuffer.Length);
            var rightRead = rightStream.Read(rightBuffer, 0, rightBuffer.Length);
            if (leftRead != rightRead)
            {
                return false;
            }

            if (leftRead == 0)
            {
                return true;
            }

            if (!leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead)))
            {
                return false;
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Best-effort cleanup of a temp sibling; the original failure is rethrown by the caller.
        }
    }

    private static void TryDeleteDirectory(string path, ILogger? logger)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Could not delete mod-update directory {Path}.", path);
        }
    }
}

internal sealed class ModBatchTarget
{
    public ModBatchTarget(long workshopId, string livePath, string stagedPath, string fileName)
    {
        WorkshopId = workshopId;
        LivePath = livePath;
        StagedPath = stagedPath;
        FileName = fileName;
    }

    public long WorkshopId { get; }

    public string LivePath { get; }

    public string StagedPath { get; }

    public string FileName { get; }
}

internal sealed class ModBatchRecord
{
    public required long WorkshopId { get; init; }

    public required string LivePath { get; init; }

    public required string StagedPath { get; init; }

    public required string FileName { get; init; }

    public string? RollbackPath { get; init; }

    public bool HadOriginalLiveFile { get; init; }

    public bool Replaced { get; set; }
}

internal readonly record struct ModLiveReplacementContext(
    long WorkshopId,
    string LivePath,
    string? RollbackPath,
    string RollbackDirectory,
    int ReplacedCount);
