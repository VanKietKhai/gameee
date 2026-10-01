using ConanServerControl.Core.Diagnostics;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.LiveTesting;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;
using ConanServerControl.Infrastructure.Concurrency;
using ConanServerControl.Infrastructure.ProcessManagement;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

/// <summary>
/// M3 Task 4 pre-live safety: hard server-executable gate, QA-018 path normalization,
/// and the live-test marker/workspace guard. Fully offline.
/// </summary>
public sealed class M3LiveSafetyTests
{
    private const string ClientRoot = @"D:\conan exiles\Conan Exiles Enhanced";

    // ------------------------------------------------------------ Hard executable gate

    [Theory]
    [InlineData(@"D:\conan exiles\Run Me!.bat")]
    [InlineData(@"D:\conan exiles\Conan Exiles Enhanced\ConanSandbox.exe")]
    [InlineData(@"D:\conan exiles\Conan Exiles Enhanced\ConanSandbox\Binaries\Win64\ConanSandbox-Win64-Shipping.exe")]
    [InlineData(@"E:\CSC\server\start.cmd")]
    [InlineData(@"E:\CSC\server\start.ps1")]
    [InlineData(@"E:\CSC\server\ConanSandbox\Binaries\Win64\ConanSandboxServer-Win64-Shipping.exe")]
    [InlineData(@"E:\CSC\server\notepad.exe")]
    [InlineData("")]
    public void Gate_blocks_everything_that_is_not_ConanSandboxServer_exe(string exe)
    {
        var result = ServerExecutableGate.Evaluate(exe, ClientRoot);

        Assert.False(result.Allowed);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public void Gate_allows_ConanSandboxServer_exe_outside_client_root()
    {
        var result = ServerExecutableGate.Evaluate(@"E:\CSC\server\ConanSandbox\Binaries\Win64\ConanSandboxServer.exe", ClientRoot);

        Assert.True(result.Allowed);
        Assert.Equal(ConanExecutableKind.DedicatedServer, result.Kind);
    }

    [Theory]
    [InlineData(@"D:\conan exiles\Conan Exiles Enhanced")]
    [InlineData(@"D:\conan exiles\Conan Exiles Enhanced\")]
    [InlineData(@"d:\CONAN EXILES\conan exiles enhanced\")]
    [InlineData(@"D:/conan exiles/Conan Exiles Enhanced/")]
    public void Gate_blocks_server_named_exe_inside_client_root_regardless_of_trailing_separator(string clientRoot)
    {
        var result = ServerExecutableGate.Evaluate(@"D:\conan exiles\Conan Exiles Enhanced\ConanSandboxServer.exe", clientRoot);

        Assert.False(result.Allowed);
        Assert.Contains("standalone client", result.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Run Me!.bat")]
    [InlineData("ConanSandbox.exe")]
    public async Task Process_manager_blocks_start_of_client_files_before_launching(string fileName)
    {
        var temp = Path.Combine(Path.GetTempPath(), "csc-m3-gate", Guid.NewGuid().ToString("n"));
        var client = Directory.CreateDirectory(Path.Combine(temp, "conan exiles", "Conan Exiles Enhanced")).FullName;
        var target = fileName == "Run Me!.bat"
            ? Path.Combine(Path.GetDirectoryName(client)!, fileName)
            : Path.Combine(client, fileName);
        await File.WriteAllTextAsync(target, "client");

        var (_, _, settings) = QaTestSupport.CreateData();
        await settings.UpdateAsync(s =>
        {
            s.ServerPaths.ServerExecutablePath = target;
            s.ServerPaths.ServerWorkingDirectory = Path.GetDirectoryName(target);
        });
        var starter = new CountingStarter();
        var manager = new ServerProcessManager(
            settings,
            starter,
            new ServerActionGate(),
            new MemoryActivityLog(),
            new FakeRcon(),
            new ImmediateReadyProbe(),
            NullLogger<ServerProcessManager>.Instance);

        var error = await Record.ExceptionAsync(() => manager.StartAsync());

        var facing = Assert.IsType<UserFacingException>(error);
        Assert.Equal("Server start blocked", facing.Title);
        Assert.Equal(0, starter.Starts);
        Assert.Equal(ServerStatus.Offline, manager.State.Status);
    }

    [Fact]
    public async Task Process_manager_blocks_server_named_exe_inside_configured_client_root_with_trailing_slash()
    {
        var temp = Path.Combine(Path.GetTempPath(), "csc-m3-gate", Guid.NewGuid().ToString("n"));
        var client = Directory.CreateDirectory(Path.Combine(temp, "Conan Exiles Enhanced")).FullName;
        var exe = Path.Combine(client, "ConanSandboxServer.exe");
        await File.WriteAllTextAsync(exe, "renamed");
        var (_, _, settings) = QaTestSupport.CreateData();
        await settings.UpdateAsync(s =>
        {
            s.Client.RootDirectory = client + Path.DirectorySeparatorChar;
            s.ServerPaths.ServerExecutablePath = exe;
        });
        var starter = new CountingStarter();
        var manager = new ServerProcessManager(
            settings, starter, new ServerActionGate(), new MemoryActivityLog(), new FakeRcon(),
            new ImmediateReadyProbe(), NullLogger<ServerProcessManager>.Instance);

        var error = await Record.ExceptionAsync(() => manager.StartAsync());

        Assert.IsType<UserFacingException>(error);
        Assert.Equal(0, starter.Starts);
    }

    [Fact]
    public async Task Diagnostics_reports_start_blocked_for_shipping_binary()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var install = await h.ConfigureServerAsync();
        var shipping = Path.Combine(install, @"ConanSandbox\Binaries\Win64\ConanSandboxServer-Win64-Shipping.exe");
        await File.WriteAllTextAsync(shipping, "x");
        await h.Settings.UpdateAsync(s => s.ServerPaths.ServerExecutablePath = shipping);

        var report = await h.Service.RunAsync();

        var check = report.Find(DiagnosticCheckIds.ServerExecutable)!;
        Assert.Equal(DiagnosticStatus.Fail, check.Status);
        Assert.Equal("NO", check.Facts["StartAllowed"]);
    }

    // ------------------------------------------------------------ QA-018 normalization

    [Theory]
    [InlineData(@"D:\conan exiles\Conan Exiles Enhanced", @"D:\conan exiles\Conan Exiles Enhanced\")]
    [InlineData(@"D:\conan exiles\Conan Exiles Enhanced\", @"D:\conan exiles\Conan Exiles Enhanced")]
    [InlineData(@"D:\conan exiles\Conan Exiles Enhanced", @"d:\Conan Exiles\conan exiles enhanced")]
    [InlineData(@"D:/conan exiles/Conan Exiles Enhanced/", @"D:\conan exiles\Conan Exiles Enhanced")]
    public void Trailing_separator_and_case_do_not_change_path_identity(string a, string b)
    {
        Assert.True(PathValidator.PathsEqual(a, b));
        Assert.True(PathValidator.IsUnderRoot(a, b));
        Assert.True(PathValidator.IsUnderRoot(b, a));
        Assert.True(PathValidator.Overlaps(a, b));
    }

    [Fact]
    public void Sibling_with_common_prefix_is_not_under_root()
    {
        Assert.False(PathValidator.IsUnderRoot(@"D:\conan exiles\Conan Exiles Enhanced2", @"D:\conan exiles\Conan Exiles Enhanced\"));
        Assert.True(PathValidator.IsUnderRoot(@"D:\conan exiles\Conan Exiles Enhanced\ConanSandbox", @"D:\conan exiles\Conan Exiles Enhanced\"));
    }

    [Fact]
    public void Drive_root_keeps_its_separator_when_normalized()
    {
        Assert.Equal(@"C:\", PathValidator.NormalizeFullPath(@"C:\"));
        Assert.True(PathValidator.IsUnderRoot(@"C:\x", @"C:\"));
    }

    [Fact]
    public async Task Diagnostics_treat_client_root_with_trailing_separator_as_the_known_client()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var clientRoot = h.CreateClient();
        await h.Settings.UpdateAsync(s =>
        {
            s.Client.RootDirectory = clientRoot + Path.DirectorySeparatorChar;
            s.ServerPaths.ServerInstallDirectory = clientRoot;
        });

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ClientExecutable)!.Status);
        Assert.Equal(DiagnosticStatus.Fail, report.Find(DiagnosticCheckIds.ServerWorkspace)!.Status);
    }

    // ------------------------------------------------------------ Live-test guard

    [Fact]
    public void Live_layout_with_separate_children_outside_client_is_safe()
    {
        var layout = LiveTestGuard.CreateLayout(@"E:\CSC-M3-Live\");

        var problems = LiveTestGuard.ValidateLayout(layout, [ClientRoot, @"D:\conan exiles"]);

        Assert.Empty(problems);
        Assert.Equal(@"E:\CSC-M3-Live", layout.Root);
        Assert.Equal(@"E:\CSC-M3-Live\steamcmd", layout.SteamCmd);
        Assert.Equal(@"E:\CSC-M3-Live\server", layout.Server);
    }

    [Theory]
    [InlineData(@"D:\conan exiles\CSC")]
    [InlineData(@"D:\conan exiles\Conan Exiles Enhanced\server")]
    [InlineData(@"D:\conan exiles\")]
    [InlineData(@"D:\")]
    [InlineData(@"E:\")]
    public void Live_layout_overlapping_client_or_drive_root_is_rejected(string root)
    {
        var layout = LiveTestGuard.CreateLayout(root);

        var problems = LiveTestGuard.ValidateLayout(layout, [ClientRoot, @"D:\conan exiles"]);

        Assert.NotEmpty(problems);
    }

    [Fact]
    public void Live_layout_with_nested_steamcmd_and_server_is_rejected()
    {
        var layout = new LiveTestLayout(
            @"E:\CSC-M3-Live",
            @"E:\CSC-M3-Live\server\steamcmd",
            @"E:\CSC-M3-Live\server",
            @"E:\CSC-M3-Live\live-test",
            @"E:\CSC-M3-Live\app-data");

        var problems = LiveTestGuard.ValidateLayout(layout, [ClientRoot]);

        Assert.Contains(problems, p => p.Contains("overlap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Live_operations_require_both_env_variable_and_marker()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "csc-m3-guard", Guid.NewGuid().ToString("n"))).FullName;

        Assert.Throws<InvalidOperationException>(() => LiveTestGuard.EnsureLiveAllowed(null, root));
        Assert.Throws<InvalidOperationException>(() => LiveTestGuard.EnsureLiveAllowed("1", root));
        File.WriteAllText(Path.Combine(root, LiveTestGuard.MarkerFileName), "m3");
        Assert.Throws<InvalidOperationException>(() => LiveTestGuard.EnsureLiveAllowed("0", root));
        Assert.Throws<InvalidOperationException>(() => LiveTestGuard.EnsureLiveAllowed(null, root));

        LiveTestGuard.EnsureLiveAllowed("1", root);
    }

    [Fact]
    public void Destructive_actions_are_refused_outside_marked_workspace()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "csc-m3-guard", Guid.NewGuid().ToString("n"))).FullName;
        var inside = Path.Combine(root, "server", "ConanSandbox");

        Assert.Throws<InvalidOperationException>(() => LiveTestGuard.EnsureInsideMarkedWorkspace(inside, root));
        File.WriteAllText(Path.Combine(root, LiveTestGuard.MarkerFileName), "m3");

        LiveTestGuard.EnsureInsideMarkedWorkspace(inside, root);
        LiveTestGuard.EnsureInsideMarkedWorkspace(inside, root + Path.DirectorySeparatorChar);
        Assert.Throws<InvalidOperationException>(() => LiveTestGuard.EnsureInsideMarkedWorkspace(root, root));
        Assert.Throws<InvalidOperationException>(() => LiveTestGuard.EnsureInsideMarkedWorkspace(ClientRoot, root));
        Assert.Throws<InvalidOperationException>(() => LiveTestGuard.EnsureInsideMarkedWorkspace(Path.GetTempPath(), root));
    }

    // ------------------------------------------------------------ External server directory (sibling of client)

    [Theory]
    [InlineData(@"D:\conan exiles\Conan Exiles Dedicated Server")]
    [InlineData(@"D:\conan exiles\Conan Exiles Dedicated Server\")]
    [InlineData(@"F:\ConanServer")]
    public void External_server_directory_sibling_of_client_is_allowed(string serverDir)
    {
        var layout = LiveTestGuard.CreateLayout(@"E:\CSC-M3-Live");

        Assert.Empty(LiveTestGuard.ValidateExternalServerDirectory(serverDir, layout, ClientRoot));
        Assert.Empty(LiveTestGuard.ValidateExternalServerDirectory(serverDir, layout, ClientRoot + @"\"));
    }

    [Theory]
    [InlineData(@"D:\conan exiles\Conan Exiles Enhanced")]
    [InlineData(@"D:\conan exiles\Conan Exiles Enhanced\Server")]
    [InlineData(@"D:\conan exiles")]
    [InlineData(@"D:\conan exiles\")]
    [InlineData(@"D:\")]
    [InlineData(@"E:\CSC-M3-Live\steamcmd\server")]
    [InlineData(@"E:\CSC-M3-Live\app-data")]
    public void External_server_directory_overlapping_client_launcher_or_workspace_is_rejected(string serverDir)
    {
        var layout = LiveTestGuard.CreateLayout(@"E:\CSC-M3-Live");

        Assert.NotEmpty(LiveTestGuard.ValidateExternalServerDirectory(serverDir, layout, ClientRoot));
    }

    [Fact]
    public void External_server_directory_with_client_or_unknown_files_is_rejected_but_existing_install_is_allowed()
    {
        var temp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "csc-m3-extserver", Guid.NewGuid().ToString("n"))).FullName;
        var client = Path.Combine(temp, "conan exiles", "Conan Exiles Enhanced");
        var layout = LiveTestGuard.CreateLayout(Path.Combine(temp, "workspace"));

        var withClientFile = Directory.CreateDirectory(Path.Combine(temp, "conan exiles", "WithClient")).FullName;
        File.WriteAllText(Path.Combine(withClientFile, "Run Me!.bat"), "launcher");
        Assert.Contains(LiveTestGuard.ValidateExternalServerDirectory(withClientFile, layout, client),
            p => p.Contains("Run Me!.bat", StringComparison.Ordinal));

        var unknown = Directory.CreateDirectory(Path.Combine(temp, "conan exiles", "Unknown")).FullName;
        File.WriteAllText(Path.Combine(unknown, "notes.txt"), "user file");
        Assert.Contains(LiveTestGuard.ValidateExternalServerDirectory(unknown, layout, client),
            p => p.Contains("not empty", StringComparison.Ordinal));

        var empty = Directory.CreateDirectory(Path.Combine(temp, "conan exiles", "Empty")).FullName;
        Assert.Empty(LiveTestGuard.ValidateExternalServerDirectory(empty, layout, client));

        var installed = Directory.CreateDirectory(Path.Combine(temp, "conan exiles", "Conan Exiles Dedicated Server")).FullName;
        Directory.CreateDirectory(Path.Combine(installed, "steamapps"));
        File.WriteAllText(Path.Combine(installed, "steamapps", "appmanifest_443030.acf"), "\"AppState\" {}");
        Assert.Empty(LiveTestGuard.ValidateExternalServerDirectory(installed, layout, client));
    }

    [Fact]
    public async Task Diagnostics_and_gate_accept_server_installed_as_sibling_of_the_client()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var clientRoot = h.CreateClient();
        var launcher = Path.GetDirectoryName(clientRoot)!;
        var server = Directory.CreateDirectory(Path.Combine(launcher, "Conan Exiles Dedicated Server")).FullName;
        Directory.CreateDirectory(Path.Combine(server, "ConanSandbox", "Binaries", "Win64"));
        var exe = Path.Combine(server, "ConanSandboxServer.exe");
        await File.WriteAllTextAsync(exe, "server");
        await h.Settings.UpdateAsync(s =>
        {
            s.Client.RootDirectory = clientRoot + Path.DirectorySeparatorChar;
            s.ServerPaths.ServerInstallDirectory = server;
            s.ServerPaths.ServerExecutablePath = exe;
        });

        var report = await h.Service.RunAsync();

        Assert.True(ServerExecutableGate.Evaluate(exe, clientRoot).Allowed);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ServerExecutable)!.Status);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ServerWorkspace)!.Status);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ClientExecutable)!.Status);
    }

    [Fact]
    public async Task Throwing_state_subscriber_does_not_abort_start_or_stop()
    {
        var (_, _, settings) = QaTestSupport.CreateData();
        await QaTestSupport.ConfigureInstallAsync(settings, Path.Combine(Path.GetTempPath(), "csc-m3-sub", Guid.NewGuid().ToString("n")));
        await settings.UpdateAsync(s =>
        {
            s.Advanced.GracefulStopTimeoutSeconds = 1;
            s.Advanced.ForceStopTimeoutSeconds = 1;
            s.Advanced.ReadinessPollIntervalMilliseconds = 20;
        });
        var manager = new ServerProcessManager(
            settings, new FakeProcessStarter { Settings = settings }, new ServerActionGate(), new MemoryActivityLog(),
            new FakeRcon(), new ImmediateReadyProbe(), NullLogger<ServerProcessManager>.Instance);
        var seen = new List<ServerStatus>();
        manager.StateChanged += (_, _) => throw new ArgumentOutOfRangeException("index", "observer bug");
        manager.StateChanged += (_, state) => seen.Add(state.Status);

        await manager.StartAsync();
        Assert.Equal(ServerStatus.Online, manager.State.Status);

        await manager.StopAsync();
        Assert.Equal(ServerStatus.Offline, manager.State.Status);
        Assert.Contains(ServerStatus.Stopping, seen);
    }

    // ------------------------------------------------------------ Graceful stop command (live finding)

    [Fact]
    public async Task Graceful_stop_sends_rcon_shutdown_not_doexit_and_does_not_kill()
    {
        var (_, _, settings) = QaTestSupport.CreateData();
        await QaTestSupport.ConfigureInstallAsync(settings, Path.Combine(Path.GetTempPath(), "csc-m3-shutdown", Guid.NewGuid().ToString("n")));
        await settings.UpdateAsync(s =>
        {
            s.Rcon.Enabled = true;
            s.Advanced.ReadinessPollIntervalMilliseconds = 20;
        });
        await settings.UpdateSecretsAsync(s => s.RconPassword = "test-only-rcon");
        var starter = new FakeProcessStarter { Settings = settings };
        var rcon = new ShutdownAwareRcon(() => starter.Last.CloseMainWindow());
        var manager = new ServerProcessManager(
            settings, starter, new ServerActionGate(), new MemoryActivityLog(), rcon, new ImmediateReadyProbe(),
            NullLogger<ServerProcessManager>.Instance);
        await manager.StartAsync();
        var clock = System.Diagnostics.Stopwatch.StartNew();

        await manager.StopAsync();

        Assert.Equal(ServerStatus.Offline, manager.State.Status);
        Assert.Contains("shutdown", rcon.Commands);
        Assert.DoesNotContain("DoExit", rcon.Commands);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"Stop took {clock.Elapsed}; it should finish when the server exits on 'shutdown'.");
        Assert.Equal(300, new Core.Settings.AdvancedSettings().GracefulStopTimeoutSeconds);
        Assert.Equal("shutdown", new Core.Settings.RconSettings().ShutdownCommand);
    }

    private sealed class ShutdownAwareRcon(Action onShutdown) : Core.Abstractions.IRconService
    {
        public List<string> Commands { get; } = new();

        public bool IsConnected => true;

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            if (command == "shutdown")
            {
                onShutdown();
            }

            return Task.FromResult("Successfully executed: " + command);
        }

        public Task AnnounceAsync(string message, CancellationToken cancellationToken = default)
        {
            Commands.Add("broadcast " + message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PlayerInfo>> GetPlayersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlayerInfo>>(Array.Empty<PlayerInfo>());
    }

    // ------------------------------------------------------------ Readiness: world loaded, not just port bound (live finding)

    [Theory]
    [InlineData(0, false)]
    [InlineData(57, true)]
    public async Task Port_bound_is_not_ready_while_the_current_run_log_is_still_on_frame_zero(int frame, bool expectedReady)
    {
        using var udp = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var port = ((System.Net.IPEndPoint)udp.Client.LocalEndPoint!).Port;
        var started = DateTimeOffset.UtcNow.AddSeconds(-10);
        var (settings, _) = await ProbeSettingsAsync(port,
            $"[{Stamp(started.AddSeconds(5))}][  0]LogNet: IpNetDriver listening on port {port}",
            $"[{Stamp(started.AddSeconds(8))}][{frame,3}]LogServerStats: something");
        var probe = new Infrastructure.Health.EndpointServerReadinessProbe(settings, new FakeRcon(), NullLogger<Infrastructure.Health.EndpointServerReadinessProbe>.Instance);

        var result = await probe.ProbeAsync(new Core.Abstractions.ServerReadinessContext { GamePort = port, ProcessId = 1, StartedAt = started });

        Assert.Equal(expectedReady, result.IsReady);
        Assert.Contains(expectedReady ? "ticking" : "still loading", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stale_log_from_a_previous_run_does_not_block_port_readiness()
    {
        using var udp = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var port = ((System.Net.IPEndPoint)udp.Client.LocalEndPoint!).Port;
        var started = DateTimeOffset.UtcNow;
        var (settings, _) = await ProbeSettingsAsync(port, $"[{Stamp(started.AddHours(-1))}][  0]LogInit: old run still loading");
        var probe = new Infrastructure.Health.EndpointServerReadinessProbe(settings, new FakeRcon(), NullLogger<Infrastructure.Health.EndpointServerReadinessProbe>.Instance);

        var result = await probe.ProbeAsync(new Core.Abstractions.ServerReadinessContext { GamePort = port, ProcessId = 1, StartedAt = started });

        Assert.True(result.IsReady);
    }

    [Fact]
    public async Task Rcon_reply_is_not_ready_while_the_world_is_still_loading()
    {
        var started = DateTimeOffset.UtcNow.AddSeconds(-10);
        var (settings, _) = await ProbeSettingsAsync(1, $"[{Stamp(started.AddSeconds(5))}][  0]LogRcon: Display: Rcon is ready for client connections");
        await settings.UpdateSecretsAsync(s => s.RconPassword = "test-only-rcon");
        await settings.UpdateAsync(s => s.Rcon.Enabled = true);
        var probe = new Infrastructure.Health.EndpointServerReadinessProbe(settings, new FakeRcon(), NullLogger<Infrastructure.Health.EndpointServerReadinessProbe>.Instance);

        var result = await probe.ProbeAsync(new Core.Abstractions.ServerReadinessContext { GamePort = 1, ProcessId = 1, StartedAt = started });

        Assert.False(result.IsReady);
    }

    private static async Task<(Infrastructure.Settings.JsonSettingsService Settings, string Install)> ProbeSettingsAsync(int gamePort, params string[] logLines)
    {
        var (_, _, settings) = QaTestSupport.CreateData();
        var install = Path.Combine(Path.GetTempPath(), "csc-m3-probe", Guid.NewGuid().ToString("n"));
        var logs = Directory.CreateDirectory(Path.Combine(install, "ConanSandbox", "Saved", "Logs")).FullName;
        await File.WriteAllLinesAsync(Path.Combine(logs, "ConanSandbox.log"), logLines);
        await settings.UpdateAsync(s =>
        {
            s.ServerPaths.ServerInstallDirectory = install;
            s.Server.GamePort = gamePort;
        });
        return (settings, install);
    }

    private static string Stamp(DateTimeOffset utc) =>
        utc.UtcDateTime.ToString("yyyy.MM.dd-HH.mm.ss:fff", System.Globalization.CultureInfo.InvariantCulture);

    // ------------------------------------------------------------ Private friends-only deployment (Radmin VPN)

    [Fact]
    public async Task Radmin_vpn_address_is_reported_as_the_friends_direct_connect_target()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        h.Network.RadminIp = "26.84.226.21";

        var report = await h.Service.RunAsync();

        var check = report.Find(DiagnosticCheckIds.NetworkPrivateVpn)!;
        Assert.Equal(DiagnosticStatus.Pass, check.Status);
        Assert.Equal("26.84.226.21:7777", check.Facts["FriendsDirectConnect"]);
        Assert.Equal("not required", check.Facts["PublicServerBrowserRegistration"]);
    }

    [Fact]
    public async Task Missing_radmin_vpn_warns_but_never_blocks_server_readiness()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureServerAsync();
        h.Network.RadminIp = null;

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Warning, report.Find(DiagnosticCheckIds.NetworkPrivateVpn)!.Status);
        Assert.True(report.ServerLiveTest.IsReady, string.Join(" | ", report.ServerLiveTest.Blockers));
        Assert.DoesNotContain(report.ServerLiveTest.Blockers, b => b.Contains("register", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Rcon_exposure_warns_that_conan_listens_on_all_interfaces_and_app_uses_loopback()
    {
        var h = await DiagnosticsHarness.CreateAsync();

        var enabled = await h.Service.RunAsync();
        var check = enabled.Find(DiagnosticCheckIds.RconExposure)!;
        Assert.Equal(DiagnosticStatus.Warning, check.Status);
        Assert.Equal("127.0.0.1:25575", check.Facts["AppConnectsTo"]);
        Assert.Contains("port-forward", check.SuggestedAction, StringComparison.OrdinalIgnoreCase);

        await h.Settings.UpdateAsync(s => s.Rcon.Enabled = false);
        var disabled = await h.Service.RunAsync();
        Assert.Equal(DiagnosticStatus.NotConfigured, disabled.Find(DiagnosticCheckIds.RconExposure)!.Status);
    }

    private sealed class CountingStarter : Core.Abstractions.IProcessStarter
    {
        public int Starts { get; private set; }

        public Core.Abstractions.IManagedProcess Start(Core.Abstractions.ProcessStartRequest request)
        {
            Starts++;
            throw new InvalidOperationException("Must not launch in this test.");
        }
    }
}
