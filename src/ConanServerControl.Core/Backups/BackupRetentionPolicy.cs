using ConanServerControl.Core.Models;
using ConanServerControl.Core.Settings;

namespace ConanServerControl.Core.Backups;

public static class BackupRetentionPolicy
{
    public static IReadOnlyList<BackupRecord> SelectForDeletion(
        IEnumerable<BackupRecord> backups,
        BackupSettings settings,
        DateTimeOffset utcNow,
        IEnumerable<string>? protectedBackupIdsOrPaths = null)
    {
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(settings);

        var ordered = backups
            .OrderByDescending(b => b.CreatedAt)
            .ToList();

        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keepLatest = (int)settings.KeepLatest;
        foreach (var backup in ordered.Take(keepLatest))
        {
            keep.Add(backup.Id);
        }

        if (settings.KeepDays is > 0)
        {
            var cutoff = utcNow.AddDays(-settings.KeepDays.Value);
            foreach (var backup in ordered.Where(b => b.CreatedAt >= cutoff))
            {
                keep.Add(backup.Id);
            }
        }

        foreach (var protectedId in NormalizeProtected(protectedBackupIdsOrPaths))
        {
            keep.Add(protectedId);
        }

        return ordered.Where(b => !IsProtected(b, keep)).ToArray();
    }

    private static HashSet<string> NormalizeProtected(IEnumerable<string>? protectedBackupIdsOrPaths)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (protectedBackupIdsOrPaths is null)
        {
            return set;
        }

        foreach (var value in protectedBackupIdsOrPaths)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            set.Add(value.Trim());
            try
            {
                set.Add(Path.GetFullPath(value.Trim()));
            }
            catch (Exception)
            {
                // Not a filesystem path; the raw id is enough.
            }
        }

        return set;
    }

    private static bool IsProtected(BackupRecord backup, HashSet<string> keep)
    {
        if (keep.Contains(backup.Id))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(backup.DirectoryPath))
        {
            return false;
        }

        if (keep.Contains(backup.DirectoryPath))
        {
            return true;
        }

        try
        {
            return keep.Contains(Path.GetFullPath(backup.DirectoryPath));
        }
        catch (Exception)
        {
            return false;
        }
    }
}
