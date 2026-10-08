using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConanServerControl.Core.Models;

namespace ConanServerControl.Core.Notifications;

public sealed record DiscordEmbed(string Title, string Description, int Color, DateTimeOffset Timestamp, string? Footer = null);

/// <summary>Values the embed text needs that are not part of the event itself.</summary>
public sealed record DiscordEmbedContext
{
    public string ServerName { get; init; } = "Conan Server";

    public string? ServerIp { get; init; }

    public int GamePort { get; init; } = AppConstants.DefaultGamePort;

    /// <summary>Text from <see cref="StartupTimeEstimator.Describe"/>.</summary>
    public string? StartupEstimate { get; init; }

    public bool RestartAfterCrash { get; init; }

    public AutomationMode AutomationMode { get; init; } = AutomationMode.Manual;
}

/// <summary>
/// Builds one Discord embed per event: coloured bar, emoji title, short Vietnamese body, timestamp.
/// </summary>
public static class DiscordEmbedFactory
{
    public const int Green = 0x2ECC71;
    public const int Orange = 0xE67E22;
    public const int Yellow = 0xF1C40F;
    public const int Red = 0xE74C3C;
    public const int Blue = 0x3498DB;
    public const int Purple = 0x9B59B6;
    public const int Grey = 0x95A5A6;

    public static DiscordEmbed Create(ServerEvent e, DiscordEmbedContext context)
    {
        var (title, body, color) = e.Kind switch
        {
            ServerEventKind.PlayerJoined => ("➕ Người chơi vào server", $"**{Player(e)}** vừa tham gia.", Green),
            ServerEventKind.PlayerLeft => ("➖ Người chơi rời server", $"**{Player(e)}** đã thoát.", Orange),
            ServerEventKind.PlayerDisconnected => ("⚠️ Mất kết nối bất thường",
                $"**{Player(e)}** bị ngắt đột ngột (không phải tự thoát).", Yellow),
            ServerEventKind.ServerStarting => ("🟢 Server đang khởi động...",
                $"{context.StartupEstimate ?? "Vui lòng chờ một lát."}{Environment.NewLine}Sẽ báo lại khi server sẵn sàng.", Green),
            ServerEventKind.ServerOnline => ("✅ Server đã sẵn sàng", Ready(context), Blue),
            ServerEventKind.ServerStopped => ("🔴 Server đã tắt", Stopped(e), Red),
            ServerEventKind.ServerCrashed => ("💥 Server vừa bị crash", Crashed(e, context), Red),
            ServerEventKind.ServerCrashLoop => ("🚨 Server crash liên tục",
                "Server bị crash nhiều lần liên tiếp nên app đã ngừng tự khởi động lại. Chờ admin kiểm tra.", Red),
            ServerEventKind.ServerStartFailed => ("❌ Khởi động thất bại",
                string.IsNullOrWhiteSpace(e.Detail) ? "Server không khởi động được. Chờ admin kiểm tra." : $"Server không khởi động được: {e.Detail}", Red),
            ServerEventKind.ServerUnresponsive => ("⚠️ Server không phản hồi",
                "Server vẫn chạy nhưng không phản hồi. App đang theo dõi.", Yellow),
            ServerEventKind.ServerUpdateAvailable => ("🆕 Có bản cập nhật server",
                $"Build đang chạy: `{e.InstalledBuild ?? "?"}`{Environment.NewLine}Build mới trên Steam: `{e.AvailableBuild ?? "?"}`{Environment.NewLine}{Automation(context)}", Purple),
            ServerEventKind.ModUpdateAvailable => ("🧩 Mod có bản cập nhật", ModsBody(e, context), Purple),
            ServerEventKind.ClientUpdateAvailable => ("💻 Game Conan Exiles có bản cập nhật",
                $"Build mới: `{e.AvailableBuild ?? "?"}` (đang cài: `{e.InstalledBuild ?? "?"}`).{Environment.NewLine}Mở Steam để cập nhật game trước khi vào server.", Purple),
            ServerEventKind.UpdateStarted => ("🛠️ Đang cập nhật",
                $"{DescribeAction(e.Action)}. Server sẽ tạm tắt nếu đang chạy, xong sẽ báo lại.", Purple),
            ServerEventKind.UpdateCompleted => ("🆙 Cập nhật xong", UpdatedBody(e), Green),
            ServerEventKind.UpdateFailed => ("❌ Cập nhật thất bại",
                $"{DescribeAction(e.Action)} không thành công: {e.Detail ?? "không rõ lỗi"}.{Environment.NewLine}Dữ liệu thế giới không bị xoá; chờ admin kiểm tra.", Red),
            ServerEventKind.AdminMessage => ("📢 Thông báo từ admin", Escape(e.Detail ?? string.Empty), Blue),
            ServerEventKind.Test => ("🔔 Thử thông báo", "Webhook Discord của Conan Server Control đang hoạt động.", Grey),
            _ => (e.Kind.ToString(), e.Detail ?? string.Empty, Grey)
        };

        return new DiscordEmbed(title, body, color, e.Timestamp, context.ServerName);
    }

    /// <summary>Discord webhook JSON body for one or more embeds.</summary>
    public static string ToWebhookJson(string? username, IEnumerable<DiscordEmbed> embeds)
    {
        var payload = new WebhookPayload
        {
            Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim(),
            Embeds = embeds.Select(e => new WebhookEmbed
            {
                Title = Truncate(e.Title, 256),
                Description = Truncate(e.Description, 4096),
                Color = e.Color,
                Timestamp = e.Timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Footer = string.IsNullOrWhiteSpace(e.Footer) ? null : new WebhookFooter { Text = Truncate(e.Footer, 2048) }
            }).ToArray(),
            AllowedMentions = new WebhookAllowedMentions()
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string DescribeAction(string? action) => action switch
    {
        "Update server" => "Cập nhật server",
        "Update mods" or "Update selected mods" => "Cập nhật mod",
        "Update everything" => "Cập nhật server và mod",
        "Import local mod" => "Cài mod",
        "Replace local mod" => "Thay mod",
        null or "" => "Cập nhật",
        _ => action
    };

    private static string Player(ServerEvent e) => Escape(ConanPlayerLogParser.DisplayName(e.PlayerName ?? "?"));

    private static string Ready(DiscordEmbedContext context)
    {
        var ip = context.ServerIp ?? "?";
        return $"Direct IP: `{ip}` Port: `{context.GamePort}`{Environment.NewLine}Direct Connect: `{ip}:{context.GamePort}`";
    }

    private static string Stopped(ServerEvent e)
    {
        if (e.Unexpected)
        {
            return "Server tự tắt (không phải do lệnh dừng từ app).";
        }

        var action = e.Action ?? string.Empty;
        if (action.StartsWith("Update", StringComparison.Ordinal) || action.Contains("mod", StringComparison.OrdinalIgnoreCase))
        {
            return "Server tắt để cập nhật.";
        }

        return action.Contains("Restart", StringComparison.OrdinalIgnoreCase)
            ? "Server tắt để khởi động lại."
            : "Server đã dừng.";
    }

    private static string Crashed(ServerEvent e, DiscordEmbedContext context)
    {
        var code = e.ExitCode is null ? string.Empty : $" (exit code {e.ExitCode})";
        var next = context.RestartAfterCrash
            ? "App đang tự khởi động lại server."
            : "Tự khởi động lại đang tắt, chờ admin mở lại.";
        return $"Server dừng đột ngột{code}.{Environment.NewLine}{next}";
    }

    private static string ModsBody(ServerEvent e, DiscordEmbedContext context)
    {
        var sb = new StringBuilder();
        foreach (var item in e.Items.Take(15))
        {
            sb.Append("• ").AppendLine(Escape(item));
        }

        if (e.Items.Count > 15)
        {
            sb.AppendLine($"• ... và {e.Items.Count - 15} mod khác");
        }

        sb.AppendLine("Mọi người để Steam tải bản mod mới (Workshop) trước khi vào lại server.");
        sb.Append(Automation(context));
        return sb.ToString();
    }

    private static string UpdatedBody(ServerEvent e)
    {
        var sb = new StringBuilder($"{DescribeAction(e.Action)} đã xong.");
        if (!string.IsNullOrWhiteSpace(e.InstalledBuild))
        {
            sb.Append(Environment.NewLine).Append($"Build server: `{e.InstalledBuild}`");
        }

        if (e.Items.Count > 0)
        {
            sb.Append(Environment.NewLine).Append("Mod: ").Append(Escape(string.Join(", ", e.Items)));
        }

        return sb.ToString();
    }

    private static string Automation(DiscordEmbedContext context) => context.AutomationMode switch
    {
        AutomationMode.Automatic => "App sẽ tự cập nhật (sao lưu trước, chờ server trống người).",
        AutomationMode.Scheduled => "App sẽ tự cập nhật trong khung giờ bảo trì.",
        _ => "Admin sẽ cập nhật thủ công."
    };

    /// <summary>Stops player names from turning into Discord markdown or mentions.</summary>
    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '*' or '_' or '`' or '~' or '|' or '>' or '\\')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString().Replace("@", "@​", StringComparison.Ordinal);
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class WebhookPayload
    {
        [JsonPropertyName("username")] public string? Username { get; init; }

        [JsonPropertyName("embeds")] public WebhookEmbed[] Embeds { get; init; } = Array.Empty<WebhookEmbed>();

        [JsonPropertyName("allowed_mentions")] public WebhookAllowedMentions? AllowedMentions { get; init; }
    }

    private sealed class WebhookEmbed
    {
        [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;

        [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;

        [JsonPropertyName("color")] public int Color { get; init; }

        [JsonPropertyName("timestamp")] public string Timestamp { get; init; } = string.Empty;

        [JsonPropertyName("footer")] public WebhookFooter? Footer { get; init; }
    }

    private sealed class WebhookFooter
    {
        [JsonPropertyName("text")] public string Text { get; init; } = string.Empty;
    }

    private sealed class WebhookAllowedMentions
    {
        [JsonPropertyName("parse")] public string[] Parse { get; init; } = Array.Empty<string>();
    }
}

public static class DiscordWebhook
{
    /// <summary>Accepts only https Discord webhook URLs.</summary>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps
               && uri.Host is "discord.com" or "discordapp.com" or "canary.discord.com" or "ptb.discord.com"
               && uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal);
    }
}
