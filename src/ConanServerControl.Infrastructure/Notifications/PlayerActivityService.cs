using System.Text;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Notifications;
using ConanServerControl.Infrastructure.Health;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Notifications;

/// <summary>
/// Follows the dedicated server's ConanSandbox.log while the server is online and turns
/// join / quit / dropped-connection lines into <see cref="ServerEvent"/>s. Also keeps the
/// online player list in <see cref="ServerRuntimeState.Players"/>. Reads only; the log is
/// opened with full sharing so the server is never blocked.
/// </summary>
public sealed class PlayerActivityService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private const int MaxReadPerPoll = 4 * 1024 * 1024;

    private readonly IServerProcessManager _server;
    private readonly ISettingsService _settings;
    private readonly IRconService _rcon;
    private readonly IServerEventBus _events;
    private readonly ILogger<PlayerActivityService> _logger;
    private readonly List<string> _online = new();

    private long _position = -1;
    private ConanPlayerLogParser _parser = new();
    private Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _partialLine = new();

    public PlayerActivityService(
        IServerProcessManager server,
        ISettingsService settings,
        IRconService rcon,
        IServerEventBus events,
        ILogger<PlayerActivityService> logger)
    {
        _server = server;
        _settings = settings;
        _rcon = rcon;
        _events = events;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Already online when the app starts: skip the history and seed the list over RCON.
        if (IsOnline(_server.State.Status))
        {
            var path = LogPath();
            _position = path is not null && File.Exists(path) ? new FileInfo(path).Length : 0;
            await SeedFromRconAsync(stoppingToken).ConfigureAwait(false);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Poll();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Player log poll failed.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private void Poll()
    {
        if (!IsOnline(_server.State.Status))
        {
            if (_position >= 0 || _online.Count > 0)
            {
                // Server stopped or stopping: the "server stopped" notice covers everyone.
                _position = -1;
                _online.Clear();
                PublishPlayers();
            }

            return;
        }

        var path = LogPath();
        if (path is null || !File.Exists(path))
        {
            return;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (_position < 0 || stream.Length < _position)
        {
            // Came online while we were watching (a fresh log), or the log was rotated.
            ResetReader();
        }

        if (stream.Length == _position)
        {
            return;
        }

        stream.Seek(_position, SeekOrigin.Begin);
        var toRead = (int)Math.Min(MaxReadPerPoll, stream.Length - _position);
        var bytes = new byte[toRead];
        var read = stream.Read(bytes, 0, toRead);
        _position += read;
        var chars = new char[_decoder.GetCharCount(bytes, 0, read)];
        _decoder.GetChars(bytes, 0, read, chars, 0);
        _partialLine.Append(chars);

        var text = _partialLine.ToString();
        var lastBreak = text.LastIndexOf('\n');
        if (lastBreak < 0)
        {
            return;
        }

        _partialLine.Clear().Append(text, lastBreak + 1, text.Length - lastBreak - 1);
        var changed = false;
        foreach (var line in text[..lastBreak].Split('\n'))
        {
            foreach (var e in _parser.Feed(line.TrimEnd('\r')))
            {
                changed |= Apply(e);
            }
        }

        if (changed)
        {
            PublishPlayers();
        }
    }

    private bool Apply(PlayerLogEvent e)
    {
        switch (e.Kind)
        {
            case PlayerLogEventKind.Joined:
                if (!_online.Contains(e.Name))
                {
                    _online.Add(e.Name);
                }

                _events.Publish(new ServerEvent { Kind = ServerEventKind.PlayerJoined, PlayerName = e.Name });
                return true;

            case PlayerLogEventKind.Left:
                RemoveOnline(e.Name);
                _events.Publish(new ServerEvent { Kind = ServerEventKind.PlayerLeft, PlayerName = e.Name, Detail = e.Reason });
                return true;

            case PlayerLogEventKind.Dropped:
                RemoveOnline(e.Name);
                _events.Publish(new ServerEvent { Kind = ServerEventKind.PlayerDisconnected, PlayerName = e.Name, Detail = e.Reason });
                return true;

            default:
                return RemoveOnline(e.Name);
        }
    }

    /// <summary>
    /// Log names are Funcom ids ("name#1234"); names seeded over RCON may be formatted
    /// differently, so fall back to a match on the display name.
    /// </summary>
    private bool RemoveOnline(string name)
    {
        if (_online.Remove(name))
        {
            return true;
        }

        var display = ConanPlayerLogParser.DisplayName(name);
        return _online.RemoveAll(n => n.Contains(display, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    private void PublishPlayers()
    {
        _server.State.Players = _online
            .Select(n => new PlayerInfo { Name = ConanPlayerLogParser.DisplayName(n) })
            .ToArray();
        _server.State.PlayerCount = _online.Count;
    }

    private async Task SeedFromRconAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_settings.Secrets.RconPassword))
        {
            return;
        }

        try
        {
            var reply = await _rcon.SendCommandAsync("listplayers", cancellationToken).ConfigureAwait(false);
            foreach (var player in RconPlayerListParser.Parse(reply))
            {
                _online.Add(player.Name);
            }

            PublishPlayers();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not read the player list over RCON.");
        }
    }

    private void ResetReader()
    {
        _position = 0;
        _parser = new ConanPlayerLogParser();
        _decoder = Encoding.UTF8.GetDecoder();
        _partialLine.Clear();
        _online.Clear();
    }

    private string? LogPath() => ConanLogShutdownProbe.LogPathFor(_settings.Current.ServerPaths);

    private static bool IsOnline(ServerStatus status) => status is ServerStatus.Online or ServerStatus.Unresponsive;
}
