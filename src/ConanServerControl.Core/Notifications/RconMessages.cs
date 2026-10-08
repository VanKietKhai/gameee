namespace ConanServerControl.Core.Notifications;

/// <summary>
/// Every in-game broadcast the app sends over RCON, in one place. Vietnamese with diacritics:
/// RCON carries UTF-8 and the players' client patch ships a font with Vietnamese glyphs.
/// Double quotes are not allowed (RconService turns them into single quotes).
/// </summary>
public static class RconMessages
{
    /// <summary>Warning marks before any planned stop, restart or update: 30, 10, 5 and 1 minute.</summary>
    public static readonly IReadOnlyList<int> CountdownMarksMinutes = new[] { 30, 10, 5, 1 };

    /// <summary>
    /// The marks that fit a countdown of <paramref name="totalMinutes"/>, largest first. A total that
    /// is not itself a mark (for example 15) is announced first so players hear about it at once.
    /// </summary>
    public static IReadOnlyList<int> MarksFor(int totalMinutes)
    {
        if (totalMinutes <= 0)
        {
            return Array.Empty<int>();
        }

        var marks = CountdownMarksMinutes.Where(m => m <= totalMinutes).ToList();
        if (!marks.Contains(totalMinutes))
        {
            marks.Insert(0, totalMinutes);
        }

        return marks;
    }

    /// <summary>Sent right before the graceful shutdown command (stop, restart, update).</summary>
    public const string ShuttingDown = "[SERVER] Server đang tắt. Hẹn gặp lại mọi người!";

    /// <summary>Automatic update countdown while players are online.</summary>
    public static string UpdateWarning(int minutes) =>
        $"[SERVER] Server sẽ tắt sau {minutes} phút để cập nhật. Hãy về nơi an toàn và thoát game.";

    /// <summary>Scheduled restart countdown.</summary>
    public static string RestartWarning(int minutes) =>
        $"[SERVER] Server sẽ khởi động lại sau {minutes} phút. Hãy về nơi an toàn và thoát game.";

    /// <summary>Scheduled stop countdown.</summary>
    public static string StopWarning(int minutes) =>
        $"[SERVER] Server sẽ tắt sau {minutes} phút. Hãy về nơi an toàn và thoát game.";

    /// <summary>A scheduled stop or restart was called off.</summary>
    public const string CountdownCancelled = "[SERVER] Đã huỷ lịch tắt server. Mọi người chơi tiếp nhé!";
}
