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

        var fullPath = Path.GetFullPath(path!);
        var fullRoot = Path.GetFullPath(root!);
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            fullRoot += Path.DirectorySeparatorChar;
        }

        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
               || string.Equals(Path.GetFullPath(fullPath), Path.GetFullPath(root!), StringComparison.OrdinalIgnoreCase);
    }

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
