using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Health;

public sealed class ServerHealthService : IServerHealthService
{
    private readonly IServerProcessManager _server;
    private readonly ISettingsService _settings;
    private readonly IRconService _rcon;
    private readonly ILogger<ServerHealthService> _logger;

    public ServerHealthService(
        IServerProcessManager server,
        ISettingsService settings,
        IRconService rcon,
        ILogger<ServerHealthService> logger)
    {
        _server = server;
        _settings = settings;
        _rcon = rcon;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        await _server.RefreshAsync(cancellationToken).ConfigureAwait(false);
        var state = _server.State;
        if (state.ProcessId is null || state.Status is ServerStatus.Offline or ServerStatus.Error)
        {
            return HealthCheckResult.ServerOffline;
        }

        if (state.Status is ServerStatus.Starting or ServerStatus.Restarting or ServerStatus.Updating or ServerStatus.Stopping)
        {
            return HealthCheckResult.ServerStarting;
        }

        if (state.Status is ServerStatus.Unresponsive)
        {
            return HealthCheckResult.ServerUnresponsive;
        }

        var port = _settings.Current.Server.GamePort;
        var portInUse = IsUdpPortInUse(port);
        if (!portInUse && state.Status is ServerStatus.Online)
        {
            _logger.LogDebug("Game port {Port} is not in use yet.", port);
        }

        if (_settings.Current.Rcon.Enabled && !string.IsNullOrEmpty(_settings.Secrets.RconPassword))
        {
            try
            {
                await _rcon.SendCommandAsync("ping", cancellationToken).ConfigureAwait(false);
                return HealthCheckResult.ServerOnline;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "RCON health check failed.");
                return portInUse ? HealthCheckResult.ProcessRunning : HealthCheckResult.ServerUnresponsive;
            }
        }

        return portInUse ? HealthCheckResult.ServerOnline : HealthCheckResult.ProcessRunning;
    }

    private static bool IsUdpPortInUse(int port)
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

public sealed class NetworkInfoService : INetworkInfoService
{
    public string? GetLanIPv4()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                // VPN adapters are reported separately (GetRadminVpnIPv4 / GetTailscaleIPv4).
                var adapter = ni.Name + " " + ni.Description;
                if (adapter.Contains("radmin", StringComparison.OrdinalIgnoreCase) ||
                    adapter.Contains("tailscale", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var address in ni.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(address.Address))
                    {
                        return address.Address.ToString();
                    }
                }
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    public string? GetRadminVpnIPv4()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    !(ni.Name + " " + ni.Description).Contains("radmin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var address in ni.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))
                    {
                        return address.Address.ToString();
                    }
                }
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    public string? GetTailscaleIPv4()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                var name = ni.Name + " " + ni.Description;
                if (name.Contains("tailscale", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("tailscale", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var address in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var text = address.Address.ToString();
                            if (text.StartsWith("100.", StringComparison.Ordinal))
                            {
                                return text;
                            }
                        }
                    }
                }

                foreach (var address in ni.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var text = address.Address.ToString();
                        if (text.StartsWith("100.", StringComparison.Ordinal))
                        {
                            return text;
                        }
                    }
                }
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    public string GetWebAdminUrl(string bindAddress, int port)
    {
        var host = bindAddress is "0.0.0.0" or "*" or "+" ? "127.0.0.1" : bindAddress;
        return $"http://{host}:{port}";
    }
}
