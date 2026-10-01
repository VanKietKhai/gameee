using System.Globalization;
using System.Text.Json;
using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Mods;
using ConanServerControl.Core.Validation;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Workshop;

public sealed class SteamWorkshopClient : ISteamWorkshopClient
{
    public const string HttpClientName = "steam-workshop";
    private const int BatchSize = 50;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SteamWorkshopClient> _logger;

    public SteamWorkshopClient(IHttpClientFactory httpClientFactory, ILogger<SteamWorkshopClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WorkshopPublishedFileDetails>> GetPublishedFileDetailsAsync(
        IReadOnlyList<long> workshopIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workshopIds);
        if (workshopIds.Count == 0)
        {
            return Array.Empty<WorkshopPublishedFileDetails>();
        }

        var results = new List<WorkshopPublishedFileDetails>();
        foreach (var batch in workshopIds.Distinct().Chunk(BatchSize))
        {
            results.AddRange(await FetchBatchAsync(batch, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    private async Task<IReadOnlyList<WorkshopPublishedFileDetails>> FetchBatchAsync(
        long[] batch,
        CancellationToken cancellationToken)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("itemcount", batch.Length.ToString(CultureInfo.InvariantCulture))
        };
        for (var i = 0; i < batch.Length; i++)
        {
            form.Add(new($"publishedfileids[{i}]", batch[i].ToString(CultureInfo.InvariantCulture)));
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(AppConstants.SteamPublishedFileDetailsUrl, content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new UserFacingException(
                "Steam Workshop lookup failed",
                $"Steam returned HTTP {(int)response.StatusCode} while reading Workshop details.",
                "Check internet access. Existing installed mods were not changed.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("response", out var root) ||
            !root.TryGetProperty("publishedfiledetails", out var details) ||
            details.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Steam Workshop response did not contain publishedfiledetails.");
            return Array.Empty<WorkshopPublishedFileDetails>();
        }

        var parsed = new List<WorkshopPublishedFileDetails>();
        foreach (var item in details.EnumerateArray())
        {
            var idText = item.TryGetProperty("publishedfileid", out var idEl) ? idEl.ToString() : null;
            if (!WorkshopIdValidator.TryParse(idText, out var id))
            {
                continue;
            }

            if (item.TryGetProperty("result", out var resultEl))
            {
                var result = resultEl.ValueKind == JsonValueKind.Number
                    ? resultEl.GetInt32()
                    : int.TryParse(resultEl.GetString(), out var parsedResult) ? parsedResult : 0;
                if (result != 1)
                {
                    continue;
                }
            }

            var title = item.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : null;
            var fileName = item.TryGetProperty("filename", out var fileEl) ? fileEl.GetString() : null;
            long unix = 0;
            if (item.TryGetProperty("time_updated", out var timeEl))
            {
                if (timeEl.ValueKind == JsonValueKind.Number)
                {
                    unix = timeEl.GetInt64();
                }
                else if (long.TryParse(timeEl.GetString(), out var parsedUnix))
                {
                    unix = parsedUnix;
                }
            }

            parsed.Add(new WorkshopPublishedFileDetails
            {
                WorkshopId = id,
                Title = string.IsNullOrWhiteSpace(title) ? $"Workshop {id}" : title!,
                TimeUpdated = WorkshopUpdateComparer.FromUnixSeconds(unix),
                FileName = string.IsNullOrWhiteSpace(fileName) ? null : Path.GetFileName(fileName)
            });
        }

        return parsed;
    }
}
