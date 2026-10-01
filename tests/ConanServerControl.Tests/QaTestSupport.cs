using System.Net;
using System.Text;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

internal static class QaTestSupport
{
    public static (string Data, AppPaths Paths, JsonSettingsService Settings) CreateData()
    {
        var data = Path.Combine(Path.GetTempPath(), "csc-qa", Guid.NewGuid().ToString("n"));
        var paths = new AppPaths(data);
        paths.EnsureCreated();
        var settings = new JsonSettingsService(paths, new PassthroughProtector(), NullLogger<JsonSettingsService>.Instance);
        settings.LoadAsync().GetAwaiter().GetResult();
        return (data, paths, settings);
    }

    public static string InstallRoot(string data) => Path.Combine(data, "server");

    public static async Task ConfigureInstallAsync(JsonSettingsService settings, string install)
    {
        Directory.CreateDirectory(install);
        var exe = Path.Combine(install, "ConanSandboxServer.exe");
        if (!File.Exists(exe))
        {
            await File.WriteAllTextAsync(exe, "fake-exe");
        }

        await settings.UpdateAsync(s =>
        {
            s.ServerPaths.ServerExecutablePath = exe;
            s.ServerPaths.ServerWorkingDirectory = install;
            s.ServerPaths.ServerInstallDirectory = install;
            s.Backups.BackupBeforeServerUpdate = true;
            s.Backups.BackupBeforeModUpdate = true;
            s.General.StartServerWhenManagerLaunches = false;
        });
    }
}

internal sealed class RecordingActivityLog : IActivityLog
{
    public List<string> Messages { get; } = new();

    public Task AddAsync(string category, string message, string? actor = null, string? details = null, CancellationToken cancellationToken = default)
    {
        Messages.Add($"{category}: {message}");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActivityLogEntry>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ActivityLogEntry>>(Array.Empty<ActivityLogEntry>());
}

internal sealed class CountingBackup : IBackupService
{
    public int Count { get; private set; }

    public List<string> Reasons { get; } = new();

    public Func<string, CancellationToken, Task>? OnBackup { get; set; }

    public async Task<BackupRecord> BackupNowAsync(string reason, CancellationToken cancellationToken = default)
    {
        Count++;
        Reasons.Add(reason);
        if (OnBackup is not null)
        {
            await OnBackup(reason, cancellationToken).ConfigureAwait(false);
        }

        return new BackupRecord
        {
            Id = "backup-" + Count,
            DirectoryPath = Path.GetTempPath(),
            CreatedAt = DateTimeOffset.UtcNow,
            Reason = reason,
            IncludesWorld = true
        };
    }

    public Task RestoreAsync(string backupId, bool startAfter, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BackupRecord>>(Array.Empty<BackupRecord>());

    public Task ApplyRetentionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class ScriptedSteamCmd : ISteamCmdService
{
    public List<string> Events { get; } = new();

    public List<long> WorkshopIds { get; } = new();

    public List<string> WorkshopDirectories { get; } = new();

    public int ServerExitCode { get; set; }

    public int WorkshopExitCode { get; set; }

    public Exception? ServerException { get; set; }

    public Exception? WorkshopException { get; set; }

    public Func<long, string, CancellationToken, Task>? OnWorkshop { get; set; }

    public Func<CancellationToken, Task>? OnServer { get; set; }

    public TaskCompletionSource<bool>? BlockServer { get; set; }

    public string? ExecutablePath => "steamcmd.exe";

    public bool IsInstalled => true;

    public Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UpdateAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<ProcessExecutionResult> InstallOrUpdateDedicatedServerAsync(
        string installDirectory,
        bool validate,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Events.Add("server-update");
        if (BlockServer is not null)
        {
            await BlockServer.Task.ConfigureAwait(false);
        }

        if (OnServer is not null)
        {
            await OnServer(cancellationToken).ConfigureAwait(false);
        }

        if (ServerException is not null)
        {
            throw ServerException;
        }

        if (ServerExitCode != 0)
        {
            throw new UserFacingException("SteamCMD failed", $"exit {ServerExitCode}");
        }

        return new ProcessExecutionResult { ExitCode = 0, Duration = TimeSpan.FromMilliseconds(1) };
    }

    public async Task<ProcessExecutionResult> DownloadWorkshopItemAsync(
        long workshopId,
        string installDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Events.Add("workshop:" + workshopId);
        WorkshopIds.Add(workshopId);
        WorkshopDirectories.Add(installDirectory);
        if (OnWorkshop is not null)
        {
            await OnWorkshop(workshopId, installDirectory, cancellationToken).ConfigureAwait(false);
        }

        if (WorkshopException is not null)
        {
            throw WorkshopException;
        }

        if (WorkshopExitCode != 0)
        {
            throw new UserFacingException(
                "SteamCMD could not download Workshop item",
                $"exit {WorkshopExitCode}");
        }

        return new ProcessExecutionResult { ExitCode = 0, Duration = TimeSpan.FromMilliseconds(1) };
    }
}

internal sealed class RecordingServer : IServerProcessManager
{
    private readonly IServerActionGate _gate;

    public RecordingServer(IServerActionGate gate) => _gate = gate;

    public List<string> Calls { get; } = new();

    public ServerRuntimeState State { get; } = new();

    public event EventHandler<ServerRuntimeState>? StateChanged
    {
        add { }
        remove { }
    }

    public bool IsConanServerProcess(string processName) =>
        processName.Contains("Conan", StringComparison.OrdinalIgnoreCase);

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        RunPublic("Start server", "start", () => State.Status = ServerStatus.Online);

    public Task StopAsync(bool force = false, CancellationToken cancellationToken = default) =>
        RunPublic(force ? "Force stop server" : "Stop server", "stop", () => State.Status = ServerStatus.Offline);

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        if (!_gate.TryBegin("Restart server", out var lease) || lease is null)
        {
            throw Busy("restart");
        }

        using (lease)
        {
            Calls.Add("restart");
            State.Status = ServerStatus.Offline;
            State.Status = ServerStatus.Online;
        }

        await Task.CompletedTask;
    }

    public Task StartUnderLockAsync(CancellationToken cancellationToken = default)
    {
        if (!_gate.IsBusy)
        {
            throw new InvalidOperationException("no lease");
        }

        Calls.Add("start-under-lock");
        State.Status = ServerStatus.Online;
        return Task.CompletedTask;
    }

    public Task StopUnderLockAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!_gate.IsBusy)
        {
            throw new InvalidOperationException("no lease");
        }

        Calls.Add("stop-under-lock");
        State.Status = ServerStatus.Offline;
        return Task.CompletedTask;
    }

    private Task RunPublic(string action, string call, Action mutate)
    {
        if (!_gate.TryBegin(action, out var lease) || lease is null)
        {
            throw Busy(call);
        }

        using (lease)
        {
            Calls.Add(call);
            mutate();
        }

        return Task.CompletedTask;
    }

    private UserFacingException Busy(string call) =>
        new("Server action already running", $"Cannot {call} because {_gate.CurrentAction} is in progress.", "Wait.");
}

internal sealed class ScriptedWorkshop : IWorkshopModService
{
    public int UpdateAllCount { get; private set; }

    public int CheckCount { get; private set; }

    public Exception? UpdateAllException { get; set; }

    public IReadOnlyList<WorkshopMod> Mods => Array.Empty<WorkshopMod>();

    public Task AddAsync(long workshopId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RemoveAsync(long workshopId, bool confirmed, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SetEnabledAsync(long workshopId, bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task MoveAsync(long workshopId, int newIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateAsync(long workshopId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateAllAsync(CancellationToken cancellationToken = default)
    {
        UpdateAllCount++;
        if (UpdateAllException is not null)
        {
            throw UpdateAllException;
        }

        return Task.CompletedTask;
    }

    public Task ApplyUpdatesAsync(IReadOnlyList<long>? workshopIds, CancellationToken cancellationToken = default) =>
        UpdateAllAsync(cancellationToken);

    public Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        CheckCount++;
        return Task.CompletedTask;
    }

    public string GetShareableModList() => string.Empty;
}

internal sealed class EmptyWorkshopClient : ISteamWorkshopClient
{
    public Task<IReadOnlyList<WorkshopPublishedFileDetails>> GetPublishedFileDetailsAsync(
        IReadOnlyList<long> workshopIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkshopPublishedFileDetails>>(Array.Empty<WorkshopPublishedFileDetails>());
}

internal sealed class StubHttpHandler : HttpMessageHandler
{
    public string Body { get; set; } = """{"response":{"publishedfiledetails":[]}}""";

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(Status)
        {
            Content = new StringContent(Body, Encoding.UTF8, "application/json")
        });
}

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}

internal sealed class ExplodingProcessRunner : IProcessRunner
{
    public int Calls { get; private set; }

    public Task<ProcessExecutionResult> RunAsync(
        ProcessStartRequest request,
        TimeSpan? timeout = null,
        IProgress<string>? output = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        throw new InvalidOperationException("SteamCMD process must not start in this test.");
    }
}

internal sealed class StatusOnlyServer : IServerProcessManager
{
    public ServerRuntimeState State { get; } = new();

    public event EventHandler<ServerRuntimeState>? StateChanged
    {
        add { }
        remove { }
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(bool force = false, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RestartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartUnderLockAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopUnderLockAsync(bool force = false, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public bool IsConanServerProcess(string processName) => false;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
