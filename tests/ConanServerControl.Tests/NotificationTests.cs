using System.Text.Json;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Notifications;
using ConanServerControl.Core.Updates;

namespace ConanServerControl.Tests;

public class ConanPlayerLogParserTests
{
    private static List<PlayerLogEvent> FeedAll(params string[] lines)
    {
        var parser = new ConanPlayerLogParser();
        return lines.SelectMany(parser.Feed).ToList();
    }

    [Fact]
    public void Join_line_is_a_join()
    {
        var events = FeedAll("[2026.10.08-12.31.49:719][ 11]LogNet: Join succeeded: duckkk2301#87235");

        var e = Assert.Single(events);
        Assert.Equal(PlayerLogEventKind.Joined, e.Kind);
        Assert.Equal("duckkk2301#87235", e.Name);
    }

    [Fact]
    public void Control_channel_close_is_a_normal_quit()
    {
        var events = FeedAll(
            "[2026.10.08-14.22.19:644][443]LogNet: UNetConnection::Close: [UNetConnection] RemoteAddr: 26.150.137.65:55595, PC: FunCombat_PlayerController_C_2",
            "[2026.10.08-14.22.19:644][443]LogNet: Player disconnected: duckkk2301#87235",
            "[2026.10.08-14.22.19:644][443]LogNet: UNetConnection::SendCloseReason:",
            "[2026.10.08-14.22.19:645][443]LogNet:  - Result=ControlChannelClose, ErrorContext=\"ControlChannelClose\"");

        var e = Assert.Single(events);
        Assert.Equal(PlayerLogEventKind.Left, e.Kind);
        Assert.Equal("duckkk2301#87235", e.Name);
    }

    [Fact]
    public void Cleanup_without_client_close_is_a_dropped_connection()
    {
        var events = FeedAll(
            "[2026.10.07-06.39.43:925][481]LogNet: UNetConnection::Cleanup: Closing open connection. [UNetConnection] RemoteAddr: 26.150.137.65:61903",
            "[2026.10.07-06.39.43:926][481]LogNet: Player disconnected: duckkk2301#87235",
            "[2026.10.07-06.39.43:926][481]LogNet: UNetConnection::SendCloseReason:",
            "[2026.10.07-06.39.43:926][481]LogNet:  - Result=Cleanup, ErrorContext=\"Cleanup\"");

        var e = Assert.Single(events);
        Assert.Equal(PlayerLogEventKind.Dropped, e.Kind);
        Assert.Equal("Cleanup", e.Reason);
    }

    [Fact]
    public void Disconnect_right_after_host_closed_is_a_server_shutdown()
    {
        var events = FeedAll(
            "[2026.10.07-08.13.43:998][630]LogNet: UNetConnection::SendCloseReason:",
            "[2026.10.07-08.13.43:998][630]LogNet:  - Result=HostClosedConnection, ErrorContext=\"HostClosedConnection\"",
            "[2026.10.07-08.13.44:034][630]LogNet: UNetConnection::Cleanup: Closing open connection.",
            "[2026.10.07-08.13.44:035][630]LogNet: UNetConnection::Close: [UNetConnection] RemoteAddr: 26.150.137.65:58670",
            "[2026.10.07-08.13.44:036][630]LogNet: Player disconnected: duckkk2301#87235",
            "[2026.10.07-08.13.44:036][630]LogNet: UNetConnection::SendCloseReason:",
            "[2026.10.07-08.13.44:036][630]LogNet:  - Result=Cleanup, ErrorContext=\"Cleanup\"");

        var e = Assert.Single(events);
        Assert.Equal(PlayerLogEventKind.ServerShutdown, e.Kind);
    }

    [Fact]
    public void Unknown_disconnects_from_rejected_logins_are_ignored()
    {
        var events = FeedAll(
            "[2026.10.08-12.49.27:488][535]LogNet: Player disconnected: Unknown",
            "[2026.10.08-12.49.27:488][535]LogNet:  - Result=PreLoginFailure, ErrorContext=\"PreLoginFailure\"");

        Assert.Empty(events);
    }

    [Fact]
    public void Missing_result_line_falls_back_to_a_normal_quit()
    {
        var events = FeedAll(
            "LogNet: Player disconnected: Uyna#89835",
            "line 1",
            "line 2",
            "line 3",
            "line 4",
            "line 5");

        var e = Assert.Single(events);
        Assert.Equal(PlayerLogEventKind.Left, e.Kind);
    }

    [Theory]
    [InlineData("duckkk2301#87235", "duckkk2301")]
    [InlineData("Uyna", "Uyna")]
    [InlineData("#123", "#123")]
    public void Display_name_drops_the_discriminator(string raw, string expected)
    {
        Assert.Equal(expected, ConanPlayerLogParser.DisplayName(raw));
    }
}

public class ServerStateTransitionTests
{
    private static ServerRuntimeState State(ServerStatus status, bool crash = false, bool loop = false) => new()
    {
        Status = status,
        LastExitWasCrash = crash,
        CrashLoopDetected = loop,
        LastExitCode = crash ? -1073741819 : 0
    };

    [Fact]
    public void Same_status_republished_produces_nothing()
    {
        Assert.Empty(ServerStateTransitions.Detect(State(ServerStatus.Online), State(ServerStatus.Online), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void First_snapshot_produces_nothing()
    {
        Assert.Empty(ServerStateTransitions.Detect(null, State(ServerStatus.Online), DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(ServerStatus.Offline, ServerStatus.Starting, ServerEventKind.ServerStarting)]
    [InlineData(ServerStatus.Starting, ServerStatus.Online, ServerEventKind.ServerOnline)]
    [InlineData(ServerStatus.Stopping, ServerStatus.Offline, ServerEventKind.ServerStopped)]
    [InlineData(ServerStatus.Online, ServerStatus.Unresponsive, ServerEventKind.ServerUnresponsive)]
    public void Lifecycle_changes_map_to_events(ServerStatus from, ServerStatus to, ServerEventKind expected)
    {
        var e = Assert.Single(ServerStateTransitions.Detect(State(from), State(to), DateTimeOffset.UtcNow));
        Assert.Equal(expected, e.Kind);
    }

    [Fact]
    public void Recovering_from_unresponsive_is_not_announced_as_ready()
    {
        Assert.Empty(ServerStateTransitions.Detect(State(ServerStatus.Unresponsive), State(ServerStatus.Online), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Requested_stop_is_expected_but_direct_exit_is_unexpected()
    {
        var requested = Assert.Single(ServerStateTransitions.Detect(State(ServerStatus.Stopping), State(ServerStatus.Offline), DateTimeOffset.UtcNow));
        var direct = Assert.Single(ServerStateTransitions.Detect(State(ServerStatus.Online), State(ServerStatus.Offline), DateTimeOffset.UtcNow));

        Assert.False(requested.Unexpected);
        Assert.True(direct.Unexpected);
    }

    [Fact]
    public void Crash_exit_is_a_crash_with_exit_code()
    {
        var e = Assert.Single(ServerStateTransitions.Detect(State(ServerStatus.Online), State(ServerStatus.Offline, crash: true), DateTimeOffset.UtcNow));

        Assert.Equal(ServerEventKind.ServerCrashed, e.Kind);
        Assert.Equal(-1073741819, e.ExitCode);
    }

    [Fact]
    public void Crash_loop_is_announced_once()
    {
        var first = ServerStateTransitions.Detect(State(ServerStatus.Offline, crash: true), State(ServerStatus.Error, crash: true, loop: true), DateTimeOffset.UtcNow);
        var again = ServerStateTransitions.Detect(State(ServerStatus.Error, crash: true, loop: true), State(ServerStatus.Error, crash: true, loop: true), DateTimeOffset.UtcNow);

        Assert.Contains(first, e => e.Kind == ServerEventKind.ServerCrashLoop);
        Assert.Empty(again);
    }

    [Fact]
    public void Error_while_starting_is_a_failed_start()
    {
        var current = State(ServerStatus.Error);
        current.LastError = "Readiness timed out";

        var e = Assert.Single(ServerStateTransitions.Detect(State(ServerStatus.Starting), current, DateTimeOffset.UtcNow));

        Assert.Equal(ServerEventKind.ServerStartFailed, e.Kind);
        Assert.Equal("Readiness timed out", e.Detail);
    }
}

public class StartupTimeEstimatorTests
{
    private static IEnumerable<ActivityLogEntry> History(params int[] seconds)
    {
        var t = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        var id = 0;
        foreach (var s in seconds)
        {
            yield return new ActivityLogEntry { Id = ++id, Timestamp = t, Message = "Server process launched (PID 1). Waiting for readiness." };
            yield return new ActivityLogEntry { Id = ++id, Timestamp = t.AddSeconds(s), Message = "Server is ready." };
            t = t.AddHours(1);
        }
    }

    [Fact]
    public void Measures_launch_to_ready_pairs()
    {
        var durations = StartupTimeEstimator.MeasureDurations(History(37, 102, 38));

        Assert.Equal(new[] { 37d, 102d, 38d }, durations.Select(d => d.TotalSeconds));
    }

    [Fact]
    public void Launch_that_never_became_ready_is_ignored()
    {
        var t = DateTimeOffset.UtcNow;
        var entries = new[]
        {
            new ActivityLogEntry { Id = 1, Timestamp = t, Message = "Server process launched (PID 1). Waiting for readiness." },
            new ActivityLogEntry { Id = 2, Timestamp = t.AddSeconds(20), Message = "Server process exited unexpectedly." },
            new ActivityLogEntry { Id = 3, Timestamp = t.AddMinutes(5), Message = "Server is ready." }
        };

        Assert.Empty(StartupTimeEstimator.MeasureDurations(entries));
    }

    [Fact]
    public void Describes_typical_and_worst_case_from_measured_starts()
    {
        var text = StartupTimeEstimator.Describe(StartupTimeEstimator.MeasureDurations(History(37, 102, 38, 35, 36, 38, 37)));

        Assert.Equal("Thường mất khoảng 40 giây, có lúc tới 2 phút.", text);
    }

    [Fact]
    public void Consistent_starts_give_a_single_figure()
    {
        Assert.Equal("Thường mất khoảng 40 giây.", StartupTimeEstimator.Describe(StartupTimeEstimator.MeasureDurations(History(36, 37, 38))));
    }

    [Fact]
    public void No_history_gives_a_generic_message()
    {
        Assert.Equal("Vui lòng chờ một lát.", StartupTimeEstimator.Describe(Array.Empty<TimeSpan>()));
    }
}

public class DiscordEmbedFactoryTests
{
    private static readonly DiscordEmbedContext Context = new()
    {
        ServerName = "Test Server",
        ServerIp = "26.84.226.21",
        GamePort = 7777,
        StartupEstimate = "Thường mất khoảng 40 giây."
    };

    [Fact]
    public void Ready_embed_shows_direct_connect_address_as_inline_code()
    {
        var embed = DiscordEmbedFactory.Create(new ServerEvent { Kind = ServerEventKind.ServerOnline }, Context);

        Assert.Equal("✅ Server đã sẵn sàng", embed.Title);
        Assert.Contains("Direct IP: `26.84.226.21` Port: `7777`", embed.Description);
        Assert.Equal(DiscordEmbedFactory.Blue, embed.Color);
    }

    [Fact]
    public void Starting_embed_uses_the_measured_estimate()
    {
        var embed = DiscordEmbedFactory.Create(new ServerEvent { Kind = ServerEventKind.ServerStarting }, Context);

        Assert.StartsWith("Thường mất khoảng 40 giây.", embed.Description);
        Assert.DoesNotContain("30 giây", embed.Description);
    }

    [Fact]
    public void Player_names_lose_the_discriminator_and_markdown()
    {
        var embed = DiscordEmbedFactory.Create(new ServerEvent { Kind = ServerEventKind.PlayerJoined, PlayerName = "bad_*name*#1234" }, Context);

        Assert.Equal("**bad\\_\\*name\\*** vừa tham gia.", embed.Description);
    }

    [Fact]
    public void Webhook_json_has_embed_fields_and_blocks_mentions()
    {
        var embed = DiscordEmbedFactory.Create(new ServerEvent { Kind = ServerEventKind.PlayerLeft, PlayerName = "Uyna#89835", Timestamp = new DateTimeOffset(2026, 10, 9, 1, 2, 3, TimeSpan.Zero) }, Context);

        using var doc = JsonDocument.Parse(DiscordEmbedFactory.ToWebhookJson("Conan Server", new[] { embed }));
        var root = doc.RootElement;
        var e = root.GetProperty("embeds")[0];

        Assert.Equal("Conan Server", root.GetProperty("username").GetString());
        Assert.Equal(0, root.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());
        Assert.Equal("➖ Người chơi rời server", e.GetProperty("title").GetString());
        Assert.Equal("**Uyna** đã thoát.", e.GetProperty("description").GetString());
        Assert.Equal(DiscordEmbedFactory.Orange, e.GetProperty("color").GetInt32());
        Assert.Equal("2026-10-09T01:02:03.000Z", e.GetProperty("timestamp").GetString());
        Assert.Equal("Test Server", e.GetProperty("footer").GetProperty("text").GetString());
    }

    [Fact]
    public void Every_event_kind_builds_a_titled_embed()
    {
        foreach (var kind in Enum.GetValues<ServerEventKind>())
        {
            var embed = DiscordEmbedFactory.Create(new ServerEvent { Kind = kind, PlayerName = "p", Items = new[] { "Mod A" } }, Context);
            Assert.False(string.IsNullOrWhiteSpace(embed.Title), kind.ToString());
            Assert.False(string.IsNullOrWhiteSpace(embed.Description), kind.ToString());
        }
    }

    [Theory]
    [InlineData("https://discord.com/api/webhooks/1/abc", true)]
    [InlineData("https://discordapp.com/api/webhooks/1/abc", true)]
    [InlineData("http://discord.com/api/webhooks/1/abc", false)]
    [InlineData("https://evil.example/api/webhooks/1/abc", false)]
    [InlineData("https://discord.com/channels/1", false)]
    [InlineData("", false)]
    public void Only_https_discord_webhooks_are_accepted(string url, bool valid)
    {
        Assert.Equal(valid, DiscordWebhook.IsValidUrl(url));
    }
}

public class SteamBuildInfoTests
{
    [Fact]
    public void Parses_public_build_from_steamcmd_net()
    {
        const string json = """
            {"data":{"443030":{"depots":{"branches":{"public":{"buildid":"25792439","timeupdated":"1759900000"}}}}},"status":"success"}
            """;

        var build = SteamBuildInfo.ParseSteamCmdNet(443030, json);

        Assert.NotNull(build);
        Assert.Equal("25792439", build!.BuildId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1759900000), build.UpdatedAt);
    }

    [Theory]
    [InlineData("""{"data":{}}""")]
    [InlineData("""{"data":{"443030":{"depots":{"branches":{"public":{"buildid":"abc"}}}}}}""")]
    [InlineData("not json")]
    public void Unusable_replies_give_null(string json)
    {
        Assert.Null(SteamBuildInfo.ParseSteamCmdNet(443030, json));
    }

    [Fact]
    public void Finds_the_manifest_in_the_steam_library_of_the_install()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "csc-steam-" + Guid.NewGuid().ToString("n"))).FullName;
        try
        {
            var steamapps = Directory.CreateDirectory(Path.Combine(root, "steamapps")).FullName;
            var install = Directory.CreateDirectory(Path.Combine(steamapps, "common", "Conan Exiles Dedicated Server")).FullName;
            File.WriteAllText(Path.Combine(steamapps, "appmanifest_443030.acf"), "\"AppState\"\n{\n\t\"appid\"\t\t\"443030\"\n\t\"buildid\"\t\t\"25738716\"\n}\n");
            File.WriteAllText(Path.Combine(steamapps, "appmanifest_440900.acf"), "\"AppState\"\n{\n\t\"buildid\"\t\t\"25791535\"\n}\n");

            Assert.Equal("25738716", SteamBuildInfo.FindInstalledBuild(443030, install));
            // The client is found through the server's library when its own folder is unknown.
            Assert.Equal("25791535", SteamBuildInfo.FindInstalledBuild(440900, null, install));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("25738716", "25792439", true)]
    [InlineData("25792439", "25792439", false)]
    [InlineData("25792439", "25738716", false)]
    [InlineData(null, "25792439", false)]
    [InlineData("25738716", null, false)]
    public void Newer_only_when_both_numeric_and_higher(string? installed, string? available, bool expected)
    {
        Assert.Equal(expected, SteamBuildInfo.IsNewer(installed, available));
    }
}

public class RconPlayerListParserTests
{
    [Fact]
    public void Parses_pipe_separated_listplayers()
    {
        const string reply = "Idx | Char name | Player name | User ID | Platform ID | Platform Name\n" +
                             "  0 | Duc | duckkk2301#87235 | ABCDEF0123456789 | 76561199389893030 | Steam\n";

        var player = Assert.Single(RconPlayerListParser.Parse(reply));

        Assert.Equal("Duc (duckkk2301#87235)", player.Name);
        Assert.Equal("76561199389893030", player.SteamId);
    }

    [Fact]
    public void Header_only_means_nobody_online()
    {
        Assert.Empty(RconPlayerListParser.Parse("Idx | Char name | Player name | User ID | Platform ID | Platform Name\n"));
    }
}
