using System.Text.Json;
using System.Text.Json.Serialization;

namespace MainQuestline.Reference;

/// <summary>Campaign data file: a schema version, the acts and the quest records (the sources for the mod's DataTables).</summary>
public sealed record QuestDataFile(int DataSchemaVersion, string Status, IReadOnlyList<ActDefinition> Acts, IReadOnlyList<QuestDefinition> Quests);

public static class QuestDataLoader
{
    /// <summary>2 added acts, quest objectives and locked-quest disclosure (Main Quest tab). Version 1 never shipped in a mod.</summary>
    public const int CurrentDataSchemaVersion = 2;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>Parses and validates a campaign data file. Any malformed field or record rejects the whole file.</summary>
    public static (QuestDataFile File, QuestCatalog Catalog) Load(string json)
    {
        QuestDataFile? file;
        try
        {
            file = JsonSerializer.Deserialize<QuestDataFile>(json, Json);
        }
        catch (JsonException ex)
        {
            throw new QuestCatalogException([$"malformed quest data: {ex.Message}"]);
        }

        if (file is null) throw new QuestCatalogException(["quest data is empty"]);
        if (file.DataSchemaVersion != CurrentDataSchemaVersion)
            throw new QuestCatalogException([$"unsupported dataSchemaVersion {file.DataSchemaVersion}"]);
        if (file.Acts is null || file.Acts.Count == 0) throw new QuestCatalogException(["quest data has no acts"]);
        return (file, new QuestCatalog(file.Quests ?? [], file.Acts));
    }
}
