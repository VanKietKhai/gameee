using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Concurrency;
using ConanServerControl.Infrastructure.Health;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.ProcessManagement;
using ConanServerControl.Infrastructure.Settings;
using ConanServerControl.Infrastructure.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;

namespace ConanServerControl.Tests;

public class M3ServerReadinessTests
{
    [Fact]
    [Trait("Issue", "QA-011")]
    public async Task Process_start_then_probe_ready_goes_starting_then_online()
    {
        var probe = new ScriptedReadinessProbe { ReadyAfterAttempts = 3 };
        var (manager, _) = await CreateManagerAsync(probe);
        var statuses = new List<ServerStatus>();
        manager.StateChanged += (_, state) => statuses.Add(state.Status);

        await manager.StartAsync();

        Assert.Equal(ServerStatus.Online, manager.State.Status);
        Assert.Equal(HealthCheckResult.ServerOnline, manager.State.Health);
        Assert.Contains(ServerStatus.Starting, statuses);
        Assert.True(probe.Attempts >= 3);
        Assert.Contains(false, probe.ObservedReadyFlags);
        Assert.Contains(true, probe.ObservedReadyFlags);
    }

    [Fact]
    [Trait("Issue", "QA-011")]
    public async Task Probe_never_ready_times_out_and_is_not_online()
    {
        var probe = new ScriptedReadinessProbe { NeverReady = true };
        var (manager, settings) = await CreateManagerAsync(probe);
        await settings.UpdateAsync(s =>
        {
            s.Advanced.StartupReadyTimeoutSeconds = 1;
            s.Advanced.ReadinessPollIntervalMilliseconds = 50;
        });

        var error = await Record.ExceptionAsync(() => manager.StartAsync());

        Assert.IsType<UserFacingException>(error);
        Assert.NotEqual(ServerStatus.Online, manager.State.Status);
        Assert.Equal(ServerStatus.Unresponsive, manager.State.Status);
        Assert.Equal(HealthCheckResult.ServerUnresponsive, manager.State.Health);
        Assert.DoesNotContain("Online", manager.State.Status.ToString());
    }

    [Fact]
    [Trait("Issue", "QA-011")]
    public async Task Process_exit_during_startup_is_startup_failure()
    {
        FakeProcessStarter? starter = null;
        var probe = new ScriptedReadinessProbe();
        probe.Handler = (_, _) =>
        {
            starter!.Last.HasExited = true;
            return Task.FromResult(new ServerReadinessResult
            {
                IsReady = false,
                Reason = "not-yet",
                Health = HealthCheckResult.ServerStarting
            });
        };

        var (manager, settings) = await CreateManagerAsync(probe, s => starter = s);
        await settings.UpdateAsync(s =>
        {
            s.Advanced.StartupReadyTimeoutSeconds = 2;
            s.Advanced.ReadinessPollIntervalMilliseconds = 20;
        });

        var error = await Record.ExceptionAsync(() => manager.StartAsync());

        Assert.IsType<UserFacingException>(error);
        Assert.Contains("exited", error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(ServerStatus.Online, manager.State.Status);
    }

    [Fact]
    [Trait("Issue", "QA-011")]
    public async Task Transient_probe_failure_then_success_becomes_online()
    {
        var probe = new ScriptedReadinessProbe { ReadyAfterAttempts = 4 };
        var (manager, settings) = await CreateManagerAsync(probe);
        await settings.UpdateAsync(s => s.Advanced.ReadinessPollIntervalMilliseconds = 20);

        await manager.StartAsync();

        Assert.Equal(ServerStatus.Online, manager.State.Status);
        Assert.True(probe.Attempts >= 4);
    }

    [Fact]
    [Trait("Issue", "QA-011")]
    public async Task Cancellation_during_readiness_wait_does_not_report_online()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new ScriptedReadinessProbe { NeverReady = true };
        probe.Handler = async (_, ct) =>
        {
            entered.TrySetResult();
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new ServerReadinessResult { IsReady = false, Reason = "late" };
        };

        var (manager, settings) = await CreateManagerAsync(probe);
        await settings.UpdateAsync(s =>
        {
            s.Advanced.StartupReadyTimeoutSeconds = 30;
            s.Advanced.ReadinessPollIntervalMilliseconds = 50;
        });

        using var cts = new CancellationTokenSource();
        var start = manager.StartAsync(cts.Token);
        await entered.Task;
        cts.Cancel();
        var error = await Record.ExceptionAsync(() => start);

        Assert.True(
            error is OperationCanceledException,
            $"Cancellation must surface as cancellation, not Online. error={error?.GetType().Name}: {error?.Message}");
        Assert.NotEqual(ServerStatus.Online, manager.State.Status);
    }

    [Fact]
    [Trait("Issue", "QA-011")]
    public async Task Probe_throw_is_retried_and_does_not_fake_online()
    {
        var probe = new ScriptedReadinessProbe { ThrowTimes = 2, ReadyAfterAttempts = 1 };
        var (manager, settings) = await CreateManagerAsync(probe);
        await settings.UpdateAsync(s => s.Advanced.ReadinessPollIntervalMilliseconds = 20);

        await manager.StartAsync();

        Assert.Equal(ServerStatus.Online, manager.State.Status);
        Assert.True(probe.Attempts >= 3);
    }

    [Fact]
    [Trait("Issue", "QA-011")]
    public async Task Probe_that_always_throws_times_out_without_online()
    {
        var probe = new ScriptedReadinessProbe { AlwaysThrow = true };
        var (manager, settings) = await CreateManagerAsync(probe);
        await settings.UpdateAsync(s =>
        {
            s.Advanced.StartupReadyTimeoutSeconds = 1;
            s.Advanced.ReadinessPollIntervalMilliseconds = 40;
        });

        var error = await Record.ExceptionAsync(() => manager.StartAsync());

        Assert.IsType<UserFacingException>(error);
        Assert.NotEqual(ServerStatus.Online, manager.State.Status);
        Assert.Equal(ServerStatus.Unresponsive, manager.State.Status);
    }

    [Fact]
    [Trait("Issue", "QA-011")]
    public async Task Post_update_start_does_not_report_online_before_ready()
    {
        var probe = new ScriptedReadinessProbe();
        var (manager, settings, starter, gate) = await CreateManagerWithStarterAsync(probe);
        await settings.UpdateAsync(s =>
        {
            s.Backups.BackupBeforeServerUpdate = false;
            s.Advanced.ReadinessPollIntervalMilliseconds = 20;
            s.Advanced.StartupReadyTimeoutSeconds = 10;
        });

        await manager.StartAsync();
        Assert.Equal(ServerStatus.Online, manager.State.Status);
        var afterFirst = probe.Attempts;
        starter.Last.HasExited = true;

        var sawOnlineBeforeReady = false;
        probe.Handler = (n, _) =>
        {
            var ready = n >= afterFirst + 3;
            if (!ready && manager.State.Status == ServerStatus.Online)
            {
                sawOnlineBeforeReady = true;
            }

            return Task.FromResult(new ServerReadinessResult
            {
                IsReady = ready,
                Reason = ready ? "ready" : "not-yet",
                Health = ready ? HealthCheckResult.ServerOnline : HealthCheckResult.ServerStarting
            });
        };

        var updates = new ServerUpdateService(
            settings,
            new ScriptedSteamCmd(),
            manager,
            new CountingBackup(),
            new ScriptedWorkshop(),
            gate,
            new MemoryActivityLog(),
            NullLogger<ServerUpdateService>.Instance);

        await updates.UpdateAsync(restartAfter: true);

        Assert.False(sawOnlineBeforeReady);
        Assert.Equal(ServerStatus.Online, manager.State.Status);
    }

    [Fact]
    public async Task Endpoint_probe_is_ready_when_game_port_is_bound()
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        var (paths, settings) = CreateSettings();
        await settings.UpdateAsync(s => s.Server.GamePort = port);
        var probe = new EndpointServerReadinessProbe(settings, new FakeRcon(), NullLogger<EndpointServerReadinessProbe>.Instance);

        var result = await probe.ProbeAsync(new ServerReadinessContext { GamePort = port, ProcessId = 1 });

        Assert.True(result.IsReady);
        Assert.Contains("port", result.Reason, StringComparison.OrdinalIgnoreCase);
        _ = paths;
    }

    [Fact]
    public async Task Endpoint_probe_does_not_require_rcon_password()
    {
        var (paths, settings) = CreateSettings();
        await settings.UpdateAsync(s =>
        {
            s.Rcon.Enabled = true;
            s.Server.GamePort = 1;
        });
        var probe = new EndpointServerReadinessProbe(settings, new FakeRcon(), NullLogger<EndpointServerReadinessProbe>.Instance);

        var result = await probe.ProbeAsync(new ServerReadinessContext { GamePort = 1, ProcessId = 1 });

        Assert.False(result.IsReady);
        _ = paths;
    }

    private static async Task<(ServerProcessManager Manager, JsonSettingsService Settings)> CreateManagerAsync(
        IServerReadinessProbe probe,
        Action<FakeProcessStarter>? captureStarter = null)
    {
        var created = await CreateManagerWithStarterAsync(probe);
        captureStarter?.Invoke(created.Starter);
        return (created.Manager, created.Settings);
    }

    private static async Task<(ServerProcessManager Manager, JsonSettingsService Settings, FakeProcessStarter Starter, ServerActionGate Gate)> CreateManagerWithStarterAsync(
        IServerReadinessProbe probe)
    {
        var data = Path.Combine(Path.GetTempPath(), "csc-m3-ready", Guid.NewGuid().ToString("n"));
        var paths = new AppPaths(data);
        paths.EnsureCreated();
        var settings = new JsonSettingsService(paths, new PassthroughProtector(), NullLogger<JsonSettingsService>.Instance);
        await settings.LoadAsync();
        var install = Path.Combine(data, "server");
        await QaTestSupport.ConfigureInstallAsync(settings, install);
        await settings.UpdateAsync(s =>
        {
            s.Advanced.GracefulStopTimeoutSeconds = 1;
            s.Advanced.ForceStopTimeoutSeconds = 1;
            s.Advanced.ReadinessPollIntervalMilliseconds = 20;
        });
        var starter = new FakeProcessStarter { Settings = settings };
        var gate = new ServerActionGate();
        var manager = new ServerProcessManager(
            settings,
            starter,
            gate,
            new MemoryActivityLog(),
            new FakeRcon(),
            probe,
            NullLogger<ServerProcessManager>.Instance);
        return (manager, settings, starter, gate);
    }

    private static (AppPaths Paths, JsonSettingsService Settings) CreateSettings()
    {
        var data = Path.Combine(Path.GetTempPath(), "csc-m3-ready", Guid.NewGuid().ToString("n"));
        var paths = new AppPaths(data);
        paths.EnsureCreated();
        var settings = new JsonSettingsService(paths, new PassthroughProtector(), NullLogger<JsonSettingsService>.Instance);
        settings.LoadAsync().GetAwaiter().GetResult();
        return (paths, settings);
    }
}
