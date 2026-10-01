using ConanServerControl.Core.Models;

namespace ConanServerControl.Core.Abstractions;

/// <summary>
/// Mockable readiness check used after the dedicated-server process is launched.
/// Process-alive is owned by <see cref="IServerProcessManager"/>; this probe
/// reports whether a configured endpoint (game port and/or RCON) has responded.
/// </summary>
public interface IServerReadinessProbe
{
    Task<ServerReadinessResult> ProbeAsync(
        ServerReadinessContext context,
        CancellationToken cancellationToken = default);
}

public sealed class ServerReadinessContext
{
    public int ProcessId { get; init; }

    public int GamePort { get; init; }

    public DateTimeOffset StartedAt { get; init; }
}

public sealed class ServerReadinessResult
{
    public bool IsReady { get; init; }

    public string Reason { get; init; } = string.Empty;

    public TimeSpan Elapsed { get; init; }

    public string? LastError { get; init; }

    public HealthCheckResult Health { get; init; } = HealthCheckResult.ServerStarting;
}
