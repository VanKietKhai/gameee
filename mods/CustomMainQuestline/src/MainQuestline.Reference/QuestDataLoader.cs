using System.Text.Json;
using System.Text.Json.Serialization;

namespace MainQuestline.Reference;

/// <summary>Campaign data file: a schema version plus the quest records (the source for the mod's DataTable).</summary>
public sealed record QuestDataFile(int DataSchemaVersion, string Status, IReadOnlyList<QuestDefinition> Quests);

public static class QuestDataLoader
{
    public const int CurrentDataSchemaVersion = 1;

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
        return (file, new QuestCatalog(file.Quests ?? []));
    }
}
