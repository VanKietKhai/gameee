using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Validation;
using ConanServerControl.Infrastructure.Backups;

namespace ConanServerControl.Tests;

public class M3BackupPathTests
{
    [Fact]
    public void Valid_child_path_is_allowed()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "csc-m3-backup", Guid.NewGuid().ToString("n")));
        Directory.CreateDirectory(root);
        var child = Path.GetFullPath(Path.Combine(root, "2026-10-01_120000"));
        BackupService.EnsureDestinationIsUnderBackupRoot(child, root);
        Assert.True(PathValidator.IsUnderRoot(child, root));
    }

    [Fact]
    public void Traversal_outside_backup_root_is_rejected()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "csc-m3-backup", Guid.NewGuid().ToString("n")));
        Directory.CreateDirectory(root);
        var escaped = Path.GetFullPath(Path.Combine(root, "..", "outside"));
        var error = Assert.Throws<UserFacingException>(
            () => BackupService.EnsureDestinationIsUnderBackupRoot(escaped, root));
        Assert.Contains("backup", error.Title, StringComparison.OrdinalIgnoreCase);
        Assert.False(PathValidator.IsUnderRoot(escaped, root));
    }

    [Fact]
    public void Absolute_path_outside_root_is_rejected()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "csc-m3-backup", Guid.NewGuid().ToString("n")));
        Directory.CreateDirectory(root);
        var outside = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "csc-m3-elsewhere", Guid.NewGuid().ToString("n")));
        Assert.Throws<UserFacingException>(() => BackupService.EnsureDestinationIsUnderBackupRoot(outside, root));
        Assert.False(PathValidator.IsUnderRoot(outside, root));
    }

    [Fact]
    public void Prefix_confusion_path_is_rejected()
    {
        var temp = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "csc-m3-prefix", Guid.NewGuid().ToString("n")));
        var root = Path.Combine(temp, "Backup");
        var evil = Path.Combine(temp, "Backup-Evil", "stamp");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.GetDirectoryName(evil)!);

        Assert.False(PathValidator.IsUnderRoot(evil, root));
        Assert.Throws<UserFacingException>(() => BackupService.EnsureDestinationIsUnderBackupRoot(evil, root));
    }

    [Fact]
    public void Under_root_uses_windows_case_insensitive_prefix_not_naive_starts_with()
    {
        var temp = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "csc-m3-case", Guid.NewGuid().ToString("n")));
        var root = Path.Combine(temp, "Backup");
        var child = Path.Combine(root, "child");
        Directory.CreateDirectory(child);

        Assert.True(PathValidator.IsUnderRoot(child, root));
        Assert.False(PathValidator.IsUnderRoot(root + "-Evil" + Path.DirectorySeparatorChar + "x", root));
    }
}
