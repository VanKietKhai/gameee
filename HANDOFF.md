# M3 — Task 3 Integration Diagnostics Handoff

Branch: `claude/m3-task3-diagnostics` (based on `cursor/m3-live-windows-integration-a853` @ `63691d5`).

## Baseline

**First run on this Windows host: 155 / 157, not 157 / 157.** It was investigated before any Task 3 code was changed.

- The machine had no .NET SDK, only runtimes. With the operator's approval, SDK **8.0.425** (the version `global.json` pins) was installed per-user via Microsoft's `dotnet-install.ps1` into `%LOCALAPPDATA%\Microsoft\dotnet`. No admin rights were needed and no system changes were made.
- Release build: 0 warnings / 0 errors.
- The 2 failures are **Windows-only test-harness issues, not product regressions**. Earlier tasks ran on a Linux agent:
  1. `M3BackupVerificationTests.Quick_check_error_is_invalid`: `SqliteTestDb.WriteValid` used a *pooled* SQLite connection. On Windows the pool keeps the file handle open, so the test's own `File.WriteAllBytes` hit a sharing violation. Fix: `Pooling = false` in the test helper. Production `SqliteBackupVerifier` already used `Pooling = false`.
  2. `QaWorkshopFileSafetyTests.Real_steamcmd_service_on_this_host_fails_before_launch_and_leaves_the_pak`: this test relied on `SteamCmdService` throwing "SteamCMD requires Windows" on a non-Windows host. Fix: the non-Windows assertions are kept **verbatim**. On Windows, a runner that never launches anything returns a failed exit, and the test still asserts `UserFacingException`, exactly one runner call, and an untouched live pak.
- After commit `bc7c2ed`: **157 / 157** on Windows. No test was deleted, skipped or weakened.

## Diagnostics Architecture

- `Core/Abstractions/IIntegrationDiagnosticsService` exposes `RunAsync`, `ExportAsync` and `ReportDirectory`.
- `Core/Diagnostics/` (pure, no I/O except formatting):
  - `DiagnosticModels.cs` holds `DiagnosticStatus` (Pass / Warning / Fail / NotConfigured / NotTested) and `DiagnosticEvidence`:
    - ConfigurationChecked
    - FilesystemInspected
    - RecordedResult
    - RuntimeObserved
    - NotExercised
    - LiveVerified, **reserved for Task 4 and never produced**

    It also holds `DiagnosticCheckResult` (Id, Category, Name, Status, Evidence, Summary, Details, SuggestedAction, Facts), `IntegrationDiagnosticsReport`, `LiveTestReadiness`, and the stable `DiagnosticCheckIds` / `DiagnosticCategories`.
  - `ConanExecutableClassifier` classifies by file name only (server, shipping, client, `Run Me!.bat`, script).
  - `LiveTestReadinessCalculator` computes the server-live and client-compatibility verdicts.
  - `DiagnosticReportRedactor` and `DiagnosticReportFormatter` handle redaction and JSON / Markdown output.
- `Infrastructure/Diagnostics/IntegrationDiagnosticsService` is split into partial files: core / System / SteamCMD / Backups, `.Server.cs` (server + client), `.Content.cs` (mods + world), and `.Network.cs` (ports + RCON). It is registered as a singleton.
- Every check is wrapped: an exception becomes `Fail` with a message, and the rest of the report still completes.
- **Read-only guarantees** (each one is covered by tests):
  - It does not depend on `IServerActionGate`.
  - It never calls Start/Stop.
  - It never executes SteamCMD.
  - It never opens the live SQLite database (only `FileInfo` size and mtime).
  - It never writes during `RunAsync`. Backup metadata is read directly, deliberately *not* via `BackupService.ListAsync`, which creates the backups folder.
- `ExportAsync` writes `integration-<yyyyMMdd-HHmmss>.json` and `.md` under `%DATA%\diagnostics` only. It uses `FileMode.CreateNew` and a `-2`, `-3`, … suffix, so it never overwrites or deletes.
- The WPF `DiagnosticsViewModel` (moved into its own file) only maps the report for display.

## SteamCMD Diagnostics

- `steamcmd.executable` reports PASS (FILES INSPECTED), "Configured but not live-tested (not executed)".
- A folder counts as *configured* only when it differs from the app default, because settings load always fills in the default.
- Outcomes:
  - Configured folder missing `steamcmd.exe`: **FAIL**.
  - Default folder with no `steamcmd.exe`: **NOT CONFIGURED**.
  - Zero-byte exe: FAIL.
  - Non-anonymous login: WARNING.
- Steam username and password appear only as `…Configured: YES/NO`.
- `steamcmd.live` is always NOT TESTED / NOT EXERCISED.
- **No SteamCMD command of any kind is run.** The architect's "version banner" check was not added: running `steamcmd.exe` triggers SteamCMD's self-update, and the assignment forbids executing it in Task 3.

## Dedicated Server Diagnostics

- `server.executable`:
  - Not configured: NOT CONFIGURED (shows the expected path).
  - `Run Me!.bat`: **FAIL**.
  - Client `ConanSandbox.exe` or `-Win64-Shipping`: **FAIL**.
  - Any `.bat` / `.cmd` / `.ps1` / `.lnk`: FAIL.
  - Inside the configured client root: FAIL.
  - Unexpected name: WARNING.
  - Valid name but file missing: WARNING ("not installed yet", which does not block a fresh install).
  - Found: PASS "not started by diagnostics".
- `server.working-directory`: empty means the exe folder is used (PASS). A missing folder is FAIL when the exe exists, otherwise WARNING.
- `server.install-directory`: checks `ConanSandbox\` and `ConanSandbox\Binaries\Win64`, and reads `steamapps\appmanifest_443030.acf` `buildid` / `StateFlags` / `installdir` as plain text. A folder holding the client `ConanSandbox.exe` without a server exe is **FAIL**.
- `server.workspace` (safe workspace) fails when:
  - the folder is a drive root,
  - it overlaps the client root,
  - it contains `Run Me!.bat`,
  - the app data dir sits inside the workspace, or
  - it is inside the SteamCMD folder.

  It warns when the workspace is under the app data dir, under Windows or Program Files (needs Administrator), or when the parent folder is missing.
- `server.save-location` resolves `ConanSandbox\Saved`.
- `server.live` is NOT TESTED. Even when Online, the detail says this "is not proof that a client can join".

## Standalone Client Diagnostics

- New setting `AppSettings.Client.RootDirectory` (`StandaloneClientSettings`). It is optional, editable in Settings with Browse, and **not hardcoded** anywhere. Existing settings JSON deserializes unchanged.
- `client.root`:
  - Not configured: NOT CONFIGURED, "optional; not required for server tests".
  - Pointing at the launcher folder (contains `Run Me!.bat`, no `ConanSandbox.exe`): WARNING with `SuggestedRoot` set to the subfolder that has the exe.
  - Inside the server install: FAIL.
- `client.executable` checks `ConanSandbox.exe` at the root, then `ConanSandbox\Binaries\Win64`. It reports PASS "not launched". A folder containing the dedicated server is FAIL.
- `client.mods`: client `ConanSandbox\Mods` and `modlist.txt` (entries, missing / zero-byte paks). Problems are reported as WARNING with a manual-copy hint.
- `client.config`: `ConanSandbox\Saved\Config\*` (read-only).
- `client.mod-parity`: reports enabled server mods that are missing from the client modlist as WARNING, worded "Client Workshop sync is not automatic".
- `client.join-live`: NOT TESTED.
- Client mod state is a separate category from server mods.
- **Verified on this machine (read-only)** against `D:\conan exiles\Conan Exiles Enhanced`:
  - root PASS, exe PASS, Saved/Config PASS
  - "no ConanSandbox\Mods" WARNING (no local client mods)
  - pointing the server exe at `Run Me!.bat` or the client `ConanSandbox.exe` produces FAIL in both cases
  - a 37-entry before/after snapshot of the client tree (top level, `ConanSandbox`, `Mods`, `Saved\Config`) showed **0 differences**

## Mod Diagnostics

Server checks are resolved from `ServerInstallDirectory ?? ServerWorkingDirectory`, the same resolution `WorkshopModService` and `BackupService` use.

- `mods.directory`: exists. Enabled mods without the folder is FAIL.
- `mods.modlist`: present. Enabled mods without `modlist.txt` is FAIL. **The file is never modified** (byte-compared in a test).
- `mods.pak-files`: every `modlist.txt` line, plus every enabled catalog `LocalFileName`. Missing paks are **FAIL**, and zero-byte paks are **FAIL**. Lines may be relative, absolute or `*`-prefixed.
- `mods.ordering`: compares against `ModListGenerator.Generate(catalog)`. An exact match is PASS. Same mods in a different order is WARNING. Extra or missing entries are WARNING with lists.
- `mods.duplicates`: duplicate Workshop IDs, one pak claimed by several IDs, and duplicate modlist lines are all WARNING.
- `mods.workshop-live`: NOT TESTED. "A .pak existing does not mean Conan loaded it."

## World Diagnostics

Uses `ConanWorldFiles.Present / DetectWorldType / MainDatabaseFileName`.

| State | Result |
| --- | --- |
| Enhanced `game_0.db` | PASS |
| Legacy `game.db` | PASS |
| Both worlds | WARNING (backups use Enhanced) |
| No known world | WARNING (fresh server creates one; not a blocker) |
| WAL/SHM without the main DB | **FAIL** |
| Zero-byte main DB | WARNING |

Facts recorded: `WalPresent`, `ShmPresent`, main DB size and mtime, and `LiveDbOpened: NO`. No repair or `quick_check` is run on the live DB.

`backups.last-verified` reads `metadata.json` from Task 2 backups. A verified backup requires `Succeeded && HashesVerified && SqliteVerified`.

| State | Result |
| --- | --- |
| Latest backup verified | PASS (FROM RECORD) |
| Latest unverified, but an older verified backup exists | WARNING |
| No backup has ever verified | FAIL |
| No backups | NOT TESTED |

## Network/RCON Diagnostics

- `network.ports` checks Game, Game+1, Query, RCON and Web Admin:
  - Ports must be in range 1–65535 (FAIL otherwise).
  - Query colliding with game or game+1 is FAIL.
  - RCON equal to the Web Admin TCP port is FAIL.
  - Privileged ports and reused port numbers are WARNING.
  - The summary is prefixed **"CONFIG VALID"** and adds "Firewall/router reachability not tested".
- `network.runtime`:
  - Server offline: **NOT TESTED**, "RUNTIME NOT TESTED … This is not a configuration failure".
  - Server Online: a read-only UDP listener check (the same helper the readiness probe uses), reported as RUNTIME OBSERVED with "does not prove external players can join".
- `rcon.configuration`: enabled, port and timeout.
- `rcon.password`: shows only `Password configured: YES/NO`.
- `rcon.runtime`: only when Online **and** a password is set, it sends one `listplayers` through the existing `IRconService`, with a timeout. Otherwise it is NOT TESTED.

## Report Redaction

Two layers:

1. **Checks never include secret values.** Secrets appear only as YES/NO flags.
2. **`DiagnosticReportRedactor`** runs over every displayed and exported report, then a final pass runs over the serialized JSON and Markdown text. It removes:
   - `RconPassword`, `ServerPassword`, `AdminPassword`, `SteamPassword`
   - `WebAdminPasswordHash`
   - the Steam username
   - the raw protected `secrets.bin` payload
   - any `*password* / *secret* / *token* / *session* / *cookie* / *credential* = value` pair, such as a launch argument `-ServerPassword=x`

   Short secrets (under 4 characters) are replaced only as whole tokens. Benign `…Configured: YES/NO` flags are kept.

Paths stay visible for troubleshooting.

## Live Test Readiness

`LiveTestReadinessCalculator` produces two verdicts, both based on configuration checks only. The notes always say nothing was live verified.

- **READY / NOT READY FOR SERVER LIVE TEST**
  - These checks must be PASS or WARNING: `steamcmd.executable`, `server.workspace`, `app.backup-root`.
  - These must not FAIL: app data, settings, server exe, working dir, install dir, ports, RCON config.
  - A not-yet-installed server in a safe workspace **is ready**: the note says the live test will install it.
  - The standalone client is **never** required.
- **READY FOR CLIENT COMPATIBILITY TEST / CLIENT COMPATIBILITY TEST NOT POSSIBLE YET**
  - Requires server readiness, `client.executable` PASS, and `client.mods` not FAIL.
  - Notes say the client is optional and that client Workshop sync is not automatic.

The current state of this machine, with an isolated data dir and the real client root, is **NOT READY FOR SERVER LIVE TEST**. The single blocker is accurate: SteamCMD is not installed yet.

## Diagnostics UI

The Diagnostics page shows:

- **RUN INTEGRATION CHECK** (also runs automatically when the page is first opened), Export report, Open report folder, and the existing Install SteamCMD button.
- Per-category cards with a status badge (green, amber, red, or grey for NOT CONFIGURED / NOT TESTED). An **evidence label** sits under every badge, for example "CONFIG CHECKED", "FILES INSPECTED" or "NOT EXERCISED", so a green PASS never implies a live test.
- The two readiness cards at the bottom, plus a collapsed Environment section (the previous snapshot: Web Admin URL, Tailscale and so on).

It uses only the existing theme brushes and button styles.

To verify the page, the app was launched against an isolated `CONAN_SERVER_CONTROL_DATA`, the Diagnostics nav button was clicked via UI Automation, and the window was captured. It renders correctly against the configured paths.

## Files Changed

- `src/ConanServerControl.Core/Abstractions/IIntegrationDiagnosticsService.cs` (new)
- `src/ConanServerControl.Core/Diagnostics/DiagnosticModels.cs` (new)
- `src/ConanServerControl.Core/Diagnostics/ConanExecutableClassifier.cs` (new)
- `src/ConanServerControl.Core/Diagnostics/LiveTestReadinessCalculator.cs` (new)
- `src/ConanServerControl.Core/Diagnostics/DiagnosticReportRedactor.cs` (new)
- `src/ConanServerControl.Core/Diagnostics/DiagnosticReportFormatter.cs` (new)
- `src/ConanServerControl.Core/Settings/AppSettings.cs` (`StandaloneClientSettings`)
- `src/ConanServerControl.Infrastructure/Diagnostics/IntegrationDiagnosticsService*.cs` (new, 4 partial files)
- `src/ConanServerControl.Infrastructure/ServiceCollectionExtensions.cs` (registration)
- `src/ConanServerControl.App/ViewModels/DiagnosticsViewModel.cs` (new; old class removed from `SettingsViewModel.cs`)
- `src/ConanServerControl.App/ViewModels/SettingsViewModel.cs` (client root field / browse / save / load)
- `src/ConanServerControl.App/Views/DiagnosticsView.xaml`, `SettingsView.xaml`
- `tests/ConanServerControl.Tests/M3IntegrationDiagnosticsTests.cs` (new)
- `tests/ConanServerControl.Tests/QaTestSupport.cs`, `QaWorkshopFileSafetyTests.cs` (Windows portability, see Baseline)
- `STATUS.md`, `HANDOFF.md`

## Tests Added

`M3IntegrationDiagnosticsTests`: 37 test methods, 46 test cases. They cover:

- **SteamCMD:** configured valid (and not executed); missing in a configured folder gives FAIL; unconfigured gives NOT CONFIGURED.
- **Dedicated server:** valid exe (not started); client exe as server gives FAIL; `Run Me!.bat` as server gives FAIL; install dir holding the client gives FAIL; workspace overlapping the client gives FAIL; a not-yet-installed server is ready; classifier theory (7 cases).
- **Standalone client:** valid root (read-only); launcher-folder suggestion; client absent does not block server readiness; client missing server mods means manual copy, not sync.
- **World:** Enhanced detection (live DB not opened); Legacy detection; WAL without main DB gives FAIL; no world gives WARNING and is not a blocker.
- **Mods:** modlist matches state; different order gives WARNING and the file is byte-identical afterwards; missing pak gives FAIL; zero-byte pak gives FAIL; duplicate lines give WARNING.
- **Network / RCON:** valid ports are CONFIG VALID and offline runtime is NOT TESTED with no probe; invalid ports (4 cases) give FAIL; Online uses read-only probes only; RCON password status never exposes the password; missing password is a WARNING, not a blocker.
- **Redaction:** exported JSON / MD and the in-memory report contain none of 6 known secrets nor the protected blob; redactor unit test covers key=value, quoted and short-token cases.
- **Read-only proof:** a full temp-tree snapshot is unchanged after `RunAsync`; export writes only under `diagnostics\`; a second export never overwrites; 0 SteamCMD runner calls, 0 Start/Stop calls, 0 RCON commands.
- **No live claims:** no check carries `LiveVerified`, and all four live items are NOT TESTED / NOT EXERCISED.
- **Backups:** last verified backup read from metadata; latest unverified gives WARNING and reports the last verified.
- **Readiness:** server-live calculation; client-compatibility calculation; end-to-end server and client ready.

## Build

`dotnet build -c Release` (also `--no-incremental`):
PASS
0 warnings
0 errors

## Tests

`dotnet test -c Release`:
**203 / 203** passed, 0 failed, 0 skipped.

(157 previous tests, all green, plus 46 new test cases.)

## Known Limitations

- Nothing is live verified. Diagnostics answer "is this machine configured safely for Task 4", not "does Conan work".
- The runtime UDP check sees only a local listener; it says nothing about firewall, NAT or router reachability.
- No SteamCMD version or `app_info` query (see SteamCMD section). `appmanifest_443030.acf` is read as text when present.
- Disk space below the thresholds (10 GB install, 2 GB backups/data) is a WARNING only and is not part of readiness.
- Client `modlist.txt` entries are resolved relative to the client `ConanSandbox\Mods`. Non-Steam client mod layouts beyond that are not inferred.
- **Not done in this task** (listed by the architect for Task 3, not in this assignment's scope): QA-017 `UPDATE EVERYTHING` label rename, the Dashboard `STARTING` label, and the Backups "Verified" badge (AC3-10).
- Pre-existing, not changed: the Settings "New Web Admin password" field is a plain `TextBox`, so the password is visible while typing. This should become a `PasswordBox` in a follow-up.
- Dev/run environment: this host has no system-wide .NET SDK and no system-wide ASP.NET Core runtime (the App hosts the Web project). Both exist only in the per-user `%LOCALAPPDATA%\Microsoft\dotnet` install, so running the built App requires `DOTNET_ROOT` to point there, or a system-wide .NET 8 Desktop + ASP.NET Core runtime.

## Deferred to Task 4

- real SteamCMD execution and `app_update 443030`
- real Conan dedicated server boot, readiness and attach-to-existing (attach still marks Online without the probe)
- real Workshop download, real `.pak` load
- standalone client join and mod compatibility
- the `LiveWindows` guarded harness (LV-01…LV-14). `DiagnosticEvidence.LiveVerified` is reserved for its observations.
