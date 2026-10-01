namespace ConanServerControl.Core.Exceptions;

/// <summary>
/// Live Workshop mod batch replacement failed. Distinguishes a verified
/// rollback of every already-replaced pak from an incomplete or unverifiable recovery.
/// </summary>
public sealed class ModBatchCommitException : Exception
{
    public const string RecoveryRequiredTitle = "Mod update failed — recovery required";

    public const string UnverifiedRollbackGuidance =
        "Mod update failed and rollback could not be verified. Server was left offline to prevent starting with an inconsistent mod set.";

    public ModBatchCommitException(
        string message,
        bool rollbackCompleted,
        bool recoveryRequired,
        Exception? inner = null)
        : this(
            message,
            rollbackAttempted: true,
            rollbackCompleted,
            rollbackVerified: rollbackCompleted && !recoveryRequired,
            recoveryRequired,
            inner)
    {
    }

    public ModBatchCommitException(
        string message,
        bool rollbackAttempted,
        bool rollbackCompleted,
        bool rollbackVerified,
        bool recoveryRequired,
        Exception? inner = null)
        : base(message, inner)
    {
        RollbackAttempted = rollbackAttempted;
        RollbackCompleted = rollbackCompleted;
        RollbackVerified = rollbackVerified;
        RecoveryRequired = recoveryRequired;
    }

    public bool RollbackAttempted { get; }

    public bool RollbackCompleted { get; }

    public bool RollbackVerified { get; }

    public bool RecoveryRequired { get; }

    public bool IsSafeToRestart =>
        RollbackAttempted && RollbackCompleted && RollbackVerified && !RecoveryRequired;
}
