namespace ConanServerControl.Core.Diagnostics;

/// <summary>
/// Outcome of a single integration diagnostic check.
/// </summary>
public enum DiagnosticStatus
{
    Pass = 0,
    Warning = 1,
    Fail = 2,
    NotConfigured = 3,
    NotTested = 4
}

/// <summary>
/// What a check actually looked at. Task 3 diagnostics never produce
/// <see cref="LiveVerified"/>; that value is reserved for the guarded Task 4 live harness.
/// A <see cref="DiagnosticStatus.Pass"/> with <see cref="ConfigurationChecked"/> means the
/// settings look usable, not that SteamCMD, Conan, a mod, or a client join worked.
/// </summary>
public enum DiagnosticEvidence
{
    /// <summary>Settings values were validated. Nothing on disk or at runtime was exercised.</summary>
    ConfigurationChecked = 0,

    /// <summary>Paths/files were inspected read-only (exists, size, timestamps). Nothing was executed or opened for write.</summary>
    FilesystemInspected = 1,

    /// <summary>Read from an existing record written earlier (for example backup metadata).</summary>
    RecordedResult = 2,

    /// <summary>A read-only runtime observation (for example a UDP listener exists). Not proof that players can join.</summary>
    RuntimeObserved = 3,

    /// <summary>The live behaviour has not been exercised. Deferred to the Task 4 live harness.</summary>
    NotExercised = 4,

    /// <summary>Reserved for Task 4. Never produced by read-only diagnostics.</summary>
    LiveVerified = 5
}

public static class DiagnosticCategories
{
    public const string System = "System";
    public const string SteamCmd = "SteamCMD";
    public const string DedicatedServer = "Dedicated Server";
    public const string StandaloneClient = "Standalone Client";
    public const string Network = "Network";
    public const string Rcon = "RCON";
    public const string World = "World / Saves";
    public const string Mods = "Mods";
    public const string Backups = "Backups";
    public const string LiveTests = "Live Tests";

    public static readonly IReadOnlyList<string> DisplayOrder =
    [
        System,
        SteamCmd,
        DedicatedServer,
        StandaloneClient,
        Network,
        Rcon,
        World,
        Mods,
        Backups,
        LiveTests
    ];
}

/// <summary>
/// Stable check identifiers. Readiness calculation and tests reference these.
/// </summary>
public static class DiagnosticCheckIds
{
    public const string AppDataDirectory = "app.data-directory";
    public const string AppSettings = "app.settings";
    public const string AppBackupRoot = "app.backup-root";
    public const string AppDiskSpace = "app.disk-space";

    public const string SteamCmdExecutable = "steamcmd.executable";
    public const string SteamCmdLive = "steamcmd.live";

    public const string ServerExecutable = "server.executable";
    public const string ServerWorkingDirectory = "server.working-directory";
    public const string ServerInstallDirectory = "server.install-directory";
    public const string ServerWorkspace = "server.workspace";
    public const string ServerSaveLocation = "server.save-location";
    public const string ServerLive = "server.live";

    public const string ClientRoot = "client.root";
    public const string ClientExecutable = "client.executable";
    public const string ClientMods = "client.mods";
    public const string ClientConfig = "client.config";
    public const string ClientModParity = "client.mod-parity";
    public const string ClientJoinLive = "client.join-live";

    public const string NetworkPorts = "network.ports";
    public const string NetworkRuntime = "network.runtime";

    public const string RconConfiguration = "rcon.configuration";
    public const string RconPassword = "rcon.password";
    public const string RconRuntime = "rcon.runtime";

    public const string WorldFiles = "world.files";

    public const string ModsDirectory = "mods.directory";
    public const string ModsModList = "mods.modlist";
    public const string ModsPakFiles = "mods.pak-files";
    public const string ModsOrdering = "mods.ordering";
    public const string ModsDuplicates = "mods.duplicates";
    public const string ModsWorkshopLive = "mods.workshop-live";

    public const string BackupsLastVerified = "backups.last-verified";
}

public sealed record DiagnosticCheckResult
{
    public required string Id { get; init; }

    public required string Category { get; init; }

    public required string Name { get; init; }

    public required DiagnosticStatus Status { get; init; }

    public DiagnosticEvidence Evidence { get; init; } = DiagnosticEvidence.ConfigurationChecked;

    public required string Summary { get; init; }

    /// <summary>Human-readable detail. Paths are allowed; secrets never.</summary>
    public string? Details { get; init; }

    public string? SuggestedAction { get; init; }

    public IReadOnlyDictionary<string, string> Facts { get; init; } = new Dictionary<string, string>();
}

public sealed record LiveTestReadiness
{
    public required string Name { get; init; }

    public required bool IsReady { get; init; }

    public required string Headline { get; init; }

    /// <summary>Check ids/reasons that block readiness.</summary>
    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();

    /// <summary>Non-blocking notes (for example client mods that must be copied manually).</summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record IntegrationDiagnosticsReport
{
    public const string CurrentReportVersion = "1";

    public string ReportVersion { get; init; } = CurrentReportVersion;

    public required DateTimeOffset CreatedAt { get; init; }

    public required string AppVersion { get; init; }

    public required string OsDescription { get; init; }

    public string? ServerInstallDirectory { get; init; }

    public string? StandaloneClientRoot { get; init; }

    public required IReadOnlyList<DiagnosticCheckResult> Checks { get; init; }

    public required LiveTestReadiness ServerLiveTest { get; init; }

    public required LiveTestReadiness ClientCompatibilityTest { get; init; }

    /// <summary>
    /// Always true for Task 3 reports: nothing in this report was live verified.
    /// </summary>
    public bool ConfigurationOnly { get; init; } = true;

    public DiagnosticCheckResult? Find(string id) =>
        Checks.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
}
