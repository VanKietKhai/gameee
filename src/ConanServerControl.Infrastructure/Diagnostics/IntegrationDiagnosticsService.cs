using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Diagnostics;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Settings;
using ConanServerControl.Infrastructure.Health;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Diagnostics;

/// <summary>
/// Read-only integration diagnostics (M3 Task 3). Never starts/stops/installs/downloads,
/// never takes the server action gate, never opens the live world database, and never
/// writes outside <see cref="ReportDirectory"/> (and only on explicit export).
/// </summary>
public sealed partial class IntegrationDiagnosticsService : IIntegrationDiagnosticsService
{
    private const string Yes = "YES";
    private const string No = "NO";

    private readonly IAppPaths _paths;
    private readonly ISettingsService _settings;
    private readonly ISteamCmdService _steamCmd;
    private readonly IServerProcessManager _server;
    private readonly IRconService _rcon;
    private readonly INetworkInfoService _network;
    private readonly ILogger<IntegrationDiagnosticsService> _logger;
    private readonly Func<int, bool> _isUdpPortInUse;

    public IntegrationDiagnosticsService(
        IAppPaths paths,
        ISettingsService settings,
        ISteamCmdService steamCmd,
        IServerProcessManager server,
        IRconService rcon,
        INetworkInfoService network,
        ILogger<IntegrationDiagnosticsService> logger)
        : this(paths, settings, steamCmd, server, rcon, network, logger, EndpointServerReadinessProbe.IsUdpPortInUse)
    {
    }

    internal IntegrationDiagnosticsService(
        IAppPaths paths,
        ISettingsService settings,
        ISteamCmdService steamCmd,
        IServerProcessManager server,
        IRconService rcon,
        INetworkInfoService network,
        ILogger<IntegrationDiagnosticsService> logger,
        Func<int, bool> isUdpPortInUse)
    {
        _network = network;
        _paths = paths;
        _settings = settings;
        _steamCmd = steamCmd;
        _server = server;
        _rcon = rcon;
        _logger = logger;
        _isUdpPortInUse = isUdpPortInUse;
    }

    public string ReportDirectory => Path.Combine(_paths.DataDirectory, "diagnostics");

    public async Task<IntegrationDiagnosticsReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;
        var serverStatus = _server.State.Status;
        var context = new CheckContext(settings, serverStatus);

        var checks = await Task.Run(() => RunFileSystemChecks(context, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
        checks.Add(await SafeAsync(
                DiagnosticCheckIds.RconRuntime,
                DiagnosticCategories.Rcon,
                "RCON connectivity",
                () => CheckRconRuntimeAsync(context, cancellationToken))
            .ConfigureAwait(false));

        var ordered = DiagnosticReportFormatter.OrderedCategories(checks)
            .SelectMany(category => checks.Where(c => c.Category == category))
            .ToArray();

        var serverReadiness = LiveTestReadinessCalculator.CalculateServer(ordered);
        var clientReadiness = LiveTestReadinessCalculator.CalculateClientCompatibility(ordered, serverReadiness);
        var redactor = CreateRedactor();

        var report = new IntegrationDiagnosticsReport
        {
            CreatedAt = DateTimeOffset.Now,
            AppVersion = typeof(IntegrationDiagnosticsService).Assembly.GetName().Version?.ToString() ?? "unknown",
            OsDescription = RuntimeInformation.OSDescription,
            ServerInstallDirectory = context.InstallDirectory,
            StandaloneClientRoot = NullIfBlank(settings.Client.RootDirectory),
            Checks = ordered,
            ServerLiveTest = serverReadiness,
            ClientCompatibilityTest = clientReadiness
        };

        // Displayed reports are redacted too, not only exported ones.
        return redactor.Redact(report);
    }

    public async Task<DiagnosticReportExport> ExportAsync(
        IntegrationDiagnosticsReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        var redactor = CreateRedactor();
        var json = DiagnosticReportFormatter.ToJson(report, redactor);
        var text = DiagnosticReportFormatter.ToText(report, redactor);

        Directory.CreateDirectory(ReportDirectory);
        var stem = $"integration-{report.CreatedAt:yyyyMMdd-HHmmss}";
        var jsonPath = UniquePath(stem, ".json");
        var textPath = Path.ChangeExtension(jsonPath, ".md");

        await WriteNewFileAsync(jsonPath, json, cancellationToken).ConfigureAwait(false);
        await WriteNewFileAsync(textPath, text, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Integration diagnostics report exported to {Path}", jsonPath);
        return new DiagnosticReportExport(jsonPath, textPath);
    }

    private List<DiagnosticCheckResult> RunFileSystemChecks(CheckContext context, CancellationToken cancellationToken)
    {
        var checks = new List<DiagnosticCheckResult>();

        void Add(string id, string category, string name, Func<DiagnosticCheckResult> check)
        {
            cancellationToken.ThrowIfCancellationRequested();
            checks.Add(Safe(id, category, name, check));
        }

        Add(DiagnosticCheckIds.AppDataDirectory, DiagnosticCategories.System, "Application data directory", CheckDataDirectory);
        Add(DiagnosticCheckIds.AppSettings, DiagnosticCategories.System, "Application settings", () => CheckSettings(context));
        Add(DiagnosticCheckIds.AppBackupRoot, DiagnosticCategories.System, "Backup root", () => CheckBackupRoot(context));
        Add(DiagnosticCheckIds.AppDiskSpace, DiagnosticCategories.System, "Free disk space", () => CheckDiskSpace(context));

        Add(DiagnosticCheckIds.SteamCmdExecutable, DiagnosticCategories.SteamCmd, "SteamCMD executable", () => CheckSteamCmd(context));
        Add(DiagnosticCheckIds.SteamCmdLive, DiagnosticCategories.SteamCmd, "SteamCMD live download", CheckSteamCmdLive);

        Add(DiagnosticCheckIds.ServerExecutable, DiagnosticCategories.DedicatedServer, "Dedicated server executable", () => CheckServerExecutable(context));
        Add(DiagnosticCheckIds.ServerWorkingDirectory, DiagnosticCategories.DedicatedServer, "Server working directory", () => CheckServerWorkingDirectory(context));
        Add(DiagnosticCheckIds.ServerInstallDirectory, DiagnosticCategories.DedicatedServer, "Server install directory", () => CheckServerInstallDirectory(context));
        Add(DiagnosticCheckIds.ServerWorkspace, DiagnosticCategories.DedicatedServer, "Server workspace safety", () => CheckServerWorkspace(context));
        Add(DiagnosticCheckIds.ServerSaveLocation, DiagnosticCategories.DedicatedServer, "Server save location", () => CheckServerSaveLocation(context));
        Add(DiagnosticCheckIds.ServerLive, DiagnosticCategories.DedicatedServer, "Dedicated server live boot", () => CheckServerLive(context));

        Add(DiagnosticCheckIds.ClientRoot, DiagnosticCategories.StandaloneClient, "Standalone client root", () => CheckClientRoot(context));
        Add(DiagnosticCheckIds.ClientExecutable, DiagnosticCategories.StandaloneClient, "Standalone client executable", () => CheckClientExecutable(context));
        Add(DiagnosticCheckIds.ClientMods, DiagnosticCategories.StandaloneClient, "Standalone client mods", () => CheckClientMods(context));
        Add(DiagnosticCheckIds.ClientConfig, DiagnosticCategories.StandaloneClient, "Standalone client Saved/Config", () => CheckClientConfig(context));
        Add(DiagnosticCheckIds.ClientModParity, DiagnosticCategories.StandaloneClient, "Client vs server mod list", () => CheckClientModParity(context));
        Add(DiagnosticCheckIds.ClientJoinLive, DiagnosticCategories.StandaloneClient, "Client join live test", CheckClientJoinLive);

        Add(DiagnosticCheckIds.NetworkPorts, DiagnosticCategories.Network, "Port configuration", () => CheckPorts(context));
        Add(DiagnosticCheckIds.NetworkRuntime, DiagnosticCategories.Network, "Runtime port binding", () => CheckNetworkRuntime(context));
        Add(DiagnosticCheckIds.NetworkPrivateVpn, DiagnosticCategories.Network, "Private friends-only network (Radmin VPN)", () => CheckPrivateVpn(context));

        Add(DiagnosticCheckIds.RconConfiguration, DiagnosticCategories.Rcon, "RCON configuration", () => CheckRconConfiguration(context));
        Add(DiagnosticCheckIds.RconPassword, DiagnosticCategories.Rcon, "RCON password", () => CheckRconPassword(context));
        Add(DiagnosticCheckIds.RconExposure, DiagnosticCategories.Rcon, "RCON stays private", () => CheckRconExposure(context));

        Add(DiagnosticCheckIds.WorldFiles, DiagnosticCategories.World, "World database files", () => CheckWorldFiles(context));

        Add(DiagnosticCheckIds.ModsSources, DiagnosticCategories.Mods, "Mod sources (Local / Workshop)", () => CheckModSources(context));
        Add(DiagnosticCheckIds.ModsDirectory, DiagnosticCategories.Mods, "Server Mods directory", () => CheckModsDirectory(context));
        Add(DiagnosticCheckIds.ModsModList, DiagnosticCategories.Mods, "Server modlist.txt", () => CheckModList(context));
        Add(DiagnosticCheckIds.ModsPakFiles, DiagnosticCategories.Mods, "Server .pak files", () => CheckPakFiles(context));
        Add(DiagnosticCheckIds.ModsOrdering, DiagnosticCategories.Mods, "modlist.txt matches application state", () => CheckModOrdering(context));
        Add(DiagnosticCheckIds.ModsDuplicates, DiagnosticCategories.Mods, "Duplicate / conflicting mod entries", () => CheckModDuplicates(context));
        Add(DiagnosticCheckIds.ModsWorkshopLive, DiagnosticCategories.Mods, "Workshop download / mod load", CheckWorkshopLive);

        Add(DiagnosticCheckIds.BackupsLastVerified, DiagnosticCategories.Backups, "Last verified backup", CheckBackups);

        return checks;
    }

    private DiagnosticCheckResult Safe(string id, string category, string name, Func<DiagnosticCheckResult> check)
    {
        try
        {
            return check();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Integration diagnostic check {Id} threw.", id);
            return Result(id, category, name, DiagnosticStatus.Fail, $"Check could not complete: {ex.Message}");
        }
    }

    private async Task<DiagnosticCheckResult> SafeAsync(
        string id,
        string category,
        string name,
        Func<Task<DiagnosticCheckResult>> check)
    {
        try
        {
            return await check().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Integration diagnostic check {Id} threw.", id);
            return Result(id, category, name, DiagnosticStatus.Fail, $"Check could not complete: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- System

    private DiagnosticCheckResult CheckDataDirectory()
    {
        const string id = DiagnosticCheckIds.AppDataDirectory;
        const string name = "Application data directory";
        var facts = new Dictionary<string, string>
        {
            ["DataDirectory"] = _paths.DataDirectory,
            ["LogsDirectory"] = _paths.LogsDirectory,
            ["SettingsFile"] = _paths.SettingsFilePath,
            ["StagingDirectory"] = _paths.StagingDirectory,
            ["OperatingSystem"] = RuntimeInformation.OSDescription,
            ["Runtime"] = RuntimeInformation.FrameworkDescription
        };

        if (!Directory.Exists(_paths.DataDirectory))
        {
            return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Fail,
                "Application data directory does not exist.",
                DiagnosticEvidence.FilesystemInspected,
                details: _paths.DataDirectory,
                action: "Restart Conan Server Control so it can create its data directory.",
                facts: facts);
        }

        return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Pass,
            "Application data directory exists.",
            DiagnosticEvidence.FilesystemInspected,
            details: _paths.DataDirectory,
            facts: facts);
    }

    private DiagnosticCheckResult CheckSettings(CheckContext context)
    {
        const string id = DiagnosticCheckIds.AppSettings;
        const string name = "Application settings";
        var s = context.Settings;
        var problems = new List<string>();
        var warnings = new List<string>();

        if (s.Advanced.StartupReadyTimeoutSeconds <= 0)
        {
            problems.Add("Startup readiness timeout must be greater than 0 seconds.");
        }

        if (s.Advanced.ReadinessPollIntervalMilliseconds <= 0)
        {
            problems.Add("Readiness poll interval must be greater than 0 ms.");
        }

        if (s.Advanced.GracefulStopTimeoutSeconds <= 0 || s.Advanced.UnacknowledgedStopTimeoutSeconds <= 0 ||
            s.Advanced.ForceStopTimeoutSeconds <= 0)
        {
            problems.Add("Stop timeouts must be greater than 0 seconds.");
        }

        if (!File.Exists(_paths.SettingsFilePath))
        {
            warnings.Add("settings.json has not been saved yet (defaults in use).");
        }

        if (!s.IsSetupComplete)
        {
            warnings.Add("Setup has not been completed in Settings.");
        }

        var facts = new Dictionary<string, string>
        {
            ["SettingsVersion"] = s.SettingsVersion.ToString(),
            ["SetupComplete"] = s.IsSetupComplete ? Yes : No,
            ["StartupReadyTimeoutSeconds"] = s.Advanced.StartupReadyTimeoutSeconds.ToString(),
            ["BackupBeforeServerUpdate"] = s.Backups.BackupBeforeServerUpdate ? Yes : No,
            ["BackupBeforeModUpdate"] = s.Backups.BackupBeforeModUpdate ? Yes : No,
            ["WebAdminPasswordConfigured"] = string.IsNullOrEmpty(_settings.Secrets.WebAdminPasswordHash) ? No : Yes
        };

        if (problems.Count > 0)
        {
            return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Fail,
                "Some application settings are invalid.",
                details: string.Join(Environment.NewLine, problems.Concat(warnings)),
                action: "Correct the values in Settings and save.",
                facts: facts);
        }

        if (warnings.Count > 0)
        {
            return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Warning,
                "Settings are usable but incomplete.",
                details: string.Join(Environment.NewLine, warnings),
                action: "Review Settings and save once.",
                facts: facts);
        }

        return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Pass,
            "Required application settings are valid.", facts: facts);
    }

    private DiagnosticCheckResult CheckBackupRoot(CheckContext context)
    {
        const string id = DiagnosticCheckIds.AppBackupRoot;
        const string name = "Backup root";
        var root = _paths.BackupsDirectory;
        var facts = new Dictionary<string, string> { ["BackupRoot"] = root };

        if (!Core.Validation.PathValidator.IsSafeAbsolutePath(root))
        {
            return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Fail,
                "Backup root is not a safe absolute path.", details: root,
                action: "Use an absolute backup folder without '..' segments.", facts: facts);
        }

        var install = context.InstallDirectory;
        if (install is not null && Core.Validation.PathValidator.IsSafeAbsolutePath(install))
        {
            var saved = Path.Combine(install, "ConanSandbox", "Saved");
            if (Core.Validation.PathValidator.IsUnderRoot(root, saved))
            {
                return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Fail,
                    "Backup root is inside the live server Saved folder.",
                    details: root,
                    action: "Move backups outside the dedicated server install; a restore would copy backups into themselves.",
                    facts: facts);
            }

            if (Core.Validation.PathValidator.IsUnderRoot(install, root))
            {
                return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Fail,
                    "The dedicated server install is inside the backup root.",
                    details: $"Install: {install}{Environment.NewLine}Backups: {root}",
                    action: "Keep the server install and the backup folder separate.",
                    facts: facts);
            }

            if (Core.Validation.PathValidator.IsUnderRoot(root, install))
            {
                return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Warning,
                    "Backup root is inside the dedicated server install folder.",
                    details: root,
                    action: "Prefer a backup folder outside the server install so server reinstalls cannot affect backups.",
                    facts: facts);
            }
        }

        if (!Directory.Exists(root))
        {
            return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Warning,
                "Backup root does not exist yet; it is created by the first backup.",
                DiagnosticEvidence.FilesystemInspected, details: root, facts: facts);
        }

        return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Pass,
            "Backup root is valid and separate from the live world.",
            DiagnosticEvidence.FilesystemInspected, details: root, facts: facts);
    }

    private DiagnosticCheckResult CheckDiskSpace(CheckContext context)
    {
        const string id = DiagnosticCheckIds.AppDiskSpace;
        const string name = "Free disk space";
        const long installMinimum = 10L * 1024 * 1024 * 1024;
        const long backupMinimum = 2L * 1024 * 1024 * 1024;

        var facts = new Dictionary<string, string>();
        var warnings = new List<string>();
        var measured = false;

        void Measure(string label, string? path, long minimum)
        {
            var free = TryGetFreeBytes(path);
            if (free is null)
            {
                return;
            }

            measured = true;
            facts[$"{label}FreeGB"] = (free.Value / (1024d * 1024 * 1024)).ToString("0.0");
            if (free.Value < minimum)
            {
                warnings.Add($"{label} volume has less than {minimum / (1024 * 1024 * 1024)} GB free.");
            }
        }

        Measure("Install", context.InstallDirectory, installMinimum);
        Measure("Backups", _paths.BackupsDirectory, backupMinimum);
        Measure("Data", _paths.DataDirectory, backupMinimum);

        if (!measured)
        {
            return Result(id, DiagnosticCategories.System, name, DiagnosticStatus.NotTested,
                "Free disk space could not be determined.", DiagnosticEvidence.FilesystemInspected, facts: facts);
        }

        return warnings.Count > 0
            ? Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Warning,
                "Low free disk space.", DiagnosticEvidence.FilesystemInspected,
                details: string.Join(Environment.NewLine, warnings),
                action: "Free space before installing the server or creating backups.", facts: facts)
            : Result(id, DiagnosticCategories.System, name, DiagnosticStatus.Pass,
                "Enough free disk space on the install and backup volumes.",
                DiagnosticEvidence.FilesystemInspected, facts: facts);
    }

    // ---------------------------------------------------------------- SteamCMD

    private DiagnosticCheckResult CheckSteamCmd(CheckContext context)
    {
        const string id = DiagnosticCheckIds.SteamCmdExecutable;
        const string name = "SteamCMD executable";
        var s = context.Settings.SteamCmd;
        // Settings load fills in the application default folder, so only a different
        // folder counts as explicitly configured.
        var configured = !string.IsNullOrWhiteSpace(s.InstallDirectory) &&
                         !string.Equals(
                             Path.TrimEndingDirectorySeparator(s.InstallDirectory.Trim()),
                             Path.TrimEndingDirectorySeparator(_paths.SteamCmdDefaultDirectory),
                             StringComparison.OrdinalIgnoreCase);
        var exe = _steamCmd.ExecutablePath;
        var facts = new Dictionary<string, string>
        {
            ["Source"] = configured ? "configured" : "application default",
            ["ExecutablePath"] = exe ?? string.Empty,
            ["AnonymousLogin"] = s.UseAnonymousLogin ? Yes : No,
            ["SteamUsernameConfigured"] = string.IsNullOrWhiteSpace(s.SteamUsername) ? No : Yes,
            ["SteamPasswordConfigured"] = string.IsNullOrEmpty(_settings.Secrets.SteamPassword) ? No : Yes,
            ["Executed"] = No
        };

        if (configured && !Core.Validation.PathValidator.IsSafeAbsolutePath(s.InstallDirectory))
        {
            return Result(id, DiagnosticCategories.SteamCmd, name, DiagnosticStatus.Fail,
                "Configured SteamCMD folder is not a safe absolute path.",
                details: s.InstallDirectory,
                action: "Choose an absolute SteamCMD folder in Settings.", facts: facts);
        }

        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            return configured
                ? Result(id, DiagnosticCategories.SteamCmd, name, DiagnosticStatus.Fail,
                    "steamcmd.exe was not found in the configured SteamCMD folder.",
                    DiagnosticEvidence.FilesystemInspected,
                    details: exe,
                    action: "Correct the SteamCMD folder in Settings, clear it, or use Install SteamCMD. SteamCMD is optional.", facts: facts)
                : Result(id, DiagnosticCategories.SteamCmd, name, DiagnosticStatus.NotConfigured,
                    "SteamCMD is not installed. It is optional: only Workshop mods and automatic server install/update need it.",
                    DiagnosticEvidence.FilesystemInspected,
                    details: exe,
                    action: "Not needed for an existing server installation and Local mods. Use Install SteamCMD only if you want Workshop mods or automatic server updates.", facts: facts);
        }

        var directory = Path.GetDirectoryName(exe);
        facts["WorkingDirectory"] = directory ?? string.Empty;
        if (new FileInfo(exe).Length == 0)
        {
            return Result(id, DiagnosticCategories.SteamCmd, name, DiagnosticStatus.Fail,
                "steamcmd.exe is a zero-byte file.", DiagnosticEvidence.FilesystemInspected,
                details: exe, action: "Delete the SteamCMD folder and reinstall SteamCMD.", facts: facts);
        }

        if (!s.UseAnonymousLogin)
        {
            return Result(id, DiagnosticCategories.SteamCmd, name, DiagnosticStatus.Warning,
                "steamcmd.exe found, but anonymous login is disabled. Not executed.",
                DiagnosticEvidence.FilesystemInspected,
                details: exe,
                action: "The dedicated server (app 443030) downloads anonymously; enable anonymous login unless you need otherwise.",
                facts: facts);
        }

        return Result(id, DiagnosticCategories.SteamCmd, name, DiagnosticStatus.Pass,
            "steamcmd.exe found. Configured but not live-tested (not executed).",
            DiagnosticEvidence.FilesystemInspected, details: exe, facts: facts);
    }

    private static DiagnosticCheckResult CheckSteamCmdLive() =>
        Result(DiagnosticCheckIds.SteamCmdLive, DiagnosticCategories.SteamCmd, "SteamCMD live download",
            DiagnosticStatus.NotTested,
            "Optional SteamCMD integration: a SteamCMD download has not been verified in a live run.",
            DiagnosticEvidence.NotExercised,
            details: "Diagnostics never execute SteamCMD or app_update. SteamCMD is optional for a standalone setup.",
            action: "Exercised by the guarded M3 Task 4 live test.");

    // ---------------------------------------------------------------- Backups

    private DiagnosticCheckResult CheckBackups()
    {
        const string id = DiagnosticCheckIds.BackupsLastVerified;
        const string name = "Last verified backup";
        var records = ReadBackupRecords();
        var facts = new Dictionary<string, string>
        {
            ["BackupRoot"] = _paths.BackupsDirectory,
            ["BackupCount"] = records.Count.ToString()
        };

        if (records.Count == 0)
        {
            return Result(id, DiagnosticCategories.Backups, name, DiagnosticStatus.NotTested,
                "No backup has been created yet.", DiagnosticEvidence.RecordedResult,
                action: "Create a backup with BACKUP NOW once the server install is configured.", facts: facts);
        }

        var latest = records.OrderByDescending(r => r.CreatedAt).First();
        var lastVerified = records
            .Where(IsVerified)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();

        facts["LastBackupId"] = latest.Id;
        facts["LastBackupAt"] = latest.CreatedAt.ToString("u");
        facts["LastBackupVerified"] = IsVerified(latest) ? Yes : No;
        facts["LastVerificationDetail"] = latest.VerificationDetail ?? string.Empty;
        if (lastVerified is not null)
        {
            facts["LastVerifiedBackupId"] = lastVerified.Id;
            facts["LastVerifiedAt"] = (lastVerified.VerifiedAt ?? lastVerified.CreatedAt).ToString("u");
            facts["LastVerifiedWorldType"] = lastVerified.WorldType ?? string.Empty;
            facts["LastVerifiedMainDb"] = lastVerified.MainDbFileName ?? string.Empty;
        }

        if (IsVerified(latest))
        {
            return Result(id, DiagnosticCategories.Backups, name, DiagnosticStatus.Pass,
                $"Latest backup {latest.Id} passed hash and SQLite quick_check verification.",
                DiagnosticEvidence.RecordedResult,
                details: latest.VerificationDetail, facts: facts);
        }

        if (lastVerified is not null)
        {
            return Result(id, DiagnosticCategories.Backups, name, DiagnosticStatus.Warning,
                $"Latest backup {latest.Id} is not verified; last verified backup is {lastVerified.Id}.",
                DiagnosticEvidence.RecordedResult,
                details: latest.VerificationDetail,
                action: "Create a new backup with the server stopped and check the verification detail.", facts: facts);
        }

        return Result(id, DiagnosticCategories.Backups, name, DiagnosticStatus.Fail,
            "No backup has passed verification.",
            DiagnosticEvidence.RecordedResult,
            details: latest.VerificationDetail,
            action: "Create a backup with the server stopped. Do not run live tests without a verified backup of any world you care about.",
            facts: facts);
    }

    private static bool IsVerified(BackupRecord record) =>
        record.Succeeded && record.HashesVerified && record.SqliteVerified;

    /// <summary>Reads backup metadata without creating or modifying anything.</summary>
    private List<BackupRecord> ReadBackupRecords()
    {
        var records = new List<BackupRecord>();
        if (!Directory.Exists(_paths.BackupsDirectory))
        {
            return records;
        }

        foreach (var dir in Directory.EnumerateDirectories(_paths.BackupsDirectory))
        {
            var metadata = Path.Combine(dir, "metadata.json");
            if (!File.Exists(metadata))
            {
                continue;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<BackupRecord>(File.ReadAllText(metadata));
                if (parsed is not null)
                {
                    if (string.IsNullOrWhiteSpace(parsed.Id))
                    {
                        parsed.Id = Path.GetFileName(dir);
                    }

                    records.Add(parsed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read backup metadata in {Dir}", dir);
            }
        }

        return records;
    }

    // ---------------------------------------------------------------- Helpers

    private DiagnosticReportRedactor CreateRedactor()
    {
        var secrets = _settings.Secrets;
        var known = new List<string?>
        {
            secrets.RconPassword,
            secrets.ServerPassword,
            secrets.AdminPassword,
            secrets.SteamPassword,
            secrets.WebAdminPasswordHash,
            _settings.Current.SteamCmd.SteamUsername
        };

        // The protected secrets blob itself must never leak into a report either.
        try
        {
            if (File.Exists(_paths.SecretsFilePath))
            {
                known.Add(File.ReadAllText(_paths.SecretsFilePath).Trim());
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read the protected secrets payload for redaction.");
        }

        return new DiagnosticReportRedactor(known);
    }

    private string UniquePath(string stem, string extension)
    {
        var candidate = Path.Combine(ReportDirectory, stem + extension);
        for (var i = 2; File.Exists(candidate) || File.Exists(Path.ChangeExtension(candidate, ".md")); i++)
        {
            candidate = Path.Combine(ReportDirectory, $"{stem}-{i}{extension}");
        }

        return candidate;
    }

    private static async Task WriteNewFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static long? TryGetFreeBytes(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            return null;
        }

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            return drive.IsReady ? drive.AvailableFreeSpace : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DiagnosticCheckResult Result(
        string id,
        string category,
        string name,
        DiagnosticStatus status,
        string summary,
        DiagnosticEvidence evidence = DiagnosticEvidence.ConfigurationChecked,
        string? details = null,
        string? action = null,
        IReadOnlyDictionary<string, string>? facts = null) =>
        new()
        {
            Id = id,
            Category = category,
            Name = name,
            Status = status,
            Evidence = evidence,
            Summary = summary,
            Details = details,
            SuggestedAction = action,
            Facts = facts ?? new Dictionary<string, string>()
        };

    private sealed class CheckContext
    {
        public CheckContext(AppSettings settings, ServerStatus serverStatus)
        {
            Settings = settings;
            ServerStatus = serverStatus;
            InstallDirectory = NullIfBlank(settings.ServerPaths.ServerInstallDirectory)
                               ?? NullIfBlank(settings.ServerPaths.ServerWorkingDirectory);
            ClientRoot = NullIfBlank(settings.Client.RootDirectory);
        }

        public AppSettings Settings { get; }

        public ServerStatus ServerStatus { get; }

        /// <summary>Same resolution as BackupService/WorkshopModService: install dir, else working dir.</summary>
        public string? InstallDirectory { get; }

        public string? ClientRoot { get; }

        public bool ServerOnline => ServerStatus == ServerStatus.Online;
    }
}
