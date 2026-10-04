# Conan Server Control — Current Status

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
- Licensed Steam clients remain the requirement for testers.

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
- Per the project rules, the client is not modified, and no help is given to restore or repair authentication workarounds. Testers need a licensed Steam client; Steam's "Verify integrity of game files" repairs a licensed install.

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
2. *Client authentication.* The observed local client on the host machine is still **blocked by client authentication** (4F; Steam-emulation artifacts, untouched). Testers need their own licensed Steam client.
3. *Harness wiring.* `--observe` is wired only to the plain `boot` command, not `mod-boot`. RCON was therefore confirmed with a separate localhost probe; a later harness fix can wire it.

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
