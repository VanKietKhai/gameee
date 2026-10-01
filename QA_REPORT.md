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
- **Component:** `BackupService.BackupNowAsync`
- **Description:** If the install directory exists but `ConanSandbox/Saved` does not, the backup still creates a folder, writes metadata with `IncludesWorld = false`, and logs `Backup completed`. The update pipeline treats that as a successful pre-update backup and continues. The dashboard alerts “Backup created” whenever the method returns.
- **Steps to Reproduce:** Set the install directory to an empty temp folder. Call `BackupNowAsync("pre-server-update")`.
- **Expected:** The method throws. The activity log does not say the backup completed.
- **Actual:** No exception. Activity: `Backup completed (2026-10-01_150056).`
- **Evidence:** `Backup_that_copies_no_world_must_not_be_reported_as_success`. `BackupService.cs:60-90`.
- **Suggested Fix Direction:** If the world directory was not copied, throw `UserFacingException` and do not write the “Backup completed” activity line. Let the update pipeline fail before stop/SteamCMD.

### QA-012

- **Severity:** P1
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
- **Component:** `BackupService.BackupNowAsync` path check
- **Description:** The escape check compares `Path.GetFullPath(dest)` to itself, so the branch never throws. Backup folder names are timestamp strings today, so this is dead code rather than a reached escape.
- **Steps to Reproduce:** Read `BackupService.cs:39-43`.
- **Expected:** A destination outside the backups root throws.
- **Actual:** The second comparison is always false, so the `if` is always false.
- **Evidence:** `BackupService.cs:39-43`.
- **Suggested Fix Direction:** Throw when `!PathValidator.IsUnderRoot(dest, _paths.BackupsDirectory)`.

### QA-014

- **Severity:** P3
- **Component:** Settings flags that are not read
- **Description:** `BackupBeforeLoadOrderChange` defaults to true and has no readers. Move Up / Move Down confirm in the UI and do not back up. `DelayedRestartService.FinishAsync` backs up when `BackupFirst` is set and then calls `RestartAsync`. It does not read `DelayedRestartRequest.UpdateServer` or `UpdateMods`.
- **Steps to Reproduce:** Search for `BackupBeforeLoadOrderChange` (definition only, `AppSettings.cs:124`). Read `DelayedRestartService.FinishAsync` (`ServerUpdateService.cs:329-337`).
- **Expected:** A load-order change with the flag on creates a backup. A delayed restart with update flags runs those updates.
- **Actual:** Move only rewrites `modlist.txt`. Delayed restart is backup (optional) plus restart.
- **Evidence:** Grep shows a single hit for `BackupBeforeLoadOrderChange`. `FinishAsync` as cited. UI confirm text is in `SettingsViewModel.cs` Move Up / Move Down.
- **Suggested Fix Direction:** Either call `BackupNowAsync` from `MoveAsync` when the flag is set, or stop defaulting the flag to true until it is wired. Honor the delayed-restart update flags or stop exposing them on the request object.

### QA-015

- **Severity:** P3
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
