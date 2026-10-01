# Conan Server Control — Current Status

## Last Updated
2026-10-02

## Current Milestone
M3 — Live Windows Integration & Diagnostics, **Task 4 in progress** (guarded live integration). The product is **standalone-first on the server/management side** (see "Terminology").

Checkpoint **4E PASSED** (one real Local `.pak`, `WickProbe.pak`: import, positive server-side mount/load evidence, clean stop, integrity, removal, clean restart). Checkpoint **4C/4D PASSED** on an existing dedicated server: boot, readiness, Start/Stop/Restart, graceful RCON stop, and a verified cold backup of the real Enhanced world. Waiting for review before 4E. See `M3_LIVE_TEST_REPORT.md`.

**4F CLIENT JOIN = BLOCKED BY CLIENT AUTHENTICATION.** This is not a network failure, a server failure or a version mismatch. 4E is not blocked by it.

## Deployment Model

PROJECT DEPLOYMENT TARGET: **private friends-only dedicated server over Radmin VPN.**
- approximately 5 players
- Radmin VPN virtual LAN
- direct connection over the Radmin/private IP, when the client is authenticated
- no public server browser requirement
- no public IP exposure requirement
- no router port forwarding requirement, unless explicitly requested later
- no UPnP requirement
- RCON must remain private/local
- public FLS server registration is **not** a release criterion

The server's `Autologin attempt failed, unable to register server!` is **NOT A BLOCKER** for this deployment, provided authenticated clients can direct-connect. No project time is spent making the server public. See HANDOFF "Deployment model".

## Terminology: standalone-first

Conan Server Control is standalone-first on the **SERVER/MANAGEMENT** side. SteamCMD and the Steam client are not required for normal server-management operations once a valid Dedicated Server installation exists.

Local `.pak` mods and Client Mod Bundles may be managed independently of Workshop.

However, multiplayer clients must use a legitimate Conan client session capable of obtaining the platform authentication token required by Funcom Live Services. Radmin VPN does not replace FLS/platform authentication.

## Server-Side Goals (unchanged by the 4F block)

- Start / Stop / Restart
- real readiness detection
- cold verified backups
- restore
- RCON
- Local `.pak` import
- transactional mod install / rollback
- mod load order
- Client Mod Bundle
- diagnostics
- delayed restart
- wait-until-empty
- Web Admin / phone control (later)

## Implemented and Verified

- Tasks 1–3 (accepted by QA at `0352f40`).
- **Hard server-executable gate**: only `ConanSandboxServer.exe` outside the client folder can start. Everything else is blocked (unit-tested).
- **QA-018**: normalized path comparison (trailing separator, slash and case).
- **Live harness and guard**: `CSC_LIVE_TESTS=1` plus the `.csc-live-test` marker; layout overlap validation; destructive-action refusal (unit-tested, and verified live against real paths).
- **Local mods** (no SteamCMD): copy-to-staging, SHA-256, locked pipeline with verified cold backup, transactional commit with rollback, manual update (unit-tested).
- **Client Mod Bundle export** plus a read-only client sync planner (unit-tested, temp folders only).
  - It distributes **only** `.pak` mod files, the modlist, manifest/hash metadata, and permitted configuration material.
  - It does **not** distribute the game client, provide authentication, replace a platform license, bypass FLS, or modify Steam authentication.
- **Optional SteamCMD**: readiness accepts a valid existing server without SteamCMD; Settings **Use existing server installation**.
- **Live:** SteamCMD install into `E:\CSC-M3-Live\steamcmd` (4A PASS).
- **Live:** existing dedicated server boot, real readiness, Start/Stop/Restart, graceful RCON stop, verified cold backup (4C/4D PASS).

## Implemented but Not Fully Verified

- Server mod load with one Local mod (4E). Not blocked by the client.
- Client bundle applied to a real client, and client join (4F): **BLOCKED BY CLIENT AUTHENTICATION**.
- Workshop download / SteamCMD `app_update` on this host: **blocked by network** (Valve Fastly CDN TLS reset). Optional integration; not a core blocker.

## Placeholder / Mock / Stub

- Server INI editor, tray, wait-until-empty, scheduled backup/update, first-run wizard.
- Web Admin endpoints for Local mods and bundles.
- QA-014, QA-015, QA-017, QA-018 UI items listed by QA as non-blocking debt (the QA-018 path normalization itself is fixed).

## Known Bugs / Risks

- None open at P0/P1.
- Attach-to-existing still matches any `ConanSandboxServer` process (the harness refuses to start when one exists).
- 4F confirmed: the observed client (`D:\conan exiles\Conan Exiles Enhanced`) cannot join. Its log says `Steam auth token not available`, FLS login fails, and it falls back to offline/single-player mode. Its installation contains Steam-emulation artifacts. They were not modified, and no authentication bypass will be attempted.
- Pre-existing: the Settings Web Admin password field is a plain TextBox.

## Build Status

Measured 2026-10-02 at `0d54acc` (pre-4E validation):

`dotnet build -c Release --no-incremental`:
PASS
0 warnings
0 errors

`dotnet test -c Release`:
313 passed
0 failed
313 total
0 skipped

(`982a5c3` alone, before the pre-4E fixes: build PASS, 293 / 293.)

## Current Architecture

Unchanged gate / lease / readiness / cold-backup / `ModBatchTransaction`. Additions:
- `ServerExecutableGate`
- `LiveTestGuard`
- `ModSourceType` + `ModKeys`
- `IModCatalogService` (Local import/replace, key-based catalog operations)
- `IServerUpdateService.ImportLocalModAsync` / `ReplaceLocalModAsync` (same locked pipeline)
- `IClientModBundleService` + `ClientModSyncPlanner`
- `DedicatedServerLocator`
- `IServerShutdownProbe` / `ConanLogShutdownProbe` (server-log shutdown evidence) and `ServerRuntimeState.LastStop`
- `ProcessTree` (launcher → `-Shipping` child tracking for Stop)
- `NetworkAddressSelector` (physical LAN vs Radmin VPN)

## Current Blockers

- None for 4E (one Local mod). Waiting for checkpoint review.
- SteamCMD download (optional) is still blocked by network (Valve Fastly CDN).
- Version mismatch resolved: the server at `D:\conan exiles\depot_443031` is CL-377096 (2.2.2), the same as the client. Boot, graceful stop and cold backup PASS.
- **4F CLIENT JOIN = BLOCKED BY CLIENT AUTHENTICATION.** It resumes only with a legitimate Conan client session that can obtain the FLS platform token (for example a licensed Steam copy). Client bypass investigation is closed.
- `Autologin attempt failed, unable to register server!` is **not a blocker** for the private Radmin deployment.

## Next Recommended Work

After review: M3 Task 4E, one Local `.pak` mod on the real server. It is verified server-side only, with no client connection:
- backup before mutation (verified cold backup)
- transactional install
- `modlist.txt`
- server startup
- real readiness
- server log evidence that the mod loaded
- rollback verification

## Last Completed Task

M3 Task 4 pre-live fixes, 4A SteamCMD, and the standalone-first pivot (Local mods, Client Mod Bundle, optional SteamCMD).

## Current Task

Matched-version server verified. 4F recorded as blocked by client authentication. Pre-4E validation done (`0d54acc`): acknowledgement-gated graceful stop with process-tree Offline, Radmin/LAN address split, 313 / 313 tests, live 10-minute stop PASS (154 s, exit 0, no kill, no WAL, no orphans). Waiting for review before 4E (one Local mod). Do not modify the real client, and do not configure a public Internet server.
