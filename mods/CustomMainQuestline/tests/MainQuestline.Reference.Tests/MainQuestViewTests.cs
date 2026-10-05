namespace MainQuestline.Reference.Tests;

/// <summary>
/// Main Quest tab, Track Quest and notification contract, on a TEST catalog with fictitious targets.
/// Act A1 = Q01, Q02; act A2 = Q03 (weight 2).
/// </summary>
public sealed class MainQuestViewTests
{
    private const string BossA = "/Test/BossA.BossA_C";
    private const string BossB = "/Test/BossB.BossB_C";
    private const string BossC = "/Test/BossC.BossC_C";

    private static readonly ActDefinition[] Acts = [new("A1", 1, "Act I — Test"), new("A2", 2, "Act II — Test")];

    private static QuestDefinition Quest(string id, string act, int seq, string? prev, string? next, string target, int minLevel, int weight = 1,
        LockedDisclosure disclosure = LockedDisclosure.Partial) => new()
    {
        QuestId = id, ActId = act, Sequence = seq, DisplayName = "Title " + id, Description = "Desc " + id,
        Target = new QuestTarget(QuestTargetType.BossKill, "Boss " + id, target, TargetVerification.Verified),
        PreviousQuestId = prev, NextQuestId = next, MinimumLevel = minLevel,
        RecommendedLevelMin = minLevel, RecommendedLevelMax = minLevel + 5, RecommendedPartyMin = 1, RecommendedPartyMax = 3,
        LocationName = "Place " + id, Marker = new MapMarker("Place " + id, 100, 200, null, 3000),
        Reward = new RewardDefinition("RW_" + id, [new RewardEntry(RewardType.Xp, "xp", 10)], Placeholder: true),
        CompletionCreditRadius = 5000, CampaignWeight = weight, LockedDisclosure = disclosure
    };

    private static CampaignEngine Engine(LockedDisclosure q2 = LockedDisclosure.Partial, LockedDisclosure q3 = LockedDisclosure.Partial) =>
        new(new QuestCatalog([
            Quest("Q01", "A1", 10, null, "Q02", BossA, 15),
            Quest("Q02", "A1", 20, "Q01", "Q03", BossB, 25, disclosure: q2),
            Quest("Q03", "A2", 30, "Q02", null, BossC, 35, weight: 2, disclosure: q3)
        ], Acts));

    private static BossDeathEvent Death(string id, string cls, int level) => new(id, cls, 0, 0, 0, [new PlayerSnapshot("p1", level, 100, 0, 0)]);

    private static readonly AdminContext Admin = new("qa", IsAdmin: true);

    [Fact]
    public void New_player_sees_first_quest_active_and_the_rest_locked()
    {
        var engine = Engine();
        var v = engine.BuildView(engine.NewPlayer("p1"), 20);

        Assert.Equal([QuestStatus.Active, QuestStatus.Locked], v.Acts[0].Quests.Select(q => q.Status));
        Assert.Equal(QuestStatus.Locked, Assert.Single(v.Acts[1].Quests).Status);
        Assert.Equal([ActStatus.Active, ActStatus.Locked], v.Acts.Select(a => a.Status));
        Assert.Equal(["Act I — Test", "Act II — Test"], v.Acts.Select(a => a.DisplayName));
        Assert.Equal(0, v.CampaignProgressPercent);
        Assert.False(v.CampaignComplete);
        Assert.Empty(v.History);

        var cur = v.Current!;
        Assert.Equal(("Q01", "Title Q01", "Boss Q01", "Place Q01"), (cur.QuestId, cur.Title, cur.BossName, cur.Location));
        Assert.Equal(("15–20", "1–3", "Defeat Boss Q01."), (cur.RecommendedLevel, cur.RecommendedParty, cur.Objective));
        Assert.Equal(["10 XP"], cur.Reward!);
        Assert.True(cur.Available && cur.IsTracked && cur.CanTrack && cur.CreditEnabled);
        Assert.True(v.Tracking.MarkerVisible);
    }

    [Fact]
    public void Locked_partial_shows_title_and_recommendations_only()
    {
        var engine = Engine();
        var d = engine.Detail(engine.NewPlayer("p1"), 20, "Q02");

        Assert.Equal(QuestStatus.Locked, d.Status);
        Assert.Equal("Title Q02", d.Title);
        Assert.Equal(("25–30", "1–3"), (d.RecommendedLevel, d.RecommendedParty));
        Assert.Null(d.BossName);
        Assert.Null(d.Location);
        Assert.Null(d.Description);
        Assert.Null(d.Objective);
        Assert.Null(d.Reward);
        Assert.Equal(MainQuestViews.LockedHint, d.StatusNote);
        Assert.False(d.CanTrack || d.IsTracked || d.Available);
    }

    [Fact]
    public void Locked_hidden_reveals_nothing_but_the_act_and_the_hint()
    {
        var engine = Engine(q3: LockedDisclosure.Hidden);
        var p = engine.NewPlayer("p1");
        var d = engine.Detail(p, 20, "Q03");

        Assert.Equal((MainQuestViews.Unknown, "Act II — Test"), (d.Title, d.ActDisplayName));
        Assert.Null(d.BossName);
        Assert.Null(d.RecommendedLevel);
        Assert.Null(d.RecommendedParty);
        Assert.Equal(MainQuestViews.LockedHint, d.StatusNote);
        Assert.Equal(MainQuestViews.Unknown, Assert.Single(engine.BuildView(p, 20).Acts[1].Quests).Title);
    }

    [Fact]
    public void Locked_full_shows_everything_but_still_cannot_be_tracked()
    {
        var engine = Engine(q2: LockedDisclosure.Full);
        var d = engine.Detail(engine.NewPlayer("p1"), 20, "Q02");

        Assert.Equal(("Boss Q02", "Place Q02"), (d.BossName, d.Location));
        Assert.Equal(QuestStatus.Locked, d.Status);
        Assert.False(d.CanTrack);
    }

    [Fact]
    public void Completed_quests_are_shown_in_full_whatever_their_disclosure()
    {
        var engine = Engine(q2: LockedDisclosure.Hidden);
        var p = engine.NewPlayer("p1");
        engine.CompleteCurrentQuest(Admin, p);
        engine.CompleteCurrentQuest(Admin, p);

        var d = engine.Detail(p, 40, "Q02");
        Assert.Equal((QuestStatus.Completed, "Title Q02", "Boss Q02"), (d.Status, d.Title, d.BossName));
        Assert.False(d.CanTrack);
    }

    [Fact]
    public void Active_quest_below_its_level_is_shown_but_unavailable_and_has_no_marker()
    {
        var engine = Engine();
        var v = engine.BuildView(engine.NewPlayer("p1"), 10);

        Assert.Equal(QuestStatus.Active, v.Current!.Status);
        Assert.False(v.Current.Available);
        Assert.Equal("Reach level 15 to begin.", v.Current.StatusNote);
        Assert.False(v.Tracking.MarkerVisible);
        Assert.Equal("Requires level 15", v.Tracking.MarkerHiddenReason);
    }

    [Fact]
    public void Act_and_campaign_progress_are_weighted_and_floored()
    {
        var engine = Engine();
        var p = engine.NewPlayer("p1");
        engine.CompleteCurrentQuest(Admin, p);

        var v = engine.BuildView(p, 30);
        Assert.Equal((1, 2, 50), (v.Acts[0].CompletedQuests, v.Acts[0].TotalQuests, v.Acts[0].ProgressPercent));
        Assert.Equal(25, v.CampaignProgressPercent);   // 1 of weight 4

        engine.CompleteCurrentQuest(Admin, p);
        v = engine.BuildView(p, 40);
        Assert.Equal(ActStatus.Completed, v.Acts[0].Status);
        Assert.Equal(ActStatus.Active, v.Acts[1].Status);
        Assert.Equal(50, v.CampaignProgressPercent);

        var thirds = new CampaignEngine(new QuestCatalog([
            Quest("Q01", "A1", 10, null, "Q02", BossA, 1), Quest("Q02", "A1", 20, "Q01", "Q03", BossB, 1), Quest("Q03", "A1", 30, "Q02", null, BossC, 1)]));
        var t = thirds.NewPlayer("p1");
        thirds.CompleteCurrentQuest(Admin, t);
        thirds.CompleteCurrentQuest(Admin, t);
        Assert.Equal(66, thirds.BuildView(t, 1).CampaignProgressPercent); // 66.7% never rounds up to 67, and never to 100 early
    }

    [Fact]
    public void Finishing_an_act_emits_a_milestone_between_quest_complete_and_new_boss()
    {
        var engine = Engine();
        var p = engine.NewPlayer("p1");
        engine.OnBossDeath(Death("e1", BossA, 20), _ => p);

        var r = engine.OnBossDeath(Death("e2", BossB, 30), _ => p);

        Assert.Equal([NotificationKind.QuestComplete, NotificationKind.CampaignMilestone, NotificationKind.NewBossUnlocked], r.Notifications.Select(n => n.Kind));
        var m = r.Notifications[1];
        Assert.Equal((MilestoneKind.ActComplete, "A1", "CAMPAIGN MILESTONE", "Act I — Test complete"), (m.Milestone!.Value, m.ActId, m.Title, m.Subtitle));
        Assert.Equal(("NEW BOSS UNLOCKED", "Boss Q03", "Q03", "A2"), (r.Notifications[2].Title, r.Notifications[2].Subtitle, r.Notifications[2].QuestId, r.Notifications[2].ActId));
    }

    [Fact]
    public void A_quest_inside_an_act_emits_no_milestone()
    {
        var engine = Engine();
        var p = engine.NewPlayer("p1");

        var r = engine.OnBossDeath(Death("e1", BossA, 20), _ => p);

        Assert.Equal([NotificationKind.QuestComplete, NotificationKind.NewBossUnlocked], r.Notifications.Select(n => n.Kind));
        Assert.All(r.Notifications, n => Assert.Null(n.Milestone));
    }

    [Fact]
    public void Final_quest_emits_only_the_campaign_complete_milestone()
    {
        var engine = Engine();
        var p = engine.NewPlayer("p1");
        engine.CompleteCurrentQuest(Admin, p);
        engine.CompleteCurrentQuest(Admin, p);

        var r = engine.OnBossDeath(Death("e3", BossC, 40), _ => p);

        Assert.Equal([NotificationKind.QuestComplete, NotificationKind.CampaignMilestone], r.Notifications.Select(n => n.Kind));
        Assert.Equal(MilestoneKind.CampaignComplete, r.Notifications[1].Milestone);
        var v = engine.BuildView(p, 40);
        Assert.True(v.CampaignComplete);
        Assert.Equal(100, v.CampaignProgressPercent);
        Assert.Null(v.Current);
        Assert.All(v.Acts, a => Assert.Equal(ActStatus.Completed, a.Status));
        Assert.Equal("No active Main Quest", v.Tracking.MarkerHiddenReason);
    }

    [Fact]
    public void Notification_ids_are_unique_per_banner_and_stable_per_completion()
    {
        var engine = Engine();
        var p = engine.NewPlayer("p1");
        engine.OnBossDeath(Death("e1", BossA, 20), _ => p);

        var r = engine.OnBossDeath(Death("e2", BossB, 30), _ => p);

        Assert.Equal(r.Notifications.Count, r.Notifications.Select(n => n.NotificationId).Distinct().Count());
        Assert.All(r.Notifications, n => Assert.StartsWith("boss:e2|p1|", n.NotificationId, StringComparison.Ordinal));
        Assert.Empty(engine.OnBossDeath(Death("e2", BossB, 30), _ => p).Notifications); // duplicate event: no banners
    }

    [Fact]
    public void History_lists_completions_in_order_and_keeps_retired_quests()
    {
        var engine = Engine();
        var p = engine.NewPlayer("p1");
        p.Completed.Add(new CompletedQuest("Q01", DateTimeOffset.UnixEpoch.AddDays(2), "t"));
        p.Completed.Add(new CompletedQuest("Q_RETIRED", DateTimeOffset.UnixEpoch.AddDays(1), "t"));
        p.CurrentQuestId = p.TrackedQuestId = "Q02";

        var h = engine.BuildView(p, 30).History;

        Assert.Equal(["Q_RETIRED", "Q01"], h.Select(e => e.QuestId));
        Assert.Equal((true, "Q_RETIRED", (string?)null), (h[0].Retired, h[0].Title, h[0].BossName));
        Assert.Equal((false, "Title Q01", "Boss Q01", "Act I — Test"), (h[1].Retired, h[1].Title, h[1].BossName, h[1].ActDisplayName));
    }

    [Fact]
    public void Track_quest_toggle_is_validated_by_the_server()
    {
        var engine = Engine();
        var p = engine.NewPlayer("p1");

        var off = engine.SetTrackedQuest(p, null);
        Assert.True(off.Accepted && off.Changed);
        var v = engine.BuildView(p, 20);
        Assert.False(v.Current!.IsTracked);
        Assert.Equal("Quest not tracked", v.Tracking.MarkerHiddenReason);

        Assert.False(engine.SetTrackedQuest(p, "Q02").Accepted);   // locked future quest
        Assert.False(engine.SetTrackedQuest(p, "Q99").Accepted);   // unknown
        Assert.Null(p.TrackedQuestId);                              // rejected requests change nothing

        var on = engine.SetTrackedQuest(p, "Q01");
        Assert.True(on.Accepted && on.Changed);
        Assert.False(engine.SetTrackedQuest(p, "Q01").Changed);     // idempotent
        Assert.True(engine.BuildView(p, 20).Tracking.MarkerVisible);
    }

    [Fact]
    public void An_untracked_player_stays_untracked_after_completing_a_quest()
    {
        var engine = Engine();
        var p = engine.NewPlayer("p1");
        engine.SetTrackedQuest(p, null);

        engine.OnBossDeath(Death("e1", BossA, 20), _ => p);

        Assert.Equal("Q02", p.CurrentQuestId);
        Assert.Null(p.TrackedQuestId);
        Assert.False(engine.BuildView(p, 30).Tracking.MarkerVisible);
    }

    [Fact]
    public void Building_the_view_never_changes_the_state()
    {
        var engine = Engine();
        var p = engine.NewPlayer("p1");
        engine.CompleteCurrentQuest(Admin, p);
        var before = QuestSaveCodec.Serialize(p);

        engine.BuildView(p, 1);
        engine.BuildView(p, 99);
        engine.Detail(p, 30, "Q03");

        Assert.Equal(before, QuestSaveCodec.Serialize(p));
    }

    [Fact]
    public void Detail_of_an_unknown_quest_is_rejected()
    {
        var engine = Engine();
        Assert.Throws<ArgumentException>(() => engine.Detail(engine.NewPlayer("p1"), 20, "Q99"));
    }

    public static TheoryData<string, ActDefinition[]> MalformedActs => new()
    {
        { "unknown act", [new("A1", 1, "One")] },
        { "duplicate act id", [new("A1", 1, "One"), new("A1", 2, "Again"), new("A2", 3, "Two")] },
        { "duplicate order", [new("A1", 1, "One"), new("A2", 1, "Two")] },
        { "empty act", [new("A1", 1, "One"), new("A2", 2, "Two"), new("A3", 3, "Three")] },
        { "act order backwards", [new("A1", 2, "One"), new("A2", 1, "Two")] },
        { "blank name", [new("A1", 1, " "), new("A2", 2, "Two")] },
    };

    [Theory]
    [MemberData(nameof(MalformedActs))]
    public void Malformed_acts_fail_closed(string label, ActDefinition[] acts)
    {
        var quests = new[]
        {
            Quest("Q01", "A1", 10, null, "Q02", BossA, 1), Quest("Q02", "A1", 20, "Q01", "Q03", BossB, 1), Quest("Q03", "A2", 30, "Q02", null, BossC, 1)
        };
        Assert.Throws<QuestCatalogException>(() => new QuestCatalog(quests, acts));
        Assert.False(string.IsNullOrEmpty(label));
    }

    [Fact]
    public void Interleaved_acts_fail_closed()
    {
        var quests = new[]
        {
            Quest("Q01", "A1", 10, null, "Q02", BossA, 1), Quest("Q02", "A2", 20, "Q01", "Q03", BossB, 1), Quest("Q03", "A1", 30, "Q02", null, BossC, 1)
        };
        Assert.Throws<QuestCatalogException>(() => new QuestCatalog(quests, Acts));
    }
}

public sealed class ProvisionalMainQuestTabTests
{
    private static (QuestDataFile File, QuestCatalog Catalog) Load() =>
        QuestDataLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "main-quests.provisional.v2.json")));

    [Fact]
    public void Provisional_acts_match_the_campaign_layout()
    {
        var (_, catalog) = Load();

        Assert.Equal(["Act I — Survival", "Act II — Ancient Blood", "Act III — Frozen North", "Act IV — Dark Powers", "Act V — Endgame", "Epilogue"],
            catalog.Acts.Select(a => a.DisplayName));
        Assert.All(catalog.Enabled, q => Assert.False(string.IsNullOrWhiteSpace(q.Objective)));
    }

    [Fact]
    public void Black_keep_example_renders_from_saved_state()
    {
        var (_, catalog) = Load();
        var engine = new CampaignEngine(catalog);
        var p = engine.NewPlayer("p1");
        foreach (var id in new[] { "MQ01", "MQ02", "MQ03", "MQ04" }) p.Completed.Add(new CompletedQuest(id, DateTimeOffset.UnixEpoch, "t"));
        p.CurrentQuestId = p.TrackedQuestId = "MQ05";

        var v = engine.BuildView(p, 48);

        Assert.Equal(("Act III — Frozen North", "The Black Keep", "Kinscourge", "The Black Keep"),
            (v.Current!.ActDisplayName, v.Current.Title, v.Current.BossName, v.Current.Location));
        Assert.Equal(("Defeat the Kinscourge.", "40–50", "2–5", true), (v.Current.Objective, v.Current.RecommendedLevel, v.Current.RecommendedParty, v.Current.Available));
        Assert.Equal(["1000 XP"], v.Current.Reward!);
        Assert.Equal(36, v.CampaignProgressPercent);                        // 4 of 11 equal weights
        var locked = engine.Detail(p, 48, "MQ06");
        Assert.Equal((QuestStatus.Locked, "Midnight Grove", "50–55", "3–5", (string?)null), (locked.Status, locked.Title, locked.RecommendedLevel, locked.RecommendedParty, locked.BossName));
        Assert.Equal([100, 100, 50, 0, 0, 0], v.Acts.Select(a => a.ProgressPercent));
        Assert.False(v.Current.CreditEnabled);                              // provisional targets are unverified
        Assert.Equal(["MQ01", "MQ02", "MQ03", "MQ04"], v.History.Select(h => h.QuestId));
        Assert.False(v.Tracking.MarkerVisible);                             // provisional quests carry no marker coordinates yet
        Assert.Equal("This quest has no map marker", v.Tracking.MarkerHiddenReason);
    }

    [Fact]
    public void Equal_range_ends_render_as_one_number()
    {
        var (_, catalog) = Load();
        var engine = new CampaignEngine(catalog);
        var d = engine.Detail(engine.NewPlayer("p1"), 60, "MQ09");

        Assert.Equal(("60", "3–5"), (d.RecommendedLevel, d.RecommendedParty));
    }
}
