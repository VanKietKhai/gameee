using ConanServerControl.Core.Diagnostics;

namespace ConanServerControl.Infrastructure.Diagnostics;

public sealed partial class IntegrationDiagnosticsService
{
    private const string RuntimeNotTestedOffline =
        "Server is not online, so runtime connectivity was not tested. This is not a configuration failure.";

    private static DiagnosticCheckResult CheckPorts(CheckContext context)
    {
        const string id = DiagnosticCheckIds.NetworkPorts;
        const string name = "Port configuration";
        var s = context.Settings;
        var game = s.Server.GamePort;
        var query = s.Server.QueryPort;
        var rcon = s.Rcon.Port;
        var web = s.WebAdmin.Port;
        var errors = new List<string>();
        var warnings = new List<string>();

        void Validate(string label, int port)
        {
            if (port is < 1 or > 65535)
            {
                errors.Add($"{label} port {port} is outside 1-65535.");
            }
            else if (port < 1024)
            {
                warnings.Add($"{label} port {port} is a privileged/well-known port.");
            }
        }

        Validate("Game", game);
        Validate("Query", query);
        if (s.Rcon.Enabled)
        {
            Validate("RCON", rcon);
        }

        if (s.WebAdmin.Enabled)
        {
            Validate("Web Admin", web);
        }

        // Conan uses the game port and game port + 1 (UDP).
        if (query == game || query == game + 1)
        {
            errors.Add($"Query port {query} collides with the game ports {game}/{game + 1} (UDP).");
        }

        if (s.Rcon.Enabled && s.WebAdmin.Enabled && rcon == web)
        {
            errors.Add($"RCON port and Web Admin port are both {rcon} (TCP).");
        }

        if (s.Rcon.Enabled && (rcon == game || rcon == game + 1 || rcon == query))
        {
            warnings.Add($"RCON port {rcon} (TCP) reuses a UDP game/query port number; allowed, but confusing for firewall rules.");
        }

        var facts = new Dictionary<string, string>
        {
            ["GamePort"] = game.ToString(),
            ["GamePortPlusOne"] = (game + 1).ToString(),
            ["QueryPort"] = query.ToString(),
            ["RconPort"] = rcon.ToString(),
            ["RconEnabled"] = s.Rcon.Enabled ? Yes : No,
            ["WebAdminEnabled"] = s.WebAdmin.Enabled ? Yes : No,
            ["WebAdminPort"] = web.ToString(),
            ["WebAdminBind"] = DiagnosticsService.ResolveBindAddress(s.WebAdmin)
        };

        if (errors.Count > 0)
        {
            return Result(id, DiagnosticCategories.Network, name, DiagnosticStatus.Fail,
                "Port configuration is invalid.",
                details: string.Join(Environment.NewLine, errors.Concat(warnings)),
                action: "Correct the ports in Settings.", facts: facts);
        }

        if (warnings.Count > 0)
        {
            return Result(id, DiagnosticCategories.Network, name, DiagnosticStatus.Warning,
                "CONFIG VALID with warnings (runtime not tested).",
                details: string.Join(Environment.NewLine, warnings), facts: facts);
        }

        return Result(id, DiagnosticCategories.Network, name, DiagnosticStatus.Pass,
            $"CONFIG VALID: game {game}/{game + 1} UDP, query {query} UDP, RCON {rcon} TCP. Firewall/router reachability not tested.",
            facts: facts);
    }

    private DiagnosticCheckResult CheckNetworkRuntime(CheckContext context)
    {
        const string id = DiagnosticCheckIds.NetworkRuntime;
        const string name = "Runtime port binding";
        var game = context.Settings.Server.GamePort;
        var facts = new Dictionary<string, string> { ["ServerStatus"] = context.ServerStatus.ToString() };

        if (!context.ServerOnline)
        {
            return Result(id, DiagnosticCategories.Network, name, DiagnosticStatus.NotTested,
                "RUNTIME NOT TESTED: " + RuntimeNotTestedOffline,
                DiagnosticEvidence.NotExercised, facts: facts);
        }

        var bound = game is >= 1 and <= 65535 && _isUdpPortInUse(game);
        facts["GamePortBound"] = bound ? Yes : No;
        return bound
            ? Result(id, DiagnosticCategories.Network, name, DiagnosticStatus.Pass,
                $"A local UDP listener exists on game port {game}. This does not prove external players can join.",
                DiagnosticEvidence.RuntimeObserved, facts: facts)
            : Result(id, DiagnosticCategories.Network, name, DiagnosticStatus.Warning,
                $"Server reports Online, but no local UDP listener was found on game port {game}.",
                DiagnosticEvidence.RuntimeObserved,
                action: "Check that the server is using the configured game port.", facts: facts);
    }

    /// <summary>
    /// Intended deployment: PRIVATE friends-only multiplayer over Radmin VPN. Public server-browser
    /// registration, public IP exposure, port forwarding and UPnP are not goals and are never configured.
    /// </summary>
    private DiagnosticCheckResult CheckPrivateVpn(CheckContext context)
    {
        const string id = DiagnosticCheckIds.NetworkPrivateVpn;
        const string name = "Private friends-only network (Radmin VPN)";
        var game = context.Settings.Server.GamePort;
        var radmin = _network.GetRadminVpnIPv4();
        var facts = new Dictionary<string, string>
        {
            ["DeploymentModel"] = "private friends-only over Radmin VPN",
            ["RadminVpnIPv4"] = radmin ?? "not detected",
            ["LanIPv4"] = _network.GetLanIPv4() ?? "not detected",
            ["PublicServerBrowserRegistration"] = "not required",
            ["PortForwardingOrUpnp"] = "not configured by this app"
        };

        if (radmin is null)
        {
            return Result(id, DiagnosticCategories.Network, name, DiagnosticStatus.Warning,
                "Radmin VPN adapter not detected, so friends cannot reach the private server.",
                DiagnosticEvidence.RuntimeObserved,
                details: "Public server-browser registration is not needed for this deployment (Conan's 'Autologin attempt failed' is expected).",
                action: "Start Radmin VPN and join the same Radmin network as your friends. No router port forwarding or public IP is needed.",
                facts: facts);
        }

        facts["FriendsDirectConnect"] = $"{radmin}:{game}";
        return Result(id, DiagnosticCategories.Network, name, DiagnosticStatus.Pass,
            $"Radmin VPN {radmin}: friends direct-connect to {radmin}:{game}. Public registration and port forwarding are not used.",
            DiagnosticEvidence.RuntimeObserved,
            details: "Reachability from friends' PCs is not tested here (firewall/VPN state on both ends).",
            facts: facts);
    }

    private static DiagnosticCheckResult CheckRconExposure(CheckContext context)
    {
        const string id = DiagnosticCheckIds.RconExposure;
        const string name = "RCON stays private";
        var rcon = context.Settings.Rcon;
        if (!rcon.Enabled)
        {
            return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.NotConfigured, "RCON is disabled.");
        }

        var facts = new Dictionary<string, string>
        {
            ["AppConnectsTo"] = $"127.0.0.1:{rcon.Port}",
            ["ConanListensOn"] = $"0.0.0.0:{rcon.Port} (all interfaces; observed live, not configurable in Conan)"
        };
        if (context.ServerOnline)
        {
            var listeners = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
                .Where(e => e.Port == rcon.Port).Select(e => e.Address.ToString()).Distinct().ToArray();
            facts["ObservedListeners"] = listeners.Length == 0 ? "none" : string.Join(", ", listeners);
        }

        return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.Warning,
            $"Conan's RCON (TCP {rcon.Port}) listens on all interfaces, so LAN and Radmin VPN peers can reach it. The app itself only connects to 127.0.0.1.",
            context.ServerOnline ? DiagnosticEvidence.RuntimeObserved : DiagnosticEvidence.ConfigurationChecked,
            action: $"Never port-forward TCP {rcon.Port}. Keep a strong RCON password. Optionally allow inbound TCP {rcon.Port} only from this PC with a Windows Firewall rule (manual; this app never changes the firewall).",
            facts: facts);
    }

    private static DiagnosticCheckResult CheckRconConfiguration(CheckContext context)
    {
        const string id = DiagnosticCheckIds.RconConfiguration;
        const string name = "RCON configuration";
        var rcon = context.Settings.Rcon;
        var facts = new Dictionary<string, string>
        {
            ["RconEnabled"] = rcon.Enabled ? Yes : No,
            ["RconPort"] = rcon.Port.ToString(),
            ["TimeoutSeconds"] = rcon.TimeoutSeconds.ToString()
        };

        if (!rcon.Enabled)
        {
            return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.NotConfigured,
                "RCON is disabled. Graceful stop falls back to closing the process.", facts: facts);
        }

        if (rcon.Port is < 1 or > 65535)
        {
            return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.Fail,
                $"RCON port {rcon.Port} is outside 1-65535.",
                action: "Correct the RCON port in Settings.", facts: facts);
        }

        if (rcon.TimeoutSeconds <= 0)
        {
            return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.Warning,
                "RCON timeout is not positive.", action: "Set an RCON timeout of a few seconds.", facts: facts);
        }

        return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.Pass,
            $"CONFIG VALID: RCON enabled on TCP {rcon.Port} (runtime not tested here).", facts: facts);
    }

    private DiagnosticCheckResult CheckRconPassword(CheckContext context)
    {
        const string id = DiagnosticCheckIds.RconPassword;
        const string name = "RCON password";
        var configured = !string.IsNullOrEmpty(_settings.Secrets.RconPassword);
        var facts = new Dictionary<string, string> { ["PasswordConfigured"] = configured ? Yes : No };

        if (!context.Settings.Rcon.Enabled)
        {
            return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.NotConfigured,
                $"RCON is disabled. Password configured: {(configured ? Yes : No)}.", facts: facts);
        }

        return configured
            ? Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.Pass,
                "Password configured: YES (value is never displayed or exported).", facts: facts)
            : Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.Warning,
                "Password configured: NO. Graceful stop and broadcasts via RCON are unavailable.",
                action: "Set the RCON password in Settings (write-only; stored with DPAPI). It must match the server's RconPassword.",
                facts: facts);
    }

    private async Task<DiagnosticCheckResult> CheckRconRuntimeAsync(CheckContext context, CancellationToken cancellationToken)
    {
        const string id = DiagnosticCheckIds.RconRuntime;
        const string name = "RCON connectivity";
        var rcon = context.Settings.Rcon;

        if (!context.ServerOnline)
        {
            return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.NotTested,
                "RUNTIME NOT TESTED: " + RuntimeNotTestedOffline, DiagnosticEvidence.NotExercised);
        }

        if (!rcon.Enabled || string.IsNullOrEmpty(_settings.Secrets.RconPassword))
        {
            return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.NotTested,
                "RCON is disabled or has no password; connectivity not tested.", DiagnosticEvidence.NotExercised);
        }

        // Read-only probe through the existing RCON service: one harmless command.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(rcon.TimeoutSeconds, 1, 30) + 1));
        try
        {
            await _rcon.SendCommandAsync("listplayers", timeout.Token).ConfigureAwait(false);
            return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.Pass,
                $"RCON responded on TCP {rcon.Port} (read-only listplayers).", DiagnosticEvidence.RuntimeObserved);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.Warning,
                $"RCON did not respond on TCP {rcon.Port} within the timeout.", DiagnosticEvidence.RuntimeObserved,
                action: "Check that the server's RCON port/password match Settings.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result(id, DiagnosticCategories.Rcon, name, DiagnosticStatus.Warning,
                $"RCON probe failed: {ex.Message}", DiagnosticEvidence.RuntimeObserved,
                action: "Check that the server's RCON port/password match Settings.");
        }
    }
}
