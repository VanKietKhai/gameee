namespace MainQuestline.Reference;

/// <summary>Display status of one quest. Derived from the player's saved state, never stored.</summary>
public enum QuestStatus
{
    Locked,
    Active,
    Completed
}

/// <summary>Completed: every enabled quest in the act is done. Active: holds the current quest or some completed quest. Locked: otherwise.</summary>
public enum ActStatus
{
    Locked,
    Active,
    Completed
}

/// <summary>One row in an act's quest list (left column).</summary>
public sealed record QuestRowView(string QuestId, int Sequence, QuestStatus Status, string Title, bool IsTracked);

/// <summary>One act in the left column, with its own weighted progress.</summary>
public sealed record ActView(
    string ActId,
    int Order,
    string DisplayName,
    ActStatus Status,
    int CompletedQuests,
    int TotalQuests,
    double Progress,
    int ProgressPercent,
    IReadOnlyList<QuestRowView> Quests);

/// <summary>
/// The main panel for one quest. Fields a LOCKED quest's <see cref="LockedDisclosure"/> hides are null, so the
/// widget renders "???" and cannot leak them by accident.
/// </summary>
public sealed record QuestDetailView(
    string QuestId,
    string ActId,
    string ActDisplayName,
    QuestStatus Status,
    string Title,
    string? BossName,
    string? Description,
    string? Objective,
    string? Location,
    // In-game map grid cell of the entrance (e.g. "D4"); hidden together with Location.
    string? MapGrid,
    // Player tips (how to enter, what to bring); null while a locked quest hides them.
    IReadOnlyList<string>? Hints,
    string? RecommendedLevel,
    string? RecommendedParty,
    IReadOnlyList<string>? Reward,
    bool RewardIsPlaceholder,
    // Active only: the player meets the quest's minimum level and the previous quest is done.
    bool Available,
    // Why an active quest is not yet available (e.g. "Đạt cấp 25 để bắt đầu."), or the locked hint.
    string? StatusNote,
    // False while the target is unverified: the boss cannot grant credit yet (staging/QA display).
    bool CreditEnabled,
    bool IsTracked,
    bool CanTrack);

/// <summary>Completed / History section entry. Retired = completed quest that is no longer an enabled catalog quest.</summary>
public sealed record HistoryEntryView(string QuestId, string Title, string? BossName, string? ActDisplayName, DateTimeOffset CompletedAtUtc, bool Retired);

/// <summary>Track Quest state and the resulting marker.</summary>
public sealed record TrackingView(string? TrackedQuestId, bool MarkerVisible, MapMarker? Marker, string? MarkerHiddenReason);

/// <summary>Everything the Main Quest tab displays. Rebuilt whenever the replicated quest state changes.</summary>
public sealed record MainQuestView(
    string PlayerId,
    int CompletedQuests,
    int TotalQuests,
    double CampaignProgress,
    int CampaignProgressPercent,
    bool CampaignComplete,
    IReadOnlyList<ActView> Acts,
    QuestDetailView? Current,
    IReadOnlyList<HistoryEntryView> History,
    TrackingView Tracking);

/// <summary>
/// Read-only projection of a player's authoritative quest state onto the Main Quest tab. It decides nothing:
/// progression, credit and tracking changes happen only in <see cref="CampaignEngine"/> on the server.
/// </summary>
public static class MainQuestViews
{
    public const string LockedHint = MainQuestText.LockedHint;
    public const string Unknown = "???";

    public static MainQuestView BuildView(this CampaignEngine engine, PlayerQuestState state, int playerLevel)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(state);
        var catalog = engine.Catalog;

        var acts = catalog.Acts.Select(a =>
        {
            var quests = catalog.QuestsInAct(a.ActId);
            var done = quests.Where(q => state.IsCompleted(q.QuestId)).ToList();
            var (progress, percent) = Ratio(done.Sum(q => q.CampaignWeight), quests.Sum(q => q.CampaignWeight));
            var status = done.Count == quests.Count ? ActStatus.Completed
                : done.Count > 0 || quests.Any(q => IsCurrent(state, q)) ? ActStatus.Active
                : ActStatus.Locked;
            var rows = quests.Select(q =>
            {
                var s = StatusOf(state, q);
                var title = s == QuestStatus.Locked && q.LockedDisclosure == LockedDisclosure.Hidden ? Unknown : q.DisplayName;
                return new QuestRowView(q.QuestId, q.Sequence, s, title, s == QuestStatus.Active && IsTracked(state, q));
            }).ToList();
            return new ActView(a.ActId, a.Order, a.DisplayName, status, done.Count, quests.Count, progress, percent, rows);
        }).ToList();

        var completedEnabled = catalog.Enabled.Count(q => state.IsCompleted(q.QuestId));
        var (campaign, campaignPercent) = Ratio(
            catalog.Enabled.Where(q => state.IsCompleted(q.QuestId)).Sum(q => q.CampaignWeight), catalog.TotalWeight);
        var campaignComplete = completedEnabled == catalog.Enabled.Count;

        var current = catalog.Find(state.CurrentQuestId) is { } cur && !state.IsCompleted(cur.QuestId)
            ? engine.Detail(state, playerLevel, cur.QuestId)
            : null;

        var history = state.Completed
            .Select((c, i) => (c, i))
            .OrderBy(x => x.c.CompletedAtUtc).ThenBy(x => x.i)
            .Select(x =>
            {
                var def = catalog.Lookup(x.c.QuestId);
                var enabled = catalog.Find(x.c.QuestId) is not null;
                return new HistoryEntryView(x.c.QuestId, def?.DisplayName ?? x.c.QuestId, def?.Target.DisplayName,
                    enabled ? catalog.Act(def!.ActId).DisplayName : null, x.c.CompletedAtUtc, !enabled);
            }).ToList();

        return new MainQuestView(state.PlayerId, completedEnabled, catalog.Enabled.Count, campaign, campaignPercent, campaignComplete,
            acts, current, history, Tracking(engine, state, playerLevel));
    }

    /// <summary>The main panel for any enabled quest the player selects in the left column.</summary>
    public static QuestDetailView Detail(this CampaignEngine engine, PlayerQuestState state, int playerLevel, string questId)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(state);
        var q = engine.Catalog.Find(questId) ?? throw new ArgumentException($"unknown or disabled quest {questId}", nameof(questId));
        var act = engine.Catalog.Act(q.ActId);
        var status = StatusOf(state, q);
        var level = Range(q.RecommendedLevelMin, q.RecommendedLevelMax);
        var party = Range(q.RecommendedPartyMin, q.RecommendedPartyMax);
        var reward = q.Reward.Entries.Select(FormatReward).ToList();
        var objective = q.Objective ?? string.Format(MainQuestText.DefeatFormat, q.Target.DisplayName);

        if (status == QuestStatus.Locked)
        {
            return q.LockedDisclosure switch
            {
                LockedDisclosure.Full => new QuestDetailView(q.QuestId, q.ActId, act.DisplayName, status, q.DisplayName, q.Target.DisplayName,
                    q.Description, objective, q.LocationName, q.MapGrid, q.Hints, level, party, reward, q.Reward.Placeholder, false, LockedHint,
                    q.Target.CanGrantCredit, false, false),
                LockedDisclosure.Partial => new QuestDetailView(q.QuestId, q.ActId, act.DisplayName, status, q.DisplayName, null,
                    null, null, null, null, null, level, party, null, false, false, LockedHint, q.Target.CanGrantCredit, false, false),
                _ => new QuestDetailView(q.QuestId, q.ActId, act.DisplayName, status, Unknown, null,
                    null, null, null, null, null, null, null, null, false, false, LockedHint, q.Target.CanGrantCredit, false, false)
            };
        }

        var active = status == QuestStatus.Active;
        var available = active && engine.IsCurrentQuestAvailable(state, playerLevel);
        string? note = !active || available ? null
            : playerLevel < q.MinimumLevel ? string.Format(MainQuestText.ReachLevelFormat, q.MinimumLevel)
            : LockedHint;
        return new QuestDetailView(q.QuestId, q.ActId, act.DisplayName, status, q.DisplayName, q.Target.DisplayName, q.Description,
            objective, q.LocationName, q.MapGrid, q.Hints, level, party, reward, q.Reward.Placeholder, available, note, q.Target.CanGrantCredit,
            active && IsTracked(state, q), active);
    }

    private static TrackingView Tracking(CampaignEngine engine, PlayerQuestState state, int playerLevel)
    {
        var marker = engine.ActiveMarker(state, playerLevel);
        if (marker is not null) return new TrackingView(state.TrackedQuestId, true, marker, null);
        var cur = engine.Catalog.Find(state.CurrentQuestId);
        var reason = cur is null || state.IsCompleted(cur.QuestId) ? MainQuestText.MarkerNoActiveQuest
            : !string.Equals(state.TrackedQuestId, cur.QuestId, StringComparison.Ordinal) ? MainQuestText.MarkerNotTracked
            : playerLevel < cur.MinimumLevel ? string.Format(MainQuestText.MarkerRequiresLevelFormat, cur.MinimumLevel)
            : !engine.IsCurrentQuestAvailable(state, playerLevel) ? MainQuestText.MarkerPreviousNotDone
            : MainQuestText.MarkerNone;
        return new TrackingView(state.TrackedQuestId, false, null, reason);
    }

    private static QuestStatus StatusOf(PlayerQuestState state, QuestDefinition q) =>
        state.IsCompleted(q.QuestId) ? QuestStatus.Completed
        : IsCurrent(state, q) ? QuestStatus.Active
        : QuestStatus.Locked;

    private static bool IsCurrent(PlayerQuestState state, QuestDefinition q) =>
        string.Equals(state.CurrentQuestId, q.QuestId, StringComparison.Ordinal) && !state.IsCompleted(q.QuestId);

    private static bool IsTracked(PlayerQuestState state, QuestDefinition q) =>
        string.Equals(state.TrackedQuestId, q.QuestId, StringComparison.Ordinal);

    /// <summary>Percent is floored, so 100% shows only when every weighted quest is complete.</summary>
    private static (double Ratio, int Percent) Ratio(int done, int total) =>
        total <= 0 ? (0, 0) : ((double)done / total, done * 100 / total);

    private static string Range(int min, int max) => min == max ? $"{min}" : $"{min}–{max}";

    private static string FormatReward(RewardEntry e) => e.Type switch
    {
        RewardType.Xp => string.Format(MainQuestText.XpFormat, e.Amount),
        _ => e.Amount == 1 ? $"{e.Type}: {e.Id}" : $"{e.Type}: {e.Id} x{e.Amount}"
    };
}
