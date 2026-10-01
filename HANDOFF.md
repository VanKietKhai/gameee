# Handoff to CONAN QA — Stabilization Retest

## QA Issues

QA-001:
FIXED

Restore now passes the selected backup id and the in-progress safety backup into `ApplyRetentionAsync(protectedBackupIdsOrPaths)`. Retention cannot delete the restore source. After the safety backup, restore re-checks that the source still exists, copies world files (copy failures become `UserFacingException`), and logs `"Restored backup …"` only after a successful copy.

QA-002:
FIXED

Each Workshop download wipes `staging/workshop/<id>` first. Validation requires exactly one `.pak`, size > 0, produced by this attempt. Multiple `.pak` files fail closed. The live pak is not replaced until validation succeeds. `UpdateAll` / `ApplyUpdatesAsync` stage every target before any live replacement.

QA-003:
FIXED

`RunLockedAsync` captures `wasRunning` and starts the process only when it was originally online. Dashboard and Web Update Server / Update Mods no longer pass `restartAfter: true` as a start request. Update Everything also stays offline if it started offline.

QA-004:
FIXED

`Validating → Completed` is an explicit legal transition for the offline success path. Online updates still go Validating → Starting → HealthCheck → Completed. Failed validation still ends in Failed.

QA-005:
FIXED

Mods Update Selected and Update All take the same `IServerActionGate`. The locked update pipeline calls `ApplyUpdatesAsync` so it does not nest `TryBegin`. Check Updates / metadata remain ungated.

QA-006:
FIXED (mod batch; no fake server-binary rollback)

Mods are staged and validated as a set before replacement, so a later staging failure leaves both live paks unchanged. If the server was originally online, a failed run starts it again after the unchanged mod set. Server SteamCMD success is not rolled back; the operation still returns FAILED.

QA-007:
FIXED

`BackupNowAsync` requires a world/save copy. Missing `ConanSandbox/Saved`, copy errors, and unwritable destinations throw `UserFacingException`, discard the incomplete folder, and never log `"Backup completed"`.

QA-012:
FIXED

Restore calls `TryBegin("Restore backup")` around the whole operation, including the safety backup. A busy lease is rejected. Failures release the gate in `finally`. Restore still refuses while status is Online (existing passing test). `startAfter` uses `StartUnderLockAsync(lease)`.

## Architect Findings Addressed

- F-1 / QA-008: Under-lock start/stop require `IServerOperationLease`; a foreign or missing lease throws `InvalidOperationException`.
- F-2 / QA-005: Mods page updates go through the global gate (ViewModel uses `IServerUpdateService`; `WorkshopModService.UpdateAsync`/`UpdateAllAsync` also take the gate).
- F-3 / QA-002: Fresh staging + validation before replacement.
- F-4 / QA-003: Offline stays offline across Update Server / Mods / Everything.
- F-5 / QA-006: Partial mod set no longer committed; previously online process is restored after failure. Server binary is not rolled back.
- F-6 / QA-009: Steam `result != 1` is skipped; missing details keep the installed name and set Error.
- F-7 / QA-001: Restore source protected from retention.
- F-9 / QA-010 (partial): Web `/api` returns 401 (so `site.js` no longer follows a login redirect). SteamCMD cancel-on-HTTP-abort was not changed.
- QA-010: CSRF required on authenticated mutations; `/api/me` requires auth; activity/player/log lists use `textContent`.

Not done (explicitly deferred):

- `app_info_print` / remote dedicated-server build id
- Delayed Restart / Wait Until Empty
- First-run wizard / UI polish
- Splitting UPDATE AVAILABLE MODS vs VERIFY / RE-DOWNLOAD ALL (current `UpdateAllAsync` still re-downloads every enabled mod)
- Backup-before-stop (cold backup) as a separate workflow change
- Enable/Disable/Move still do not take the destructive gate

## Files Changed

- `src/ConanServerControl.Core/Abstractions/IAppPaths.cs` — `IServerOperationLease`
- `src/ConanServerControl.Core/Abstractions/IServerProcessManager.cs` — lease-typed UnderLock, `ApplyRetentionAsync` protected set, `ApplyUpdatesAsync`, `UpdateSelectedModsAsync`
- `src/ConanServerControl.Core/Backups/BackupRetentionPolicy.cs`
- `src/ConanServerControl.Core/Updates/UpdatePipelineStateMachine.cs`
- `src/ConanServerControl.Infrastructure/Backups/BackupService.cs`
- `src/ConanServerControl.Infrastructure/Concurrency/ServerActionGate.cs`
- `src/ConanServerControl.Infrastructure/ProcessManagement/ServerProcessManager.cs`
- `src/ConanServerControl.Infrastructure/Updates/ServerUpdateService.cs`
- `src/ConanServerControl.Infrastructure/Workshop/WorkshopModService.cs`
- `src/ConanServerControl.Infrastructure/Workshop/SteamWorkshopClient.cs`
- `src/ConanServerControl.App/ViewModels/DashboardViewModel.cs`
- `src/ConanServerControl.App/ViewModels/SettingsViewModel.cs`
- `src/ConanServerControl.Web/WebAdminExtensions.cs`
- `src/ConanServerControl.Web/wwwroot/js/site.js`
- `tests/ConanServerControl.Tests/Qa*.cs` (imported from QA PR #2, not merged as a branch)
- `tests/ConanServerControl.Tests/BackupAndPipelineTests.cs`
- `STATUS.md`

QA/Architect review branches were **not** merged.

## Regression Tests

Imported QA suite plus additional cases for protected retention, restore copy failure, valid Workshop replacement, Update All gate, lease ownership, restore gate release.

Two QA assertions were aligned with the required product design (documented in the tests):

- Missing Workshop staging directory is now an empty validated staging result (`UserFacingException`), not `DirectoryNotFoundException`, because staging is created fresh before SteamCMD.
- Restore/Update Selected gate tests now share the same `IServerActionGate` instance the service is constructed with. A disconnected `new ServerActionGate()` cannot observe global coordination.

Do not delete the `QA-KnownFailure` traits; they remain as issue tags.

## Build

Release:
PASS

## Tests

95 passed
0 failed
95 total

(Baseline was 82 total / 62 passed / 20 failed. New regression tests were added; none of the QA regressions were deleted.)

## Live Tests Still Needed

SteamCMD:
Windows host, real `steamcmd.exe`, `app_update 443030`, Workshop download of a known item, cancel during download (F-9 process kill still open).

Real Conan server:
Start / stop / update while a world is loaded. Confirm offline update stays offline. Confirm restore refuses while the process is writing `game.db`.

Workshop:
Item with one `.pak`, item that fails validation, Update All with two mods where the second download fails (live paks unchanged).

Web Admin:
Login, CSRF header on Update Mods/Server from the real `site.js`, 401 when signed out.

## Remaining Risks

- SteamCMD is still not killed when the HTTP request is aborted (Architect F-9 remainder).
- Pre-update backup still runs before stop (live SQLite world).
- Enable/Disable/Move still rewrite `modlist.txt` without the destructive gate.
- Update All still re-downloads every enabled mod.
- No live SteamCMD / Conan / Workshop verification on this Linux agent.
- Server binary is not rolled back if it updates successfully and a later step fails; backup is preserved.

Do **not** start Delayed Restart or Wait Until Empty until this retest PASSes.
