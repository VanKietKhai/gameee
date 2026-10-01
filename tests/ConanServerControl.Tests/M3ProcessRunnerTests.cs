using ConanServerControl.Core.Abstractions;
using ConanServerControl.Infrastructure.ProcessManagement;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class M3ProcessRunnerTests
{
    [Fact]
    public async Task Caller_cancellation_after_start_kills_process_tree()
    {
        var starter = new ScriptedStarter();
        var runner = new ProcessRunner(starter, NullLogger<ProcessRunner>.Instance);
        using var cts = new CancellationTokenSource();

        var run = runner.RunAsync(
            new ProcessStartRequest { FileName = "steamcmd.exe" },
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cts.Token);

        await starter.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cts.CancelAsync();

        var error = await Record.ExceptionAsync(() => run);
        Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.Equal(1, starter.StartCount);
        Assert.NotNull(starter.Last);
        Assert.Equal(1, starter.Last!.KillCount);
        Assert.True(starter.Last.LastKillEntireTree);
        Assert.True((DateTime.UtcNow - starter.StartedAt) < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Timeout_kills_process_tree_and_is_not_success()
    {
        var starter = new ScriptedStarter();
        var runner = new ProcessRunner(starter, NullLogger<ProcessRunner>.Instance);

        var result = await runner.RunAsync(
            new ProcessStartRequest { FileName = "steamcmd.exe" },
            timeout: TimeSpan.FromMilliseconds(80));

        Assert.True(result.TimedOut);
        Assert.False(result.Succeeded);
        Assert.Equal(-1, result.ExitCode);
        Assert.Equal(1, starter.Last!.KillCount);
        Assert.True(starter.Last.LastKillEntireTree);
    }

    [Fact]
    public async Task Normal_exit_does_not_kill()
    {
        var starter = new ScriptedStarter { ExitImmediately = true };
        var runner = new ProcessRunner(starter, NullLogger<ProcessRunner>.Instance);

        var result = await runner.RunAsync(new ProcessStartRequest { FileName = "steamcmd.exe" });

        Assert.False(result.TimedOut);
        Assert.True(result.Succeeded);
        Assert.Equal(0, starter.Last!.KillCount);
    }

    [Fact]
    public async Task Cancellation_before_start_does_not_start_or_kill()
    {
        var starter = new ScriptedStarter();
        var runner = new ProcessRunner(starter, NullLogger<ProcessRunner>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var error = await Record.ExceptionAsync(
            () => runner.RunAsync(new ProcessStartRequest { FileName = "steamcmd.exe" }, cancellationToken: cts.Token));

        Assert.IsType<OperationCanceledException>(error);
        Assert.Equal(0, starter.StartCount);
        Assert.Null(starter.Last);
    }

    [Fact]
    public async Task Kill_throw_on_caller_cancel_does_not_become_success()
    {
        var starter = new ScriptedStarter { KillException = new IOException("kill failed") };
        var runner = new ProcessRunner(starter, NullLogger<ProcessRunner>.Instance);
        using var cts = new CancellationTokenSource();
        var run = runner.RunAsync(
            new ProcessStartRequest { FileName = "steamcmd.exe" },
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cts.Token);
        await starter.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cts.CancelAsync();

        var error = await Record.ExceptionAsync(() => run);
        Assert.NotNull(error);
        Assert.False(error is null);
        Assert.True(error is IOException or OperationCanceledException);
    }

    private sealed class ScriptedStarter : IProcessStarter
    {
        public int StartCount { get; private set; }

        public ScriptedProcess? Last { get; private set; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DateTime StartedAt { get; private set; }

        public bool ExitImmediately { get; init; }

        public Exception? KillException { get; init; }

        public IManagedProcess Start(ProcessStartRequest request)
        {
            StartCount++;
            StartedAt = DateTime.UtcNow;
            Last = new ScriptedProcess
            {
                HasExited = ExitImmediately,
                KillException = KillException
            };
            Started.TrySetResult();
            return Last;
        }
    }

    private sealed class ScriptedProcess : IManagedProcess
    {
        public int KillCount { get; private set; }

        public bool LastKillEntireTree { get; private set; }

        public Exception? KillException { get; init; }

        public int Id { get; } = 4242;

        public bool HasExited { get; set; }

        public int ExitCode { get; set; }

        public DateTime StartTime { get; } = DateTime.UtcNow;

        public TimeSpan TotalProcessorTime => TimeSpan.Zero;

        public long WorkingSet64 => 0;

        public StreamReader? StandardOutput => StreamReader.Null;

        public StreamReader? StandardError => StreamReader.Null;

        public event EventHandler? Exited
        {
            add { }
            remove { }
        }

        public bool CloseMainWindow() => false;

        public void Kill(bool entireProcessTree)
        {
            if (KillException is not null)
            {
                throw KillException;
            }

            KillCount++;
            LastKillEntireTree = entireProcessTree;
            HasExited = true;
        }

        public void Dispose()
        {
        }
    }
}
