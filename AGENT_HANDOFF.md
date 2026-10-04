# Batch D handoff — FINAL 10-MOD CAMPAIGN PACK SERVER-SIDE = PASS

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

- CURRENT LOCAL TIME: 2026-10-05 04:2x +07:00.
- CURRENT BRANCH: claude/m3-task4-live-windows (primary checkout of the repo).
- CURRENT HEAD: the migration commit after 28e9f62 (docs: server Mods incident).
- PRESERVED SAFETY BRANCH: codex/m3-batch-d-safety at abe3875 (original checkpoint 0bb2b2a). Its worktree is E:\github\gameee\.worktrees\batch-d.
- CURRENT STAGE: MIGRATED TO CONAN EXILES ENHANCED 2.2.3 (CL-378132, release-beta). 13-MOD STAGING PACK SERVER-SIDE = PASS on 2.2.3 (smoke, 660 s full validation, clean restart). Conan Server Control points at the new 2.2.3 server; acceptance PASS. 2.2.3 client bundle READY. Old 2.2.2 environment PRESERVED FOR ROLLBACK (renamed folder, untouched). Custom Main Questline PAUSED. Production world NOT CREATED.
- LAST COMPLETED CHECKPOINT: v223-final13-restart PASS (readiness 41.6 s, hold 660.9 s, NORMAL 166.0 s, 0 unknown, LoadErrors 61 exact, C88 signature exact) + app acceptance cycle/diagnostics/backup PASS.
- CURRENT ACTIVE MODLIST (accepted 13, on 2.2.3): StackMe10K.pak -> SavageParagon.pak -> GritandGrease.pak -> ThrallReputation.pak -> ImprovedThrallsAndQoL.pak -> WO_RidingThralls.pak -> Ancient_Realms.pak -> Cannibal_Captivity.pak -> NightTerrors.pak -> PvEPlusAmbush.pak -> PlayerDBNO.pak -> Simple_Minimap.pak -> ChestLabels.pak
- LATEST VERIFIED BACKUP: 2026-10-05_041457 (final 2.2.3, after the acceptance cycle). 2.2.3 chain: 040847 (restart), 035257 (full), 033640 (smoke). Migration source (last 2.2.2 world): 2026-10-05_032214. 2.2.2 validated: 2026-10-05_014347.
- SERVER STATE: OFFLINE (2.2.3 server; no Conan or harness process). The 2.2.2 server is never to be started from the renamed folder except for an explicit rollback.
- LAST SHUTDOWN CLASS: NORMAL (acceptance cycle final stop, exit 0; v223-final13-restart NORMAL 166.0 s).
- ACCEPTED WARNING GATES (ModBootGates): ITQoL mailbox x1; 7 Ancient Realms dangling refs; Ancient Realms MergeDataTables-null (exact, AR hash, max 2); Cannibal teardown set CANNIBAL-CAPTIVITY-TEARDOWN-NO-WORLD (exact hash, exact message and path family, after main-world teardown only, exactly 99, all-or-nothing); base-game noise kinds incl. the two healthy spawn-table ids only. Nothing for Night Terrors or PvE Plus Ambush was needed (no new LoadErrors or errors). Fantasy Races, Shemite and Thrall Wars errors are NOT whitelisted.
- ANALYSIS: every mod-boot writes an immutable snapshot (E:\CSC-M3-Live\live-test\batch-snapshots\<batch>, read-only, log hash-verified); pre-batch plans are pre-batch.json there too. Replay with `analyze-snapshot <dir> [backupId] [--current-catalog]`. Snapshots: cannibal-run-1/2, core8-final-*, shemite-run-1(-pre), p1-nightterrors-*, p2-pveambush-*, p3-thrallwars-*. Helper scripts are in the session scratchpad (phase_prep.ps1, phase_boot.ps1, phase_post.ps1, light_sampler.ps1).
- OPERATOR DECISION 2026-10-04: Thrall Wars Dungeon EXCLUDED / DEFERRED (do not whitelist the 73 LoadErrors, 3 LogMaterial or 4 LogModController merge errors; the loot-table row-structure mismatches are NOT accepted as harmless; keep all evidence, snapshots and the failed-world backup 2026-10-04_031850). Accepted pack = the 10 mods; Fantasy Races, Shemite and Thrall Wars excluded.
- (superseded) ROOM FOR ONE MORE earlier blocked on the source package (now supplied and tested; see the #11 checkpoint above). A read-only search (1,195 candidate .pak files on the profile, D:, E:, F:) found no package with that embedded Workshop ID; the WorkshopDL tool on F: holds none; nothing was downloaded or guessed. Needed: the .pak from the operator (or permission to download). When it arrives: prove it by embedded modinfo (id 3811298984, Enhanced, WindowsServer content, size, SHA-256, dependencies), then run the same fail-closed cycle (phase_prep.ps1 with BaseSnap final10-restart and BaseBackup 2026-10-04_035205, phase_boot.ps1, phase_post.ps1), keeping WO_RidingThralls installed, and check specifically for new mount/rider/passenger/attachment/seat/controller lines against the accepted 10-mod boot.
- NEXT ACTION: operator review. Multiplayer testing on 2.2.3 needs testers with licensed Steam clients (bundle ConanClientModBundle-20261005-041519). Optional: apply the RCON TCP 25575 block rule; fix the Local-mod duplicate-diagnostic false positive (separate task). Do not start the Main Questline or create the production world.
- OPERATOR DECISION 2026-10-04: #11 WO - Room For One More EXCLUDED / DEFERRED (do not whitelist its 5 merge errors; do not revisit in this phase; keep evidence and failed-world backup 2026-10-04_050719). #12 Simple Minimap, #13 Player DBNO and #14 Chest Labels continue independently, cumulatively on the 10-mod baseline (expected 13 mods), then a final 13-mod validation with a clean restart; stop before the custom quest mod and report first.
- OPEN BLOCKERS: none server-side. The new local client folder contains Steam-emulator configuration and was not prepared (licensed clients required). RCON reachable from LAN/Radmin through the Windows-created Private allow rule (not public).

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
