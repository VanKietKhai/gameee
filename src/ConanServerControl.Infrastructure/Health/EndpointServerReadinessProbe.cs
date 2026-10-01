using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Health;

/// <summary>
/// Production readiness probe. Ready when the game UDP port is bound or, when
/// RCON is configured with a password, an RCON ping succeeds. RCON is optional;
/// a missing password does not block basic port-based readiness.
/// <para>
/// M3 live finding: Conan binds the game port and answers RCON about 30 s before its world is
/// loaded (every log line is still on engine frame 0). When the server log has entries from the
/// current run and the newest one is still on frame 0, the server is reported NOT ready, whatever
/// the port/RCON signals say. Without such log evidence the port/RCON rules apply unchanged.
/// </para>
/// </summary>
public sealed class EndpointServerReadinessProbe : IServerReadinessProbe
{
    private readonly ISettingsService _settings;
    private readonly IRconService _rcon;
    private readonly ILogger<EndpointServerReadinessProbe> _logger;

    public EndpointServerReadinessProbe(
        ISettingsService settings,
        IRconService rcon,
        ILogger<EndpointServerReadinessProbe> logger)
    {
        _settings = settings;
        _rcon = rcon;
        _logger = logger;
    }

    public async Task<ServerReadinessResult> ProbeAsync(
        ServerReadinessContext context,
        CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        string? lastError = null;

        var tick = ReadWorldTick(context);
        if (tick is { Frame: 0 })
        {
            return new ServerReadinessResult
            {
                IsReady = false,
                Reason = "World is still loading (server log is on engine frame 0).",
                Elapsed = clock.Elapsed,
                LastError = "World still loading.",
                Health = HealthCheckResult.ServerStarting
            };
        }

        var tickNote = tick is null ? string.Empty : $" World is ticking (server log frame {tick.Value.Frame}).";

        if (context.GamePort > 0 && IsUdpPortInUse(context.GamePort))
        {
            return new ServerReadinessResult
            {
                IsReady = true,
                Reason = $"Game port {context.GamePort} is bound.{tickNote}",
                Elapsed = clock.Elapsed,
                Health = HealthCheckResult.ServerOnline
            };
        }

        lastError = $"Game port {context.GamePort} is not bound yet.";

        var rcon = _settings.Current.Rcon;
        var password = _settings.Secrets.RconPassword;
        if (rcon.Enabled && !string.IsNullOrEmpty(password))
        {
            try
            {
                await _rcon.SendCommandAsync("ping", cancellationToken).ConfigureAwait(false);
                return new ServerReadinessResult
                {
                    IsReady = true,
                    Reason = $"RCON ping succeeded.{tickNote}",
                    Elapsed = clock.Elapsed,
                    Health = HealthCheckResult.ServerOnline
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogDebug(ex, "RCON readiness ping failed.");
            }
        }

        return new ServerReadinessResult
        {
            IsReady = false,
            Reason = "No configured readiness endpoint has responded yet.",
            Elapsed = clock.Elapsed,
            LastError = lastError,
            Health = HealthCheckResult.ServerStarting
        };
    }

    private static readonly Regex LogLinePrefix = new(
        @"^\[(?<ts>\d{4}\.\d{2}\.\d{2}-\d{2}\.\d{2}\.\d{2}:\d{3})\]\[\s*(?<frame>\d+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Newest engine frame number logged by the CURRENT server run, or null when there is no
    /// such evidence (no install directory, no log, or only lines older than this start).
    /// Unreal log timestamps are UTC; the frame counter is GFrameCounter % 1000.
    /// </summary>
    internal (DateTime Timestamp, int Frame)? ReadWorldTick(ServerReadinessContext context)
    {
        var install = _settings.Current.ServerPaths.ServerInstallDirectory ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        if (string.IsNullOrWhiteSpace(install))
        {
            return null;
        }

        var logPath = Path.Combine(install, "ConanSandbox", "Saved", "Logs", "ConanSandbox.log");
        try
        {
            if (!File.Exists(logPath))
            {
                return null;
            }

            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            const int tailBytes = 64 * 1024;
            if (stream.Length > tailBytes)
            {
                stream.Seek(-tailBytes, SeekOrigin.End);
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var lines = reader.ReadToEnd().Split('\n');
            var notBefore = context.StartedAt.UtcDateTime.AddSeconds(-5);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var match = LogLinePrefix.Match(lines[i]);
                if (!match.Success)
                {
                    continue;
                }

                var timestamp = DateTime.ParseExact(match.Groups["ts"].Value, "yyyy.MM.dd-HH.mm.ss:fff",
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                return timestamp < notBefore
                    ? null
                    : (timestamp, int.Parse(match.Groups["frame"].Value, CultureInfo.InvariantCulture));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read the server log for readiness.");
        }

        return null;
    }

    internal static bool IsUdpPortInUse(int port)
    {
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
            return listeners.Any(e => e.Port == port);
        }
        catch
        {
            return false;
        }
    }
}
