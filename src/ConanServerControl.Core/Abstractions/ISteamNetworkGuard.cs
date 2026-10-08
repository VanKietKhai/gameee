namespace ConanServerControl.Core.Abstractions;

/// <summary>
/// Makes Steam reachable for SteamCMD downloads where the local network blocks it
/// (for example by connecting Cloudflare WARP), and undoes that when the last user releases.
/// Leases nest: only the outermost acquire connects and only its release disconnects.
/// </summary>
public interface ISteamNetworkGuard
{
    Task<IAsyncDisposable> AcquireAsync(string reason, CancellationToken cancellationToken = default);
}

public sealed class NoOpAsyncDisposable : IAsyncDisposable
{
    public static readonly NoOpAsyncDisposable Instance = new();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
