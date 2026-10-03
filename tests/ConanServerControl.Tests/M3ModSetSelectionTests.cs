using ConanServerControl.Core.LiveTesting;

namespace ConanServerControl.Tests;

/// <summary>Input rules for the live harness <c>set-mods</c> command (controlled staging mod sets).</summary>
public sealed class M3ModSetSelectionTests
{
    [Theory]
    [InlineData("none")]
    [InlineData(" NONE ")]
    public void None_selects_no_mods(string spec)
    {
        Assert.Empty(ModSetSelection.Parse(spec));
    }

    [Fact]
    public void Order_is_kept_and_whitespace_trimmed()
    {
        var mods = ModSetSelection.Parse(" StackMe10K.pak, SavageParagon.pak ,GritandGrease.pak,ImprovedThrallsAndQoL.pak");

        Assert.Equal(["StackMe10K.pak", "SavageParagon.pak", "GritandGrease.pak", "ImprovedThrallsAndQoL.pak"], mods);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("StackMe10K")]
    [InlineData("StackMe10K.zip")]
    [InlineData(@"Mods\StackMe10K.pak")]
    [InlineData("C:StackMe10K.pak")]
    [InlineData("StackMe10K.pak,stackme10k.PAK")]
    public void Invalid_or_duplicate_names_are_rejected(string spec)
    {
        Assert.Throws<ArgumentException>(() => ModSetSelection.Parse(spec));
    }

    [Fact]
    public void Names_missing_from_the_catalog_are_reported_case_insensitively()
    {
        var unknown = ModSetSelection.Unknown(
            ["stackme10k.pak", "Missing.pak", "Ancient_Realms.pak"],
            ["StackMe10K.pak", "Ancient_Realms.pak"]);

        Assert.Equal(["Missing.pak"], unknown);
    }
}
