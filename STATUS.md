# Conan Server Control — Current Status

## Last Updated
2026-10-01 16:50 UTC

## Current Milestone
QA-016 only: fail-closed live Workshop mod rollback when verification throws.

Delayed Restart, Wait Until Empty, `app_info_print`, first-run wizard, Update Available vs Verify All, and new Web Admin features were **not** started.

## Implemented and Verified

- Solution structure (App / Core / Infrastructure / Web / Tests)
- Strongly typed settings JSON + DPAPI-protected secrets
- PBKDF2 Web Admin password hashing
- Workshop ID and path validation
- Mod list generation and reorder
- Backup retention with a protected-id set (QA-001)
- Restore refuses to delete its source and fails closed on copy errors (QA-001)
- Incomplete world backups fail instead of logging success (QA-007)
- Restore takes the same action gate as Start/Stop/Update (QA-012)
- Workshop staging is wiped, validated (single non-empty current `.pak`), then replaced (QA-002)
- Multi-mod updates stage and validate every target before any live replacement (QA-002 / QA-006)
- Live multi-mod commit copies every current live pak into an isolated rollback directory, then replaces one at a time. Any replacement failure rolls back every already-changed pak (QA-016)
- Rollback restore, verification, and `FilesEqual` share one fail-closed boundary. Any throw becomes recovery-required (QA-016)
- Failed live commit with a **verified** rollback may restart a previously online server onto the restored old set, and still reports FAILED (QA-016)
- Failed live commit whose rollback fails, cannot be verified, or throws leaves the server OFFLINE (`recovery required`) even if it was online before (QA-016)
- Update Server / Mods / Everything preserve original online/offline state when a safe set can be restored (QA-003)
- `Validating → Completed` is a legal offline success transition (QA-004)
- Mods Update Selected / Update All use the global action gate (QA-005)
- Failed Update Everything restores a previously online process after an unchanged or rolled-back mod set (QA-006 / QA-016)
- `StartUnderLockAsync` / `StopUnderLockAsync` require `IServerOperationLease` (QA-008)
- Steam Workshop `result != 1` is ignored (QA-009)
- Web Admin `/api` returns 401 instead of a login redirect; mutations require CSRF (QA-010)

## Implemented but Not Fully Verified

- SteamCMD install / `app_update 443030` / workshop download — real ProcessStartInfo paths; not run against live SteamCMD here (Linux agent)
- Live ConanSandboxServer.exe start/stop
- Source RCON client — protocol implemented; needs a running server
- Restore on a real `game.db`
- Steam `GetPublishedFileDetails` over the real network (client is implemented; tests use a fake)
- Live Windows File.Replace / locked `.pak` during a real Conan process (covered by unit IO failures, not a running dedicated server)

## Partially Implemented

- Dedicated-server “latest build” comparison still has no Steam depot query (`app_info_print` is deferred)
- `UpdateAllAsync` still re-downloads every enabled mod (UPDATE AVAILABLE vs VERIFY/RE-DOWNLOAD ALL is not split yet)
- Delayed restart still ignores `UpdateServer`/`UpdateMods` flags on the request object
- First-run wizard — Settings page is the substitute
- Mods UI is a text list + ID field, not drag-and-drop
- Steam dedicated-server binaries are not rolled back if `app_update` succeeds and a later mod commit fails

## Placeholder / Mock / Stub

- Server INI editor (Server page text only)
- Tray icon / start with Windows
- Wait-until-empty automation
- Scheduled backup/update hosted services
- First-run 8-step wizard

## Known Bugs

- None remaining from the confirmed P0/P1 QA list (QA-001 through QA-010, QA-012, QA-016) in this suite.
- Residual: no remote Steam build id without `app_info_print`.
- Residual: Update All vs Update Available naming. Not a safety blocker.

## Build Status

Solution build:
PASS
0 warnings
0 errors

Tests:
108 passed
0 failed
108 total

## Current Architecture

.NET 8 WPF host + Core + Infrastructure + optional Web Admin.

`IServerActionGate.TryBegin` issues `IServerOperationLease`. Destructive start/stop under lock require that lease. Restore, update pipelines, and Mods Update Selected/All share the same gate. Workshop downloads use a fresh staging directory and fail closed before touching live paks. A multi-mod live commit uses `ModBatchTransaction` plus `RollbackResult`: isolated rollback copies, replace one-by-one, and treat any rollback/verification exception as recovery-required. The server restarts after a failed update only when live mods were never mutated or when rollback is positively verified.

## Current Blockers

- No live Conan dedicated server or SteamCMD on this Linux agent

## Next Recommended Work

1. CONAN QA final retest of QA-016
2. After QA PASS: split UPDATE AVAILABLE MODS vs VERIFY / RE-DOWNLOAD ALL
3. Then, and only then: Delayed Restart / Wait Until Empty / `app_info_print`

## Last Completed Task

QA-016: fail-closed rollback when verification throws. Restart only after positively verified rollback. Full suite 108/108.

## Current Task

Hand off to CONAN QA for QA-016 final retest (`HANDOFF.md`).
