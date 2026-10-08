namespace ConanServerControl.Core.Notifications;

public enum ServerEventKind
{
    ServerStarting,
    ServerOnline,
    ServerStopped,
    ServerCrashed,
    ServerCrashLoop,
    ServerStartFailed,
    ServerUnresponsive,
    PlayerJoined,
    PlayerLeft,
    PlayerDisconnected,
    ServerUpdateAvailable,
    ModUpdateAvailable,
    ClientUpdateAvailable,
    UpdateStarted,
    UpdateCompleted,
    UpdateFailed,
    Test
}

/// <summary>Something worth telling the players about (Discord, activity log).</summary>
public sealed record ServerEvent
{
    public ServerEventKind Kind { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public string? PlayerName { get; init; }

    /// <summary>The app action involved, for example "Update server" (see <c>IServerActionGate</c>).</summary>
    public string? Action { get; init; }

    /// <summary>Free text: an error or a disconnect reason.</summary>
    public string? Detail { get; init; }

    public string? InstalledBuild { get; init; }

    public string? AvailableBuild { get; init; }

    public int? ExitCode { get; init; }

    /// <summary>The server stopped without a stop request from the app.</summary>
    public bool Unexpected { get; init; }

    public IReadOnlyList<string> Items { get; init; } = Array.Empty<string>();
}

/// <summary>In-process fan-out of <see cref="ServerEvent"/>s from producers to notifiers.</summary>
public interface IServerEventBus
{
    event EventHandler<ServerEvent>? Published;

    void Publish(ServerEvent serverEvent);
}

public sealed class ServerEventBus : IServerEventBus
{
    public event EventHandler<ServerEvent>? Published;

    public void Publish(ServerEvent serverEvent)
    {
        var handlers = Published;
        if (handlers is null)
        {
            return;
        }

        // A failing subscriber must never break the operation that published the event.
        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<ServerEvent>>())
        {
            try
            {
                handler(this, serverEvent);
            }
            catch
            {
                // ignored
            }
        }
    }
}
