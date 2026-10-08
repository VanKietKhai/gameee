using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Settings;
using ConanServerControl.Infrastructure.Steam;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class WarpNetworkGuardTests : IDisposable
{
    private readonly string _cli = Path.Combine(Path.GetTempPath(), "csc-warp-" + Guid.NewGuid().ToString("n") + ".exe");

    public WarpNetworkGuardTests() => File.WriteAllText(_cli, "fake");

    public void Dispose() => File.Delete(_cli);

    [Theory]
    [InlineData("Status update: Connected\n", true)]
    [InlineData("Status update: Connected\r\nNetwork: healthy\r\n", true)]
    [InlineData("Status update: Connecting\nReason: Establishing a connection\n", false)]
    [InlineData("Status update: Disconnected\nReason: Manual Disconnection\n", false)]
    [InlineData("", false)]
    public void Recognises_connected_status(string output, bool connected)
    {
        Assert.Equal(connected, WarpNetworkGuard.IsConnectedStatus(output));
    }

    [Fact]
    public async Task Off_by_setting_never_runs_warp()
    {
        var runner = new FakeWarp();
        var guard = Guard(runner, enabled: false);

        await using (await guard.AcquireAsync("test"))
        {
        }

        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task Connects_once_for_nested_leases_and_disconnects_after_the_last()
    {
        var runner = new FakeWarp();
        var guard = Guard(runner, enabled: true);

        var outer = await guard.AcquireAsync("update");
        var inner = await guard.AcquireAsync("SteamCMD");
        await inner.DisposeAsync();
        Assert.DoesNotContain("disconnect", runner.Commands);

        await outer.DisposeAsync();

        Assert.Equal(1, runner.Commands.Count(c => c == "connect"));
        Assert.Equal("disconnect", runner.Commands.Last());
        Assert.False(runner.Connected);
    }

    [Fact]
    public async Task Leaves_warp_alone_when_it_was_already_connected()
    {
        var runner = new FakeWarp { Connected = true };
        var guard = Guard(runner, enabled: true);

        await using (await guard.AcquireAsync("update"))
        {
        }

        Assert.DoesNotContain("connect", runner.Commands);
        Assert.DoesNotContain("disconnect", runner.Commands);
        Assert.True(runner.Connected);
    }

    private WarpNetworkGuard Guard(FakeWarp runner, bool enabled)
    {
        var settings = new MemorySettings();
        settings.Current.SteamCmd.UseCloudflareWarp = enabled;
        settings.Current.SteamCmd.WarpCliPath = _cli;
        return new WarpNetworkGuard(settings, runner, new MemoryActivityLog(), NullLogger<WarpNetworkGuard>.Instance);
    }

    private sealed class FakeWarp : IProcessRunner
    {
        public bool Connected { get; set; }

        public List<string> Commands { get; } = new();

        public Task<ProcessExecutionResult> RunAsync(ProcessStartRequest request, TimeSpan? timeout = null, IProgress<string>? output = null, CancellationToken cancellationToken = default)
        {
            var command = request.Arguments;
            if (command != "status")
            {
                Commands.Add(command);
            }

            Connected = command switch
            {
                "connect" => true,
                "disconnect" => false,
                _ => Connected
            };
            var text = Connected ? "Status update: Connected\n" : "Status update: Disconnected\n";
            return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, StandardOutput = command == "status" ? text : "Success\n" });
        }
    }

    private sealed class MemorySettings : ISettingsService
    {
        public AppSettings Current { get; } = new();

        public ProtectedSecrets Secrets { get; } = new();

        public event EventHandler? Changed;

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default)
        {
            mutate(Current);
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task UpdateSecretsAsync(Action<ProtectedSecrets> mutate, CancellationToken cancellationToken = default)
        {
            mutate(Secrets);
            return Task.CompletedTask;
        }
    }
}
