namespace MainQuestline.Reference;

/// <summary>What completes a quest. Phase 14A supports boss kills only.</summary>
public enum QuestTargetType
{
    BossKill
}

/// <summary>
/// Whether a target's identity was proven from the exact game build's assets or logs. Only <see cref="Verified"/>
/// targets can ever grant credit; an unverified target fails closed (never matches a death event).
/// </summary>
public enum TargetVerification
{
    Unverified,
    Verified
}

/// <summary>
/// The thing a quest asks the player to kill. <see cref="TargetClassPath"/> is compared ordinally and exactly with the
/// class path reported by the server for the dead NPC; nothing is matched by substring or wildcard.
/// </summary>
public sealed record QuestTarget(
    QuestTargetType Type,
    string DisplayName,
    string? TargetClassPath,
    TargetVerification Verification,
    IReadOnlyList<string>? Candidates = null,
    string? VerificationNote = null)
{
    public bool CanGrantCredit => Verification == TargetVerification.Verified && !string.IsNullOrWhiteSpace(TargetClassPath);
}

/// <summary>General area marker (dungeon/encounter area, not the boss's exact coordinates).</summary>
public sealed record MapMarker(string Label, double X, double Y, double? Z, double AreaRadius);

public enum RewardType
{
    Xp,
    Item,
    Consumable,
    Recipe,
    Utility,
    Milestone
}

public sealed record RewardEntry(RewardType Type, string Id, int Amount);

public sealed record RewardDefinition(string RewardId, IReadOnlyList<RewardEntry> Entries, bool Placeholder);

/// <summary>One main-quest record (maps 1:1 to a DataTable row in the mod).</summary>
public sealed record QuestDefinition
{
    public required string QuestId { get; init; }
    public required string ActId { get; init; }
    public required int Sequence { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required QuestTarget Target { get; init; }
    public string? PreviousQuestId { get; init; }
    public string? NextQuestId { get; init; }
    public required int MinimumLevel { get; init; }
    public required int RecommendedLevelMin { get; init; }
    public required int RecommendedLevelMax { get; init; }
    public required int RecommendedPartyMin { get; init; }
    public required int RecommendedPartyMax { get; init; }
    public required string LocationName { get; init; }
    public MapMarker? Marker { get; init; }
    public required RewardDefinition Reward { get; init; }

    /// <summary>Credit radius around the boss's death location, in Unreal units (cm).</summary>
    public required double CompletionCreditRadius { get; init; }

    public required int CampaignWeight { get; init; }
    public bool Enabled { get; init; } = true;
}

public sealed class QuestCatalogException(IReadOnlyList<string> errors)
    : Exception("Invalid main-quest catalog: " + string.Join(" | ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>
/// A validated, immutable main-quest catalog. Construction fails closed: any malformed definition rejects the whole
/// catalog (the mod must then refuse to grant progression rather than run on partial data).
/// </summary>
public sealed class QuestCatalog
{
    public const double MaxCreditRadius = 20_000; // 200 m

    private readonly Dictionary<string, QuestDefinition> _byId;

    public QuestCatalog(IEnumerable<QuestDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var all = definitions.ToList();
        var errors = Validate(all);
        if (errors.Count > 0)
        {
            throw new QuestCatalogException(errors);
        }

        _byId = all.ToDictionary(q => q.QuestId, StringComparer.Ordinal);
        Enabled = all.Where(q => q.Enabled).OrderBy(q => q.Sequence).ToList();
        First = Enabled.Single(q => q.PreviousQuestId is null);
    }

    public IReadOnlyList<QuestDefinition> Enabled { get; }

    public QuestDefinition First { get; }

    public QuestDefinition? Find(string? questId) =>
        questId is not null && _byId.TryGetValue(questId, out var q) && q.Enabled ? q : null;

    public int TotalWeight => Enabled.Sum(q => q.CampaignWeight);

    private static List<string> Validate(List<QuestDefinition> all)
    {
        var errors = new List<string>();
        if (all.Count == 0)
        {
            errors.Add("catalog is empty");
            return errors;
        }

        foreach (var g in all.GroupBy(q => q.QuestId, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            errors.Add($"duplicate QuestId {g.Key}");
        }

        var enabled = all.Where(q => q.Enabled).ToList();
        var ids = enabled.Select(q => q.QuestId).ToHashSet(StringComparer.Ordinal);
        foreach (var g in enabled.GroupBy(q => q.Sequence).Where(g => g.Count() > 1))
        {
            errors.Add($"duplicate Sequence {g.Key}");
        }

        foreach (var q in all)
        {
            var p = $"{q.QuestId}:";
            if (string.IsNullOrWhiteSpace(q.QuestId) || q.QuestId.Any(char.IsWhiteSpace)) errors.Add($"{p} invalid QuestId");
            if (string.IsNullOrWhiteSpace(q.ActId)) errors.Add($"{p} missing ActId");
            if (string.IsNullOrWhiteSpace(q.DisplayName)) errors.Add($"{p} missing DisplayName");
            if (string.IsNullOrWhiteSpace(q.LocationName)) errors.Add($"{p} missing LocationName");
            if (q.Target is null || string.IsNullOrWhiteSpace(q.Target.DisplayName)) errors.Add($"{p} missing target");
            if (q.Target is { Verification: TargetVerification.Verified } && string.IsNullOrWhiteSpace(q.Target.TargetClassPath))
                errors.Add($"{p} verified target without TargetClassPath");
            if (q.MinimumLevel < 1) errors.Add($"{p} MinimumLevel < 1");
            if (q.RecommendedLevelMin < 1 || q.RecommendedLevelMax < q.RecommendedLevelMin) errors.Add($"{p} bad recommended level range");
            if (q.RecommendedPartyMin < 1 || q.RecommendedPartyMax < q.RecommendedPartyMin) errors.Add($"{p} bad recommended party range");
            if (!(q.CompletionCreditRadius > 0) || q.CompletionCreditRadius > MaxCreditRadius) errors.Add($"{p} CompletionCreditRadius out of range");
            if (q.CampaignWeight < 1) errors.Add($"{p} CampaignWeight < 1");
            if (q.Reward is null || string.IsNullOrWhiteSpace(q.Reward.RewardId)) errors.Add($"{p} missing reward");
            else if (q.Reward.Entries.Any(e => e.Amount < 1 || string.IsNullOrWhiteSpace(e.Id))) errors.Add($"{p} bad reward entry");
            if (q.Marker is { AreaRadius: <= 0 }) errors.Add($"{p} marker radius <= 0");
            if (!q.Enabled) continue;
            if (q.PreviousQuestId is not null && !ids.Contains(q.PreviousQuestId)) errors.Add($"{p} PreviousQuestId {q.PreviousQuestId} is not an enabled quest");
            if (q.NextQuestId is not null && !ids.Contains(q.NextQuestId)) errors.Add($"{p} NextQuestId {q.NextQuestId} is not an enabled quest");
            if (q.PreviousQuestId == q.QuestId || q.NextQuestId == q.QuestId) errors.Add($"{p} links to itself");
        }

        var byId = enabled.GroupBy(q => q.QuestId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var q in enabled.Where(q => q.NextQuestId is not null && byId.ContainsKey(q.NextQuestId)))
        {
            var next = byId[q.NextQuestId!];
            if (!string.Equals(next.PreviousQuestId, q.QuestId, StringComparison.Ordinal)) errors.Add($"{q.QuestId}: Next {next.QuestId} does not point back (Previous={next.PreviousQuestId ?? "none"})");
            if (next.Sequence <= q.Sequence) errors.Add($"{q.QuestId}: Next {next.QuestId} has a lower or equal Sequence");
        }

        var roots = enabled.Where(q => q.PreviousQuestId is null).ToList();
        if (roots.Count != 1) errors.Add($"expected exactly one first quest (no PreviousQuestId), found {roots.Count}");
        else if (errors.Count == 0)
        {
            // the chain from the root must reach every enabled quest exactly once (no cycles, no orphans)
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (var cur = roots[0]; cur is not null; cur = cur.NextQuestId is null ? null : byId[cur.NextQuestId])
            {
                if (!visited.Add(cur.QuestId)) { errors.Add($"cycle at {cur.QuestId}"); break; }
            }

            foreach (var orphan in enabled.Where(q => !visited.Contains(q.QuestId))) errors.Add($"{orphan.QuestId}: not reachable from the first quest");
        }

        return errors;
    }
}
