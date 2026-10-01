using System.Net.Sockets;
using System.Text;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Rcon;

/// <summary>
/// Source RCON client used by Conan Exiles dedicated servers.
/// Arbitrary commands are never accepted from unauthenticated Web Admin callers.
/// </summary>
public sealed class RconService : IRconService, IDisposable
{
    private const int ServerDataAuth = 3;
    private const int ServerDataAuthResponse = 2;
    private const int ServerDataExecCommand = 2;
    private const int ServerDataResponseValue = 0;

    private readonly ISettingsService _settings;
    private readonly ILogger<RconService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TcpClient? _client;
    private int _requestId = 1;

    public RconService(ISettingsService settings, ILogger<RconService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool IsConnected => _client?.Connected == true;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var password = _settings.Secrets.RconPassword;
        if (string.IsNullOrEmpty(password))
        {
            throw new UserFacingException(
                "RCON password is not configured",
                "Player warnings and graceful shutdown via RCON need an RCON password.",
                "Set the RCON password in Settings. Use the same value as the dedicated server.");
        }

        await DisconnectAsync().ConfigureAwait(false);
        var port = _settings.Current.Rcon.Port;
        var timeout = TimeSpan.FromSeconds(Math.Max(1, _settings.Current.Rcon.TimeoutSeconds));

        var client = new TcpClient();
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        await client.ConnectAsync("127.0.0.1", port, linked.Token).ConfigureAwait(false);
        _client = client;

        var authId = await SendPacketAsync(ServerDataAuth, password, cancellationToken).ConfigureAwait(false);
        var response = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);
        if (response.Id == -1 || (response.Type == ServerDataAuthResponse && response.Id != authId && response.Id != 0))
        {
            await DisconnectAsync().ConfigureAwait(false);
            throw new UserFacingException(
                "RCON authentication failed",
                "The dedicated server rejected the RCON password.",
                "Confirm the password matches the server configuration.");
        }
    }

    public Task DisconnectAsync()
    {
        _client?.Close();
        _client?.Dispose();
        _client = null;
        return Task.CompletedTask;
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command is required.", nameof(command));
        }

        if (command.Length > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "RCON command is too long.");
        }

        // A hung server can accept the TCP connection and never answer; every exchange is bounded so
        // callers such as graceful stop fall through to their own fallback instead of waiting forever.
        var timeout = TimeSpan.FromSeconds(Math.Max(1, _settings.Current.Rcon.TimeoutSeconds));
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"RCON is busy; no answer within {timeout.TotalSeconds:0} s.");
        }

        try
        {
            if (!IsConnected)
            {
                await ConnectAsync(linked.Token).ConfigureAwait(false);
            }

            await SendPacketAsync(ServerDataExecCommand, command, linked.Token).ConfigureAwait(false);
            var response = await ReadPacketAsync(linked.Token).ConfigureAwait(false);
            return response.Body;
        }
        catch (OperationCanceledException)
        {
            // A half-read reply would be misread by the next command: drop the connection.
            await DisconnectAsync().ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException($"RCON did not answer within {timeout.TotalSeconds:0} s.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task AnnounceAsync(string message, CancellationToken cancellationToken = default)
    {
        var sanitized = message.Replace("\"", "'");
        return SendCommandAsync($"broadcast {sanitized}", cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerInfo>> GetPlayersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendCommandAsync("listplayers", cancellationToken).ConfigureAwait(false);
            return ParsePlayers(response);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RCON listplayers is not available yet.");
            return Array.Empty<PlayerInfo>();
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _gate.Dispose();
    }

    private async Task<int> SendPacketAsync(int type, string body, CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("RCON is not connected.");
        }

        var id = Interlocked.Increment(ref _requestId);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var size = 4 + 4 + bodyBytes.Length + 2;
        var packet = new byte[4 + size];
        BitConverter.GetBytes(size).CopyTo(packet, 0);
        BitConverter.GetBytes(id).CopyTo(packet, 4);
        BitConverter.GetBytes(type).CopyTo(packet, 8);
        bodyBytes.CopyTo(packet, 12);
        packet[^2] = 0;
        packet[^1] = 0;
        await _client.GetStream().WriteAsync(packet, cancellationToken).ConfigureAwait(false);
        return id;
    }

    private async Task<(int Id, int Type, string Body)> ReadPacketAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("RCON is not connected.");
        }

        var stream = _client.GetStream();
        var sizeBuffer = await ReadExactAsync(stream, 4, cancellationToken).ConfigureAwait(false);
        var size = BitConverter.ToInt32(sizeBuffer, 0);
        if (size < 10 || size > 4096)
        {
            throw new InvalidOperationException("Invalid RCON packet size.");
        }

        var payload = await ReadExactAsync(stream, size, cancellationToken).ConfigureAwait(false);
        var id = BitConverter.ToInt32(payload, 0);
        var type = BitConverter.ToInt32(payload, 4);
        var body = Encoding.UTF8.GetString(payload, 8, size - 10);
        return (id, type, body);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("RCON connection closed.");
            }

            offset += read;
        }

        return buffer;
    }

    private static IReadOnlyList<PlayerInfo> ParsePlayers(string response)
    {
        if (string.IsNullOrWhiteSpace(response) ||
            response.Contains("no players", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<PlayerInfo>();
        }

        var players = new List<PlayerInfo>();
        foreach (var raw in response.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("idx", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(',');
            var name = parts.Length > 1 ? parts[1].Trim().Trim('"') : line;
            players.Add(new PlayerInfo { Name = name });
        }

        return players;
    }
}
