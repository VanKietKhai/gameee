# Conan Server Control — QA Report

Independent verification of branch `cursor/conan-server-control-a853` (PR #1, commit `593d9d6`) plus the architect review of that PR. Production code was not modified. Regression tests live in `tests/ConanServerControl.Tests/Qa*.cs` and are marked `[Trait("Category", "QA-KnownFailure")]` when they assert required behavior the product does not meet yet.

Run date: 2026-10-01. Host: Linux, .NET SDK 8.0.425. All file fixtures used `/tmp/csc-qa` or `/tmp/csc-tests`. `D:\ConanServer` and `D:\ConanBackups` were not opened.

## Test Scope

- Action gate: `StartAsync` / `StopAsync` / `RestartAsync`, `StartUnderLockAsync` / `StopUnderLockAsync`, update pipeline, Web action endpoints, desktop command bindings (source review; WPF was not clicked).
- Restore safety: real `BackupService` on temp directories, default retention (keep latest 10, keep 14 days), an old backup plus 10 newer ones.
- Workshop file safety: real `WorkshopModService` with a scripted SteamCMD stand-in, temp install trees, and the real `SteamCmdService` only as far as this Linux host will go (it refuses to launch `steamcmd.exe`).
- Update Server, Update Mods, Update Everything, Check Updates, Enable/Disable/Move, duplicate Workshop IDs.
- Web Admin process: real Kestrel host from `WebHostFactory`, cookie login, unauthenticated and authenticated POSTs.
- Not in scope and not executed: a Windows desktop session, a real SteamCMD binary, a real Conan dedicated server, a real Workshop item, RCON against a live game, or any operator backup directory.

## Overall Status

**FAIL**

P0 QA-001 is reproduced: restoring an old backup can delete that backup and still log success while the live world is unchanged. Several P1 paths are also reproduced (mod replace, offline server started by Update Server/Update Mods, mods-page updates bypassing the gate, no rollback when Update Everything fails part-way, empty backup reported as success).

The nested action-gate deadlock described in the handoff is **not** reproduced. Public Start/Stop/Restart/Update calls reject a second destructive operation while a lease is held, and an exception releases the lease.

## Build

| Project | Configuration | Result |
| --- | --- | --- |
| `ConanServerControl.Core` | Release, `net8.0` | Succeeded |
| `ConanServerControl.Infrastructure` | Release, `net8.0` | Succeeded |
| `ConanServerControl.Web` | Release, `net8.0` | Succeeded |
| `ConanServerControl.Tests` | Release, `net8.0` | Succeeded |
| `ConanServerControl.App` (WPF) | Release, `net8.0-windows`, `EnableWindowsTargeting=true` | Succeeded, 0 warnings, 0 errors |

The WPF project compiled to `src/ConanServerControl.App/bin/Release/net8.0-windows/ConanServerControl.dll`. It was not started. This host cannot run a Windows desktop UI.

## Tests

Command:

```text
dotnet test tests/ConanServerControl.Tests/ConanServerControl.Tests.csproj -c Release
```

| Suite | Passed | Failed | Skipped | Total |
| --- | ---: | ---: | ---: | ---: |
| Full suite | 62 | 20 | 0 | 82 |
| Excluding `Category=QA-KnownFailure` | 62 | 0 | 0 | 62 |

The 20 failures are the new tests that encode the required behavior. Each carries `Category=QA-KnownFailure` and an `Issue=QA-0xx` trait. The previous builder run of 29 passed tests is inside the 62 passing tests. Those 29 do not cover restore retention, pak bytes, or the offline-start paths. `ServerUpdatePipelineTests` uses `FakeWorkshop` and turns the pre-update backup off, so a green result there does not prove the mod pipeline or a real backup.

## Verified Working

These passed against real types (real gate, real process manager with a fake process, real `WorkshopModService` / `BackupService` / `SteamWorkshopClient` parser) or against the code path itself. They are not live SteamCMD or live Conan results.

- Update Server, Update Mods, and Update Everything **while the server is already online** take one lease, one backup, one `StopUnderLock`, one SteamCMD server update and/or one `UpdateAll`, then one `StartUnderLock`, and finish Online. No nested `TryBegin`.
- Real `ServerProcessManager`: Update Server while a fake process is online reaches SteamCMD only after status is Offline, then returns to Online, and the gate is free afterward. (`Real_process_manager_update_while_online_does_not_deadlock_on_the_gate`)
- A second Restart and a second Stop while that update holds the lease throw `UserFacingException` (“already running”). The update then completes.
- An exception thrown while the lease is held releases it. A later Update Server succeeds.
- `StartUnderLockAsync` with **no** lease held throws `InvalidOperationException`.
- Restore of a **recent** backup (inside the 14-day / latest-10 window) copies the world marker and leaves the source directory in place.
- Restore while status is Online throws before copying.
- Workshop failure paths that do **not** replace the installed pak: SteamCMD exception, non-zero exit (fake throws the way `SteamCmdService` does), exit 0 with an empty staging directory, unknown Workshop ID, a failed `AddAsync` of a different ID, and the real `SteamCmdService` on Linux (throws “SteamCMD requires Windows” before `IProcessRunner.RunAsync`; runner call count stays 0).
- A missing staging directory throws `DirectoryNotFoundException` and leaves the installed pak bytes and `modlist.txt` unchanged.
- Check Updates with a normal Steam payload sets `UpdateAvailable` and does not download, does not change pak bytes, and does not rewrite `modlist.txt`. An HTTP 500 from Steam throws and leaves the name and the pak unchanged. `ServerUpdateService.CheckAsync` does not call start, stop, or SteamCMD.
- Enable/Disable and Move write `modlist.txt` in load order and omit disabled mods. A duplicate Workshop ID is rejected before download. Moving the first mod onto its current index keeps order. `ModListGenerator` strips `../../` from a stored file name on this OS.
- `UpdateAllAsync` downloads every **enabled** mod, including mods whose `UpdateAvailable` is false, and skips disabled mods. This is current behavior, not filed as a naming defect. See Update All semantics below.
- Unauthenticated `POST /api/server/update-mods` does not return HTTP 200 `{"ok":true}`. The action does not run. It returns **302** to `/login.html` (see QA-010).
- Authenticated `POST /api/server/update-mods` without a CSRF header is accepted by the auth cookie and enters the real update service (see QA-010).

## P0 Issues

### QA-001

- **Severity:** P0
- **Retest:** RESOLVED
- **Component:** `BackupService.RestoreAsync` + `BackupRetentionPolicy`
- **Description:** Restoring backup A creates a safety backup first. That safety backup runs retention before any bytes are copied from A. With the shipping defaults (keep latest 10 and 14 days), an A that is older than 14 days and outside the newest 10 is deleted. The copy then no-ops because the source directory is gone, and the activity log still says the restore succeeded. The live world is left as it was. The historical backup the admin tried to recover is gone.
- **Architect item 1:** CONFIRMED
- **Steps to Reproduce:**
  1. Temp data directory. Install folder contains `ConanSandbox/Saved/marker.txt` = `CURRENT`.
  2. Settings: `KeepLatest = Ten`, `KeepDays = 14`. Server status Offline.
  3. Write backup `2026-08-01_000001` with `CreatedAt` 40 days ago and `world/marker.txt` = `FROM-A`.
  4. Write 10 newer backup folders dated within the last day.
  5. Call `RestoreAsync("2026-08-01_000001", startAfter: false)`.
- **Expected:** A remains on disk for the whole restore. The live marker becomes `FROM-A`. If A cannot be read, the method throws and does not log success.
- **Actual:** No exception. `sourceSurvived=False`. Live marker stays `CURRENT`. Activity log: `Backup completed (2026-10-01_150056).` then `Restored backup 2026-08-01_000001.`
- **Evidence:** `QaRestoreSafetyTests.Restore_must_not_delete_the_source_backup_or_report_success_when_it_is_gone`. Control test `Restore_of_a_recent_backup_copies_world_and_keeps_the_source` passes, so the fixture format can restore when retention does not delete the source. Code: `BackupService.RestoreAsync` calls `BackupNowAsync("pre-restore")` at `BackupService.cs:124` before the copy at lines 136–144, and logs success at line 146 with no check that the source still exists or that any file was copied. `BackupNowAsync` calls `ApplyRetentionAsync` at line 93. `BackupRetentionPolicy.SelectForDeletion` (`BackupRetentionPolicy.cs:20-36`) keeps only the newest `(int)KeepLatest` **and** backups inside `KeepDays`. The new safety backup occupies a “newest” slot, which pushes an old A out of both windows. Desktop UI then shows success: `SettingsViewModel.cs` restore command alerts “Backup restored” when `RestoreAsync` does not throw.
- **Suggested Fix Direction:** Exclude the selected backup id from the retention pass that runs inside the pre-restore safety backup. After that pass, if the source directory is missing, throw and do not log “Restored”. Log success only after the world copy actually wrote the source files. Do not delete the source folder as part of restore.

Related, not separately reproduced: the safety-backup folder name is `DateTime.Now` formatted as `yyyy-MM-dd_HHmmss` (`BackupService.cs:37`). A restore whose id equals that same second would write the safety copy into the source folder. Clock-sensitive; not executed.

## P1 Issues

### QA-002

- **Severity:** P1
- **Retest:** RESOLVED
- **Component:** `WorkshopModService.DownloadAndStageAsync` / `UpdateAllAsync`
- **Description:** Replacement is stage-then-replace for a **single** chosen `.pak`, and the staging folder is not cleared at the start of the attempt. There is no size or “this download created this file” check. The installed file is not deleted before the download. Failure paths that throw before `File.Replace` leave the installed pak in place. A SteamCMD exit 0 that does not write a new pak still treats any pre-existing `*.pak` in that mod’s staging directory as the download and replaces the live file.
- **Architect item 2:** CONFIRMED unsafe. The delete-then-download pattern is NOT CONFIRMED.
- **Steps to Reproduce:**
  1. Installed `ConanSandbox/Mods/Working.pak` bytes `WORKING-MOD`, `modlist.txt` = `Working.pak`.
  2. Staging `staging/workshop/111/Working.pak` bytes `STALE-PARTIAL`. Scripted SteamCMD returns exit 0 and writes nothing new.
  3. `UpdateAsync(111)`.
  4. Separate cases: write a 0-byte `Working.pak` during the download; write both `Alpha.pak` and `Beta.pak`; `UpdateAll` where mod 1 writes `MOD1-NEW` and mod 2 throws.
- **Expected:** A download that does not produce a validated new pak leaves `WORKING-MOD` as the live file. A multi-pak download either installs every pak or fails without changing `modlist.txt`. `UpdateAll` does not keep an earlier mod replaced when a later mod fails.
- **Actual:**
  - Stale staging: no exception, live bytes `STALE-PARTIAL`, `Working.pak.bak` = `WORKING-MOD`.
  - 0-byte download: no exception, live length 0.
  - Two paks: no exception, `modlist.txt` becomes `Alpha.pak` only, disk has `Alpha.pak` and the old `Working.pak`. `Beta.pak` is not copied. `Directory.EnumerateFiles(...).FirstOrDefault()` picks one file.
  - `UpdateAll`: throws on mod 2, mod 1 is `MOD1-NEW`, mod 2 stays `MOD2-OLD`.
- **Evidence:** `Exit_zero_without_a_new_pak_must_not_replace_the_installed_mod_with_staged_leftovers`, `Zero_byte_pak_must_not_replace_a_working_mod`, `Multiple_downloaded_paks_must_not_leave_a_single_arbitrary_file_as_the_mod`, `UpdateAll_failure_on_a_later_mod_must_leave_earlier_mods_unchanged`. Code: `WorkshopModService.cs:257-289`. The `ProcessExecutionResult` from `DownloadWorkshopItemAsync` is ignored; safety on a non-zero exit depends on `SteamCmdService` throwing (`SteamCmdService.cs:230-235`), which the fake mirrors. Passing tests listed under Verified Working cover exception, non-zero, empty staging, unknown id, and the Linux `SteamCmdService` guard.
- **Suggested Fix Direction:** Create a fresh staging directory per attempt (do not reuse leftovers). Reject a 0-byte pak and any pak whose timestamp is not newer than the start of this call. If more than one pak is present, copy all of them or fail before touching `Mods`. Replace only after those checks. On `UpdateAll`, download every mod to staging first and replace only if every download succeeded. `File.Replace` overwrites an existing `.bak`; do not treat `.bak` as a durable second copy across a later failed attempt.

Invalid Workshop IDs are `long` values. `WorkshopIdValidator` rejects command and path fragments (`QaWorkshopFileSafetyTests` theory, passing). Staging path recorded for id 111 is `staging/workshop/111`.

### QA-003

- **Severity:** P1
- **Retest:** RESOLVED
- **Component:** Dashboard, Web Admin, `ServerUpdateService.RunLockedAsync`
- **Description:** Update Server and Update Mods always pass `restartAfter: true`. The pipeline starts the server when `restartAfter || wasRunning`. A server that was Offline is Online after a successful update. The buttons are labeled UPDATE SERVER and UPDATE MODS, not restart.
- **Architect item 4:** CONFIRMED for Update Server and Update Mods.
- **Steps to Reproduce:** Set status Offline. Call `UpdateAsync(true)` and `UpdateModsAsync(true)` the way `DashboardViewModel.cs:116-119` and `WebAdminExtensions.cs:231-236` do.
- **Expected:** Offline before the update stays Offline unless a separate “start afterwards” choice is set.
- **Actual:** Both calls finish with status Online and a single `start-under-lock`. No exception.
- **Evidence:** `Shipped_update_server_call_starts_a_server_that_was_offline`, `Shipped_update_mods_call_starts_a_server_that_was_offline`. Web host, authenticated `POST /api/server/update-mods` with no install path, body: `Update mods failed` / `Configured path: (not set)`. That is the start step running on an offline server (`QaWebAdminTests.Authenticated_mutation_without_the_csrf_header_is_rejected`).
- **Suggested Fix Direction:** Pass `restartAfter: false` from Update Server and Update Mods until a real “start afterwards” control exists. Keep `if (wasRunning)` so a server that was online comes back. Do this together with QA-004 or the false path reports failure after the files are already updated.

Update Everything is separate: `UpdateEverythingAsync` hardcodes `restartAfter: true` (`ServerUpdateService.cs:89-90`). While offline it also starts (`Update_everything_while_offline_stays_offline`: status Online, calls `[start-under-lock]`). The desktop button is labeled `UPDATE EVERYTHING & RESTART` (`DashboardView.xaml:58`), so that start matches the label. Web Admin has no Update Everything endpoint. Architect item 4 for Update Everything: PARTIALLY CONFIRMED (behavior is real; the labeled button is an explicit restart). Do not remove that start unless the label changes.

### QA-004

- **Severity:** P1
- **Retest:** RESOLVED
- **Component:** `UpdatePipelineStateMachine`
- **Description:** `Validating -> Completed` is not an allowed transition. Any update that does not start the server (offline and `restartAfter: false`) applies the file work, then throws `Illegal update pipeline transition: Validating -> Completed.` The user sees a failure after the mods or server files were already updated. This blocks the correct fix for QA-003.
- **Architect item 4:** CONFIRMED as the reason the stay-offline API cannot succeed.
- **Steps to Reproduce:** Status Offline. `UpdateModsAsync(false)` or `UpdateAsync(false)`. Or drive the state machine Checking → UpdatingMods → Validating → Completed.
- **Expected:** The pipeline reaches Completed, status stays Offline, and start is not called.
- **Actual:** `UserFacingException: Illegal update pipeline transition: Validating -> Completed.` Status stays Offline. `start-under-lock` is not called. The throw is after `UpdateAllAsync` / SteamCMD (`ServerUpdateService.cs:156-170`).
- **Evidence:** `Update_mods_while_offline_stays_offline_when_start_afterwards_is_not_requested`, `Update_server_while_offline_stays_offline_when_start_afterwards_is_not_requested`, `Pipeline_can_complete_after_validation_when_the_server_is_not_started` (`UpdatePipelineStateMachine.cs:86` and the switch at lines 151-154, which allows `Validating -> Starting` only).
- **Suggested Fix Direction:** Allow `Validating -> Completed`. That is the success edge when the server is not started.

### QA-005

- **Severity:** P1
- **Retest:** RESOLVED
- **Component:** Mods page `UpdateSelectedAsync` / `UpdateAllAsync`
- **Description:** The Mods page calls `IWorkshopModService.UpdateAsync` / `UpdateAllAsync` directly. Those methods do not take `IServerActionGate`, do not back up, and do not stop the server. Dashboard Update Mods uses `IServerUpdateService.UpdateModsAsync`, which does. A probe `TryBegin` during Update Selected succeeds, so the download runs with the gate free and can overlap a dashboard or Web update.
- **Architect item 3 (mods bypass):** CONFIRMED
- **Steps to Reproduce:** During `WorkshopModService.UpdateAsync`, call `IServerActionGate.TryBegin`.
- **Expected:** The update holds the same lease as Update Mods, stops the server when it is running, and backs up first.
- **Actual:** `TryBegin` succeeds during the download. `Update_selected_must_hold_the_action_gate_while_it_downloads` fails with “replaced files without holding IServerActionGate.”
- **Evidence:** `SettingsViewModel.cs:462-495`. Contrast with `DashboardViewModel.cs:119`.
- **Suggested Fix Direction:** Point Update Selected and Update All at the same `RunLockedAsync` path as Update Mods (single id for selected), or take the gate inside `DownloadAndStageAsync` and stop when the server is online. Smallest change is the view-model call site plus a service method that already holds the lease.

### QA-006

- **Severity:** P1
- **Retest:** RESOLVED for the original download-failure steps. Live-commit rollback is QA-016.
- **Component:** `ServerUpdateService.UpdateEverythingAsync`
- **Description:** On the success path there is one lock, one backup, one stop, one server update, one mod pass, one start. On failure after the server update and after the first mod replace, there is no rollback. The catch block does not start the server again and the error text does not say the server was left stopped.
- **Architect item 5:** CONFIRMED for the failure outcome. The success path is one cycle (passing tests). Rollback is absent.
- **Steps to Reproduce:** Status Online. Two enabled mods. SteamCMD server update returns 0. Workshop id 1 writes `MOD1-NEW` over `Mod1.pak`. Workshop id 2 throws. Call `UpdateEverythingAsync`.
- **Expected:** Previous online state restored, or a clear “server was stopped and not restarted,” and neither mod left half-applied.
- **Actual:** `UserFacingException: mod download failed`. `toldStopped=False`. Status Offline. Calls `[stop-under-lock]` (no start). Backups `1 [pre-update-everything]`. Steam events `server-update, workshop:1, workshop:2`. `mod1=MOD1-NEW`, `mod2=MOD2-OLD`.
- **Evidence:** `Server_update_success_plus_mod_update_failure_must_not_leave_a_running_server_down_or_a_partial_mod_set`. Success-path counts: `Update_everything_while_online_is_a_single_cycle`.
- **Suggested Fix Direction:** Download every mod into staging before any `File.Replace`. In the catch, say that the server was stopped and was not restarted. Do not claim saves were untouched if a pak was already replaced.

### QA-007

- **Severity:** P1
- **Retest:** RESOLVED
- **Component:** `BackupService.BackupNowAsync`
- **Description:** If the install directory exists but `ConanSandbox/Saved` does not, the backup still creates a folder, writes metadata with `IncludesWorld = false`, and logs `Backup completed`. The update pipeline treats that as a successful pre-update backup and continues. The dashboard alerts “Backup created” whenever the method returns.
- **Steps to Reproduce:** Set the install directory to an empty temp folder. Call `BackupNowAsync("pre-server-update")`.
- **Expected:** The method throws. The activity log does not say the backup completed.
- **Actual:** No exception. Activity: `Backup completed (2026-10-01_150056).`
- **Evidence:** `Backup_that_copies_no_world_must_not_be_reported_as_success`. `BackupService.cs:60-90`.
- **Suggested Fix Direction:** If the world directory was not copied, throw `UserFacingException` and do not write the “Backup completed” activity line. Let the update pipeline fail before stop/SteamCMD.

### QA-012

- **Severity:** P1
- **Retest:** RESOLVED
- **Component:** `BackupService.RestoreAsync`
- **Description:** Restore does not take `IServerActionGate`. A restore runs to completion while another lease named “Update server” is held. Online status is refused (`Restore_refuses_while_status_is_online` passes). The gate itself is ignored, so a concurrent start that flips status after the initial check can copy world files under a live process.
- **Steps to Reproduce:** Status Offline, one recent backup. `TryBegin("Update server")`, then `RestoreAsync`.
- **Expected:** `UserFacingException` because an action lease is held.
- **Actual:** `Record.ExceptionAsync` returns null. The restore completes.
- **Evidence:** `Restore_must_not_run_while_another_action_holds_the_gate`. `RestoreAsync` never references `IServerActionGate`.
- **Suggested Fix Direction:** `TryBegin("Restore backup")` around the whole restore, including the safety backup, and release it in a `finally`.

## P2 Issues

### QA-008

- **Severity:** P2
- **Retest:** RESOLVED
- **Component:** `ServerProcessManager.EnsureLockHeld`
- **Description:** `StartUnderLockAsync` / `StopUnderLockAsync` treat “any lease is busy” as “the caller owns it.” With no lease, start throws (passing test). With a different caller’s lease held, `StartUnderLockAsync` starts the fake server and returns Online.
- **Architect item 3 (wrong owner):** PARTIALLY CONFIRMED. Web and desktop buttons use `StartAsync` / `StopAsync` / `RestartAsync` / `UpdateAsync`, which `TryBegin` their own lease. Those concurrent calls are rejected (passing test). The under-lock methods are public on `IServerProcessManager` and are not ownership-checked.
- **Steps to Reproduce:** Configure a fake `ConanSandboxServer.exe`. `TryBegin("Update server")` on the shared gate. From another caller, `StartUnderLockAsync()`.
- **Expected:** `InvalidOperationException`. The process stays down.
- **Actual:** No exception. Status Online after the method returns (the 2-second start path ran).
- **Evidence:** `UnderLock_start_must_require_the_caller_to_own_the_lease`. `ServerProcessManager.cs:161-170` (`IsBusy` only). `StartUnderLock_without_any_lease_is_rejected` passes.
- **Suggested Fix Direction:** Pass the `IDisposable` lease into the under-lock methods, or an owner token captured when the lease is created, and refuse when the token does not match. Keep the no-lease throw.

### QA-009

- **Severity:** P2
- **Retest:** RESOLVED
- **Component:** `SteamWorkshopClient` + `WorkshopModService.CheckForUpdatesAsync`
- **Description:** Check Updates does not stop, start, download, or replace pak files. A Steam `publishedfiledetails` row with `result: 9` and no title is still parsed as a real file. The mod name becomes `Workshop 111`, `Error` is cleared, and `UpdateAvailable` is false.
- **Architect item 8:** PARTIALLY CONFIRMED. Read-only for process and files (passing tests). Metadata is wrong for a not-found id.
- **Steps to Reproduce:** Installed mod name `Working Mod`, pak `WORKING-MOD`. HTTP body `{"response":{"publishedfiledetails":[{"publishedfileid":"111","result":9}]}}`. Call `CheckForUpdatesAsync`.
- **Expected:** Name stays `Working Mod`. Error text says Steam returned no details. Pak bytes and `modlist.txt` stay as they were.
- **Actual:** Files unchanged (`AssertLiveUntouched` passed before the name assert). `name=Workshop 111; error=; updateAvailable=False`.
- **Evidence:** `Check_must_not_overwrite_a_working_mod_when_steam_returns_no_file_details`. Parser does not read `result` (`SteamWorkshopClient.cs:82-111`). Service assigns `mod.Name = remote.Title` and `mod.Error = null` (`WorkshopModService.cs:184-198`).
- **Suggested Fix Direction:** Skip items whose `result` is not success. Leave `Name` and `Error` unchanged when details are missing, and set the existing “installed copy was not removed” error.

### QA-010

- **Severity:** P2
- **Retest:** RESOLVED
- **Component:** Web Admin auth, CSRF, `wwwroot/js/site.js`
- **Description:** Action POSTs require an auth cookie. They do not validate the antiforgery token that login and `GET /api/csrf` issue. Unauthenticated API calls get a 302 to `/login.html` rather than 401, so `site.js` (which only special-cases status 401) follows the redirect with `fetch`. `GET /api/me` is anonymous. Player names, activity lines, and log lines are inserted with `innerHTML`. Rate limiting is on login only (5 per minute per IP). `SameSite=Strict` on the auth cookie is the control that limits classic cross-site POSTs.
- **Steps to Reproduce:**
  1. Start `WebHostFactory` against a temp data directory.
  2. `POST /api/server/update-server` with `AllowAutoRedirect=false`.
  3. `GET /api/me` with no cookie.
  4. Login as `admin`, then `POST /api/server/update-mods` with the cookie and no `X-CSRF-TOKEN`.
- **Expected:** 401 for anonymous calls. Authenticated mutations without the CSRF header rejected as antiforgery failures. `/api/me` requires auth.
- **Actual:**
  - Anonymous update: `302` to `http://127.0.0.1:<port>/login.html?ReturnUrl=%2Fapi%2Fserver%2Fupdate-server`.
  - `/api/me`: `200 OK`.
  - Authenticated update without CSRF: `400` body `Update mods failed` / `Configured path: (not set)`. The update service ran. Body does not contain `antiforgery`.
- **Evidence:** `Unauthenticated_mutation_returns_401_instead_of_a_login_redirect`, `Anonymous_identity_endpoint_requires_authentication`, `Authenticated_mutation_without_the_csrf_header_is_rejected`. `WebAdminExtensions.cs:42-47` registers antiforgery; no action endpoint calls `ValidateRequestAsync`. `MapGet("/api/me")` at line 265 has no `RequireAuthorization`. `site.js:61-72` posts `data-action` with cookies only. `site.js:50`, `site.js:100`, `site.js:111` use `innerHTML`.
- **Suggested Fix Direction:** For `/api/*`, return 401 instead of redirecting to the login page. Call antiforgery validation on each authenticated POST and send `X-CSRF-TOKEN` from `site.js`. Require authorization on `/api/me`. Insert player, log, and activity text with `textContent`.

### QA-011

- **Severity:** P2
- **Retest:** OPEN (not part of this stabilization pass; start still marks Online after two seconds)
- **Component:** `ServerProcessManager.StartCoreAsync` status
- **Description:** After two seconds, if the process has not exited, status becomes Online and health stays `ServerStarting`. No game-port or query check is required. The dashboard binds `StatusText` to that enum, so the UI can read ONLINE while the dedicated server is still starting.
- **Steps to Reproduce:** Code path: `StartCoreAsync` delay then `State.Status = ServerStatus.Online` (`ServerProcessManager.cs:275-284`). The under-lock test reached status Online for a fake exe that never bound a port.
- **Expected:** ONLINE only after the process is accepting players, or the label stays STARTING until then.
- **Actual:** Status Online, health `ServerStarting`, two seconds after process start.
- **Evidence:** `ServerProcessManager.cs:275-284`. `DashboardViewModel.cs:186` prints `state.Status` uppercased. Not executed against a real `ConanSandboxServer.exe`.
- **Suggested Fix Direction:** Leave status at Starting until the existing health check reports the game port or RCON. Keep the 2-second delay as a minimum, not as the Online transition.

## P3 Issues

### QA-013

- **Severity:** P3
- **Retest:** OPEN (the path check still compares `Path.GetFullPath(dest)` to itself)
- **Component:** `BackupService.BackupNowAsync` path check
- **Description:** The escape check compares `Path.GetFullPath(dest)` to itself, so the branch never throws. Backup folder names are timestamp strings today, so this is dead code rather than a reached escape.
- **Steps to Reproduce:** Read `BackupService.cs:39-43`.
- **Expected:** A destination outside the backups root throws.
- **Actual:** The second comparison is always false, so the `if` is always false.
- **Evidence:** `BackupService.cs:39-43`.
- **Suggested Fix Direction:** Throw when `!PathValidator.IsUnderRoot(dest, _paths.BackupsDirectory)`.

### QA-014

- **Severity:** P3
- **Retest:** OPEN (not part of this stabilization pass)
- **Component:** Settings flags that are not read
- **Description:** `BackupBeforeLoadOrderChange` defaults to true and has no readers. Move Up / Move Down confirm in the UI and do not back up. `DelayedRestartService.FinishAsync` backs up when `BackupFirst` is set and then calls `RestartAsync`. It does not read `DelayedRestartRequest.UpdateServer` or `UpdateMods`.
- **Steps to Reproduce:** Search for `BackupBeforeLoadOrderChange` (definition only, `AppSettings.cs:124`). Read `DelayedRestartService.FinishAsync` (`ServerUpdateService.cs:329-337`).
- **Expected:** A load-order change with the flag on creates a backup. A delayed restart with update flags runs those updates.
- **Actual:** Move only rewrites `modlist.txt`. Delayed restart is backup (optional) plus restart.
- **Evidence:** Grep shows a single hit for `BackupBeforeLoadOrderChange`. `FinishAsync` as cited. UI confirm text is in `SettingsViewModel.cs` Move Up / Move Down.
- **Suggested Fix Direction:** Either call `BackupNowAsync` from `MoveAsync` when the flag is set, or stop defaulting the flag to true until it is wired. Honor the delayed-restart update flags or stop exposing them on the request object.

### QA-015

- **Severity:** P3
- **Retest:** OPEN (not part of this stabilization pass)
- **Component:** Secrets and session
- **Description:** Web Admin passwords are PBKDF2 hashes. RCON, server, admin, and Steam passwords sit in `secrets.bin` via `ISecretProtector`. On Windows that protector uses DPAPI. On any other OS it stores `dev-base64:` plus base64 and logs that this is not protected (`DpapiSecretProtector.cs:29-31`). SteamCMD is invoked as anonymous; the argument log does not include a Steam password. RCON password is not written into the log lines reviewed. Cookie options use a 30-minute sliding expiration (`WebAdminExtensions.cs:36`) while sign-in also sets `ExpiresUtc` from `SessionMinutes` (lines 191-195).
- **Steps to Reproduce:** Code review of `ProtectedSecrets`, `DpapiSecretProtector`, `SteamCmdService.RunSteamCmdAsync`, `WebAdminExtensions` cookie setup. No production secrets file from an operator install was opened.
- **Expected:** Secrets stay out of logs and out of `settings.json`. Session length follows the setting.
- **Actual:** `settings.json` does not contain the secret fields (they are a separate object). Non-Windows storage is reversible base64. Two session lifetimes are configured.
- **Evidence:** Files cited above.
- **Suggested Fix Direction:** Keep refusing to log secret fields. Make the cookie lifetime use `SessionMinutes` only. Leave DPAPI as the Windows path; the base64 path is a development fallback and should stay out of operator machines.

## Gate Regression Verification

| Case | Result | How |
| --- | --- | --- |
| A. Update Server while running | No nested-gate failure. One lock, backup, stop, SteamCMD, start, Online. | Passing unit test, recording server and real `ServerProcessManager` |
| B. Update Mods while running | Same shape, one `UpdateAll`, no server SteamCMD. | Passing unit test |
| C. Update Everything while running | One backup, one stop, one server update, one mod pass, one start. | Passing unit test |
| D. Concurrent Restart or Stop during Update | Second call gets `UserFacingException`. No deadlock. Gate free after the first finishes. | Real process manager + blocking backup |
| D. Web vs desktop | Embedded Web Admin forwards the same `IServerUpdateService` and `IServerProcessManager` (`WebAdminEmbeddedHost.cs:48-57`, `App.xaml.cs:39-40`). A second process (`dotnet run` on the Web project) has its own gate. Not executed as two processes. | Code review |
| D. Under-lock without the caller’s lease | No lease: throw (pass). Someone else’s lease: start proceeds (QA-008). | Unit test |
| E. Exception while the lease is held | Lease released. Later update runs. | Passing unit test |
| Mods Update Selected / Update All | Bypass the gate (QA-005). | Unit test |
| Restore during a held lease | Proceeds (QA-012). | Unit test |

Deadlock from the handoff (orchestrator calls `StartAsync` while it already holds the lease): **not reproduced**. `RunLockedAsync` calls `StartUnderLockAsync` / `StopUnderLockAsync`.

## Workshop File-Safety Verification

| Case | Installed pak | Result |
| --- | --- | --- |
| Invalid / unknown id (not in the list) | Unchanged, SteamCMD not called | Pass |
| Add of another id whose download throws | Existing pak unchanged; new row gets an error | Pass |
| SteamCMD throws (network / not found) | Unchanged, modlist unchanged | Pass |
| Non-zero exit (fake throws like `SteamCmdService`) | Unchanged, even if a partial pak is already in staging | Pass |
| Exit 0, staging directory empty | Throws “no .pak”, pak unchanged | Pass |
| Staging directory missing | `DirectoryNotFoundException`, pak unchanged | Pass |
| Real `SteamCmdService` on Linux | Throws before process start, runner not called, pak unchanged | Pass. This is not a Windows SteamCMD run. |
| Exit 0, leftover pak already in staging | Live file replaced; previous bytes only in `.bak` | **QA-002 fail** |
| Exit 0, new 0-byte pak | Live file replaced with length 0 | **QA-002 fail** |
| Two paks in the download | One arbitrary pak installed, modlist rewritten, the other pak dropped | **QA-002 fail** |
| UpdateAll, second mod throws | First mod already replaced | **QA-002 fail** |
| Path / command text as a Workshop ID | Rejected by `WorkshopIdValidator` | Pass |
| `modlist.txt` traversal `../../...` | Line is `evil.pak` only | Pass on this OS. Backslash-only names are a Windows `Path.GetFileName` question and were not executed on Windows. |

Steam `result: 9` does not delete the pak (QA-009). It does overwrite the display name.

## Update Pipeline Verification

Success, server already online (recording fakes, one real process-manager pass for Update Server):

- Update Server: `backup pre-server-update` → `stop-under-lock` → `server-update` → `start-under-lock` → Online.
- Update Mods: `backup pre-mod-update` → `stop-under-lock` → one `UpdateAll` → `start-under-lock` → Online.
- Update Everything: one backup `pre-update-everything`, one stop, one server update, one mod pass, one start → Online.

Offline:

- Update Server / Update Mods with the shipped `restartAfter: true` → Online (QA-003).
- Update Server / Update Mods with `restartAfter: false` → files step runs, then the state machine throws, status stays Offline (QA-004).
- Update Everything → Online, one `start-under-lock` (labeled restart button).

Check Updates: no stop, no start, no download, no pak replace, no modlist rewrite. Metadata write is real and, for `result: 9`, wrong (QA-009).

Update All semantics (not a defect by name): `WorkshopModService.UpdateAllAsync` (`WorkshopModService.cs:156-161`) re-downloads every enabled mod and skips disabled mods. `UpdateAvailable` is ignored. Test `Update_all_downloads_every_enabled_mod_including_ones_without_update_available` passed (ids 1 and 2 downloaded, id 3 disabled skipped). The safety problem is QA-002 / QA-005, not the word “all.”

Latest dedicated-server build via SteamCMD `app_info_print`: **not a blocker** for this milestone. `CheckAsync` still reports the installed ACF build id only and sets `AvailableBuild` null (`ServerUpdateService.cs:64-78`). Recommendation only: a 90-second `app_info_print`, a buildid parser, a 15-minute cache, and “Unknown” on failure. Do not block QA-001 on this.

## Web/Desktop Consistency

| Action | Desktop | Web | Same pipeline | Gate |
| --- | --- | --- | --- | --- |
| Start / Stop / Restart | `IServerProcessManager` | Same, when embedded | Yes | `TryBegin` |
| Update Server | `UpdateAsync(true)` | `POST /api/server/update-server` → `UpdateAsync(true)` | Yes | Yes |
| Update Mods | Dashboard: `UpdateModsAsync(true)`. Mods page: direct `UpdateAsync` / `UpdateAllAsync` | `POST /api/server/update-mods` → `UpdateModsAsync(true)` | Dashboard and Web yes. Mods page no (QA-005). | Dashboard and Web yes. Mods page no. |
| Update Everything | `UpdateEverythingAsync` | No endpoint | Desktop only | Yes |
| Check Updates | `CheckAsync` and Mods page `CheckForUpdatesAsync` | `POST /api/server/check-updates` | Service check yes | Check does not take the gate |
| Backup | `BackupNowAsync` | `POST /api/server/backup` | Same service | Backup and restore do not take the gate (QA-012) |
| Auth | Desktop UI | Cookie required on action POSTs | — | 302 instead of 401; CSRF token not checked (QA-010) |

Useful errors: `UserFacingException` becomes HTTP 400 `FormatForDisplay()`. Other exceptions become HTTP 500 with `ex.Message` (`WebAdminExtensions.cs:286-289`).

## Architect Review Verification

| # | Architect claim | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Restore can delete the backup it is copying and still report success | **CONFIRMED** | QA-001. Source directory removed, live world stayed `CURRENT`, activity log said restored. Recent-backup control passes. |
| 2 | Mod replacement is not transactional | **CONFIRMED** unsafe. Delete-before-download **NOT CONFIRMED** | QA-002. Exception / non-zero / empty staging leave the pak. Exit 0 plus a leftover or 0-byte pak replaces it. Multi-pak and `UpdateAll` are partial. |
| 3 | Under-lock methods can run without owning the lease; mods operations bypass the gate | **PARTIALLY CONFIRMED** | No lease throws. A foreign lease can start the server (QA-008). Public Restart/Stop during Update are rejected. No nested deadlock. Mods Update Selected does not hold the gate (QA-005). |
| 4 | Offline before update becomes Online after | **CONFIRMED** for Update Server and Update Mods. **PARTIALLY CONFIRMED** for Update Everything | QA-003. Shipped callers pass `restartAfter: true`. Everything hardcodes true; its button says RESTART. `restartAfter: false` does not stay a successful offline update (QA-004). |
| 5 | Update Everything is one cycle, and rollback is missing | **CONFIRMED** | Passing single-cycle test while online. QA-006 failure: one backup, one stop, server update applied, mod 1 replaced, mod 2 unchanged, status left Offline, no start, error does not say stopped. |
| 6 | What Update All does | **Behavior recorded, not filed as a naming bug** | Every enabled mod is re-downloaded. Disabled mods are skipped. `UpdateAvailable` is not consulted. |
| 7 | `app_info_print` for the latest server build | **Not a milestone blocker** | Recommendation only. Check still cannot show a remote server build id. |
| 8 | Check Updates is read-only | **PARTIALLY CONFIRMED** | No stop, start, download, pak replace, or modlist rewrite. `result: 9` overwrites the mod name and clears Error (QA-009). |

## Regression Risks

- Fixing QA-003 by passing `restartAfter: false` without QA-004 makes every offline update throw after the files change.
- Fixing QA-002 by deleting the live pak before download reintroduces the failure mode these tests forbid. Clear staging, then replace.
- Retention changes for QA-001 must not start deleting the backup the admin selected. The passing recent-backup test is the control.
- `File.Replace` on a second bad attempt overwrites `.bak`. A fix that only “keeps .bak” is not durable.
- Builder tests that stub `IWorkshopModService` will stay green if the pak replace regresses. Keep the `QaWorkshopFileSafetyTests` that read file bytes.
- Two processes (desktop plus a standalone Web host) do not share a gate. The embedded host does.

## Missing Tests

- Windows `steamcmd.exe` exit codes for a bad Workshop id, a missing item, and a killed download.
- `File.Replace` while `ConanSandboxServer.exe` has the pak open.
- Restore copy interrupted after the first file (in-place copy, no staging).
- Two OS processes fighting over one install directory.
- Browser click-through of the WPF buttons and the Web Admin pages (CSRF header, 302 handling, innerHTML).
- Delayed-restart timer firing Update flags (the flags are unread; no timer test was added).
- Backup id collision in the same second.

## Unable to Verify

- WPF click paths, dialogs, and the “change load order?” warning. The commands were read. The window was not opened. Reason: this host compiled the Windows-targeted project and cannot run WPF.
- Real SteamCMD `app_update 443030` and `workshop_download_item`, including where the pak lands (`force_install_dir` versus `steamapps/workshop/content`). Reason: no Windows SteamCMD. The service throws before launch on Linux, which only proves fail-closed on this OS.
- Real Conan process, ports, RCON, crash restart against a live server. Reason: no dedicated server.
- DPAPI round-trip of `secrets.bin`. Reason: not Windows. The non-Windows base64 path was read, not used with operator secrets.
- Whether a SteamCMD non-zero exit for an invalid Workshop id is what Valve’s client actually returns. The code throws on non-zero **if** `SteamCmdService` is the implementation. A zero exit with a leftover staging pak is the tested destruction path.

## Live Environment Tests Still Required

1. Windows: invalid Workshop id, nonexistent id, and a killed `workshop_download_item` against an installed pak. Confirm the live `Working.pak` bytes and `modlist.txt` after each.
2. Windows: Update Server and Update Mods with the game **online**, then with it **offline**. Confirm process stop/start and that offline stays offline after QA-003/QA-004.
3. Windows: Update Everything while online; kill SteamCMD during the mod phase. Record process status, which paks changed, and the error text (QA-006).
4. Windows: restore a backup older than 14 days when at least 10 newer backups exist. Confirm that backup folder still exists and the world matches it (QA-001). Use a copy, never `D:\ConanBackups`.
5. Windows: Mods page Update Selected while the server is online. Confirm it cannot overlap Dashboard Update Server (QA-005).
6. Browser: logged-in Web Admin POST without `X-CSRF-TOKEN`; anonymous POST status code; player name containing markup on the players panel (QA-010).

## Recommended Builder Actions

1. QA-001 first. Pin the restore source out of retention, and do not log success if that directory disappeared or nothing was copied.
2. QA-004 and QA-003 together. Allow `Validating -> Completed`. Stop passing `restartAfter: true` from Update Server and Update Mods.
3. QA-002. Fresh staging directory, reject empty paks, do not `File.Replace` on a file this attempt did not create, and make `UpdateAll` all-or-nothing before replace.
4. QA-005 and QA-012. One gate for mod updates and for restore.
5. QA-006 and QA-007. Say when the server was left stopped. Fail a backup that copied no world before the pipeline stops the server.
6. Leave `app_info_print` off this fix list.

## Recommended Next QA Target

Re-run this suite after the builder touches restore and the update pipeline. The 20 `QA-KnownFailure` tests should go green without assertion edits, except `Update_everything_while_offline_stays_offline` if the “& RESTART” label stays and that start is intentional. Then the Windows list above. Do not start the next milestone while QA-001 is open.

---

CONAN QA → CONAN BUILDER
BLOCKING: QA-001 (restore deletes the selected backup and reports success). Also fix before calling this milestone verified: QA-002, QA-003, QA-004, QA-005, QA-006, QA-007, QA-012.
P0: QA-001 RestoreAsync safety-backup retention can delete the source backup, then log “Restored” while the live world is unchanged.
P1: QA-002 unverified pak replace (stale staging, 0-byte, multi-pak, partial Update All). QA-003 Update Server/Update Mods start a server that was offline. QA-004 offline update with restartAfter false throws Validating→Completed after the file work. QA-005 Mods page Update Selected/Update All bypass the action gate. QA-006 Update Everything failure leaves the server offline, mod 1 replaced, mod 2 unchanged, no rollback message. QA-007 empty backup logged as completed. QA-012 restore ignores the action gate.
SAFE TO CONTINUE: NO
NEXT REQUIRED FIX: QA-001 — exclude the selected backup from the pre-restore retention pass, and fail the restore if that source is gone or nothing was copied.

---

# Stabilization Retest

Independent retest of `origin/cursor/conan-server-control-a853` at `71afd24`, merged into `cursor/qa-gate-workshop-58f9`. Production code was not changed by QA. Historical findings above are unchanged. This section is the current verdict.

## Build

`dotnet build -c Release` on this agent: **succeeded, 0 warnings, 0 errors**.

## Tests

`dotnet test -c Release`: **97 passed, 1 failed, 0 skipped, 98 total**.

The one failure is the new QA-016 regression `Partial_live_commit_must_not_restart_onto_a_mixed_mod_set`. Builder’s claimed 95/95 is the suite before these three QA additions (98 − 3 = 95). Those 95 are included and passed.

## Test Integrity

- Old QA tests retained: **YES**. Every `[Fact]` / `[Theory]` method from the previous QA commit `ea7854d` is still present. Name check against that commit found none missing.
- Assertions materially weakened: **NO**. Safety asserts (source backup survives, live pak bytes, offline stays offline, 401, antiforgery body, lease ownership) are intact. Two adaptations are stronger or equivalent: restore/mod-update gate tests now use the same `ServerActionGate` the service uses, and `Missing_staging_directory_does_not_change_the_installed_pak` expects `UserFacingException` (message contains “pak”) instead of `DirectoryNotFoundException` because staging is created empty before SteamCMD. It still requires the live pak to stay unchanged.
- Tests skipped/disabled: **NO**. No `[Fact(Skip)]`, no `Skip =`, no trait filter. `dotnet test --list-tests` discovered 98. The run reported Skipped: 0. `Category=QA-KnownFailure` tests were executed.
- New tests, builder (13), all passing: protected retention ids; `Validating → Completed`; fail-from-validating; Update Selected rejected while Restart holds the gate; Update All rejected while Update Server holds the gate; owning lease can start; restore copy failure does not log success; failed restore releases the gate; valid world backup succeeds; world copy exception fails the backup; unwritable destination fails the backup; a valid single pak replaces the live mod; Update All holds the gate.
- New tests, this retest (3):
  - `Update_server_failure_while_offline_stays_offline` — QA-003 failure path. Passed.
  - `Disposed_lease_and_a_lease_from_another_gate_cannot_start_under_lock` — QA-008 disposed lease and a lease from a different gate. Passed.
  - `Partial_live_commit_must_not_restart_onto_a_mixed_mod_set` — QA-016. **Failed** (see New Issues).

## Issue Status (QA-001..QA-010, QA-012, plus the rest of this report)

| ID | Severity | Status | Evidence |
| --- | --- | --- | --- |
| QA-001 | P0 | RESOLVED | Success: `Restore_must_not_delete_the_source_backup_or_report_success_when_it_is_gone` passed. `BackupNowCoreAsync` passes the source id, source path, and full path into retention, and also protects the new safety backup id and directory. Copy then requires `world/` and `CopyDirectory`. “Restored backup” is logged only after that copy and a second existence check. Failure: `Restore_must_fail_and_not_log_success_when_copy_fails` passed (live marker replaced with a directory so the copy throws). No “Restored” activity. `Failed_restore_releases_the_action_gate` passed. Unexpected exceptions still leave the `finally` that disposes the lease. |
| QA-002 | P1 | RESOLVED | A exit 0 + no pak, B zero-byte, C stale staging, D two paks, F download exception: live `WORKING-MOD` unchanged (those tests passed). E valid single non-empty pak: `Valid_pak_replaces_the_installed_mod` passed and replacement is after validation. `PrepareFreshStaging` deletes `staging/workshop/{id}` and recreates it. Validation requires exactly one `.pak`, length > 0, and `LastWriteTimeUtc` not older than 5 seconds before the attempt. Exit code alone is not accepted. |
| QA-003 | P1 | RESOLVED | Six success cases passed: Update Server, Update Mods, and Update Everything, each from Online (stop then start, ends Online) and from Offline (no `start-under-lock`, stays Offline), including the shipped `restartAfter: true` calls. Offline failure (`Update_server_failure_while_offline_stays_offline`) stays Offline and does not stop or start. Online failure before any mod replace restarts (`Exception_while_the_gate_is_held_releases_it_for_a_later_action`). The unsafe restart is QA-016, not a random status flip. |
| QA-004 | P1 | RESOLVED | Offline success reaches Completed with no illegal transition (the stay-offline tests would throw on `Validating → Completed`). State machine allows `Validating → Completed` and `Validating → Starting`. Online path is Checking → (Backup) → Stopping → Updating → Validating → Starting → HealthCheck → Completed, observed as stop-under-lock then start-under-lock and status Online. `Failed_validation_reaches_failed_from_validating` passed. The service calls `_pipeline.Fail` in the catch and does not transition to Completed after an exception. |
| QA-005 | P1 | RESOLVED | Mods page `UpdateSelectedAsync` / `UpdateAllAsync` call `IServerUpdateService.UpdateSelectedModsAsync` / `UpdateModsAsync`, which take the shared singleton gate, back up, and stop when the server was running. `WorkshopModService.UpdateAsync` / `UpdateAllAsync` also `TryBegin` and the download-time probe tests passed. Restart, Stop, Update Server, and a later Update All are rejected or serialized (`Concurrent_restart_and_stop_are_rejected_while_an_update_holds_the_gate`, `Update_selected_is_rejected_while_restart_holds_the_gate`, `Update_all_is_rejected_while_update_server_holds_the_gate`). The pipeline calls `ApplyUpdatesAsync` so it does not take the gate twice. No deadlock. |
| QA-006 | P1 | RESOLVED | Original steps (server update succeeds, mod 2 throws during download): `Server_update_success_plus_mod_update_failure_must_not_leave_a_running_server_down_or_a_partial_mod_set` passed. Both live paks stay `MOD*-OLD`, one backup, one stop, server returns Online, the call throws `UserFacingException`, and the activity log does not record “Update everything completed”. Server binaries are not rolled back. The harder live-commit case is QA-016. |
| QA-007 | P1 | RESOLVED | Missing `ConanSandbox/Saved` throws and does not log “Backup completed”. A present world logs success and sets `IncludesWorld`. A copy that throws (`game.db` mode `000` on this agent) and an unwritable backups directory throw and do not log success. Config and modlist remain optional. A required world copy that fails discards the incomplete folder. |
| QA-008 | P2 | RESOLVED | `EnsureOwns` requires `IServerActionGate.Owns(lease)` (same lease id, not disposed, gate busy). Null lease, a fake `IServerOperationLease`, a disposed owning lease, and a lease from a different gate all throw `InvalidOperationException` and leave the process Offline. The owning lease starts. This is an accidental-misuse check, not a cryptographic one. |
| QA-009 | P2 | RESOLVED | `SteamWorkshopClient` skips `result != 1`. `Check_must_not_overwrite_a_working_mod_when_steam_returns_no_file_details` passed: name stays `Working Mod`, error text is set, pak and modlist unchanged. Check still does not stop, start, or download. |
| QA-010 | P2 | RESOLVED | Anonymous `POST /api/server/update-server` is 401, not 302. `GET /api/me` is 401. Authenticated `POST /api/server/update-mods` without `X-CSRF-TOKEN` is rejected and the body contains “antiforgery”; the update delegate is not entered (`RunAction` validates before `work()`). The same check is on start/stop/restart/backup/check/update and on delayed-restart/cancel. `site.js` sends `X-CSRF-TOKEN` and renders player, log, and activity text with `textContent`. |
| QA-011 | P2 | START PATH ADDRESSED | The two-second Online transition is gone. `StartCore` stays Starting until `IServerReadinessProbe` succeeds; timeout is Unresponsive; process exit during startup is Error. `TryAttachToExistingProcess` still sets Online without the probe. |
| QA-012 | P1 | RESOLVED | `RestoreAsync` takes “Restore backup” for the safety backup and the copy, and disposes the lease in `finally`. A held “Update server” lease rejects restore. After a copy failure the gate accepts a new lease. Online status is still refused before the copy. |
| QA-013 | P3 | RESOLVED | `EnsureDestinationIsUnderBackupRoot` throws unless `PathValidator.IsUnderRoot` is true. Traversal, an absolute path outside the root, and a `Backup` / `Backup-Evil` prefix are rejected (`M3BackupPathTests`). |
| QA-014 | P3 | OPEN | Unchanged. Load-order backup flag and delayed-restart update flags are still unwired. |
| QA-015 | P3 | OPEN | Unchanged. Non-Windows secret storage is still reversible base64. Two session lifetimes remain. |

## New Issues

### QA-016

- **Severity:** P1
- **Status:** RESOLVED (verification of `b422e1fbaf11dc0f9dfd3e977e24298a9307da64`). Verified rollback restores the old set and may restart. If restore, compare, or verification throws, or verification returns false, the server stays offline with recovery required.
- **Component:** `WorkshopModService.ApplyUpdatesAsync` + `ServerUpdateService.RunLockedAsync` catch
- **Description:** Staging is all-or-nothing. The live commit is not. After every target has passed validation, `CommitStagedAsync` replaces mods one by one. If a later `File.Replace` / `File.Move` throws, earlier mods stay replaced. There is no `.bak` rollback. If the server was online, the catch then calls `StartUnderLockAsync` anyway.
- **Steps:** Two enabled mods. Both downloads write a valid non-empty pak. `Mod1.pak` is a normal file (`MOD1-OLD`). `Mod2.pak` is a directory so the second commit throws. Server status Online. `UpdateEverythingAsync`.
- **Expected:** Failure, and either both live files restored to the previous bytes before any start, or the server left stopped. Do not start a previously online server on a mixed set.
- **Actual:** `UserFacingException: Is a directory`. `mod1=MOD1-NEW`. `Mod2.pak` still a directory. Calls `[stop-under-lock, start-under-lock]`. Status Online. Activity: `Installed/updated Workshop mod 1.` No “Update everything completed”. Guidance on the restored-online path does not say the mod set is mixed.
- **Evidence:** `Partial_live_commit_must_not_restart_onto_a_mixed_mod_set` failed. Code: commit loop at `WorkshopModService.cs` (stage all, then `CommitStagedAsync` with no rollback). Restart at `ServerUpdateService.cs` catch when `wasRunning` is true, with no check that every live replace finished.
- **Fix direction:** On a commit failure, restore already-replaced paks from the `.bak` created by `File.Replace` (or stage the whole live swap and commit it in one pass). Restart a previously online server only when no live pak from this attempt was left changed. Say so in the error when the server was left stopped or the set was restored.

### QA-017

- **Severity:** P3
- **Status:** OPEN
- **Component:** `DashboardView.xaml` button label vs `UpdateEverythingAsync`
- **Description:** The button still says `UPDATE EVERYTHING & RESTART`. An offline server is not started. `restartAfter` is accepted by `UpdateAsync` / `UpdateModsAsync` and ignored; only the pre-update online/offline state decides the start. This matches the QA-003 contract and disagrees with the label.
- **Not a stabilization blocker.** Rename the button, or add a real “start afterwards” control. Do not reintroduce an unconditional start.

## Code/Unit Verified

Restore retention exclusion and failed-copy reporting. Fresh Workshop staging and pak validation. Online/offline preservation for Update Server, Update Mods, and Update Everything, including the offline failure path. `Validating → Completed`. Shared action gate for mod updates and restore, including release after failure. Update Everything download-phase failure leaves both paks old and brings a previously online server back. Incomplete world backups fail. Lease ownership, including disposed and cross-gate leases. Steam `result != 1` skipped. Web 401, CSRF, and `/api/me` authorization.

## Live Windows Verification Still Required

No Windows host, SteamCMD, Workshop, or Conan dedicated server was available.

- Restore a backup older than 14 days with at least 10 newer backups. Confirm that folder and the new safety backup both remain, and the live world matches the selected backup.
- SteamCMD `workshop_download_item` with `+force_install_dir` set to `staging/workshop/<id>`. Confirm the `.pak` actually appears under that tree. If SteamCMD writes only under its own `steamapps/workshop/content`, validation fails closed and the live mod is not updated.
- Confirm a real downloaded `.pak` is not rejected by the “mtime within 5 seconds of start” check. Unit tests write files with `File.WriteAllText`, which stamps “now”. A cache copy with an older timestamp is rejected and the live pak is left unchanged.
- Update Server / Update Mods / Update Everything with the game online, then offline. Confirm the process actually stops and starts only when it was online.
- Update Everything while online; fail the second mod during download. Confirm both paks unchanged and the process is back up.
- Reproduce QA-016 on Windows if a second pak replace can fail (locked file or full volume) and confirm whether the dedicated server is started on the mixed set.
- Browser: anonymous API call is 401; logged-in POST without `X-CSRF-TOKEN` does not run the action.

## Remaining Risks

- **QA-016 (P1).** Auto-restart after a partial live commit. This is the stabilization blocker.
- **Known UX/semantic debt, not a blocker:** `UpdateAllAsync` / Update Mods still re-downloads every enabled mod. `UpdateAvailable` is not a filter. Disabled mods are skipped. Test `Update_all_downloads_every_enabled_mod_including_ones_without_update_available` passed.
- **QA-017 (P3).** Dashboard label still says restart.
- Restore copy is not transactional. A throw in the middle of `CopyDirectory` leaves a mixed live world, does not log “Restored”, and keeps the source. Not the original QA-001 failure (source deletion plus a success log).
- `ApplyUpdatesAsync` does not take the gate. Production UI and the update pipeline call it only while the gate is already held. A direct caller would mutate mods without the gate.
- QA-011, QA-013, QA-014, QA-015 remain open and are outside this pass.
- `app_info_print`, Delayed Restart, Wait Until Empty, and First Run Wizard were not implemented. `CheckAsync` still reports the installed ACF build only.

## GO / NO-GO

QA-001 is resolved. The previous P1 list QA-002, QA-003, QA-004, QA-005, QA-006, QA-007, and QA-012 is resolved against the original findings. Build is clean. One new test fails. QA-016 is a new P1: a previously online server is started after a live mod commit has already changed an earlier pak. That is not safe to treat as done.

CONAN QA → CONAN BUILDER
OVERALL: FAIL
BUILD: PASS (0 warnings, 0 errors)
TESTS: 97 / 98 passed (1 failed, 0 skipped)
P0 OPEN: none
P1 OPEN: QA-016
SAFE TO CONTINUE: NO
LIVE TEST STILL REQUIRED: SteamCMD workshop download path and pak timestamp, Conan process stop/start, restore of an old backup on Windows, browser 401/CSRF
NEXT ACTION: Fix QA-016. Do not restart a previously online server when a multi-mod live commit is partial, or roll the replaced paks back before that restart. Re-run `Partial_live_commit_must_not_restart_onto_a_mixed_mod_set`.

---

# Final Stabilization Retest

Revision tested: `9bea046fad4978acdedaf61a17e3bde714fff829` (`fix: rollback partial live mod batch on commit failure`), merged into `cursor/qa-gate-workshop-58f9`. QA did not change production code. Historical findings above stay as written.

## Build

`dotnet build -c Release`: **succeeded, 0 warnings, 0 errors**.

## Tests

`dotnet test -c Release`: **discovered 105, passed 104, failed 1, skipped 0**.

Builder’s 104/104 is the suite without `Rollback_verify_throw_leaves_previously_online_server_offline`. That test is the one failure. The other 104, including the original QA-016 regression and builder’s six QA-016 tests, passed.

## Test Integrity

- OLD QA TESTS RETAINED: **YES**. Every QA method from `ea7854d` is still present.
- QA-016 ORIGINAL REGRESSION RETAINED AND UNWEAKENED: **YES**. `Partial_live_commit_must_not_restart_onto_a_mixed_mod_set` still asserts `UserFacingException`, no “Update everything completed”, and `!(mod1 == MOD1-NEW && restarted)`. It passed on this revision because rollback restores Mod1 to `MOD1-OLD` before the restart.
- SKIPPED TESTS: **none**. No `[Fact(Skip)]`. Discovery and the run both report 0 skipped.
- ASSERTIONS WEAKENED: **NO**.
- NEW QA-016 TESTS (builder, six), all passed:
  1. `TestA_two_mods_second_replace_fails_restores_both_old_and_fails` — two-mod live replace failure, both end old, operation failed. Same fault as Test C, without the restart asserts.
  2. `TestB_three_mods_third_replace_fails_restores_all_old_and_fails` — distinct: third replace fails, all three old.
  3. `TestC_replace_fails_rollback_succeeds_previously_online_server_may_restart_but_operation_failed` — same two-mod fault as A, plus Online restart only after “Previous mod set restored”.
  4. `TestD_replace_fails_and_rollback_fails_leaves_previously_online_server_offline` — distinct: rollback directory deleted, server stays Offline, title contains “recovery required”.
  5. `TestE_all_replacements_succeed_live_mods_are_new_and_rollback_is_cleaned` — success path, not a failure path. Both paks NEW, activity records both installs, rollback dir cleaned, Online.
  6. `TestF_originally_offline_commit_failure_with_successful_rollback_stays_offline` — distinct: same replace failure while Offline, no stop, no start.
- QA addition, failed: `Rollback_verify_throw_leaves_previously_online_server_offline`. After Mod1 is replaced, the rollback copy is left in place but mode `000`, so reading it throws. This is a different failure from Test D (missing directory returns false; unreadable file throws out of `TryRollback`).

## QA-016

**PARTIALLY RESOLVED.**

What holds:

- Two mods, both stage, Mod1 replace succeeds, Mod2 replace fails, rollback copy readable: Mod1 is `MOD1-OLD`, Mod2 stays the original directory, operation throws, and a previously online server may restart. Not `MOD1-NEW` + online. Original regression and Test A/C passed.
- Three mods: Test B passed. Mod1 and Mod2 restored to `MOD*-OLD`, Mod3 unchanged, operation failed.
- Missing rollback directory (Test D): `TryRollback` returns false, `RecoveryRequired` is true, no `start-under-lock`, status Offline, message says recovery required and left offline. Activity does not say “Update everything completed”.
- Success (Test E): both paks `MOD*-NEW`, installed activity written, rollback storage removed, Online stays Online. Offline success still follows `wasRunning` (QA-003 offline test passed; `RunLockedAsync` starts only when the server was online).
- Metadata on the verified-rollback path: `PersistSuccessfulBatchAsync` runs only after every `ReplaceLive` succeeds. Settings `InstalledTimestamp`, `modlist.txt`, and “Installed/updated Workshop mod” are not written when a later replace fails and rollback returns. Test E is the only path that asserts the installed activity lines.
- Rollback directories: `staging/mod-update/<new-guid>/rollback/<workshopId>/<file>`. A new id is created per `Prepare`. Copies are taken from the live pak before the first replace. Cleanup runs after a verified rollback or a full success. `KeepRollbackForRecovery` retains the directory when `TryRollback` returns false.
- Cancellation: the live loop does not read the cancellation token. `PersistSuccessfulBatchAsync` uses `CancellationToken.None`. `ThrowIfCancellationRequested` runs once, before `Prepare`. A cancel before commit does not replace files. A cancel during the replace/rollback loop is not observed.
- Server binary: when the failure is a `ModBatchCommitException`, the catch logs “Server binary update may have succeeded, but the Workshop mod live commit failed.” The operation still throws. Binaries are not rolled back. That log line is not written when `TryRollback` throws, because that exception is not a batch result.

What does not hold:

- Rollback copy present but unreadable. `TryRollback` catches the failed restore, then `FilesEqual` throws `UnauthorizedAccessException` from outside that catch. `ApplyUpdatesAsync` does not turn that into `ModBatchCommitException`. `RunLockedAsync` therefore treats it as a generic failure and calls `StartUnderLockAsync`.
- Observed: `mod1=MOD1-NEW`, calls `[stop-under-lock, start-under-lock]`, status Online, display “Update everything failed / Access to the path …/rollback/1/Mod1.pak is denied.” The guidance is the generic “saves were not deleted” text. It does not say recovery required. The `finally` also deletes the rollback directory because `KeepRollbackForRecovery` was never set.

## Smoke

QA-001, QA-002, QA-003, QA-004, QA-005, QA-006, QA-007, QA-008, QA-009, QA-010, and QA-012 stay **RESOLVED**. Their regression tests passed on this revision. This commit does not reopen them.

## Non-blocking

QA-011 (P2), QA-013, QA-014, QA-015, QA-017 (P3) stay open. `UpdateAllAsync` still re-downloads every enabled mod. The dashboard button still says UPDATE EVERYTHING & RESTART. Severity unchanged.

## Code/Unit Verified

Batch prepare, per-mod rollback copies, verified restore before restart, missing-copy recovery leaving the server offline, success-path metadata, and the unreadable-copy restart. No SteamCMD or Conan process was started.

## Live Windows Verification Still Required

Real SteamCMD workshop download, real `.pak` timestamps, Conan dedicated server stop/start, a player join after a rolled-back mod update, and a Windows `File.Replace` failure while a rollback copy cannot be read.

CONAN QA → STABILIZATION FINAL
REVISION TESTED: 9bea046fad4978acdedaf61a17e3bde714fff829
OVERALL: FAIL
BUILD: PASS
TESTS: 104 / 105 passed, 0 skipped
TEST INTEGRITY: PASS
QA-016: PARTIAL
P0 OPEN: none
P1 OPEN: QA-016
NON-BLOCKING OPEN: QA-011, QA-013, QA-014, QA-015, QA-017, Update All re-downloads every enabled mod, dashboard label still says UPDATE EVERYTHING & RESTART
CODE/UNIT VERIFIED: verified rollback restores OLD/OLD and may restart; missing rollback copy stays offline with recovery required; unreadable rollback copy restarts onto MOD1-NEW
LIVE WINDOWS TEST STILL REQUIRED: SteamCMD workshop download, real pak replace, Conan stop/start and join
SAFE TO CONTINUE: NO
NEXT ACTION: QA-016 — if rollback throws or cannot be verified, do not call StartUnderLockAsync; leave the server offline and report recovery required. Re-run Rollback_verify_throw_leaves_previously_online_server_offline.

---

# Final QA-016 Verification

Revision tested: `b422e1fbaf11dc0f9dfd3e977e24298a9307da64` (`fix: fail closed when mod rollback verification throws`). Merged into `cursor/qa-gate-workshop-58f9`. QA did not change production code.

## Build

`dotnet build -c Release`: **succeeded, 0 warnings, 0 errors**.

## Tests

`dotnet test -c Release`: **discovered 108, passed 108, failed 0, skipped 0**.

This matches the builder claim 108/108/0/0. The previous QA total was 105 (104 passed, 1 failed). The fail-closed commit adds three tests (Test 4, Test 5, Test 6) and the previous failure now passes. 105 − 1 failed + 1 now passing + 3 new = 108.

## Test Integrity

- Previous QA regressions: **present**. `QaActionGateAndPipelineTests.cs` is unchanged from the prior QA commit. The full suite, including QA-001 through QA-010 and QA-012, passed.
- `Partial_live_commit_must_not_restart_onto_a_mixed_mod_set`: **unchanged**. Diff against the previous QA commit is empty for that file.
- `Rollback_verify_throw_leaves_previously_online_server_offline`: **unchanged**. Method text matches the previous QA commit exactly, including the mode `000` setup and the offline / no-start / recovery-required asserts. It now passes.
- Skipped tests: **none**. No `[Fact(Skip)]`. The run reported 0 skipped. The POSIX early return in the mode `000` test did not run; this host executed the body.
- Assertions weakened: **NO**. Test D’s activity substring changed from “rollback was incomplete” to “could not be verified” so it matches the new recovery guidance. It still requires Offline, no `start-under-lock`, a recovery-required title, and no “Update everything completed”.
- New tests are separate paths (see below).

## QA-016

**RESOLVED.**

`TryRollback` returns `RollbackResult` and catches every exception except `OutOfMemoryException` around restore and around `VerifyOriginals`. `FilesEqual` failures are caught inside verification and mark the result unverified. `SafeRollback` catches a throw from `TryRollback` the same way. `RunLockedAsync` restarts a previously online server only when there is no `ModBatchCommitException`, or when `IsSafeToRestart` is true (`Attempted && Succeeded && Verified && !RecoveryRequired`). Any other batch result throws the recovery-required error and does not call `StartUnderLockAsync`.

| Case | Result on this run |
| --- | --- |
| Mod1 replace OK, Mod2 replace fails, rollback verifies | OLD/OLD (Mod2 stays the original directory), FAILED, may restart. Original regression and Test A/C passed. Not a mixed online set. |
| Verification throws (`UnauthorizedAccessException`) | Mode `000` rollback copy. `Rollback_verify_throw_leaves_previously_online_server_offline` passed: Offline, no start, display contains “recovery required”. |
| Rollback copy throws (`IOException`) | Test 4 holds the copy with `FileShare.None`. A same-process `File.Copy` of that pattern throws `System.IO.IOException` (“being used by another process”) on this host. The test passed: Offline, no start, recovery required. |
| Verification returns false | Test D deletes the rollback directory. `FileNotFoundException` is swallowed into a failed `RollbackResult`. Passed: Offline, recovery required, no start. |
| Pre-mutation failure | Test 6 throws during Workshop download before `ReplaceLive`. Both paks stay `MOD*-OLD`. Server returns Online. Display does not say recovery required. |
| Verified rollback | Test C: OLD set, FAILED, Online restart, activity says the previous set was restored. |
| Verified rollback, then restart throws | Test 5: start is attempted and throws, status is not Online, mods stay old, display does not say completed. Guidance in `RunLockedAsync` is “The server was stopped for this update and was not restarted.” The start exception is written to the error log. |
| Metadata | `PersistSuccessfulBatchAsync` runs only after every live replace succeeds. Failure paths do not write `InstalledTimestamp`, `modlist.txt`, or “Installed/updated Workshop mod”. Recovery activity is the unverified-rollback guidance. Verified-rollback activity says the previous set was restored. Neither says the update completed. |

## TEST 4 / TEST 5 / TEST 6

- **TEST 4** `Test4_rollback_copy_throw_leaves_server_offline_with_recovery_required`: distinct. The rollback file still exists, unlike Test D, and is readable in principle, unlike mode `000`. The exclusive lock makes the restore copy throw `IOException`. The server stays offline. This is not the same branch as a false verification result or a pre-mutation failure.
- **TEST 5** `Test5_verified_rollback_then_restart_failure_still_reports_failed`: distinct. Rollback is allowed to succeed and the restart is invoked, then `StartUnderLockAsync` throws. The operation stays failed and the process is not left Online. This does not exercise an unverified rollback.
- **TEST 6** `Test6_failure_before_live_mutation_may_restart_previously_online_server`: distinct. The download throws before any live replace, so there is no `ModBatchCommitException`. A previously online server may restart onto the unchanged old paks. This is the safe restart, not the recovery-required path.

## Smoke

QA-001, QA-002, QA-003, QA-004, QA-005, QA-006, QA-007, QA-008, QA-009, QA-010, and QA-012 stay **RESOLVED**. Their tests passed. This commit does not change restore, staging validation, the action gate, auth, or the offline/online success transitions except the rollback restart rule above.

## Non-blocking

QA-011 (P2), QA-013, QA-014, QA-015, QA-017 (P3), and Update All still re-downloading every enabled mod. Not reopened.

## Code/Unit Verified

Fail-closed rollback for `UnauthorizedAccessException`, `IOException`, and a missing rollback copy. Verified restore may restart. Pre-mutation failure may restart. Restart failure after a verified restore stays failed. Metadata is written only after a fully successful live commit.

## Live Windows Verification Still Required

SteamCMD workshop download, real `.pak` replacement, Conan dedicated server stop/start, and a player join after a rolled-back mod update. `File.Replace` on Windows was not executed.

CONAN QA → STABILIZATION FINAL
REVISION TESTED: b422e1fbaf11dc0f9dfd3e977e24298a9307da64
OVERALL: PASS WITH ISSUES
BUILD: PASS
TESTS: 108 / 108 passed, 0 skipped
TEST INTEGRITY: PASS
QA-016: RESOLVED
P0 OPEN: none
P1 OPEN: none
NON-BLOCKING OPEN: QA-011, QA-013, QA-014, QA-015, QA-017, Update All re-downloads every enabled mod
CODE/UNIT VERIFIED: rollback verify/copy/compare failures stay offline with recovery required; verified rollback and pre-mutation failure may restart; metadata is not marked installed on those failures
LIVE WINDOWS TEST STILL REQUIRED: SteamCMD, Conan Dedicated Server, Workshop download, real pak replace, restart/join
SAFE TO CONTINUE: YES
NEXT ACTION: Ready for CONAN ARCHITECT to select the next milestone.

# M3 Pre-Live Review

Reviewed builder branch `claude/m3-task3-diagnostics` at `0352f4040208fc48590b15e92a4933c08e8b7405` (`docs: hand off M3 task 3 integration diagnostics`), merged into `cursor/qa-gate-workshop-58f9` as `ebf3ab3f7788098e5852d2ff2a77b0fbd64c5243`. No production code was edited by QA. No SteamCMD, Conan, or live Windows process was started.

REVISION: 0352f4040208fc48590b15e92a4933c08e8b7405
BUILD: PASS — `dotnet build -c Release` succeeded, 0 Warning(s), 0 Error(s). Matches the builder claim.
TESTS: PASS — `dotnet test -c Release`: Passed 203, Failed 0, Skipped 0, Total 203. Matches 203/203, 0 skipped.
TASK 2 COLD BACKUP: PASS
SERVER READINESS: PASS
TASK 3 READ-ONLY: PASS
STANDALONE CLIENT SEPARATION: PASS
REPORT REDACTION: PASS
LIVE CLAIM ACCURACY: PASS
P0 OPEN: none
P1 OPEN: none
NON-BLOCKING DEBT: QA-014, QA-015, QA-017, QA-018; Update All still re-downloads every enabled mod; dashboard button still says UPDATE EVERYTHING & RESTART; dashboard status text is the enum uppercased, so Starting displays STARTING; last-backup line is a timestamp with no Verified badge; mods row says "Update available" and the action is CHECK UPDATES / UPDATE ALL, not Verify-All; Web Admin password is a visible TextBox while typing and is hashed then cleared (stored plaintext is not loaded back)
LIVE TEST BLOCKER: SteamCMD not installed
SAFE FOR M3 TASK 4: YES
NEXT ACTION: Ready for CONAN BUILDER to perform guarded M3 Task 4 Live Windows integration.

## Test integrity

- Previous QA regression tests removed: **NO**. `Qa016ModBatchTransactionTests.cs` and `QaWebAdminTests.cs` are byte-identical to `581d208`. No `[Fact]` / `[Theory]` from that commit is missing.
- Assertions weakened: **NO**. `QaActionGateAndPipelineTests` adds the timeline assert `stop, backup, update, start` and passes `ImmediateReadyProbe` into the new `ServerProcessManager` constructor. `QaRestoreSafetyTests` constructs `SqliteBackupVerifier` and, on the success case, writes a valid `game.db` and requires `Succeeded` and `SqliteVerified`; the missing-world and copy-failure cases still require that "Backup completed" is not logged. `QaWorkshopFileSafetyTests` keeps the Linux path at zero process-runner calls and an untouched live pak. The Windows branch expects one failing runner call and the same untouched-pak assert. That branch did not run here.
- Skipped or disabled: **NO**. No `Skip` attribute. The Release run reported Skipped: 0.
- New redaction test: not added. `Exported_report_redacts_all_known_secrets_and_protected_blob` already serializes a report with RCON, server, admin, and Steam passwords, the Web Admin hash, the Steam username, and the secrets-file blob, and asserts none of those values appear in the exported JSON, the Markdown, or `JsonSerializer.Serialize(report)`.

## Area evidence

- Task 2 cold backup: `ServerUpdateService.RunLockedAsync` stops, then throws unless status is Offline or Error, then calls `BackupNowAsync`, and sets `mutationStarted` only after that returns. `StopCoreAsync` throws if the process has not exited. `BackupNowCoreAsync` throws on a missing main world DB, a copy/hash failure, or a failed `SqliteBackupVerifier` result, and logs "Backup completed" only when the manifest, hashes, SQLite check, and `Succeeded` are all true. Offline updates do not start. `M3ColdBackupPipelineTests` and `M3BackupVerificationTests` passed, including stop-failure, backup-failure, hash/quick_check failure, and manual Backup Now while online.
- Backup verification: hashes are SHA-256 of the backup copies. `ConanWorldFiles.Present` names only `game_0.db`/`game.db` and their `-wal`/`-shm`. An unrelated `.db` is not the world. `SqliteBackupVerifier` opens the copy with `SqliteOpenMode.ReadOnly`, pooling off, and `PRAGMA quick_check`; it does not VACUUM, checkpoint, or repair. `PRAGMA query_only=ON` is attempted and a failure is logged, then the check continues under the read-only connection. The live database is not opened.
- Server readiness: process start sets Starting and becomes Online only after the probe returns ready. Timeout sets Unresponsive and throws. Exit during startup sets Error and throws. The post-update start uses the same `StartUnderLockAsync` path and throws unless status is Online. Limitation: UDP bind or an RCON ping is not proof a player can join. `TryAttachToExistingProcess` still marks an already-running process Online without the probe.
- Task 3 read-only: `IntegrationDiagnosticsService.RunAsync` only reads files, settings, and (when the process manager already reports Online) UDP listeners and, if RCON is enabled with a password, one `listplayers` command. It does not start `Run Me!.bat`, `ConanSandbox.exe`, or `ConanSandboxServer.exe`, does not run SteamCMD or `app_update`, does not download Workshop mods, does not write `modlist.txt` or Conan configs, does not open the live world database, and does not change firewall, router, or Tailscale settings. The Diagnostics page button "Install SteamCMD" calls `SteamCmdService.InstallAsync` only when that button is used; the report run does not.
- Standalone client: `ConanSandbox.exe`, `ConanSandbox-Win64-Shipping.exe`, `Run Me!.bat`, and `.bat`/`.cmd`/`.ps1`/`.vbs`/`.lnk` are Fail as the dedicated-server executable. A server path inside the client root, a workspace that overlaps the client, and an install folder that contains the client exe without the server exe are Fail. Classification is case-insensitive and splits on `\` and `/`. `..` is resolved by `Path.GetFullPath` before overlap checks. Missing client root does not block server-live readiness. Client mod parity is a separate warning and the notes say Workshop sync is not automatic.
- Report redaction: exported JSON and Markdown, and the in-memory report, pass through `DiagnosticReportRedactor`. Known secrets are the RCON, server, admin, and Steam passwords, the Web Admin password hash, the Steam username, and the secrets-file text. RCON facts are `PasswordConfigured` YES/NO. Paths stay visible.
- Live claim accuracy: SteamCMD existence is `FilesystemInspected` with `Executed=NO` and `steamcmd.live` is NotTested. Server exe existence is not a boot (`server.live` NotTested, `Started=NO`). A `.pak` check says a file existing is not Conan loading it (`mods.workshop-live` NotTested). Port text says firewall/router reachability was not tested. `ConfigurationOnly` stays true and no check uses `LiveVerified`. The diagnostics header says nothing was live verified.
- Real path safety: export stems are `integration-{timestamp}` under `DataDirectory/diagnostics`, with `FileMode.CreateNew`. The stem is not user input, so it cannot traverse out of that directory. `Run_is_read_only_and_export_writes_only_to_diagnostics_folder` passed.

## QA-018

- **Severity:** P2
- **Status:** OPEN, non-blocking
- **Component:** `ConanExecutableClassifier.GetFileName`, `ServerProcessManager.StartCoreAsync`
- **Description:** A trailing `\` or `/` makes the classifier return an empty file name, so `ConanSandbox.exe\` and `Run Me!.bat\` are Unknown (Warning) instead of Fail, and that Warning does not block "READY FOR SERVER LIVE TEST". 8.3 names such as `CONANS~1.EXE` are also Unknown; `GetFullPath` expands short directory names only when the path exists, and the classifier itself does not. `StartCoreAsync` logs a warning and still launches an executable whose name is not `ConanSandboxServer`.
- **Why it does not block Task 4:** The canonical path `D:\conan exiles\Conan Exiles Enhanced\ConanSandbox.exe`, `Run Me!.bat`, scripts, and a workspace inside that client install are Fail and block server-live readiness. A guarded Task 4 uses the long dedicated-server path and does not Start the client.

## Task 4 guardrails

- Install SteamCMD in its own directory. Do not put it inside `D:\conan exiles\Conan Exiles Enhanced` or inside the dedicated-server workspace.
- Install or attach the dedicated server only as `ConanSandboxServer.exe` (or the shipping server binary) in a folder that does not contain `Run Me!.bat` or `ConanSandbox.exe`.
- Leave backup-before-update enabled. The online order is stop, confirm the process exited, cold backup, verify, then mutate, then start and wait for the readiness probe.
- Call the server Online only after the probe. UDP bind and RCON are not proof a player can join. Do not treat attach-to-an-existing-process as that probe.
- Run one Workshop mod test only after the dedicated server is Online. Copy mods to the standalone client manually; the app does not sync them.
- Do not click Diagnostics "Install SteamCMD" against the client folder. A diagnostics run while the server is already Online and RCON is configured sends one `listplayers` command.

## Prior IDs touched by this revision

- QA-011 start path is addressed, as above. Attach-without-probe remains a limitation inside that item.
- QA-013 self-compare is gone. See the status table.
- QA-014, QA-015, QA-016, and QA-017 are unchanged. QA-016 stays resolved. QA-017 stays the dashboard wording issue.
