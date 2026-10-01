using System.Security.Cryptography;

namespace ConanServerControl.Core.Backups;

/// <summary>
/// Streaming SHA-256 of a file on disk. Does not load the whole file into memory.
/// </summary>
public static class BackupFileHasher
{
    public static string Sha256File(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.SequentialScan);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}
