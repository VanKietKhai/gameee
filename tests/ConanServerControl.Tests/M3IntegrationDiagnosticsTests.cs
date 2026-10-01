using System.Text.Json;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Diagnostics;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Diagnostics;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.Settings;
using ConanServerControl.Infrastructure.Steam;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConanServerControl.Tests;

/// <summary>
/// M3 Task 3: read-only integration diagnostics against temp trees.
/// No real SteamCMD, Conan server, client, Workshop or network is touched.
/// </summary>
public sealed class M3IntegrationDiagnosticsTests
{
    // ------------------------------------------------------------ SteamCMD

    [Fact]
    public async Task Configured_valid_steamcmd_path_passes_without_executing_steamcmd()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureSteamCmdAsync();

        var report = await h.Service.RunAsync();

        var check = report.Find(DiagnosticCheckIds.SteamCmdExecutable)!;
        Assert.Equal(DiagnosticStatus.Pass, check.Status);
        Assert.Equal(DiagnosticEvidence.FilesystemInspected, check.Evidence);
        Assert.Contains("not live-tested", check.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("NO", check.Facts["Executed"]);
        Assert.Equal(0, h.SteamRunner.Calls);
        Assert.Equal(DiagnosticStatus.NotTested, report.Find(DiagnosticCheckIds.SteamCmdLive)!.Status);
    }

    [Fact]
    public async Task Missing_steamcmd_in_configured_folder_fails()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var empty = Directory.CreateDirectory(Path.Combine(h.Root, "no-steamcmd")).FullName;
        await h.Settings.UpdateAsync(s => s.SteamCmd.InstallDirectory = empty);

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, report.Find(DiagnosticCheckIds.SteamCmdExecutable)!.Status);
        Assert.False(report.ServerLiveTest.IsReady);
        Assert.Contains(report.ServerLiveTest.Blockers, b => b.Contains(DiagnosticCheckIds.SteamCmdExecutable, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unconfigured_and_absent_steamcmd_is_not_configured()
    {
        var h = await DiagnosticsHarness.CreateAsync();

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.NotConfigured, report.Find(DiagnosticCheckIds.SteamCmdExecutable)!.Status);
        Assert.False(report.ServerLiveTest.IsReady);
    }

    // ------------------------------------------------------------ Dedicated server

    [Fact]
    public async Task Valid_dedicated_server_executable_passes_and_is_not_started()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureServerAsync();

        var report = await h.Service.RunAsync();

        var exe = report.Find(DiagnosticCheckIds.ServerExecutable)!;
        Assert.Equal(DiagnosticStatus.Pass, exe.Status);
        Assert.Equal("NO", exe.Facts["Started"]);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ServerInstallDirectory)!.Status);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ServerWorkspace)!.Status);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ServerSaveLocation)!.Status);
        Assert.Equal(DiagnosticStatus.NotTested, report.Find(DiagnosticCheckIds.ServerLive)!.Status);
        Assert.Equal(0, h.Server.StartCalls);
    }

    [Fact]
    public async Task Client_executable_selected_as_server_fails()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureSteamCmdAsync();
        await h.ConfigureServerAsync();
        var clientRoot = h.CreateClient();
        await h.Settings.UpdateAsync(s =>
            s.ServerPaths.ServerExecutablePath = Path.Combine(clientRoot, "ConanSandbox.exe"));

        var report = await h.Service.RunAsync();

        var exe = report.Find(DiagnosticCheckIds.ServerExecutable)!;
        Assert.Equal(DiagnosticStatus.Fail, exe.Status);
        Assert.Contains("CLIENT", exe.Summary, StringComparison.Ordinal);
        Assert.False(report.ServerLiveTest.IsReady);
    }

    [Fact]
    public async Task Run_me_bat_selected_as_server_fails()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureSteamCmdAsync();
        await h.ConfigureServerAsync();
        var clientRoot = h.CreateClient();
        var launcher = Path.Combine(Path.GetDirectoryName(clientRoot)!, "Run Me!.bat");
        await h.Settings.UpdateAsync(s => s.ServerPaths.ServerExecutablePath = launcher);

        var report = await h.Service.RunAsync();

        var exe = report.Find(DiagnosticCheckIds.ServerExecutable)!;
        Assert.Equal(DiagnosticStatus.Fail, exe.Status);
        Assert.Contains("Run Me!.bat", exe.Summary, StringComparison.Ordinal);
        Assert.False(report.ServerLiveTest.IsReady);
    }

    [Fact]
    public async Task Install_directory_that_holds_the_game_client_fails()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var clientRoot = h.CreateClient();
        await h.Settings.UpdateAsync(s => s.ServerPaths.ServerInstallDirectory = clientRoot);

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, report.Find(DiagnosticCheckIds.ServerInstallDirectory)!.Status);
    }

    [Fact]
    public async Task Workspace_overlapping_configured_client_root_fails()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var clientRoot = h.CreateClient();
        await h.Settings.UpdateAsync(s =>
        {
            s.Client.RootDirectory = clientRoot;
            s.ServerPaths.ServerInstallDirectory = Path.Combine(clientRoot, "server");
        });

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, report.Find(DiagnosticCheckIds.ServerWorkspace)!.Status);
        Assert.False(report.ServerLiveTest.IsReady);
    }

    [Fact]
    public async Task Not_yet_installed_server_with_safe_workspace_is_ready_for_server_live_test()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureSteamCmdAsync();
        var install = Path.Combine(h.Root, "fresh-server");
        await h.Settings.UpdateAsync(s =>
        {
            s.ServerPaths.ServerInstallDirectory = install;
            s.ServerPaths.ServerExecutablePath = Path.Combine(install, @"ConanSandbox\Binaries\Win64\ConanSandboxServer.exe");
        });

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Warning, report.Find(DiagnosticCheckIds.ServerExecutable)!.Status);
        Assert.True(report.ServerLiveTest.IsReady, string.Join(" | ", report.ServerLiveTest.Blockers));
        Assert.Contains(report.ServerLiveTest.Notes, n => n.Contains("not installed", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(@"C:\ConanServer\ConanSandbox\Binaries\Win64\ConanSandboxServer.exe", ConanExecutableKind.DedicatedServer)]
    [InlineData(@"C:\ConanServer\ConanSandbox\Binaries\Win64\ConanSandboxServer-Win64-Shipping.exe", ConanExecutableKind.DedicatedServerShipping)]
    [InlineData(@"D:\conan exiles\Run Me!.bat", ConanExecutableKind.ClientLauncherBatch)]
    [InlineData(@"D:\conan exiles\Conan Exiles Enhanced\ConanSandbox.exe", ConanExecutableKind.StandaloneClient)]
    [InlineData(@"D:\x\start-server.cmd", ConanExecutableKind.BatchOrScript)]
    [InlineData(@"D:\x\other.exe", ConanExecutableKind.Unknown)]
    [InlineData("", ConanExecutableKind.Empty)]
    public void Executable_classifier_identifies_server_client_and_launcher(string path, ConanExecutableKind expected)
    {
        Assert.Equal(expected, ConanExecutableClassifier.Classify(path));
    }

    // ------------------------------------------------------------ Standalone client

    [Fact]
    public async Task Valid_standalone_client_root_is_detected_read_only()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var clientRoot = h.CreateClient(modPaks: ["ClientMod.pak"]);
        await h.Settings.UpdateAsync(s => s.Client.RootDirectory = clientRoot);

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ClientRoot)!.Status);
        var exe = report.Find(DiagnosticCheckIds.ClientExecutable)!;
        Assert.Equal(DiagnosticStatus.Pass, exe.Status);
        Assert.Equal("NO", exe.Facts["Launched"]);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ClientMods)!.Status);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ClientConfig)!.Status);
        Assert.Equal(DiagnosticStatus.NotTested, report.Find(DiagnosticCheckIds.ClientJoinLive)!.Status);
        Assert.Equal(clientRoot, report.StandaloneClientRoot);
    }

    [Fact]
    public async Task Client_root_pointing_at_launcher_folder_suggests_subfolder()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var clientRoot = h.CreateClient();
        var launcherFolder = Path.GetDirectoryName(clientRoot)!;
        await h.Settings.UpdateAsync(s => s.Client.RootDirectory = launcherFolder);

        var report = await h.Service.RunAsync();

        var root = report.Find(DiagnosticCheckIds.ClientRoot)!;
        Assert.Equal(DiagnosticStatus.Warning, root.Status);
        Assert.Equal(clientRoot, root.Facts["SuggestedRoot"]);
    }

    [Fact]
    public async Task Standalone_client_absent_does_not_block_server_only_readiness()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureSteamCmdAsync();
        await h.ConfigureServerAsync();

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.NotConfigured, report.Find(DiagnosticCheckIds.ClientRoot)!.Status);
        Assert.True(report.ServerLiveTest.IsReady, string.Join(" | ", report.ServerLiveTest.Blockers));
        Assert.Equal("READY FOR SERVER LIVE TEST", report.ServerLiveTest.Headline);
        Assert.False(report.ClientCompatibilityTest.IsReady);
    }

    [Fact]
    public async Task Client_missing_server_mods_is_reported_as_manual_copy_not_sync()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureServerAsync(modPaks: ["Alpha.pak", "Beta.pak"]);
        var clientRoot = h.CreateClient(modPaks: ["Alpha.pak"]);
        await h.Settings.UpdateAsync(s => s.Client.RootDirectory = clientRoot);

        var report = await h.Service.RunAsync();

        var parity = report.Find(DiagnosticCheckIds.ClientModParity)!;
        Assert.Equal(DiagnosticStatus.Warning, parity.Status);
        Assert.Contains("Beta.pak", parity.Details, StringComparison.Ordinal);
        Assert.Contains("not automatic", parity.Summary, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------ World

    [Fact]
    public async Task Enhanced_world_is_detected_without_opening_live_db()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var install = await h.ConfigureServerAsync();
        var saved = Path.Combine(install, "ConanSandbox", "Saved");
        await File.WriteAllTextAsync(Path.Combine(saved, "game_0.db"), "not-really-sqlite");
        await File.WriteAllTextAsync(Path.Combine(saved, "game_0.db-wal"), "wal");

        var report = await h.Service.RunAsync();

        var world = report.Find(DiagnosticCheckIds.WorldFiles)!;
        Assert.Equal(DiagnosticStatus.Pass, world.Status);
        Assert.Equal("Enhanced", world.Facts["WorldType"]);
        Assert.Equal("game_0.db", world.Facts["MainDb"]);
        Assert.Equal("YES", world.Facts["WalPresent"]);
        Assert.Equal("NO", world.Facts["ShmPresent"]);
        Assert.Equal("NO", world.Facts["LiveDbOpened"]);
    }

    [Fact]
    public async Task Legacy_world_is_detected()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var install = await h.ConfigureServerAsync();
        await File.WriteAllTextAsync(Path.Combine(install, "ConanSandbox", "Saved", "game.db"), "legacy");

        var report = await h.Service.RunAsync();

        var world = report.Find(DiagnosticCheckIds.WorldFiles)!;
        Assert.Equal(DiagnosticStatus.Pass, world.Status);
        Assert.Equal("Legacy", world.Facts["WorldType"]);
        Assert.Equal("game.db", world.Facts["MainDb"]);
    }

    [Fact]
    public async Task Wal_without_main_world_db_fails()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var install = await h.ConfigureServerAsync();
        await File.WriteAllTextAsync(Path.Combine(install, "ConanSandbox", "Saved", "game_0.db-wal"), "orphan");

        var report = await h.Service.RunAsync();

        var world = report.Find(DiagnosticCheckIds.WorldFiles)!;
        Assert.Equal(DiagnosticStatus.Fail, world.Status);
        Assert.Equal("none", world.Facts["MainDb"]);
    }

    [Fact]
    public async Task No_known_world_is_a_warning_and_does_not_block_server_readiness()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureSteamCmdAsync();
        await h.ConfigureServerAsync();

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Warning, report.Find(DiagnosticCheckIds.WorldFiles)!.Status);
        Assert.True(report.ServerLiveTest.IsReady);
    }

    // ------------------------------------------------------------ Mods

    [Fact]
    public async Task Server_modlist_matching_application_state_passes()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureServerAsync(modPaks: ["Alpha.pak", "Beta.pak"]);

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ModsDirectory)!.Status);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ModsModList)!.Status);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ModsPakFiles)!.Status);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ModsOrdering)!.Status);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ModsDuplicates)!.Status);
        Assert.Equal(DiagnosticStatus.NotTested, report.Find(DiagnosticCheckIds.ModsWorkshopLive)!.Status);
    }

    [Fact]
    public async Task Server_modlist_in_different_order_warns_and_is_not_modified()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var install = await h.ConfigureServerAsync(modPaks: ["Alpha.pak", "Beta.pak"]);
        var modList = Path.Combine(install, "ConanSandbox", "Mods", "modlist.txt");
        await File.WriteAllTextAsync(modList, "Beta.pak\r\nAlpha.pak");
        var before = await File.ReadAllBytesAsync(modList);

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Warning, report.Find(DiagnosticCheckIds.ModsOrdering)!.Status);
        Assert.Equal(before, await File.ReadAllBytesAsync(modList));
    }

    [Fact]
    public async Task Missing_expected_pak_fails()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var install = await h.ConfigureServerAsync(modPaks: ["Alpha.pak", "Beta.pak"]);
        File.Delete(Path.Combine(install, "ConanSandbox", "Mods", "Beta.pak"));

        var report = await h.Service.RunAsync();

        var paks = report.Find(DiagnosticCheckIds.ModsPakFiles)!;
        Assert.Equal(DiagnosticStatus.Fail, paks.Status);
        Assert.Equal("1", paks.Facts["MissingPaks"]);
        Assert.Contains("Beta.pak", paks.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zero_byte_pak_fails()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var install = await h.ConfigureServerAsync(modPaks: ["Alpha.pak"]);
        await File.WriteAllBytesAsync(Path.Combine(install, "ConanSandbox", "Mods", "Alpha.pak"), Array.Empty<byte>());

        var report = await h.Service.RunAsync();

        var paks = report.Find(DiagnosticCheckIds.ModsPakFiles)!;
        Assert.Equal(DiagnosticStatus.Fail, paks.Status);
        Assert.Equal("1", paks.Facts["ZeroBytePaks"]);
    }

    [Fact]
    public async Task Duplicate_modlist_entries_warn()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        var install = await h.ConfigureServerAsync(modPaks: ["Alpha.pak"]);
        await File.WriteAllTextAsync(Path.Combine(install, "ConanSandbox", "Mods", "modlist.txt"), "Alpha.pak\nalpha.pak");

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Warning, report.Find(DiagnosticCheckIds.ModsDuplicates)!.Status);
    }

    // ------------------------------------------------------------ Network / RCON

    [Fact]
    public async Task Valid_network_settings_are_config_valid_and_offline_runtime_is_not_tested()
    {
        var h = await DiagnosticsHarness.CreateAsync();

        var report = await h.Service.RunAsync();

        var ports = report.Find(DiagnosticCheckIds.NetworkPorts)!;
        Assert.Equal(DiagnosticStatus.Pass, ports.Status);
        Assert.Equal(DiagnosticEvidence.ConfigurationChecked, ports.Evidence);
        Assert.StartsWith("CONFIG VALID", ports.Summary, StringComparison.Ordinal);
        var runtime = report.Find(DiagnosticCheckIds.NetworkRuntime)!;
        Assert.Equal(DiagnosticStatus.NotTested, runtime.Status);
        Assert.StartsWith("RUNTIME NOT TESTED", runtime.Summary, StringComparison.Ordinal);
        Assert.Equal(0, h.PortProbeCalls);
    }

    [Theory]
    [InlineData(0, 27015, 25575)]
    [InlineData(70000, 27015, 25575)]
    [InlineData(7777, 7778, 25575)]
    [InlineData(7777, 7777, 25575)]
    public async Task Invalid_network_settings_fail(int game, int query, int rcon)
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.Settings.UpdateAsync(s =>
        {
            s.Server.GamePort = game;
            s.Server.QueryPort = query;
            s.Rcon.Port = rcon;
        });

        var report = await h.Service.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, report.Find(DiagnosticCheckIds.NetworkPorts)!.Status);
        Assert.False(report.ServerLiveTest.IsReady);
    }

    [Fact]
    public async Task Online_server_uses_read_only_runtime_probes()
    {
        var h = await DiagnosticsHarness.CreateAsync(portBound: true);
        await h.Settings.UpdateSecretsAsync(s => s.RconPassword = "Online-Rcon-Secret-123");
        h.Server.State.Status = ServerStatus.Online;

        var report = await h.Service.RunAsync();

        var runtime = report.Find(DiagnosticCheckIds.NetworkRuntime)!;
        Assert.Equal(DiagnosticStatus.Pass, runtime.Status);
        Assert.Equal(DiagnosticEvidence.RuntimeObserved, runtime.Evidence);
        Assert.Contains("does not prove", runtime.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.RconRuntime)!.Status);
        Assert.Equal(["listplayers"], h.Rcon.Commands);
        Assert.Equal(0, h.Server.StartCalls + h.Server.StopCalls);
    }

    [Fact]
    public async Task Rcon_password_status_never_exposes_the_password()
    {
        const string secret = "Very-Secret-Rcon-Pw-42";
        var h = await DiagnosticsHarness.CreateAsync();
        await h.Settings.UpdateSecretsAsync(s => s.RconPassword = secret);

        var report = await h.Service.RunAsync();

        var check = report.Find(DiagnosticCheckIds.RconPassword)!;
        Assert.Equal(DiagnosticStatus.Pass, check.Status);
        Assert.Equal("YES", check.Facts["PasswordConfigured"]);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(report), StringComparison.Ordinal);
        Assert.Empty(h.Rcon.Commands);
    }

    [Fact]
    public async Task Missing_rcon_password_is_a_warning_not_a_blocker()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureSteamCmdAsync();
        await h.ConfigureServerAsync();

        var report = await h.Service.RunAsync();

        var check = report.Find(DiagnosticCheckIds.RconPassword)!;
        Assert.Equal(DiagnosticStatus.Warning, check.Status);
        Assert.Equal("NO", check.Facts["PasswordConfigured"]);
        Assert.True(report.ServerLiveTest.IsReady);
    }

    // ------------------------------------------------------------ Redaction / export

    [Fact]
    public async Task Exported_report_redacts_all_known_secrets_and_protected_blob()
    {
        var secrets = new[]
        {
            "Rcon-Secret-Value-001",
            "Server-Join-Secret-002",
            "Admin-Secret-Value-003",
            "Steam-Password-Secret-004",
            "pbkdf2$100000$WebAdminHashSecret005",
            "steam_login_name_006"
        };
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureSteamCmdAsync();
        await h.ConfigureServerAsync(modPaks: ["Alpha.pak"]);
        await h.Settings.UpdateAsync(s =>
        {
            s.SteamCmd.UseAnonymousLogin = false;
            s.SteamCmd.SteamUsername = secrets[5];
        });
        await h.Settings.UpdateSecretsAsync(s =>
        {
            s.RconPassword = secrets[0];
            s.ServerPassword = secrets[1];
            s.AdminPassword = secrets[2];
            s.SteamPassword = secrets[3];
            s.WebAdminPasswordHash = secrets[4];
        });
        var protectedBlob = (await File.ReadAllTextAsync(h.Paths.SecretsFilePath)).Trim();

        var report = await h.Service.RunAsync();
        var export = await h.Service.ExportAsync(report);

        var json = await File.ReadAllTextAsync(export.JsonPath);
        var text = await File.ReadAllTextAsync(export.TextPath);
        foreach (var output in new[] { json, text, JsonSerializer.Serialize(report) })
        {
            foreach (var secret in secrets)
            {
                Assert.DoesNotContain(secret, output, StringComparison.Ordinal);
            }

            Assert.DoesNotContain(protectedBlob, output, StringComparison.Ordinal);
        }

        Assert.Contains("SteamPasswordConfigured", json, StringComparison.Ordinal);
        Assert.Contains(h.ServerInstall!, text, StringComparison.Ordinal);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal("1", parsed.RootElement.GetProperty("ReportVersion").GetString());
    }

    [Fact]
    public void Redactor_removes_secrets_from_details_facts_and_key_value_pairs()
    {
        var redactor = new DiagnosticReportRedactor(["Hunter2-Secret", "ab"]);
        var check = new DiagnosticCheckResult
        {
            Id = "x",
            Category = DiagnosticCategories.System,
            Name = "n",
            Status = DiagnosticStatus.Warning,
            Summary = "password is Hunter2-Secret",
            Details = "args: -log -ServerPassword=OtherUnknownValue ?AdminPassword=\"quoted value\" token: abc123",
            SuggestedAction = "ab cab ab",
            Facts = new Dictionary<string, string> { ["Path"] = @"C:\Servers\Hunter2-Secret\x" }
        };

        var safe = redactor.Redact(check);

        Assert.DoesNotContain("Hunter2-Secret", safe.Summary + safe.Facts["Path"], StringComparison.Ordinal);
        Assert.DoesNotContain("OtherUnknownValue", safe.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("quoted value", safe.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", safe.Details, StringComparison.Ordinal);
        Assert.Contains("-ServerPassword=[REDACTED]", safe.Details, StringComparison.Ordinal);
        // Short secrets are only redacted as whole tokens; "cab" stays intact.
        Assert.Equal("[REDACTED] cab [REDACTED]", safe.SuggestedAction);
    }

    [Fact]
    public async Task Run_is_read_only_and_export_writes_only_to_diagnostics_folder()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureSteamCmdAsync();
        var install = await h.ConfigureServerAsync(modPaks: ["Alpha.pak"]);
        await File.WriteAllTextAsync(Path.Combine(install, "ConanSandbox", "Saved", "game_0.db"), "db");
        var clientRoot = h.CreateClient(modPaks: ["Alpha.pak"]);
        await h.Settings.UpdateAsync(s => s.Client.RootDirectory = clientRoot);
        var before = Snapshot(h.Root);

        var report = await h.Service.RunAsync();

        Assert.Equal(before, Snapshot(h.Root));
        Assert.False(Directory.Exists(h.Service.ReportDirectory));

        var export = await h.Service.ExportAsync(report);
        var second = await h.Service.ExportAsync(report);

        var after = Snapshot(h.Root);
        var added = after.Keys.Except(before.Keys).ToArray();
        Assert.All(added, p => Assert.StartsWith(h.Service.ReportDirectory, p, StringComparison.OrdinalIgnoreCase));
        // Everything that existed is unchanged, except the parent folder's timestamp
        // changing because the diagnostics folder was created inside it.
        Assert.All(
            before.Where(kv => !h.Service.ReportDirectory.StartsWith(kv.Key + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)),
            kv => Assert.Equal(kv.Value, after[kv.Key]));
        Assert.NotEqual(export.JsonPath, second.JsonPath);
        Assert.Equal(0, h.SteamRunner.Calls);
        Assert.Equal(0, h.Server.StartCalls + h.Server.StopCalls);
        Assert.Empty(h.Rcon.Commands);
    }

    [Fact]
    public async Task No_check_claims_live_verification()
    {
        var h = await DiagnosticsHarness.CreateAsync(portBound: true);
        await h.ConfigureSteamCmdAsync();
        await h.ConfigureServerAsync(modPaks: ["Alpha.pak"]);
        h.Server.State.Status = ServerStatus.Online;

        var report = await h.Service.RunAsync();

        Assert.True(report.ConfigurationOnly);
        Assert.DoesNotContain(report.Checks, c => c.Evidence == DiagnosticEvidence.LiveVerified);
        foreach (var id in new[]
                 {
                     DiagnosticCheckIds.SteamCmdLive, DiagnosticCheckIds.ServerLive,
                     DiagnosticCheckIds.ModsWorkshopLive, DiagnosticCheckIds.ClientJoinLive
                 })
        {
            var check = report.Find(id)!;
            Assert.Equal(DiagnosticStatus.NotTested, check.Status);
            Assert.Equal(DiagnosticEvidence.NotExercised, check.Evidence);
        }

        Assert.Contains(report.ServerLiveTest.Notes, n => n.Contains("not been live verified", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------ Backups

    [Fact]
    public async Task Last_verified_backup_is_read_from_metadata()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        h.WriteBackupMetadata("2026-01-01_000000", DateTimeOffset.UtcNow.AddDays(-2), verified: true);
        h.WriteBackupMetadata("2026-01-02_000000", DateTimeOffset.UtcNow.AddDays(-1), verified: true);

        var report = await h.Service.RunAsync();

        var check = report.Find(DiagnosticCheckIds.BackupsLastVerified)!;
        Assert.Equal(DiagnosticStatus.Pass, check.Status);
        Assert.Equal(DiagnosticEvidence.RecordedResult, check.Evidence);
        Assert.Equal("2026-01-02_000000", check.Facts["LastVerifiedBackupId"]);
    }

    [Fact]
    public async Task Latest_unverified_backup_warns_and_reports_last_verified()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        h.WriteBackupMetadata("old", DateTimeOffset.UtcNow.AddDays(-2), verified: true);
        h.WriteBackupMetadata("new", DateTimeOffset.UtcNow.AddDays(-1), verified: false);

        var report = await h.Service.RunAsync();

        var check = report.Find(DiagnosticCheckIds.BackupsLastVerified)!;
        Assert.Equal(DiagnosticStatus.Warning, check.Status);
        Assert.Equal("old", check.Facts["LastVerifiedBackupId"]);
        Assert.Equal("NO", check.Facts["LastBackupVerified"]);
    }

    // ------------------------------------------------------------ Readiness calculation

    [Fact]
    public void Server_live_readiness_requires_steamcmd_workspace_and_backup_root()
    {
        var ready = LiveTestReadinessCalculator.CalculateServer(
        [
            Check(DiagnosticCheckIds.SteamCmdExecutable, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.ServerWorkspace, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.AppBackupRoot, DiagnosticStatus.Warning),
            Check(DiagnosticCheckIds.ServerExecutable, DiagnosticStatus.Warning),
            Check(DiagnosticCheckIds.ClientRoot, DiagnosticStatus.NotConfigured),
            Check(DiagnosticCheckIds.ClientExecutable, DiagnosticStatus.NotConfigured)
        ]);
        Assert.True(ready.IsReady);
        Assert.Equal("READY FOR SERVER LIVE TEST", ready.Headline);

        var noSteam = LiveTestReadinessCalculator.CalculateServer(
        [
            Check(DiagnosticCheckIds.SteamCmdExecutable, DiagnosticStatus.NotConfigured),
            Check(DiagnosticCheckIds.ServerWorkspace, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.AppBackupRoot, DiagnosticStatus.Pass)
        ]);
        Assert.False(noSteam.IsReady);
        Assert.Equal("NOT READY FOR SERVER LIVE TEST", noSteam.Headline);

        var badBackupRoot = LiveTestReadinessCalculator.CalculateServer(
        [
            Check(DiagnosticCheckIds.SteamCmdExecutable, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.ServerWorkspace, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.AppBackupRoot, DiagnosticStatus.Fail)
        ]);
        Assert.False(badBackupRoot.IsReady);

        var clientAsServer = LiveTestReadinessCalculator.CalculateServer(
        [
            Check(DiagnosticCheckIds.SteamCmdExecutable, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.ServerWorkspace, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.AppBackupRoot, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.ServerExecutable, DiagnosticStatus.Fail)
        ]);
        Assert.False(clientAsServer.IsReady);
    }

    [Fact]
    public void Client_compatibility_readiness_needs_server_readiness_and_client_executable()
    {
        var serverChecks = new List<DiagnosticCheckResult>
        {
            Check(DiagnosticCheckIds.SteamCmdExecutable, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.ServerWorkspace, DiagnosticStatus.Pass),
            Check(DiagnosticCheckIds.AppBackupRoot, DiagnosticStatus.Pass)
        };
        var server = LiveTestReadinessCalculator.CalculateServer(serverChecks);

        var noClient = LiveTestReadinessCalculator.CalculateClientCompatibility(
            [.. serverChecks, Check(DiagnosticCheckIds.ClientExecutable, DiagnosticStatus.NotConfigured)], server);
        Assert.False(noClient.IsReady);
        Assert.Contains(noClient.Notes, n => n.Contains("not automatic", StringComparison.OrdinalIgnoreCase));

        var withClient = LiveTestReadinessCalculator.CalculateClientCompatibility(
            [.. serverChecks, Check(DiagnosticCheckIds.ClientExecutable, DiagnosticStatus.Pass)], server);
        Assert.True(withClient.IsReady);
        Assert.Equal("READY FOR CLIENT COMPATIBILITY TEST", withClient.Headline);

        var serverNotReady = LiveTestReadinessCalculator.CalculateServer([]);
        var clientButNoServer = LiveTestReadinessCalculator.CalculateClientCompatibility(
            [Check(DiagnosticCheckIds.ClientExecutable, DiagnosticStatus.Pass)], serverNotReady);
        Assert.False(clientButNoServer.IsReady);
    }

    [Fact]
    public async Task End_to_end_server_and_client_ready_report()
    {
        var h = await DiagnosticsHarness.CreateAsync();
        await h.ConfigureSteamCmdAsync();
        await h.ConfigureServerAsync(modPaks: ["Alpha.pak"]);
        var clientRoot = h.CreateClient(modPaks: ["Alpha.pak"]);
        await h.Settings.UpdateAsync(s => s.Client.RootDirectory = clientRoot);

        var report = await h.Service.RunAsync();

        Assert.True(report.ServerLiveTest.IsReady, string.Join(" | ", report.ServerLiveTest.Blockers));
        Assert.True(report.ClientCompatibilityTest.IsReady, string.Join(" | ", report.ClientCompatibilityTest.Blockers));
        Assert.Equal(DiagnosticStatus.Pass, report.Find(DiagnosticCheckIds.ClientModParity)!.Status);
    }

    private static DiagnosticCheckResult Check(string id, DiagnosticStatus status) => new()
    {
        Id = id,
        Category = DiagnosticCategories.System,
        Name = id,
        Status = status,
        Summary = status.ToString()
    };

    private static Dictionary<string, (long Size, DateTime Modified)> Snapshot(string root) =>
        Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                p => p,
                p => File.Exists(p)
                    ? (new FileInfo(p).Length, File.GetLastWriteTimeUtc(p))
                    : (-1L, Directory.GetLastWriteTimeUtc(p)),
                StringComparer.OrdinalIgnoreCase);
}

internal sealed class DiagnosticsHarness
{
    private DiagnosticsHarness(string root, AppPaths paths, JsonSettingsService settings, bool portBound)
    {
        Root = root;
        Paths = paths;
        Settings = settings;
        var steam = new SteamCmdService(
            settings,
            paths,
            SteamRunner,
            new StubHttpClientFactory(new StubHttpHandler()),
            NullLogger<SteamCmdService>.Instance);
        Service = new IntegrationDiagnosticsService(
            paths,
            settings,
            steam,
            Server,
            Rcon,
            Network,
            NullLogger<IntegrationDiagnosticsService>.Instance,
            _ =>
            {
                PortProbeCalls++;
                return portBound;
            });
    }

    public string Root { get; }

    public AppPaths Paths { get; }

    public JsonSettingsService Settings { get; }

    public IntegrationDiagnosticsService Service { get; }

    public ExplodingProcessRunner SteamRunner { get; } = new();

    public CountingServer Server { get; } = new();

    public RecordingRcon Rcon { get; } = new();

    public FakeNetworkInfo Network { get; } = new();

    public int PortProbeCalls { get; private set; }

    public string? ServerInstall { get; private set; }

    public static async Task<DiagnosticsHarness> CreateAsync(bool portBound = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "csc-m3-diag", Guid.NewGuid().ToString("n"));
        var paths = new AppPaths(Path.Combine(root, "data"));
        paths.EnsureCreated();
        // The default SteamCMD folder must start empty so "not installed" is observable.
        var settings = new JsonSettingsService(paths, new PassthroughProtector(), NullLogger<JsonSettingsService>.Instance);
        await settings.LoadAsync();
        await settings.UpdateAsync(s => s.IsSetupComplete = true);
        return new DiagnosticsHarness(root, paths, settings, portBound);
    }

    public async Task<string> ConfigureSteamCmdAsync()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Root, "steamcmd")).FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, "steamcmd.exe"), "fake-steamcmd");
        await Settings.UpdateAsync(s => s.SteamCmd.InstallDirectory = dir);
        return dir;
    }

    public async Task<string> ConfigureServerAsync(IReadOnlyList<string>? modPaks = null)
    {
        var install = Path.Combine(Root, "ConanServer");
        var binaries = Directory.CreateDirectory(Path.Combine(install, "ConanSandbox", "Binaries", "Win64")).FullName;
        Directory.CreateDirectory(Path.Combine(install, "ConanSandbox", "Saved"));
        var exe = Path.Combine(binaries, "ConanSandboxServer.exe");
        await File.WriteAllTextAsync(exe, "fake-server");

        var mods = new List<WorkshopMod>();
        if (modPaks is { Count: > 0 })
        {
            var modsDir = Directory.CreateDirectory(Path.Combine(install, "ConanSandbox", "Mods")).FullName;
            for (var i = 0; i < modPaks.Count; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(modsDir, modPaks[i]), "pak-" + modPaks[i]);
                mods.Add(new WorkshopMod
                {
                    WorkshopId = 1000 + i,
                    Name = Path.GetFileNameWithoutExtension(modPaks[i]),
                    LocalFileName = modPaks[i],
                    Enabled = true,
                    LoadOrder = i + 1
                });
            }

            await File.WriteAllTextAsync(Path.Combine(modsDir, "modlist.txt"), string.Join(Environment.NewLine, modPaks));
        }

        await Settings.UpdateAsync(s =>
        {
            s.ServerPaths.ServerExecutablePath = exe;
            s.ServerPaths.ServerInstallDirectory = install;
            s.ServerPaths.ServerWorkingDirectory = null;
            s.Mods.Mods = mods;
        });
        ServerInstall = install;
        return install;
    }

    /// <summary>
    /// Mirrors the standalone layout: "&lt;launcher folder&gt;\Run Me!.bat" and
    /// "&lt;launcher folder&gt;\Conan Exiles Enhanced\ConanSandbox.exe". Returns the client root.
    /// </summary>
    public string CreateClient(IReadOnlyList<string>? modPaks = null)
    {
        var launcher = Directory.CreateDirectory(Path.Combine(Root, "conan exiles")).FullName;
        File.WriteAllText(Path.Combine(launcher, "Run Me!.bat"), "start \"\" \"Conan Exiles Enhanced\\ConanSandbox.exe\"");
        var clientRoot = Directory.CreateDirectory(Path.Combine(launcher, "Conan Exiles Enhanced")).FullName;
        File.WriteAllText(Path.Combine(clientRoot, "ConanSandbox.exe"), "fake-client");
        Directory.CreateDirectory(Path.Combine(clientRoot, "ConanSandbox", "Saved", "Config", "WindowsNoEditor"));
        if (modPaks is { Count: > 0 })
        {
            var modsDir = Directory.CreateDirectory(Path.Combine(clientRoot, "ConanSandbox", "Mods")).FullName;
            foreach (var pak in modPaks)
            {
                File.WriteAllText(Path.Combine(modsDir, pak), "client-pak");
            }

            File.WriteAllText(Path.Combine(modsDir, "modlist.txt"), string.Join(Environment.NewLine, modPaks));
        }

        return clientRoot;
    }

    public void WriteBackupMetadata(string id, DateTimeOffset createdAt, bool verified)
    {
        var dir = Directory.CreateDirectory(Path.Combine(Paths.BackupsDirectory, id)).FullName;
        var record = new BackupRecord
        {
            Id = id,
            DirectoryPath = dir,
            CreatedAt = createdAt,
            Succeeded = verified,
            ManifestWritten = true,
            HashesVerified = verified,
            SqliteVerified = verified,
            WorldType = "Enhanced",
            MainDbFileName = "game_0.db",
            VerificationDetail = verified ? "quick_check ok" : "quick_check failed",
            VerifiedAt = createdAt
        };
        File.WriteAllText(Path.Combine(dir, "metadata.json"), JsonSerializer.Serialize(record));
    }
}

internal sealed class CountingServer : IServerProcessManager
{
    public ServerRuntimeState State { get; } = new();

    public int StartCalls { get; private set; }

    public int StopCalls { get; private set; }

    public event EventHandler<ServerRuntimeState>? StateChanged
    {
        add { }
        remove { }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        StartCalls++;
        return Task.CompletedTask;
    }

    public Task StopAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        StopCalls++;
        return Task.CompletedTask;
    }

    public Task RestartAsync(CancellationToken cancellationToken = default)
    {
        StartCalls++;
        StopCalls++;
        return Task.CompletedTask;
    }

    public Task StartUnderLockAsync(IServerOperationLease lease, CancellationToken cancellationToken = default) => StartAsync(cancellationToken);

    public Task StopUnderLockAsync(IServerOperationLease lease, bool force = false, CancellationToken cancellationToken = default) =>
        StopAsync(force, cancellationToken);

    public bool IsConanServerProcess(string processName) => false;

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeNetworkInfo : INetworkInfoService
{
    public string? RadminIp { get; set; }

    public string? GetLanIPv4() => "192.168.0.10";

    public string? GetTailscaleIPv4() => null;

    public string? GetRadminVpnIPv4() => RadminIp;

    public string GetWebAdminUrl(string bindAddress, int port) => $"http://{bindAddress}:{port}/";
}

internal sealed class RecordingRcon : IRconService
{
    public List<string> Commands { get; } = new();

    public bool IsConnected => false;

    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DisconnectAsync() => Task.CompletedTask;

    public Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        Commands.Add(command);
        return Task.FromResult(string.Empty);
    }

    public Task AnnounceAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<PlayerInfo>> GetPlayersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PlayerInfo>>(Array.Empty<PlayerInfo>());
}
