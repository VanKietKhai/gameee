using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Mods;
using ConanServerControl.Infrastructure.Concurrency;
using ConanServerControl.Infrastructure.Workshop;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class QaModManagementTests
{
    [Fact]
    public async Task Enable_disable_and_move_rewrite_modlist_in_load_order_without_disabled_mods()
    {
        var fx = await CreateAsync();
        await fx.Settings.UpdateAsync(s =>
        {
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 1, Name = "A", Enabled = true, LoadOrder = 1, LocalFileName = "A.pak" });
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 2, Name = "B", Enabled = true, LoadOrder = 2, LocalFileName = "B.pak" });
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 3, Name = "C", Enabled = true, LoadOrder = 3, LocalFileName = "C.pak" });
        });

        await fx.Mods.SetEnabledAsync(2, false);
        await fx.Mods.MoveAsync(3, 0);

        var lines = ModListGenerator.Parse(await File.ReadAllTextAsync(fx.ModList));
        Assert.Equal(new[] { "C.pak", "A.pak" }, lines);
    }

    [Fact]
    public async Task Duplicate_workshop_id_is_rejected_and_does_not_download()
    {
        var fx = await CreateAsync();
        await fx.Settings.UpdateAsync(s =>
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 5, Name = "A", Enabled = true, LoadOrder = 1, LocalFileName = "A.pak" }));

        var error = await Record.ExceptionAsync(() => fx.Mods.AddAsync(5));

        Assert.IsType<UserFacingException>(error);
        Assert.Empty(fx.Steam.WorkshopIds);
        Assert.Single(fx.Mods.Mods);
    }

    [Fact]
    public async Task Moving_the_first_mod_to_the_same_index_keeps_order()
    {
        var fx = await CreateAsync();
        await fx.Settings.UpdateAsync(s =>
        {
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 1, Name = "A", Enabled = true, LoadOrder = 1, LocalFileName = "A.pak" });
            s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 2, Name = "B", Enabled = true, LoadOrder = 2, LocalFileName = "B.pak" });
        });

        await fx.Mods.MoveAsync(1, 0);

        var lines = ModListGenerator.Parse(await File.ReadAllTextAsync(fx.ModList));
        Assert.Equal(new[] { "A.pak", "B.pak" }, lines);
    }

    [Fact]
    public void Modlist_line_strips_directory_traversal_from_the_stored_file_name()
    {
        var text = ModListGenerator.Generate(
        [
            new WorkshopMod
            {
                Enabled = true,
                LoadOrder = 1,
                LocalFileName = "../../Windows/system32/evil.pak"
            }
        ]);

        Assert.Equal("evil.pak", text.Trim());
        Assert.DoesNotContain("..", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Modlist_uses_the_stored_file_name_even_when_it_does_not_match_a_name_pak()
    {
        var text = ModListGenerator.Generate(
        [
            new WorkshopMod
            {
                Enabled = true,
                LoadOrder = 1,
                Name = "Pretty Name",
                LocalFileName = "ActualFile.pak"
            }
        ]);

        Assert.Equal("ActualFile.pak", text.Trim());
    }

    private static async Task<Fx> CreateAsync()
    {
        var (data, paths, settings) = QaTestSupport.CreateData();
        var install = QaTestSupport.InstallRoot(data);
        await QaTestSupport.ConfigureInstallAsync(settings, install);
        var modsDir = Path.Combine(install, "ConanSandbox", "Mods");
        Directory.CreateDirectory(modsDir);
        var steam = new ScriptedSteamCmd();
        return new Fx
        {
            Settings = settings,
            Steam = steam,
            Mods = new WorkshopModService(
                settings,
                steam,
                new CountingBackup(),
                new ServerActionGate(),
                paths,
                new EmptyWorkshopClient(),
                new RecordingActivityLog(),
                NullLogger<WorkshopModService>.Instance),
            ModList = Path.Combine(modsDir, "modlist.txt")
        };
    }

    private sealed class Fx
    {
        public required ConanServerControl.Infrastructure.Settings.JsonSettingsService Settings { get; init; }

        public required ScriptedSteamCmd Steam { get; init; }

        public required WorkshopModService Mods { get; init; }

        public required string ModList { get; init; }
    }
}
