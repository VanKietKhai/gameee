using ConanServerControl.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Steam;

/// <summary>
/// Connects Cloudflare WARP (<c>warp-cli connect</c>) for the duration of SteamCMD work when
/// <see cref="Core.Settings.SteamCmdSettings.UseCloudflareWarp"/> is on, because SteamCMD cannot
/// reach Steam on this host's network without it. WARP is disconnected again afterwards, unless it
/// was already connected before (then it is left as the user had it).
/// </summary>
public sealed class WarpNetworkGuard : ISteamNetworkGuard
{
    public const string DefaultCliPath = @"C:\Program Files\Cloudflare\Cloudflare WARP\warp-cli.exe";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(20);

    private readonly ISettingsService _settings;
    private readonly IProcessRunner _runner;
    private readonly IActivityLog _activity;
    private readonly ILogger<WarpNetworkGuard> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _leases;
    private bool _connectedByUs;

    public WarpNetworkGuard(ISettingsService settings, IProcessRunner runner, IActivityLog activity, ILogger<WarpNetworkGuard> logger)
    {
        _settings = settings;
        _runner = runner;
        _activity = activity;
        _logger = logger;
    }

    public async Task<IAsyncDisposable> AcquireAsync(string reason, CancellationToken cancellationToken = default)
    {
        var cli = CliPath();
        if (!_settings.Current.SteamCmd.UseCloudflareWarp || cli is null)
        {
            return NoOpAsyncDisposable.Instance;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_leases++ == 0)
            {
                if (await IsConnectedAsync(cli, cancellationToken).ConfigureAwait(false))
                {
                    _connectedByUs = false;
                }
                else
                {
                    await ConnectAsync(cli, reason, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            _leases--;
            throw;
        }
        finally
        {
            _lock.Release();
        }

        return new Lease(this, cli);
    }

    private async Task ConnectAsync(string cli, string reason, CancellationToken cancellationToken)
    {
        await RunAsync(cli, "connect", cancellationToken).ConfigureAwait(false);
        var deadline = DateTimeOffset.UtcNow + ConnectTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await IsConnectedAsync(cli, cancellationToken).ConfigureAwait(false))
            {
                _connectedByUs = true;
                // Give routing and DNS a moment before SteamCMD starts.
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Cloudflare WARP connected for {Reason}.", reason);
                await _activity.AddAsync("Steam", $"Cloudflare WARP connected for {reason}.", cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        // Still ours to disconnect on release; SteamCMD reports its own network error if any.
        _connectedByUs = true;
        _logger.LogWarning("Cloudflare WARP did not report Connected within {Seconds} s; continuing.", ConnectTimeout.TotalSeconds);
    }

    private async Task ReleaseAsync(string cli)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (--_leases > 0 || !_connectedByUs)
            {
                return;
            }

            _connectedByUs = false;
            await RunAsync(cli, "disconnect", CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation("Cloudflare WARP disconnected after the Steam download.");
            await _activity.AddAsync("Steam", "Cloudflare WARP disconnected.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not disconnect Cloudflare WARP; disconnect it manually.");
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<bool> IsConnectedAsync(string cli, CancellationToken cancellationToken)
    {
        var result = await RunAsync(cli, "status", cancellationToken).ConfigureAwait(false);
        return IsConnectedStatus(result.StandardOutput);
    }

    /// <summary>"Status update: Connected" (not "Connecting" / "Disconnected").</summary>
    internal static bool IsConnectedStatus(string statusOutput) =>
        statusOutput.Split('\n')
            .Select(l => l.Trim())
            .Any(l => l.Equals("Status update: Connected", StringComparison.OrdinalIgnoreCase));

    private Task<Core.Models.ProcessExecutionResult> RunAsync(string cli, string arguments, CancellationToken cancellationToken) =>
        _runner.RunAsync(
            new ProcessStartRequest { FileName = cli, Arguments = arguments, RedirectStandardIO = true, CreateNoWindow = true },
            CommandTimeout,
            cancellationToken: cancellationToken);

    private string? CliPath()
    {
        var configured = _settings.Current.SteamCmd.WarpCliPath;
        var path = string.IsNullOrWhiteSpace(configured) ? DefaultCliPath : configured.Trim();
        return File.Exists(path) ? path : null;
    }

    private sealed class Lease : IAsyncDisposable
    {
        private readonly WarpNetworkGuard _owner;
        private readonly string _cli;
        private int _disposed;

        public Lease(WarpNetworkGuard owner, string cli)
        {
            _owner = owner;
            _cli = cli;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                await _owner.ReleaseAsync(_cli).ConfigureAwait(false);
            }
        }
    }
}
