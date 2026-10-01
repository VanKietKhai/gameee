using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Backups;
using ConanServerControl.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Backups;

/// <summary>
/// Read-only verification of a copied Conan world backup. Never opens the live DB.
/// </summary>
public sealed class SqliteBackupVerifier : IBackupVerifier
{
    private readonly ILogger<SqliteBackupVerifier> _logger;

    public SqliteBackupVerifier(ILogger<SqliteBackupVerifier> logger)
    {
        _logger = logger;
    }

    public async Task<BackupVerificationResult> VerifyAsync(
        string backupWorldDirectory,
        BackupRecord record,
        CancellationToken cancellationToken = default)
    {
        var verifiedAt = DateTimeOffset.UtcNow;
        var main = record.MainDbFileName;
        if (string.IsNullOrWhiteSpace(main))
        {
            return Fail(verifiedAt, hashes: false, sqlite: false, "Expected main world database is missing from the backup record.");
        }

        if (string.IsNullOrWhiteSpace(backupWorldDirectory) || !Directory.Exists(backupWorldDirectory))
        {
            return Fail(verifiedAt, hashes: false, sqlite: false, "Backup world directory was not found.");
        }

        var mainPath = Path.Combine(backupWorldDirectory, main);
        if (!File.Exists(mainPath))
        {
            return Fail(verifiedAt, hashes: false, sqlite: false, $"Expected main world database '{main}' is missing from the backup copy.");
        }

        foreach (var file in record.WorldFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(file.FileName))
            {
                return Fail(verifiedAt, hashes: false, sqlite: false, "Backup manifest contains a world file with no name.");
            }

            var copyPath = Path.Combine(backupWorldDirectory, file.FileName);
            if (!File.Exists(copyPath))
            {
                return Fail(verifiedAt, hashes: false, sqlite: false, $"Copied world file '{file.FileName}' is missing.");
            }

            try
            {
                var actualHash = BackupFileHasher.Sha256File(copyPath);
                if (!string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Fail(verifiedAt, hashes: false, sqlite: false, $"SHA-256 mismatch for '{file.FileName}'.");
                }

                var actualSize = new FileInfo(copyPath).Length;
                if (actualSize != file.SizeBytes)
                {
                    return Fail(verifiedAt, hashes: false, sqlite: false, $"Size mismatch for '{file.FileName}'.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not hash backup copy {File}.", copyPath);
                return Fail(verifiedAt, hashes: false, sqlite: false, $"Could not hash copied world file '{file.FileName}': {ex.Message}");
            }
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = mainPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            await using var connection = new SqliteConnection(builder.ToString());
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await using var queryOnly = connection.CreateCommand();
                queryOnly.CommandText = "PRAGMA query_only=ON;";
                await queryOnly.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "PRAGMA query_only is unavailable on the copied database; continuing with read-only quick_check.");
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<string>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(reader.GetString(0));
            }

            if (rows.Count == 1 && string.Equals(rows[0], "ok", StringComparison.OrdinalIgnoreCase))
            {
                return new BackupVerificationResult
                {
                    Succeeded = true,
                    HashesVerified = true,
                    SqliteVerified = true,
                    Detail = "ok",
                    VerifiedAt = verifiedAt
                };
            }

            var detail = rows.Count == 0
                ? "PRAGMA quick_check returned no result."
                : string.Join("; ", rows);
            return Fail(verifiedAt, hashes: true, sqlite: false, detail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not open copied world database {Path} read-only.", mainPath);
            return Fail(verifiedAt, hashes: true, sqlite: false, $"Could not open copied world database: {ex.Message}");
        }
    }

    private static BackupVerificationResult Fail(
        DateTimeOffset verifiedAt,
        bool hashes,
        bool sqlite,
        string detail) =>
        new()
        {
            Succeeded = false,
            HashesVerified = hashes,
            SqliteVerified = sqlite,
            Detail = detail,
            VerifiedAt = verifiedAt
        };
}
