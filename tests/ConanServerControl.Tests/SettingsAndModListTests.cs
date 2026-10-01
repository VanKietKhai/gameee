using System.Text.Json;
using ConanServerControl.Core.Mods;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Settings;

namespace ConanServerControl.Tests;

public class SettingsParsingTests
{
    [Fact]
    public void AppSettings_round_trips_json()
    {
        var settings = new AppSettings
        {
            IsSetupComplete = true,
            Server =
            {
                ServerName = "Khải's Age of War",
                GamePort = 7778,
                MaxPlayers = 8
            },
            Mods =
            {
                Mods =
                {
                    new WorkshopMod { WorkshopId = 111, Name = "Pippi", LoadOrder = 1, Enabled = true, LocalFileName = "Pippi.pak" }
                }
            }
        };

        var json = JsonSerializer.Serialize(settings);
        var loaded = JsonSerializer.Deserialize<AppSettings>(json);
        Assert.NotNull(loaded);
        Assert.Equal("Khải's Age of War", loaded!.Server.ServerName);
        Assert.Equal(7778, loaded.Server.GamePort);
        Assert.Single(loaded.Mods.Mods);
        Assert.Equal(111, loaded.Mods.Mods[0].WorkshopId);
    }

    [Fact]
    public void Default_update_interval_is_30_minutes()
    {
        Assert.Equal(UpdateCheckInterval.Minutes30, new AppSettings().Updates.CheckInterval);
    }
}

public class ModListGeneratorTests
{
    [Fact]
    public void Generate_uses_ui_order_for_enabled_mods_only()
    {
        var mods = new[]
        {
            new WorkshopMod { Name = "ModC", LocalFileName = "ModC.pak", LoadOrder = 3, Enabled = true },
            new WorkshopMod { Name = "ModA", LocalFileName = "ModA.pak", LoadOrder = 1, Enabled = true },
            new WorkshopMod { Name = "ModB", LocalFileName = "ModB.pak", LoadOrder = 2, Enabled = false }
        };

        var text = ModListGenerator.Generate(mods);
        var lines = ModListGenerator.Parse(text);
        Assert.Equal(["ModA.pak", "ModC.pak"], lines);
    }

    [Fact]
    public void Reorder_moves_item_and_rewrites_load_order()
    {
        var mods = new List<WorkshopMod>
        {
            new() { Name = "A", LocalFileName = "A.pak", LoadOrder = 1 },
            new() { Name = "B", LocalFileName = "B.pak", LoadOrder = 2 },
            new() { Name = "C", LocalFileName = "C.pak", LoadOrder = 3 }
        };

        var reordered = ModListGenerator.Reorder(mods, fromIndex: 2, toIndex: 0);
        Assert.Equal("C", reordered[0].Name);
        Assert.Equal(1, reordered[0].LoadOrder);
        Assert.Equal("A", reordered[1].Name);
        Assert.Equal(2, reordered[1].LoadOrder);
    }
}
