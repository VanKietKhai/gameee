# M3 — Task 1 Foundation Handoff

## Base Revision

`b422e1fbaf11dc0f9dfd3e977e24298a9307da64` on `cursor/conan-server-control-a853` (stabilization CLOSED, 108/108).

New branch: `cursor/m3-live-windows-integration-a853`

`architect/review-2`, QA branches, and unrelated `main` changes were **not** merged.

## RCON Settings

The Settings UI and dashboard now read/write `Rcon.Port`. `Server.RconPort` is `[Obsolete]` and used only for JSON deserialization.

`JsonSettingsService.LoadAsync` migrates once: if `Rcon.Port` is still the default (25575) and legacy `Server.RconPort` differs, copy it and persist. An explicit `Rcon.Port` is never overwritten. Repeated loads are stable.

`RconService` already used `Rcon.Port`; no dual-field runtime consumers remain.

## Secret Storage

Settings has a write-only RCON PasswordBox, a "Password configured" / "Not configured" indicator, and an explicit **Clear RCON password** action (blank save does not erase).

The password is stored only through `ISecretProtector` (`DpapiSecretProtector` on Windows). It is not serialized into `settings.json`, not loaded into the PasswordBox, and not logged.

## Process Cancellation

Once `ProcessRunner` has started a child:

- normal exit → no Kill
- timeout → `Kill(entireProcessTree: true)`, result is TimedOut (not success)
- caller `CancellationToken` cancelled after start → kill tree, then rethrow cancellation
- cancellation before start → no Start, no Kill
- Kill throwing is logged; the operation is not converted to success

Only the process instance started by that invocation is killed.

## QA-013

`BackupNowCoreAsync` used a tautological `dest == dest` check. Destinations now must satisfy `PathValidator.IsUnderRoot(dest, BackupsDirectory)` or the backup fails closed. Traversal, absolute paths outside the root, and prefix-confusion (`Backup` vs `Backup-Evil`) are rejected. No naive `StartsWith`.

## Conan World Files

`ConanWorldFiles` (Core) lists Enhanced `game_0.db` / `-wal` / `-shm` and legacy `game.db` / `-wal` / `-shm`. `Present(savedDir)` returns only those names that exist as files in that directory. Unrelated `.db` files and nested copies are ignored. No world files are deleted or opened. `GameDbRelative` remains as a documented legacy constant.

## Files Changed

- `src/ConanServerControl.Core/Settings/AppSettings.cs`
- `src/ConanServerControl.Core/AppConstants.cs`
- `src/ConanServerControl.Core/Backups/ConanWorldFiles.cs`
- `src/ConanServerControl.Infrastructure/Settings/JsonSettingsService.cs`
- `src/ConanServerControl.Infrastructure/ProcessManagement/ProcessRunner.cs`
- `src/ConanServerControl.Infrastructure/Backups/BackupService.cs`
- `src/ConanServerControl.App/ViewModels/SettingsViewModel.cs`
- `src/ConanServerControl.App/ViewModels/DashboardViewModel.cs`
- `src/ConanServerControl.App/Views/SettingsView.xaml`
- `src/ConanServerControl.App/Views/SettingsView.xaml.cs`
- `tests/ConanServerControl.Tests/M3RconSettingsTests.cs`
- `tests/ConanServerControl.Tests/M3ProcessRunnerTests.cs`
- `tests/ConanServerControl.Tests/M3BackupPathTests.cs`
- `tests/ConanServerControl.Tests/M3ConanWorldFilesTests.cs`
- `STATUS.md`, `HANDOFF.md`

## Tests Added

RCON migration (legacy copies, explicit wins, stable reload). RCON secret (not in settings JSON, goes through `ISecretProtector`, not logged). ProcessRunner (cancel-after-start kills tree within 2 s, timeout kills, normal exit does not, pre-start cancel does not start, kill-throw is not success). QA-013 (child allowed; traversal / absolute / prefix-confusion rejected). ConanWorldFiles (Enhanced only; Enhanced+wal+shm; legacy; empty; unrelated ignored).

No existing tests were deleted, skipped, or weakened.

## Build

PASS
0 warnings
0 errors

## Tests

130 passed
0 failed
130 total

## Architect Acceptance Criteria Covered

- **AC3-1** (Task 1 portion): 0 warnings; all 108 pre-existing tests pass; new unit tests added
- **AC3-5**: caller cancel after start observes `Kill(true)` within 2 s
- **AC3-6**: `Server.RconPort=25580` + default `Rcon.Port` loads as `Rcon.Port=25580`; password stored only via `ISecretProtector` and is absent from settings JSON (integration-report redaction is Task 3)
- **AC3-7** (QA-013): destination outside `BackupsDirectory` throws
- World-file support (Task 1 portion of AC3-4): `ConanWorldFiles.Present` only; hashes / `quick_check` are Task 2

## Deferred To Task 2

- `IServerReadinessProbe`
- QA-011 (Starting until ready)
- Stop → Backup pipeline reorder
- `BackupRecord` world hashes
- SQLite `PRAGMA quick_check`
- `IBackupVerifier`

Also still deferred: Integration Diagnostics UI, LiveWindows harness, Delayed Restart, Wait Until Empty, Web Admin expansion, `app_info_print`, Update Available / Verify-All split.
