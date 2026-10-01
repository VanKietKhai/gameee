using ConanServerControl.Core.Validation;

namespace ConanServerControl.Tests;

public class WorkshopIdValidatorTests
{
    [Theory]
    [InlineData("123456789", true)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("-12", false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("abc", false)]
    [InlineData("12abc", false)]
    [InlineData("123456789012345678901", false)]
    public void TryParse_validates_workshop_ids(string input, bool expected)
    {
        var ok = WorkshopIdValidator.TryParse(input, out var id);
        Assert.Equal(expected, ok);
        if (expected)
        {
            Assert.True(id > 0);
        }
    }
}

public class PathValidatorTests
{
    [Fact]
    public void Rejects_relative_and_traversal()
    {
        Assert.False(PathValidator.IsSafeAbsolutePath("mods\\pippi.pak"));
        Assert.False(PathValidator.IsSafeRelativeName(".."));
        Assert.False(PathValidator.IsSafeRelativeName("folder/file"));
        Assert.True(PathValidator.IsSafeRelativeName("Pippi.pak"));
    }

    [Fact]
    public void CombineUnderRoot_blocks_escape()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "csc-root"));
        Directory.CreateDirectory(root);
        Assert.Throws<ArgumentException>(() => PathValidator.CombineUnderRoot(root, "..\\escape"));
        var safe = PathValidator.CombineUnderRoot(root, "workshop");
        Assert.True(PathValidator.IsUnderRoot(safe, root));
    }

    [Fact]
    public void Absolute_windows_style_or_unix_temp_is_accepted_when_rooted()
    {
        var path = Path.GetFullPath(Path.GetTempPath());
        Assert.True(PathValidator.IsSafeAbsolutePath(path));
    }
}
