# Conan Server Control — Current Status

## Codex takeover preflight — 2026-10-04

Accepted state remains **11 mods**, server **OFFLINE**, production world **NOT CREATED**. Branch `claude/m3-task4-live-windows` at `26f7a1c` matched the remote after fetch. Tracked files were clean; pre-existing untracked work was preserved.

The accepted 11-mod order, every package hash and size match snapshot `p13-dbno-1`, with no extra installed packages. The live database is byte-identical to accepted backup `2026-10-04_062716`; WAL is empty. A hash-verified copy returned `quick_check=ok`, with live files rechecked unchanged. Required mailbox/controller singleton counts are 1/1/1; Player DBNO has one controller, and all 30 persisted controller IDs are unique. The inherited WickProbe actor is unchanged baseline state. Server log build: CL-377096. Detailed hashes and operational evidence remain local.

No server or harness process or server-related scheduled task was found; the host was reasonably quiet. Build PASS, no warnings/errors. Initial sandboxed tests: 489 passed and 2 failed in web-admin/process inspection checks. With required process/network access, all **491 tests passed**, none failed/skipped; the test report is retained locally.

**Exclusive control confirmed by the operator:** the prior validation session is paused/finished; only Codex may operate the server and publish validation results during this phase. The initial preflight waited for this confirmation because the application's action gate coordinates only within one process.

**Next action:** recheck mutable state, then run THREE unchanged 11-mod control boots, each with a verified pre-run backup, immutable evidence, at least 10-minute hold, graceful shutdown and integrity/persistence checks. The warning catalog stays unchanged for all three. Control boots 1/2/3 NOT RUN at this checkpoint; neither priest ID is assessed by new control evidence. No catalog change proposed/applied. Simple Minimap and Chest Labels remain deferred; retests and final 13-mod validation NOT RUN. Latest accepted verified backup: `2026-10-04_062716`. Safe to start Custom Main Questline: NO. Client and source packages unchanged.

## Last Updated
2026-10-03

## Operator decision 2026-10-03 (after D1 STOP B)

- **Fantasy Races Of Exiles: EXCLUDED / DEFERRED, no longer part of Modpack V1.** It reached readiness and did not corrupt the world, but produced 15 novel `NPC: Error: Data: No stat templates found for StatModifier template None.` lines, absent from all earlier healthy boots, which may affect NPC stats or spawn behavior. Gameplay impact cannot be ruled out, so it is excluded rather than accepting an unsafe warning. Not re-run, the 15 lines are not whitelisted, and the D1 logs and evidence are preserved for possible future investigation. Historical D1 records are unchanged.
- **Ancient Realms `MergeDataTables - ToBeAddedDataTable is null`: ACCEPTED as KNOWN NON-BLOCKING**, only with the exact signature, `Ancient_Realms.pak` SHA-256 `12F7E719…FD1A`, and at most 2 occurrences per boot. A third occurrence, a changed signature or a changed hash fails.
- **Accepted core (7):** StackMe10K, Savage Paragon, Grit & Grease, Thrall Reputation, Improved Thralls & QoL, WO Riding Thralls, Ancient Realms Enhanced. **Next candidates:** Cannibal Captivity Enhanced (Workshop 3765743138), then Shemite City State Enhanced (3755371705).

## #14 Chest Labels (2026-10-04): run 1 clean, restart run showed intermittent spawn-table ids; rolled back (INCONCLUSIVE); accepted pack = 11 mods

Chest Labels (`ChestLabels.pak`, Workshop 3735258746, SHA-256 `C79C7E00…0CD8`): run 1 was clean (readiness 33.1 s, 660.5 s hold, 0 unknown, no new LoadErrors, one controller object, shutdown NORMAL 152.3 s, `quick_check` ok). An extra restart boot of the same state logged two unvalidated weighted-table ids once each (`Exile_Priest_4_Hyrkanian`, `Exile_OrchidPriest_4_Nordheimer`), so per the stop-on-unknown rule it was rolled back to the verified pre-#14 backup `2026-10-04_062824` (11-mod state exact, live DB identical to `2026-10-04_062716`). **`Exile_Priest_4_Hyrkanian` has now also appeared without Simple Minimap installed**, so it is not minimap-specific and looks like intermittent base-game variance; nothing was whitelisted and Simple Minimap stays deferred pending the operator. Accepted pack: the 10 plus Player DBNO (11 mods). Server OFFLINE; gameplay NOT YET VERIFIED; production world NOT CREATED. Details: `M3_LIVE_TEST_REPORT.md`.

## #13 Player DBNO (2026-10-04): PASS server-side (11 mods installed)

Player DBNO (`PlayerDBNO.pak`, Workshop 3718882569, SHA-256 `3E7FEEED…FD65`) on the 10-mod baseline: readiness 35.3 s, 660.6 s hold, complete-log analysis 0 unknown, no new LoadErrors (60 vs 60), no warning/error naming it, one new controller object (`PNO_MC_ModController_C`), shutdown NORMAL 177.4 s, `quick_check` ok, singletons 1/1/1. Verified backups: pre-#13 `2026-10-04_061131`, post-#13 `2026-10-04_062716`. Gameplay (down/revive) NOT YET VERIFIED. Next: #14 Chest Labels. Production world NOT CREATED.

## Step A control boot (2026-10-04): `Exile_Priest_4_Hyrkanian` ABSENT in the unchanged 10-mod baseline; Simple Minimap stays FAIL / DEFERRED

The control boot of the accepted 10 mods (batch `ctrl10-1`) passed everything (readiness 34.2 s, 660.4 s hold, 0 unknown, LoadErrors 60 vs 60, only the two accepted spawn-table ids, controllers one each, shutdown NORMAL 159.6 s, `quick_check` ok) and did **not** contain `Exile_Priest_4_Hyrkanian`, which appears only in the Simple Minimap boot. Per the operator rule Simple Minimap stays FAIL / DEFERRED (its LoadError `C88E5FE76A79516D` and the spawn-table line are not whitelisted; no rerun this phase; one control boot does not prove causation). Verified backup `2026-10-04_061022`. #13 Player DBNO and #14 Chest Labels proceed independently on the 10-mod baseline. Details: `M3_LIVE_TEST_REPORT.md`.

## #12 Simple Minimap (2026-10-04): FAIL / STOP, rolled back; #13 and #14 NOT RUN

Simple Minimap (`Simple_Minimap.pak`, Workshop 3719513784, SHA-256 `04F31A75…6AC9`) loaded and ran stably (readiness 34.2 s, 660.4 s hold, shutdown NORMAL 172.4 s, `quick_check` ok, one new controller) but logged 1 new LoadError (package None -> id `C88E5FE76A79516D`, present only in its container) and 1 new spawn-table error (`Exile_Priest_4_Hyrkanian`, seen in no earlier boot, cause unattributed). Nothing whitelisted; rolled back to the verified pre-#12 backup `2026-10-04_052716` and verified (10-mod state exact, live DB identical to `2026-10-04_035205`). #13 and #14 not run (the stated rule stops the series on an unknown). Failed world: backup `2026-10-04_054302`. Server OFFLINE; accepted pack = the 10 mods; gameplay NOT YET VERIFIED; production world NOT CREATED. Decisions pending; details in `M3_LIVE_TEST_REPORT.md`.

## Operator decision 2026-10-04: #11 WO - Room For One More EXCLUDED / DEFERRED; #12-#14 continue independently

Not whitelisted: its 5 `MergeDataTables - ToBeAddedDataTable is null` errors. Reasons: five new unvalidated merge errors right after its controller registered, its controller blueprint name overlaps with Riding Thralls' (`WO_BP_RT_ModController_C`), the mod itself warns about compatibility risk with mount/passenger mods, and gameplay compatibility cannot be verified. Not revisited in this phase; all #11 evidence, snapshot `p11-roomforone-1` and the failed-world backup `2026-10-04_050719` are kept. **#12 Simple Minimap, #13 Player DBNO and #14 Chest Labels are tested independently**, cumulatively on the accepted 10-mod baseline (expected 13 mods if all pass), one at a time, then a final 13-mod validation with a clean restart. Excluded / deferred: Fantasy Races, Shemite City State, Thrall Wars Dungeon, Room For One More.

## Mods #11-#14 series (2026-10-04): #11 Room For One More FAIL / STOP, rolled back; #12-#14 NOT RUN

The four packages were verified read-only from their embedded metadata and the public Workshop metadata (`WO_RoomForOneMore.pak` FA608653…52FA, `Simple_Minimap.pak` 04F31A75…6AC9, `PlayerDBNO.pak` 3E7FEEED…FD65, `ChestLabels.pak` C79C7E00…0CD8; all Enhanced, devkit 1002, WindowsServer payload, sizes byte-identical to Workshop; #11 carries no embedded Workshop ID and was accepted on exact name plus size, flagged). #11 loaded cleanly (readiness 34.2 s, 660.3 s hold, no LoadErrors, shutdown NORMAL 154.6 s, `quick_check` ok) but logged 5 new unvalidated `MergeDataTables - ToBeAddedDataTable is null` errors right after its controller registered (7 total vs the 2 allowed for Ancient Realms), and its controller shares the blueprint name `WO_BP_RT_ModController_C` with Riding Thralls. Nothing whitelisted; rolled back to the verified pre-#11 backup `2026-10-04_045203` and verified (10-mod state exact, live DB identical to `2026-10-04_035205`). #12-#14 not run (each mod must pass before the next). Server OFFLINE; gameplay NOT YET VERIFIED; production world NOT CREATED. Failed world: backup `2026-10-04_050719`. Decisions pending; details in `M3_LIVE_TEST_REPORT.md`.

## (superseded) Room For One More (mod #11, Workshop 3811298984): BLOCKED, source package not found locally (2026-10-04)

The batch (add `[Enhanced] WO - Room For One More` as mod 11 while keeping WO - Riding Thralls) did not start: no local package could be proven. A read-only search of the user profile, D:, E: and F: (1,195 candidate `.pak` files, including the 13 in `C:\Users\vkkha\Downloads\mod conan`) found none whose embedded `modinfo.json` carries Workshop ID 3811298984; the only IDs present belong to packages already used (3723538551, 3721274811, 3766043945, 3755371705, 3722829382, 3735091187, 3787066846, 3803149679, plus the five without a readable `mainClient`). The user's `F:\steamworkshopdownload\WorkshopDL` tool holds no content for it. Nothing was downloaded, renamed or guessed; no backup, plan, import or boot was run. The accepted 10-mod pack, the server (OFFLINE) and the final verified backup `2026-10-04_035205` are unchanged. Needed from the operator: the Room For One More `.pak` (for example placed in `C:\Users\vkkha\Downloads\mod conan`), or permission to download it. Gameplay NOT YET VERIFIED; production world NOT CREATED.

## FINAL 10-MOD CAMPAIGN PACK SERVER-SIDE = PASS (2026-10-04)

The accepted 10-mod pack passed a full validation and a clean restart on a quiet host. Full run: readiness 36.4 s, 660.7 s hold, runtime load order exact (Order 1000-1009), complete-log analysis 0 unknown with every known warning set exact, no new LoadErrors (60 vs 60), no new warning/error kinds against the accepted boot, no crash/assertion/persistence error, shutdown NORMAL 174.1 s, exit 0, no forced kill, no orphan, `quick_check` ok, singletons and mod controllers 1 each. Restart: readiness 40.5 s, same exact analysis, shutdown NORMAL 191.4 s, `quick_check` ok. Final verified backup **`2026-10-04_035205`**. Pack: StackMe10K, Savage Paragon, Grit & Grease, Thrall Reputation, Improved Thralls & QoL, WO Riding Thralls, Ancient Realms Enhanced, Cannibal Captivity Enhanced, Night Terrors, PvE Plus Ambush. Excluded / deferred: Fantasy Races, Shemite City State, Thrall Wars Dungeon. **Gameplay NOT YET VERIFIED (4F blocked by client authentication). Production world NOT CREATED** and must not be created without explicit approval. Details: `M3_LIVE_TEST_REPORT.md`.

## Operator decision 2026-10-04: Thrall Wars Dungeon EXCLUDED / DEFERRED from Modpack V1

Not whitelisted: its 73 LoadErrors, 3 `LogMaterial` errors and 4 `LogModController` merge errors; in particular the loot-table row-structure mismatches (`LootTableRow` vs `LootTableWeightedRow`) are NOT accepted as a known harmless baseline. All Thrall Wars evidence (log, snapshots `p3-thrallwars-pre` and `p3-thrallwars-1`, the failed-world backup `2026-10-04_031850`, the retired pak archive, `artifacts/batch-d-20261003/P3-ThrallWars-*`) is preserved for possible future investigation. **Accepted 10 mods:** StackMe10K, Savage Paragon, Grit & Grease, Thrall Reputation, Improved Thralls & QoL, WO Riding Thralls, Ancient Realms Enhanced, Cannibal Captivity Enhanced, Night Terrors, PvE Plus Ambush. **Excluded / deferred:** Fantasy Races, Shemite City State, Thrall Wars Dungeon. Latest verified accepted backup `2026-10-04_031929`. Next: one final full validation of the 10-mod pack. Historical text below is unchanged.

## Boss/PvE phase (2026-10-04): P3 Thrall Wars Dungeon FAIL / STOP, rolled back (10 mods installed)

Thrall Wars Dungeon (`SlaveWarsServer.pak`, Workshop 3722829382, SHA-256 `A6238D37…623B`) mounted and ran stably (readiness 37.5 s, 660.6 s hold, shutdown NORMAL 176.0 s, `quick_check` ok, singletons 1/1/1, one new controller) but introduced 73 new unvalidated LoadErrors and 7 new errors (4 loot-table `MergeDataTables` failures, 3 `LogMaterial` errors). Nothing whitelisted; rolled back to the verified PRE-P3 backup `2026-10-04_030240` and verified (10-mod state exact, live DB identical to the post-P2 backup). The final 11-mod validation was not run. Latest verified 10-mod backup `2026-10-04_031929`; post-P3 failed world `2026-10-04_031850`. Server OFFLINE. Production world NOT CREATED. Decision pending: baseline or exclude Thrall Wars Dungeon; details in `M3_LIVE_TEST_REPORT.md`.

## Boss/PvE phase (2026-10-04): P2 PvE Plus Ambush PASS server-side (10 mods installed)

PvE Plus Ambush (`PvEPlusAmbush.pak`, Workshop 3721274811, SHA-256 `C9C816FA…5B74`) added on top of Night Terrors: readiness 37.5 s, 660.5 s hold, complete-log analysis 0 unknown, no new LoadErrors (60 vs 60), no warning/error naming it, no conflict between the two ambush systems, no new NPC/stat/spawn-table errors, three controllers (one of each in the world, no duplicates), shutdown NORMAL 177.0 s, `quick_check` ok, singletons 1/1/1. Verified backups: pre-P2 `2026-10-04_024506`, post-P2 `2026-10-04_030121`. Gameplay NOT YET VERIFIED. Next: P3 Thrall Wars Dungeon (`SlaveWarsServer.pak`). Production world NOT CREATED.

## Boss/PvE phase (2026-10-04): P1 Night Terrors PASS server-side (9 mods installed)

Night Terrors (`NightTerrors.pak`, Workshop 3723538551, SHA-256 `2FE3E7AD…FBE61`) imported and booted cleanly: readiness 36.4 s, 660.6 s hold, complete-log analysis 0 unknown, no new LoadErrors (60 vs 60), no new warnings/errors naming it, one new controller object, shutdown NORMAL 177.3 s, `quick_check` ok, singletons 1/1/1. A scanner defect (a mod name containing "error") was fixed in `af0b25e`. Verified backups: pre-P1 `2026-10-04_022500`, post-P1 `2026-10-04_024312`. Gameplay NOT YET VERIFIED. Next: P2 PvE Plus Ambush, then P3 Thrall Wars Dungeon (`SlaveWarsServer.pak`). Production world NOT CREATED.

## CORE MODPACK V1 SERVER-SIDE = PASS (2026-10-04, quiet-host retry)

The final 8-mod core validation passed on a quiet host (League of Legends closed; host CPU 27% before, 34-38% during): full validation (readiness 36.4 s, 660.5 s hold, complete boot/runtime/teardown analysis with 0 unknown and every known warning set exact, shutdown NORMAL 175.1 s with host CPU 36%, exit 0, no forced kill, no orphan, `quick_check` ok, singletons 1/1/1) and the clean restart validation (readiness 36.4 s, same exact analysis, shutdown NORMAL 178.1 s, `quick_check` ok). Final verified backup **`2026-10-04_020607`**. Accepted core (8): StackMe10K, Savage Paragon, Grit & Grease, Thrall Reputation, Improved Thralls & QoL, WO Riding Thralls, Ancient Realms Enhanced, Cannibal Captivity Enhanced. Gameplay NOT YET VERIFIED (4F blocked by client authentication); production world NOT CREATED; Shemite and Fantasy Races EXCLUDED / DEFERRED. The boss/PvE mod phase may begin (server-side). Details: `M3_LIVE_TEST_REPORT.md`.

## Final 8-mod core validation, attempt 1 (2026-10-04): all checks pass except shutdown time (DEGRADED 307.9 s under heavy host load) (historical)

Readiness 71.3 s, 661.7 s hold, complete boot/runtime/teardown analysis PASS (0 unknown; all known warning sets exact), `quick_check` ok, singletons 1/1/1, no crash/assertion/persistence error, process tree fully stopped, no forced kill. The stop took 307.9 s (DEGRADED) while host CPU averaged 79% from a running game; the same 8 mods stopped in 181.7 s and 199.2 s on a quiet host. By the stated rule this is a FAIL, so **CORE MODPACK V1 SERVER-SIDE = NOT YET**; the clean-restart validation was not run. Latest verified backups: post-run `2026-10-04_012406`, pre-final `2026-10-04_010449`. 8-mod state installed; server OFFLINE. A retry belongs on a quiet host. Details: `M3_LIVE_TEST_REPORT.md`.

## Operator decision 2026-10-04: Shemite City State EXCLUDED / DEFERRED from Modpack V1

Not re-run; its 9 LoadErrors and 6 spawn-table errors (`Catacomb_Wretch` x5, `Wildlife_SiptahTwoHornedRhino_Baby` x1) are NOT whitelisted. It reached readiness and preserved world integrity but introduced those unvalidated errors (the spawn-table ones may affect gameplay/spawn behavior and are not sufficiently explained) and a DEGRADED 319.6 s shutdown. All Shemite evidence, snapshots and backups are kept for possible future investigation. **Accepted server-side core (8):** StackMe10K, Savage Paragon, Grit & Grease, Thrall Reputation, Improved Thralls & QoL, WO Riding Thralls, Ancient Realms Enhanced, Cannibal Captivity Enhanced. Fantasy Races and Shemite are both EXCLUDED / DEFERRED. Next: one final full 8-mod core validation. Historical Shemite text below is unchanged.

## Shemite City State (2026-10-04): STOP, rolled back (INCONCLUSIVE, gate-blocked)

Imported and booted cleanly (readiness 41.6 s, order 1008, 1,999 packages, controller registered, 11-minute hold stable, quick_check ok, singletons 1/1/1, one new controller object, no crash/assertion/persistence/map-streaming difference). Blocked by: 9 new unvalidated Shemite LoadErrors, 6 new spawn-table errors (a scanner gap that hid them is fixed in `c0ef218`), and a DEGRADED shutdown of 319.6 s (cause not established; host memory was tight). Nothing whitelisted; rolled back to the verified PRE-SHEMITE backup `2026-10-04_001912` and verified (DB and 8-mod modlist identical to the accepted state). The 9-mod core validation was not run. Latest verified 8-mod backup `2026-10-04_004256`. Boss/PvE mods not started; server OFFLINE. Details and decisions needed: `M3_LIVE_TEST_REPORT.md`.

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
