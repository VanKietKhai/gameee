using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Notifications;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Notifications;

public sealed class AdminAnnouncer : IAdminAnnouncer
{
    private readonly IServerProcessManager _server;
    private readonly IRconService _rcon;
    private readonly IServerEventBus _events;
    private readonly IActivityLog _activity;
    private readonly ILogger<AdminAnnouncer> _logger;

    public AdminAnnouncer(
        IServerProcessManager server,
        IRconService rcon,
        IServerEventBus events,
        IActivityLog activity,
        ILogger<AdminAnnouncer> logger)
    {
        _server = server;
        _rcon = rcon;
        _events = events;
        _activity = activity;
        _logger = logger;
    }

    public async Task<AdminAnnouncementResult> SendAsync(
        string? message,
        bool toGame,
        bool toDiscord,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var text = AdminAnnouncement.Normalize(message);
        if (text is null)
        {
            return new AdminAnnouncementResult(false, false, "Chưa nhập nội dung thông báo.");
        }

        var online = _server.State.Status is ServerStatus.Online or ServerStatus.Unresponsive;
        if (!toDiscord && (!toGame || !online))
        {
            return new AdminAnnouncementResult(false, false, "Server đang tắt nên không gửi vào game được. Hãy chọn gửi lên Discord.");
        }

        var sentToGame = false;
        string? gameError = null;
        if (toGame && online)
        {
            try
            {
                await _rcon.AnnounceAsync(AdminAnnouncement.ForGame(text), cancellationToken).ConfigureAwait(false);
                sentToGame = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Admin announcement could not be broadcast over RCON.");
                gameError = "Không gửi được vào game (RCON lỗi).";
            }
        }

        if (toDiscord)
        {
            _events.Publish(new ServerEvent { Kind = ServerEventKind.AdminMessage, Detail = text, PlayerName = actor });
        }

        await _activity.AddAsync("Announce", $"{actor}: {text}", actor, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new AdminAnnouncementResult(sentToGame, toDiscord, gameError);
    }
}
