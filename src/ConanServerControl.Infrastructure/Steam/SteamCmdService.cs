using System.IO.Compression;
using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Steam;

public sealed class SteamCmdService : ISteamCmdService
{
    public const string HttpClientName = "steamcmd";

    private readonly ISettingsService _settings;
    private readonly IAppPaths _paths;
    private readonly IProcessRunner _runner;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SteamCmdService> _logger;
    private readonly ISteamNetworkGuard? _network;

    public SteamCmdService(
        ISettingsService settings,
        IAppPaths paths,
        IProcessRunner runner,
        IHttpClientFactory httpClientFactory,
        ILogger<SteamCmdService> logger,
        ISteamNetworkGuard? network = null)
    {
        _settings = settings;
        _paths = paths;
        _runner = runner;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _network = network;
    }

    public string? ExecutablePath
    {
        get
        {
            var dir = _settings.Current.SteamCmd.InstallDirectory;
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = _paths.SteamCmdDefaultDirectory;
            }

            return Path.Combine(dir, AppConstants.SteamCmdExecutableWindows);
        }
    }

    public bool IsInstalled => !string.IsNullOrWhiteSpace(ExecutablePath) && File.Exists(ExecutablePath);

    public async Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var directory = _settings.Current.SteamCmd.InstallDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = _paths.SteamCmdDefaultDirectory;
            await _settings.UpdateAsync(s => s.SteamCmd.InstallDirectory = directory, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!PathValidator.IsSafeAbsolutePath(directory))
        {
            throw new UserFacingException(
                "Invalid SteamCMD directory",
                $"Configured SteamCMD directory:{Environment.NewLine}{directory}",
                "Set a valid absolute folder such as C:\\ConanServerControl\\steamcmd\\ in Settings.");
        }

        Directory.CreateDirectory(directory);
        progress?.Report("Downloading SteamCMD...");
        _logger.LogInformation("Installing SteamCMD into {Dir}", directory);

        var zipPath = Path.Combine(_paths.StagingDirectory, "steamcmd.zip");
        Directory.CreateDirectory(_paths.StagingDirectory);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using (var response = await client.GetAsync(AppConstants.SteamCmdZipUrl, cancellationToken).ConfigureAwait(false))
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new UserFacingException(
                    "SteamCMD download failed",
                    $"HTTP {(int)response.StatusCode} while downloading SteamCMD from Valve.",
                    "Check internet access, then try Install SteamCMD again.");
            }

            await using var file = File.Create(zipPath);
            await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report("Extracting SteamCMD...");
        ZipFile.ExtractToDirectory(zipPath, directory, overwriteFiles: true);
        try
        {
            File.Delete(zipPath);
        }
        catch
        {
            // non-fatal
        }

        if (!IsInstalled)
        {
            throw new UserFacingException(
                "SteamCMD installation incomplete",
                $"steamcmd.exe was not found after extraction:{Environment.NewLine}{ExecutablePath}",
                "Delete the SteamCMD folder and try Install SteamCMD again.");
        }

        progress?.Report("SteamCMD files extracted. Running first self-update...");
        await UpdateAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        EnsureInstalled();
        progress?.Report("Updating SteamCMD...");
        var result = await RunSteamCmdAsync(["+quit"], TimeSpan.FromMinutes(5), progress, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded && result.ExitCode != 7)
        {
            // SteamCMD often returns 7 on first run after self-update.
            throw CreateSteamCmdFailure("SteamCMD could not update itself.", result);
        }
    }

    public Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ExecutablePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Task.FromResult(false);
        }

        var info = new FileInfo(path);
        return Task.FromResult(info.Length > 0);
    }

    public async Task<ProcessExecutionResult> InstallOrUpdateDedicatedServerAsync(
        string installDirectory,
        bool validate,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInstalled();
        if (!PathValidator.IsSafeAbsolutePath(installDirectory))
        {
            throw new UserFacingException(
                "Invalid dedicated server directory",
                $"Install directory:{Environment.NewLine}{installDirectory}",
                "Choose an absolute folder such as C:\\ConanServer\\ from Settings.");
        }

        Directory.CreateDirectory(installDirectory);
        progress?.Report("Installing or updating Conan Exiles Dedicated Server via SteamCMD...");

        var args = new List<string>
        {
            "+force_install_dir",
            Quote(installDirectory),
            "+login",
            "anonymous",
            "+app_update",
            AppConstants.ConanDedicatedServerAppId.ToString()
        };

        if (validate)
        {
            args.Add("validate");
        }

        args.Add("+quit");

        var result = await RunSteamCmdAsync(args, TimeSpan.FromHours(2), progress, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw CreateSteamCmdFailure(
                "SteamCMD could not install or update the Conan dedicated server.",
                result);
        }

        return result;
    }

    public async Task<ProcessExecutionResult> DownloadWorkshopItemAsync(
        long workshopId,
        string installDirectory,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInstalled();
        if (workshopId <= 0)
        {
            throw new UserFacingException(
                "Invalid Workshop ID",
                $"Workshop ID {workshopId} is not valid.",
                WorkshopIdValidator.DescribeRule());
        }

        if (!PathValidator.IsSafeAbsolutePath(installDirectory))
        {
            throw new UserFacingException(
                "Invalid Workshop download directory",
                installDirectory,
                "Use the application staging directory; do not enter arbitrary paths.");
        }

        Directory.CreateDirectory(installDirectory);
        progress?.Report($"Downloading Workshop item {workshopId}...");

        var args = new List<string>
        {
            "+force_install_dir",
            Quote(installDirectory),
            "+login",
            "anonymous",
            "+workshop_download_item",
            AppConstants.ConanExilesAppId.ToString(),
            workshopId.ToString(),
            "+quit"
        };

        var result = await RunSteamCmdAsync(args, TimeSpan.FromHours(1), progress, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw CreateSteamCmdFailure(
                $"SteamCMD could not download Workshop item {workshopId}. The previously installed mod was left unchanged.",
                result);
        }

        return result;
    }

    private void EnsureInstalled()
    {
        if (IsInstalled)
        {
            return;
        }

        throw new UserFacingException(
            "SteamCMD is not installed",
            $"Expected:{Environment.NewLine}{ExecutablePath}",
            "Open Diagnostics or Settings and choose Install SteamCMD. Conan Server Control can download it automatically.");
    }

    private async Task<ProcessExecutionResult> RunSteamCmdAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new UserFacingException(
                "SteamCMD requires Windows",
                "Conan Server Control runs SteamCMD as steamcmd.exe for the Windows dedicated server.",
                "Install and run Conan Server Control on Windows 10/11 x64.");
        }

        var exe = ExecutablePath!;
        var argumentLine = string.Join(' ', arguments);
        _logger.LogInformation("SteamCMD: {Exe} {Args}", exe, argumentLine);

        await using var network = _network is null
            ? NoOpAsyncDisposable.Instance
            : await _network.AcquireAsync("SteamCMD", cancellationToken).ConfigureAwait(false);
        var result = await _runner.RunAsync(
                new ProcessStartRequest
                {
                    FileName = exe,
                    Arguments = argumentLine,
                    WorkingDirectory = Path.GetDirectoryName(exe),
                    RedirectStandardIO = true,
                    CreateNoWindow = true
                },
                timeout,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "SteamCMD finished in {Duration} with exit code {Code}",
            result.Duration,
            result.ExitCode);
        return result;
    }

    private static string Quote(string path)
    {
        if (path.Contains('"'))
        {
            throw new UserFacingException(
                "Invalid path",
                "Paths passed to SteamCMD cannot contain double quotes.",
                "Choose a folder without \" characters.");
        }

        return path.Contains(' ') ? $"\"{path}\"" : path;
    }

    private UserFacingException CreateSteamCmdFailure(string title, ProcessExecutionResult result)
    {
        var logHint = Path.Combine(_paths.LogsDirectory, $"steamcmd-{DateTime.UtcNow:yyyyMMdd}.log");
        return new UserFacingException(
            title,
            $"SteamCMD exit code: {result.ExitCode}{(result.TimedOut ? " (timed out)" : string.Empty)}",
            $"Open the SteamCMD log for details:{Environment.NewLine}{logHint}");
    }
}
