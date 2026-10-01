namespace ConanServerControl.Core.Abstractions;

/// <summary>
/// Positive evidence that the dedicated server is executing a shutdown, read from the server's own
/// log (for example "Engine exit requested"). It decides whether a stop gets the extended graceful
/// window. Missing evidence is not proof of a hang; it only keeps the short window.
/// </summary>
public interface IServerShutdownProbe
{
    /// <summary>Marks the point after which evidence counts. Call before sending the shutdown command.</summary>
    long Mark();

    /// <summary>The first shutdown evidence line written after <paramref name="mark"/>, or null.</summary>
    string? FindShutdownEvidence(long mark);
}
