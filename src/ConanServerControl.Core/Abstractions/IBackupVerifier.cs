using ConanServerControl.Core.Models;

namespace ConanServerControl.Core.Abstractions;

/// <summary>
/// Verifies a copied Conan world backup. Never opens or mutates the live world.
/// </summary>
public interface IBackupVerifier
{
    Task<BackupVerificationResult> VerifyAsync(
        string backupWorldDirectory,
        BackupRecord record,
        CancellationToken cancellationToken = default);
}

public sealed class BackupVerificationResult
{
    public bool Succeeded { get; init; }

    public bool HashesVerified { get; init; }

    public bool SqliteVerified { get; init; }

    public string Detail { get; init; } = string.Empty;

    public DateTimeOffset VerifiedAt { get; init; } = DateTimeOffset.UtcNow;
}
