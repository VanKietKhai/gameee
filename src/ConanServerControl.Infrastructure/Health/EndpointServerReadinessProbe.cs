using System.Diagnostics;
using System.Net.NetworkInformation;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Health;

/// <summary>
/// Production readiness probe. Ready when the game UDP port is bound or, when
/// RCON is configured with a password, an RCON ping succeeds. RCON is optional;
/// a missing password does not block basic port-based readiness.
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

        if (context.GamePort > 0 && IsUdpPortInUse(context.GamePort))
        {
            return new ServerReadinessResult
            {
                IsReady = true,
                Reason = $"Game port {context.GamePort} is bound.",
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
                    Reason = "RCON ping succeeded.",
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
