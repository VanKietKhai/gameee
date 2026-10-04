using ConanServerControl.Core.LiveTesting;

namespace ConanServerControl.Tests;

/// <summary>
/// Signature-bound LoadErrors baselines (Phase 2, 2026-10-05): a validated entry may also be bound to its exact
/// two-line form and engine frame. Everything that differs fails closed; baselines without signatures behave as before.
/// </summary>
public sealed class LoadErrorSignatureTests
{
    private const string Pak = "Simple_Minimap.pak";
    private const string Sha = "04F31A75559665C1A949D7A9A632F9A777CE7D79E926032CFC9FD101B1626AC9";
    private const string Key = "None -> C88E5FE76A79516D";

    private const string Message =
        "LoadErrors: While trying to load package None, a dependent package None (C88E5FE76A79516D) was not available. " +
        "Additional explanatory information follows:";

    private const string Detail =
        "FPackageName: Unable to identify a valid mount point associated with skipped package None. The package root is unknown.";

    private static readonly LoadErrorBaseline Baseline = new(
        Pak, Sha, LoadErrorBaselineStatus.KnownNonBlocking,
        new Dictionary<string, int>(StringComparer.Ordinal) { [Key] = 1 }, "test")
    {
        Signatures = new Dictionary<string, LoadErrorSignature>(StringComparer.Ordinal)
        {
            [Key] = new LoadErrorSignature(Message, Detail, 0)
        }
    };

    private static ValidatedCatalog Catalog(params LoadErrorBaseline[] baselines) =>
        ValidatedCatalog.Current with { LoadErrorBaselines = baselines };

    private static string Logged(int frame, string message) => $"[2026.10.04-16.53.20:838][{frame,3}]{message}";

    private static IReadOnlyList<LoadErrorEntry> Parse(params string[] lines) => ModBootGates.ParseLoadErrors(lines);

    private static BootGateResult Evaluate(IReadOnlyList<LoadErrorEntry> entries, string sha = Sha) =>
        ModBootGates.EvaluateLoadErrors(Pak, sha, entries, Catalog(Baseline));

    [Fact]
    public void Exact_hash_reference_count_message_and_frame_pass()
    {
        var gate = Evaluate(Parse("[x][  0]LogTemp: before", Logged(0, Message), Detail, Logged(0, "LogTemp: after")));

        Assert.True(gate.Pass, gate.Detail);
        Assert.StartsWith("KNOWN NON-BLOCKING: 1 LoadErrors = validated set (exact); 1 exact signature(s)", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_records_the_message_the_following_line_and_the_frame()
    {
        var entry = Assert.Single(Parse(Logged(7, Message), Detail + "\r"));

        Assert.Equal(Key, entry.ToString());
        Assert.Equal(Message, entry.Message);
        Assert.Equal(Detail, entry.DetailLine);
        Assert.Equal(7, entry.Frame);
    }

    [Fact]
    public void Wrong_hash_fails()
    {
        var gate = Evaluate(Parse(Logged(0, Message), Detail), new string('A', 64));

        Assert.False(gate.Pass);
        Assert.Contains("not the validated version", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Wrong_reference_fails()
    {
        var other = Message.Replace("C88E5FE76A79516D", "C88E5FE76A79516E", StringComparison.Ordinal);

        Assert.False(Evaluate(Parse(Logged(0, other), Detail)).Pass);
    }

    [Fact]
    public void Count_zero_fails()
    {
        var gate = Evaluate([]);

        Assert.False(gate.Pass);
        Assert.Contains($"missing [{Key} x1]", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Count_two_fails()
    {
        var gate = Evaluate(Parse(Logged(0, Message), Detail, Logged(0, Message), Detail));

        Assert.False(gate.Pass);
        Assert.Contains($"{Key} x1", gate.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("FPackageName: Unable to identify a valid mount point associated with skipped package None. The package root is known.")]
    [InlineData("FPackageName: Unable to identify a valid mount point associated with skipped package None. The package root is unknown. ")]
    [InlineData(" FPackageName: Unable to identify a valid mount point associated with skipped package None. The package root is unknown.")]
    [InlineData("LogTemp: Display: something else")]
    public void Explanatory_line_drift_fails(string detail)
    {
        var gate = Evaluate(Parse(Logged(0, Message), detail));

        Assert.False(gate.Pass);
        Assert.Contains("drifted", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_explanatory_line_fails()
    {
        Assert.False(Evaluate(Parse(Logged(0, Message))).Pass);
    }

    [Fact]
    public void Message_drift_fails_even_when_the_key_matches()
    {
        // The parser accepts only the exact template, so drift inside a parsed entry is injected directly.
        var entry = Assert.Single(Parse(Logged(0, Message), Detail)) with { Message = Message + " " };

        Assert.False(Evaluate([entry]).Pass);
        Assert.False(Evaluate([new LoadErrorEntry("None", "C88E5FE76A79516D")]).Pass); // no recorded form at all
    }

    [Theory]
    [InlineData(1)]
    [InlineData(46)]
    public void Frame_drift_fails(int frame)
    {
        var gate = Evaluate(Parse(Logged(frame, Message), Detail));

        Assert.False(gate.Pass);
        Assert.Contains($"(frame {frame})", gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Line_without_a_prefix_has_no_frame_and_fails()
    {
        Assert.False(Evaluate(Parse(Message, Detail)).Pass);
    }

    [Fact]
    public void An_extra_load_error_for_the_same_mod_fails()
    {
        var extra = Message.Replace("C88E5FE76A79516D", "0123456789ABCDEF", StringComparison.Ordinal);
        var gate = Evaluate(Parse(Logged(0, Message), Detail, Logged(0, extra), Detail));

        Assert.False(gate.Pass);
        Assert.Contains("None -> 0123456789ABCDEF x1", gate.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("LoadErrors: While trying to load package None, a dependent package None (C88E5FE76A79516D) was not available.")]
    [InlineData("LoadErrors: something new")]
    public void Malformed_or_unparsed_load_error_fails(string line)
    {
        var entries = Parse(Logged(0, Message), Detail, Logged(0, line), Detail);

        Assert.Contains(entries, e => e.RawUnparsedLine is not null);
        Assert.False(Evaluate(entries).Pass);
    }

    [Fact]
    public void Baselines_without_signatures_keep_the_key_and_count_behaviour()
    {
        var plain = Baseline with { Signatures = null };
        var gate = ModBootGates.EvaluateLoadErrors(Pak, Sha, [new LoadErrorEntry("None", "C88E5FE76A79516D")], Catalog(plain));

        Assert.True(gate.Pass, gate.Detail);
        Assert.All(ModBootGates.LoadErrorBaselines.Where(b => b.ModPakFileName != Pak), b => Assert.Null(b.Signatures));
    }

    [Fact]
    public void A_catalog_without_signatures_serializes_and_hashes_as_before()
    {
        var withoutSignatures = ValidatedCatalog.Current with
        {
            LoadErrorBaselines = ModBootGates.LoadErrorBaselines.Where(b => b.Signatures is null).ToList()
        };

        Assert.DoesNotContain("Signatures", System.Text.Json.JsonSerializer.Serialize(withoutSignatures), StringComparison.Ordinal);
        // The catalog SHA-256 recorded for every batch up to and including Phase 2 (before any signature-bound baseline).
        Assert.Equal("767B4BCD5C3D7B8C1F3473074894131FE468EC4656DFC1E4A31B4C1EE4417818",
            BatchAnalysisSnapshotStore.CatalogSha256(withoutSignatures));
    }

    [Fact]
    public void The_committed_simple_minimap_baseline_is_exactly_the_approved_rule()
    {
        var committed = Assert.Single(ModBootGates.LoadErrorBaselines, b => b.Signatures is not null);

        Assert.Equal(3, ModBootGates.LoadErrorBaselines.Count);
        Assert.Equal(Pak, committed.ModPakFileName);
        Assert.Equal(Sha, committed.ValidatedPakSha256);
        Assert.Equal(LoadErrorBaselineStatus.KnownNonBlocking, committed.Status);
        Assert.Equal([new KeyValuePair<string, int>(Key, 1)], committed.Expected);
        Assert.Equal([new KeyValuePair<string, LoadErrorSignature>(Key, new LoadErrorSignature(Message, Detail, 0))], committed.Signatures!);
        Assert.True(ModBootGates.EvaluateLoadErrors(Pak, Sha, Parse(Logged(0, Message), Detail)).Pass);
        Assert.False(ModBootGates.EvaluateLoadErrors(Pak, Sha, Parse(Logged(1, Message), Detail)).Pass);
    }

    [Fact]
    public void Signatures_round_trip_through_a_recorded_catalog()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Baseline);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<LoadErrorBaseline>(json)!;

        Assert.Equal(Baseline.Signatures![Key], loaded.Signatures![Key]);
        Assert.True(ModBootGates.EvaluateLoadErrors(Pak, Sha, Parse(Logged(0, Message), Detail), Catalog(loaded)).Pass);
    }
}
