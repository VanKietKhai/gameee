namespace ConanServerControl.Core.Exceptions;

/// <summary>
/// Live Workshop mod batch replacement failed. Distinguishes a verified
/// rollback of every already-replaced pak from an incomplete recovery.
/// </summary>
public sealed class ModBatchCommitException : Exception
{
    public ModBatchCommitException(
        string message,
        bool rollbackCompleted,
        bool recoveryRequired,
        Exception? inner = null)
        : base(message, inner)
    {
        RollbackCompleted = rollbackCompleted;
        RecoveryRequired = recoveryRequired;
    }

    public bool RollbackCompleted { get; }

    public bool RecoveryRequired { get; }
}
