using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Concurrency;
using ConanServerControl.Infrastructure.Health;
using ConanServerControl.Infrastructure.ProcessManagement;
using ConanServerControl.Infrastructure.Rcon;
using ConanServerControl.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

/// <summary>
/// Pre-4E validation of the graceful-stop design and of network address selection for the private
/// friends-only Radmin VPN deployment.
/// </summary>
public class M3PreE4StopAndNetworkTests
{
    private const string Acknowledged = "Successfully executed: shutdown";

    // ------------------------------------------------------------ Network: Radmin vs physical LAN

    private static NetworkAdapterSnapshot Adapter(
        string name, string description, string? ipv4, bool up = true, bool gateway = true,
        NetworkInterfaceType type = NetworkInterfaceType.Ethernet) =>
        new(name, description, type, up ? OperationalStatus.Up : OperationalStatus.Down,
            ipv4 is null ? Array.Empty<IPAddress>() : [IPAddress.Parse(ipv4)], gateway);

    /// <summary>The adapter set observed live on the host PC (2026-10-02).</summary>
    private static NetworkAdapterSnapshot[] HostAdapters() =>
    [
        Adapter("Radmin VPN", "Famatech Radmin VPN Ethernet Adapter", "26.84.226.21"),
        Adapter("Ethernet", "Realtek PCIe GbE Family Controller", "192.168.0.244"),
        Adapter("Ethernet 2", "NW TAP-Win32 Adapter V9.21", "169.254.74.222", up: false, gateway: false),
        Adapter("Bluetooth Network Connection 2", "Bluetooth Device (Personal Area Network) #2", "169.254.127.216", up: false, gateway: false),
        Adapter("Loopback Pseudo-Interface 1", "Software Loopback Interface 1", "127.0.0.1", gateway: false, type: NetworkInterfaceType.Loopback),
        Adapter("Teredo Tunneling Pseudo-Interface", "Microsoft Teredo Tunneling Adapter", null, gateway: false, type: NetworkInterfaceType.Tunnel)
    ];

    [Fact]
    public void Radmin_adapter_is_reported_separately_from_the_physical_lan()
    {
        var network = new NetworkInfoService(HostAdapters);

        Assert.Equal("192.168.0.244", network.GetLanIPv4());
        Assert.Equal("26.84.226.21", network.GetRadminVpnIPv4());

        // Adapter order does not matter: Radmin listed first or last gives the same answer.
        var reversed = new NetworkInfoService(() => HostAdapters().Reverse().ToArray());
        Assert.Equal("192.168.0.244", reversed.GetLanIPv4());
        Assert.Equal("26.84.226.21", reversed.GetRadminVpnIPv4());
    }

    [Fact]
    public void Unrelated_vpn_and_virtual_adapters_are_never_labelled_radmin_or_physical_lan()
    {
        NetworkAdapterSnapshot[] adapters =
        [
            Adapter("Tailscale", "Tailscale Tunnel", "100.101.12.7", type: NetworkInterfaceType.Unknown),
            Adapter("ZeroTier One [abc]", "ZeroTier Virtual Port", "10.147.17.5"),
            Adapter("Hamachi", "LogMeIn Hamachi Virtual Ethernet Adapter", "25.12.34.56"),
            Adapter("wg0", "WireGuard Tunnel", "10.6.0.2"),
            Adapter("Ethernet 3", "TAP-Windows Adapter V9", "10.8.0.6"),
            Adapter("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter", "172.25.16.1"),
            // Renamed by the user to look like Radmin; the driver says otherwise.
            Adapter("Radmin VPN", "WireGuard Tunnel #2", "26.1.2.3"),
            Adapter("Wi-Fi", "Intel(R) Wi-Fi 6 AX201 160MHz", "192.168.1.20", type: NetworkInterfaceType.Wireless80211)
        ];
        var network = new NetworkInfoService(() => adapters);

        Assert.Null(network.GetRadminVpnIPv4());
        Assert.Equal("192.168.1.20", network.GetLanIPv4());

        var vpnOnly = new NetworkInfoService(() => adapters[..^1]);
        Assert.Null(vpnOnly.GetLanIPv4());
        Assert.Null(vpnOnly.GetRadminVpnIPv4());
    }

    [Fact]
    public void No_usable_radmin_adapter_is_not_detected_and_no_address_is_invented()
    {
        NetworkAdapterSnapshot physical = Adapter("Ethernet", "Realtek PCIe GbE Family Controller", "192.168.0.244");

        Assert.Null(new NetworkInfoService(() => [physical]).GetRadminVpnIPv4());
        Assert.Null(new NetworkInfoService(() =>
            [physical, Adapter("Radmin VPN", "Famatech Radmin VPN Ethernet Adapter", "26.84.226.21", up: false)]).GetRadminVpnIPv4());
        Assert.Null(new NetworkInfoService(() =>
            [physical, Adapter("Radmin VPN", "Famatech Radmin VPN Ethernet Adapter", "169.254.10.20")]).GetRadminVpnIPv4());
        Assert.Null(new NetworkInfoService(() => throw new NetworkInformationException()).GetRadminVpnIPv4());
    }

    [Fact]
    public void Radmin_prefers_its_26_network_address_when_the_adapter_has_several()
    {
        var radmin = new NetworkAdapterSnapshot("Radmin VPN", "Famatech Radmin VPN Ethernet Adapter", NetworkInterfaceType.Ethernet,
            OperationalStatus.Up, [IPAddress.Parse("169.254.1.1"), IPAddress.Parse("10.0.0.5"), IPAddress.Parse("26.84.226.21")], true);

        Assert.Equal("26.84.226.21", NetworkAddressSelector.SelectRadminIPv4([radmin]));
    }

    // ------------------------------------------------------------ Graceful stop design

    [Theory]
    [InlineData("Successfully executed: shutdown", true)]
    [InlineData("successfully executed: Shutdown", true)]
    [InlineData("Couldn't find the command: exit", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_a_successful_execution_reply_acknowledges_the_shutdown(string? reply, bool acknowledged)
    {
        Assert.Equal(acknowledged, ServerProcessManager.IsShutdownAcknowledged(reply));
    }

    [Fact]
    public async Task Acknowledged_shutdown_gets_the_extended_window_and_is_not_killed()
    {
        var h = await StopHarness.CreateAsync(rconConfigured: true);
        h.Rcon.Reply = Acknowledged;
        h.Rcon.OnShutdown = () => h.Process.ExitAfter(TimeSpan.FromSeconds(2.5)); // longer than the 1 s short window

        await h.Manager.StartAsync();
        await h.Manager.StopAsync();

        var stop = h.Manager.State.LastStop!;
        Assert.Equal(ServerStatus.Offline, h.Manager.State.Status);
        Assert.True(stop.ShutdownAcknowledged);
        Assert.True(stop.ExtendedWindowUsed);
        Assert.Equal(300, stop.GracefulWindowSeconds);
        Assert.False(stop.ForcedKill);
        Assert.Equal(0, h.Process.KillCalls);
        Assert.Equal(0, stop.ExitCode);
        Assert.NotNull(stop.ProcessTreeExitedAt);
    }

    [Fact]
    public async Task Shutdown_progress_in_the_server_log_gets_the_extended_window_without_an_rcon_reply()
    {
        var h = await StopHarness.CreateAsync(rconConfigured: true);
        h.Rcon.Throw = new TimeoutException("RCON did not answer within 5 s.");
        h.Rcon.OnShutdown = () =>
        {
            h.Probe.Evidence = "[2026.10.01-22.38.12:551][470]LogCore: Engine exit requested (reason: GenericPlatform RequestExit)";
            h.Process.ExitAfter(TimeSpan.FromSeconds(2.5));
        };

        await h.Manager.StartAsync();
        await h.Manager.StopAsync();

        var stop = h.Manager.State.LastStop!;
        Assert.Equal(ServerStatus.Offline, h.Manager.State.Status);
        Assert.False(stop.ShutdownAcknowledged);
        Assert.Contains("Engine exit requested", stop.ShutdownProgressEvidence);
        Assert.True(stop.ExtendedWindowUsed);
        Assert.False(stop.ForcedKill);
        Assert.Equal(0, h.Process.KillCalls);
    }

    public static TheoryData<string> UnresponsiveCases => new() { "no-reply", "rejected", "no-rcon" };

    [Theory]
    [MemberData(nameof(UnresponsiveCases))]
    public async Task Unresponsive_shutdown_does_not_wait_the_extended_window(string scenario)
    {
        var h = await StopHarness.CreateAsync(rconConfigured: scenario != "no-rcon");
        if (scenario == "no-reply")
        {
            h.Rcon.Throw = new TimeoutException("RCON did not answer within 5 s.");
        }
        else
        {
            h.Rcon.Reply = "Couldn't find the command: shutdown";
        }

        await h.Manager.StartAsync();
        var clock = Stopwatch.StartNew();
        await h.Manager.StopAsync();

        var stop = h.Manager.State.LastStop!;
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(15), $"{scenario}: stop took {clock.Elapsed} with a 300 s window and a 600 s ceiling.");
        Assert.False(stop.ExtendedWindowUsed);
        Assert.Equal(1, stop.GracefulWindowSeconds);
        Assert.Equal(0, stop.EmergencyCeilingSeconds);
        Assert.False(stop.GracefulWindowExceeded);
        Assert.True(stop.ForcedKill);
        Assert.Equal(1, h.Process.KillCalls);
        Assert.Equal(ServerStatus.Offline, h.Manager.State.Status);
    }

    [Fact]
    public async Task Forced_kill_remains_the_final_fallback_at_the_emergency_ceiling_after_an_acknowledged_shutdown_that_never_exits()
    {
        var h = await StopHarness.CreateAsync(rconConfigured: true, gracefulSeconds: 5, ceilingSeconds: 8);
        h.Rcon.Reply = Acknowledged; // acknowledged, but the process hangs in its exit sequence

        await h.Manager.StartAsync();
        var clock = Stopwatch.StartNew();
        await h.Manager.StopAsync();

        var stop = h.Manager.State.LastStop!;
        Assert.True(stop.ExtendedWindowUsed);
        Assert.Equal(5, stop.GracefulWindowSeconds);
        Assert.Equal(8, stop.EmergencyCeilingSeconds);
        Assert.True(stop.GracefulWindowExceeded);
        Assert.True(clock.Elapsed >= TimeSpan.FromSeconds(7.5), $"Killed after {clock.Elapsed}, before the emergency ceiling.");
        Assert.True(stop.ForcedKill);
        Assert.Equal(1, h.Process.KillCalls);
        Assert.Equal(ServerStatus.Offline, h.Manager.State.Status);
    }

    // ------------------------------------------------------------ 2026-10-03 policy: graceful window vs emergency ceiling

    [Fact]
    public async Task Acknowledged_shutdown_that_outlasts_the_graceful_window_finishes_without_a_kill()
    {
        // Live 2026-10-03: two clean shutdowns were still exiting at 300 s and were killed mid-exit.
        var h = await StopHarness.CreateAsync(rconConfigured: true, gracefulSeconds: 5, ceilingSeconds: 15);
        h.Rcon.Reply = Acknowledged;
        h.Rcon.OnShutdown = () => h.Process.ExitAfter(TimeSpan.FromSeconds(7)); // past the 5 s window, before the ceiling

        await h.Manager.StartAsync();
        await h.Manager.StopAsync();

        var stop = h.Manager.State.LastStop!;
        Assert.Equal(ServerStatus.Offline, h.Manager.State.Status);
        Assert.True(stop.ExtendedWindowUsed);
        Assert.True(stop.GracefulWindowExceeded);
        Assert.Equal(5, stop.GracefulWindowSeconds);
        Assert.Equal(15, stop.EmergencyCeilingSeconds);
        Assert.False(stop.ForcedKill);
        Assert.Equal(0, h.Process.KillCalls);
        Assert.Equal(0, stop.ExitCode);
    }

    [Fact]
    public async Task Log_progress_without_an_rcon_reply_also_continues_past_the_graceful_window()
    {
        var h = await StopHarness.CreateAsync(rconConfigured: true, gracefulSeconds: 5, ceilingSeconds: 15);
        h.Rcon.Throw = new TimeoutException("RCON did not answer within 5 s.");
        h.Rcon.OnShutdown = () =>
        {
            h.Probe.Evidence = "[2026.10.03-17.42.41:027][934]LogCore: Engine exit requested (reason: GenericPlatform RequestExit)";
            h.Process.ExitAfter(TimeSpan.FromSeconds(7));
        };

        await h.Manager.StartAsync();
        await h.Manager.StopAsync();

        var stop = h.Manager.State.LastStop!;
        Assert.False(stop.ShutdownAcknowledged);
        Assert.Contains("Engine exit requested", stop.ShutdownProgressEvidence);
        Assert.True(stop.GracefulWindowExceeded);
        Assert.False(stop.ForcedKill);
        Assert.Equal(0, h.Process.KillCalls);
        Assert.Equal(ServerStatus.Offline, h.Manager.State.Status);
    }

    [Fact]
    public async Task Unacknowledged_shutdown_without_progress_never_waits_for_the_emergency_ceiling()
    {
        var h = await StopHarness.CreateAsync(rconConfigured: true, gracefulSeconds: 5, ceilingSeconds: 600);
        h.Rcon.Throw = new TimeoutException("RCON did not answer within 5 s."); // no reply, no log progress

        await h.Manager.StartAsync();
        var clock = Stopwatch.StartNew();
        await h.Manager.StopAsync();

        var stop = h.Manager.State.LastStop!;
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"Stop took {clock.Elapsed}; an unproven shutdown must use the short window.");
        Assert.False(stop.ExtendedWindowUsed);
        Assert.Equal(0, stop.EmergencyCeilingSeconds);
        Assert.False(stop.GracefulWindowExceeded);
        Assert.True(stop.ForcedKill);
        Assert.Equal(1, h.Process.KillCalls);
    }

    [Fact]
    public async Task Emergency_ceiling_below_the_graceful_window_is_raised_to_the_graceful_window()
    {
        var h = await StopHarness.CreateAsync(rconConfigured: true, gracefulSeconds: 5, ceilingSeconds: 1);
        h.Rcon.Reply = Acknowledged; // never exits

        await h.Manager.StartAsync();
        var clock = Stopwatch.StartNew();
        await h.Manager.StopAsync();

        var stop = h.Manager.State.LastStop!;
        Assert.Equal(5, stop.EmergencyCeilingSeconds);
        Assert.True(clock.Elapsed >= TimeSpan.FromSeconds(4.5), $"Killed after {clock.Elapsed}, before the graceful window.");
        Assert.True(stop.ForcedKill);
    }

    [Fact]
    public void Stop_policy_defaults_are_30_s_short_300_s_graceful_600_s_ceiling()
    {
        var advanced = new Core.Settings.AdvancedSettings();
        Assert.Equal(30, advanced.UnacknowledgedStopTimeoutSeconds);
        Assert.Equal(300, advanced.GracefulStopTimeoutSeconds);
        Assert.Equal(600, advanced.EmergencyStopCeilingSeconds);
        Assert.True(Core.LiveTesting.ModBootGates.ShutdownHighRiskSeconds < advanced.GracefulStopTimeoutSeconds);
    }

    [Fact]
    public async Task An_existing_settings_file_without_the_ceiling_key_loads_the_600_s_default()
    {
        var (_, paths, settings) = QaTestSupport.CreateData();
        Directory.CreateDirectory(Path.GetDirectoryName(paths.SettingsFilePath)!);
        await File.WriteAllTextAsync(paths.SettingsFilePath,
            """{ "advanced": { "gracefulStopTimeoutSeconds": 300, "unacknowledgedStopTimeoutSeconds": 30 } }""");

        var loaded = await settings.LoadAsync();

        Assert.Equal(300, loaded.Advanced.GracefulStopTimeoutSeconds);
        Assert.Equal(600, loaded.Advanced.EmergencyStopCeilingSeconds);
    }

    [Fact]
    public async Task Offline_is_not_reported_until_the_child_server_process_exits()
    {
        var h = await StopHarness.CreateAsync(rconConfigured: true);
        h.Rcon.Reply = Acknowledged;
        var clock = Stopwatch.StartNew();
        TimeSpan? childGone = null;
        h.Rcon.OnShutdown = () =>
        {
            // The launcher exits first (and raises Exited); the -Shipping child exits later.
            h.Process.ExitAfter(TimeSpan.FromSeconds(0.5), childDelay: TimeSpan.FromSeconds(2));
            h.Process.TreeExitedCallback = () => childGone = clock.Elapsed;
        };
        var offlineAt = new List<TimeSpan>();
        var statuses = new List<ServerStatus>();
        // A throwing observer must stay isolated on the new stop path too.
        h.Manager.StateChanged += (_, _) => throw new InvalidOperationException("observer bug");
        h.Manager.StateChanged += (_, state) =>
        {
            lock (statuses)
            {
                statuses.Add(state.Status);
                if (state.Status == ServerStatus.Offline)
                {
                    offlineAt.Add(clock.Elapsed);
                }
            }
        };

        await h.Manager.StartAsync();
        var stopping = h.Manager.StopAsync();
        await Task.Delay(1200);
        Assert.True(h.Process.HasExited, "launcher should have exited by now");
        Assert.Equal(ServerStatus.Stopping, h.Manager.State.Status);
        await stopping;

        Assert.NotNull(childGone);
        Assert.Single(offlineAt);
        Assert.True(offlineAt[0] >= childGone, $"Offline at {offlineAt[0]} before the child exited at {childGone}.");
        Assert.Equal(ServerStatus.Offline, h.Manager.State.Status);
        Assert.False(h.Manager.State.LastStop!.ForcedKill);
        Assert.Contains(ServerStatus.Stopping, statuses);
    }

    [Fact]
    public async Task A_child_that_survives_the_kill_is_an_error_never_offline()
    {
        var h = await StopHarness.CreateAsync(rconConfigured: false);
        h.Process.KillReachesChild = false;
        var statuses = new List<ServerStatus>();
        h.Manager.StateChanged += (_, state) =>
        {
            lock (statuses)
            {
                statuses.Add(state.Status);
            }
        };

        await h.Manager.StartAsync();
        var error = await Assert.ThrowsAsync<UserFacingException>(() => h.Manager.StopAsync(force: true));

        Assert.Contains("did not stop", error.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ServerStatus.Error, h.Manager.State.Status);
        Assert.DoesNotContain(ServerStatus.Offline, statuses.SkipWhile(s => s != ServerStatus.Stopping));
        Assert.True(h.Manager.State.LastStop!.ForcedKill);
        Assert.Null(h.Manager.State.LastStop.ProcessTreeExitedAt);
    }

    [Fact]
    public void Real_process_tree_is_tracked_after_the_launcher_exits_and_killed_as_a_whole()
    {
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        using var root = (SystemManagedProcess)new SystemProcessStarter().Start(new ProcessStartRequest
        {
            FileName = cmd,
            Arguments = "/c ping -n 60 127.0.0.1 >nul"
        });

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && CountChildren(root) == 0)
        {
            Thread.Sleep(100);
        }

        Assert.True(CountChildren(root) > 0, "cmd.exe should have started ping.exe");
        root.Kill(entireProcessTree: false); // the launcher dies alone; its child keeps running
        Assert.True(SpinUntil(() => root.HasExited, TimeSpan.FromSeconds(10)));
        Assert.False(root.TreeHasExited, "the orphaned child must keep the tree alive");

        root.Kill(entireProcessTree: true);
        Assert.True(SpinUntil(() => root.TreeHasExited, TimeSpan.FromSeconds(10)), "tracked child should be killed");
    }

    private static int CountChildren(SystemManagedProcess process)
    {
        process.TrackDescendants();
        return ProcessTree.GetDescendantIds(process.Id).Count;
    }

    private static bool SpinUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return condition();
    }

    [Fact]
    public async Task Rcon_command_to_a_server_that_never_answers_times_out_instead_of_hanging()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var accepted = new List<TcpClient>();
        var accepting = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    accepted.Add(await listener.AcceptTcpClientAsync());
                }
            }
            catch
            {
                // listener stopped
            }
        });

        try
        {
            var (_, _, settings) = QaTestSupport.CreateData();
            await settings.UpdateAsync(s =>
            {
                s.Rcon.Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                s.Rcon.TimeoutSeconds = 1;
            });
            await settings.UpdateSecretsAsync(s => s.RconPassword = "test-only-rcon");
            using var rcon = new RconService(settings, NullLogger<RconService>.Instance);

            var clock = Stopwatch.StartNew();
            await Assert.ThrowsAsync<TimeoutException>(() => rcon.SendCommandAsync("shutdown"));
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"RCON waited {clock.Elapsed}.");
            Assert.False(rcon.IsConnected);
        }
        finally
        {
            listener.Stop();
            await accepting;
            accepted.ForEach(c => c.Dispose());
        }
    }

    [Fact]
    public async Task Log_probe_only_counts_shutdown_lines_written_after_the_mark()
    {
        var (_, _, settings) = QaTestSupport.CreateData();
        var install = Path.Combine(Path.GetTempPath(), "csc-m3-shutdown-log", Guid.NewGuid().ToString("n"));
        var logs = Path.Combine(install, "ConanSandbox", "Saved", "Logs");
        Directory.CreateDirectory(logs);
        var log = Path.Combine(logs, "ConanSandbox.log");
        await File.WriteAllTextAsync(log,
            "[2026.10.01-21.41.07:334][ 22]LogCore: Engine exit requested (reason: GenericPlatform RequestExit)\n" +
            "[2026.10.01-21.42.08:022][ 23]LogExit: Exiting.\n");
        await settings.UpdateAsync(s => s.ServerPaths.ServerInstallDirectory = install);
        var probe = new ConanLogShutdownProbe(settings, NullLogger<ConanLogShutdownProbe>.Instance);

        var mark = probe.Mark();
        Assert.Null(probe.FindShutdownEvidence(mark)); // the previous run's exit does not count

        await File.AppendAllTextAsync(log, "[2026.10.01-22.38.12:064][456]LogRcon: Warning: Received Rcon: broadcast Server is shutting down.\n");
        Assert.Null(probe.FindShutdownEvidence(mark));

        await File.AppendAllTextAsync(log, "[2026.10.01-22.38.12:551][470]LogCore: Engine exit requested (reason: GenericPlatform RequestExit)\n");
        Assert.Contains("Engine exit requested", probe.FindShutdownEvidence(mark));

        // A rotated (shorter) log is read from its start.
        await File.WriteAllTextAsync(log, "[2026.10.01-22.40.44:067][471]LogExit: Preparing to exit.\n");
        Assert.Contains("Preparing to exit", probe.FindShutdownEvidence(mark));
    }

    // ------------------------------------------------------------ fakes

    private sealed class StopHarness
    {
        private StopHarness(ServerProcessManager manager, ScriptedProcess process, ScriptedRcon rcon, ScriptedShutdownProbe probe)
        {
            Manager = manager;
            Process = process;
            Rcon = rcon;
            Probe = probe;
        }

        public ServerProcessManager Manager { get; }

        public ScriptedProcess Process { get; }

        public ScriptedRcon Rcon { get; }

        public ScriptedShutdownProbe Probe { get; }

        public static async Task<StopHarness> CreateAsync(bool rconConfigured, int gracefulSeconds = 300, int ceilingSeconds = 600)
        {
            var (_, _, settings) = QaTestSupport.CreateData();
            await QaTestSupport.ConfigureInstallAsync(settings, Path.Combine(Path.GetTempPath(), "csc-m3-stop", Guid.NewGuid().ToString("n")));
            await settings.UpdateAsync(s =>
            {
                s.Rcon.Enabled = true;
                s.Advanced.GracefulStopTimeoutSeconds = gracefulSeconds;
                s.Advanced.EmergencyStopCeilingSeconds = ceilingSeconds;
                s.Advanced.UnacknowledgedStopTimeoutSeconds = 1;
                s.Advanced.ForceStopTimeoutSeconds = 1;
                s.Advanced.ReadinessPollIntervalMilliseconds = 20;
                s.Advanced.RestartAfterCrash = false;
            });
            if (rconConfigured)
            {
                await settings.UpdateSecretsAsync(s => s.RconPassword = "test-only-rcon");
            }

            var process = new ScriptedProcess();
            var rcon = new ScriptedRcon();
            var probe = new ScriptedShutdownProbe();
            var manager = new ServerProcessManager(
                settings, new SingleProcessStarter(process), new ServerActionGate(), new MemoryActivityLog(), rcon,
                new ImmediateReadyProbe(), NullLogger<ServerProcessManager>.Instance, probe);
            return new StopHarness(manager, process, rcon, probe);
        }
    }

    private sealed class SingleProcessStarter(ScriptedProcess process) : IProcessStarter
    {
        public IManagedProcess Start(ProcessStartRequest request) => process;
    }

    /// <summary>
    /// A launcher with one child server process. Exits only when scripted to, or when killed; it has
    /// no main window, like the real console server started with CreateNoWindow.
    /// </summary>
    private sealed class ScriptedProcess : IManagedProcess
    {
        private readonly object _sync = new();
        private DateTime? _rootExitAt;
        private DateTime? _treeExitAt;
        private bool _treeReported;

        public int Id { get; } = Random.Shared.Next(10_000, 60_000);

        public bool KillReachesChild { get; set; } = true;

        public int KillCalls { get; private set; }

        public Action? TreeExitedCallback { get; set; }

        public bool HasExited
        {
            get
            {
                lock (_sync)
                {
                    return _rootExitAt is { } at && DateTime.UtcNow >= at;
                }
            }
        }

        public bool TreeHasExited
        {
            get
            {
                bool gone;
                lock (_sync)
                {
                    gone = HasExited && _treeExitAt is { } at && DateTime.UtcNow >= at;
                    if (!gone || _treeReported)
                    {
                        return gone;
                    }

                    _treeReported = true;
                }

                TreeExitedCallback?.Invoke();
                return true;
            }
        }

        public int ExitCode => 0;

        public DateTime StartTime { get; } = DateTime.Now;

        public TimeSpan TotalProcessorTime => TimeSpan.Zero;

        public long WorkingSet64 => 0;

        public StreamReader? StandardOutput => null;

        public StreamReader? StandardError => null;

        public event EventHandler? Exited;

        public void ExitAfter(TimeSpan rootDelay, TimeSpan? childDelay = null)
        {
            var now = DateTime.UtcNow;
            lock (_sync)
            {
                _rootExitAt = now + rootDelay;
                _treeExitAt = now + (childDelay ?? rootDelay);
            }

            _ = Task.Delay(rootDelay).ContinueWith(_ => Exited?.Invoke(this, EventArgs.Empty), TaskScheduler.Default);
        }

        public bool CloseMainWindow() => false;

        public void Kill(bool entireProcessTree)
        {
            KillCalls++;
            var now = DateTime.UtcNow;
            lock (_sync)
            {
                _rootExitAt = now;
                _treeExitAt = KillReachesChild ? now : DateTime.MaxValue;
            }

            Exited?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
        }
    }

    private sealed class ScriptedRcon : IRconService
    {
        public string Reply { get; set; } = string.Empty;

        public Exception? Throw { get; set; }

        public Action? OnShutdown { get; set; }

        public bool IsConnected => true;

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            if (command == "shutdown")
            {
                OnShutdown?.Invoke();
            }

            return Throw is null ? Task.FromResult(Reply) : Task.FromException<string>(Throw);
        }

        public Task AnnounceAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<PlayerInfo>> GetPlayersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlayerInfo>>(Array.Empty<PlayerInfo>());
    }

    private sealed class ScriptedShutdownProbe : IServerShutdownProbe
    {
        public string? Evidence { get; set; }

        public long Mark() => 0;

        public string? FindShutdownEvidence(long mark) => Evidence;
    }
}
