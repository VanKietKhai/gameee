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

    /// <summary>
    /// True when this process and every descendant it started have exited. The dedicated server
    /// launcher (ConanSandboxServer.exe) runs the real server as a child process, so the launcher
    /// exiting alone does not mean the server is gone.
    /// </summary>
    bool TreeHasExited => HasExited;

    /// <summary>Records current descendants so they can be awaited and killed even after this process exits.</summary>
    void TrackDescendants()
    {
    }

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
