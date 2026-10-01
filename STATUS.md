# Conan Server Control — Current Status

## Last Updated
2026-10-01 16:30 UTC

## Current Milestone
Stabilization of the existing update / backup / restore pipeline (QA retest of PR #1).

Delayed Restart, Wait Until Empty, `app_info_print`, first-run wizard, and new Web Admin features were **not** started.

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
- Update Server / Mods / Everything preserve original online/offline state (QA-003)
- `Validating → Completed` is a legal offline success transition (QA-004)
- Mods Update Selected / Update All use the global action gate (QA-005)
- Failed Update Everything restores a previously online process after an unchanged mod set (QA-006)
- `StartUnderLockAsync` / `StopUnderLockAsync` require `IServerOperationLease` (QA-008)
- Steam Workshop `result != 1` is ignored (QA-009)
- Web Admin `/api` returns 401 instead of a login redirect; mutations require CSRF (QA-010)

## Implemented but Not Fully Verified

- SteamCMD install / `app_update 443030` / workshop download — real ProcessStartInfo paths; not run against live SteamCMD here (Linux agent)
- Live ConanSandboxServer.exe start/stop
- Source RCON client — protocol implemented; needs a running server
- Restore on a real `game.db`
- Steam `GetPublishedFileDetails` over the real network (client is implemented; tests use a fake)

## Partially Implemented

- Dedicated-server “latest build” comparison still has no Steam depot query (`app_info_print` is deferred)
- `UpdateAllAsync` still re-downloads every enabled mod (UPDATE AVAILABLE vs VERIFY/RE-DOWNLOAD ALL is not split yet)
- Delayed restart still ignores `UpdateServer`/`UpdateMods` flags on the request object
- First-run wizard — Settings page is the substitute
- Mods UI is a text list + ID field, not drag-and-drop

## Placeholder / Mock / Stub

- Server INI editor (Server page text only)
- Tray icon / start with Windows
- Wait-until-empty automation
- Scheduled backup/update hosted services
- First-run 8-step wizard

## Known Bugs

- None remaining from the confirmed P0/P1 QA list (QA-001, 002, 003, 004, 005, 006, 007, 012) or from QA-008/009/010 in this suite.
- Residual: no remote Steam build id without `app_info_print`.
- Residual: Update All vs Update Available naming (QA observation / Architect recommendation). Not a safety blocker.

## Build Status

Solution build:
PASS

Tests:
95 passed
0 failed

## Current Architecture

.NET 8 WPF host + Core + Infrastructure + optional Web Admin.

`IServerActionGate.TryBegin` issues `IServerOperationLease`. Destructive start/stop under lock require that lease. Restore, update pipelines, and Mods Update Selected/All share the same gate. Workshop downloads use a fresh staging directory and fail closed before touching live paks. Backup retention accepts a protected backup set so restore cannot delete its source.

## Current Blockers

- No live Conan dedicated server or SteamCMD on this Linux agent

## Next Recommended Work

1. CONAN QA retest of this stabilization branch
2. After QA PASS: split UPDATE AVAILABLE MODS vs VERIFY / RE-DOWNLOAD ALL
3. Then, and only then: Delayed Restart / Wait Until Empty / `app_info_print`

## Last Completed Task

Stabilized the update / backup / restore pipeline against the QA FAIL report (82/62/20). All confirmed P0/P1 issues addressed. Full suite 95/95.

## Current Task

Hand off to CONAN QA for stabilization retest (`HANDOFF.md`).
