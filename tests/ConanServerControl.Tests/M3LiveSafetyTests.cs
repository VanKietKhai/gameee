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
