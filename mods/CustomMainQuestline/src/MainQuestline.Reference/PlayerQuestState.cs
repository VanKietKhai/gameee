using System.Text.Json;
using System.Text.Json.Serialization;

namespace MainQuestline.Reference;

/// <summary>One completed main quest, kept even if the quest later leaves the catalog.</summary>
public sealed record CompletedQuest(string QuestId, DateTimeOffset CompletedAtUtc, string Source);

/// <summary>
/// Per-player campaign state. Keyed by the stable player identity (the world's <c>characters.playerId</c>), never by
/// clan. Persisted by the mod's controller as one versioned record per player.
/// </summary>
public sealed class PlayerQuestState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public required string PlayerId { get; init; }

    /// <summary>Null when the campaign is complete.</summary>
    public string? CurrentQuestId { get; set; }

    public List<CompletedQuest> Completed { get; set; } = [];

    public string? TrackedQuestId { get; set; }

    public bool IsCompleted(string questId) => Completed.Any(c => string.Equals(c.QuestId, questId, StringComparison.Ordinal));
}

public sealed class QuestSaveException(string message) : Exception(message);

/// <summary>
/// Versioned (de)serialization of <see cref="PlayerQuestState"/>. Fails closed: unreadable data or a newer schema is
/// rejected instead of being replaced with an empty state, so progress is never wiped silently.
/// </summary>
public static class QuestSaveCodec
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Upgrade steps keyed by the version they upgrade FROM. Version 1 is the first; none exist yet.</summary>
    private static readonly Dictionary<int, Func<JsonElement, JsonElement>> Migrations = new();

    public static string Serialize(PlayerQuestState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != PlayerQuestState.CurrentSchemaVersion)
            throw new QuestSaveException($"refusing to write schema {state.SchemaVersion}; current is {PlayerQuestState.CurrentSchemaVersion}");
        return JsonSerializer.Serialize(state, Json);
    }

    public static PlayerQuestState Deserialize(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) throw new QuestSaveException("empty quest save");
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(payload).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new QuestSaveException("unreadable quest save: " + ex.Message);
        }

        if (!root.TryGetProperty("schemaVersion", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var version) || version < 1)
            throw new QuestSaveException("quest save has no valid schemaVersion");
        if (version > PlayerQuestState.CurrentSchemaVersion)
            throw new QuestSaveException($"quest save schema {version} is newer than this build ({PlayerQuestState.CurrentSchemaVersion}); not loaded, not overwritten");

        while (version < PlayerQuestState.CurrentSchemaVersion)
        {
            if (!Migrations.TryGetValue(version, out var step)) throw new QuestSaveException($"no migration from schema {version}");
            root = step(root);
            version++;
        }

        PlayerQuestState state;
        try
        {
            state = root.Deserialize<PlayerQuestState>(Json) ?? throw new QuestSaveException("quest save deserialized to null");
        }
        catch (JsonException ex)
        {
            throw new QuestSaveException("invalid quest save: " + ex.Message);
        }

        if (string.IsNullOrWhiteSpace(state.PlayerId)) throw new QuestSaveException("quest save has no playerId");
        state.SchemaVersion = PlayerQuestState.CurrentSchemaVersion;
        state.Completed ??= [];
        return state;
    }
}
