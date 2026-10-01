using System.Diagnostics;
using System.Text;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.ProcessManagement;

public sealed class SystemProcessStarter : IProcessStarter
{
    public IManagedProcess Start(ProcessStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!PathValidator.IsSafeAbsolutePath(request.FileName) && !File.Exists(request.FileName))
        {
            // Allow executables resolved from PATH only when the name has no directory separators.
            if (request.FileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
                && !PathValidator.IsSafeAbsolutePath(request.FileName))
            {
                throw new UserFacingException(
                    "Invalid executable path",
                    $"The process path is not allowed:{Environment.NewLine}{request.FileName}",
                    "Choose a server executable from the Settings page.");
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            Arguments = request.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? Path.GetDirectoryName(request.FileName) ?? Environment.CurrentDirectory
                : request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = request.CreateNoWindow,
            RedirectStandardOutput = request.RedirectStandardIO,
            RedirectStandardError = request.RedirectStandardIO,
            RedirectStandardInput = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (request.Environment is not null)
        {
            foreach (var pair in request.Environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
        {
            process.Dispose();
            throw new UserFacingException(
                "Process failed to start",
                $"Windows did not start:{Environment.NewLine}{request.FileName}",
                "Confirm the path is an executable file and that you have permission to run it.");
        }

        return new SystemManagedProcess(process);
    }
}

public sealed class SystemManagedProcess : IManagedProcess
{
    private readonly Process _process;
    private bool _disposed;

    public SystemManagedProcess(Process process)
    {
        _process = process;
        _process.Exited += (_, _) => Exited?.Invoke(this, EventArgs.Empty);
    }

    public int Id => _process.Id;

    public bool HasExited
    {
        get
        {
            try
            {
                return _process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }

    public int ExitCode => _process.HasExited ? _process.ExitCode : 0;

    public DateTime StartTime
    {
        get
        {
            try
            {
                return _process.StartTime;
            }
            catch
            {
                return DateTime.Now;
            }
        }
    }

    public TimeSpan TotalProcessorTime
    {
        get
        {
            try
            {
                return _process.TotalProcessorTime;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }
    }

    public long WorkingSet64
    {
        get
        {
            try
            {
                return _process.WorkingSet64;
            }
            catch
            {
                return 0;
            }
        }
    }

    public StreamReader? StandardOutput => _process.StartInfo.RedirectStandardOutput ? _process.StandardOutput : null;

    public StreamReader? StandardError => _process.StartInfo.RedirectStandardError ? _process.StandardError : null;

    public event EventHandler? Exited;

    public bool CloseMainWindow()
    {
        try
        {
            return _process.CloseMainWindow();
        }
        catch
        {
            return false;
        }
    }

    public void Kill(bool entireProcessTree)
    {
        if (HasExited)
        {
            return;
        }

        _process.Kill(entireProcessTree);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _process.Dispose();
    }
}

public sealed class ProcessRunner : IProcessRunner
{
    private readonly IProcessStarter _starter;
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(IProcessStarter starter, ILogger<ProcessRunner> logger)
    {
        _starter = starter;
        _logger = logger;
    }

    public async Task<ProcessExecutionResult> RunAsync(
        ProcessStartRequest request,
        TimeSpan? timeout = null,
        IProgress<string>? output = null,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        cancellationToken.ThrowIfCancellationRequested();
        using var process = _starter.Start(request);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var stdoutTask = DrainAsync(process.StandardOutput, stdout, output, cancellationToken);
        var stderrTask = DrainAsync(process.StandardError, stderr, output, cancellationToken);

        using var timeoutCts = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var timedOut = false;
        try
        {
            while (!process.HasExited)
            {
                linked.Token.ThrowIfCancellationRequested();
                await Task.Delay(50, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            _logger.LogWarning("Process {File} timed out after {Timeout} and will be terminated.", request.FileName, timeout);
            KillStartedProcess(process, request.FileName, preserveAsTimeout: true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Process {File} was cancelled after start and will be terminated.", request.FileName);
            KillStartedProcess(process, request.FileName, preserveAsTimeout: false);
            await Task.WhenAll(Safe(stdoutTask), Safe(stderrTask)).ConfigureAwait(false);
            throw;
        }

        await Task.WhenAll(Safe(stdoutTask), Safe(stderrTask)).ConfigureAwait(false);

        return new ProcessExecutionResult
        {
            ExitCode = timedOut ? -1 : process.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
            Duration = DateTime.UtcNow - started,
            TimedOut = timedOut
        };
    }

    private static async Task DrainAsync(StreamReader? reader, StringBuilder buffer, IProgress<string>? output, CancellationToken cancellationToken)
    {
        if (reader is null)
        {
            return;
        }

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            buffer.AppendLine(line);
            output?.Report(line);
        }
    }

    private static async Task Safe(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Drain cancelled with the process.
        }
    }

    private void KillStartedProcess(IManagedProcess process, string fileName, bool preserveAsTimeout)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate process tree for {File}.", fileName);
            if (!preserveAsTimeout)
            {
                throw;
            }
        }
    }
}
