# Conan Server Control — Current Status

## Last Updated
2026-10-02

## Current Milestone
M3 — Live Windows Integration & Diagnostics, **Task 4 in progress** (guarded live integration), with the product direction now **standalone-first**.

Checkpoint **4C/4D PASSED** on the existing dedicated server `D:\conan exiles\Conan Exiles Dedicated Server`: boot, readiness, Start/Stop/Restart, graceful RCON stop, and a verified cold backup of the real Enhanced world. Waiting for review before 4E. See `M3_LIVE_TEST_REPORT.md`.

## Implemented and Verified

- Tasks 1–3 (accepted by QA at `0352f40`).
- **Hard server-executable gate**: only `ConanSandboxServer.exe` outside the client folder can start. Everything else is blocked (unit-tested).
- **QA-018**: normalized path comparison (trailing separator, slash and case).
- **Live harness and guard**: `CSC_LIVE_TESTS=1` plus the `.csc-live-test` marker; layout overlap validation; destructive-action refusal (unit-tested, and verified live against real paths).
- **Local mods** (no SteamCMD): copy-to-staging, SHA-256, locked pipeline with verified cold backup, transactional commit with rollback, manual update (unit-tested).
- **Client Mod Bundle export** plus a read-only client sync planner (unit-tested, temp folders only).
- **Optional SteamCMD**: readiness accepts a valid existing server without SteamCMD; Settings **Use existing server installation**.
- **Live:** SteamCMD install into `E:\CSC-M3-Live\steamcmd` (4A PASS).

## Implemented but Not Fully Verified

- Dedicated server boot, readiness, Start/Stop/Restart, real world backup, server mod load (4C–4E).
- Client bundle applied to a real standalone client, and client join (4F).
- Workshop download / SteamCMD `app_update` on this host: **blocked by network** (Valve Fastly CDN TLS reset). Optional integration; not a core blocker.

## Placeholder / Mock / Stub

- Server INI editor, tray, wait-until-empty, scheduled backup/update, first-run wizard.
- Web Admin endpoints for Local mods and bundles.
- QA-014, QA-015, QA-017, QA-018 UI items listed by QA as non-blocking debt (the QA-018 path normalization itself is fixed).

## Known Bugs / Risks

- None open at P0/P1.
- Attach-to-existing still matches any `ConanSandboxServer` process (the harness refuses to start when one exists).
- Standalone client join may require Steam authentication. It is unknown and will be observed in 4F. No authentication bypass will be implemented.
- Pre-existing: the Settings Web Admin password field is a plain TextBox.

## Build Status

`dotnet build -c Release --no-incremental`:
PASS
0 warnings
0 errors

`dotnet test -c Release`:
272 passed
0 failed
272 total
0 skipped

## Current Architecture

Unchanged gate / lease / readiness / cold-backup / `ModBatchTransaction`. Additions:
- `ServerExecutableGate`
- `LiveTestGuard`
- `ModSourceType` + `ModKeys`
- `IModCatalogService` (Local import/replace, key-based catalog operations)
- `IServerUpdateService.ImportLocalModAsync` / `ReplaceLocalModAsync` (same locked pipeline)
- `IClientModBundleService` + `ClientModSyncPlanner`
- `DedicatedServerLocator`

## Current Blockers

- **A valid existing Conan Exiles Dedicated Server installation (`ConanSandboxServer.exe`) is required for 4C.** SteamCMD download of app 443030 is blocked by this network.

## Next Recommended Work

Provide or copy a legitimate dedicated server install outside `D:\conan exiles\`, then continue M3 Task 4C–4E. 4F (client observation) follows a working modded server.

## Last Completed Task

M3 Task 4 pre-live fixes, 4A SteamCMD, and the standalone-first pivot (Local mods, Client Mod Bundle, optional SteamCMD).

## Current Task

Waiting for a dedicated server installation. Do not modify the real client.
