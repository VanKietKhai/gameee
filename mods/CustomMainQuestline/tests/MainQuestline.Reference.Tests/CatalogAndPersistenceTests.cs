namespace MainQuestline.Reference.Tests;

public sealed class CatalogAndPersistenceTests
{
    private static QuestDefinition Q(string id, int seq, string? prev, string? next) => new()
    {
        QuestId = id, ActId = "A", Sequence = seq, DisplayName = id, Description = "d",
        Target = new QuestTarget(QuestTargetType.BossKill, "B", "/Test/" + id + "." + id + "_C", TargetVerification.Verified),
        PreviousQuestId = prev, NextQuestId = next, MinimumLevel = 1, RecommendedLevelMin = 1, RecommendedLevelMax = 2,
        RecommendedPartyMin = 1, RecommendedPartyMax = 1, LocationName = "L",
        Reward = new RewardDefinition("R", [new RewardEntry(RewardType.Xp, "xp", 1)], true), CompletionCreditRadius = 100, CampaignWeight = 1
    };

    public static TheoryData<string, QuestDefinition[]> Malformed => new()
    {
        { "empty", [] },
        { "duplicate id", [Q("Q1", 1, null, null), Q("Q1", 2, null, null)] },
        { "dangling next", [Q("Q1", 1, null, "Q9")] },
        { "next does not point back", [Q("Q1", 1, null, "Q2"), Q("Q2", 2, null, null)] },
        { "two roots", [Q("Q1", 1, null, null), Q("Q2", 2, null, null)] },
        { "cycle", [Q("Q1", 1, "Q2", "Q2"), Q("Q2", 2, "Q1", "Q1")] },
        { "zero weight", [Q("Q1", 1, null, null) with { CampaignWeight = 0 }] },
        { "radius zero", [Q("Q1", 1, null, null) with { CompletionCreditRadius = 0 }] },
        { "radius huge", [Q("Q1", 1, null, null) with { CompletionCreditRadius = 1e9 }] },
        { "bad level range", [Q("Q1", 1, null, null) with { RecommendedLevelMin = 30, RecommendedLevelMax = 20 }] },
        { "bad party", [Q("Q1", 1, null, null) with { RecommendedPartyMin = 0 }] },
        { "min level 0", [Q("Q1", 1, null, null) with { MinimumLevel = 0 }] },
        { "verified without class", [Q("Q1", 1, null, null) with { Target = new QuestTarget(QuestTargetType.BossKill, "B", null, TargetVerification.Verified) }] },
        { "bad reward", [Q("Q1", 1, null, null) with { Reward = new RewardDefinition("R", [new RewardEntry(RewardType.Xp, "xp", 0)], true) }] },
        { "next lower sequence", [Q("Q1", 5, null, "Q2"), Q("Q2", 1, "Q1", null)] },
        { "whitespace id", [Q("Q 1", 1, null, null)] },
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void Malformed_catalogs_fail_closed(string label, QuestDefinition[] defs)
    {
        var ex = Assert.Throws<QuestCatalogException>(() => new QuestCatalog(defs));
        Assert.NotEmpty(ex.Errors);
        Assert.False(string.IsNullOrEmpty(label));
    }

    [Fact]
    public void A_valid_chain_is_accepted()
    {
        var catalog = new QuestCatalog([Q("Q1", 1, null, "Q2"), Q("Q2", 2, "Q1", null)]);
        Assert.Equal("Q1", catalog.First.QuestId);
        Assert.Equal(2, catalog.TotalWeight);
    }

    [Fact]
    public void Save_round_trips_with_schema_version_1()
    {
        var s = new PlayerQuestState { PlayerId = "76561190000000000", CurrentQuestId = "Q2", TrackedQuestId = "Q2" };
        s.Completed.Add(new CompletedQuest("Q1", new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero), "boss:e1"));

        var json = QuestSaveCodec.Serialize(s);
        var back = QuestSaveCodec.Deserialize(json);

        Assert.Contains("\"schemaVersion\":1", json, StringComparison.Ordinal);
        Assert.Equal(1, PlayerQuestState.CurrentSchemaVersion);
        Assert.Equal(s.PlayerId, back.PlayerId);
        Assert.Equal("Q2", back.CurrentQuestId);
        Assert.Equal("Q2", back.TrackedQuestId);
        Assert.Equal(s.Completed, back.Completed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"playerId\":\"p\"}")]
    [InlineData("{\"schemaVersion\":0,\"playerId\":\"p\"}")]
    [InlineData("{\"schemaVersion\":\"1\",\"playerId\":\"p\"}")]
    [InlineData("{\"schemaVersion\":1}")]
    public void Unreadable_or_incomplete_saves_are_rejected_not_reset(string payload)
    {
        Assert.Throws<QuestSaveException>(() => QuestSaveCodec.Deserialize(payload));
    }

    [Fact]
    public void A_newer_schema_is_refused_so_progress_is_never_wiped()
    {
        var ex = Assert.Throws<QuestSaveException>(() => QuestSaveCodec.Deserialize("{\"schemaVersion\":2,\"playerId\":\"p\",\"completed\":[]}"));
        Assert.Contains("newer", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Writing_a_foreign_schema_version_is_refused()
    {
        Assert.Throws<QuestSaveException>(() => QuestSaveCodec.Serialize(new PlayerQuestState { PlayerId = "p", SchemaVersion = 2 }));
    }

    [Fact]
    public void Completed_quests_unknown_to_the_catalog_survive_a_round_trip()
    {
        var s = new PlayerQuestState { PlayerId = "p" };
        s.Completed.Add(new CompletedQuest("MQ_FUTURE_REMOVED", DateTimeOffset.UnixEpoch, "t"));

        Assert.Equal("MQ_FUTURE_REMOVED", Assert.Single(QuestSaveCodec.Deserialize(QuestSaveCodec.Serialize(s)).Completed).QuestId);
    }
}

public sealed class ProvisionalDataTests
{
    private static string DataJson() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "main-quests.provisional.v1.json"));

    [Fact]
    public void Provisional_campaign_loads_and_is_a_valid_chain()
    {
        var (file, catalog) = QuestDataLoader.Load(DataJson());

        Assert.Equal(11, catalog.Enabled.Count);
        Assert.Equal("MQ01", catalog.First.QuestId);
        Assert.Equal(["ACT_I_SURVIVAL", "ACT_II_ANCIENT_BLOOD", "ACT_III_FROZEN_NORTH", "ACT_IV_DARK_POWERS", "ACT_V_ENDGAME", "EPILOGUE"],
            catalog.Enabled.Select(q => q.ActId).Distinct());
        Assert.StartsWith("PROVISIONAL", file.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void No_provisional_target_is_verified_or_carries_a_guessed_class()
    {
        var (_, catalog) = QuestDataLoader.Load(DataJson());

        Assert.All(catalog.Enabled, q =>
        {
            Assert.Equal(TargetVerification.Unverified, q.Target.Verification);
            Assert.Null(q.Target.TargetClassPath);
            Assert.False(q.Target.CanGrantCredit);
            Assert.True(q.Reward.Placeholder);
        });
    }

    [Fact]
    public void Quest_01_records_the_ranked_candidates_but_cannot_complete_yet()
    {
        var (_, catalog) = QuestDataLoader.Load(DataJson());
        var q1 = catalog.First;
        var engine = new CampaignEngine(catalog);
        var p = engine.NewPlayer("p1");

        Assert.Equal("Abysmal Remnant", q1.Target.DisplayName);
        Assert.Equal(15, q1.MinimumLevel);
        Assert.Equal(2, q1.Target.Candidates!.Count);
        var r = engine.OnBossDeath(new BossDeathEvent("e1", "/Game/Characters/NPCs/BP_NPC_Wildlife_SewerAbomination.BP_NPC_Wildlife_SewerAbomination_C",
            0, 0, 0, [new PlayerSnapshot("p1", 30, 0, 0, 0)]), _ => p);
        Assert.Null(r.QuestId);
        Assert.Empty(p.Completed);
    }

    [Theory]
    [InlineData("{\"dataSchemaVersion\":2,\"status\":\"x\",\"quests\":[]}")]
    [InlineData("{\"dataSchemaVersion\":1,\"status\":\"x\",\"quests\":[],\"surprise\":1}")]
    [InlineData("{ broken")]
    public void Malformed_data_files_fail_closed(string json)
    {
        Assert.Throws<QuestCatalogException>(() => QuestDataLoader.Load(json));
    }
}
