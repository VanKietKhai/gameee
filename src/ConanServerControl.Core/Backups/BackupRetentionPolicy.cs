using ConanServerControl.Core.Models;
using ConanServerControl.Core.Settings;

namespace ConanServerControl.Core.Backups;

public static class BackupRetentionPolicy
{
    public static IReadOnlyList<BackupRecord> SelectForDeletion(
        IEnumerable<BackupRecord> backups,
        BackupSettings settings,
        DateTimeOffset utcNow)
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

        return ordered.Where(b => !keep.Contains(b.Id)).ToArray();
    }
}
