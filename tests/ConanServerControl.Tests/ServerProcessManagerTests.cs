using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Concurrency;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.ProcessManagement;
using ConanServerControl.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class ServerProcessManagerTests
{
    [Fact]
    public void Recognizes_conan_dedicated_server_process_names()
    {
        var manager = CreateManager(out _);
        Assert.True(manager.IsConanServerProcess("ConanSandboxServer"));
        Assert.True(manager.IsConanServerProcess("ConanSandboxServer.exe"));
        Assert.True(manager.IsConanServerProcess("ConanSandboxServer-Win64-Shipping.exe"));
        Assert.False(manager.IsConanServerProcess("notepad.exe"));
    }

    [Fact]
    public async Task Start_stop_restart_with_fake_process()
    {
        var manager = CreateManager(out var starter);
        var temp = Path.Combine(Path.GetTempPath(), "csc-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(temp);
        var exe = Path.Combine(temp, "ConanSandboxServer.exe");
        await File.WriteAllTextAsync(exe, "fake");

        var settings = starter.Settings;
        await settings.UpdateAsync(s =>
        {
            s.ServerPaths.ServerExecutablePath = exe;
            s.ServerPaths.ServerWorkingDirectory = temp;
            s.Advanced.GracefulStopTimeoutSeconds = 1;
            s.Advanced.ForceStopTimeoutSeconds = 1;
        });

        await manager.StartAsync();
        Assert.Equal(ServerStatus.Online, manager.State.Status);
        Assert.NotNull(manager.State.ProcessId);

        await manager.StopAsync();
        Assert.Equal(ServerStatus.Offline, manager.State.Status);

        starter.Last.HasExited = false;
        await manager.StartAsync();
        await manager.RestartAsync();
        Assert.Equal(ServerStatus.Online, manager.State.Status);
    }

    [Fact]
    public void Action_gate_prevents_overlap()
    {
        var gate = new ServerActionGate();
        Assert.True(gate.TryBegin("a", out var first));
        Assert.False(gate.TryBegin("b", out var second));
        Assert.Null(second);
        first!.Dispose();
        Assert.True(gate.TryBegin("b", out var third));
        third!.Dispose();
    }

    private static ServerProcessManager CreateManager(out FakeProcessStarter starter)
    {
        var data = Path.Combine(Path.GetTempPath(), "csc-tests", Guid.NewGuid().ToString("n"));
        var paths = new AppPaths(data);
        paths.EnsureCreated();
        var protector = new PassthroughProtector();
        var settings = new JsonSettingsService(paths, protector, NullLogger<JsonSettingsService>.Instance);
        settings.LoadAsync().GetAwaiter().GetResult();
        starter = new FakeProcessStarter { Settings = settings };
        var gate = new ServerActionGate();
        var activity = new MemoryActivityLog();
        var rcon = new FakeRcon();
        return new ServerProcessManager(settings, starter, gate, activity, rcon, new ImmediateReadyProbe(), NullLogger<ServerProcessManager>.Instance);
    }
}

internal sealed class FakeProcessStarter : IProcessStarter
{
    public required JsonSettingsService Settings { get; init; }

    public FakeManagedProcess Last { get; private set; } = new();

    public IManagedProcess Start(ProcessStartRequest request)
    {
        Last = new FakeManagedProcess();
        return Last;
    }
}

internal sealed class FakeManagedProcess : IManagedProcess
{
    public int Id { get; } = Random.Shared.Next(1000, 9999);
    public bool HasExited { get; set; }
    public int ExitCode { get; set; }
    public DateTime StartTime { get; } = DateTime.Now;
    public TimeSpan TotalProcessorTime => TimeSpan.FromMilliseconds(10);
    public long WorkingSet64 => 1024 * 1024;
    public StreamReader? StandardOutput => StreamReader.Null;
    public StreamReader? StandardError => StreamReader.Null;
    public event EventHandler? Exited;

    public bool CloseMainWindow()
    {
        HasExited = true;
        Exited?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Kill(bool entireProcessTree)
    {
        HasExited = true;
        Exited?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
    }
}

internal sealed class MemoryActivityLog : IActivityLog
{
    public Task AddAsync(string category, string message, string? actor = null, string? details = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<ActivityLogEntry>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ActivityLogEntry>>(Array.Empty<ActivityLogEntry>());
}

internal sealed class FakeRcon : IRconService
{
    public bool IsConnected => false;
    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;
    public Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
    public Task AnnounceAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<PlayerInfo>> GetPlayersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PlayerInfo>>(Array.Empty<PlayerInfo>());
}

internal sealed class PassthroughProtector : ISecretProtector
{
    public string Protect(string plaintext) => "dev-base64:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));
    public string Unprotect(string protectedPayload) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedPayload["dev-base64:".Length..]));
}
