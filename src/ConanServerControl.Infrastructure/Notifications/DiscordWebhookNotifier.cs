using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Notifications;

public interface IDiscordNotifier
{
    /// <summary>Sends a test embed now. Returns null on success, otherwise a reason.</summary>
    Task<string?> SendTestAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Posts server lifecycle, player and update events to a Discord webhook, one embed per event,
/// through a single queue so Discord rate limits (HTTP 429) are respected. The webhook URL is a
/// secret (<see cref="Core.Settings.ProtectedSecrets.DiscordWebhookUrl"/>) and is never logged.
/// </summary>
public sealed class DiscordWebhookNotifier : BackgroundService, IDiscordNotifier
{
    public const string HttpClientName = "discord-webhook";
    private const int MaxAttempts = 5;

    private readonly ISettingsService _settings;
    private readonly IServerProcessManager _server;
    private readonly IServerEventBus _events;
    private readonly IActivityLog _activity;
    private readonly INetworkInfoService _network;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IAppPaths _paths;
    private readonly ILogger<DiscordWebhookNotifier> _logger;
    private readonly Channel<ServerEvent> _queue = Channel.CreateUnbounded<ServerEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _stateSync = new();
    private ServerRuntimeState? _lastState;

    public DiscordWebhookNotifier(
        ISettingsService settings,
        IServerProcessManager server,
        IServerEventBus events,
        IActivityLog activity,
        INetworkInfoService network,
        IHttpClientFactory httpFactory,
        IAppPaths paths,
        ILogger<DiscordWebhookNotifier> logger)
    {
        _settings = settings;
        _server = server;
        _events = events;
        _activity = activity;
        _network = network;
        _httpFactory = httpFactory;
        _paths = paths;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_stateSync)
        {
            _lastState = _server.State.Clone();
        }

        _server.StateChanged += OnStateChanged;
        _events.Published += OnEventPublished;
        return base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _server.StateChanged -= OnStateChanged;
        _events.Published -= OnEventPublished;
        _queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> SendTestAsync(CancellationToken cancellationToken = default)
    {
        var url = _settings.Secrets.DiscordWebhookUrl;
        if (!DiscordWebhook.IsValidUrl(url))
        {
            return "Chưa có webhook Discord hợp lệ.";
        }

        var embed = DiscordEmbedFactory.Create(new ServerEvent { Kind = ServerEventKind.Test }, await BuildContextAsync(ServerEventKind.Test, cancellationToken).ConfigureAwait(false));
        return await SendAsync(url!, embed, cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MigrateLegacyWebhookAsync(stoppingToken).ConfigureAwait(false);
        await foreach (var e in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                if (!ShouldSend(e, out var url))
                {
                    continue;
                }

                var embed = DiscordEmbedFactory.Create(e, await BuildContextAsync(e.Kind, stoppingToken).ConfigureAwait(false));
                var error = await SendAsync(url, embed, stoppingToken).ConfigureAwait(false);
                if (error is not null)
                {
                    _logger.LogWarning("Discord notification {Kind} was not delivered: {Error}", e.Kind, error);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Discord notification {Kind} failed.", e.Kind);
            }
        }
    }

    private void OnStateChanged(object? sender, ServerRuntimeState state)
    {
        IReadOnlyList<ServerEvent> events;
        lock (_stateSync)
        {
            events = ServerStateTransitions.Detect(_lastState, state, DateTimeOffset.UtcNow);
            _lastState = state;
        }

        foreach (var e in events)
        {
            _queue.Writer.TryWrite(e);
        }
    }

    private void OnEventPublished(object? sender, ServerEvent e) => _queue.Writer.TryWrite(e);

    private bool ShouldSend(ServerEvent e, out string url)
    {
        url = _settings.Secrets.DiscordWebhookUrl ?? string.Empty;
        var options = _settings.Current.Notifications;
        if (!options.DiscordEnabled || !DiscordWebhook.IsValidUrl(url))
        {
            return false;
        }

        return e.Kind switch
        {
            ServerEventKind.PlayerJoined or ServerEventKind.PlayerLeft or ServerEventKind.PlayerDisconnected => options.NotifyPlayerEvents,
            ServerEventKind.ServerUpdateAvailable or ServerEventKind.ModUpdateAvailable or ServerEventKind.ClientUpdateAvailable
                or ServerEventKind.UpdateStarted or ServerEventKind.UpdateCompleted or ServerEventKind.UpdateFailed => options.NotifyUpdates,
            _ => true
        };
    }

    private async Task<DiscordEmbedContext> BuildContextAsync(ServerEventKind kind, CancellationToken cancellationToken)
    {
        var settings = _settings.Current;
        string? estimate = null;
        if (kind == ServerEventKind.ServerStarting)
        {
            try
            {
                var entries = await _activity.GetRecentAsync(500, cancellationToken).ConfigureAwait(false);
                estimate = StartupTimeEstimator.Describe(StartupTimeEstimator.MeasureDurations(entries));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Could not read start-up history.");
            }
        }

        return new DiscordEmbedContext
        {
            ServerName = settings.Server.ServerName,
            ServerIp = _network.GetRadminVpnIPv4() ?? _network.GetLanIPv4(),
            GamePort = settings.Server.GamePort,
            StartupEstimate = estimate,
            RestartAfterCrash = settings.Advanced.RestartAfterCrash,
            AutomationMode = settings.Updates.AutomationMode
        };
    }

    private async Task<string?> SendAsync(string url, DiscordEmbed embed, CancellationToken cancellationToken)
    {
        var json = DiscordEmbedFactory.ToWebhookJson(_settings.Current.Notifications.DiscordUsername, new[] { embed });
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = _httpFactory.CreateClient(HttpClientName);
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response;
                try
                {
                    response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    if (attempt == MaxAttempts)
                    {
                        return $"không gửi được ({ex.Message})";
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5 * attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                using (response)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxAttempts)
                    {
                        var wait = await RetryAfterAsync(response, cancellationToken).ConfigureAwait(false);
                        await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if ((int)response.StatusCode >= 500 && attempt < MaxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5 * attempt), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized
                        ? $"Discord từ chối webhook (HTTP {(int)response.StatusCode}); webhook có thể đã bị xoá."
                        : $"Discord trả về HTTP {(int)response.StatusCode}.";
                }
            }

            return "Discord đang giới hạn tốc độ, đã bỏ qua tin này.";
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static async Task<TimeSpan> RetryAfterAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var seconds = 2.0;
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("retry_after", out var retry) && retry.TryGetDouble(out var value))
            {
                seconds = value;
            }
        }
        catch (JsonException)
        {
            if (response.Headers.RetryAfter?.Delta is { } delta)
            {
                seconds = delta.TotalSeconds;
            }
        }

        return TimeSpan.FromSeconds(Math.Clamp(seconds, 0.5, 60));
    }

    /// <summary>
    /// The operator first stored the webhook with PowerShell <c>ConvertFrom-SecureString</c>
    /// (hex DPAPI CurrentUser blob of a UTF-16 string) under data\secrets. Move it into the
    /// app's protected secrets once; the old file is left in place.
    /// </summary>
    private async Task MigrateLegacyWebhookAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || !string.IsNullOrEmpty(_settings.Secrets.DiscordWebhookUrl))
        {
            return;
        }

        var legacy = Path.Combine(_paths.DataDirectory, "secrets", "discord_webhook.dpapi");
        if (!File.Exists(legacy))
        {
            return;
        }

        try
        {
            var hex = (await File.ReadAllTextAsync(legacy, cancellationToken).ConfigureAwait(false)).Trim();
            var plain = Encoding.Unicode.GetString(
                ProtectedData.Unprotect(Convert.FromHexString(hex), optionalEntropy: null, DataProtectionScope.CurrentUser)).Trim();
            if (!DiscordWebhook.IsValidUrl(plain))
            {
                _logger.LogWarning("The stored Discord webhook file does not contain a Discord webhook URL; ignored.");
                return;
            }

            await _settings.UpdateSecretsAsync(s => s.DiscordWebhookUrl = plain, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Discord webhook imported into the protected secrets store.");
            await _activity.AddAsync("Notifications", "Discord webhook imported.", cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not import the stored Discord webhook.");
        }
    }
}
