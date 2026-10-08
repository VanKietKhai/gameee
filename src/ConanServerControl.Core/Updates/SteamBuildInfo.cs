using System.Globalization;
using System.Text.Json;

namespace ConanServerControl.Core.Updates;

public sealed record SteamPublicBuild(int AppId, string BuildId, DateTimeOffset? UpdatedAt);

/// <summary>Looks up the current public-branch build id of a Steam app.</summary>
public interface ISteamBuildInfoClient
{
    /// <summary>Null when the lookup failed or returned nothing usable.</summary>
    Task<SteamPublicBuild?> GetPublicBuildAsync(int appId, CancellationToken cancellationToken = default);
}

public static class SteamBuildInfo
{
    /// <summary>
    /// Parses an api.steamcmd.net <c>/v1/info/{appid}</c> reply:
    /// <c>data.{appid}.depots.branches.public.buildid</c> and <c>timeupdated</c> (unix seconds).
    /// </summary>
    public static SteamPublicBuild? ParseSteamCmdNet(int appId, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty(appId.ToString(CultureInfo.InvariantCulture), out var app)
                || !app.TryGetProperty("depots", out var depots)
                || !depots.TryGetProperty("branches", out var branches)
                || !branches.TryGetProperty("public", out var pub)
                || !pub.TryGetProperty("buildid", out var buildIdElement))
            {
                return null;
            }

            var buildId = ReadScalar(buildIdElement);
            if (string.IsNullOrWhiteSpace(buildId) || !buildId.All(char.IsAsciiDigit))
            {
                return null;
            }

            DateTimeOffset? updated = null;
            if (pub.TryGetProperty("timeupdated", out var timeElement)
                && long.TryParse(ReadScalar(timeElement), NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix)
                && unix > 0)
            {
                updated = DateTimeOffset.FromUnixTimeSeconds(unix);
            }

            return new SteamPublicBuild(appId, buildId, updated);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads <c>"buildid" "123"</c> from a Steam appmanifest .acf file.</summary>
    public static string? ReadManifestBuildId(string acf)
    {
        const string key = "\"buildid\"";
        var index = acf.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var rest = acf[(index + key.Length)..];
        var first = rest.IndexOf('"', StringComparison.Ordinal);
        var second = first < 0 ? -1 : rest.IndexOf('"', first + 1);
        if (first < 0 || second < 0)
        {
            return null;
        }

        var value = rest.Substring(first + 1, second - first - 1).Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>
    /// Where the appmanifest of an install can be: inside the install (SteamCMD
    /// <c>force_install_dir</c>), or in the Steam library that holds it
    /// (<c>&lt;library&gt;\steamapps\common\&lt;game&gt;</c> → <c>&lt;library&gt;\steamapps</c>).
    /// </summary>
    public static IEnumerable<string> ManifestCandidates(string? installDirectory, int appId)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            yield break;
        }

        var fileName = $"appmanifest_{appId.ToString(CultureInfo.InvariantCulture)}.acf";
        var install = Path.TrimEndingDirectorySeparator(installDirectory.Trim());
        yield return Path.Combine(install, "steamapps", fileName);

        var common = Path.GetDirectoryName(install);
        var steamapps = common is null ? null : Path.GetDirectoryName(common);
        if (common is not null
            && steamapps is not null
            && Path.GetFileName(common).Equals("common", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(steamapps).Equals("steamapps", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(steamapps, fileName);
        }
    }

    /// <summary>Build id from the first manifest found for any of the given install folders.</summary>
    public static string? FindInstalledBuild(int appId, params string?[] installDirectories)
    {
        foreach (var dir in installDirectories)
        {
            foreach (var manifest in ManifestCandidates(dir, appId))
            {
                try
                {
                    if (File.Exists(manifest))
                    {
                        var build = ReadManifestBuildId(File.ReadAllText(manifest));
                        if (build is not null)
                        {
                            return build;
                        }
                    }
                }
                catch (IOException)
                {
                    // Steam may be rewriting the manifest; try the next candidate.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        return null;
    }

    /// <summary>True only when both ids are numeric and the available one is higher.</summary>
    public static bool IsNewer(string? installed, string? available) =>
        long.TryParse(installed, NumberStyles.None, CultureInfo.InvariantCulture, out var current)
        && long.TryParse(available, NumberStyles.None, CultureInfo.InvariantCulture, out var latest)
        && latest > current;

    private static string? ReadScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetRawText(),
        _ => null
    };
}
