namespace ConanServerControl.Core.Validation;

public static class PathValidator
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

    public static bool IsSafeRelativeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.IndexOfAny(InvalidFileNameChars) >= 0)
        {
            return false;
        }

        if (name.Contains("..", StringComparison.Ordinal) ||
            name.Contains(Path.DirectorySeparatorChar) ||
            name.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        return true;
    }

    public static bool IsSafeAbsolutePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();
        if (trimmed.IndexOfAny(InvalidPathChars) >= 0)
        {
            return false;
        }

        if (!Path.IsPathRooted(trimmed))
        {
            return false;
        }

        try
        {
            var full = Path.GetFullPath(trimmed);
            if (full.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool IsUnderRoot(string? path, string? root)
    {
        if (!IsSafeAbsolutePath(path) || !IsSafeAbsolutePath(root))
        {
            return false;
        }

        // QA-018: compare normalized full paths so "X" and "X\" are the same directory.
        var fullPath = NormalizeFullPath(path!);
        var fullRoot = NormalizeFullPath(root!);
        if (string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = Path.EndsInDirectorySeparator(fullRoot)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Full path with separators normalized and any trailing separator removed
    /// (a drive root such as <c>C:\</c> keeps its separator). Windows 8.3 short
    /// names are not expanded.
    /// </summary>
    public static string NormalizeFullPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path.Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        return Path.TrimEndingDirectorySeparator(full);
    }

    /// <summary>True when both are safe absolute paths naming the same location after normalization.</summary>
    public static bool PathsEqual(string? a, string? b) =>
        IsSafeAbsolutePath(a) &&
        IsSafeAbsolutePath(b) &&
        string.Equals(NormalizeFullPath(a!), NormalizeFullPath(b!), StringComparison.OrdinalIgnoreCase);

    /// <summary>True when either path is the same as, or contains, the other.</summary>
    public static bool Overlaps(string? a, string? b) =>
        IsUnderRoot(a, b) || IsUnderRoot(b, a);

    public static string CombineUnderRoot(string root, string relative)
    {
        if (!IsSafeAbsolutePath(root))
        {
            throw new ArgumentException("Root path is not a safe absolute path.", nameof(root));
        }

        if (string.IsNullOrWhiteSpace(relative) ||
            relative.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            throw new ArgumentException("Relative path is not allowed.", nameof(relative));
        }

        var combined = Path.GetFullPath(Path.Combine(root, relative));
        if (!IsUnderRoot(combined, root))
        {
            throw new InvalidOperationException("Path escaped the allowed root directory.");
        }

        return combined;
    }
}
