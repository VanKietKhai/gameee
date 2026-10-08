namespace ConanServerControl.Core.Notifications;

/// <summary>
/// Every in-game broadcast the app sends over RCON, in one place. Vietnamese with diacritics:
/// RCON carries UTF-8 and the players' client patch ships a font with Vietnamese glyphs.
/// Double quotes are not allowed (RconService turns them into single quotes).
/// </summary>
public static class RconMessages
{
    /// <summary>Sent right before the graceful shutdown command (stop, restart, update).</summary>
    public const string ShuttingDown = "[SERVER] Server đang tắt. Hẹn gặp lại mọi người!";

    /// <summary>Automatic update countdown while players are online.</summary>
    public static string UpdateWarning(int minutes) =>
        $"[SERVER] Server sẽ tắt sau {minutes} phút để cập nhật. Hãy về nơi an toàn và thoát game.";

    /// <summary>Delayed restart countdown, minute marks.</summary>
    public static string RestartWarningMinutes(int minutes) =>
        $"[SERVER] Server sẽ khởi động lại sau {minutes} phút. Hãy về nơi an toàn.";

    /// <summary>Delayed restart countdown, last seconds.</summary>
    public static string RestartWarningSeconds(int seconds) =>
        $"[SERVER] Server khởi động lại sau {seconds} giây. Thoát game ngay để lưu nhân vật.";
}
