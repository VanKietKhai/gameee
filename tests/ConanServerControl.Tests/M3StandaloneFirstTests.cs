using System.Text.Json;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Backups;
using ConanServerControl.Core.Diagnostics;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Mods;
using ConanServerControl.Infrastructure.Concurrency;
using ConanServerControl.Infrastructure.Mods;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.Settings;
using ConanServerControl.Infrastructure.Updates;
using ConanServerControl.Infrastructure.Workshop;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

/// <summary>
/// Standalone-first product direction: Local .pak mods (no SteamCMD), Client Mod Bundle export,
/// read-only client sync planning, existing-server support and optional SteamCMD readiness.
/// </summary>
public sealed class M3StandaloneFirstTests
{
    // ------------------------------------------------------------ Local import: validation

    [Fact]
    public async Task Local_import_copies_verifies_and_commits_without_touching_source_or_steamcmd()
    {
        var fx = await LocalFixture.CreateAsync();
        var source = fx.WriteSource("CoolMod.pak", "COOL-MOD-V1");
        var before = SourceState(source);

        await fx.Updates.ImportLocalModAsync(source);

        Assert.Equal(before, SourceState(source));
        var live = Path.Combine(fx.ModsDir, "CoolMod.pak");
        Assert.Equal("COOL-MOD-V1", await File.ReadAllTextAsync(live));
        var mod = Assert.Single(fx.Mods.Mods);
        Assert.Equal(ModSourceType.Local, mod.SourceType);
        Assert.Equal(0, mod.WorkshopId);
        Assert.Equal("CoolMod.pak", mod.LocalFileName);
        Assert.Equal(source, mod.LocalSourcePath);
        Assert.Equal(BackupFileHasher.Sha256File(source), mod.Sha256);
        Assert.False(mod.UpdateAvailable);
        Assert.Equal("CoolMod.pak", (await File.ReadAllTextAsync(fx.ModListPath)).Trim());
        Assert.Empty(fx.Steam.Events);
        Assert.False(Directory.Exists(Path.Combine(fx.Paths.StagingDirectory, "local")) &&
                     Directory.EnumerateFileSystemEntries(Path.Combine(fx.Paths.StagingDirectory, "local")).Any());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-extension")]
    [InlineData("zero-byte")]
    [InlineData("folder")]
    [InlineData("relative")]
    public async Task Local_import_rejects_invalid_files_before_touching_the_server(string kind)
    {
        var fx = await LocalFixture.CreateAsync();
        var path = kind switch
        {
            "missing" => Path.Combine(fx.SourceDir, "Nope.pak"),
            "wrong-extension" => fx.WriteSource("Mod.zip", "zip"),
            "zero-byte" => fx.WriteSource("Empty.pak", string.Empty),
            "folder" => Directory.CreateDirectory(Path.Combine(fx.SourceDir, "Folder.pak")).FullName,
            _ => "relative\\Mod.pak"
        };

        var error = await Record.ExceptionAsync(() => fx.Updates.ImportLocalModAsync(path));

        Assert.IsType<UserFacingException>(error);
        Assert.Empty(fx.Mods.Mods);
        Assert.Empty(fx.Server.Calls);
        Assert.Equal(0, fx.Backup.Count);
        Assert.False(File.Exists(fx.ModListPath));
    }

    [Fact]
    public async Task Local_import_refuses_a_source_inside_the_server_mods_folder()
    {
        var fx = await LocalFixture.CreateAsync();
        var inMods = Path.Combine(fx.ModsDir, "Placed.pak");
        await File.WriteAllTextAsync(inMods, "placed");

        var error = await Record.ExceptionAsync(() => fx.Updates.ImportLocalModAsync(inMods));

        Assert.IsType<UserFacingException>(error);
        Assert.Equal("placed", await File.ReadAllTextAsync(inMods));
        Assert.Empty(fx.Mods.Mods);
    }

    [Fact]
    public async Task Local_import_refuses_a_name_already_used_by_another_mod()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Dup.pak", "first"));
        var otherDir = Directory.CreateDirectory(Path.Combine(fx.SourceDir, "other")).FullName;
        var second = Path.Combine(otherDir, "Dup.pak");
        await File.WriteAllTextAsync(second, "second");

        var error = await Record.ExceptionAsync(() => fx.Updates.ImportLocalModAsync(second));

        Assert.IsType<UserFacingException>(error);
        Assert.Equal("first", await File.ReadAllTextAsync(Path.Combine(fx.ModsDir, "Dup.pak")));
        Assert.Single(fx.Mods.Mods);
    }

    [Fact]
    public async Task Local_import_does_not_overwrite_a_different_unmanaged_file()
    {
        var fx = await LocalFixture.CreateAsync();
        var unmanaged = Path.Combine(fx.ModsDir, "Hand.pak");
        await File.WriteAllTextAsync(unmanaged, "hand-placed");

        var error = await Record.ExceptionAsync(() => fx.Updates.ImportLocalModAsync(fx.WriteSource("Hand.pak", "different")));

        Assert.IsType<UserFacingException>(error);
        Assert.Equal("hand-placed", await File.ReadAllTextAsync(unmanaged));
        Assert.Empty(fx.Mods.Mods);
    }

    // ------------------------------------------------------------ Local import: pipeline

    [Fact]
    public async Task Local_import_offline_runs_backup_then_commit_and_stays_offline()
    {
        var fx = await LocalFixture.CreateAsync();
        fx.Backup.OnBackup = (_, _) =>
        {
            fx.Timeline.Add(File.Exists(Path.Combine(fx.ModsDir, "Order.pak")) ? "backup-after-commit" : "backup-before-commit");
            return Task.CompletedTask;
        };

        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Order.pak", "x"));

        Assert.Equal(["backup", "backup-before-commit"], fx.Timeline);
        Assert.Equal(["pre-local-mod-import"], fx.Backup.Reasons);
        Assert.Equal(ServerStatus.Offline, fx.Server.State.Status);
        Assert.Empty(fx.Server.Calls);
    }

    [Fact]
    public async Task Local_import_while_online_stops_backs_up_commits_and_restarts()
    {
        var fx = await LocalFixture.CreateAsync();
        fx.Server.State.Status = ServerStatus.Online;

        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Live.pak", "x"));

        Assert.Equal(["stop", "backup", "start"], fx.Timeline);
        Assert.Equal(ServerStatus.Online, fx.Server.State.Status);
        Assert.True(File.Exists(Path.Combine(fx.ModsDir, "Live.pak")));
    }

    [Fact]
    public async Task Local_import_backup_failure_blocks_the_commit()
    {
        var fx = await LocalFixture.CreateAsync();
        fx.Backup.FailWith = new UserFacingException("Backup failed", "boom");

        var error = await Record.ExceptionAsync(() => fx.Updates.ImportLocalModAsync(fx.WriteSource("Blocked.pak", "x")));

        Assert.IsType<UserFacingException>(error);
        Assert.False(File.Exists(Path.Combine(fx.ModsDir, "Blocked.pak")));
        Assert.Empty(fx.Mods.Mods);
    }

    // ------------------------------------------------------------ Local manual update

    [Fact]
    public async Task Local_replace_installs_newer_same_name_file_and_updates_hash()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Upd.pak", "v1"));
        var newerDir = Directory.CreateDirectory(Path.Combine(fx.SourceDir, "newer")).FullName;
        var newer = Path.Combine(newerDir, "Upd.pak");
        await File.WriteAllTextAsync(newer, "v2-longer");

        await fx.Updates.ReplaceLocalModAsync(ModKeys.Local("Upd.pak"), newer);

        Assert.Equal("v2-longer", await File.ReadAllTextAsync(Path.Combine(fx.ModsDir, "Upd.pak")));
        var mod = Assert.Single(fx.Mods.Mods);
        Assert.Equal(BackupFileHasher.Sha256File(newer), mod.Sha256);
        Assert.Equal(newer, mod.LocalSourcePath);
        Assert.True(File.Exists(newer));
    }

    [Fact]
    public async Task Local_replace_with_a_different_file_name_is_rejected()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Keep.pak", "v1"));

        var error = await Record.ExceptionAsync(() =>
            fx.Updates.ReplaceLocalModAsync(ModKeys.Local("Keep.pak"), fx.WriteSource("Keep_v2.pak", "v2")));

        Assert.IsType<UserFacingException>(error);
        Assert.Equal("v1", await File.ReadAllTextAsync(Path.Combine(fx.ModsDir, "Keep.pak")));
        Assert.False(File.Exists(Path.Combine(fx.ModsDir, "Keep_v2.pak")));
    }

    [Fact]
    public async Task Local_replace_failure_after_live_write_rolls_back_file_and_catalog()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Roll.pak", "v1"));
        var original = Assert.Single(fx.Mods.Mods).Sha256;
        var newerDir = Directory.CreateDirectory(Path.Combine(fx.SourceDir, "newer")).FullName;
        var newer = Path.Combine(newerDir, "Roll.pak");
        await File.WriteAllTextAsync(newer, "v2");
        fx.Mods.AfterLiveReplacementForTests = _ => throw new IOException("simulated failure after live write");

        var error = await Record.ExceptionAsync(() => fx.Updates.ReplaceLocalModAsync(ModKeys.Local("Roll.pak"), newer));

        Assert.IsType<UserFacingException>(error);
        Assert.Equal("v1", await File.ReadAllTextAsync(Path.Combine(fx.ModsDir, "Roll.pak")));
        Assert.Equal(original, Assert.Single(fx.Mods.Mods).Sha256);
    }

    [Fact]
    public async Task Workshop_mods_cannot_be_replaced_from_a_local_file()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.AddWorkshopModAsync(555, "Ws.pak");

        var error = await Record.ExceptionAsync(() =>
            fx.Updates.ReplaceLocalModAsync(ModKeys.Workshop(555), fx.WriteSource("Ws.pak", "local")));

        Assert.IsType<UserFacingException>(error);
        Assert.Equal("workshop-bytes", await File.ReadAllTextAsync(Path.Combine(fx.ModsDir, "Ws.pak")));
    }

    // ------------------------------------------------------------ Local mods vs Workshop paths

    [Fact]
    public async Task Update_all_and_update_checks_never_touch_local_mods()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Solo.pak", "local"));
        var client = new RecordingWorkshopClient();
        var mods = fx.CreateModService(client);

        await mods.ApplyUpdatesAsync(workshopIds: null);
        await mods.CheckForUpdatesAsync();

        Assert.Empty(fx.Steam.Events);
        Assert.Empty(client.Requested);
        var mod = Assert.Single(fx.Mods.Mods);
        Assert.Null(mod.LastChecked);
        Assert.False(mod.UpdateAvailable);
        Assert.Equal("local", await File.ReadAllTextAsync(Path.Combine(fx.ModsDir, "Solo.pak")));
    }

    [Fact]
    public async Task Workshop_download_that_collides_with_a_local_pak_is_rejected()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Same.pak", "local-original"));
        fx.Steam.OnWorkshop = (_, dir, _) => File.WriteAllTextAsync(Path.Combine(dir, "Same.pak"), "workshop-version");
        await fx.Settings.UpdateAsync(s => s.Mods.Mods.Add(new WorkshopMod { WorkshopId = 777, Name = "Ws", Enabled = true, LoadOrder = 2 }));

        var error = await Record.ExceptionAsync(() => fx.Mods.ApplyUpdatesAsync([777]));

        Assert.IsType<UserFacingException>(error);
        Assert.Equal("local-original", await File.ReadAllTextAsync(Path.Combine(fx.ModsDir, "Same.pak")));
    }

    [Fact]
    public async Task Key_based_catalog_operations_work_for_local_mods_and_long_zero_is_rejected()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("A.pak", "a"));
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("B.pak", "b"));

        await fx.Mods.MoveAsync(ModKeys.Local("B.pak"), 0);
        Assert.Equal("B.pak" + Environment.NewLine + "A.pak", await File.ReadAllTextAsync(fx.ModListPath));

        await fx.Mods.SetEnabledAsync(ModKeys.Local("b.pak"), false);
        Assert.Equal("A.pak", await File.ReadAllTextAsync(fx.ModListPath));

        await fx.Mods.RemoveAsync(ModKeys.Local("A.pak"), confirmed: true);
        Assert.Single(fx.Mods.Mods);

        await Assert.ThrowsAsync<UserFacingException>(() => fx.Mods.SetEnabledAsync(0L, true));
        await Assert.ThrowsAsync<UserFacingException>(() => fx.Mods.RemoveAsync(0L, confirmed: true));
        Assert.Single(fx.Mods.Mods);
    }

    [Fact]
    public async Task Shareable_list_describes_local_mods_without_a_steam_link()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Share.pak", "s"));

        var text = fx.Mods.GetShareableModList();

        Assert.Contains("local file Share.pak", text, StringComparison.Ordinal);
        Assert.DoesNotContain("steamcommunity.com/sharedfiles/filedetails/?id=0", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("123456", "workshop:123456")]
    [InlineData("MyMod.pak", "local:MyMod.pak")]
    [InlineData("local:Other.pak", "local:Other.pak")]
    public void Mod_keys_parse_administrator_input(string input, string expected)
    {
        Assert.True(ModKeys.TryParseUserInput(input, out var key));
        Assert.Equal(expected, key);
    }

    [Fact]
    public async Task Old_settings_without_source_type_load_as_workshop_mods()
    {
        var (data, paths, _) = QaTestSupport.CreateData();
        await File.WriteAllTextAsync(paths.SettingsFilePath, """
            {
              "settingsVersion": 1,
              "mods": { "mods": [ { "workshopId": 880454836, "name": "Legacy", "localFileName": "Legacy.pak", "enabled": true, "loadOrder": 1 } ] }
            }
            """);
        var settings = new JsonSettingsService(paths, new PassthroughProtector(), NullLogger<JsonSettingsService>.Instance);

        await settings.LoadAsync();

        var mod = Assert.Single(settings.Current.Mods.Mods);
        Assert.Equal(ModSourceType.Workshop, mod.SourceType);
        Assert.Equal(880454836, mod.WorkshopId);
        Assert.Equal("workshop:880454836", ModKeys.For(mod));
        _ = data;
    }

    // ------------------------------------------------------------ Client Mod Bundle

    [Fact]
    public async Task Bundle_export_contains_enabled_mods_in_load_order_with_manifest_hashes()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.AddWorkshopModAsync(4242, "Ws.pak");
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Local.pak", "local-bytes"));
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Off.pak", "disabled"));
        await fx.Mods.SetEnabledAsync(ModKeys.Local("Off.pak"), false);
        var serverBefore = Snapshot(fx.ModsDir);
        var outRoot = Directory.CreateDirectory(Path.Combine(fx.Root, "exports")).FullName;

        var export = await fx.Bundles.ExportAsync(outRoot);

        Assert.Equal(serverBefore, Snapshot(fx.ModsDir));
        Assert.StartsWith(outRoot, export.BundleDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["Ws.pak", "Local.pak"], export.Manifest.Mods.Select(m => m.FileName));
        Assert.Equal("Ws.pak" + Environment.NewLine + "Local.pak", await File.ReadAllTextAsync(export.ModListPath));
        Assert.False(File.Exists(Path.Combine(export.BundleDirectory, "Mods", "Off.pak")));
        foreach (var entry in export.Manifest.Mods)
        {
            var copy = Path.Combine(export.BundleDirectory, "Mods", entry.FileName);
            Assert.Equal(BackupFileHasher.Sha256File(Path.Combine(fx.ModsDir, entry.FileName)), entry.Sha256);
            Assert.Equal(entry.Sha256, BackupFileHasher.Sha256File(copy));
            Assert.Equal(new FileInfo(copy).Length, entry.SizeBytes);
        }

        var ws = export.Manifest.Mods[0];
        Assert.Equal(ModSourceType.Workshop, ws.SourceType);
        Assert.Equal(4242, ws.WorkshopId);
        var local = export.Manifest.Mods[1];
        Assert.Equal(ModSourceType.Local, local.SourceType);
        Assert.Null(local.WorkshopId);
        Assert.Equal(2, local.LoadOrder);

        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(export.ManifestPath));
        Assert.Equal("Local", json.RootElement.GetProperty("mods")[1].GetProperty("sourceType").GetString());
        Assert.True(File.Exists(Path.Combine(export.BundleDirectory, ClientModBundleService.ReadmeFileName)));
        Assert.Empty(Directory.EnumerateFiles(export.BundleDirectory, "*.exe", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Bundle_export_refuses_client_and_server_locations_including_trailing_separator()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("X.pak", "x"));
        var client = Directory.CreateDirectory(Path.Combine(fx.Root, "conan exiles", "Conan Exiles Enhanced")).FullName;
        await File.WriteAllTextAsync(Path.Combine(client, "ConanSandbox.exe"), "client");
        await fx.Settings.UpdateAsync(s => s.Client.RootDirectory = client + Path.DirectorySeparatorChar);

        foreach (var target in new[]
                 {
                     client,
                     Path.Combine(client, "ConanSandbox", "Mods"),
                     Path.GetDirectoryName(client)!,
                     fx.Install,
                     fx.ModsDir
                 })
        {
            if (target == Path.GetDirectoryName(client))
            {
                await File.WriteAllTextAsync(Path.Combine(target, "Run Me!.bat"), "launcher");
            }

            var error = await Record.ExceptionAsync(() => fx.Bundles.ExportAsync(target));
            Assert.IsType<UserFacingException>(error);
        }

        Assert.False(Directory.Exists(Path.Combine(client, "ConanSandbox")));
        Assert.Empty(Directory.EnumerateDirectories(fx.ModsDir));
    }

    [Fact]
    public async Task Bundle_export_with_a_missing_server_pak_fails_and_leaves_no_bundle()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Gone.pak", "x"));
        File.Delete(Path.Combine(fx.ModsDir, "Gone.pak"));
        var outRoot = Directory.CreateDirectory(Path.Combine(fx.Root, "exports")).FullName;

        var error = await Record.ExceptionAsync(() => fx.Bundles.ExportAsync(outRoot));

        Assert.IsType<UserFacingException>(error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(outRoot));
    }

    [Fact]
    public async Task Client_sync_plan_is_read_only_and_detects_missing_matching_and_different_files()
    {
        var fx = await LocalFixture.CreateAsync();
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("One.pak", "one"));
        await fx.Updates.ImportLocalModAsync(fx.WriteSource("Two.pak", "two"));
        var export = await fx.Bundles.ExportAsync(Directory.CreateDirectory(Path.Combine(fx.Root, "exports")).FullName);
        var client = Directory.CreateDirectory(Path.Combine(fx.Root, "client")).FullName;
        await File.WriteAllTextAsync(Path.Combine(client, "ConanSandbox.exe"), "client");
        var before = Snapshot(client);

        var empty = fx.Bundles.PlanClientSync(export.BundleDirectory, client);

        Assert.Equal(before, Snapshot(client));
        Assert.True(empty.ClientExecutableFound);
        Assert.All(empty.Items, i => Assert.Equal(ClientModSyncAction.Copy, i.Action));
        Assert.False(empty.ModListMatches);
        Assert.False(empty.IsInSync);

        var clientMods = Directory.CreateDirectory(Path.Combine(client, "ConanSandbox", "Mods")).FullName;
        File.Copy(Path.Combine(export.BundleDirectory, "Mods", "One.pak"), Path.Combine(clientMods, "One.pak"));
        await File.WriteAllTextAsync(Path.Combine(clientMods, "Two.pak"), "older-two");
        await File.WriteAllTextAsync(Path.Combine(clientMods, "Extra.pak"), "extra");
        var mixed = fx.Bundles.PlanClientSync(export.BundleDirectory, client);
        Assert.Equal(ClientModSyncAction.UpToDate, mixed.Items.Single(i => i.FileName == "One.pak").Action);
        Assert.Equal(ClientModSyncAction.Replace, mixed.Items.Single(i => i.FileName == "Two.pak").Action);
        Assert.Equal(["Extra.pak"], mixed.ExtraClientPaks);

        File.Copy(Path.Combine(export.BundleDirectory, "Mods", "Two.pak"), Path.Combine(clientMods, "Two.pak"), overwrite: true);
        File.Copy(export.ModListPath, Path.Combine(clientMods, "modlist.txt"));
        var synced = fx.Bundles.PlanClientSync(export.BundleDirectory, client);
        Assert.True(synced.IsInSync);
    }

    [Fact]
    public void Client_sync_plan_rejects_unsafe_manifest_names()
    {
        var manifest = new ClientModBundleManifest
        {
            CreatedAt = DateTimeOffset.Now,
            Generator = "test",
            Mods = [new ClientModBundleEntry(1, @"..\..\evil.pak", 1, "00", ModSourceType.Local, null, "evil")]
        };

        Assert.Throws<InvalidDataException>(() => ClientModSyncPlanner.Plan(manifest, Path.GetTempPath()));
    }

    // ------------------------------------------------------------ Existing server / optional SteamCMD

    [Fact]
    public void Locator_finds_root_or_binaries_server_exe_and_never_shipping_or_client()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "csc-locator", Guid.NewGuid().ToString("n"))).FullName;
        Assert.Null(DedicatedServerLocator.Find(root));

        File.WriteAllText(Path.Combine(root, "ConanSandbox.exe"), "client");
        var bin = Directory.CreateDirectory(Path.Combine(root, "ConanSandbox", "Binaries", "Win64")).FullName;
        File.WriteAllText(Path.Combine(bin, "ConanSandboxServer-Win64-Shipping.exe"), "shipping");
        Assert.Null(DedicatedServerLocator.Find(root));

        File.WriteAllText(Path.Combine(bin, "ConanSandboxServer.exe"), "server");
        Assert.Equal(Path.Combine(bin, "ConanSandboxServer.exe"), DedicatedServerLocator.Find(root));

        File.WriteAllText(Path.Combine(root, "ConanSandboxServer.exe"), "launcher");
        Assert.Equal(Path.Combine(root, "ConanSandboxServer.exe"), DedicatedServerLocator.Find(root));
    }

    [Fact]
    public void Readiness_accepts_existing_server_without_steamcmd()
    {
        var result = LiveTestReadinessCalculator.CalculateServer(
        [
            Check(DiagnosticCheckIds.ServerExecutable, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.SteamCmdExecutable, DiagnosticStatus.NotConfigured),
            Check(DiagnosticCheckIds.ServerWorkspace, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.AppBackupRoot, DiagnosticStatus.Pass)
        ]);

        Assert.True(result.IsReady, string.Join(" | ", result.Blockers));
        Assert.Contains(result.Notes, n => n.Contains("SteamCMD is not available", StringComparison.Ordinal));
    }

    [Fact]
    public void Readiness_without_server_or_steamcmd_names_both_sources()
    {
        var result = LiveTestReadinessCalculator.CalculateServer(
        [
            Check(DiagnosticCheckIds.ServerExecutable, DiagnosticStatus.NotConfigured),
            Check(DiagnosticCheckIds.SteamCmdExecutable, DiagnosticStatus.NotConfigured),
            Check(DiagnosticCheckIds.ServerWorkspace, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.AppBackupRoot, DiagnosticStatus.Pass)
        ]);

        Assert.False(result.IsReady);
        var blocker = Assert.Single(result.Blockers);
        Assert.Contains("existing ConanSandboxServer.exe", blocker, StringComparison.Ordinal);
        Assert.Contains("SteamCMD", blocker, StringComparison.Ordinal);
    }

    [Fact]
    public void Readiness_with_steamcmd_but_no_server_is_ready_for_install()
    {
        var result = LiveTestReadinessCalculator.CalculateServer(
        [
            Check(DiagnosticCheckIds.ServerExecutable, DiagnosticStatus.Warning),
            Check(DiagnosticCheckIds.SteamCmdExecutable, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.ServerWorkspace, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.AppBackupRoot, DiagnosticStatus.Pass)
        ]);

        Assert.True(result.IsReady);
    }

    [Fact]
    public async Task Diagnostics_existing_server_without_steamcmd_is_ready_for_server_live_test()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureServerAsync();

        var report = await h.Service.RunAsync();

        var steam = report.Find(DiagnosticCheckIds.SteamCmdExecutable)!;
        Assert.Equal(DiagnosticStatus.NotConfigured, steam.Status);
        Assert.Contains("optional", steam.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ServerExecutable)!.Status);
        Assert.True(report.ServerLiveTest.IsReady, string.Join(" | ", report.ServerLiveTest.Blockers));
    }

    [Fact]
    public async Task Diagnostics_warn_when_enabled_workshop_mods_have_no_steamcmd()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureServerAsync(modPaks: ["Alpha.pak"]);

        var report = await h.Service.RunAsync();

        var sources = report.Find(DiagnosticCheckIds.ModsSources)!;
        Assert.Equal(DiagnosticStatus.Warning, sources.Status);
        Assert.Equal("NO", sources.Facts["SteamCmdAvailable"]);
    }

    [Fact]
    public async Task Diagnostics_local_mods_do_not_need_steamcmd()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var install = await h.ConfigureServerAsync(modPaks: ["Alpha.pak"]);
        await h.Settings.UpdateAsync(s =>
        {
            s.Mods.Mods[0].SourceType = ModSourceType.Local;
            s.Mods.Mods[0].WorkshopId = 0;
        });

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ModsSources)!.Status);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ModsPakFiles)!.Status);
        _ = install;
    }

    // ------------------------------------------------------------ helpers

    private static DiagnosticCheckResult Check(string id, DiagnosticStatus status) => new()
    {
        Id = id,
        Category = DiagnosticCategories.System,
        Name = id,
        Status = status,
        Summary = status.ToString()
    };

    private static (long Length, DateTime Modified, string Hash) SourceState(string path) =>
        (new FileInfo(path).Length, File.GetLastWriteTimeUtc(path), BackupFileHasher.Sha256File(path));

    private static Dictionary<string, string> Snapshot(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(p => p, p => $"{new FileInfo(p).Length}|{File.GetLastWriteTimeUtc(p).Ticks}|{BackupFileHasher.Sha256File(p)}");

    private sealed class RecordingWorkshopClient : ISteamWorkshopClient
    {
        public List<long> Requested { get; } = new();

        public Task<IReadOnlyList<WorkshopPublishedFileDetails>> GetPublishedFileDetailsAsync(
            IReadOnlyList<long> workshopIds,
            CancellationToken cancellationToken = default)
        {
            Requested.AddRange(workshopIds);
            return Task.FromResult<IReadOnlyList<WorkshopPublishedFileDetails>>(Array.Empty<WorkshopPublishedFileDetails>());
        }
    }

    private sealed class LocalFixture
    {
        public required string Root { get; init; }

        public required AppPaths Paths { get; init; }

        public required JsonSettingsService Settings { get; init; }

        public required string Install { get; init; }

        public required string ModsDir { get; init; }

        public required string SourceDir { get; init; }

        public required ScriptedSteamCmd Steam { get; init; }

        public required ServerActionGate Gate { get; init; }

        public required CountingBackup Backup { get; init; }

        public required RecordingServer Server { get; init; }

        public required WorkshopModService Mods { get; init; }

        public required ServerUpdateService Updates { get; init; }

        public required ClientModBundleService Bundles { get; init; }

        public required List<string> Timeline { get; init; }

        public string ModListPath => Path.Combine(ModsDir, "modlist.txt");

        public static async Task<LocalFixture> CreateAsync()
        {
            var (data, paths, settings) = QaTestSupport.CreateData();
            var root = Path.GetDirectoryName(data)!;
            var install = Path.Combine(root, Guid.NewGuid().ToString("n") + "-server");
            await QaTestSupport.ConfigureInstallAsync(settings, install);
            var modsDir = Directory.CreateDirectory(Path.Combine(install, "ConanSandbox", "Mods")).FullName;
            var sourceDir = Directory.CreateDirectory(Path.Combine(root, Guid.NewGuid().ToString("n") + "-downloads")).FullName;
            var timeline = new List<string>();
            var steam = new ScriptedSteamCmd();
            var gate = new ServerActionGate();
            var backup = new CountingBackup { Timeline = timeline };
            var server = new RecordingServer(gate) { Timeline = timeline };
            var mods = new WorkshopModService(
                settings, steam, backup, gate, paths, new EmptyWorkshopClient(), new RecordingActivityLog(),
                NullLogger<WorkshopModService>.Instance);
            var updates = new ServerUpdateService(
                settings, steam, server, backup, mods, gate, new RecordingActivityLog(),
                NullLogger<ServerUpdateService>.Instance);
            var bundles = new ClientModBundleService(settings, paths, NullLogger<ClientModBundleService>.Instance);
            return new LocalFixture
            {
                Root = Directory.CreateDirectory(Path.Combine(root, Guid.NewGuid().ToString("n"))).FullName,
                Paths = paths,
                Settings = settings,
                Install = install,
                ModsDir = modsDir,
                SourceDir = sourceDir,
                Steam = steam,
                Gate = gate,
                Backup = backup,
                Server = server,
                Mods = mods,
                Updates = updates,
                Bundles = bundles,
                Timeline = timeline
            };
        }

        public string WriteSource(string fileName, string content)
        {
            var path = Path.Combine(SourceDir, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        public async Task AddWorkshopModAsync(long id, string fileName)
        {
            await File.WriteAllTextAsync(Path.Combine(ModsDir, fileName), "workshop-bytes");
            await Settings.UpdateAsync(s => s.Mods.Mods.Add(new WorkshopMod
            {
                WorkshopId = id,
                Name = "Workshop " + id,
                LocalFileName = fileName,
                Enabled = true,
                LoadOrder = s.Mods.Mods.Count + 1
            }));
        }

        public WorkshopModService CreateModService(ISteamWorkshopClient client) =>
            new(Settings, Steam, Backup, Gate, Paths, client, new RecordingActivityLog(), NullLogger<WorkshopModService>.Instance);
    }
}
