using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Diagnostics;
using ConanServerControl.Core.LiveTesting;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Mods;
using ConanServerControl.Core.Validation;
using ConanServerControl.Infrastructure;
using ConanServerControl.Infrastructure.Logging;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.Steam;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ConanServerControl.LiveHarness;

/// <summary>
/// M3 Task 4 guarded live harness. One checkpoint per invocation.
///
/// Environment:
///   CSC_LIVE_ROOT    dedicated live workspace root (required)
///   CSC_CLIENT_ROOT  standalone Conan client root (optional; protected, read-only)
///   CSC_SERVER_DIR   dedicated server directory (optional; default &lt;root&gt;\server). May live outside
///                    the workspace, e.g. a sibling of the client; validated by LiveTestGuard.
///   CSC_LIVE_TESTS=1 required for every command except 'plan'
/// The workspace root must contain .csc-live-test for every command except 'plan'/'init'.
/// </summary>
internal static class Program
{
    private static readonly string[] ServerProcessNames =
    [
        AppConstants.DedicatedServerProcessName,
        AppConstants.DedicatedServerShippingProcessName
    ];

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: <plan|init|diag|install-steamcmd|install-server|boot|cycle|backup|configure-rcon|add-mod ID|update-mod ID|mod-boot|client-snapshot LABEL|client-compare A B> [--hold SECONDS]");
            return 2;
        }

        var command = args[0];
        var root = Environment.GetEnvironmentVariable("CSC_LIVE_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.Error.WriteLine("CSC_LIVE_ROOT is not set.");
            return 2;
        }

        var clientRoot = Environment.GetEnvironmentVariable("CSC_CLIENT_ROOT");
        var layout = LiveTestGuard.CreateLayout(root);
        var forbidden = ProtectedLocations(clientRoot);
        IReadOnlyList<string> problems = LiveTestGuard.ValidateLayout(layout, forbidden);

        Console.WriteLine("Proposed M3 live workspace:");
        Console.WriteLine($"  root      {layout.Root}");
        Console.WriteLine($"  marker    {layout.Marker}  (exists: {File.Exists(layout.Marker)})");
        foreach (var (label, path) in layout.Children)
        {
            Console.WriteLine($"  {label,-9} {path}  (exists: {Directory.Exists(path)})");
        }

        var serverDirectory = ResolveServerDirectory(layout);
        var external = !PathValidator.PathsEqual(serverDirectory, layout.Server);
        Console.WriteLine($"  protected {string.Join(" | ", forbidden)}");
        Console.WriteLine($"  free      {FreeGb(layout.Root)}");
        Console.WriteLine($"  SERVER    {serverDirectory}  (exists: {Directory.Exists(serverDirectory)}; {(external ? "external, CSC_SERVER_DIR" : "workspace default")}; free {FreeGb(serverDirectory)})");
        if (external)
        {
            problems = problems.Concat(LiveTestGuard.ValidateExternalServerDirectory(serverDirectory, layout, clientRoot)).ToList();
        }
        if (problems.Count > 0)
        {
            Console.Error.WriteLine("LAYOUT REJECTED:");
            foreach (var p in problems)
            {
                Console.Error.WriteLine("  - " + p);
            }

            return 3;
        }

        Console.WriteLine("  overlap   PASS (workspace and server directory do not overlap the client, the launcher folder or each other)");
        if (command == "plan")
        {
            return 0;
        }

        var enabled = Environment.GetEnvironmentVariable(LiveTestGuard.EnableVariable);
        if (command == "init")
        {
            if (!LiveTestGuard.IsEnabled(enabled))
            {
                Console.Error.WriteLine($"Refusing: {LiveTestGuard.EnableVariable}=1 is required.");
                return 4;
            }

            foreach (var (_, path) in layout.Children)
            {
                Directory.CreateDirectory(path);
            }

            if (!File.Exists(layout.Marker))
            {
                File.WriteAllText(layout.Marker,
                    "Conan Server Control M3 Task 4 live-test workspace. Harness live/destructive actions are allowed only under this folder." + Environment.NewLine);
            }

            Console.WriteLine("Workspace initialised and marker written.");
            return 0;
        }

        try
        {
            LiveTestGuard.EnsureLiveAllowed(enabled, layout.Root);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine("Refusing: " + ex.Message);
            return 4;
        }

        var harness = await Harness.CreateAsync(layout, serverDirectory, clientRoot);
        try
        {
            return command switch
            {
                "diag" => await harness.DiagAsync(),
                "install-steamcmd" => await harness.InstallSteamCmdAsync(),
                "install-server" => await harness.InstallServerAsync(),
                "boot" => await harness.BootAsync("boot", HoldSeconds(args), scanMods: false, ObserveSeconds(args)),
                "mod-boot" => await harness.BootAsync("mod-boot", HoldSeconds(args), scanMods: true),
                "cycle" => await harness.CycleAsync(),
                "backup" => await harness.BackupAsync(),
                "add-mod" => await harness.AddModAsync(long.Parse(args[1])),
                "update-mod" => await harness.UpdateModAsync(long.Parse(args[1])),
                "client-snapshot" => harness.ClientSnapshot(args[1]),
                "client-compare" => harness.ClientCompare(args[1], args[2]),
                "configure-rcon" => await harness.ConfigureRconAsync(),
                "stop" => await harness.StopExistingAsync(),
                "graceful-test" => await harness.GracefulTestAsync(args[1]),
                _ => Unknown(command)
            };
        }
        finally
        {
            await harness.DisposeAsync();
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine("Unknown command: " + command);
        return 2;
    }

    private static int ObserveSeconds(string[] args)
    {
        var index = Array.IndexOf(args, "--observe");
        return index >= 0 && index + 1 < args.Length ? int.Parse(args[index + 1]) : 0;
    }

    private static int HoldSeconds(string[] args)
    {
        var index = Array.IndexOf(args, "--hold");
        return index >= 0 && index + 1 < args.Length ? int.Parse(args[index + 1]) : 0;
    }

    private static string ResolveServerDirectory(LiveTestLayout layout)
    {
        var configured = Environment.GetEnvironmentVariable("CSC_SERVER_DIR");
        return string.IsNullOrWhiteSpace(configured) || !PathValidator.IsSafeAbsolutePath(configured)
            ? layout.Server
            : PathValidator.NormalizeFullPath(configured);
    }

    private static string[] ProtectedLocations(string? clientRoot)
    {
        if (string.IsNullOrWhiteSpace(clientRoot))
        {
            return [];
        }

        var list = new List<string> { PathValidator.NormalizeFullPath(clientRoot) };
        var parent = Path.GetDirectoryName(PathValidator.NormalizeFullPath(clientRoot));
        if (parent is not null && File.Exists(Path.Combine(parent, ConanExecutableClassifier.ClientLauncherBatch)))
        {
            list.Add(parent);
        }

        return list.ToArray();
    }

    internal static string FreeGb(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
            return $"{drive.AvailableFreeSpace / (1024d * 1024 * 1024):0.0} GB free on {drive.Name}";
        }
        catch (Exception ex)
        {
            return "unknown (" + ex.Message + ")";
        }
    }

    internal static IReadOnlyList<(int Pid, string Name, string? Path)> ServerProcesses()
    {
        var list = new List<(int, string, string?)>();
        foreach (var name in ServerProcessNames)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                string? path = null;
                try
                {
                    path = p.MainModule?.FileName;
                }
                catch
                {
                    // access denied
                }

                list.Add((p.Id, p.ProcessName, path));
                p.Dispose();
            }
        }

        return list;
    }
}

internal sealed class Harness : IAsyncDisposable
{
    private readonly LiveTestLayout _layout;
    private readonly string _serverDir;
    private readonly string? _clientRoot;
    private readonly ServiceProvider _services;
    private readonly ISettingsService _settings;
    private readonly RecordingSteamCmd _steam;
    private readonly LiveLog _log;

    private Harness(LiveTestLayout layout, string serverDir, string? clientRoot, ServiceProvider services, RecordingSteamCmd steam, LiveLog log)
    {
        _layout = layout;
        _serverDir = serverDir;
        _clientRoot = clientRoot;
        _services = services;
        _settings = services.GetRequiredService<ISettingsService>();
        _steam = steam;
        _log = log;
    }

    private string ServerExe =>
        DedicatedServerLocator.Find(_serverDir) ?? Path.Combine(_serverDir, AppConstants.DedicatedServerExecutable);

    private string ServerLog => Path.Combine(_serverDir, "ConanSandbox", "Saved", "Logs", "ConanSandbox.log");

    private string Saved => Path.Combine(_serverDir, "ConanSandbox", "Saved");

    public static async Task<Harness> CreateAsync(LiveTestLayout layout, string serverDir, string? clientRoot)
    {
        var paths = new AppPaths(layout.AppData);
        paths.EnsureCreated();
        var liveLogBuffer = new LiveLogBuffer();
        Log.Logger = LoggingSetup.CreateLoggerConfiguration(paths, liveLogBuffer)
            .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSerilog(dispose: false));
        services.AddSingleton<ILiveLogBuffer>(liveLogBuffer);
        services.AddConanServerControl(paths);
        RecordingSteamCmd? recording = null;
        services.AddSingleton<ISteamCmdService>(sp =>
            recording = new RecordingSteamCmd(ActivatorUtilities.CreateInstance<SteamCmdService>(sp)));
        var provider = services.BuildServiceProvider(); // hosted services are never started

        // Same database initialization the app performs at startup (activity log table).
        await ActivatorUtilities.CreateInstance<ConanServerControl.Infrastructure.Hosting.DatabaseInitializerHostedService>(provider)
            .StartAsync(CancellationToken.None);

        var settings = provider.GetRequiredService<ISettingsService>();
        await settings.LoadAsync();
        await settings.UpdateAsync(s =>
        {
            s.IsSetupComplete = true;
            s.SteamCmd.InstallDirectory = layout.SteamCmd;
            s.SteamCmd.UseAnonymousLogin = true;
            s.ServerPaths.ServerInstallDirectory = serverDir;
            s.ServerPaths.ServerExecutablePath =
                DedicatedServerLocator.Find(serverDir) ?? Path.Combine(serverDir, AppConstants.DedicatedServerExecutable);
            s.ServerPaths.ServerWorkingDirectory = null;
            s.Client.RootDirectory = string.IsNullOrWhiteSpace(clientRoot) ? null : clientRoot;
            s.Server.ServerName = "CSC M3 Live Test";
            s.General.StartServerWhenManagerLaunches = false;
            s.Advanced.RestartAfterCrash = false; // never auto-restart while diagnosing live boots
            s.WebAdmin.Enabled = false;
        });

        _ = provider.GetRequiredService<ISteamCmdService>();
        var secrets = settings.Secrets;
        var redactor = new DiagnosticReportRedactor(
        [
            secrets.RconPassword, secrets.ServerPassword, secrets.AdminPassword, secrets.SteamPassword, secrets.WebAdminPasswordHash
        ]);
        var log = new LiveLog(Path.Combine(layout.LiveTest, "m3-live-log.jsonl"), redactor);
        return new Harness(layout, serverDir, clientRoot, provider, recording!, log);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await Log.CloseAndFlushAsync();
    }

    // ------------------------------------------------------------ diag

    public async Task<int> DiagAsync()
    {
        var diag = _services.GetRequiredService<IIntegrationDiagnosticsService>();
        var clock = Stopwatch.StartNew();
        var report = await diag.RunAsync();
        var export = await diag.ExportAsync(report);
        var sb = new StringBuilder();
        foreach (var c in report.Checks)
        {
            sb.AppendLine($"[{DiagnosticReportFormatter.StatusLabel(c.Status),-14}] [{DiagnosticReportFormatter.EvidenceLabel(c.Evidence),-16}] {c.Category} / {c.Name}: {c.Summary}");
        }

        foreach (var r in new[] { report.ServerLiveTest, report.ClientCompatibilityTest })
        {
            sb.AppendLine(r.Headline);
            foreach (var b in r.Blockers)
            {
                sb.AppendLine("  BLOCKER: " + b);
            }
        }

        _log.Write("diagnostics", "IntegrationDiagnostics.RunAsync + ExportAsync", "INFO", clock.Elapsed,
            new Dictionary<string, string>
            {
                ["ServerLiveTest"] = report.ServerLiveTest.Headline,
                ["ClientCompatibilityTest"] = report.ClientCompatibilityTest.Headline,
                ["Report"] = export.JsonPath
            },
            sb.ToString(), liveFilesChanged: "no");
        return 0;
    }

    // ------------------------------------------------------------ 4A SteamCMD

    public async Task<int> InstallSteamCmdAsync()
    {
        var steam = _services.GetRequiredService<ISteamCmdService>();
        var clock = Stopwatch.StartNew();
        try
        {
            await steam.InstallAsync(new Progress<string>(_ => { }));
        }
        catch (Exception ex)
        {
            _log.Write("4A", "SteamCmdService.InstallAsync", "FAIL", clock.Elapsed,
                Facts(("SteamCmdDirectory", _layout.SteamCmd), ("Error", ex.Message)), Tail(_steam.Records), "steamcmd folder only");
            return 1;
        }

        var exe = steam.ExecutablePath!;
        _log.Write("4A", "SteamCmdService.InstallAsync", File.Exists(exe) ? "PASS" : "FAIL", clock.Elapsed,
            Facts(
                ("SteamCmdExe", exe),
                ("Exists", File.Exists(exe).ToString()),
                ("SizeBytes", File.Exists(exe) ? new FileInfo(exe).Length.ToString() : "0"),
                ("Source", AppConstants.SteamCmdZipUrl),
                ("TopLevelEntries", string.Join(", ", Directory.EnumerateFileSystemEntries(_layout.SteamCmd).Select(Path.GetFileName)))),
            Tail(_steam.Records), "steamcmd folder only");
        return File.Exists(exe) ? 0 : 1;
    }

    // ------------------------------------------------------------ 4B server install

    public async Task<int> InstallServerAsync()
    {
        // SteamCMD's bootstrapper must reach Valve's update CDN before it can download anything.
        // Check first so a blocked network creates nothing (the service creates the target folder).
        var preflight = await SteamUpdateHostReachableAsync();
        if (preflight is not null)
        {
            _log.Write("4B", "network preflight (client-update.steamstatic.com)", "BLOCKED", null,
                Facts(("ServerDirectory", _serverDir), ("Reason", preflight), ("ServerDirectoryCreated", "no")),
                liveFilesChanged: "no");
            return 5;
        }

        var steam = _services.GetRequiredService<ISteamCmdService>();
        var clock = Stopwatch.StartNew();
        ProcessExecutionResult? result = null;
        string? error = null;
        try
        {
            result = await steam.InstallOrUpdateDedicatedServerAsync(_serverDir, validate: true, new Progress<string>(_ => { }));
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        var record = _steam.Records.LastOrDefault();
        if (record is not null)
        {
            File.WriteAllLines(Path.Combine(_layout.LiveTest, $"steamcmd-app_update-{DateTime.Now:yyyyMMdd-HHmmss}.log"), record.Output);
        }

        var gate = ServerExecutableGate.Evaluate(ServerExe, _clientRoot);
        var exists = File.Exists(ServerExe);
        var manifest = Path.Combine(_serverDir, "steamapps", $"appmanifest_{AppConstants.ConanDedicatedServerAppId}.acf");
        var ok = error is null && exists && gate.Allowed;
        _log.Write("4B", "SteamCmdService.InstallOrUpdateDedicatedServerAsync(validate)", ok ? "PASS" : "FAIL", clock.Elapsed,
            Facts(
                ("Command", record?.Command ?? "(none)"),
                ("ExitCode", result?.ExitCode.ToString() ?? "n/a"),
                ("TimedOut", result?.TimedOut.ToString() ?? "n/a"),
                ("Error", error ?? string.Empty),
                ("StdErr", Truncate(result?.StandardError, 400)),
                ("ServerExe", ServerExe),
                ("ServerExeExists", exists.ToString()),
                ("Classification", gate.Kind.ToString()),
                ("StartAllowed", gate.Allowed.ToString()),
                ("GateReason", gate.Reason),
                ("AppManifest", File.Exists(manifest) ? File.ReadAllText(manifest).Replace('\t', ' ').Replace("\r\n", " ").Replace('\n', ' ') : "missing"),
                ("ServerTopLevel", Directory.Exists(_serverDir) ? string.Join(", ", Directory.EnumerateFileSystemEntries(_serverDir).Select(Path.GetFileName)) : "missing"),
                ("Win64", Directory.Exists(Path.GetDirectoryName(ServerExe)) ? string.Join(", ", Directory.EnumerateFiles(Path.GetDirectoryName(ServerExe)!, "*.exe").Select(Path.GetFileName)) : "missing")),
            Tail(_steam.Records), "server folder only (fresh install)");
        return ok ? 0 : 1;
    }

    // ------------------------------------------------------------ 4C boot / cycle

    public async Task<int> BootAsync(string step, int holdSeconds, bool scanMods, int observeSeconds = 0)
    {
        if (!EnsureNoForeignServer(step))
        {
            return 1;
        }

        var server = _services.GetRequiredService<IServerProcessManager>();
        var timeline = TrackStates(server);
        var logOffset = FileLength(ServerLog);
        var clock = Stopwatch.StartNew();
        try
        {
            await server.StartAsync();
        }
        catch (Exception ex)
        {
            _log.Write(step, "IServerProcessManager.StartAsync", "FAIL", clock.Elapsed,
                Facts(("Error", ex.Message), ("Status", server.State.Status.ToString()), ("LastExitCode", server.State.LastExitCode?.ToString() ?? "n/a"),
                    ("Timeline", string.Join(" -> ", timeline)), ("Processes", DescribeProcesses())),
                ReadLogFrom(ServerLog, logOffset, 60), "server Saved folder (Conan)");
            await TryStopAsync(server);
            return 1;
        }

        var startup = clock.Elapsed;
        var probe = await _services.GetRequiredService<IServerReadinessProbe>().ProbeAsync(new ServerReadinessContext
        {
            ProcessId = server.State.ProcessId ?? 0,
            GamePort = _settings.Current.Server.GamePort,
            StartedAt = server.State.StartedAt ?? DateTimeOffset.UtcNow
        });

        var game = _settings.Current.Server.GamePort;
        _log.Write(step, "IServerProcessManager.StartAsync (readiness)", server.State.Status == ServerStatus.Online ? "PASS" : "FAIL", startup,
            Facts(
                ("Status", server.State.Status.ToString()),
                ("Health", server.State.Health.ToString()),
                ("ManagedPid", server.State.ProcessId?.ToString() ?? "n/a"),
                ("Processes", DescribeProcesses()),
                ("Timeline", string.Join(" -> ", timeline)),
                ("ReadinessReason", probe.Reason),
                ($"UdpBound{game}", UdpBound(game).ToString()),
                ($"UdpBound{game + 1}", UdpBound(game + 1).ToString()),
                ($"UdpBoundQuery{_settings.Current.Server.QueryPort}", UdpBound(_settings.Current.Server.QueryPort).ToString()),
                ($"TcpListenRcon{_settings.Current.Rcon.Port}", TcpListening(_settings.Current.Rcon.Port).ToString()),
                ("RconPasswordConfigured", string.IsNullOrEmpty(_settings.Secrets.RconPassword) ? "NO" : "YES"),
                ("ServerLog", ServerLog)),
            Interesting(ReadLogFrom(ServerLog, logOffset, int.MaxValue), scanMods), "server Saved folder (Conan creates world/logs)");

        if (observeSeconds > 0)
        {
            await ObserveReadinessSignalsAsync(step, observeSeconds, clock, logOffset);
        }

        if (holdSeconds > 0)
        {
            Console.WriteLine($"Holding the server online for {holdSeconds}s (client observation window)...");
            await Task.Delay(TimeSpan.FromSeconds(holdSeconds));
        }

        return await StopAndRecordAsync(server, step, timeline) ? 0 : 1;
    }

    /// <summary>
    /// Samples readiness signals once per second after Start to find which one really means
    /// "world loaded": game/query UDP bind, RCON TCP listen, RCON reply, log frame counter > 0.
    /// </summary>
    private async Task ObserveReadinessSignalsAsync(string step, int seconds, Stopwatch sinceStart, long logOffset)
    {
        var first = new Dictionary<string, string>();
        var frameRegex = new System.Text.RegularExpressions.Regex(@"^\[\d{4}\.\d\d\.\d\d-[\d.:]+\]\[\s*(\d+)\]");
        var rcon = _services.GetRequiredService<IRconService>();
        var game = _settings.Current.Server.GamePort;
        var query = _settings.Current.Server.QueryPort;
        var rconPort = _settings.Current.Rcon.Port;
        var rconConfigured = !string.IsNullOrEmpty(_settings.Secrets.RconPassword);
        string? rconError = null;

        void Mark(string key, bool condition)
        {
            if (condition && !first.ContainsKey(key))
            {
                first[key] = $"{sinceStart.Elapsed.TotalSeconds:0}s";
                Console.WriteLine($"    signal {key} at {first[key]}");
            }
        }

        for (var i = 0; i < seconds; i++)
        {
            Mark($"UdpBound{game}", UdpBound(game));
            Mark($"UdpBound{query}", UdpBound(query));
            Mark($"TcpListen{rconPort}", TcpListening(rconPort));

            var lastFrame = 0;
            foreach (var line in ReadLogFrom(ServerLog, logOffset, 200).Split(Environment.NewLine).Reverse())
            {
                var m = frameRegex.Match(line);
                if (m.Success)
                {
                    lastFrame = int.Parse(m.Groups[1].Value);
                    break;
                }
            }

            Mark("LogFrameAdvancing", lastFrame > 0);
            Mark("LogServerStats", ReadLogFrom(ServerLog, logOffset, int.MaxValue).Contains("LogServerStats: Status report", StringComparison.Ordinal));

            if (rconConfigured && !first.ContainsKey("RconReply") && TcpListening(rconPort))
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await rcon.SendCommandAsync("listplayers", cts.Token);
                    Mark("RconReply", true);
                }
                catch (Exception ex)
                {
                    rconError = ex.Message;
                }
            }

            var done = first.ContainsKey("LogFrameAdvancing") && (!rconConfigured || first.ContainsKey("RconReply"));
            if (done && i >= 5)
            {
                break;
            }

            await Task.Delay(1000);
        }

        var facts = new Dictionary<string, string>(first)
        {
            ["RconConfigured"] = rconConfigured ? "YES" : "NO",
            ["LastRconError"] = rconError ?? string.Empty,
            ["Note"] = "Times are seconds since StartAsync began."
        };
        _log.Write(step, "readiness signal observation", "INFO", sinceStart.Elapsed, facts);
    }

    public async Task<int> CycleAsync()
    {
        if (!EnsureNoForeignServer("cycle"))
        {
            return 1;
        }

        var server = _services.GetRequiredService<IServerProcessManager>();
        var timeline = TrackStates(server);
        var ok = true;

        async Task Step(string name, Func<Task> action, ServerStatus expected)
        {
            var clock = Stopwatch.StartNew();
            string? error = null;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            var pass = error is null && server.State.Status == expected;
            ok &= pass;
            _log.Write("4C-cycle", name, pass ? "PASS" : "FAIL", clock.Elapsed,
                Facts(("Status", server.State.Status.ToString()), ("Expected", expected.ToString()), ("Error", error ?? string.Empty),
                    ("ManagedPid", server.State.ProcessId?.ToString() ?? "n/a"), ("Processes", DescribeProcesses()),
                    ("LastExitCode", server.State.LastExitCode?.ToString() ?? "n/a"), ("Timeline", string.Join(" -> ", timeline))));
            timeline.Clear();
        }

        await Step("Start", () => server.StartAsync(), ServerStatus.Online);
        if (ok)
        {
            await Step("Stop", () => server.StopAsync(), ServerStatus.Offline);
        }

        if (ok)
        {
            await Step("Start (for restart)", () => server.StartAsync(), ServerStatus.Online);
        }

        if (ok)
        {
            await Step("Restart", () => server.RestartAsync(), ServerStatus.Online);
        }

        await Step("Final stop", () => server.StopAsync(), ServerStatus.Offline);
        var leftovers = Program.ServerProcesses();
        _log.Write("4C-cycle", "post-cycle process check", leftovers.Count == 0 ? "PASS" : "FAIL", null,
            Facts(("RemainingServerProcesses", DescribeProcesses())));
        return ok && leftovers.Count == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------ 4D backup

    public async Task<int> BackupAsync()
    {
        var bootResult = await BootAsync("4D-boot", 0, scanMods: false);
        if (bootResult != 0)
        {
            return bootResult;
        }

        return await ColdBackupAsync("4D", "m3-live-cold-backup") ? 0 : 1;
    }

    private async Task<bool> ColdBackupAsync(string step, string reason)
    {
        var server = _services.GetRequiredService<IServerProcessManager>();
        if (server.State.Status is not ServerStatus.Offline and not ServerStatus.Error || Program.ServerProcesses().Count > 0)
        {
            _log.Write(step, "cold backup precondition", "FAIL", null, Facts(("Status", server.State.Status.ToString()), ("Processes", DescribeProcesses())));
            return false;
        }

        var before = WorldSnapshot();
        var clock = Stopwatch.StartNew();
        try
        {
            var record = await _services.GetRequiredService<IBackupService>().BackupNowAsync(reason);
            var after = WorldSnapshot();
            var unchanged = before.SequenceEqual(after);
            _log.Write(step, "IBackupService.BackupNowAsync (cold)", record.Succeeded ? "PASS" : "FAIL", clock.Elapsed,
                Facts(
                    ("BackupId", record.Id),
                    ("Directory", record.DirectoryPath),
                    ("WorldType", record.WorldType ?? "none"),
                    ("MainDb", record.MainDbFileName ?? "none"),
                    ("WorldFiles", string.Join("; ", record.WorldFiles.Select(f => $"{f.FileName} {f.SizeBytes}B sha256={f.Sha256}"))),
                    ("ManifestWritten", record.ManifestWritten.ToString()),
                    ("HashesVerified", record.HashesVerified.ToString()),
                    ("SqliteVerified", record.SqliteVerified.ToString()),
                    ("VerificationDetail", record.VerificationDetail ?? string.Empty),
                    ("LiveSavedWorldFiles", string.Join("; ", before)),
                    ("LiveWorldUnchangedByBackup", unchanged.ToString()),
                    ("SavedTopLevel", Directory.Exists(Saved) ? string.Join(", ", Directory.EnumerateFileSystemEntries(Saved).Select(Path.GetFileName)) : "missing")),
                liveFilesChanged: unchanged ? "no (live world files identical before/after)" : "YES - investigate");
            return record.Succeeded && unchanged;
        }
        catch (Exception ex)
        {
            _log.Write(step, "IBackupService.BackupNowAsync (cold)", "FAIL", clock.Elapsed,
                Facts(("Error", ex.Message), ("LiveSavedWorldFiles", string.Join("; ", before))));
            return false;
        }
    }

    // ------------------------------------------------------------ 4E Workshop

    public async Task<int> AddModAsync(long workshopId)
    {
        if (!await ColdBackupAsync("4E-pre", $"pre-mod-add-{workshopId}"))
        {
            Console.Error.WriteLine("Verified cold backup failed; refusing to add the mod.");
            return 1;
        }

        var mods = _services.GetRequiredService<IWorkshopModService>();
        var clock = Stopwatch.StartNew();
        string? error = null;
        try
        {
            await mods.AddAsync(workshopId);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        return RecordModResult("4E-add", "IWorkshopModService.AddAsync", workshopId, clock.Elapsed, error);
    }

    public async Task<int> UpdateModAsync(long workshopId)
    {
        var updates = _services.GetRequiredService<IServerUpdateService>();
        var clock = Stopwatch.StartNew();
        string? error = null;
        try
        {
            await updates.UpdateSelectedModsAsync(workshopId, new Progress<PipelineProgress>(p =>
                Console.WriteLine($"    pipeline> {p.State} {p.CurrentStep}/{p.TotalSteps} {p.StepDescription} {p.Error}")));
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        return RecordModResult("4E-update", "IServerUpdateService.UpdateSelectedModsAsync (transactional pipeline)", workshopId, clock.Elapsed, error);
    }

    private int RecordModResult(string step, string operation, long workshopId, TimeSpan duration, string? error)
    {
        var mod = _settings.Current.Mods.Mods.FirstOrDefault(m => m.WorkshopId == workshopId);
        var modsDir = Path.Combine(_serverDir, "ConanSandbox", "Mods");
        var modList = Path.Combine(modsDir, AppConstants.ModListFileName);
        var record = _steam.Records.LastOrDefault(r => r.Command.Contains("workshop_download_item", StringComparison.Ordinal));
        if (record is not null)
        {
            File.WriteAllLines(Path.Combine(_layout.LiveTest, $"steamcmd-workshop-{workshopId}-{DateTime.Now:yyyyMMdd-HHmmss}.log"), record.Output);
        }

        var livePak = mod?.LocalFileName is null ? null : Path.Combine(modsDir, mod.LocalFileName);
        var ok = error is null && mod?.Error is null && livePak is not null && File.Exists(livePak);
        _log.Write(step, operation, ok ? "PASS" : "FAIL", duration,
            Facts(
                ("WorkshopId", workshopId.ToString()),
                ("Error", error ?? mod?.Error ?? string.Empty),
                ("SteamCmdCommand", record?.Command ?? "(not called)"),
                ("SteamCmdSucceeded", record?.Succeeded.ToString() ?? "n/a"),
                ("SteamCmdDuration", record is null ? "n/a" : $"{record.Duration.TotalSeconds:0.0}s"),
                ("StagedFiles", record is null ? "n/a" : string.Join("; ", record.StagedFiles.Select(f => $"{f.RelativePath} ({f.Size}B)"))),
                ("CatalogLocalFileName", mod?.LocalFileName ?? "none"),
                ("LivePak", livePak ?? "none"),
                ("LivePakSize", livePak is not null && File.Exists(livePak) ? new FileInfo(livePak).Length.ToString() : "missing"),
                ("LivePakSha256", livePak is not null && File.Exists(livePak) ? Sha256(livePak) : "missing"),
                ("ModsDirectory", Directory.Exists(modsDir) ? string.Join(", ", Directory.EnumerateFiles(modsDir).Select(Path.GetFileName)) : "missing"),
                ("ModList", File.Exists(modList) ? File.ReadAllText(modList).Replace("\r\n", " | ").Replace('\n', '|') : "missing")),
            Tail(_steam.Records), "server Mods folder + modlist.txt (expected)");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// Stops a server left running by an interrupted harness run, through the application:
    /// attach (RefreshAsync) then StopAsync. Refuses unless every Conan server process runs
    /// from the configured server directory.
    /// </summary>
    public async Task<int> StopExistingAsync()
    {
        var existing = Program.ServerProcesses();
        if (existing.Count == 0)
        {
            _log.Write("stop", "stop existing server", "INFO", null, Facts(("Processes", DescribeProcesses())), "No server process is running.");
            return 0;
        }

        var foreign = existing.Where(p => p.Path is null || !PathValidator.IsUnderRoot(p.Path, _serverDir)).ToArray();
        if (foreign.Length > 0)
        {
            _log.Write("stop", "stop existing server", "FAIL", null, Facts(("Processes", DescribeProcesses())),
                "A Conan server process outside the configured server directory is running. Refusing to attach.");
            return 1;
        }

        var server = _services.GetRequiredService<IServerProcessManager>();
        await server.RefreshAsync();
        var timeline = TrackStates(server);
        _log.Write("stop", "IServerProcessManager.RefreshAsync (attach)", server.State.ProcessId is null ? "FAIL" : "PASS", null,
            Facts(("Status", server.State.Status.ToString()), ("AttachedPid", server.State.ProcessId?.ToString() ?? "n/a"), ("Processes", DescribeProcesses())));
        if (server.State.ProcessId is null)
        {
            return 1;
        }

        return await StopAndRecordAsync(server, "stop", timeline) ? 0 : 1;
    }

    /// <summary>
    /// Bounded experiment: boot, wait for readiness, send ONE candidate shutdown command over RCON,
    /// record the reply and whether the server exits cleanly within 60 s. Falls back to the
    /// application's normal stop when it does not.
    /// </summary>
    public async Task<int> GracefulTestAsync(string command)
    {
        if (!EnsureNoForeignServer("graceful-test"))
        {
            return 1;
        }

        var server = _services.GetRequiredService<IServerProcessManager>();
        var timeline = TrackStates(server);
        var clock = Stopwatch.StartNew();
        await server.StartAsync();
        _log.Write("graceful-test", "StartAsync (readiness)", server.State.Status == ServerStatus.Online ? "PASS" : "FAIL", clock.Elapsed,
            Facts(("Status", server.State.Status.ToString()), ("Timeline", string.Join(" -> ", timeline)), ("Processes", DescribeProcesses())));
        if (server.State.Status != ServerStatus.Online)
        {
            await TryStopAsync(server);
            return 1;
        }

        var logOffset = FileLength(ServerLog);
        string reply;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            reply = await _services.GetRequiredService<IRconService>().SendCommandAsync(command, cts.Token);
        }
        catch (Exception ex)
        {
            reply = "(exception) " + ex.Message;
        }

        var exitClock = Stopwatch.StartNew();
        while (exitClock.Elapsed < TimeSpan.FromSeconds(60) && Program.ServerProcesses().Count > 0)
        {
            await Task.Delay(1000);
        }

        var exited = Program.ServerProcesses().Count == 0;
        _log.Write("graceful-test", $"RCON '{command}'", exited ? "PASS" : "FAIL", exitClock.Elapsed,
            Facts(("Command", command), ("Reply", string.IsNullOrEmpty(reply) ? "(empty)" : reply), ("ExitedWithin60s", exited.ToString()),
                ("ExitCode", server.State.LastExitCode?.ToString() ?? "n/a"), ("Processes", DescribeProcesses())),
            Interesting(ReadLogFrom(ServerLog, logOffset, int.MaxValue), scanMods: false) + Environment.NewLine +
            "--- last log lines ---" + Environment.NewLine + ReadLogFrom(ServerLog, logOffset, 15));

        if (!exited)
        {
            await StopAndRecordAsync(server, "graceful-test", timeline);
        }
        else
        {
            await server.RefreshAsync();
        }

        return exited ? 0 : 1;
    }

    // ------------------------------------------------------------ RCON for the throwaway test server

    /// <summary>
    /// Enables RCON on the throwaway test server so graceful stop (RCON DoExit) can be exercised.
    /// Writes only the server's own Saved\Config\WindowsServer\Game.ini [RconPlugin] section and
    /// the app's DPAPI-protected secret. The password is random and never printed.
    /// </summary>
    public async Task<int> ConfigureRconAsync()
    {
        if (Program.ServerProcesses().Count > 0)
        {
            _log.Write("4C-rcon", "configure RCON", "FAIL", null, Facts(("Processes", DescribeProcesses())), "Stop the server first.");
            return 1;
        }

        var configDir = Path.Combine(Saved, "Config", "WindowsServer");
        if (!Directory.Exists(configDir))
        {
            _log.Write("4C-rcon", "configure RCON", "FAIL", null, Facts(("ConfigDirectory", configDir)), "Boot the server once so Conan creates its config first.");
            return 1;
        }

        var gameIni = Path.Combine(configDir, "Game.ini");
        var existed = File.Exists(gameIni);
        var lines = existed ? File.ReadAllLines(gameIni).ToList() : new List<string>();
        var start = lines.FindIndex(l => l.Trim().Equals("[RconPlugin]", StringComparison.OrdinalIgnoreCase));
        if (start >= 0)
        {
            var end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith('['));
            lines.RemoveRange(start, (end < 0 ? lines.Count : end) - start);
        }

        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var password = new string(Enumerable.Range(0, 24).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
        var port = _settings.Current.Rcon.Port;
        if (lines.Count > 0 && lines[^1].Length > 0)
        {
            lines.Add(string.Empty);
        }

        lines.AddRange(["[RconPlugin]", "RconEnabled=1", $"RconPassword={password}", $"RconPort={port}", "RconMaxKarma=60"]);
        File.WriteAllLines(gameIni, lines);
        await _settings.UpdateSecretsAsync(sec => sec.RconPassword = password);
        await _settings.UpdateAsync(st => st.Rcon.Enabled = true);

        _log.Write("4C-rcon", "configure RCON on throwaway server", "PASS", null,
            Facts(("GameIni", gameIni), ("GameIniExisted", existed.ToString()), ("RconPort", port.ToString()),
                ("RconPasswordConfigured", "YES"), ("Written", "[RconPlugin] RconEnabled=1, RconPassword=(redacted), RconPort, RconMaxKarma=60")),
            liveFilesChanged: "server Game.ini [RconPlugin] only");
        return 0;
    }

    // ------------------------------------------------------------ 4F client snapshots (read-only)

    public int ClientSnapshot(string label)
    {
        if (string.IsNullOrWhiteSpace(_clientRoot) || !Directory.Exists(_clientRoot))
        {
            Console.Error.WriteLine("CSC_CLIENT_ROOT is not set or does not exist.");
            return 1;
        }

        var entries = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in ClientWatchDirectories())
        {
            if (!Directory.Exists(dir))
            {
                entries[dir + Path.DirectorySeparatorChar] = "absent";
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                entries[file] = $"{info.Length}|{info.LastWriteTimeUtc:o}|{(info.Length <= 4 * 1024 * 1024 ? Sha256(file) : "large")}";
            }
        }

        var path = Path.Combine(_layout.LiveTest, $"client-snapshot-{label}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        _log.Write("4F", $"client snapshot '{label}' (read-only)", "INFO", null,
            Facts(("Snapshot", path), ("Entries", entries.Count.ToString()), ("Watched", string.Join(" | ", ClientWatchDirectories()))),
            liveFilesChanged: "no (read-only)");
        return 0;
    }

    public int ClientCompare(string a, string b)
    {
        var left = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(_layout.LiveTest, $"client-snapshot-{a}.json")))!;
        var right = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(_layout.LiveTest, $"client-snapshot-{b}.json")))!;
        var added = right.Keys.Except(left.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
        var removed = left.Keys.Except(right.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
        var changed = left.Keys.Intersect(right.Keys, StringComparer.OrdinalIgnoreCase).Where(k => left[k] != right[k]).ToArray();
        var sb = new StringBuilder();
        foreach (var x in added) sb.AppendLine("ADDED   " + x + "  " + right[x]);
        foreach (var x in removed) sb.AppendLine("REMOVED " + x);
        foreach (var x in changed) sb.AppendLine("CHANGED " + x + "  " + left[x] + " -> " + right[x]);
        _log.Write("4F", $"client compare '{a}' -> '{b}'", "INFO", null,
            Facts(("Added", added.Length.ToString()), ("Removed", removed.Length.ToString()), ("Changed", changed.Length.ToString())),
            sb.ToString(), liveFilesChanged: added.Length + removed.Length + changed.Length == 0 ? "no" : "yes (client wrote files; see excerpt)");
        return 0;
    }

    private IEnumerable<string> ClientWatchDirectories()
    {
        yield return Path.Combine(_clientRoot!, "ConanSandbox", "Mods");
        yield return Path.Combine(_clientRoot!, "ConanSandbox", "Saved", "Config");
        yield return Path.Combine(_clientRoot!, "ConanSandbox", "Saved", "Logs");
        yield return Path.Combine(_clientRoot!, "ConanSandbox", "Saved", "SaveGames");
        yield return Path.Combine(_clientRoot!, "ConanSandbox", "Content", "Paks", "Mods");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ConanSandbox");
    }

    // ------------------------------------------------------------ helpers

    private bool EnsureNoForeignServer(string step)
    {
        var game = _settings.Current.Server.GamePort;
        if (UdpBound(game))
        {
            _log.Write(step, "pre-start port check", "FAIL", null,
                Facts(($"UdpBound{game}", "True"), ("Processes", DescribeProcesses())),
                $"UDP game port {game} is already in use before start, so the readiness probe would be meaningless. Refusing to start.");
            return false;
        }

        var existing = Program.ServerProcesses();
        if (existing.Count == 0)
        {
            return true;
        }

        _log.Write(step, "pre-start process check", "FAIL", null,
            Facts(("ExistingServerProcesses", DescribeProcesses())),
            "A Conan dedicated server process is already running. Refusing to start (attach-to-existing is not trusted in Task 4).");
        return false;
    }

    private async Task<bool> StopAndRecordAsync(IServerProcessManager server, string step, List<string> timeline)
    {
        timeline.Clear();
        var logOffset = FileLength(ServerLog);
        var clock = Stopwatch.StartNew();
        string? error = null;
        try
        {
            await server.StopAsync();
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        await Task.Delay(2000);
        var leftovers = Program.ServerProcesses();
        var ok = error is null && server.State.Status == ServerStatus.Offline && leftovers.Count == 0;
        _log.Write(step, "IServerProcessManager.StopAsync", ok ? "PASS" : "FAIL", clock.Elapsed,
            Facts(("Status", server.State.Status.ToString()), ("Error", error ?? string.Empty), ("LastExitCode", server.State.LastExitCode?.ToString() ?? "n/a"),
                ("Timeline", string.Join(" -> ", timeline)), ("RemainingServerProcesses", DescribeProcesses()),
                ("StopPath", string.IsNullOrEmpty(_settings.Secrets.RconPassword) ? "no RCON password -> CloseMainWindow -> kill tree" : "RCON DoExit attempted first")),
            ReadLogFrom(ServerLog, logOffset, 25));
        return ok;
    }

    private static async Task TryStopAsync(IServerProcessManager server)
    {
        try
        {
            await server.StopAsync(force: true);
        }
        catch
        {
            // best effort; reported by the caller
        }
    }

    private static async Task<string?> SteamUpdateHostReachableAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            using var request = new HttpRequestMessage(HttpMethod.Head, "https://client-update.steamstatic.com/steam_cmd_win64");
            using var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            return ex.GetBaseException().Message;
        }
    }

    private static List<string> TrackStates(IServerProcessManager server)
    {
        var clock = Stopwatch.StartNew();
        var timeline = new List<string> { $"{server.State.Status}@0s" };
        server.StateChanged += (_, state) =>
        {
            var entry = $"{state.Status}@{clock.Elapsed.TotalSeconds:0}s";
            lock (timeline)
            {
                if (timeline.Count == 0 || !timeline[^1].StartsWith(state.Status + "@", StringComparison.Ordinal))
                {
                    timeline.Add(entry);
                }
            }
        };
        return timeline;
    }

    private List<string> WorldSnapshot()
    {
        var list = new List<string>();
        foreach (var name in ConanServerControl.Core.Backups.ConanWorldFiles.Present(Saved))
        {
            var info = new FileInfo(Path.Combine(Saved, name));
            list.Add($"{name} {info.Length}B mtime={info.LastWriteTimeUtc:o} sha256={Sha256(info.FullName)}");
        }

        return list;
    }

    private static string DescribeProcesses()
    {
        var list = Program.ServerProcesses();
        var server = list.Count == 0 ? "none" : string.Join("; ", list.Select(p => $"{p.Name} pid={p.Pid} path={p.Path ?? "?"}"));
        var clients = new List<string>();
        foreach (var name in new[] { "ConanSandbox", "ConanSandbox-Win64-Shipping", "ConanSandbox_BE" })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                clients.Add($"{p.ProcessName} pid={p.Id}");
                p.Dispose();
            }
        }

        return clients.Count == 0 ? $"{server} | client processes: none" : $"{server} | CLIENT PROCESSES: {string.Join("; ", clients)}";
    }

    private static bool UdpBound(int port) =>
        IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(e => e.Port == port);

    private static bool TcpListening(int port) =>
        IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == port);

    private static long FileLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    private static string ReadLogFrom(string path, long offset, int maxLines)
    {
        if (!File.Exists(path))
        {
            return $"(server log not found: {path})";
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (offset > stream.Length)
        {
            offset = 0; // log rotated
        }

        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        return string.Join(Environment.NewLine, lines.Length > maxLines ? lines[^maxLines..] : lines);
    }

    private static string Interesting(string log, bool scanMods)
    {
        var patterns = scanMods
            ? new[] { "mod", ".pak", "mount", "Error", "Fatal", "listening", "port", "Session", "Steam" }
            : new[] { "Error", "Fatal", "listening", "port", "Session", "Steam", "world", "game_0", "game.db", "rcon" };
        var lines = log.Split(Environment.NewLine)
            .Where(l => patterns.Any(p => l.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var picked = lines.Length > 80 ? lines[..40].Concat(["..."]).Concat(lines[^40..]).ToArray() : lines;
        return string.Join(Environment.NewLine, picked);
    }

    private static string Tail(List<SteamCmdRecord> records)
    {
        var last = records.LastOrDefault();
        if (last is null)
        {
            return string.Empty;
        }

        var lines = last.Output.ToArray();
        return $"{last.Command}{Environment.NewLine}{string.Join(Environment.NewLine, lines.Length > 30 ? lines[^30..] : lines)}";
    }

    private static string Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max] + "...";

    private static string Sha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static Dictionary<string, string> Facts(params (string Key, string Value)[] facts) =>
        facts.ToDictionary(f => f.Key, f => f.Value);
}
