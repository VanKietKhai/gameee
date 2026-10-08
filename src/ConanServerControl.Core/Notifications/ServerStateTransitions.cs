using ConanServerControl.Core.Models;

namespace ConanServerControl.Core.Notifications;

/// <summary>
/// Turns consecutive <see cref="ServerRuntimeState"/> snapshots into lifecycle events.
/// The process manager republishes the same state on every refresh tick, so only real
/// status changes produce events.
/// </summary>
public static class ServerStateTransitions
{
    public static IReadOnlyList<ServerEvent> Detect(ServerRuntimeState? previous, ServerRuntimeState current, DateTimeOffset now)
    {
        if (previous is null)
        {
            return Array.Empty<ServerEvent>();
        }

        var events = new List<ServerEvent>();
        if (current.CrashLoopDetected && !previous.CrashLoopDetected)
        {
            events.Add(new ServerEvent { Kind = ServerEventKind.ServerCrashLoop, Timestamp = now, Detail = current.LastErrorGuidance });
        }

        if (previous.Status == current.Status)
        {
            return events;
        }

        switch (current.Status)
        {
            case ServerStatus.Starting:
                events.Add(new ServerEvent { Kind = ServerEventKind.ServerStarting, Timestamp = now });
                break;

            case ServerStatus.Online when previous.Status is ServerStatus.Starting or ServerStatus.Restarting or ServerStatus.Updating:
                events.Add(new ServerEvent { Kind = ServerEventKind.ServerOnline, Timestamp = now, InstalledBuild = current.InstalledBuild });
                break;

            case ServerStatus.Unresponsive when previous.Status is ServerStatus.Online:
                events.Add(new ServerEvent { Kind = ServerEventKind.ServerUnresponsive, Timestamp = now });
                break;

            case ServerStatus.Offline when current.LastExitWasCrash:
                events.Add(new ServerEvent
                {
                    Kind = ServerEventKind.ServerCrashed,
                    Timestamp = now,
                    ExitCode = current.LastExitCode
                });
                break;

            case ServerStatus.Offline:
                events.Add(new ServerEvent
                {
                    Kind = ServerEventKind.ServerStopped,
                    Timestamp = now,
                    ExitCode = current.LastExitCode,
                    // Requested stops pass through Stopping; going straight to Offline means the
                    // server exited on its own (cleanly, otherwise it would be a crash).
                    Unexpected = previous.Status is ServerStatus.Online or ServerStatus.Unresponsive,
                    Action = current.CurrentAction
                });
                break;

            case ServerStatus.Error when previous.Status is ServerStatus.Starting && !current.CrashLoopDetected:
                events.Add(new ServerEvent { Kind = ServerEventKind.ServerStartFailed, Timestamp = now, Detail = current.LastError });
                break;
        }

        return events;
    }
}
