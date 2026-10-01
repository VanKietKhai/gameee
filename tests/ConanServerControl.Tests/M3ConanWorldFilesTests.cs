using ConanServerControl.Core.Backups;

namespace ConanServerControl.Tests;

public class M3ConanWorldFilesTests
{
    [Fact]
    public void Enhanced_db_only()
    {
        var saved = CreateSaved();
        File.WriteAllText(Path.Combine(saved, "game_0.db"), "enhanced");
        File.WriteAllText(Path.Combine(saved, "unrelated.db"), "nope");

        Assert.Equal(["game_0.db"], ConanWorldFiles.Present(saved));
    }

    [Fact]
    public void Enhanced_db_with_wal_and_shm()
    {
        var saved = CreateSaved();
        File.WriteAllText(Path.Combine(saved, "game_0.db"), "db");
        File.WriteAllText(Path.Combine(saved, "game_0.db-wal"), "wal");
        File.WriteAllText(Path.Combine(saved, "game_0.db-shm"), "shm");

        Assert.Equal(["game_0.db", "game_0.db-wal", "game_0.db-shm"], ConanWorldFiles.Present(saved));
    }

    [Fact]
    public void Legacy_db()
    {
        var saved = CreateSaved();
        File.WriteAllText(Path.Combine(saved, "game.db"), "legacy");
        File.WriteAllText(Path.Combine(saved, "game.db-wal"), "wal");

        Assert.Equal(["game.db", "game.db-wal"], ConanWorldFiles.Present(saved));
        Assert.Equal(ConanWorldFiles.WorldTypeLegacy, ConanWorldFiles.DetectWorldType(ConanWorldFiles.Present(saved)));
        Assert.Equal("game.db", ConanWorldFiles.MainDatabaseFileName(ConanWorldFiles.Present(saved)));
    }

    [Fact]
    public void No_world_files()
    {
        var saved = CreateSaved();
        Assert.Empty(ConanWorldFiles.Present(saved));
        Assert.Empty(ConanWorldFiles.Present(null));
        Assert.Empty(ConanWorldFiles.Present(Path.Combine(saved, "missing")));
    }

    [Fact]
    public void Unrelated_db_files_and_nested_paths_are_ignored()
    {
        var saved = CreateSaved();
        File.WriteAllText(Path.Combine(saved, "foo.db"), "nope");
        File.WriteAllText(Path.Combine(saved, "game.bak.db"), "nope");
        var nested = Path.Combine(saved, "sub");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "game_0.db"), "nested");
        File.WriteAllText(Path.Combine(nested, "game.db"), "nested-legacy");

        Assert.Empty(ConanWorldFiles.Present(saved));
    }

    private static string CreateSaved()
    {
        var saved = Path.Combine(Path.GetTempPath(), "csc-m3-world", Guid.NewGuid().ToString("n"), "Saved");
        Directory.CreateDirectory(saved);
        return saved;
    }
}
