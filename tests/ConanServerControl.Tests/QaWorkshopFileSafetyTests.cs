using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;
using ConanServerControl.Infrastructure.Steam;
using ConanServerControl.Infrastructure.Updates;
using ConanServerControl.Infrastructure.Workshop;
using ConanServerControl.Infrastructure.Concurrency;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

public class QaWorkshopFileSafetyTests
{
    [Theory]
    [InlineData("123 && calc")]
    [InlineData("123;rm")]
    [InlineData("..\\111")]
    [InlineData("../111")]
    [InlineData("111\n222")]
    [InlineData("111|cmd")]
    [InlineData("111\" --")]
    public void Workshop_ids_reject_path_and_command_fragments(string value)
    {
        Assert.False(WorkshopIdValidator.TryParse(value, out _));
    }

    [Fact]
    public async Task Failed_workshop_download_does_not_change_the_installed_pak_or_modlist()
    {
        var fx = await Fixture.CreateAsync();
        fx.Steam.WorkshopException = new UserFacingException("network down", "connection reset");

        var error = await Record.ExceptionAsync(() => fx.Mods.UpdateAsync(111));

        Assert.IsType<UserFacingException>(error);
        await fx.AssertLiveUntouchedAsync();
        Assert.Equal(Path.Combine(fx.Paths.StagingDirectory, "workshop", "111"), fx.Steam.WorkshopDirectories.Single());
    }

    [Fact]
    public async Task Non_zero_steamcmd_result_does_not_change_the_installed_pak()
    {
        var fx = await Fixture.CreateAsync();
        fx.Steam.WorkshopExitCode = 8;
        Directory.CreateDirectory(fx.Staging);
        await File.WriteAllTextAsync(Path.Combine(fx.Staging, "partial.pak"), "PARTIAL");

        var error = await Record.ExceptionAsync(() => fx.Mods.UpdateAsync(111));

        Assert.IsType<UserFacingException>(error);
        await fx.AssertLiveUntouchedAsync();
    }

    [Fact]
    public async Task Success_with_no_pak_in_an_empty_staging_directory_does_not_change_the_installed_pak()
    {
        var fx = await Fixture.CreateAsync();
        Directory.CreateDirectory(fx.Staging);

        var error = await Record.ExceptionAsync(() => fx.Mods.UpdateAsync(111));

        Assert.IsType<UserFacingException>(error);
        Assert.Contains("pak", error!.Message, StringComparison.OrdinalIgnoreCase);
        await fx.AssertLiveUntouchedAsync();
    }

    [Fact]
    public async Task Missing_staging_directory_does_not_change_the_installed_pak()
    {
        // Staging is created empty before SteamCMD. A previously missing folder is
        // now an empty validated result, not DirectoryNotFoundException.
        var fx = await Fixture.CreateAsync();

        var error = await Record.ExceptionAsync(() => fx.Mods.UpdateAsync(111));

        Assert.IsType<UserFacingException>(error);
        Assert.Contains("pak", error!.Message, StringComparison.OrdinalIgnoreCase);
        await fx.AssertLiveUntouchedAsync();
    }

    [Fact]
    public async Task Unknown_workshop_id_does_not_touch_an_installed_pak()
    {
        var fx = await Fixture.CreateAsync();

        var error = await Record.ExceptionAsync(() => fx.Mods.UpdateAsync(999));

        Assert.IsType<UserFacingException>(error);
        Assert.Empty(fx.Steam.WorkshopIds);
        await fx.AssertLiveUntouchedAsync();
    }

    [Fact]
    public async Task Adding_a_bad_id_that_fails_download_does_not_touch_an_existing_mod_file()
    {
        var fx = await Fixture.CreateAsync();
        fx.Steam.WorkshopException = new InvalidOperationException("item not found");

        await fx.Mods.AddAsync(222);

        await fx.AssertLiveUntouchedAsync();
        Assert.Equal(2, fx.Mods.Mods.Count);
        Assert.Contains("not found", fx.Mods.Mods.Single(m => m.WorkshopId == 222).Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Real_steamcmd_service_on_this_host_fails_before_launch_and_leaves_the_pak()
    {
        var fx = await Fixture.CreateAsync();
        var steamDir = Path.Combine(fx.Data, "steamcmd");
        Directory.CreateDirectory(steamDir);
        await File.WriteAllTextAsync(Path.Combine(steamDir, "steamcmd.exe"), "not-real");
        await fx.Settings.UpdateAsync(s => s.SteamCmd.InstallDirectory = steamDir);
        var runner = new ExplodingProcessRunner();
        var real = new SteamCmdService(
            fx.Settings,
            fx.Paths,
            runner,
            new StubHttpClientFactory(new StubHttpHandler()),
            NullLogger<SteamCmdService>.Instance);
        var mods = fx.WithSteam(real);

        var error = await Record.ExceptionAsync(() => mods.UpdateAsync(111));

        Assert.IsType<UserFacingException>(error);
        Assert.Equal(0, runner.Calls);
        await fx.AssertLiveUntouchedAsync();
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-002")]
    public async Task Exit_zero_without_a_new_pak_must_not_replace_the_installed_mod_with_staged_leftovers()
    {
        var fx = await Fixture.CreateAsync();
        Directory.CreateDirectory(fx.Staging);
        await File.WriteAllTextAsync(Path.Combine(fx.Staging, "Working.pak"), "STALE-PARTIAL");

        var error = await Record.ExceptionAsync(() => fx.Mods.UpdateAsync(111));
        var live = await File.ReadAllTextAsync(fx.LivePak);
        var bak = fx.LivePak + ".bak";
        var bakText = File.Exists(bak) ? await File.ReadAllTextAsync(bak) : "<no bak>";

        Assert.True(
            error is not null && live == Fixture.GoodBytes,
            $"A download that does not produce a new pak must leave the working mod bytes in place. " +
            $"error={error?.GetType().Name}: {error?.Message}; live={live}; bak={bakText}");
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-002")]
    public async Task Zero_byte_pak_must_not_replace_a_working_mod()
    {
        var fx = await Fixture.CreateAsync();
        fx.Steam.OnWorkshop = (_, dir, _) =>
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "Working.pak"), Array.Empty<byte>());
            return Task.CompletedTask;
        };

        var error = await Record.ExceptionAsync(() => fx.Mods.UpdateAsync(111));
        var liveLength = new FileInfo(fx.LivePak).Length;

        Assert.True(
            error is not null && liveLength == Fixture.GoodBytes.Length,
            $"An empty download must not become the live mod. error={error?.GetType().Name}; liveLength={liveLength}");
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-002")]
    public async Task Multiple_downloaded_paks_must_not_leave_a_single_arbitrary_file_as_the_mod()
    {
        var fx = await Fixture.CreateAsync();
        fx.Steam.OnWorkshop = (_, dir, _) =>
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Alpha.pak"), "ALPHA");
            File.WriteAllText(Path.Combine(dir, "Beta.pak"), "BETA");
            return Task.CompletedTask;
        };

        var error = await Record.ExceptionAsync(() => fx.Mods.UpdateAsync(111));
        var modlist = await File.ReadAllTextAsync(fx.ModListPath);
        var modsDir = Path.GetDirectoryName(fx.LivePak)!;
        var installed = Directory.Exists(modsDir)
            ? Directory.EnumerateFiles(modsDir, "*.pak").Select(Path.GetFileName).OrderBy(n => n).ToArray()
            : Array.Empty<string>();

        Assert.True(
            error is not null || (modlist.Contains("Alpha.pak", StringComparison.Ordinal) && modlist.Contains("Beta.pak", StringComparison.Ordinal)),
            $"Ambiguous multi-pak download must fail closed or install every pak. error={error?.GetType().Name}: {error?.Message}; modlist={modlist}; installed=[{string.Join(", ", installed)}]");
        await fx.AssertLiveUntouchedAsync();
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-002")]
    public async Task UpdateAll_failure_on_a_later_mod_must_leave_earlier_mods_unchanged()
    {
        var fx = await Fixture.CreateAsync();
        var second = Path.Combine(Path.GetDirectoryName(fx.LivePak)!, "Second.pak");
        await File.WriteAllTextAsync(second, "MOD2-OLD");
        await fx.Settings.UpdateAsync(s =>
        {
            s.Mods.Mods.Add(new WorkshopMod
            {
                WorkshopId = 222,
                Name = "Second",
                Enabled = true,
                LoadOrder = 2,
                LocalFileName = "Second.pak"
            });
        });
        fx.Steam.OnWorkshop = (id, dir, _) =>
        {
            if (id == 222)
            {
                throw new InvalidOperationException("network failed on second mod");
            }

            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Working.pak"), "MOD1-NEW");
            return Task.CompletedTask;
        };

        var error = await Record.ExceptionAsync(() => fx.Mods.UpdateAllAsync());
        var first = await File.ReadAllTextAsync(fx.LivePak);
        var secondText = await File.ReadAllTextAsync(second);

        Assert.True(
            error is not null && first == Fixture.GoodBytes && secondText == "MOD2-OLD",
            $"A failed Update All must not keep a half-applied set. error={error?.GetType().Name}: {error?.Message}; first={first}; second={secondText}");
    }

    [Fact]
    [Trait("Issue", "QA-002")]
    public async Task Valid_pak_replaces_the_installed_mod()
    {
        var fx = await Fixture.CreateAsync();
        fx.Steam.OnWorkshop = (_, dir, _) =>
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Working.pak"), "MOD1-NEW");
            return Task.CompletedTask;
        };

        await fx.Mods.UpdateAsync(111);

        Assert.Equal("MOD1-NEW", await File.ReadAllTextAsync(fx.LivePak));
        Assert.Contains("Working.pak", await File.ReadAllTextAsync(fx.ModListPath), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-005")]
    public async Task Update_selected_must_hold_the_action_gate_while_it_downloads()
    {
        var fx = await Fixture.CreateAsync();
        var gate = new ServerActionGate();
        var held = false;
        fx.Steam.OnWorkshop = (_, dir, _) =>
        {
            held = !gate.TryBegin("probe", out var lease);
            lease?.Dispose();
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Working.pak"), Fixture.GoodBytes);
            return Task.CompletedTask;
        };

        await fx.Mods.UpdateAsync(111);

        Assert.True(held, "Update Selected replaced files without holding IServerActionGate.");
    }

    [Fact]
    public async Task Check_with_a_valid_payload_flags_an_update_without_touching_files_or_downloading()
    {
        var fx = await Fixture.CreateAsync();
        var handler = new StubHttpHandler
        {
            Body = """
                {"response":{"publishedfiledetails":[{"publishedfileid":"111","result":1,"title":"Pippi","filename":"Pippi.pak","time_updated":200}]}}
                """
        };
        var mods = fx.WithClient(new SteamWorkshopClient(
            new StubHttpClientFactory(handler),
            NullLogger<SteamWorkshopClient>.Instance));

        await mods.CheckForUpdatesAsync();

        var mod = Assert.Single(mods.Mods);
        Assert.Equal("Pippi", mod.Name);
        Assert.True(mod.UpdateAvailable);
        Assert.Equal("Working.pak", mod.LocalFileName);
        Assert.Empty(fx.Steam.WorkshopIds);
        await fx.AssertLiveUntouchedAsync();
    }

    [Fact]
    public async Task Check_http_failure_does_not_change_the_mod_or_the_pak()
    {
        var fx = await Fixture.CreateAsync();
        var mods = fx.WithClient(new SteamWorkshopClient(
            new StubHttpClientFactory(new StubHttpHandler { Status = System.Net.HttpStatusCode.InternalServerError }),
            NullLogger<SteamWorkshopClient>.Instance));

        var error = await Record.ExceptionAsync(() => mods.CheckForUpdatesAsync());

        Assert.IsType<UserFacingException>(error);
        Assert.Equal("Working Mod", Assert.Single(mods.Mods).Name);
        await fx.AssertLiveUntouchedAsync();
    }

    [Fact]
    [Trait("Category", "QA-KnownFailure")]
    [Trait("Issue", "QA-009")]
    public async Task Check_must_not_overwrite_a_working_mod_when_steam_returns_no_file_details()
    {
        var fx = await Fixture.CreateAsync();
        var mods = fx.WithClient(new SteamWorkshopClient(
            new StubHttpClientFactory(new StubHttpHandler
            {
                Body = """{"response":{"resultcount":1,"publishedfiledetails":[{"publishedfileid":"111","result":9}]}}"""
            }),
            NullLogger<SteamWorkshopClient>.Instance));

        await mods.CheckForUpdatesAsync();
        var mod = Assert.Single(mods.Mods);
        await fx.AssertLiveUntouchedAsync();
        Assert.True(
            mod.Name == "Working Mod" && !string.IsNullOrWhiteSpace(mod.Error),
            $"Missing Workshop details must keep the installed name and record an error. name={mod.Name}; error={mod.Error}; updateAvailable={mod.UpdateAvailable}");
    }

    [Fact]
    public async Task Server_check_does_not_stop_or_start_or_download()
    {
        var fx = await Fixture.CreateAsync();
        var gate = new ServerActionGate();
        var server = new RecordingServer(gate) { };
        server.State.Status = ServerStatus.Online;
        var updates = new ServerUpdateService(
            fx.Settings,
            fx.Steam,
            server,
            new CountingBackup(),
            fx.Mods,
            gate,
            new RecordingActivityLog(),
            NullLogger<ServerUpdateService>.Instance);

        await updates.CheckAsync();

        Assert.Equal(ServerStatus.Online, server.State.Status);
        Assert.Empty(server.Calls);
        Assert.Empty(fx.Steam.Events);
        await fx.AssertLiveUntouchedAsync();
    }

    private sealed class Fixture
    {
        public const string GoodBytes = "WORKING-MOD";

        public required string Data { get; init; }

        public required ConanServerControl.Infrastructure.Paths.AppPaths Paths { get; init; }

        public required ConanServerControl.Infrastructure.Settings.JsonSettingsService Settings { get; init; }

        public required ScriptedSteamCmd Steam { get; init; }

        public required WorkshopModService Mods { get; init; }

        public required string LivePak { get; init; }

        public required string ModListPath { get; init; }

        public required string Staging { get; init; }

        public required string ModListBefore { get; init; }

        public static async Task<Fixture> CreateAsync()
        {
            var (data, paths, settings) = QaTestSupport.CreateData();
            var install = QaTestSupport.InstallRoot(data);
            await QaTestSupport.ConfigureInstallAsync(settings, install);
            var modsDir = Path.Combine(install, "ConanSandbox", "Mods");
            Directory.CreateDirectory(modsDir);
            var live = Path.Combine(modsDir, "Working.pak");
            await File.WriteAllTextAsync(live, GoodBytes);
            var modlist = Path.Combine(modsDir, "modlist.txt");
            await File.WriteAllTextAsync(modlist, "Working.pak");
            await settings.UpdateAsync(s =>
            {
                s.Mods.Mods.Add(new WorkshopMod
                {
                    WorkshopId = 111,
                    Name = "Working Mod",
                    Enabled = true,
                    LoadOrder = 1,
                    LocalFileName = "Working.pak",
                    InstalledTimestamp = DateTimeOffset.UnixEpoch.AddSeconds(10)
                });
            });

            var steam = new ScriptedSteamCmd();
            var service = new WorkshopModService(
                settings,
                steam,
                new CountingBackup(),
                paths,
                new EmptyWorkshopClient(),
                new RecordingActivityLog(),
                NullLogger<WorkshopModService>.Instance);

            return new Fixture
            {
                Data = data,
                Paths = paths,
                Settings = settings,
                Steam = steam,
                Mods = service,
                LivePak = live,
                ModListPath = modlist,
                Staging = Path.Combine(paths.StagingDirectory, "workshop", "111"),
                ModListBefore = "Working.pak"
            };
        }

        public WorkshopModService WithSteam(ISteamCmdService steam) =>
            new(Settings, steam, new CountingBackup(), Paths, new EmptyWorkshopClient(), new RecordingActivityLog(), NullLogger<WorkshopModService>.Instance);

        public WorkshopModService WithClient(ISteamWorkshopClient client) =>
            new(Settings, Steam, new CountingBackup(), Paths, client, new RecordingActivityLog(), NullLogger<WorkshopModService>.Instance);

        public async Task AssertLiveUntouchedAsync()
        {
            Assert.Equal(GoodBytes, await File.ReadAllTextAsync(LivePak));
            Assert.Equal(ModListBefore, (await File.ReadAllTextAsync(ModListPath)).Trim());
        }
    }
}
