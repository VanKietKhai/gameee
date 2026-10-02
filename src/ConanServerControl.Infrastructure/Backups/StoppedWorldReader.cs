using Microsoft.Data.Sqlite;

namespace ConanServerControl.Infrastructure.Backups;

/// <summary>Non-mutating inspection, restricted to a proven stopped, checkpointed world.</summary>
public static class StoppedWorldReader
{
    public static SqliteConnection Open(string database, Func<bool> processTreeStopped)
    {
        // Probe before touching any world file, including for orphaned shipping children.
        if (!processTreeStopped())
            throw new InvalidOperationException("INCONCLUSIVE: server process tree is not proven stopped.");
        if (!File.Exists(database))
            throw new InvalidOperationException("INCONCLUSIVE: world database is missing.");

        var wal = new FileInfo(database + "-wal");
        var shm = new FileInfo(database + "-shm");
        if (wal.Exists && wal.Length > 0)
            throw new InvalidOperationException("INCONCLUSIVE: non-empty WAL requires isolated-copy validation; immutable inspection refused.");
        // A paired zero-byte WAL contains no frames to replay. Its leftover SHM is only an index.
        // An unpaired SHM is ambiguous and must not be silently ignored.
        if (shm.Exists && shm.Length > 0 && !wal.Exists)
            throw new InvalidOperationException("INCONCLUSIVE: unpaired non-empty SHM state.");
        if (!processTreeStopped())
            throw new InvalidOperationException("INCONCLUSIVE: server process appeared before world inspection.");

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = new Uri(Path.GetFullPath(database)).AbsoluteUri + "?immutable=1",
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
