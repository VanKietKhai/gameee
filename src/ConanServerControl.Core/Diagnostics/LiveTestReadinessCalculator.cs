namespace ConanServerControl.Core.Diagnostics;

/// <summary>
/// Derives high-level M3 Task 4 readiness from individual diagnostic checks.
/// Readiness means "safe and sufficiently configured to attempt a guarded live test",
/// never "live verified". The standalone client is never required for server tests.
/// SteamCMD is optional: it is only needed when no valid existing Dedicated Server is configured.
/// </summary>
public static class LiveTestReadinessCalculator
{
    public const string ServerLiveTestName = "Server live test";
    public const string ClientCompatibilityTestName = "Client compatibility test";

    /// <summary>Checks that must exist and be Pass or Warning.</summary>
    private static readonly (string Id, string Reason)[] ServerRequiredUsable =
    [
        (DiagnosticCheckIds.ServerWorkspace, "Dedicated server workspace is not safe or not configured"),
        (DiagnosticCheckIds.AppBackupRoot, "Backup root is not valid")
    ];

    /// <summary>Checks that block only when they Fail.</summary>
    private static readonly (string Id, string Reason)[] ServerMustNotFail =
    [
        (DiagnosticCheckIds.AppDataDirectory, "Application data directory is not usable"),
        (DiagnosticCheckIds.AppSettings, "Application settings are invalid"),
        (DiagnosticCheckIds.ServerExecutable, "Dedicated server executable is invalid"),
        (DiagnosticCheckIds.ServerWorkingDirectory, "Dedicated server working directory is invalid"),
        (DiagnosticCheckIds.ServerInstallDirectory, "Dedicated server install directory is invalid"),
        (DiagnosticCheckIds.NetworkPorts, "Network port settings are invalid"),
        (DiagnosticCheckIds.RconConfiguration, "RCON settings are invalid")
    ];

    public static LiveTestReadiness CalculateServer(IReadOnlyList<DiagnosticCheckResult> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        var blockers = new List<string>();

        foreach (var (id, reason) in ServerRequiredUsable)
        {
            var check = Find(checks, id);
            if (check is null || !IsUsable(check.Status))
            {
                blockers.Add($"{reason} ({id}: {Describe(check)})");
            }
        }

        foreach (var (id, reason) in ServerMustNotFail)
        {
            var check = Find(checks, id);
            if (check is not null && check.Status == DiagnosticStatus.Fail)
            {
                blockers.Add($"{reason} ({id}: {check.Summary})");
            }
        }

        // Server source: a valid existing ConanSandboxServer.exe, OR SteamCMD available to install one.
        var serverExe = Find(checks, DiagnosticCheckIds.ServerExecutable);
        var steamCmd = Find(checks, DiagnosticCheckIds.SteamCmdExecutable);
        var existingServer = serverExe is { Status: DiagnosticStatus.Pass };
        var steamCmdUsable = steamCmd is not null && IsUsable(steamCmd.Status);
        if (!existingServer && !steamCmdUsable)
        {
            blockers.Add(
                "No dedicated server source: configure an existing ConanSandboxServer.exe installation, or install the optional SteamCMD to download one " +
                $"({DiagnosticCheckIds.ServerExecutable}: {Describe(serverExe)}; {DiagnosticCheckIds.SteamCmdExecutable}: {Describe(steamCmd)})");
        }

        var notes = new List<string>
        {
            "Configuration checks only. Server boot, mod load and player join have not been live verified."
        };

        if (!existingServer && steamCmdUsable && blockers.Count == 0)
        {
            notes.Add("Dedicated server is not installed at the configured path yet; the live test will install it into the configured workspace with SteamCMD.");
        }

        if (existingServer && !steamCmdUsable)
        {
            notes.Add("SteamCMD is not available. That is fine for a standalone setup: the existing server installation and Local mods do not need it. Workshop mods and automatic server install/update are unavailable.");
        }

        var ready = blockers.Count == 0;
        return new LiveTestReadiness
        {
            Name = ServerLiveTestName,
            IsReady = ready,
            Headline = ready ? "READY FOR SERVER LIVE TEST" : "NOT READY FOR SERVER LIVE TEST",
            Blockers = blockers,
            Notes = notes
        };
    }

    public static LiveTestReadiness CalculateClientCompatibility(
        IReadOnlyList<DiagnosticCheckResult> checks,
        LiveTestReadiness serverReadiness)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(serverReadiness);
        var blockers = new List<string>();

        if (!serverReadiness.IsReady)
        {
            blockers.Add("Server live test is not ready; client join/mod testing needs a running dedicated server first.");
        }

        var clientExe = Find(checks, DiagnosticCheckIds.ClientExecutable);
        if (clientExe is null || clientExe.Status != DiagnosticStatus.Pass)
        {
            blockers.Add($"Standalone client executable not available ({DiagnosticCheckIds.ClientExecutable}: {Describe(clientExe)})");
        }

        var clientMods = Find(checks, DiagnosticCheckIds.ClientMods);
        if (clientMods is not null && clientMods.Status == DiagnosticStatus.Fail)
        {
            blockers.Add($"Standalone client mod folder is invalid ({DiagnosticCheckIds.ClientMods}: {clientMods.Summary})");
        }

        var notes = new List<string>
        {
            "The standalone client is optional; it is not required for server installation tests.",
            "Client Workshop synchronization is not automatic. Server mods must be copied to the standalone client manually."
        };

        var parity = Find(checks, DiagnosticCheckIds.ClientModParity);
        if (parity is not null && parity.Status == DiagnosticStatus.Warning)
        {
            notes.Add(parity.Summary);
        }

        var ready = blockers.Count == 0;
        return new LiveTestReadiness
        {
            Name = ClientCompatibilityTestName,
            IsReady = ready,
            Headline = ready ? "READY FOR CLIENT COMPATIBILITY TEST" : "CLIENT COMPATIBILITY TEST NOT POSSIBLE YET",
            Blockers = blockers,
            Notes = notes
        };
    }

    private static bool IsUsable(DiagnosticStatus status) =>
        status is DiagnosticStatus.Pass or DiagnosticStatus.Warning;

    private static DiagnosticCheckResult? Find(IReadOnlyList<DiagnosticCheckResult> checks, string id) =>
        checks.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));

    private static string Describe(DiagnosticCheckResult? check) =>
        check is null ? "missing check" : $"{check.Status} - {check.Summary}";
}
