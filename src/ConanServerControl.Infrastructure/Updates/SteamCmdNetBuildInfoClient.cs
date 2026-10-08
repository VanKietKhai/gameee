using System.Globalization;
using ConanServerControl.Core.Updates;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Updates;

/// <summary>
/// Latest public build ids from api.steamcmd.net (a public mirror of SteamCMD <c>app_info</c>).
/// Used because SteamCMD itself cannot reach Steam on every network, and the Steam Web API
/// does not expose depot build ids without a publisher key.
/// </summary>
public sealed class SteamCmdNetBuildInfoClient : ISteamBuildInfoClient
{
    public const string HttpClientName = "steam-build-info";
    private const string InfoUrl = "https://api.steamcmd.net/v1/info/";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<SteamCmdNetBuildInfoClient> _logger;

    public SteamCmdNetBuildInfoClient(IHttpClientFactory httpFactory, ILogger<SteamCmdNetBuildInfoClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<SteamPublicBuild?> GetPublicBuildAsync(int appId, CancellationToken cancellationToken = default)
    {
        var client = _httpFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(InfoUrl + appId.ToString(CultureInfo.InvariantCulture), cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Steam build lookup for app {AppId} returned HTTP {Status}.", appId, (int)response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var build = SteamBuildInfo.ParseSteamCmdNet(appId, json);
        if (build is null)
        {
            _logger.LogWarning("Steam build lookup for app {AppId} returned no public build id.", appId);
        }

        return build;
    }
}
