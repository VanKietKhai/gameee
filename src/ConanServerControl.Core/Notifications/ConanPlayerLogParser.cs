using System.Text.RegularExpressions;

namespace ConanServerControl.Core.Notifications;

public enum PlayerLogEventKind
{
    Joined,

    /// <summary>The client closed the connection itself (normal quit).</summary>
    Left,

    /// <summary>The server dropped the connection (timeout, replication stall, network loss).</summary>
    Dropped,

    /// <summary>Everyone is disconnected because the server is shutting down.</summary>
    ServerShutdown
}

public sealed record PlayerLogEvent(PlayerLogEventKind Kind, string Name, string? Reason = null);

/// <summary>
/// Stateful line parser for the dedicated server's ConanSandbox.log player lines:
/// <c>LogNet: Join succeeded: name#1234</c>, then on leave
/// <c>LogNet: Player disconnected: name#1234</c> followed by
/// <c>LogNet:  - Result=ControlChannelClose, ...</c>. A client quit closes the control channel;
/// <c>Cleanup</c> (or anything else) means the server dropped the connection, and
/// <c>HostClosedConnection</c> just before means the server itself was shutting down.
/// "Player disconnected: Unknown" belongs to rejected logins and is ignored.
/// </summary>
public sealed class ConanPlayerLogParser
{
    private const string JoinMarker = "LogNet: Join succeeded: ";
    private const string DisconnectMarker = "LogNet: Player disconnected: ";
    private const string ResultMarker = "LogNet:  - Result=";
    private const int ResultLookahead = 4;
    private const int HostClosedLookback = 8;

    private string? _pendingName;
    private bool _pendingDuringShutdown;
    private int _linesSincePending;
    private int _linesSinceHostClosed = int.MaxValue;

    public IReadOnlyList<PlayerLogEvent> Feed(string line)
    {
        var events = new List<PlayerLogEvent>(1);
        if (_linesSinceHostClosed != int.MaxValue)
        {
            _linesSinceHostClosed++;
        }

        var result = ExtractAfter(line, ResultMarker);
        if (result is not null)
        {
            var reason = result.Split(',', 2)[0].Trim();
            if (reason == "HostClosedConnection")
            {
                _linesSinceHostClosed = 0;
            }

            if (_pendingName is not null)
            {
                events.Add(Classify(_pendingName, reason, _pendingDuringShutdown));
                _pendingName = null;
            }

            return events;
        }

        if (_pendingName is not null && ++_linesSincePending > ResultLookahead)
        {
            // No result line followed; do not guess that it was abnormal.
            events.Add(new PlayerLogEvent(_pendingDuringShutdown ? PlayerLogEventKind.ServerShutdown : PlayerLogEventKind.Left, _pendingName));
            _pendingName = null;
        }

        var joined = ExtractAfter(line, JoinMarker);
        if (joined is not null)
        {
            var name = joined.Trim();
            if (name.Length > 0)
            {
                events.Add(new PlayerLogEvent(PlayerLogEventKind.Joined, name));
            }

            return events;
        }

        var left = ExtractAfter(line, DisconnectMarker);
        if (left is not null)
        {
            var name = left.Trim();
            if (name.Length > 0 && !name.Equals("Unknown", StringComparison.Ordinal))
            {
                _pendingName = name;
                _linesSincePending = 0;
                _pendingDuringShutdown = _linesSinceHostClosed <= HostClosedLookback;
            }
        }

        return events;
    }

    /// <summary>Removes the Funcom/Steam discriminator: "duckkk2301#87235" becomes "duckkk2301".</summary>
    public static string DisplayName(string name)
    {
        var trimmed = Regex.Replace(name.Trim(), @"#\d+$", string.Empty);
        return trimmed.Length == 0 ? name.Trim() : trimmed;
    }

    private static PlayerLogEvent Classify(string name, string reason, bool duringShutdown)
    {
        if (duringShutdown || reason == "HostClosedConnection")
        {
            return new PlayerLogEvent(PlayerLogEventKind.ServerShutdown, name, reason);
        }

        return reason == "ControlChannelClose"
            ? new PlayerLogEvent(PlayerLogEventKind.Left, name, reason)
            : new PlayerLogEvent(PlayerLogEventKind.Dropped, name, reason);
    }

    private static string? ExtractAfter(string line, string marker)
    {
        var index = line.IndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? null : line[(index + marker.Length)..];
    }
}
