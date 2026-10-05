namespace MainQuestline.Reference;

/// <summary>A player as seen by the server at the moment a boss died.</summary>
public sealed record PlayerSnapshot(string PlayerId, int Level, double X, double Y, double Z, bool ParticipatedInEncounter = false);

/// <summary>
/// A server-authoritative boss death. <see cref="EventId"/> is unique per death (e.g. the dead actor's persistent id
/// plus its death time) so a duplicated notification can never complete a quest twice.
/// </summary>
public sealed record BossDeathEvent(string EventId, string DeadClassPath, double X, double Y, double Z, IReadOnlyList<PlayerSnapshot> NearbyPlayers);

public enum CreditRule
{
    /// <summary>Any eligible player inside the quest's credit radius at the moment of death. No killing blow needed, so thrall/follower kills still credit their owner.</summary>
    ProximityOnly,

    /// <summary>Inside the radius AND flagged as having participated (damaged the boss, or owns a follower that did).</summary>
    ProximityAndParticipation
}

/// <summary>The three lightweight banner types. They are shown by the HUD whether or not the Main Quest tab is open.</summary>
public enum NotificationKind
{
    QuestComplete,
    NewBossUnlocked,
    CampaignMilestone
}

public enum MilestoneKind
{
    ActComplete,
    CampaignComplete
}

/// <summary>
/// One banner for one player (server → owning client). <see cref="NotificationId"/> is deterministic per completion,
/// so a client can drop a duplicate delivery. Banners are not persisted or replayed: the quest state is the record.
/// </summary>
public sealed record QuestNotification(
    string NotificationId,
    string PlayerId,
    NotificationKind Kind,
    MilestoneKind? Milestone,
    string QuestId,
    string ActId,
    string Title,
    string Subtitle);

/// <summary>Result of a Track Quest request. A rejected request leaves the state unchanged.</summary>
public sealed record TrackingChange(bool Accepted, bool Changed, string Reason);

public sealed record CreditDecision(string PlayerId, bool Credited, string Reason);

public sealed record BossDeathResult(
    string EventId,
    string? QuestId,
    IReadOnlyList<CreditDecision> Decisions,
    IReadOnlyList<PlayerQuestState> ChangedStates,
    IReadOnlyList<QuestNotification> Notifications);

/// <summary>Who is calling an admin/QA operation. Normal players are rejected.</summary>
public sealed record AdminContext(string CallerId, bool IsAdmin);

public sealed class QuestAuthorizationException(string message) : Exception(message);

/// <summary>
/// Server-authoritative main-quest rules (the reference for the mod's Blueprint implementation). The caller owns
/// persistence: every state in <see cref="BossDeathResult.ChangedStates"/> must be saved immediately.
/// </summary>
public sealed class CampaignEngine
{
    private readonly QuestCatalog _catalog;
    private readonly CreditRule _rule;
    private readonly int _maxRememberedEvents;
    private readonly Queue<string> _eventOrder = new();
    private readonly HashSet<string> _processedEvents = new(StringComparer.Ordinal);

    public CampaignEngine(QuestCatalog catalog, CreditRule rule = CreditRule.ProximityOnly, int maxRememberedEvents = 1024)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _rule = rule;
        _maxRememberedEvents = Math.Max(16, maxRememberedEvents);
    }

    public QuestCatalog Catalog => _catalog;

    public PlayerQuestState NewPlayer(string playerId) =>
        new() { PlayerId = playerId, CurrentQuestId = _catalog.First.QuestId, TrackedQuestId = _catalog.First.QuestId };

    /// <summary>The current quest is available (shown, marker on) once the player reaches its minimum level.</summary>
    public bool IsCurrentQuestAvailable(PlayerQuestState state, int playerLevel) =>
        _catalog.Find(state.CurrentQuestId) is { } q && playerLevel >= q.MinimumLevel && PreviousSatisfied(state, q);

    /// <summary>Marker of the tracked quest, only while that quest is the available current quest.</summary>
    public MapMarker? ActiveMarker(PlayerQuestState state, int playerLevel) =>
        IsCurrentQuestAvailable(state, playerLevel) &&
        string.Equals(state.TrackedQuestId, state.CurrentQuestId, StringComparison.Ordinal)
            ? _catalog.Find(state.CurrentQuestId)!.Marker
            : null;

    /// <summary>completed enabled weight / total enabled weight, in [0, 1]. Independent of player level.</summary>
    public double Progress(PlayerQuestState state)
    {
        var total = _catalog.TotalWeight;
        var done = _catalog.Enabled.Where(q => state.IsCompleted(q.QuestId)).Sum(q => q.CampaignWeight);
        return total == 0 ? 0 : (double)done / total;
    }

    /// <summary>
    /// Server handler for the Track Quest toggle. Only the player's current, not-yet-completed quest can be tracked;
    /// null untracks. Tracking only drives the marker and never affects progression. The caller persists on Changed.
    /// </summary>
    public TrackingChange SetTrackedQuest(PlayerQuestState state, string? questId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (questId is null)
        {
            var had = state.TrackedQuestId is not null;
            state.TrackedQuestId = null;
            return new TrackingChange(true, had, "untracked");
        }

        if (!string.Equals(questId, state.CurrentQuestId, StringComparison.Ordinal) || _catalog.Find(questId) is null)
            return new TrackingChange(false, false, $"{questId} is not the current quest");
        if (state.IsCompleted(questId)) return new TrackingChange(false, false, $"{questId} is already completed");
        var changed = !string.Equals(state.TrackedQuestId, questId, StringComparison.Ordinal);
        state.TrackedQuestId = questId;
        return new TrackingChange(true, changed, "tracked");
    }

    public BossDeathResult OnBossDeath(BossDeathEvent death, Func<string, PlayerQuestState?> loadState)
    {
        ArgumentNullException.ThrowIfNull(death);
        ArgumentNullException.ThrowIfNull(loadState);
        if (string.IsNullOrWhiteSpace(death.EventId) || !_processedEvents.Add(death.EventId))
        {
            return new BossDeathResult(death.EventId, null, [new CreditDecision("*", false, "duplicate or missing death event id")], [], []);
        }

        Remember(death.EventId);
        var quest = _catalog.Enabled.FirstOrDefault(q =>
            q.Target.Type == QuestTargetType.BossKill && q.Target.CanGrantCredit &&
            string.Equals(q.Target.TargetClassPath, death.DeadClassPath, StringComparison.Ordinal));
        if (quest is null)
        {
            return new BossDeathResult(death.EventId, null, [new CreditDecision("*", false, "not a verified main-quest boss")], [], []);
        }

        var decisions = new List<CreditDecision>();
        var changed = new List<PlayerQuestState>();
        var notes = new List<QuestNotification>();
        foreach (var p in death.NearbyPlayers.GroupBy(p => p.PlayerId, StringComparer.Ordinal).Select(g => g.First()))
        {
            var state = loadState(p.PlayerId);
            var reason = Ineligible(state, p, quest, death);
            if (reason is not null)
            {
                decisions.Add(new CreditDecision(p.PlayerId, false, reason));
                continue;
            }

            Complete(state!, quest, $"boss:{death.EventId}", notes);
            changed.Add(state!);
            decisions.Add(new CreditDecision(p.PlayerId, true, "credited"));
        }

        return new BossDeathResult(death.EventId, quest.QuestId, decisions, changed, notes);
    }

    private string? Ineligible(PlayerQuestState? state, PlayerSnapshot p, QuestDefinition quest, BossDeathEvent death)
    {
        if (state is null) return "no quest state";
        if (!string.Equals(state.CurrentQuestId, quest.QuestId, StringComparison.Ordinal)) return $"current quest is {state.CurrentQuestId ?? "none"}, not {quest.QuestId}";
        if (state.IsCompleted(quest.QuestId)) return "already completed";
        if (!PreviousSatisfied(state, quest)) return $"previous quest {quest.PreviousQuestId} not completed";
        if (p.Level < quest.MinimumLevel) return $"level {p.Level} < {quest.MinimumLevel}";
        var dx = p.X - death.X; var dy = p.Y - death.Y; var dz = p.Z - death.Z;
        if (Math.Sqrt(dx * dx + dy * dy + dz * dz) > quest.CompletionCreditRadius) return "outside credit radius";
        if (_rule == CreditRule.ProximityAndParticipation && !p.ParticipatedInEncounter) return "did not participate";
        return null;
    }

    private static bool PreviousSatisfied(PlayerQuestState state, QuestDefinition q) =>
        q.PreviousQuestId is null || state.IsCompleted(q.PreviousQuestId);

    /// <summary>
    /// Banner order: QUEST COMPLETE, then CAMPAIGN MILESTONE (act complete) if the act just finished, then NEW BOSS
    /// UNLOCKED. The final quest emits a single CAMPAIGN MILESTONE (campaign complete) instead of the last two.
    /// </summary>
    private void Complete(PlayerQuestState state, QuestDefinition quest, string source, List<QuestNotification> notes)
    {
        state.Completed.Add(new CompletedQuest(quest.QuestId, DateTimeOffset.UtcNow, source));
        var next = _catalog.Find(quest.NextQuestId);
        state.CurrentQuestId = next?.QuestId;
        if (string.Equals(state.TrackedQuestId, quest.QuestId, StringComparison.Ordinal)) state.TrackedQuestId = next?.QuestId;

        QuestNotification Note(NotificationKind kind, MilestoneKind? milestone, QuestDefinition q, string title, string subtitle) =>
            new($"{source}|{state.PlayerId}|{kind}{(milestone is null ? "" : ":" + milestone)}|{q.QuestId}",
                state.PlayerId, kind, milestone, q.QuestId, q.ActId, title, subtitle);

        notes.Add(Note(NotificationKind.QuestComplete, null, quest, MainQuestText.BannerQuestComplete, quest.DisplayName));
        if (next is null)
        {
            notes.Add(Note(NotificationKind.CampaignMilestone, MilestoneKind.CampaignComplete, quest, MainQuestText.BannerMilestone, MainQuestText.CampaignComplete));
            return;
        }

        var act = _catalog.Act(quest.ActId);
        if (_catalog.QuestsInAct(act.ActId).All(q => state.IsCompleted(q.QuestId)))
            notes.Add(Note(NotificationKind.CampaignMilestone, MilestoneKind.ActComplete, quest, MainQuestText.BannerMilestone, string.Format(MainQuestText.ActCompleteFormat, act.DisplayName)));
        notes.Add(Note(NotificationKind.NewBossUnlocked, null, next, MainQuestText.BannerNewBoss, next.Target.DisplayName));
    }

    private void Remember(string eventId)
    {
        _eventOrder.Enqueue(eventId);
        while (_eventOrder.Count > _maxRememberedEvents) _processedEvents.Remove(_eventOrder.Dequeue());
    }

    /// <summary>
    /// Re-anchors a loaded state against the current catalog without discarding completed progress: if the current
    /// quest no longer exists, the first enabled quest that is not completed becomes current.
    /// </summary>
    public bool Reconcile(PlayerQuestState state)
    {
        if (state.CurrentQuestId is null && _catalog.Enabled.All(q => state.IsCompleted(q.QuestId))) return false;
        if (_catalog.Find(state.CurrentQuestId) is { } cur && !state.IsCompleted(cur.QuestId)) return false;
        state.CurrentQuestId = _catalog.Enabled.FirstOrDefault(q => !state.IsCompleted(q.QuestId))?.QuestId;
        state.TrackedQuestId = state.CurrentQuestId;
        return true;
    }

    // ------------------------------------------------------------ admin / QA (staging only)

    public string Inspect(AdminContext admin, PlayerQuestState state)
    {
        Require(admin);
        return $"{state.PlayerId}: current={state.CurrentQuestId ?? "none"} tracked={state.TrackedQuestId ?? "none"} " +
               $"completed=[{string.Join(",", state.Completed.Select(c => c.QuestId))}] progress={Progress(state):P0} schema={state.SchemaVersion}";
    }

    public void SetCurrentQuest(AdminContext admin, PlayerQuestState state, string questId)
    {
        Require(admin);
        var q = _catalog.Find(questId) ?? throw new ArgumentException($"unknown or disabled quest {questId}", nameof(questId));
        state.CurrentQuestId = q.QuestId;
        state.TrackedQuestId = q.QuestId;
    }

    public IReadOnlyList<QuestNotification> CompleteCurrentQuest(AdminContext admin, PlayerQuestState state)
    {
        Require(admin);
        var q = _catalog.Find(state.CurrentQuestId) ?? throw new InvalidOperationException("player has no current quest");
        var notes = new List<QuestNotification>();
        if (!state.IsCompleted(q.QuestId)) Complete(state, q, $"admin:{admin.CallerId}", notes);
        return notes;
    }

    public void ResetProgress(AdminContext admin, PlayerQuestState state)
    {
        Require(admin);
        state.Completed.Clear();
        state.CurrentQuestId = _catalog.First.QuestId;
        state.TrackedQuestId = _catalog.First.QuestId;
    }

    private static void Require(AdminContext admin)
    {
        if (admin is null || !admin.IsAdmin) throw new QuestAuthorizationException("main-quest admin tools are admin-only");
    }
}
