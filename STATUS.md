# Conan Server Control — Current Status

## Last Updated
2026-10-03

## Operator decision 2026-10-03 (after D1 STOP B)

- **Fantasy Races Of Exiles: EXCLUDED / DEFERRED, no longer part of Modpack V1.** It reached readiness and did not corrupt the world, but produced 15 novel `NPC: Error: Data: No stat templates found for StatModifier template None.` lines, absent from all earlier healthy boots, which may affect NPC stats or spawn behavior. Gameplay impact cannot be ruled out, so it is excluded rather than accepting an unsafe warning. Not re-run, the 15 lines are not whitelisted, and the D1 logs and evidence are preserved for possible future investigation. Historical D1 records are unchanged.
- **Ancient Realms `MergeDataTables - ToBeAddedDataTable is null`: ACCEPTED as KNOWN NON-BLOCKING**, only with the exact signature, `Ancient_Realms.pak` SHA-256 `12F7E719…FD1A`, and at most 2 occurrences per boot. A third occurrence, a changed signature or a changed hash fails.
- **Accepted core (7):** StackMe10K, Savage Paragon, Grit & Grease, Thrall Reputation, Improved Thralls & QoL, WO Riding Thralls, Ancient Realms Enhanced. **Next candidates:** Cannibal Captivity Enhanced (Workshop 3765743138), then Shemite City State Enhanced (3755371705).

## Cannibal Captivity (2026-10-04): ACCEPTED server-side after a controlled rerun

Kept as the 8th mod. The 99 teardown warnings reproduced exactly (same signature and object-path family, same log positions, all after the main-world teardown began, none online, no other unknown lines, `quick_check` ok, singletons 1/1/1, NORMAL stops 199.2 s / 181.7 s) and are now **KNOWN NON-BLOCKING — SERVER-SIDE TEARDOWN WARNING**, bound to the exact pak hash `DB6E3C29…F04F`, the exact signature/path family, the teardown phase and an exact count of 99; any 100th line, pre-shutdown line, changed path/signature/hash or other unknown fails. In-game behavior NOT verified. Analysis is now reproducible from an immutable per-batch snapshot. Current load order: the seven plus `Cannibal_Captivity.pak`; latest verified backup `2026-10-04_001102`; server OFFLINE; Shemite City State not started. Details: `M3_LIVE_TEST_REPORT.md`.

## Cannibal Captivity first run (2026-10-03): STOP, rolled back (historical)

Boot (39.6 s), 10-minute hold, graceful stop (199.2 s NORMAL), `quick_check` and singleton gates all passed, but the complete-log scan found 99 identical `LogScript` warnings naming `/Game/Mods/Cannibal_Captivity/...` during world teardown. Per the operator rule (any UNKNOWN = rollback and STOP) the batch was rolled back and verified (DB/modlist hashes equal the seven-mod baseline); nothing was whitelisted. Shemite City State not started; server OFFLINE. Decision pending: accept that exact teardown warning, or exclude Cannibal Captivity. Details: `M3_LIVE_TEST_REPORT.md`.

## Latest unattended checkpoint

STOP B: Batch D1 reached readiness but full-log review found 15 new NPC stat-template errors. Rollback to the verified seven-mod world/order completed; server OFFLINE. D2/D3/final ten-mod validation not started. QA-019/020 and the reviewed integration are merged at 09cbf1a; safety and merged builds clean, 410/410 tests passed with zero skipped. Exact SDK 8.0.425 is installed user-locally; global.json unchanged. See FINAL_REPORT.md and AGENT_HANDOFF.md. New live work requires review of the D1 error and the harness scan coverage gap. Production world remains NOT CREATED.

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

Environment (operator decision, 2026-10-02):
- `D:\conan exiles\depot_443031` = **STAGING / PRE-PRODUCTION**.
- Current world = **TEST / VALIDATION WORLD**. Its `game_0.db` is not production data.
- **PRODUCTION SAVE: NOT CREATED YET.** It is created and frozen only after:
  1. all target mods are validated
  2. the final load order is accepted
  3. backup/restore is verified
  4. campaign systems are accepted
  5. the user explicitly approves starting the real campaign
- The production campaign starts on a **NEW** world. The current staging world is **never promoted** to production.
- Moving campaign-built content into the new world is conditional:
  - use a mod's export/import only if that mod or tool actually supports it, proven on staging
  - otherwise rebuild from the documented build sheet (`CAMPAIGN_V1.md` 10.4)
  - no migration capability is claimed until it is verified
- Only one session commits or pushes at a time.

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

Measured 2026-10-02 after the Batch B gates (`ModBootGates` and harness):

`dotnet build -c Release --no-incremental`:
PASS
0 warnings
0 errors

`dotnet test -c Release`:
344 passed
0 failed
344 total
0 skipped

(Earlier: `0d54acc` 313 / 313; `2aca0cf` 315 / 315; `982a5c3` 293 / 293. The .NET SDK 8.0.425 is the user-local install at `%LOCALAPPDATA%\Microsoft\dotnet`. The `dotnet` on PATH, `C:\Program Files\dotnet`, has no SDK.)

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

M3 Task 4E (one Local mod) is accepted as PASS.

Next: **Target Modpack V1** (`MODPACK_V1.md`). **Fifteen** mods are planned (the ten originals plus five campaign mods added 2026-10-02), and the metadata and conflict/test matrix are recorded. Nothing is installed yet.
- Cumulative batches: A (StackMe10K, which replaces WickStacks; Savage Paragon; Grit & Grease) → B → C → D1 / D2 / D3 → E (Sudo + Thrall Wars Utilities) → F1 (Night Terrors + PvE Plus Ambush) → F2 (Thrall Wars Dungeon Mod), then a review of all fifteen.
- **Batch A PASS** (2026-10-02, `2aca0cf`; `M3_LIVE_TEST_REPORT.md`): StackMe10K, Savage Paragon and Grit & Grease are installed on `depot_443031` in that order. The runtime mount order and container `Order` follow `modlist.txt` (PROVEN). Final verified backup `2026-10-02_074354`. StackMe10K's 10,000 stacks are not verified in-game.
- **Batch B accepted** (2026-10-02; `76ce7da` plus the 3-cycle restart test, in `M3_LIVE_TEST_REPORT.md`):
  - **SERVER-SIDE COMPATIBILITY: PASS**
  - **IN-GAME BEHAVIOR: NOT YET VERIFIED**
  - **ITQOL MAILBOX ISSUE: KNOWN NON-BLOCKING WARNING**
- **The known warning is one exact, version-bound rule** (`Core/LiveTesting/ModBootGates`). It is accepted only while the stopped world holds exactly 1 ITQoL mailbox. Any other ITQoL, BP_PL or mod error still fails.
- **Current staging order:** StackMe10K → SavageParagon → GritandGrease → ThrallReputation → ImprovedThrallsAndQoL → WO_RidingThralls.
- **Pre-Batch-B restore point `2026-10-02_141009`** is pinned at `E:\CSC-M3-Live\pinned-backups\` (outside retention, read-only, hash-verified).
- **Shutdown duration is a batch gate.** Batch B measured 177–184 s against a 300 s window. 240 s or more = HIGH RISK: stop before adding another batch.
- **Batch C (Ancient Realms) accepted 2026-10-03** (`M3_LIVE_TEST_REPORT.md` "Batch C"):
  - **BATCH C SERVER-SIDE COMPATIBILITY: PASS**
  - **ANCIENT REALMS GAMEPLAY: NOT YET VERIFIED**
  - **MAP / BUILDING / COLLISION BEHAVIOR: NOT YET VERIFIED**
  - The exact 7 / 37 AR `LoadErrors` signatures are a **KNOWN NON-BLOCKING WARNING** bound to `Ancient_Realms.pak` SHA-256 `12F7E719…FD1A`.
  - Invalidated by: a new AR signature, a changed hash, a crash, a save/persistence error, a world-integrity failure, or a missing/duplicate AR controller.
  - History: an early forced kill on a loaded host led to a rollback. The quiet-host retest passed 3/3.
  - Staging runs 7 mods with `ThrallDamageToNPCsMultiplier=0.3`.
- **Shutdown policy committed (`26091fa`):**
  - 240 s = HIGH-RISK batch gate.
  - Proven shutdowns (RCON acknowledgement or log progress) are not killed at 300 s; they get a 600 s emergency ceiling (`AdvancedSettings.EmergencyStopCeilingSeconds`; existing settings.json files get 600).
  - Unproven shutdowns keep the 30 s fallback.
  - 353 / 353 tests.
- **Harness classification code** (the AR exception rules, the exact `LoadErrors` set gate 6b, the AR controller gate): implemented, and committed only after its build and test. These are deferred until the shutdown-timing study ends, so the build does not disturb the measurements.
- **Open decision:** ITQoL's 23 `LoadErrors` per boot (Batch B) are **PENDING OPERATOR CLASSIFICATION**. They are monitored exactly and not accepted.
- **Batch D is not started**, and waits for the timing study to finish and the final shutdown policy to be committed and pushed.
- The final load order is not declared until all fifteen have been tested together with runtime evidence. The proposed starting order is in `CAMPAIGN_V1.md` section 3.

Then: **Chronicler Campaign V1** (`CAMPAIGN_V1.md`). This is design only; "Twelve Legends" is removed from the plan (it never existed on this host).
- An admin-configured PvE campaign: Acts I–IV, then Thrall Wars Normal and Hard.
- Built from Thrall Wars Utilities and Sudo CharVars. No DevKit.
- Built and QA'd on staging (`depot_443031`, a test/validation world). The production world is created only after the acceptance criteria and explicit user approval (see "Deployment Model").
- In-game building and QA are **blocked by 4F** (no authenticated admin client) and by the missing `.pak` files.

## Last Completed Task

M3 Task 4 pre-live fixes, 4A SteamCMD, and the standalone-first pivot (Local mods, Client Mod Bundle, optional SteamCMD).

## Current Task

Matched-version server verified. 4F recorded as blocked by client authentication. Pre-4E validation done (`0d54acc`): acknowledgement-gated graceful stop with process-tree Offline, Radmin/LAN address split, 313 / 313 tests, live 10-minute stop PASS (154 s, exit 0, no kill, no WAL, no orphans). 4E PASS (accepted). Target Modpack V1 (`MODPACK_V1.md`, now fifteen mods): Batch A PASS (`2aca0cf`); Batch B PASS server-side (accepted; ITQoL mailbox = known non-blocking warning); Batch C (Ancient Realms) PASS server-side after a quiet-host retest (exact AR LoadErrors = known non-blocking, bound to the file hash; gameplay and map/building/collision not verified); Batch D waits for the shutdown-timing study. Chronicler Campaign V1 designed (`CAMPAIGN_V1.md`): no in-game QA has run, and the design work changed nothing on the server. Do not modify the real client, and do not configure a public Internet server.
