using ConanServerControl.Infrastructure.Backups;
using Microsoft.Data.Sqlite;

namespace ConanServerControl.Tests;

public sealed class StoppedWorldReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "csc-stopped-world-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(_root, "world.db");

    public StoppedWorldReaderTests()
    {
        Directory.CreateDirectory(_root);
        using var connection = new SqliteConnection($"Data Source={Database};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE actors (id INTEGER); INSERT INTO actors VALUES (1);";
        command.ExecuteNonQuery();
    }

    [Fact]
    public void Normal_database_is_read_only_and_has_no_side_effects()
    {
        var before = File.ReadAllBytes(Database);
        using (var connection = StoppedWorldReader.Open(Database, () => true))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check";
            Assert.Equal("ok", command.ExecuteScalar());
            command.CommandText = "DELETE FROM actors";
            Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        }
        Assert.Equal(before, File.ReadAllBytes(Database));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public void Committed_nonempty_wal_is_blocked_without_mutation()
    {
        using var writer = new SqliteConnection($"Data Source={Database};Pooling=False");
        writer.Open();
        using var command = writer.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; INSERT INTO actors VALUES (2);";
        command.ExecuteNonQuery();
        var wal = ReadSharedBytes(Database + "-wal");
        Assert.NotEmpty(wal);
        var error = Assert.Throws<InvalidOperationException>(() => StoppedWorldReader.Open(Database, () => true));
        Assert.Contains("INCONCLUSIVE", error.Message);
        Assert.Equal(wal, ReadSharedBytes(Database + "-wal"));
    }

    [Theory]
    [InlineData("root server")]
    [InlineData("orphan shipping child")]
    public void Any_running_server_process_blocks_before_file_access(string process)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            StoppedWorldReader.Open(Path.Combine(_root, process, "missing.db"), () => false));
        Assert.Contains("process tree", error.Message);
    }

    [Fact]
    public void Process_appearing_during_preflight_blocks_inspection()
    {
        var probes = 0;
        Assert.Throws<InvalidOperationException>(() => StoppedWorldReader.Open(Database, () => ++probes == 1));
        Assert.Equal(2, probes);
    }

    [Fact]
    public void Unpaired_shm_is_inconclusive()
    {
        File.WriteAllBytes(Database + "-shm", new byte[32768]);
        Assert.Throws<InvalidOperationException>(() => StoppedWorldReader.Open(Database, () => true));
    }

    [Fact]
    public void Empty_wal_has_no_frames_even_with_leftover_shm()
    {
        File.WriteAllBytes(Database + "-wal", []);
        File.WriteAllBytes(Database + "-shm", new byte[32768]);
        using var connection = StoppedWorldReader.Open(Database, () => true);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM actors";
        Assert.Equal(1L, command.ExecuteScalar());
        Assert.Equal(0, new FileInfo(Database + "-wal").Length);
        Assert.Equal(32768, new FileInfo(Database + "-shm").Length);
    }

    private static byte[] ReadSharedBytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
