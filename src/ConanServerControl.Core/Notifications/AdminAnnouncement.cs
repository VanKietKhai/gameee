namespace ConanServerControl.Core.Notifications;

/// <summary>Validation and formatting for operator messages sent to the game and Discord.</summary>
public static class AdminAnnouncement
{
    public const int MaxLength = 200;
    public const string GamePrefix = "[ADMIN] ";

    /// <summary>
    /// One line, trimmed, at most <see cref="MaxLength"/> characters, no double quotes
    /// (the RCON broadcast command cannot carry them). Null when nothing is left to send.
    /// </summary>
    public static string? Normalize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var oneLine = string.Join(' ', message.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()))
            .Replace('"', '\'')
            .Trim();
        while (oneLine.Contains("  ", StringComparison.Ordinal))
        {
            oneLine = oneLine.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (oneLine.Length == 0)
        {
            return null;
        }

        return oneLine.Length <= MaxLength ? oneLine : oneLine[..MaxLength];
    }

    public static string ForGame(string normalized) => GamePrefix + normalized;
}

public sealed record AdminAnnouncementResult(bool SentToGame, bool SentToDiscord, string? Error);

/// <summary>Sends an operator message in game (RCON broadcast, server online only) and/or to Discord.</summary>
public interface IAdminAnnouncer
{
    Task<AdminAnnouncementResult> SendAsync(string? message, bool toGame, bool toDiscord, string actor, CancellationToken cancellationToken = default);
}
