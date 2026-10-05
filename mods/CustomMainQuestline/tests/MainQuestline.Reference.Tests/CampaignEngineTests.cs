namespace MainQuestline.Reference.Tests;

/// <summary>
/// Rules of the Custom Main Questline (mod #14), exercised on a TEST catalog whose targets are fictitious
/// (<c>/Test/...</c>). These are not game identities; the real targets stay Unverified until proven.
/// </summary>
public sealed class CampaignEngineTests
{
    private const string BossA = "/Test/BossA.BossA_C";
    private const string BossB = "/Test/BossB.BossB_C";
    private const string BossC = "/Test/BossC.BossC_C";

    private static QuestDefinition Quest(string id, int seq, string? prev, string? next, string? target, int minLevel = 10, int weight = 1,
        TargetVerification v = TargetVerification.Verified) => new()
    {
        QuestId = id, ActId = "ACT_TEST", Sequence = seq, DisplayName = id, Description = "test",
        Target = new QuestTarget(QuestTargetType.BossKill, "Boss " + id, target, v),
        PreviousQuestId = prev, NextQuestId = next, MinimumLevel = minLevel,
        RecommendedLevelMin = minLevel, RecommendedLevelMax = minLevel + 5, RecommendedPartyMin = 1, RecommendedPartyMax = 3,
        LocationName = "Test Area", Marker = new MapMarker("Test Area", 100, 200, null, 3000),
        Reward = new RewardDefinition("RW_" + id, [new RewardEntry(RewardType.Xp, "xp", 10)], Placeholder: true),
        CompletionCreditRadius = 5000, CampaignWeight = weight
    };

    private static QuestCatalog Catalog() => new([
        Quest("Q01", 10, null, "Q02", BossA, minLevel: 15),
        Quest("Q02", 20, "Q01", "Q03", BossB, minLevel: 25),
        Quest("Q03", 30, "Q02", null, BossC, minLevel: 35)
    ]);

    private static (CampaignEngine Engine, Dictionary<string, PlayerQuestState> Store) Setup(CreditRule rule = CreditRule.ProximityOnly, params string[] players)
    {
        var engine = new CampaignEngine(Catalog(), rule);
        return (engine, players.ToDictionary(p => p, engine.NewPlayer, StringComparer.Ordinal));
    }

    private static BossDeathEvent Death(string id, string cls, params PlayerSnapshot[] players) => new(id, cls, 0, 0, 0, players);

    private static PlayerSnapshot At(string id, int level, double x = 100, bool participated = false) => new(id, level, x, 0, 0, participated);

    [Fact]
    public void Correct_boss_completes_the_current_quest_and_unlocks_the_next()
    {
        var (engine, store) = Setup(players: "p1");

        var r = engine.OnBossDeath(Death("e1", BossA, At("p1", 20)), id => store.GetValueOrDefault(id));

        Assert.Equal("Q01", r.QuestId);
        Assert.True(Assert.Single(r.Decisions).Credited);
        Assert.True(store["p1"].IsCompleted("Q01"));
        Assert.Equal("Q02", store["p1"].CurrentQuestId);
        Assert.Equal("Q02", store["p1"].TrackedQuestId);
        Assert.Same(store["p1"], Assert.Single(r.ChangedStates));
        Assert.Contains(r.Notifications, n => n.Kind == NotificationKind.QuestComplete && n.QuestId == "Q01");
        Assert.Contains(r.Notifications, n => n.Kind == NotificationKind.NewBossUnlocked && n.QuestId == "Q02");
    }

    [Fact]
    public void Wrong_boss_gives_nothing()
    {
        var (engine, store) = Setup(players: "p1");

        var r = engine.OnBossDeath(Death("e1", "/Test/SomeOtherNpc.SomeOtherNpc_C", At("p1", 60)), id => store.GetValueOrDefault(id));

        Assert.Null(r.QuestId);
        Assert.Empty(r.ChangedStates);
        Assert.Equal("Q01", store["p1"].CurrentQuestId);
    }

    [Fact]
    public void Class_path_matching_is_exact_not_substring()
    {
        var (engine, store) = Setup(players: "p1");

        Assert.Null(engine.OnBossDeath(Death("e1", BossA + "_Variant", At("p1", 60)), id => store.GetValueOrDefault(id)).QuestId);
        Assert.Null(engine.OnBossDeath(Death("e2", BossA.ToLowerInvariant(), At("p1", 60)), id => store.GetValueOrDefault(id)).QuestId);
        Assert.Empty(store["p1"].Completed);
    }

    [Fact]
    public void A_future_boss_cannot_skip_progression()
    {
        var (engine, store) = Setup(players: "p1");

        var r = engine.OnBossDeath(Death("e1", BossC, At("p1", 60)), id => store.GetValueOrDefault(id));

        Assert.Equal("Q03", r.QuestId);
        Assert.False(Assert.Single(r.Decisions).Credited);
        Assert.Empty(store["p1"].Completed);
        Assert.Equal("Q01", store["p1"].CurrentQuestId);
    }

    [Fact]
    public void Previous_quest_gate_blocks_a_current_quest_whose_predecessor_is_missing()
    {
        var (engine, store) = Setup(players: "p1");
        store["p1"].CurrentQuestId = "Q02"; // inconsistent state: Q01 never completed

        var r = engine.OnBossDeath(Death("e1", BossB, At("p1", 60)), id => store.GetValueOrDefault(id));

        Assert.Contains("previous quest Q01", Assert.Single(r.Decisions).Reason, StringComparison.Ordinal);
        Assert.False(store["p1"].IsCompleted("Q02"));
        Assert.False(engine.IsCurrentQuestAvailable(store["p1"], 60));
    }

    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)]
    public void Level_gate(int level, bool credited)
    {
        var (engine, store) = Setup(players: "p1");

        var r = engine.OnBossDeath(Death("e1", BossA, At("p1", level)), id => store.GetValueOrDefault(id));

        Assert.Equal(credited, Assert.Single(r.Decisions).Credited);
        Assert.Equal(credited, engine.IsCurrentQuestAvailable(new PlayerQuestState { PlayerId = "x", CurrentQuestId = "Q01" }, level));
    }

    [Fact]
    public void Multiple_eligible_nearby_players_are_all_credited_without_a_killing_blow()
    {
        var (engine, store) = Setup(players: ["p1", "p2", "p3"]);

        var r = engine.OnBossDeath(Death("e1", BossA, At("p1", 20), At("p2", 30, x: 4000), At("p3", 16, x: -4999)), id => store.GetValueOrDefault(id));

        Assert.All(r.Decisions, d => Assert.True(d.Credited, d.Reason));
        Assert.Equal(3, r.ChangedStates.Count);
        Assert.All(store.Values, s => Assert.Equal("Q02", s.CurrentQuestId));
    }

    [Fact]
    public void Ineligible_and_out_of_radius_players_are_not_credited_but_eligible_ones_are()
    {
        var (engine, store) = Setup(players: ["near", "far", "low", "ahead"]);
        store["ahead"].CurrentQuestId = "Q02";
        store["ahead"].Completed.Add(new CompletedQuest("Q01", DateTimeOffset.UtcNow, "test"));

        var r = engine.OnBossDeath(Death("e1", BossA, At("near", 20), At("far", 20, x: 5001), At("low", 10), At("ahead", 40), At("unknown", 40)),
            id => store.GetValueOrDefault(id));

        var byId = r.Decisions.ToDictionary(d => d.PlayerId);
        Assert.True(byId["near"].Credited);
        Assert.Equal("outside credit radius", byId["far"].Reason);
        Assert.StartsWith("level 10", byId["low"].Reason, StringComparison.Ordinal);
        Assert.StartsWith("current quest is Q02", byId["ahead"].Reason, StringComparison.Ordinal);
        Assert.Equal("no quest state", byId["unknown"].Reason);
        Assert.Equal(["near"], r.ChangedStates.Select(s => s.PlayerId));
    }

    [Fact]
    public void Participation_rule_requires_the_participation_flag()
    {
        var (engine, store) = Setup(CreditRule.ProximityAndParticipation, "fighter", "bystander");

        var r = engine.OnBossDeath(Death("e1", BossA, At("fighter", 20, participated: true), At("bystander", 20)), id => store.GetValueOrDefault(id));

        Assert.True(r.Decisions.Single(d => d.PlayerId == "fighter").Credited);
        Assert.Equal("did not participate", r.Decisions.Single(d => d.PlayerId == "bystander").Reason);
    }

    [Fact]
    public void Duplicate_death_event_does_not_double_complete()
    {
        var (engine, store) = Setup(players: "p1");
        var death = Death("e1", BossA, At("p1", 20));

        engine.OnBossDeath(death, id => store.GetValueOrDefault(id));
        var again = engine.OnBossDeath(death, id => store.GetValueOrDefault(id));
        // a second, distinct death of the same boss also cannot complete Q01 again (the player moved on to Q02)
        var otherDeath = engine.OnBossDeath(Death("e2", BossA, At("p1", 20)), id => store.GetValueOrDefault(id));

        Assert.Empty(again.ChangedStates);
        Assert.Empty(otherDeath.ChangedStates);
        Assert.Single(store["p1"].Completed);
    }

    [Fact]
    public void A_player_listed_twice_in_one_event_is_credited_once()
    {
        var (engine, store) = Setup(players: "p1");

        var r = engine.OnBossDeath(Death("e1", BossA, At("p1", 20), At("p1", 20)), id => store.GetValueOrDefault(id));

        Assert.Single(r.Decisions);
        Assert.Single(store["p1"].Completed);
    }

    [Fact]
    public void Unverified_target_never_grants_credit()
    {
        var engine = new CampaignEngine(new QuestCatalog([Quest("Q01", 10, null, null, BossA, v: TargetVerification.Unverified)]));
        var state = engine.NewPlayer("p1");

        var r = engine.OnBossDeath(Death("e1", BossA, At("p1", 60)), _ => state);

        Assert.Null(r.QuestId);
        Assert.Empty(state.Completed);
    }

    [Fact]
    public void Final_quest_completes_the_campaign()
    {
        var (engine, store) = Setup(players: "p1");
        var p = store["p1"];
        var admin = new AdminContext("qa", IsAdmin: true);
        engine.CompleteCurrentQuest(admin, p);
        engine.CompleteCurrentQuest(admin, p);

        var r = engine.OnBossDeath(Death("e1", BossC, At("p1", 40)), id => store.GetValueOrDefault(id));

        Assert.Null(p.CurrentQuestId);
        Assert.Contains(r.Notifications, n => n.Kind == NotificationKind.CampaignComplete);
        Assert.Equal(1.0, engine.Progress(p));
    }

    [Fact]
    public void Campaign_percentage_is_weight_based_and_ignores_level()
    {
        var engine = new CampaignEngine(new QuestCatalog([
            Quest("Q01", 10, null, "Q02", BossA, weight: 1),
            Quest("Q02", 20, "Q01", "Q03", BossB, weight: 1),
            Quest("Q03", 30, "Q02", null, BossC, weight: 2)
        ]));
        var p = engine.NewPlayer("p1");
        Assert.Equal(0.0, engine.Progress(p));

        p.Completed.Add(new CompletedQuest("Q01", DateTimeOffset.UtcNow, "t"));
        p.Completed.Add(new CompletedQuest("Q02", DateTimeOffset.UtcNow, "t"));
        p.Completed.Add(new CompletedQuest("Q99_REMOVED", DateTimeOffset.UtcNow, "t")); // no longer in the catalog: kept, not counted

        Assert.Equal(0.5, engine.Progress(p));
    }

    [Fact]
    public void Ten_equal_quests_half_done_is_fifty_percent()
    {
        var defs = Enumerable.Range(1, 10).Select(i => Quest($"Q{i:00}", i * 10, i == 1 ? null : $"Q{i - 1:00}", i == 10 ? null : $"Q{i + 1:00}", $"/Test/B{i}.B{i}_C")).ToList();
        var engine = new CampaignEngine(new QuestCatalog(defs));
        var p = engine.NewPlayer("p1");
        foreach (var i in Enumerable.Range(1, 5)) p.Completed.Add(new CompletedQuest($"Q{i:00}", DateTimeOffset.UtcNow, "t"));

        Assert.Equal(0.5, engine.Progress(p));
    }

    [Fact]
    public void Marker_shows_only_for_the_available_tracked_current_quest()
    {
        var (engine, store) = Setup(players: "p1");
        var p = store["p1"];

        Assert.Null(engine.ActiveMarker(p, 10));            // below Q01 minimum level
        Assert.NotNull(engine.ActiveMarker(p, 15));
        p.TrackedQuestId = null;
        Assert.Null(engine.ActiveMarker(p, 20));            // not tracked
        p.TrackedQuestId = "Q01";
        engine.OnBossDeath(Death("e1", BossA, At("p1", 20)), _ => p);
        Assert.Null(engine.ActiveMarker(p, 20));            // Q02 needs level 25; the Q01 marker is gone
        Assert.NotNull(engine.ActiveMarker(p, 25));         // Q02 marker restored from state alone (reconnect/restart)
    }

    [Fact]
    public void Admin_tools_reject_normal_players()
    {
        var (engine, store) = Setup(players: "p1");
        var player = new AdminContext("p1", IsAdmin: false);

        Assert.Throws<QuestAuthorizationException>(() => engine.Inspect(player, store["p1"]));
        Assert.Throws<QuestAuthorizationException>(() => engine.SetCurrentQuest(player, store["p1"], "Q03"));
        Assert.Throws<QuestAuthorizationException>(() => engine.CompleteCurrentQuest(player, store["p1"]));
        Assert.Throws<QuestAuthorizationException>(() => engine.ResetProgress(player, store["p1"]));
        Assert.Equal("Q01", store["p1"].CurrentQuestId);
    }

    [Fact]
    public void Admin_tools_work_for_admins()
    {
        var (engine, store) = Setup(players: "p1");
        var admin = new AdminContext("qa", IsAdmin: true);
        var p = store["p1"];

        var notes = engine.CompleteCurrentQuest(admin, p);
        Assert.Contains(notes, n => n.Kind == NotificationKind.NewBossUnlocked && n.QuestId == "Q02");
        Assert.Contains("completed=[Q01]", engine.Inspect(admin, p), StringComparison.Ordinal);
        engine.SetCurrentQuest(admin, p, "Q03");
        Assert.Equal("Q03", p.CurrentQuestId);
        Assert.Throws<ArgumentException>(() => engine.SetCurrentQuest(admin, p, "Q99"));
        engine.ResetProgress(admin, p);
        Assert.Empty(p.Completed);
        Assert.Equal("Q01", p.CurrentQuestId);
    }

    [Fact]
    public void Reconcile_keeps_completed_progress_when_the_current_quest_was_removed()
    {
        var engine = new CampaignEngine(Catalog());
        var p = new PlayerQuestState { PlayerId = "p1", CurrentQuestId = "Q_OLD_REMOVED" };
        p.Completed.Add(new CompletedQuest("Q01", DateTimeOffset.UtcNow, "t"));

        Assert.True(engine.Reconcile(p));
        Assert.Equal("Q02", p.CurrentQuestId);
        Assert.Single(p.Completed);
        Assert.False(engine.Reconcile(p));
    }
}
