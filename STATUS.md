# Conan Server Control — Current Status

## Last Updated
2026-10-01 14:45 UTC

## Current Milestone
Phase 2 — Workshop mod management (update detection + real update pipelines)

## Implemented and Verified

- Solution structure (App / Core / Infrastructure / Web / Tests)
- Strongly typed settings JSON + DPAPI-protected secrets
- PBKDF2 Web Admin password hashing (unit tested)
- Workshop ID and path validation (unit tested)
- Mod list generation and reorder (unit tested)
- Backup retention policy (unit tested)
- Update pipeline state machine transitions (unit tested)
- ServerProcessManager start/stop/restart against a fake process (unit tested)
- SQLite activity log DateTime ordering (unit tested)
- Web Admin login, rate limit, cookie auth, backup API (manual browser test earlier)
- Action-gate: update pipeline can stop/start under an existing lease (unit tested)
- Workshop metadata comparison + `CheckForUpdatesAsync` with a fake Steam client (unit tested)

## Implemented but Not Fully Verified

- SteamCMD install / `app_update 443030` / workshop download — real ProcessStartInfo paths; not run against live SteamCMD here (Linux agent)
- Live ConanSandboxServer.exe start/stop
- Source RCON client — protocol implemented; needs a running server
- Restore on a real `game.db`
- Steam `GetPublishedFileDetails` over the real network (client is implemented; tests use a fake)

## Partially Implemented

- Dedicated-server “latest build” comparison still has no Steam depot query (`UpdateAvailable` for the **server** can be true only from Workshop flags after Check)
- `UpdateAllAsync` downloads every enabled mod, not only flagged ones
- Delayed restart still ignores `UpdateServer`/`UpdateMods` flags on the request object (dashboard delayed restart is backup+restart only)
- First-run wizard — Settings page is the substitute
- Mods UI is a text list + ID field, not drag-and-drop

## Placeholder / Mock / Stub

- Server INI editor (Server page text only)
- Tray icon / start with Windows
- Wait-until-empty automation
- Scheduled backup/update hosted services
- First-run 8-step wizard

## Known Bugs

- None confirmed in automated tests after the gate-deadlock fix.
- Residual P2: cannot display SERVER UPDATE AVAILABLE vs a remote Steam build id without an API key or `app_info_print` parser.

## Build Status

Solution build:
PASS

Tests:
29 passed
0 failed

## Current Architecture

.NET 8 WPF host + Core + Infrastructure + optional Web Admin. Non-reentrant `IServerActionGate`. Orchestrators that already hold the lease call `StartUnderLockAsync` / `StopUnderLockAsync`. Workshop details via `ISteamWorkshopClient` (Steam published-file API, no key).

## Current Blockers

- No live Conan dedicated server or SteamCMD on this Linux agent
- `QA_REPORT.md` / `ARCHITECTURE_REVIEW.md` still absent (see `HANDOFF.md`)

## Next Recommended Work

1. QA: Windows live SteamCMD + Workshop download + update-while-online
2. Parse SteamCMD `app_info_print` (or decide to skip remote build compare)
3. Delayed restart should honor update flags; wait-until-empty

## Last Completed Task

Fixed update-pipeline action-gate deadlock. Implemented Workshop published-file update detection. Wired UPDATE MODS / UPDATE EVERYTHING / Web Update Mods & Update Server to real services. Mods page: Enable/Disable/Move/Check/Update Selected/Update All.

## Current Task

Idle — hand off to CONAN QA (`HANDOFF.md`).
