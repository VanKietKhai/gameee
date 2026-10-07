# Batch D handoff — FINAL 10-MOD CAMPAIGN PACK SERVER-SIDE = PASS

## Desktop app prepared for daily use (2026-10-05)

**What was built and where it lives:**
- The real desktop UI is `src/ConanServerControl.App` (WPF, net8.0-windows). Release build: 0 warnings, 0 errors; tests 513/513.
- It is published **self-contained** for win-x64. A framework-dependent build could not start, because the app embeds the Web Admin host and needs the ASP.NET Core 8 runtime, which is not installed machine-wide on this host. The self-contained publish carries the .NET, Desktop and ASP.NET Core 8 runtimes.
- The app lives in a dedicated folder outside the repo and outside the live-test root. A desktop shortcut named **Conan Server Control** points to it.

**Daily data directory:**
- The app uses its own data directory, selected with the app's supported override variable `CONAN_SERVER_CONTROL_DATA` (user scope). The default ProgramData location is on a nearly full system drive.
- The directory is kept separate from the live-test harness data on purpose, because the harness rewrites its settings on every run.
- It was seeded with **copies** of the validated `settings.json` and `secrets.bin` (DPAPI, current user) and of the two key validated backups: final 2.2.3 `2026-10-05_041457` and migration source `2026-10-05_032214`. All copies were verified hash-identical.
- All 152 original backups and the live-test data stay untouched.

**What the settings carry over:**
- Server path: the validated 2.2.3 dedicated server.
- RCON: enabled on TCP 25575 with the validated secret.
- The 13 Local mods in the validated order.
- Auto-start of the server: off. Web Admin: off.

**Verification (read-only, server never started):**
- The app was opened through the desktop shortcut. It opened and responded, logged the intended data directory, and Kestrel did not listen.
- The dashboard shows **OFFLINE**, `13 installed, 0 updates`, ports 7777/27015 and RCON 25575.
- Cosmetic follow-ups: the server name is still the test label, and the dashboard says "Last backup: Never" because the new app's activity history is empty. The copied backups are in its backup folder.

The live-test harness remains a test tool only; never run it while the daily app controls the server.

## Migration to Conan Exiles Enhanced 2.2.3 — 13-MOD STAGING PACK SERVER-SIDE PASS (2026-10-05)

**OLD BUILD:** 2.2.2 / CL-377096 (`++exiles+release-CL-377096`). **OLD ENVIRONMENT: PRESERVED FOR ROLLBACK.** The operator renamed it to the `conan (old)` folder. It was only read, never booted, modified or reused.

**NEW BUILD:** 2.2.3 / **CL-378132**.
- Executables report `++exiles+release-beta-CL-378132`, which is a **beta-branch** build.
- The log reports `ProjectVersion 2.2.3`, `Net CL 378132` and engine 5.8.2-378132.
- Server executables are Authenticode-signed by Funcom Oslo AS (Valid), and its Steam DLLs by Valve. No emulation artifacts in the server.

**NEW ENVIRONMENT: STAGING.**

**Client:**
- The new client's executables are also Funcom-signed CL-378132, so **client == server** at the changelist level, identified from file metadata only.
- That client folder contains Steam-emulator configuration (a repack). It was not launched, modified, or prepared with mods.

**Phase 0:**
- The old server was already stopped gracefully (session `mp13-session-1`: NORMAL 171.8 s, exit 0, no orphan, 0 unknown).
- New migration-source cold backup **`2026-10-05_032214`**: DB `F02F179E…FE0D`, no WAL, `quick_check` ok, singletons 1/1/1, no duplicate objects. It is identical to the live old world, which the backup left unchanged.
- No other server, harness or agent process was active; two idle `codex.exe` processes had no children.

**Phase 2:** an immutable migration plan (`plan.json`, read-only, kept locally) recorded the old and new identities, the source backup, the 13 exact pak hashes, the steps, the never-copy list and the rollback rule.

**Migration:**
- *RCON config:* only the old `Game.ini` (`[RconPlugin]`) was copied before the first boot, so the harness RCON secret stays valid. The secret is not recorded.
- *Vanilla identification boot:* readiness 31.3 s, RCON reply 32 s, NORMAL 64.6 s, exit 0, no orphan. Vanilla 2.2.3 itself logs `Exile_Priest_4_Nordheimer` as a missing weighted table; this id did not appear in any modded 2.2.3 boot.
- *Config comparison:* the 2.2.3 vanilla schema is identical to the old staging config (ServerSettings 219 keys, same Engine/Game/Input keys). The only value difference is `ThrallDamageToNPCsMultiplier` (staging 0.3, 2.2.3 default 0.5). 0.3 is the deliberate campaign/staging value (CAMPAIGN_V1.md), so the old config was kept unchanged.
- *World restore:* `2026-10-05_032214` was restored through `IBackupService.RestoreAsync`, which took a pre-restore safety backup of the vanilla world. Restored DB equals the source, WAL 0 bytes, `quick_check` ok.
- *Mods:* the 13 paks were installed through the Local Mod pipeline (new harness command `replace-local` → `ReplaceLocalModAsync`, one verified cold backup and transactional commit each), using the hash-verified bundle as the source. Every destination SHA-256 equals the plan, the order is exact, there are no extra files, and the world was unchanged by the install. **MOD HASH PARITY: PASS.** No old executables, engine binaries, Steam DLLs, caches, logs, ExtractedMods or crash dumps were copied.

**2.2.3 validation** (same 13 pak bytes, new build; compared line-by-line with the accepted 2.2.2 `final13-restart`):

| Run | Readiness | Hold | Shutdown | Unknown | LoadErrors | Post backup |
|---|---|---|---|---|---|---|
| `v223-smoke-1` | 35.2 s | 120.5 s | NORMAL 178.7 s | 0 | 61 = 2.2.2 (none extra/missing/unparsed) | `2026-10-05_033640` |
| `v223-final13-1` (full) | 39.4 s | 661.4 s | NORMAL 194.7 s | 0 | 61, exact | `2026-10-05_035257` |
| `v223-final13-restart` | 41.6 s | 660.9 s | NORMAL 166.0 s | 0 | 61, exact | `2026-10-05_040847` |

**All three runs:**
- Mod set and order: the same SHA-256 and order as 2.2.2. Each mod mounted exactly once, and the controller registrations are identical to 2.2.2 (each once).
- LoadErrors and signatures: exact known baselines only (ITQoL 23, Ancient Realms 37, Simple Minimap 1). The Simple Minimap `C88E5FE76A79516D` two-line signature is byte-identical at frame 0, so no accepted signature changed.
- Errors and markers: only the two accepted spawn-table ids, no priest ids, no crash, assertion or fatal markers.
- Shutdown: RCON acknowledged, exit 0, no forced kill, no orphan.
- World: `quick_check` ok, singletons 1/1/1, no duplicate persistence objects.
- Persistence diffs: only `game_events`, plus one SQLite query-planner statistics row (`sqlite_stat1`) on the first 2.2.3 boot.

**Diff items reviewed (none is new to the mods):**
- 12 Bink title-movie lines differ only by install path (existing noise rule `BINK-TITLE-MOVIE`).
- The EntertainerHumanoidNPC thrall-spawn warning and the wildlife AILOD3 movement lines are known 2.2.2 variance.
- An in-flight Funcom stats `LogHttp` warning at teardown also occurred in 2.2.2 boots.

**13-MOD PACK ON 2.2.3 = PASS.**

**Conan Server Control:**
- *Path updated:* the configured server path now points to the new 2.2.3 server, set through the product's `ISettingsService` in the live app data. Product logic has no hard-coded paths. The desktop app has not been configured on this machine yet.
- *Acceptance PASS:*
  - `cycle`: Offline → Starting → Online (39 s) → graceful Stop → Offline (exit 0) → Start → Restart (Restarting → Stopping → Offline → Starting → Online) → Stop; no remaining processes.
  - Backup: verified `2026-10-05_041457`.
  - Diagnostics: 13 Local mods detected, all 13 paks present, `modlist.txt` matches the app's order, last backup verified.
  - Diagnostics also showed one false-positive warning: "Workshop ID 0 appears 13 times", the placeholder id of Local mods. It is flagged as a separate fix and is not a migration issue.

**Final verified 2.2.3 backup: `2026-10-05_041457`.** `quick_check` ok; Simple Minimap, Chest Labels, ITQoL controller, Ancient Realms controller and ITQoL mailbox 1 each; no duplicates.

**Client bundle 2.2.3:**
- Fresh export from the 2.2.3 server: 13/13 hashes equal the server and the accepted pack, `modlist.txt` equal, no forbidden files, no secrets or addresses, README hash check 13/13.
- The 2.2.2 bundle is marked `SUPERSEDED`.
- Mods were NOT installed into the new local client folder, because of the emulator configuration above.

**Network (unchanged policy):** Windows created the same Private-profile allow-any firewall rule for the new server executable. RCON (TCP 25575) is therefore reachable from LAN and Radmin peers but not publicly. The recommended TCP 25575 block rule remains the operator's to apply.

**Status:** CONAN 2.2.3 13-MOD STAGING PACK SERVER-SIDE = **PASS**. Server OFFLINE. Production world NOT CREATED. Custom Main Questline PAUSED.

## Incident: server Mods folder emptied during the multiplayer session; restored (2026-10-05)

**What happened:**
- At 02:26 local time, while session `mp13-session-1` was ONLINE, the live server's `ConanSandbox\Mods` folder became **empty**. All 13 `.pak` files and `modlist.txt` were gone.
- The same 13 files, with their original timestamps, appeared in the standalone client's `ConanSandbox\Mods` folder (folder changed at 02:30). This is consistent with the files being **moved** (cut and paste) from the server into the client during a manual client install, instead of copied from the client bundle.

**Impact:**
- None on the running server: the mods were already mounted at boot from `Saved\ExtractedMods`, and the server, the harness session and the world kept running.
- Latent risk: had the server restarted with an empty Mods folder, the world would have loaded **without its mods**, risking loss of modded persistence data. No restart happened while the folder was empty.

**Fix (Claude, 2026-10-05, on the operator's request to restore the folder to its earlier state):**
- The client bundle was verified first: all 13 paks equal the frozen pack's SHA-256 and the bundle `modlist.txt` equals the frozen order.
- The 13 paks and `modlist.txt` were **copied** from the bundle back into the server Mods folder. The empty folder was checked immediately before copying.
- Post-check: 13/13 SHA-256 and sizes equal the frozen `final13-restart` snapshot, the `modlist.txt` order is exact, and there are no extra files.
- The client's copies were left in place.
- The world database was not touched. The server stayed ONLINE throughout, and session `mp13-session-1` continues.

**Not restored:** the standalone client folder.
- There was no snapshot of it, since the project backs up only the server world.
- Its changes at 02:28 are in `Binaries\Win64` and the Steamworks folder. The client had just shown an `OnlineFix64.dll` load error, which belongs to a third-party Steam-authentication workaround.


**Prevention:**
- Install client mods only by **copying** from the client bundle (`client-bundles\<bundle>\Mods`). Never take files from, cut from or edit the server's `ConanSandbox\Mods`.
- Before any server start, the existing pre-start checks (modlist order, 13 exact hashes, no extra files) must pass. They would have blocked a boot with the empty folder.

## 13-MOD MULTIPLAYER GAMEPLAY TEST MODE — test environment prepared, server ONLINE (2026-10-05)

Custom Main Questline development is **PAUSED**. No mods were added or removed and no settings were changed. The production world is NOT CREATED. The current world is STAGING / TEST ONLY.

**Step 1, test pack frozen:** `TEST_PACK_13MOD_STAGING.md`, the **13-MOD STAGING GAMEPLAY TEST PACK** (not production).
- The installed `modlist.txt` equals the validated `final13-restart` order.
- All 13 installed pak hashes equal the validated snapshot, and the Mods folder has no extra files.
- Each mod is recorded with display name, pak file, size, SHA-256, load order and Workshop ID. The Workshop ID is the `mainClient` embedded in the pak and was recomputed for all 13.

**Step 2, client bundle:** built with the new harness command `export-bundle`, which uses the production `ClientModBundleService`.
- The service hash-verifies each copy and renames the folder into place atomically. The harness then re-hashes every bundled pak against the installed server pak, checks the bundle `modlist.txt` against the server's, and rejects unexpected files. Result: PASS, 13/13 equal, **CLIENT MOD PACK == SERVER MOD PACK**.
- Contents: the 13 `.pak` files, `modlist.txt`, `manifest.json`, `SHA256SUMS.txt`, `TEST_PACK_MANIFEST.txt`, `README.txt` (service) and `README_VI.txt`.
- No exe, DLL, emulator, authentication file, server binary, backup, address or secret (scanned). Size 0.88 GB. It stays outside the repo.
- The README's own hash-check command returns 13 × `OK` against the bundle.

**Steps 3–5:**
- `docs/multiplayer-test/README_VI.txt`: Vietnamese guide covering install location, load order, hash check, a normal Steam launch, Radmin + Direct Connect (IP and passwords shared privately, never written), the test-world warning and bug reporting.
- `MULTIPLAYER_TEST_CHECKLIST.md`.
- `BUG_REPORT_TEMPLATE.md`.

**Step 6, test server started.**

Pre-start checks:
- Staging DB equal to the verified backup `2026-10-05_014347`, with no WAL.
- 13 hashes and the order exact.
- `quick_check` ok and the singletons 1/1/1.
- Quiet host, no Conan process.

Session `mp13-session-1`:
- **Readiness:** true readiness in 37.5 s. Game port 7777 is bound and the world is ticking; UDP 7777, 7778 and 27015 are bound.
- **RCON:** TCP 25575 is listening. A localhost RCON `listplayers` authenticated and returned the player table (0 players).
- **Current-boot analysis:** PASS. 0 unknown; LoadErrors are only the exact known baselines (ITQoL 23, Ancient Realms 37, Simple Minimap 1 with signature and frame); no crash, assertion or fatal markers; no priest ids; only accepted spawn-table ids.
- **Operation:** the session runs detached as a harness `mod-boot` with a 72 h maximum hold.
- **Ending it:** create the `release-hold` file in the live-test folder. The harness then stops through graceful RCON, runs the complete-log analysis and records the immutable snapshot `mp13-session-1`. Do not kill the harness or the server process. Session files live under `live-test/sessions/mp13-session-1/` on the live root.

**Network:** private Radmin VPN and Direct Connect only. No UPnP, no port forwarding and no public listing were configured.

**Open findings (operator):**
1. *RCON reachability.* RCON listens on `0.0.0.0:25575`. A pre-existing Windows Firewall rule, `ConanSandboxServer` (Private profile, TCP+UDP any port, server executable, created by Windows on the server's first run), allows inbound connections on both Private networks, Ethernet and Radmin VPN.
   - RCON is therefore reachable from the LAN and from Radmin peers. It is password-protected, and it is not publicly reachable: the LAN address is RFC1918 behind NAT, there is no global IPv6 and there is no port forwarding.
   - Firewall changes are the operator's to make. The recommended fix is an inbound **block** rule for TCP 25575 on all profiles, run from an elevated prompt. Loopback is not filtered, so local management keeps working.
2. *Harness wiring.* `--observe` is wired only to the plain `boot` command, not `mod-boot`. RCON was therefore confirmed with a separate localhost probe; a later harness fix can wire it.

## Phase 3 — Chest Labels PASS; final 13-mod base pack SERVER-SIDE PASS (2026-10-05)

**Source:** `ChestLabels.pak`. Workshop ID 3735258746 was recomputed from the embedded `mainClient` (Enhanced, devkit 1002, WindowsServer payload). Size 1,192,363 bytes. Full SHA-256 `C79C7E00E8B44F7A6F1250D58BF8655BA9FFBDA16FDB7A1884BBD782186D0CD8`, recomputed before import; it is the same file as `p14-chest-1`.

**Preparation:**
- Base state: the accepted 12-mod pack, with the catalog now including the exact Simple Minimap rule (catalog `A1A51E41…F7E4`).
- Pre-checks passed: quiet host, no Conan process, the 12 installed hashes exact, live DB equal to `2026-10-05_002520`, and the accepted world gates PASS.
- Pre-run backup `2026-10-05_003936`, run plan `p3-chest-pre`.
- Import verified: 13 mods, Chest Labels last, earlier order intact, world unchanged.
- The pre-run backup for each later boot was the previous boot's verified post-run backup. Before each boot the live DB was confirmed equal to it.

| Boot | Readiness | Hold | Shutdown | Unknown | LoadErrors | Priest ids | `CL_MC_ChestLabels_C` | Integrity / persistence | Post backup |
|---|---|---|---|---|---|---|---|---|---|
| `chest-retest-1` | 36.2 s | 660.3 s | NORMAL 166.9 s | 0 | 61 = reference (none extra or missing) | both 0 | registered + spawned once (first boot: +1 `actor_position`, +1 `mod_controllers`) | `quick_check` ok, singletons 1/1/1, no duplicates, +25 `game_events` | `2026-10-05_005520` |
| `chest-retest-2` | 34.2 s | 660.8 s | NORMAL 168.4 s | 0 | 61, exact | both 0 | registered + loaded once | ok; only +23 `game_events` | `2026-10-05_011115` |
| `final13-1` (full validation) | 34.2 s | 661.0 s | NORMAL 179.6 s | 0 | 61, exact | both 0 | once | ok; +23 `game_events`, +1 base-game storm property (explained below) | `2026-10-05_012731` |
| `final13-restart` (clean restart) | 35.2 s | 660.8 s | NORMAL 162.7 s | 0 | 61, exact | both 0 | once | ok; only +25 `game_events` | **`2026-10-05_014347`** |

**Common to all four runs:**
- RCON shutdown acknowledged, exit 0, no forced kill, no orphan.
- The complete-log harness analysis passes with every known set exact: ITQoL 23, Ancient Realms 37, Simple Minimap 1 (signature and frame match).
- Only the two accepted spawn-table ids appear.
- No Chest Labels LoadErrors and no Warning/Error line naming Chest Labels.
- No storage, placeable, naming or label persistence errors, and no crash, assertion or fatal markers.

**Diff items reviewed (none is new):**
- *Thrall spawn warning* (`chest-retest-1`): `ThrallActorClass was not loaded … EntertainerHumanoidNPC`. It appears in 20 of 24 recorded boots, including all three accepted 11-mod controls. Only the reference boot `minimap-retest-2` happened not to log it.
- *Wildlife movement line* (`chest-retest-2`, `final13-1`): wildlife `Komodo` / `Komodo_Baby` AILOD3 movement lines, covered by the existing exact noise rule `WILDLIFE-AILOD3-MOVEMENT`.
- *Lamplighter attach warning*: the ITQoL Lamplighter AILOD attach warning count varies 1–5 online across all boots (2–4 in the accepted controls). The runs here logged 1–3.

**Persistence item explained (`final13-1`):** a `properties` row `BP_SiptahStormController_C.SavedStormState` was added to base-game object 121, the Siptah DLC storm controller.
- Its existing `SavedStormTime` is a countdown that fell by about 630 s per boot: 2909.9 → 2279.9 → 1649.9 → 1019.9 → 389.9.
- `final13-1` ran past the remaining 389.9 s. The storm advanced to its next state, the timer reset to 719.9, and the new state was persisted.
- This is elapsed-time base-game behaviour. No mod object was added, removed or duplicated, and the clean restart then changed only `game_events`.

**Final world:** `quick_check` ok; one each of the Simple Minimap, Chest Labels, ITQoL and Ancient Realms controllers and the ITQoL mailbox.

**Status:**
- CHEST LABELS SERVER-SIDE = **PASS**. No catalog change was needed.
- **FINAL 13-MOD BASE PACK SERVER-SIDE = PASS**: the 10 original + Player DBNO + Simple Minimap + Chest Labels.
- Final verified backup **`2026-10-05_014347`**. Server OFFLINE.
- SAFE TO START CUSTOM MAIN QUESTLINE = **YES**, but it has not been started.
- Gameplay and client-side rendering (Minimap, Chest Labels UI) NOT YET VERIFIED. Production world NOT CREATED.
- Raw evidence stays local in `artifacts/` (gitignored) with a SHA-256 manifest.

## Simple Minimap accepted: signature-bound LoadErrors baseline (2026-10-05)

**Operator decision (2026-10-05):** approve the Simple Minimap LoadError as known non-blocking, but only as a strict hash-bound exact rule.

**Model extension (minimal, backward compatible):**
- `LoadErrorBaseline` gains an optional `Signatures` map from each `Expected` key to a `LoadErrorSignature`. The signature holds the exact `LoadErrors:` message without its prefix, the exact explanatory line that follows it, and an optional required engine frame. All comparisons are ordinal and exact.
- `ParseLoadErrors` now records each entry's message, the following log line and its frame.
- A signature-bound entry fails if its message, its explanatory line or its frame differs, or if any of them is missing. The existing key/count/hash checks run first and are unchanged: wrong hash, extra, missing and unparsed entries all still fail.
- Baselines without signatures behave exactly as before. A null `Signatures` is not serialized, so the catalog without the new rule still hashes to the recorded `767B4BCD…7818`, which a test proves.
- No wildcard, substring, generic frame-0 or "Minimap warnings are safe" logic was added.

**Rule added (the only catalog change):**
- `Simple_Minimap.pak`, SHA-256 `04F31A75559665C1A949D7A9A632F9A777CE7D79E926032CFC9FD101B1626AC9`.
- `None -> C88E5FE76A79516D` exactly ×1, at frame 0, `KnownNonBlocking`.
- The message and explanatory line are taken byte-for-byte from the preserved snapshots: `LoadErrors: While trying to load package None, a dependent package None (C88E5FE76A79516D) was not available. Additional explanatory information follows:` / `FPackageName: Unable to identify a valid mount point associated with skipped package None. The package root is unknown.`

**Tests:** 22 new `LoadErrorSignatureTests`. The exact-match case passes. These cases fail: wrong hash, wrong reference, count 0, count 2, message drift, explanatory-line drift (four variants), missing line, frame drift (1, 46), no prefix, extra LoadError, and malformed/unparsed lines. Further tests check backward compatibility, the JSON round-trip and that the committed rule is exactly the approved one. Build 0 warnings / 0 errors. Full suite **513/513**, 0 skipped.

**Snapshot re-analysis** (recorded catalog vs current catalog, full output diff):

| Snapshot | Changed lines | Result |
|---|---|---|
| `p12-minimap-1` | only the Simple Minimap LoadError gate (now `KNOWN NON-BLOCKING … 1 exact signature(s) and frame(s) match`) and the LoadError problem count | still FAIL: the `Exile_Priest_4_Hyrkanian` line stays UNKNOWN, as required |
| `minimap-retest-1` | same and nothing else | **PASS**, 0 unknown, both priest ids absent |
| `minimap-retest-2` | same and nothing else | **PASS**, 0 unknown, both priest ids absent |

- The ITQoL (23) and Ancient Realms (37) LoadError results are byte-identical in all three snapshots.
- Regression under the current catalog:
  - `ctrl10-1`, `ctrl11-variance-1/2/3`, `final10-1`, `p13-dbno-1` and `p14-chest-1` all PASS with 0 unknown.
  - `p14-chest-restart` is unchanged: it still fails on its two priest lines.

**SIMPLE MINIMAP SERVER-SIDE = PASS.**
- Accepted pack: **12 mods** (the 10 original + Player DBNO + Simple Minimap), kept as the stopped 12-mod install.
- Accepted verified backup `2026-10-05_002520`.
- Client-side Minimap rendering is NOT YET VERIFIED.

## Phase 2 — Simple Minimap retest: PASS CANDIDATE, catalog rule proposed (not applied) (2026-10-05)

**Source:** `Simple_Minimap.pak`, Workshop ID 3719513784 (recomputed from the embedded `mainClient` metadata; Enhanced, devkit 1002, WindowsServer payload), 4,835,375 bytes, full SHA-256 `04F31A75559665C1A949D7A9A632F9A777CE7D79E926032CFC9FD101B1626AC9` (recomputed before import; identical to the `p12-minimap-1` file). Imported through the production Local Mod pipeline as mod #12 on top of the accepted 11-mod pack; catalog unchanged (`767B4BCD…7818`).

**Pre-batch:** safety checks passed (server offline, no orphan, quiet host). Fresh verified pre-Minimap backup `2026-10-04_235212`; immutable run plan `m12r-minimap-pre`; import verified 12 mods, Minimap last, world unchanged by the import.

| Boot | Readiness | Hold | Shutdown | C88E5FE76A79516D | Priest ids | Other unknowns | `SM_BP_ModController` | Integrity / persistence |
|---|---|---|---|---|---|---|---|---|
| `minimap-retest-1` | 37.4 s | 660.2 s | NORMAL 166.5 s (stop 168.6 s), acknowledged, exit 0, no forced kill, no orphan | 1 (frame 0, 60th of 61 LoadErrors) | both 0 | none | registered once, spawned once (first boot: +1 `actor_position`, +1 `mod_controllers`) | `quick_check` ok, singletons 1/1/1, no duplicate ids; only +23 `game_events` otherwise |
| `minimap-retest-2` | 34.2 s | 660.4 s | NORMAL 160.2 s (stop 162.3 s), acknowledged, exit 0, no forced kill, no orphan | 1 (frame 0, 60th of 61 LoadErrors) | both 0 | none | registered once, loaded once (no new row) | `quick_check` ok, singletons 1/1/1, no duplicate ids; only +24 `game_events` |

In both boots the only harness UNKNOWN was `Simple_Minimap.pak: 1 LoadErrors and no validated set for this mod`, which was expected. LoadErrors versus `ctrl11-variance-3`: +1 (C88), none missing, none unparsed. Spawn-table lines: only the two accepted ids, once each. No map/POI/UI warnings, no Warning/Error line naming Simple Minimap, no Fatal/Assertion/crash markers. In Boot 2 the independent kind-diff reported one wildlife AILOD3 movement line (`Komodo` rather than `Komodo_Baby`). It is covered by the existing exact noise rule `WILDLIFE-AILOD3-MOVEMENT`, and every recorded boot has 5–7 such lines, so it is not new. Post-run verified backups: `2026-10-05_000756` (Boot 1) and `2026-10-05_002520` (Boot 2); live DB identical to each.

**LoadError recurrence: EXACT.** Package/reference `None -> C88E5FE76A79516D`; count 1 in each boot. The full two-line signature is byte-identical in Boot 1, Boot 2 and `p12-minimap-1`: `LoadErrors: While trying to load package None, a dependent package None (C88E5FE76A79516D) was not available. Additional explanatory information follows:` / `FPackageName: Unable to identify a valid mount point associated with skipped package None. The package root is unknown.`. Source pak hash is the same in all three. Phase is the same in all three (world init, frame 0, 60th LoadError). No drift.

**Proposed exact catalog rule (NOT applied — no catalog change is approved):**

```
new LoadErrorBaseline(
    "Simple_Minimap.pak",
    "04F31A75559665C1A949D7A9A632F9A777CE7D79E926032CFC9FD101B1626AC9",
    LoadErrorBaselineStatus.KnownNonBlocking,
    new Dictionary<string, int>(StringComparer.Ordinal) { ["None -> C88E5FE76A79516D"] = 1 },
    "Simple Minimap: one unresolved dependent package, identical in p12-minimap-1, minimap-retest-1 and -2; no integrity or persistence effect.")
```

A `LoadErrorBaseline` already enforces the exact pak SHA-256 and the exact set of keys and counts: any extra or missing key fails, and so does any LoadError line that doesn't parse. It does not store the message text or the boot frame. To bind those as well, the operator would also need to approve a small extension: an optional exact two-line signature, and a frame-0 / world-init constraint on the baseline. After approval: add the rule and re-analyse both immutable snapshots with `--current-catalog`. Then confirm the only change is that the C88 line moves to KNOWN, and that the unknown count, all other lines and every other gate are unchanged.

**Status:** Simple Minimap SERVER-SIDE = **PASS CANDIDATE**, pending operator review of the rule. The accepted pack remains **11 mods** until the rule is approved and applied. The 12-mod install (Minimap as #12) is left in place on the stopped server, and the latest verified backup `2026-10-05_002520` covers it. If the rule is rejected, roll back to the 11-mod baseline backup `2026-10-04_234706`. Chest Labels NOT started. Production world NOT CREATED. Custom Main Questline NOT started. Raw evidence stays local in `artifacts/` (gitignored).

## Baseline variance controls — control 3 complete; Phase 1 conclusion (2026-10-04)

**Takeover:** Codex hit its usage limit during control 3. Claude took exclusive control and inspected read-only first: no Conan or harness process was running, the live log ended with a clean `Exiting` / `Log file closed`, and the world had no WAL. The harness had already completed the run on its own (exit 0), so the **existing run was used**, not restarted. Only Codex's post-run steps were missing; they were completed with the same `control.ps1 -Action Post`, `analyze_control.py` and `persistence_review.py` used for controls 1 and 2.

`ctrl11-variance-3`: **PASS**, exact accepted 11-mod pack and unchanged catalog. Readiness 36.5 s; hold 660.6 s; complete-log scan 0 unknown (readiness scan and full boot/runtime/teardown scan); shutdown **NORMAL 155.3 s** (stop 157.3 s), RCON acknowledged, exit 0, no forced kill, no orphan; `quick_check` ok; ITQoL mailbox, ITQoL controller and Ancient Realms controller 1 each; no Fatal/Assertion lines. Persistence review PASS: only runtime and storm clocks, 24 appended `game_events`, query-planner statistics and sub-1e-12 rotation round-off changed; no object added, removed or duplicated. Verified backups: pre `2026-10-04_180624`, post `2026-10-04_234706` (post backup taken at completion). Host CPU mean 21%, minimum free RAM 908 MB. Spawn-table lines: only the two accepted ids, once each (`WarTestLongLeash` at frame 1, `Wildlife_Siptah_Firstman_Warrior4` at frame 247).

| Control (unchanged 11-mod pack) | Readiness | Hold | Shutdown | Unknown | `Exile_Priest_4_Hyrkanian` | `Exile_OrchidPriest_4_Nordheimer` | Other weighted-table ids |
|---|---|---|---|---|---|---|---|
| `ctrl11-variance-1` | 37.3 s | 660.7 s | NORMAL 167.6 s | 0 | absent (0) | absent (0) | `WarTestLongLeash` x1, `Wildlife_Siptah_Firstman_Warrior4` x1 |
| `ctrl11-variance-2` | 36.3 s | 660.7 s | NORMAL 173.0 s | 0 | absent (0) | absent (0) | same two, once each |
| `ctrl11-variance-3` | 36.5 s | 660.6 s | NORMAL 155.3 s | 0 | absent (0) | absent (0) | same two, once each |

All three: persistence PASS, `quick_check` ok, controllers and singletons unchanged, no integrity, save or persistence effect.

**Phase 1 conclusion:** neither priest id occurs in the unchanged accepted 11-mod pack (0 of 3 controls), nor in the unchanged 10-mod control (`ctrl10-1`). Across all 20 recorded boots they occur only where a candidate mod was installed: `Exile_Priest_4_Hyrkanian` once in `p12-minimap-1` (Simple Minimap) and once in `p14-chest-restart` (Player DBNO + Chest Labels); `Exile_OrchidPriest_4_Nordheimer` once, only in `p14-chest-restart`. Both appeared at world init (frame 0) as `SpawnTable: Error: Data: USpawnTableLibrary::SpawnNPCFromWeightedTable - could not find weighted table with id: <id>`, with no crash, persistence, save or integrity effect in those runs. Under the baseline-variance policy an id may be proposed only if the unchanged pack supports it; it does not. **Proposed catalog change: NONE.** No scanner rule was weakened; the catalog is unchanged. The ids stay unknown: if a Simple Minimap or Chest Labels retest logs them, that retest fails under the current rule. Raw evidence for all three controls stays local in `artifacts/baseline-controls-20261004/` (gitignored), with a SHA-256 manifest per run.

**Next:** Phase 2 (Simple Minimap retest) is not started; it waits for the operator. Accepted pack: 11 mods. Production world NOT CREATED. Custom Main Questline NOT started.

## Baseline variance controls — control 2 complete (2026-10-04)

`ctrl11-variance-2`: **PASS**, exact accepted 11-mod pack and unchanged catalog. Readiness 36.3 s; hold 660.7 s; complete-log scan 0 unknown; shutdown **NORMAL 173.0 s**, acknowledged, exit 0, no forced kill/orphan. Pre-run backup `2026-10-04_174655`; verified post-run backup `2026-10-04_180452`. Immutable snapshot and hash-manifested raw evidence retained locally.

Both target priest IDs: **ABSENT (0)** again. Every weighted/spawn-table error: the existing exact missing-weighted-table signature for `WarTestLongLeash` x1 (frame 0) and `Wildlife_Siptah_Firstman_Warrior4` x1 (frame 198). `quick_check=ok`; required singletons 1/1/1; all 30 controller IDs/classes unchanged and unique. Persistence changes limited to runtime/storm clocks, 23 appended events, SQLite statistics and rotation roundoff below 1e-12; no object loss/duplication. No catalog additions.

Server OFFLINE; controls 1 and 2 complete, control 3 next. Accepted count 11. Candidate-mod retests/final 13-mod validation NOT RUN; quest development NOT STARTED; production world NOT CREATED. Branch: `claude/m3-task4-live-windows`; compiled code/catalog unchanged since takeover.

## Baseline variance controls — control 1 complete (2026-10-04)

`ctrl11-variance-1`: **PASS**, unchanged accepted 11-mod order/hashes and warning catalog. Readiness 37.3 s; hold 660.7 s; complete-log scan 0 unknown; shutdown **NORMAL 167.6 s**, acknowledged, exit 0, no forced kill or orphan. Pre-run backup `2026-10-04_172858`; verified post-run backup `2026-10-04_174435`. Immutable snapshot and hash-manifested raw evidence retained locally.

Both `Exile_Priest_4_Hyrkanian` and `Exile_OrchidPriest_4_Nordheimer`: **ABSENT (0)**. Every weighted/spawn-table error: `WarTestLongLeash` x1 (frame 2) and `Wildlife_Siptah_Firstman_Warrior4` x1 (frame 246), both the existing exact `USpawnTableLibrary::SpawnNPCFromWeightedTable - could not find weighted table with id:` signature. No catalog additions. `quick_check=ok`; required singletons 1/1/1; all 30 controller IDs/classes unchanged and unique. Persistence review found only runtime/storm clocks, 24 appended events, SQLite statistics and rotation roundoff below 1e-12; no object loss or duplication.

Server OFFLINE. Next: controls 2 and 3, each with a fresh verified backup and full hold. Accepted count 11; Simple Minimap/Chest Labels retests and final 13-mod validation NOT RUN. Custom Main Questline NOT STARTED; production world NOT CREATED. Branch: `claude/m3-task4-live-windows`; run code checkpoint: `e4be52f`.

## Codex takeover preflight — 2026-10-04

Accepted state remains **11 mods**, server **OFFLINE**, production world **NOT CREATED**. Branch `claude/m3-task4-live-windows` at `26f7a1c` matched the remote after fetch. Tracked files were clean; pre-existing untracked work was preserved.

The accepted 11-mod order, every package hash and size match snapshot `p13-dbno-1`, with no extra installed packages. The live database is byte-identical to accepted backup `2026-10-04_062716`; WAL is empty. A hash-verified copy returned `quick_check=ok`, with live files rechecked unchanged. Required mailbox/controller singleton counts are 1/1/1; Player DBNO has one controller, and all 30 persisted controller IDs are unique. The inherited WickProbe actor is unchanged baseline state. Server log build: CL-377096. Detailed hashes and operational evidence remain local.

No server or harness process or server-related scheduled task was found; the host was reasonably quiet. Build PASS, no warnings/errors. Initial sandboxed tests: 489 passed and 2 failed in web-admin/process inspection checks. With required process/network access, all **491 tests passed**, none failed/skipped; the test report is retained locally.

**Exclusive control confirmed by the operator:** the prior validation session is paused/finished; only Codex may operate the server and publish validation results during this phase. The initial preflight waited for this confirmation because the application's action gate coordinates only within one process.

**Next action:** recheck mutable state, then run THREE unchanged 11-mod control boots, each with a verified pre-run backup, immutable evidence, at least 10-minute hold, graceful shutdown and integrity/persistence checks. The warning catalog stays unchanged for all three. Control boots 1/2/3 NOT RUN at this checkpoint; neither priest ID is assessed by new control evidence. No catalog change proposed/applied. Simple Minimap and Chest Labels remain deferred; retests and final 13-mod validation NOT RUN. Latest accepted verified backup: `2026-10-04_062716`. Safe to start Custom Main Questline: NO. Client and source packages unchanged.

- CURRENT LOCAL TIME: 2026-10-05 13:1x +07:00.
- CURRENT BRANCH: claude/m3-task4-live-windows (primary checkout of the repo).
- CURRENT HEAD: the docs commit after a4beabb (2.2.3 migration).
- PRESERVED SAFETY BRANCH: codex/m3-batch-d-safety at abe3875 (original checkpoint 0bb2b2a). Its worktree is E:\github\gameee\.worktrees\batch-d.
- CURRENT STAGE: MIGRATED TO CONAN EXILES ENHANCED 2.2.3 (CL-378132, release-beta). 13-MOD STAGING PACK SERVER-SIDE = PASS on 2.2.3 (smoke, 660 s full validation, clean restart). Conan Server Control points at the new 2.2.3 server; acceptance PASS. 2.2.3 client bundle READY. Old 2.2.2 environment PRESERVED FOR ROLLBACK (renamed folder, untouched). Custom Main Questline PAUSED. Production world NOT CREATED.
- LAST COMPLETED CHECKPOINT: desktop app published self-contained, daily data directory seeded from verified copies (settings, RCON secret, 13-mod catalog, backups 041457/032214), desktop shortcut created; app opens and shows OFFLINE with 13 mods.
- CURRENT ACTIVE MODLIST (accepted 13, on 2.2.3): StackMe10K.pak -> SavageParagon.pak -> GritandGrease.pak -> ThrallReputation.pak -> ImprovedThrallsAndQoL.pak -> WO_RidingThralls.pak -> Ancient_Realms.pak -> Cannibal_Captivity.pak -> NightTerrors.pak -> PvEPlusAmbush.pak -> PlayerDBNO.pak -> Simple_Minimap.pak -> ChestLabels.pak
- LATEST VERIFIED BACKUP: 2026-10-05_041457 (final 2.2.3, after the acceptance cycle). 2.2.3 chain: 040847 (restart), 035257 (full), 033640 (smoke). Migration source (last 2.2.2 world): 2026-10-05_032214. 2.2.2 validated: 2026-10-05_014347.
- SERVER STATE: OFFLINE. The daily desktop app (Conan Server Control) is now the intended controller; do not run the live-test harness while it controls the server.
- LAST SHUTDOWN CLASS: NORMAL (acceptance cycle final stop, exit 0; v223-final13-restart NORMAL 166.0 s).
- ACCEPTED WARNING GATES (ModBootGates): ITQoL mailbox x1; 7 Ancient Realms dangling refs; Ancient Realms MergeDataTables-null (exact, AR hash, max 2); Cannibal teardown set CANNIBAL-CAPTIVITY-TEARDOWN-NO-WORLD (exact hash, exact message and path family, after main-world teardown only, exactly 99, all-or-nothing); base-game noise kinds incl. the two healthy spawn-table ids only. Nothing for Night Terrors or PvE Plus Ambush was needed (no new LoadErrors or errors). Fantasy Races, Shemite and Thrall Wars errors are NOT whitelisted.
- ANALYSIS: every mod-boot writes an immutable snapshot (E:\CSC-M3-Live\live-test\batch-snapshots\<batch>, read-only, log hash-verified); pre-batch plans are pre-batch.json there too. Replay with `analyze-snapshot <dir> [backupId] [--current-catalog]`. Snapshots: cannibal-run-1/2, core8-final-*, shemite-run-1(-pre), p1-nightterrors-*, p2-pveambush-*, p3-thrallwars-*. Helper scripts are in the session scratchpad (phase_prep.ps1, phase_boot.ps1, phase_post.ps1, light_sampler.ps1).
- OPERATOR DECISION 2026-10-04: Thrall Wars Dungeon EXCLUDED / DEFERRED (do not whitelist the 73 LoadErrors, 3 LogMaterial or 4 LogModController merge errors; the loot-table row-structure mismatches are NOT accepted as harmless; keep all evidence, snapshots and the failed-world backup 2026-10-04_031850). Accepted pack = the 10 mods; Fantasy Races, Shemite and Thrall Wars excluded.
- (superseded) ROOM FOR ONE MORE earlier blocked on the source package (now supplied and tested; see the #11 checkpoint above). A read-only search (1,195 candidate .pak files on the profile, D:, E:, F:) found no package with that embedded Workshop ID; the WorkshopDL tool on F: holds none; nothing was downloaded or guessed. Needed: the .pak from the operator (or permission to download). When it arrives: prove it by embedded modinfo (id 3811298984, Enhanced, WindowsServer content, size, SHA-256, dependencies), then run the same fail-closed cycle (phase_prep.ps1 with BaseSnap final10-restart and BaseBackup 2026-10-04_035205, phase_boot.ps1, phase_post.ps1), keeping WO_RidingThralls installed, and check specifically for new mount/rider/passenger/attachment/seat/controller lines against the accepted 10-mod boot.
- MAIN SESSION 2026-10-05 18:21: this Claude session is the canonical MAIN session for Mod #14 (other tabs read-only). Branch claude/m3-task4-live-windows, HEAD 522f61b before this update.
- LIVE SERVER: ONLINE, operator-owned through the desktop app (ConanServerControl started 18:10, server PID 17884 started 18:11). The main session does NOT control or inspect it while it runs.
- DEV KIT INSTALL IN PROGRESS (operator-started 17:36 in the Epic launcher): 'Conan Exiles Enhanced Dev Kit', AppVersion **377800** (CL-377800: newer than 2.2.2 CL-377096, older than the 2.2.3 beta CL-378132), target `D:\epic\CEUE5Devkit` (D:, not E:), full size ~182 GB (D: had 280 GB free), ~65.5 GB staged at 18:21, still marked incomplete.
- OPERATOR DECISIONS 2026-10-05:
  - Mod #14 targets 2.2.3 / CL-378132 (no move back to 2.2.2).
  - Dev Kit 377800 is allowed for a STAGING vertical-slice attempt only.
  - Compatibility gate: identify the exact revision → cook a minimal Mod #14 test → inspect its metadata → prove the 2.2.3 server accepts it. Never fake metadata. If the server rejects it for a build mismatch, STOP and report. No full campaign build before this passes.
  - Abysmal Remnant: Dev Kit reference tracing (Dregs → controller → spawn request → NPC class → inheritance/DataTable/display name) is primary; a licensed-client kill is secondary. SewerAbomination is not accepted on its name alone.
  - New feature: a Main Quest tab.
- DESIGN DONE (no Unreal assets): `mods/CustomMainQuestline/MAIN_QUEST_UI.md`.
  - Reference projection `MainQuestView.cs`, `CampaignEngine.SetTrackedQuest`, banner contract (QuestComplete / NewBossUnlocked / CampaignMilestone), data schema v2 (`main-quests.provisional.v2.json`: acts, objective, lockedDisclosure).
  - Mod tests 80/80. Solution Release build: 0 warnings, 0 errors.
  - ConanServerControl.Tests 512/513: `Real_process_tree_is_tracked_after_the_launcher_exits_and_killed_as_a_whole` is intermittent (2 of 4 isolated reruns pass). It is unrelated to Mod #14 (no reference). Likely cause: the test counts cmd's conhost.exe child before ping.exe starts. Flagged as a separate task.
- STATE 18:41: Dev Kit ~78.5 GB of ~182 GB staged. Live server ONLINE under the desktop app, restarted by the operator at 18:29 (PID 20804). Not touched by the main session.
- OPERATOR DECISIONS 2026-10-05 (2):
  - LockedDisclosure default = Partial, not treated as secrecy.
  - Tab entry: a supported Enhanced menu hook is preferred; otherwise a standalone UMG panel on a configurable hotkey. No vanilla widget patching if a supported hook exists.
  - The hotkey waits for the input-conflict check.
- DESIGN-ONLY DONE (2):
  - Visual mockup https://claude.ai/artifact/LqogeEnkUB7EyX7WyHzBmb (private; source in mods/CustomMainQuestline/mockup/), on real provisional values.
  - INPUT_CONFLICT_AUDIT.md: read-only scan of client bundle 041519, 13/13 SHA ok, no mod changed. Taken: F1 hold and Shift+click on the map (Simple Minimap); ~ and Insert (console). Improved Thralls & QoL has its own hotkey system with unknown defaults. Candidate keys recorded; none chosen.
  - DEVKIT_CHECKLIST.md: Part A compatibility gate A1–A7; Part B Quest 01 trace B1–B9; Part C UI entry and input.
  - evidence/dregs-asset-paths-2.2.3.txt: names read from the server .utoc indexes, read-only.
  - A3C1 corrected: a broad content-set code, not Dregs-only.
  - Projection fix: equal level ranges print as one number.
  - Mod tests 81/81.
- 2026-10-05 ~22:15 STAGING TAKEOVER by main session (operator granted). Server OFFLINE, untouched so far; no backup or install made yet.
- DEV KIT RESULTS (evidence/devkit-results-2026-10-05.md):
  - A1/A2 PASS: 5.8.2-377800 ++exiles+release, ModVersion=1002. The server log declares compatible devkit revisions [1002].
  - C1: no supported Enhanced-menu hook, so the fallback applies (standalone UMG panel + configurable key).
  - C2/C3: F7/F8 free in vanilla and the observed ITQoL keys.
  - B1–B7 resolved statically: The Dregs (Gameplay_Dungeon_Sewer, separate from the Darkened Dregs/Nahjef) → D_S_SewerBoss1 → Wildlife_SewerAbomination ("Abyssal Remnant", Npc.Boss, Npc.Dungeon.Dregs) → BP_NPC_Wildlife_SewerAbomination_C → BaseBPWildlife_C → ConanCharacter.
  - B8 (death hook) open. Remnant still Unverified.
- TOOLING: the Dev Kit commandlet (UnrealEditor-Cmd) crashes with STACK_OVERFLOW in AssetRegistry during its startup scan (twice). The GUI editor works. Retest with the registry cache is running. Dev Kit binaries not modified.
- BLOCKER for step 5 (probe): mod creation is GUI-only (Dev Kit 'Create Mod' → SaveModInfo writes the revision). BuildMod copies modinfo.json verbatim, so hand-writing modinfo would mean writing version metadata (forbidden). The operator must create the mod in the Dev Kit UI, or grant desktop control.
- 2026-10-05 ~23:55 MOD #14 PAUSED by operator ("test tổng thể trước"). State at pause:
  - Dev Kit mod `MQ14CompatProbe` exists (active mod) with one asset `BP_MQ14CompatProbeController`, an empty child of /Script/DreamworldMods.ModController. It is NOT built: no output pak, modinfo devkitRevisionNumber still 0.
  - Scratch mod `mod_game` (display name also set to MQ14CompatProbe) is unused.
  - No backup, install or boot was done for the probe. Staging server OFFLINE, 13-mod pack unchanged; server Mods = client bundle 041519 (13/13 SHA-256, modlist identical).
  - UI text is Vietnamese (MainQuestText.cs; data v2).
- RESUME POINT: Build mod in the Dev Kit → inspect modinfo (revision must be written by the Dev Kit; never edited) → DEVKIT_CHECKLIST A5–A7.
- 2026-10-06 00:01 SERVER CONTROL BACK TO OPERATOR: the operator started the staging server through the desktop app (ConanServerControl.exe, server PID 29376) for the overall play test. No pre-start cold backup was taken by the main session. The main session does not run the harness, backups or start/stop while the app controls the server. Connect: Direct Connect over Radmin VPN or LAN, port 7777 (IPs shared privately). After the session: graceful Stop in the app, then the main session reviews the session log and shutdown classification.
- 2026-10-06 02:05–02:30 MOD #14 RESUMED, then STOPPED at the compatibility gate (evidence/devkit-results-2026-10-05.md, section A4–A7):
  - Probe built (Dev Kit 377800, modinfo revision 1002 written by the Dev Kit, layout PASS). Pre-probe backup 2026-10-06_020925. Imported as #14.
  - Boot mq14-probe-1: the controller spawned, then the server **hung at world load**; harness FORCE STOP after ~10 min (exit 1).
  - Evidence kept: live-test/mq14-probe/evidence-forced-kill + backup 2026-10-06_022151.
  - Recovery: probe removed, pre-probe backup restored (world = 43a2c2d1…, WAL 0).
  - Control boot with the 13 mods only: PASS, NORMAL 189.3 s, exit 0, no orphan, quick_check ok.
  - BLOCKER: the probe hangs CL-378132 (likely the Dev Kit 377800 vs server 378132 gap; unproven). Quest 01 NOT started. Server OFFLINE with the 13-mod pack.
  - Latest verified backup: 2026-10-06_020925. Last shutdown: NORMAL (control boot).
  - Operator decision needed: re-run the probe / wait for a matching Dev Kit or move to the live release / non-controller probe variant.
- 2026-10-06 ~14:50–16:40 DIFFERENTIAL PROBE ISOLATION (operator plan A/B/C) + STAGING REBUILT ON A NEW STEAM INSTALL:
  - Old server folder `D:\Conan Exiless` disappeared (not in the recycle bin; the operator reinstalled from Steam). New licensed installs: server `D:\steamnew\steamapps\common\Conan Exiles Dedicated Server` (appmanifest 443030 buildid 25639945) and client `D:\steamnew\steamapps\common\Conan Exiles` (440900 buildid 25639639). Both are `++exiles+release-beta-CL-378132`, the same CL as validated staging; no version change.
  - Old failed probe: labelled FAILED CONTROLLER PROBE #1 in live-test/mq14-probe/evidence-forced-kill (pak SHA 051C…6876, log, DB/WAL, LABEL file). Backup 2026-10-06_022151 kept.
  - Probe mods created in the Dev Kit UI: MQ14ProbeA/B/C. PROBE A built via the GUI Build mod: `MQ14ProbeA.pak` 38,297 B, SHA 525e94e1…e818, one CurveFloat with no dependencies, modinfo revision 1002 (written by the Dev Kit), standard 11-file layout. Not yet tested (the precheck stopped the first attempt because the old server folder was gone; nothing touched).
  - Rebuild (harness, CSC_SERVER_DIR = new server):
    1. Game.ini [RconPlugin] taken from backup 2026-10-06_020925.
    2. Vanilla boot: ready 34.5 s, NORMAL 127 s, exit 0.
    3. restore 2026-10-06_020925: world + config + modlist, world matches backup, WAL 0. Vanilla world saved as 2026-10-06_162554.
    4. replace-local ×13 from bundle 041519: 13/13 SHA, modlist identical.
  - VALIDATION BOOT steamnew-13mod-validation = **NOT PASS**:
    - readiness 36.3 s; LoadErrors exact baselines, no unattributed;
    - **1 UNKNOWN**: `SpawnTable: Error: ... could not find weighted table with id: Exile_Priest_4_Nordheimer`, a vanilla 2.2.3 data line already seen in the vanilla identification boot during the migration; not mod-attributed; NOT whitelisted;
    - shutdown **DEGRADED 394.4 s** (graceful, acknowledged, exit 0, no forced kill, no orphan; previous runs 166–195 s).
    - Post-check on a DB copy: quick_check ok, 32 mod_controllers, no duplicate actor ids, no WAL.
  - STOPPED before Probe A per the batch rules. Server OFFLINE on the new install with the exact 13-mod pack. Latest verified backup: 2026-10-06_020925 (+ replace-local backups). Last shutdown: DEGRADED.
  - Operator decision needed: (a) accept `Exile_Priest_4_Nordheimer` as a known vanilla warning gate (commit), (b) re-run the validation boot once to see whether the slow shutdown repeats. Then Probe A.
- 2026-10-06 17:10 VALIDATION-2 PASS (NORMAL 180.9 s; Nordheimer line did not repeat, not whitelisted). PROBE A (data only) PASS:
  - mounted as #14, readiness 36.3 s, 0 unknowns, NORMAL 179.3 s, integrity gates PASS;
  - rolled back to 2026-10-06_165631; baseline-after PASS (NORMAL 167.7 s).
  - Toolchain/package compatibility: SUPPORTED (empirically).
  - NEXT: PROBE B (empty ModController in MQ14ProbeB, definition documented before packaging). Server OFFLINE, exact 13 mods, latest verified backup 2026-10-06_165631.
- 2026-10-06 ~17:40 HANDOVER TO A NEW SESSION (operator: continue in a new session with Computer use enabled; this session had no desktop-control tools):
  - Tooling now in repo: `mods/CustomMainQuestline/tools/devkit/` (see its README): probe creation/doc scripts, `probe_cycle.ps1` (staging cycle through the harness on the Steam install), read-only pak/IoStore inspectors.
  - Staging = `D:\steamnew\steamapps\common\Conan Exiles Dedicated Server` (CL-378132), OFFLINE, exact 13 mods, latest verified backup 2026-10-06_165631. Harness env: CSC_LIVE_TESTS=1, CSC_LIVE_ROOT=E:\CSC-M3-Live, CSC_SERVER_DIR=<server above>, CSC_CLIENT_ROOT=`D:\steamnew\steamapps\common\Conan Exiles`. Harness exe: tests/ConanServerControl.LiveHarness/bin/Release/net8.0.
  - Licensed client `D:\steamnew\steamapps\common\Conan Exiles` has the 13 mods (copied from bundle 041519, 13/13 SHA).
  - PROBE B state: mod MQ14ProbeB is ACTIVE in the Dev Kit; asset `/Game/Mods/MQ14ProbeB/BP_MQ14ProbeBController` (empty child of /Script/DreamworldMods.ModController) is created and saved; NOT documented, NOT built. A Dev Kit editor may still be open (opened 17:10).
  - RESUME STEPS:
    1. In the open editor, run `py "E:\github\gameee\mods\CustomMainQuestline	ools\devkit\probeB_doc.py"` in the Cmd box; this writes probeB_definition.json next to the script.
    2. Use the Dev Kit window (yellow-sparkle toolbar button) → Build mod (active mod MQ14ProbeB) → OK. The GUI build writes revision 1002.
    3. Inspect Saved/Mods/MQ14ProbeB/Output/MQ14ProbeB.pak: modinfo revision 1002, 11-file layout, SHA.
    4. Close the editor (RAM), then run `tools/devkit/probe_cycle.ps1 -Name MQ14ProbeB -Pak <pak> -Batch mq14-probeB-1`.
    5. Baseline boot after; then Probe C (MQ14ProbeC: Probe B + exactly one BeginPlay log node, added in the editor GUI).
  - Do NOT rerun FAILED CONTROLLER PROBE #1. Quest 01 NOT started. Production world NOT CREATED.
- 2026-10-06 ~22:45 NEW SESSION (Computer use on the Dev Kit editor) took over Mod #14 Probe B:
  - Step 1 DONE: `probeB_doc.py` → `tools/devkit/probeB_definition.json`. Parent `/Script/DreamworldMods.ModController`; only inherited components (BillboardComponent `Sprite`, ActorPersistenceComponent `PersistenceComponent`); no own components; CDO replicates/always_relevant/net_load_on_client = True (same as the base CDO), hidden, tick enabled at 0.0. Interfaces and graph nodes not readable from Python (protected), not modified.
  - Step 2 DONE: GUI Build mod (active mod MQ14ProbeB), ~20 min, "Mod built successfully".
  - Step 3 A5 PASS: `MQ14ProbeB.pak` 271,245 B, SHA-256 `0E63F87FB16B49AC495703A5A29C67C1E1BF977893C8D14AD3ACDF582D34AF29`; embedded modinfo `devkitRevisionNumber 1002` (written by the Dev Kit), snapshot 0, `minimumVersion Enhanced`, no Workshop ID; standard 11-file layout. No metadata edited.
  - Editor closed (File > Exit). Release build 0 warnings/0 errors; tests 82/82 + 513/513.
  - Step 4 **PROBE B PASS** (`probe_cycle.ps1`, batch `mq14-probeB-1`, 23:20–23:28): pre-probe backup `2026-10-06_232037` verified; mounted as #14; `Persistence: Spawning mod controller: BP_MQ14ProbeBController_C`; readiness 42.2 s; 0 unattributed LoadErrors; shutdown NORMAL 183.2 s, exit 0, no forced kill, no orphan; quick_check + ITQoL/AR gates PASS. Probe removed, `2026-10-06_232037` restored (WAL 0), exact 13 mods. Details: evidence/devkit-results-2026-10-05.md, "PROBE B".
  - Step 5 baseline `mq14-baseline-after-probeB` (13 mods): PASS, readiness 36.3 s, NORMAL 173.9 s, 0 unattributed, integrity gates PASS.
  - Meaning: an empty Dev Kit 377800 ModController spawns and runs on CL-378132; the probe #1 hang is not explained by the controller class/spawn alone.
  - STATE: server OFFLINE, exact 13-mod pack, latest verified backup `2026-10-06_232037`, last shutdown NORMAL. Dev Kit editor closed. Quest 01 NOT started. Production world NOT CREATED.
- 2026-10-07 00:20 **PROBE C PASS → COMPATIBILITY GATE A4–A7 PASS** (operator: continue until Mod #14 is complete and installed on server + client):
  - MQ14ProbeC set active (RunUAT SetActiveMod). `probeC_create.py` + `probeC_logic.py` (editor Python `BlueprintGraphEditor` API) built `Event BeginPlay → PrintString("MQ14ProbeC BeginPlay")`, compile 0/0; `probeC_definition.json`.
  - GUI Build mod: `MQ14ProbeC.pak` 272,817 B, SHA-256 `32670603…7963`, revision 1002 (Dev Kit), 11 files.
  - `mq14-probeC-1`: backup `2026-10-07_000719`; log line `MQ14ProbeC BeginPlay` on the server; readiness 43.1 s; 0 unattributed; NORMAL 217.1 s; integrity PASS; rolled back, exact 13. Baseline `mq14-baseline-after-probeC` PASS (NORMAL 120.0 s).
  - B8 lead (reflection, read-only): `ConanCharacter.SignalOnKilled(Character, Killer)` multicast delegate; `StableIdFunctionLibrary.GetActorStableId`; ModController auto-persists through its `PersistenceComponent` (ITQoL persists only its SaveGame variables).
  - Tooling: the Windows IME (TextInputHost) kept focus and blocked GUI clicks (operator denied granting it). Enabled the official PythonScriptPlugin remote execution, bound to 127.0.0.1 only, in `D:\epic\CEUE5Devkit\UE4\Saved\Config\WindowsEditor\Engine.ini` (backup `.bak-mq14-20261007`); `tools/devkit/ue_exec.py` runs a script in the open editor.
  - STATE: server OFFLINE, exact 13 mods, latest verified backup `2026-10-07_000719`, last shutdown NORMAL. Quest 01 NOT built yet. Production world NOT CREATED.
  - 2026-10-07 ~01:00 Quest 01 slice IN PROGRESS: Dev Kit mod `MQ14MainQuest` created in the GUI ("Create a new mod", active; modinfo revision still 0 until the first GUI Build mod). Builder `tools/devkit/mq14_build_controller.py` (editor Python): server binds `SignalOnKilled` on `BP_NPC_Wildlife_SewerAbomination_C`, Dregs location gate (120 m of D_S_SewerBoss1), 50 m credit radius, StableId list, HUD banner (`ConanPlayerController.ClientHUDShowNotification`); client panel = `ClientShowRichMessageBox` on **F7** (operator 2026-10-07: F7; vanilla J/OpenJourney untouched). Partial runs reached the persistence call; one in-place rebuild crashed the editor (no asset saved), so the script now refuses to run when the asset exists (delete the .uasset with the editor closed). The SaveGame flag must be set in the variable Details panel (not settable from Python). Python remote execution does not start in this Dev Kit build; the section added to `UE4\Saved\Config\WindowsEditor\Engine.ini` is to be removed with the editor closed (backup `.bak-mq14-20261007`). Current blocker: GUI typing into the Cmd box loses focus while the operator uses the desktop.
  - 2026-10-07 02:00 **Quest 01 controller BUILT, compile 0/0, saved** (`UE4\Content\Mods\MQ14MainQuest\Local\BP_MQ14MainQuestController.uasset`, 219,265 B; commit 8ef83ab). Built headless with `UnrealEditor.exe ... -ModDevKit -ExecutePythonScript=<builder>` (editor exits by itself; one build per session). Hook `State|BindEventtoSignalonKilled`, persistence `Dreamworld|Persistence|Setdirtyflag`, F7 `Input|KeyboardEvents|F7`.
  - REMAINING GUI-ONLY STEPS (no Python API, confirmed by discovery 2026-10-07): (1) open the Blueprint, select `MQ01_CompletedIds`, tick **SaveGame** (Details → Advanced), Compile, Save; (2) Dev Kit window → **Build mod** (calls the C++ `SaveModInfo`, which writes devkitRevisionNumber 1002; the CLI BuildMod would copy revision 0). Blocker: the Windows IME window (TextInputHost) takes the foreground and blocks GUI clicks while unattended; operator help or an idle desktop without the IME is needed.
  - 2026-10-07 09:25 **MOD #14 QUEST 01 INSTALLED ON STAGING SERVER + CLIENT** (details: evidence/devkit-results-2026-10-05.md, "MOD #14 QUEST 01"). Pak SHA `585F52AA…1C1D`, revision 1002. Staging boot `mq14-q01-1` PASS (controller started, 0 unknowns, NORMAL 164.3 s); baseline after PASS; then installed: backup `2026-10-07_092110` (verified) → import-local → **14 mods**; client Mods has the same pak and an identical 14-line modlist (backup `modlist.txt.bak-before-mq14-20261007`).
  - STATE: server OFFLINE, 14 mods (13 + MQ14MainQuest), latest verified backup `2026-10-07_092110`, last shutdown NORMAL. Production world NOT CREATED.
  - 2026-10-07 09:26 (operator chose option 1) daily desktop app updated: `E:\ConanServerControl\data\settings.json` (backup `settings.json.bak-before-steamnew-mq14-20261007`) now points at `D:\steamnew\steamapps\common\Conan Exiles Dedicated Server` (+ client root `D:\steamnew\steamapps\common\Conan Exiles`) and lists MQ14MainQuest as Local mod #14 (source `E:\CSC-M3-Live\mod-sources\MQ14MainQuest\MQ14MainQuest.pak`, SHA 585F52AA…). App opened: OFFLINE, "14 installed, 0 updates", Mods page shows MQ14MainQuest Order 14. Server NOT started. The operator now runs the play test through the app (the main session does not control the server while the app does).
  - 2026-10-07 09:30–09:55 **GAME UPDATE 2.2.3 → 3.0.0 (CL-378787, `++exiles+release`)**: Steam updated the licensed client; the operator's join failed with "client outdated". Server stopped gracefully under the app (~140 s); pre-update backup `2026-10-07_094300` verified; the operator updated the dedicated server in Steam (appmanifest 443030 buildid 25738716, exe `++exiles+release-CL-378787`, `ProjectVersion 3.0.0`). Mods (14, hashes unchanged), Game.ini [RconPlugin] and the world survived the update.
  - Validation boot `cl378787-14mod-validation` (14 mods) = **NOT PASS**: readiness 35.4 s; server still accepts devkit revision `[1002]`; MQ14 controller started; NORMAL 174.1 s; quick_check + ITQoL/AR gates PASS; **1 UNKNOWN**: `SpawnTable: Error: … could not find weighted table with id: Exile_Priest_4_Hyrkanian` (frame 0). Same id previously seen intermittently (p12-minimap-1, p14-chest-restart; Nordheimer variant in steamnew-13mod-validation and vanilla 2.2.3); per the 2026-10-04 baseline-variance policy it stays UNKNOWN and is NOT whitelisted. Progression STOPPED.
  - Operator decision needed: (a) control boots on CL-378787 — vanilla and the 13-mod pack without MQ14 — to establish whether the id is a 3.0.0 baseline line (then commit an exact known-warning gate) or (b) rerun the 14-mod validation; until then the Quest 01 play test waits. Server OFFLINE, 14 mods, latest verified backup `2026-10-07_094300`.
  - 2026-10-07 10:03–10:17 CONTROLS on CL-378787 (operator chose option 1; `tools/devkit/controls_cl378787.ps1`, evidence `E:\CSC-M3-Live\live-test\update-378787\controls`): control backup `2026-10-07_100339` verified.
    - **Vanilla (empty modlist)**: readiness 32.5 s, NORMAL 176.6 s; missing weighted-table ids: `Exile_OrchidPriest_4_Nordheimer`, `WarTestLongLeash`, `Wildlife_Siptah_Firstman_Warrior4`. So the vanilla 3.0.0 game itself logs a missing `Exile_*Priest_4_*` weighted table (as vanilla 2.2.3 did with `Exile_Priest_4_Nordheimer`).
    - **13 mods (MQ14 removed)**: `cl378787-13mod-control` PASS, 0 unknowns, NORMAL 213.8 s, integrity gates PASS; only the two accepted ids.
    - Restored to `2026-10-07_100339`, MQ14 re-imported, exact 14-mod state verified.
    - Reading: the priest lines are vanilla, random per boot (Nordheimer variants proven in vanilla 2.2.3 and 3.0.0; Hyrkanian seen only in modded boots so far, including before MQ14 existed). MQ14 touches no spawn table. Not whitelisted yet: operator decision on a known-warning gate.
  - 2026-10-07 10:25 **Operator decision A**: base-game noise kind SPAWNTABLE-PRIEST-VARIANT committed (exact message, ids Exile_Priest_4_<Race> / Exile_OrchidPriest_4_<Race> only; tests for near misses). Re-judged cl378787-14mod-validation with the CURRENT catalog: **PASS** (0 unattributed, 0 unknown). Staging on 3.0.0 with 14 mods is validated.
  - 2026-10-07 11:10 Operator paused the rename to MainQuestProgress and the 5 new mods (Better Thrall ICONS, Better Tavern PATRONS, Tot ! Enhanced Sudo, ModControlPanel, Edit Appearance 1.2.8; sources copied to E:\CSC-M3-Live\mod-sources with SHA-256). Kept MQ14MainQuest for the Quest 01 test; active Dev Kit mod set back to MQ14MainQuest; an empty Dev Kit mod folder MainQuestProgress exists (unused). Rejected: Tot ! Enhanced Custom (author: mostly incompatible with IQOL), Organizer Sorting Chest (duplicates ITQoL organizer; operator will enable the ITQoL one). Staging unchanged: 14 mods, validated on 3.0.0.
  - 2026-10-07 11:40 **QUEST 01 RUNTIME VERIFIED** (evidence: devkit-results, 'QUEST 01 RUNTIME VERIFICATION'): kill logged as BP_NPC_Wildlife_SewerAbomination_C / 'Abyssal Remnant', MQ14 MQ01 COMPLETE character=198, banner + F7 confirmed, completion survived a server restart. MQ01 set to Verified in the data (tests updated, 89/89 + 514/514). The installed pak's F7 text still says 'Abysmal'; the builder now says 'Abyssal' for the next build. Server currently ONLINE under the desktop app (operator).
  - 2026-10-07 11:46–11:56 **19-MOD BATCH = NOT PASS, ROLLED BACK** (	ools/devkit/install_19.ps1, evidence E:\CSC-M3-Live\live-test\install-19): backup 2026-10-07_114642 verified; read-only check of its DB copy: BP_MQ14MainQuestController_C.MQ01_CompletedIds present (24 bytes) — MQ01 progress persisted. Imported Better_Thrall_ICONS, Better_Tavern_PATRONS_E, ModAdmin (Tot ! Enhanced Sudo), ModControlPanel, IdeaPoet_EditAppearance (after MQ14). Validation cl378787-19mod-validation: readiness 40.0 s, NORMAL 177.2 s, integrity gates PASS, but UNKNOWN: ModAdmin.pak 1 LoadError (no validated set); runtime at frames 3–18: Main: Error: Data: Energy source heat map not loaded /Game/Systems/Temperature/TemperatureHeatMapData (+ _Siptah) x6 and LogDreamworld: Error: GetPlayerId could not return a valid UID! Player may be logged out! x1 — none in the 14-mod runs. Rolled back: 5 × remove-local + restore 2026-10-07_114642 → exact 14 mods (MQ14 hash OK). App/client NOT changed. Next: per-mod attribution boots (operator decision).
  - 2026-10-07 12:20–12:57 **18-MOD PACK INSTALLED (server + client + app)**. Operator dropped Tot ! Enhanced Sudo (its ModAdmin LoadError + GetPlayerId error). 18-mod validation cl378787-18mod-validation: readiness 36.4 s, NORMAL 160.4 s, integrity PASS; only unknown = Main: Error: Data: Energy source heat map not loaded /Game/Systems/Temperature/TemperatureHeatMapData(_Siptah). Bisect (	ools/devkit/bisect_heatmap.ps1): present with 16, 15 and 14 mods on the post-play world, and in a **NO-MOD vanilla boot of the post-play world** (2026-10-07_114642); absent on the pre-play world → vanilla temperature-system line triggered by world content. Committed base-game noise TEMPERATURE-HEATMAP-NOT-LOADED (exact message, two shipped assets; tests). Re-judged 18-mod validation with the current catalog: **PASS**. Final install: backup 2026-10-07_125454 verified → import ICONS, Tavern PATRONS, ModControlPanel, EditAppearance → 18 paks hash-identical to the validated snapshot. Client Mods synced (18, identical hashes and order; backup modlist.txt.bak-before-18mods-20261007). App settings.json (backup .bak-before-18mods-20261007): 17 Workshop entries with ids + SHA-256, MQ14MainQuest Local (#14); app shows OFFLINE, 18 installed. Order: 13 base, MQ14MainQuest, Better_Thrall_ICONS, Better_Tavern_PATRONS_E, ModControlPanel, IdeaPoet_EditAppearance.
  - 2026-10-07 15:10–15:20 (operator): RCON broadcast to the online player (ducc) at T-2 min and T-30 s, then graceful STOP through the app (190 s, clean Exiting). Backup 2026-10-07_151720 verified. ServerSettings.ini: DropEquipmentOnDeath=False (equipped gear stays, backpack drops; backup ServerSettings.ini.bak-before-deathdrop-20261007). Requested mod 3720737911 = Extended Thrall Stats (Enhanced): NOT in the client Workshop folder (not subscribed; the Workshop page may have been removed) — not added. Operator waived the boot check for that mod. Server OFFLINE, 18 mods.
  - 2026-10-07 15:23 Extended Thrall Stats (Enhanced) 3720737911 (ExtendedThrallStatsEnhanced.pak, rev 1002) downloaded after the operator subscribed (Cloudflare WARP connected on request). Installed as mod #19 on server (harness import-local), client (modlist backup .bak-before-19mods-20261007) and app (Workshop entry; settings backup .bak-before-19mods-20261007) — **operator waived the validation boot**. Server OFFLINE, 19 mods; app shows 19 installed.
  - 2026-10-07 ~15:45 **GAMEPLAY BALANCE applied (server OFFLINE, no boot — operator paused the server)**. Operator prompt Claude_Server_Balance_Prompt_EN_Thrall035. All 32 requested keys found in ConanSandboxServer-Win64-Shipping.exe (none unsupported/obsolete). ServerSettings.ini backup ServerSettings.ini.bak-before-balance-20261007; single [ServerSettings] section, no duplicate keys; 21 lines changed + 9 appended. New: Harvest/ResourceRespawn 5.0, CraftingCost 1.0, PlayerDamage 0.8, PlayerDamageTaken 1.15, NPCDamage 1.35, NPCDamageTaken 0.65, StaminaCost 1.15, ThrallDamageToNPCs 0.35 (was 0.3), minion limit True 5/1/2/60 (was False 50/5/10/60), XP rate 0.6 / time 0.15 / kill 1.0 / harvest 0.7 / craft 0.65, ItemConvertion 0.75, FuelBurnTime 1.25, ThrallConversion 0.75, AnimalPen 0.75, active hunger 1.2 / thirst 1.25, idle 0.7/0.7, corruption gain 1.15, EnableSandStorm True, Durability 1.0, EverybodyCanLootCorpse True. **Not changed: DropEquipmentOnDeath** (prompt said True; operator wants backpack-only drop, equipment and hotbar kept). Binary evidence: in 3.0.0 the admin-panel combo CBC_DropOnDeath is bound to the ini key DropEquipmentOnDeath and is the enum EDropOnDeath {DropNothing, DropEverything, DropBackpack} ("The backpack inventory is dropped on death"). The ini value format for DropBackpack is unknown and the legacy False probably maps to DropNothing, so the 15:20 note "False = backpack drops" is UNVERIFIED. Plan: operator sets Server Settings → Drop On Death → Drop Backpack in the in-game admin panel, then read back the value the server writes. Boot/restart validation and in-game balance checks NOT done.
  - 2026-10-07 (operator, server OFFLINE): EverybodyCanLootCorpse=True -> **False** (only the owner can loot their corpse; same backup as above).
  - 2026-10-07 (operator, server OFFLINE): DropEquipmentOnDeath=False -> **2** (operator choice; intended EDropOnDeath::DropBackpack by enum order Nothing=0/Everything=1/Backpack=2). UNVERIFIED until a boot: check the admin panel shows Drop Backpack and that the server does not rewrite the value.
  - 2026-10-07 15:49 Server STARTED via the desktop app (operator request; WARP off). Ready 15:50. The server re-serialized ServerSettings.ini at 15:48:54 (key order changed) and kept every value: all 31 balance keys match, DropEquipmentOnDeath=2, EverybodyCanLootCorpse=False. In-game check of Drop On Death = Drop Backpack still pending. ITQoL: no admin settings stored in the world DB (read-only copy of backup 2026-10-07_151720: only Player List, mailbox flag, per-character guide flags) -> ITQoL admin settings are at mod defaults (mod says everything disabled by default). Server ONLINE.
  - 2026-10-07 16:11–16:17 (operator): RCON notice to ducc at T-2 min and T-30 s, graceful STOP through the app, clean 'Exiting' 16:17:08, no Conan processes. Server OFFLINE.
  - Mod translation pilot (client-only, read-only analysis in scratch; nothing installed): the Vietnamese patch is a client pak ~mod\pakchunk0-Windows_P_999.pak that overrides the 18 en .locres files. Mod IoStore containers decompressed locally (UnrealPak + Dev Kit oo2core.dll). Extended Thrall Stats: its 7 FTexts reuse vanilla keys already translated by the patch (except 'Attribute Stats'); the English labels Details / Damage per Attribute Point / Melee+Ranged Damage Multiplier / Health per Vitality / Alias come from the mod's own built-in language table (EN/DE/IT/ES/FR/PL/PT strings), NOT locres -> cannot be translated without editing the mod. Localizable FText counts: ITQoL 1810, EditAppearance 181, GritandGrease 134, Simple_Minimap 92, PvEPlusAmbush 37, NightTerrors 35, StackMe10K 32, ModControlPanel 20, WO_RidingThralls 12, Ancient_Realms 11, ThrallReputation 10, PlayerDBNO 9, Cannibal_Captivity 2, SavageParagon 2, others 0.
  - 2026-10-07 ~16:40 Translation pilot Player DBNO INSTALLED on the client: 3 entries added to Exiles_UI.locres in namespace '' (downed message, '(Press Interact to give up)', 'Dying'); corpse-harvest line was already translated; other DBNO texts are plain strings (not translatable). Rebuilt pakchunk0-Windows_P_999.pak (43 entries, same mount point, pak v12 Zlib; original was v3) replaced on the client only; original .pak/.ucas/.utoc backed up to E:\CSC-M3-Live\client-backups\vihoa-original-20261007. Tools: tools/localization. Operator to verify in game (get downed). Rollback: copy the backup pak back.
  - 2026-10-07 16:27 Patch zip (3 pakchunk0-Windows_P_999 files + HUONG_DAN.txt) sent to the operator for the friend. Server STARTED via the app, ready 16:28. Server ONLINE.
  - 2026-10-07 18:50–18:56 (operator): 3 RCON notices one minute apart (no players online), graceful STOP through the app, clean 'Exiting' 18:56:03, no Conan processes. Server OFFLINE. Next: more mod translations (Simple Minimap + ModControlPanel proposed; DBNO in-game check still pending).
  - 2026-10-07 ~19:10 Simple Minimap translated: 92 texts found, 86 translated (kept English: Lorem ipsum placeholder, 'Simple Minimap', 'Discord', "Xevyr's Mods"). Patch rebuilt from the ORIGINAL Viet hoa Exiles_UI.locres + DBNO + Minimap (15,593 entries) and installed on the client; zip sent to the operator for the friend. Tables in tools/localization/translations. In-game check pending. Server OFFLINE.
  - 2026-10-07 Mod Control Panel translated (20/20). Patch = original + DBNO 3 + Minimap 86 + Control Panel 20 (15,613 entries), installed on the client; zip VietHoa_Mod_DBNO_Minimap_ControlPanel sent. Server OFFLINE.
  - 2026-10-07 Translated Cannibal Captivity 2/2, Thrall Reputation 10/10, Ancient Realms 9/11 (kept '--- BEYOND ARCHITECTURE ---'), Riding Thralls 11/12 (kept placeholder 'Confirmation text'). Patch now 15,645 entries, installed on the client only (operator: no more zips; will ask for a combined package later). Server OFFLINE.
  - 2026-10-07 Translated StackMe10K 32/32, Night Terrors 35/35, PvE+ Ambush 34/37, Grit and Grease 118/134 (internal ids Fire1..Lightning3/None kept), Edit Appearance 128/181 (internal enum names kept). Patch 15,992 entries installed on the client. Next: ITQoL (1810) in batches. Server OFFLINE.
  - 2026-10-07 ITQoL translated: 1145/1321 unique texts (1604/1810 entries); kept English: internal ids (Is*/Settings_*/CamelCase), sample placeholders, URL, widget-type names. Patch now 17,596 entries, installed on the client only. All tables in tools/localization/translations (15 files). Every translatable mod is done. In-game check pending. Server OFFLINE.
  - 2026-10-07 Full patch zip VietHoa_Conan_13Mod.zip sent to the operator (client files hash-identical to the zip).
  - 2026-10-08 00:33–00:45 APPLIED: 3 RCON notices (khari + ducc online), server stopped (clean Exiting 00:36:28), verified cold backup 2026-10-08_003644 (hashes + SQLite ok). ServerSettings.ini backup .bak-before-minion-20261008 -> MinionPopulationBaseValue=15, PerPlayer=12, OverpopulationAllowed=5 (items 2 below DONE). Client patch with font fix + round 2 installed (SHA-256 9F536FAF...A37B; items 1 and 3 below DONE, in-game check pending). World reset NOT done (awaiting operator confirmation + production-world approval). Server OFFLINE.
  - 2026-10-08 00:55 **WORLD RESET (operator: 'reset the world brand new, including the main quest')** = the new world is the operator-approved PLAY world. Old world (33 game_0* files, game_0.db SHA-256 25D913F4...3496, = verified backup 2026-10-08_003644) MOVED (not deleted) to E:\CSC-M3-Live\world-archive\2026-10-08_pre-reset-staging. Server started via the app 00:55; new game_0.db created; MQ14 controller started; LoadErrors 61 = validated baseline (ITQoL 23 + AR 37 + Minimap 1). PreLogin failures in the log = a client connecting with a mismatched mod list. Mod settings read (read-only, backup copy) and written into MOD_SETTINGS_CHECKLIST.txt for re-entry. Server ONLINE.
  - **PENDING CLIENT CHANGES (operator: collect requests, apply all at once when the game is closed):**
    1. Font fix - Vietnamese diacritics render as boxes in Simple Minimap ("Did you know?" popup, map hint bar). Cause: game font KelsonSans (Normal/Bold/Light) lacks 92 Vietnamese glyphs (also used by DBNO, Night Terrors, Edit Appearance). Fix: patch ships the game's own Roboto Regular/Bold/Light (full Vietnamese coverage, verified by cmap) as Fonts/KelsonSans_0_{Normal,Bold,Light}.ufont. Built pak (46 entries, SHA-256 86DC8C9D...5C1C) saved in E:\CSC-M3-Live\client-pending\vihoa-20261007-fontfix with its response file; NOT installed (game client was running).
    2. SERVER (needs server OFFLINE + ServerSettings.ini backup): follower population for one 5-player clan, ~15 per person = 75 per clan. Change MinionPopulationBaseValue 5 -> 15, MinionPopulationPerPlayer 1 -> 12 (15 + 12 x 5 = 75), MinionOverpopulationAllowed 2 -> 5; keep UseMinionPopulationLimit=True, MinionOverpopulationCleanup=60. Reason: the balance-prompt values (5/1/2) count crafters/guards/pets too and the cleanup deletes excess thralls. Combat followers stay limited by ITQoL Additional Followers = 0.
    3. CLIENT translation round 2 (install together with item 1):
       a. Savage Paragon: the 24 item/feat DataTable texts (repair kits, Potion of Paragon Memory, lore). Paragon board node texts are plain strings (not translatable) -> Vietnamese lookup file PARAGON_TRA_CUU.txt instead.
       b. Mod item DataTables (keys like DT_NightTerrors_ItemTable_<id>_Name, missed by the 32-hex scanner; new tool extract_dt.py): Night Terrors 189, ITQoL 86.
       c. Base-game 3.0 texts missing from the Viet hoa patch (patch predates 3.0): Exiles_Items 225 missing (Blood of the Wicked, Pallid Shaggai Chitin, Pallid Mi-Go Ichor, Akhamet's Journal ...). Must be added with the base English source hash.
       d. Item names the Viet hoa author kept in English that the operator showed (Blood Crystal, Black Blood, Red/Blue/Green Crystal, Frost/Yellow/Purple/Black/Golden/Grey/Crimson Lotus, True Indigo ...): override in Exiles_Items.locres itself (same key must be replaced in the same file).
       STATUS 2026-10-08: a-d TRANSLATED and BUILT, not installed. Paragon 24/24 (+ lookup file PARAGON_TRA_CUU.txt, also on Desktop); Night Terrors 189/189; ITQoL items 85/86; base 3.0 items 202/225 (rest internal ids); 69 English-kept names overridden in Exiles_Items.locres (Sen Bang, Pha Le Mau, Mau Den ...). build_all.py rebuilds Exiles_UI (18,094 entries) + Exiles_Items from the original patch. Pending pak (font fix + round 2) SHA-256 9F536FAF...A37B in E:\CSC-M3-Live\client-pending\vihoa-20261007-fontfix.
       ROUND 3 (operator 2026-10-08, IN PROGRESS): translate (a) Exiles_Special_DLC texts missing from the patch (979 entries / 634 unique, ~86k chars: Vendhya DLC, building sets, Giant-king cultist sets ...) and (b) texts the patch kept in English (1,809 entries / 989 unique; skip god names, tech terms, chants, placeholders). Ancient Realms DROPPED by the operator. DONE 2026-10-08 and INSTALLED on the client (SHA-256 055D8B82...4657): DLC 633/634 unique (979 entries), kept-English 428 unique -> 1,069 overrides across 13 locres files (proper names, place names, gods, credits, chants, tech terms kept English). build_all.py now rewrites every touched locres; the response file points all 13 at vihoa_build.
       Later / undecided: rest of base-game missing (Dialogues 179, UI 266, Special_DLC ~980), Ancient Realms ~4,900, other kept-English names (~2,600).
    4. (open) further operator requests to be added here.
    - Known, not fixed: map "Reset Markers" button overlaps the Minimap hint text (base-game layout).
  - ITQoL admin settings advised (operator applies in game): elevator 2, guide on, horn drawbridge on, fast deposit on, stack multiplier 1 (StackMe10K), container space 2, pull fuel on / distance 30, organizer on / range 30 / bearer 15, search nearby on, recipes on, lock/unlock on (server has ContainersIgnoreOwnership=True), auto-lock off, door passcode optional, pick up all on.
  - (done) operator runtime test on staging — kill the Abysmal Remnant (The Dregs, D4) with the licensed client; expect the HUD banner, F7 panel showing completion, server log `MQ14 MQ01 COMPLETE character=<id>`; then restart and confirm F7 still shows completion. Only then mark MQ01 Verified.
  - (old) Then: inspect pak → `probe_cycle.ps1 -Name MQ14MainQuest -Pak <pak> -Batch mq14-q01-1` (server must log `MQ14 MainQuest controller started`) → baseline → install on staging + client (operator decision on keeping it installed).
  - Campaign order: operator's guide adopted (`PROGRESSION_ORDER.md`); MQ06 Midnight Grove hints and map cells added; 4 operator decisions pending (non-boss stops, Captain's Quarters boss, Unnamed City scope, Sunken City boss).
  - Boss order MQ02–MQ11 is PROVISIONAL and unverified (operator asked); plan: extract `Npc.Boss` rows/spawners/levels from the Dev Kit and get operator approval before enabling any later quest.
  - NEXT ACTION: Quest 01 vertical slice (mod `MQ14MainQuest`): server controller (SignalOnKilled bind on the Remnant class, Dregs location constraint, 50 m credit radius, SaveGame per-character progress), client panel + banner; then build, staging cycle, install on server and client.
- NEXT ACTION: probe mod `MQ14CompatProbe` created in the Dev Kit UI → main session adds an empty ModController Blueprint child via editor Python → BuildMod → inspect modinfo → backup → append #14 → boot → restore the pre-probe backup. Mod #14 not built; 13-mod pack unchanged; production world NOT CREATED.
- OPERATOR DECISION 2026-10-04: #11 WO - Room For One More EXCLUDED / DEFERRED (do not whitelist its 5 merge errors; do not revisit in this phase; keep evidence and failed-world backup 2026-10-04_050719). #12 Simple Minimap, #13 Player DBNO and #14 Chest Labels continue independently, cumulatively on the 10-mod baseline (expected 13 mods), then a final 13-mod validation with a clean restart; stop before the custom quest mod and report first.

## History (newest first; kept for evidence)

- P3 THRALL WARS DUNGEON FAIL / STOP, ROLLED BACK (batch p3-thrallwars-1, plan p3-thrallwars-pre; 2026-10-04 03:02-03:20): SlaveWarsServer.pak (A6238D37486FB25FCDD3E961D4B97256505C4B63D24016D7E5CC1A0960AD623B) mounted as order 1010 (3,091 packages) and ran stably (readiness 37.5 s, hold 660.6 s, shutdown NORMAL 176.0 s, quick_check ok, singletons 1/1/1, one new controller bpModController) but produced 73 new unvalidated LoadErrors (133 vs 60) and 7 new errors (3 LootTableRow-vs-LootTableWeightedRow MergeDataTables mismatches, 1 MergeIntoDataTable null, 3 LogMaterial). Nothing whitelisted. Rolled back to PRE-P3 backup 2026-10-04_030240 and verified (10-mod order and hashes exact, live DB identical to post-P2 backup 2026-10-04_030121). Failed-P3 world: 2026-10-04_031850; pre-restore 2026-10-04_031916; latest verified 10-mod backup 2026-10-04_031929. Final 11-mod validation NOT run.
- P2 PVE PLUS AMBUSH PASS server-side (batch p2-pveambush-1, plan p2-pveambush-pre): installed as mod 10; readiness 37.5 s, hold 660.5 s, 0 unknown, LoadErrors 60 vs 60, no new NPC/stat/spawn-table errors, no conflict between the two ambush systems, 3 controllers (one each in the world), shutdown NORMAL 177.0 s, quick_check ok, singletons 1/1/1. Verified backups: pre-P2 2026-10-04_024506, post-P2 2026-10-04_030121 (latest). Helper scripts live in the session scratchpad (phase_prep.ps1, phase_boot.ps1, phase_post.ps1): audit, backup, immutable plan, import, boot with sampler, post-run evidence.
- BOSS/PVE PHASE: P1 NIGHT TERRORS PASS server-side (batch p1-nightterrors-1, plan p1-nightterrors-pre): installed as mod 9 (SHA-256 2FE3E7AD7160ABA04DD9EAB99225BBD45D937E4B69525271F119348A7BCFBE61); readiness 36.4 s, hold 660.6 s, 0 unknown after the scanner fix af0b25e, shutdown NORMAL 177.3 s, quick_check ok, singletons 1/1/1, one new controller object. Verified backups: pre-P1 2026-10-04_022500, post-P1 2026-10-04_024312. Targets: P2 PvEPlusAmbush.pak (3721274811, SHA-256 C9C816FAE72C07626D4F0AD1994347CBDE110FBF6CB295AADD804540E4EC5B74), P3 SlaveWarsServer.pak = Thrall Wars Dungeon (3722829382, SHA-256 A6238D37486FB25FCDD3E961D4B97256505C4B63D24016D7E5CC1A0960AD623B). Run each on a quiet host; rollback and STOP on any unknown or DEGRADED/EMERGENCY stop.
- OPERATOR DECISION 2026-10-04: Shemite City State EXCLUDED / DEFERRED (do not rerun; do not whitelist its 9 LoadErrors or 6 spawn-table errors; keep all evidence, snapshots, backups). Core V1 = the 8 mods; Fantasy Races also excluded.
- FINAL 8-MOD CORE VALIDATION, QUIET-HOST RETRY (2026-10-04 01:3x-02:07): PASS. A (batch core8-final-2): readiness 36.4 s, 660.5 s hold, complete-log analysis 0 unknown (ITQoL 23, AR 37, mailbox x1, AR merge x2, Cannibal 99 teardown all exact), shutdown 175.1 s NORMAL at host CPU 36%, exit 0, no kill/orphan, quick_check ok, singletons 1/1/1. B (batch core8-final-2-restart): readiness 36.4 s, same exact analysis, shutdown 178.1 s NORMAL, quick_check ok. FINAL VERIFIED BACKUP 2026-10-04_020607 (also 2026-10-04_015607 post-run A, pre-final 2026-10-04_013946). CORE MODPACK V1 SERVER-SIDE = PASS. Gameplay NOT YET VERIFIED; production world NOT CREATED.
- FINAL 8-MOD CORE VALIDATION, ATTEMPT 1 (2026-10-04 01:0x, batch core8-final-1, plan core8-final-pre): readiness 71.3 s, hold 661.7 s, complete-log analysis PASS (0 unknown; ITQoL 23, AR 37, mailbox x1, AR merge x2, Cannibal 99 teardown all exact), quick_check ok, singletons 1/1/1, exit 0, no kill, no orphan. FAILED the criterion shutdown NORMAL/WARNING: 307.9 s DEGRADED while host CPU averaged 79% (a game was running; the same 8 mods stopped in 181.7 s and 199.2 s on a quiet host). Pre-final backup 2026-10-04_010449; post-run backup 2026-10-04_012406 (latest verified). Clean-restart validation NOT run. 8-mod state stays installed; no rollback needed.
- EARLIER CHECKPOINT (historical): Shemite batch (mod-boot --hold 660 --batch shemite-run-1, pre-batch plan shemite-run-1-pre): import PASS, readiness 41.6 s, order 1008, 1,999 packages, 661 s hold stable, quick_check ok, singletons 1/1/1, one new controller object; BLOCKED by 9 new unvalidated Shemite LoadErrors, 6 new spawn-table errors (scanner gap fixed in c0ef218) and a DEGRADED 319.6 s shutdown (cause not established; host free RAM was 450-1,100 MB). Nothing whitelisted. Rolled back to PRE-SHEMITE backup 2026-10-04_001912 and verified (live DB 0EE01DD4... and 8-mod modlist identical to the accepted state). Failed-batch world: POST-SHEMITE backup 2026-10-04_004137 and pre-restore backup 2026-10-04_004207.

## SDK and validation

Official Microsoft SDK 8.0.425 installed at C:\Users\vkkha\AppData\Local\Microsoft\dotnet-sdk-8.0.425 using the official win-x64 ZIP; SHA-512 verified against Microsoft release metadata. global.json and existing runtimes unchanged. Invoke this directory's dotnet.exe explicitly (system PATH still selects the runtime-only host).

The first full safety test run had 409 passes and one Windows sharing failure in the WAL fixture's File.ReadAllBytes. Replaced only the fixture's file read with ReadWrite/Delete sharing; final safety and merged suites both pass 410/410. Saved evidence: tests/ConanServerControl.Tests/TestResults/safety-resume-fixed.trx in safety worktree, and batch-d-merged.trx in primary checkout.

## D1 evidence and rollback

Source: C:\Users\vkkha\Downloads\mod conan\FantasyRacesOfExiles.pak. Embedded metadata confirms Workshop 3780741325, Enhanced, version 1.0.6. Size 5,293,057 bytes; SHA-256 2E4D3BEEC95FCBB81A9622A42405D2C3694EE632446D89C57EA8A93E27667D4A.

Production import created verified pre-D1 backups 142352 and 142353. Eight mods mounted in intended order; readiness at approximately 39 seconds: game port bound and world ticking (frame 2). Exact 23 ITQoL + 37 Ancient Realms LoadErrors passed. The harness scan at 14:24:53 missed subsequent NPC errors, so harness exit 0 is NOT D1 acceptance.

The generic MergeDataTables null-table error occurs twice in both D1 and the prior seven-mod boot; it is not newly attributable to D1. Lamplighter attachment warnings also existed previously. The new NPC error is the STOP B basis.

After clean shutdown, production restore of 2026-10-03_142352 passed. Production removal reconciled the catalog and retired the D1 pak to E:\CSC-M3-Live\app-data\removed-mods\20261003-142751-337\FantasyRacesOfExiles.pak; source and archived hashes match. Extraction cache retained, inactive.

Restored live DB hash: 1FD6089F9A52E74A29FCB907225AD9D491F4B984BFE6562C252DFEA9F8264793; equals pre-D1 and latest verified backup. WAL=0 bytes; paired SHM=32768 bytes, no WAL frames. Backup quick_check=ok; mailbox/controller/AR controller=1/1/1. Modlist hash equals pre-D1: 4D48BF240CA224591BA9050C05D4CDFB3873E2F33A8C2E58C124FD40D0CB2D86.

D1 log preserved at E:\github\gameee\artifacts\batch-d-20261003\D1-ConanSandbox.log (SHA-256 AC751A4EA3782488809AEA012EF8608036AAAA58B417938E1DFFD75ED601522B). Structured evidence remains E:\CSC-M3-Live\live-test\m3-live-log.jsonl.

No verified backups deleted. Client unchanged. Production world NOT CREATED. Pre-existing untracked AGENTS.md, codex_prompt.txt and .worktrees/ preserved. No further live tests after STOP B.
