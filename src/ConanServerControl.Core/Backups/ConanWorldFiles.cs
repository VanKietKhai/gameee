namespace ConanServerControl.Core.Backups;

/// <summary>
/// Known Conan dedicated-server world files. Enhanced uses <c>game_0.db</c>;
/// legacy installs use <c>game.db</c>. WAL/SHM siblings are included when present.
/// This does not treat arbitrary <c>.db</c> files as the world.
/// </summary>
public static class ConanWorldFiles
{
    public static readonly string[] Enhanced =
    [
        "game_0.db",
        "game_0.db-wal",
        "game_0.db-shm"
    ];

    public static readonly string[] Legacy =
    [
        "game.db",
        "game.db-wal",
        "game.db-shm"
    ];

    /// <summary>
    /// Returns the known world-file names that exist directly under <paramref name="savedDir"/>.
    /// Does not recurse and does not open or modify the files.
    /// </summary>
    public static IReadOnlyList<string> Present(string? savedDir)
    {
        if (string.IsNullOrWhiteSpace(savedDir) || !Directory.Exists(savedDir))
        {
            return Array.Empty<string>();
        }

        var found = new List<string>(Enhanced.Length + Legacy.Length);
        foreach (var name in Enhanced)
        {
            if (File.Exists(Path.Combine(savedDir, name)))
            {
                found.Add(name);
            }
        }

        foreach (var name in Legacy)
        {
            if (File.Exists(Path.Combine(savedDir, name)))
            {
                found.Add(name);
            }
        }

        return found;
    }
}
