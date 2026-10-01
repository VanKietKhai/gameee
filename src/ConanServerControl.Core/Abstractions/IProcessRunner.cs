using ConanServerControl.Core.Models;

namespace ConanServerControl.Core.Abstractions;

public sealed class ProcessStartRequest
{
    public required string FileName { get; init; }

    public string Arguments { get; init; } = string.Empty;

    public string? WorkingDirectory { get; init; }

    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    public bool RedirectStandardIO { get; init; } = true;

    public bool CreateNoWindow { get; init; } = true;
}

public interface IManagedProcess : IDisposable
{
    int Id { get; }

    bool HasExited { get; }

    int ExitCode { get; }

    DateTime StartTime { get; }

    TimeSpan TotalProcessorTime { get; }

    long WorkingSet64 { get; }

    event EventHandler? Exited;

    bool CloseMainWindow();

    void Kill(bool entireProcessTree);

    StreamReader? StandardOutput { get; }

    StreamReader? StandardError { get; }
}

public interface IProcessStarter
{
    IManagedProcess Start(ProcessStartRequest request);
}

public interface IProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(
        ProcessStartRequest request,
        TimeSpan? timeout = null,
        IProgress<string>? output = null,
        CancellationToken cancellationToken = default);
}
