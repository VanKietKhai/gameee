using ConanServerControl.Core.Models;
using ConanServerControl.Core.Notifications;
using ConanServerControl.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class AdminAnnouncementTests
{
    [Theory]
    [InlineData("  Tối nay tắt server  ", "Tối nay tắt server")]
    [InlineData("Dòng 1\r\nDòng 2", "Dòng 1 Dòng 2")]
    [InlineData("nói \"xin chào\"", "nói 'xin chào'")]
    [InlineData("a   b", "a b")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Normalize_makes_one_clean_line(string? input, string? expected)
    {
        Assert.Equal(expected, AdminAnnouncement.Normalize(input));
    }

    [Fact]
    public void Normalize_caps_the_length()
    {
        Assert.Equal(AdminAnnouncement.MaxLength, AdminAnnouncement.Normalize(new string('x', 500))!.Length);
    }

    [Fact]
    public async Task Online_server_gets_the_broadcast_and_discord_gets_the_event()
    {
        var (announcer, rcon, events) = Create(ServerStatus.Online);

        var result = await announcer.SendAsync("23h tắt server để cập nhật", toGame: true, toDiscord: true, "admin");

        Assert.True(result.SentToGame);
        Assert.True(result.SentToDiscord);
        Assert.Equal("[ADMIN] 23h tắt server để cập nhật", Assert.Single(rcon.Announcements));
        var e = Assert.Single(events);
        Assert.Equal(ServerEventKind.AdminMessage, e.Kind);
        Assert.Equal("23h tắt server để cập nhật", e.Detail);
    }

    [Fact]
    public async Task Offline_server_only_reaches_discord()
    {
        var (announcer, rcon, events) = Create(ServerStatus.Offline);

        var result = await announcer.SendAsync("Mai bảo trì", toGame: true, toDiscord: true, "admin");

        Assert.False(result.SentToGame);
        Assert.True(result.SentToDiscord);
        Assert.Empty(rcon.Announcements);
        Assert.Single(events);
    }

    [Fact]
    public async Task Offline_server_without_discord_is_refused()
    {
        var (announcer, rcon, events) = Create(ServerStatus.Offline);

        var result = await announcer.SendAsync("Mai bảo trì", toGame: true, toDiscord: false, "admin");

        Assert.False(result.SentToGame || result.SentToDiscord);
        Assert.NotNull(result.Error);
        Assert.Empty(rcon.Announcements);
        Assert.Empty(events);
    }

    [Fact]
    public void Discord_embed_shows_the_message()
    {
        var embed = DiscordEmbedFactory.Create(
            new ServerEvent { Kind = ServerEventKind.AdminMessage, Detail = "Tối nay tắt server" },
            new DiscordEmbedContext());

        Assert.Equal("📢 Thông báo từ admin", embed.Title);
        Assert.Equal("Tối nay tắt server", embed.Description);
    }

    private static (AdminAnnouncer, RecordingAnnounceRcon, List<ServerEvent>) Create(ServerStatus status)
    {
        var server = new StatusOnlyServer();
        server.State.Status = status;
        var rcon = new RecordingAnnounceRcon();
        var bus = new ServerEventBus();
        var events = new List<ServerEvent>();
        bus.Published += (_, e) => events.Add(e);
        var announcer = new AdminAnnouncer(server, rcon, bus, new MemoryActivityLog(), NullLogger<AdminAnnouncer>.Instance);
        return (announcer, rcon, events);
    }

    private sealed class RecordingAnnounceRcon : ConanServerControl.Core.Abstractions.IRconService
    {
        public List<string> Announcements { get; } = new();

        public bool IsConnected => true;

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);

        public Task AnnounceAsync(string message, CancellationToken cancellationToken = default)
        {
            Announcements.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PlayerInfo>> GetPlayersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlayerInfo>>(Array.Empty<PlayerInfo>());
    }
}
