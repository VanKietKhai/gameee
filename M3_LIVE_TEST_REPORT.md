# M3 Task 4 — Live Test Report

## #12 Simple Minimap (Xevyr, Workshop 3719513784) — 2026-10-04 05:27-05:44 +07:00: FAIL / STOP, rolled back (unvalidated LoadError and spawn-table error)

**Simple Minimap loaded and ran stably but its boot logged 1 new LoadError and 1 new spawn-table error with no validated baseline. Per the operator rule nothing was whitelisted: evidence preserved, rolled back, STOP. #13 Player DBNO and #14 Chest Labels were NOT run. The accepted pack is still the 10 mods. Gameplay NOT YET VERIFIED; production world NOT CREATED.**

- **Source:** `C:\Users\vkkha\Downloads\mod conan\Simple_Minimap.pak`, 4,835,375 B, SHA-256 `04F31A75559665C1A949D7A9A632F9A777CE7D79E926032CFC9FD101B1626AC9`; embedded "Simple Minimap (by Xevyr) v5.2.1", `mainClient` 3719513784, Enhanced, devkit 1002, WindowsServer payload; Workshop size identical; "Compatible with everything", no dependency.
- **Preparation:** `501100d` clean; 10-mod state exact (order, all 10 hashes, no extra files); live DB = verified backup `2026-10-04_035205`; `quick_check` ok; singletons 1/1/1; host quiet (CPU 16%). Verified pre-#12 backup **`2026-10-04_052716`**; immutable plan `p12-minimap-pre`; import PASS (source unchanged, installed = source, 11 entries with it last, earlier order intact, world unchanged).
- **Boot:** true readiness 34.2 s; hold 660.4 s; runtime order exact (container Order 1000-1010); 177 packages; no duplicate mounts; controller `SM_BP_ModController_C` registered, one object in the world (+1 `actor_position`, +1 `mod_controllers`), no duplicates. RAM private peak 9.18 GB, working set peak 8.08 GB, min free RAM 361 MB; host CPU mean 26%. Shutdown **NORMAL 172.4 s** (stop 174.5 s; acknowledged, exit 0, no forced kill, no orphan); `quick_check` ok; ITQoL mailbox 1, ITQoL controller 1, AR controller 1.
- **NEW LOADERROR (1, no validated set):** one "package None" reference to missing id `C88E5FE76A79516D` (LoadErrors 61 vs 60). That id exists only in Simple Minimap's own container, so it is attributable to the mod (a dangling reference of the same category as the ITQoL and AR sets). ITQoL 23 and AR 37 stayed exact.
- **NEW SPAWN-TABLE ERROR (1):** `SpawnTable: Error: Data: ... could not find weighted table with id: Exile_Priest_4_Hyrkanian`, logged once at world init (frame 0, 22:28:21.905). The id appears in none of the other 25 logs (no earlier boot, accepted or failed); only the two accepted ids (`WarTestLongLeash`, `Wildlife_Siptah_Firstman_Warrior4`) are baseline noise. Nothing in the evidence ties it to a minimap (no warning/error line names the mod), so its cause is **unattributed**; it is new and therefore unknown.
- **Other differences against the accepted boot:** two wildlife AI-LOD3 lines of an already-known kind (`Rabbit_White`, `Fawn`); `LogHttp` shutdown sleeps 5 vs 2; Spawn 66 vs 65, NPC 55 vs 52 (those +3 are the three lines above); ModController, DataTable, Save, Persistence, streaming, crash and assertion counts identical; 0 warning/error lines name the mod.
- **Failed-batch world:** verified backup **`2026-10-04_054302`**; snapshot `p12-minimap-1` (boot log SHA-256 `EC1572B52F1DDA8488F1486C708592C5AE3F60DDC5DAC621FD7A8A07ABAFD116`); evidence `artifacts/batch-d-20261003/M12-SimpleMinimap-run1-*` (gitignored).
- **Rollback (verified):** restore of `2026-10-04_052716`, then production removal of `Simple_Minimap.pak` (retired to `E:\CSC-M3-Live\app-data\removed-mods\20261004-054408-817\`, hash equals the source). Modlist = the accepted 10-mod order, all 10 pak hashes exact, live DB identical to `2026-10-04_035205`, WAL empty, integrity gates pass, no Conan process. Safety backups: pre-restore `2026-10-04_054359`, pre-removal `2026-10-04_054408`.
- **Classification: FAIL / STOP (unvalidated LoadError and spawn-table id).** Decisions needed: baseline the one Simple Minimap LoadError and judge the spawn-table id (a control boot of the unchanged 10-mod baseline would show whether `Exile_Priest_4_Hyrkanian` is boot variance rather than the mod; not run, not authorized), exclude/defer Simple Minimap, and whether #13 and #14 may be tested on the 10-mod baseline without #12 (the stated rule stops the series).

## Operator decision — 2026-10-04: #11 Room For One More EXCLUDED / DEFERRED; #12-#14 continue independently

Do not whitelist the 5 `MergeDataTables - ToBeAddedDataTable is null` errors. Reasons recorded by the operator: five new unvalidated merge errors immediately after its controller registered; its controller blueprint name overlaps with WO Riding Thralls'; the mod warns about compatibility risk with mount/passenger mods; gameplay compatibility cannot currently be verified. #11 is not revisited in this phase; evidence (`artifacts/batch-d-20261003/M11-RoomForOneMore-*`, snapshot `p11-roomforone-1`, failed-world backup `2026-10-04_050719`) is kept. #12 Simple Minimap, #13 Player DBNO and #14 Chest Labels are tested one at a time on top of the accepted 10-mod baseline (13 mods if all pass), followed by a final 13-mod validation with a clean restart. The historical #11 record below is unchanged.

## Mods #11-#14 series — 2026-10-04 04:4x-05:08 +07:00: sources verified; #11 Room For One More FAIL / STOP, rolled back; #12-#14 NOT RUN

**Installed and accepted state is still the 10-mod pack. Per the operator rule each mod must pass completely before the next, so Simple Minimap, Player DBNO and Chest Labels were not imported. Gameplay NOT YET VERIFIED; production world NOT CREATED.**

**Step 1 — source verification (read-only, from the packages; Conan Exiles Enhanced 2.2.2, CL-377096):**

| # | File | Size (B) | SHA-256 | Embedded identity | Workshop (public metadata, read-only) |
|---|---|---|---|---|---|
| 11 | `WO_RoomForOneMore.pak` | 1,378,335 | `FA6086538C5FC6D42737DC001336918D9AA79AFF01412E89F0567BFF2C1A52FA` | name "[Enhanced] WO - Room For One More", author Sunie, v1.0.0, `minimumVersion` Enhanced, devkit 1002, folder `WO_RoomForOneMore`; **no Workshop ID embedded** (`steamWorkshopFileIds` empty); `-WindowsServer` pak/utoc/ucas present; UE pak footer valid | title identical, size 1,378,335 identical, tag Enhanced, updated 2026-10-01 |
| 12 | `Simple_Minimap.pak` | 4,835,375 | `04F31A75559665C1A949D7A9A632F9A777CE7D79E926032CFC9FD101B1626AC9` | "Simple Minimap (by Xevyr) v5.2.1", `mainClient` **3719513784**, Enhanced, devkit 1002, WindowsServer present | size identical; "Compatible with everything"; no dependency |
| 13 | `PlayerDBNO.pak` | 2,518,846 | `3E7FEEEDA8093E20211F776BC79472DC78AE338344723D55A91667BDE4F9FD65` | "Player DBNO System v1.1.1 (by Xevyr)", `mainClient` **3718882569**, Enhanced, devkit 1002, WindowsServer present | size identical; "Compatible with everything"; no dependency |
| 14 | `ChestLabels.pak` | 1,192,363 | `C79C7E00E8B44F7A6F1250D58BF8655BA9FFBDA16FDB7A1884BBD782186D0CD8` | "Chest Labels" v5.1.2, `mainClient` **3735258746**, Enhanced, devkit 1002, WindowsServer present | size identical; "Compatible with everything"; no dependency |

For #11 the numeric Workshop ID is **not provable from the package itself**: it was accepted on the exact embedded name plus a byte-exact size match against the public Workshop item (independent of the file), and this is flagged for the operator. #11's own page warns that mods altering mounts, passenger systems or attachment behavior "may conflict" and that Riding Thralls compatibility "is currently being tested". #12-#14 declare no dependency or conflict.

**#11 test (batch `p11-roomforone-1`, plan `p11-roomforone-pre`, verified pre-batch backup `2026-10-04_045203`):** preparation passed (`a2973a2` clean; 10-mod state exact; live DB = backup `2026-10-04_035205`; `quick_check` ok; singletons 1/1/1; host quiet). Import PASS (source unchanged, installed = source, 11 entries with it last, earlier order intact, world unchanged). Boot: true readiness 34.2 s; hold 660.3 s; runtime order exact (container Order 1000-1010); 9 packages; no duplicate mounts; no LoadErrors (60 vs 60); no warning/error line names Room For One More or Riding Thralls; RAM private peak 9.19 GB; host CPU mean 18%. Shutdown NORMAL 154.6 s (acknowledged, exit 0, no forced kill, no orphan); `quick_check` ok; ITQoL mailbox 1, ITQoL controller 1, AR controller 1.

**Why it failed:**
- **5 new data-table merge errors.** `LogModController: Error: AModController::MergeDataTables - ToBeAddedDataTable is null` now appears 7 times (accepted Ancient Realms baseline: 2, hash-bound and capped at 2, so the cap left these 5 as unknown). All 5 are logged within 1-2 ms after the new mod's controller registers (log lines 6886-6890, right after 6885). Counts: ModController 7 vs 2, DataTable 117 vs 112, warnings/errors before shutdown 248 vs 243. Nothing else differs.
- **Controller name collision (observed, not yet harmful):** its controller is `/Game/Mods/WO_RoomForOneMore/WO_BP_RT_ModController.WO_BP_RT_ModController_C`, the same blueprint name as Riding Thralls' `/Game/Mods/WO_RidingThralls/WO_BP_RT_ModController.WO_BP_RT_ModController_C` (different package paths). Persistence logged one `Loading mod controller: WO_BP_RT_ModController_C` and one `Spawning mod controller: WO_BP_RT_ModController_C`; the world now holds one controller of each path, no duplicates. Behavior across a restart was not tested because the run failed.
- Failed-batch world: verified backup **`2026-10-04_050719`**; snapshot `p11-roomforone-1` (boot log SHA-256 `7CCAFEB6092987C03CB9E3C382B3DC83522B19BBF34F39C8AC4F9F75A00D8F57`); evidence `artifacts/batch-d-20261003/M11-RoomForOneMore-*` (gitignored).

**Rollback (verified):** restore of `2026-10-04_045203`, then production removal of `WO_RoomForOneMore.pak` (retired to `E:\CSC-M3-Live\app-data\removed-mods\20261004-050758-071\`, hash equals the source). Modlist = the accepted 10-mod order, all 10 pak hashes and sizes exact, live DB identical to the final 10-mod backup `2026-10-04_035205`, WAL empty, integrity gates pass, no Conan process. Safety backups: pre-restore `2026-10-04_050749`, pre-removal `2026-10-04_050757`.

**Classification: FAIL / STOP (unvalidated data-table merge errors).** Not whitelisted. Decisions needed: whether to baseline these 5 lines as Room For One More's (and judge what a null merge table means for its feature), exclude/defer it, or ask the author about the controller-name overlap with Riding Thralls; and whether to run #12-#14 independently of #11 (the stated rule stops the series).

## Final 10-mod campaign pack validation — 2026-10-04 03:2x-03:53 +07:00: PASS. FINAL 10-MOD CAMPAIGN PACK SERVER-SIDE = PASS

**Both parts passed: (A) the full run and (B) the clean restart. Gameplay NOT YET VERIFIED (4F blocked by client authentication; none of the nine non-base mods' behavior has been tested with a client). Production world NOT CREATED.**

- **Accepted pack (10):** StackMe10K, Savage Paragon, Grit & Grease, Thrall Reputation, Improved Thralls & QoL, WO Riding Thralls, Ancient Realms Enhanced, Cannibal Captivity Enhanced, Night Terrors, PvE Plus Ambush. Excluded / deferred: Fantasy Races, Shemite City State, Thrall Wars Dungeon.
- **Before:** `79c666b` clean; server Offline, no Conan process; host CPU over 90 s 28, 26, 18, 24, 23, 21, 18, 21, 20 (mean 22%), game closed; modlist exactly the accepted 10-mod order; all 10 pak hashes and sizes exact against the immutable record, no extra files; live DB = verified backup `2026-10-04_031929`; `quick_check` ok; singletons 1/1/1. Verified pre-final backup **`2026-10-04_032619`**; immutable plan `final10-pre` (no new mod; catalog SHA-256 `767B4BCD…7818`, code head `79c666b`).

| | A: full run (`final10-1`) | B: clean restart (`final10-restart`) |
|---|---|---|
| True readiness (world ticking) | 36.4 s | 40.5 s |
| Online hold | 660.7 s | 301.5 s |
| Runtime load order (container Order) | modlist order, 1000-1009 | modlist order, 1000-1009 |
| Complete boot/runtime/teardown analysis | PASS, 0 unknown | PASS, 0 unknown |
| ITQoL / AR LoadErrors | 23 / 37 exact | 23 / 37 exact |
| Mailbox x1; AR merge x2 | exact | exact |
| Cannibal teardown warnings | 99 of exactly 99, after teardown | 99 of exactly 99, after teardown |
| Spawn-table noise | only the two accepted ids (x2) | same |
| New LoadErrors (total vs accepted boot) | none (60 vs 60) | none (60 vs 60) |
| Warning/error kinds new vs the accepted boot | 0 | 0 |
| Lines naming Night Terrors / PvE Plus Ambush at warning/error severity | 0 / 0 | 0 / 0 |
| NPC / Spawn / Stat / DataTable counts | 53 / 65 / 2 / 112 (identical) | 52 / 65 / 2 / 112 (same, -1 wildlife AI-LOD line) |
| Crash / assertion / save / persistence error | none | none |
| Shutdown (gate) | 174.1 s NORMAL (stop 176.1 s) | 191.4 s NORMAL (stop 193.4 s) |
| Stop details | acknowledged, exit 0, no forced kill, no root/shipping/orphan | same |
| Host CPU during the stop | mean 38% (28-48%) | mean 38% (20-59%) |
| Host CPU whole run | mean 33% (18-51%) | mean 45% (19-100%; one transient spike, still NORMAL) |
| Silent teardown phase | 154.9 s | 164.8 s |
| RAM | private peak 9.12 GB, working set peak 7.23 GB, min free RAM 445 MB | private peak 9.17 GB, working set peak 7.34 GB, min free RAM 485 MB |
| `quick_check` / mailbox / ITQoL ctrl / AR ctrl | ok / 1 / 1 / 1 | ok / 1 / 1 / 1 |
| Controllers (Night Terrors 1; PvE Plus Ambush 3, one each) | present once each, no duplicates | same |
| World table changes | `game_events` only | `game_events` +24, `properties` 253 -> 252 (the same +-1 row seen between earlier boots; no singleton or controller change) |
| Verified backup after | `2026-10-04_034202` | **`2026-10-04_035205` (FINAL)** |

Final live state: server OFFLINE; `modlist.txt` = the 10 mods in order; all 10 pak hashes and sizes exact; `game_0.db` 712,704 B with no WAL left; DB identical to the final backup. Snapshots `final10-pre`, `final10-1` (boot log SHA-256 `2C3B78312018A7FFC3CD88ED66D91EF3FBE5D28541F7B7556465FD729E88DF4E`) and `final10-restart` (`150AB724E758C140C8B74789AD5AFCC0CACDEE1EA11049F46666A6FD5D8C1BC3`). WAL-safe checks were run on the verified backup copies. **SAFE TO LOCK MODPACK (server-side) = YES. SAFE TO CREATE PRODUCTION WORLD = NO (needs explicit approval).**

## Operator decision — 2026-10-04: Thrall Wars Dungeon EXCLUDED / DEFERRED

Do not whitelist the 73 LoadErrors, the 3 `LogMaterial` errors or the 4 `LogModController` merge errors; in particular do not accept the loot-table row-structure mismatches as a known harmless baseline. All Thrall Wars evidence (snapshots `p3-thrallwars-pre` and `p3-thrallwars-1`, boot log SHA-256 `B50A313B…5161`, failed-world backup `2026-10-04_031850`, retired pak archive, `artifacts/batch-d-20261003/P3-ThrallWars-*`) is kept for possible future investigation. Accepted pack: the 10 mods through PvE Plus Ambush; Fantasy Races, Shemite and Thrall Wars are excluded. The historical P3 record below is unchanged.

## Boss/PvE phase P3 — Thrall Wars Dungeon (11th mod, `SlaveWarsServer.pak`) — 2026-10-04 03:02-03:20 +07:00: FAIL / STOP, rolled back (new unvalidated errors)

**The server loaded the mod and ran stably, but the boot produced 73 new LoadErrors and 7 new errors with no validated baseline. Per the operator rule nothing was whitelisted: evidence preserved, P3 rolled back, STOP. The final 11-mod validation was NOT run. Installed and accepted state: the 10 mods through PvE Plus Ambush.**

- **Source (proven from the package):** `C:\Users\vkkha\Downloads\mod conan\SlaveWarsServer.pak`, 466,505,066 B, SHA-256 `A6238D37486FB25FCDD3E961D4B97256505C4B63D24016D7E5CC1A0960AD623B`. Embedded `modinfo.json`: "Thrall Wars Dungeon Mod" v27.1.0, author Torkatla, `mainClient` 3722829382, Enhanced, devkit revision 1002, no HTTP origins, no startup-load flag, no dependency field; `-WindowsServer` content present. The description lists no required mod; it names a separate "Thrall Wars Deco Mod" (3720921036) only as "check also". Item ID ranges reserved: 298454001-298455000 and 189221-189500.
- **Preparation (scripted, fail-closed):** `e077b73` clean; server Offline; host quiet (CPU 32%, game closed); the 10-mod state exact (order, all 10 hashes and sizes, no extra files); live DB = verified backup `2026-10-04_030121`; `quick_check` ok; singletons 1/1/1. Verified pre-P3 backup **`2026-10-04_030240`**; immutable plan `p3-thrallwars-pre`; import PASS (source unchanged, installed = source, 11 entries with it last, earlier order intact, world unchanged).
- **Boot:** true readiness 37.5 s (no slower than the 10-mod boots); hold 660.6 s; mount order = modlist order, `Order` 1010; 3,091 packages; no duplicate mounts. RAM: private peak 9.17 GB (same as 10 mods), working set peak 7.80 GB, min free RAM 823 MB; host CPU mean 33%. Controller `bpModController` registered once.
- **NEW LOADERRORS: 73 (133 vs 60), no validated set.** 24 distinct missing package ids (top: `F8E70644D75330D3` x17, `BF81D3DE873B1D6B` x8, `A542B9C2241C6240` x5, `E971757AD2CCE316` x4, `ACE4F3820CD648F2` x4). By area: 31 "package None" attributed to the mod's container; `Devices` 16; `Blueprints` 15; `Bosses` 5; `ExternalComponents` 4; `AtlantianQueen` 1; `BuildingBlocks` 1. Dangling references of the same category as the ITQoL and AR sets, but new and unvalidated. ITQoL 23 and AR 37 stayed exact.
- **NEW ERRORS (7), all named or caused by the mod's content:**
  - 4 `LogModController: Error: AModController::MergeDataTables`: 3 x "datatables to be merged do not have the same row structure! MergeInto: LootTableRow vs ToBeAdded: LootTableWeightedRow" and 1 x "MergeIntoDataTable is null". Loot-table merges that failed may affect loot behavior.
  - 3 `LogMaterial: Error: MaterialInstance [MI_TW_lasso | MI_sw_tier3_derketo_statue_table | MI_whip] clearing bHasStaticPermutationResource to avoid shadermap/blendmode mismatch`.
  - Counts versus the P2 boot: ModController 6 vs 2, DataTable 116 vs 112, Spawn 63 vs 65, NPC 54 vs 53, warnings/errors before shutdown 252 vs 244; no crash, assertion, persistence, save or world-partition lines; no new spawn-table ids. The "Stat 5 vs 2" keyword delta is the three material lines ("StaticPermutation").
- **Shutdown: NORMAL 176.0 s** (stop 178.0 s; acknowledged, exit 0, no forced kill, no root/shipping/orphan); host CPU during the stop 36%; silent phase 155.9 s. The mod adds no measurable shutdown time.
- **World:** `quick_check` ok; ITQoL mailbox 1, ITQoL controller 1, AR controller 1; exactly one new object (`bpModController`: +1 `actor_position`, +1 `mod_controllers`), no duplicates. Verified post-P3 backup **`2026-10-04_031850`** (the failed-P3 world), snapshot `p3-thrallwars-1` (boot log SHA-256 `B50A313B5920E4B1D5F187CA96369C9F4D9ED60872095239BC18F15C28F55161`); evidence in `artifacts/batch-d-20261003/P3-ThrallWars-*` (gitignored).
- **Rollback (verified):** restore of `2026-10-04_030240`, then production removal of `SlaveWarsServer.pak` (retired to `E:\CSC-M3-Live\app-data\removed-mods\20261004-031932-699\`, hash equals the source). The modlist equals the accepted 10-mod order, all 10 pak hashes and sizes exact, live DB identical to the post-P2 backup `2026-10-04_030121`, WAL empty, no Conan process. Safety backups: pre-restore `2026-10-04_031916` (the P3 world), pre-removal `2026-10-04_031929` (latest verified 10-mod backup).
- **Classification: FAIL / STOP (unvalidated errors).** In-game behavior NOT verified. Decisions needed: validate the 73 LoadErrors and 7 errors as a Thrall Wars baseline (and judge the failed loot-table merges), or exclude/defer the mod.

## Boss/PvE phase P2 — PvE Plus Ambush (10th mod) — 2026-10-04 02:45-03:02 +07:00: PASS server-side

**PvE Plus Ambush is installed on top of Night Terrors (load order 10). Server-side PASS does NOT prove ambush gameplay or how the two ambush systems behave together in play: gameplay NOT YET VERIFIED.**

- **Source:** `C:\Users\vkkha\Downloads\mod conan\PvEPlusAmbush.pak`, 5,145,777 B, SHA-256 `C9C816FAE72C07626D4F0AD1994347CBDE110FBF6CB295AADD804540E4EC5B74` (embedded `modinfo.json`: "PvE Plus Ambush (Enhanced) - v1.0.5", `mainClient` 3721274811, Enhanced, no dependency field; `-WindowsServer` content present; description states the same mod id).
- **Preparation (scripted, fail-closed):** `c30626d` clean; server Offline, no Conan process; host quiet (CPU 30%, game closed); 9-mod state exact (modlist order, all 9 pak hashes and sizes, no extra files); live DB = verified backup `2026-10-04_024312`; `quick_check` ok; singletons 1/1/1. Verified pre-P2 backup **`2026-10-04_024506`**; immutable plan `p2-pveambush-pre`; import PASS (source unchanged, installed = source, 10 entries with the new mod last, earlier order intact, world unchanged).
- **Boot:** true readiness 37.5 s; hold 660.5 s; mount order = modlist order, `Order` 1009; 88 packages; no duplicates. RAM: private peak 9.17 GB, working set peak 6.46 GB, min free RAM 742 MB; host CPU mean 37%.
- **Complete-log analysis (snapshot `p2-pveambush-1`, log SHA-256 `3F6C3EFFD4E0CC0BEF738DA294EAEB1C0518A19771D44E024068E6D9CBCCEBE9`): PASS, 0 unknown.** ITQoL 23 and AR 37 exact; mailbox x1; AR merge x2; Cannibal teardown 99 of exactly 99; spawn-table noise only the two accepted ids; 0 LoadErrors problems.
- **Independent diff against the accepted P1 boot (not the scanner):** 0 warning/error lines name PvE Plus Ambush; LoadErrors 60 vs 60; Spawn 65 vs 65, DataTable 112 vs 112, Stat 2 vs 2, Save 6 vs 6, Persistence, ModController and streaming identical, NPC 53 vs 52 (that +1 is the wildlife AI-LOD line below), zero crash, assertion or world-partition lines; warnings/errors before shutdown 244 vs 243 and in teardown 1,543 vs 1,544 (no runaway repeats). The only new kind was one `Komodo_Baby` AI-LOD3 line (an already-known base-game kind). **No conflict or error between the two ambush systems; no new NPC/stat/spawn-table errors.**
- **Controllers:** three registered (`MDC_AmbushWidget`, `NAS_MC_ModController`, `BP_WeightedTableMerge_MC`); the world holds exactly one of each (+3 `actor_position`, +3 `mod_controllers`, +1 `properties`), no duplicates; `quick_check` ok; ITQoL mailbox 1, ITQoL controller 1, AR controller 1.
- **Shutdown: NORMAL 177.0 s** (stop 179.0 s; acknowledged, exit 0, no forced kill, no root/shipping/orphan); host CPU during the stop 39% (24-51%); silent phase 155.6 s. Verified post-P2 backup **`2026-10-04_030121`**.

## Boss/PvE phase P1 — Night Terrors (9th mod) — 2026-10-04 02:2x-02:43 +07:00: PASS server-side

**Night Terrors is installed (load order 9). Server-side PASS does NOT prove its gameplay (night encounters need players): gameplay NOT YET VERIFIED.**

- **Source (proven from the package, not the filename):** `C:\Users\vkkha\Downloads\mod conan\NightTerrors.pak`, 10,219,405 B, SHA-256 `2FE3E7AD7160ABA04DD9EAB99225BBD45D937E4B69525271F119348A7BCFBE61`. Embedded `modinfo.json`: "Night Terrors" v1.3.7 ("Recook for UE 5.8.2"), `mainClient` 3723538551, `minimumVersion` Enhanced, no dependency field; `-WindowsServer` pak/utoc/ucas present; UE pak footer valid. The other two targets were located the same way (`PvEPlusAmbush.pak` 3721274811 v1.0.5; Thrall Wars Dungeon is **`SlaveWarsServer.pak`**, 3722829382, v27.1.0, 466,505,066 B, SHA-256 `A6238D37486FB25FCDD3E961D4B97256505C4B63D24016D7E5CC1A0960AD623B`; PvE Plus Ambush SHA-256 `C9C816FAE72C07626D4F0AD1994347CBDE110FBF6CB295AADD804540E4EC5B74`). Thrall Wars Dungeon's description lists no required mods (its "dependencies" mention is an item-lookup feature) and no plain `Tot_CommonLibrary` reference was found.
- **Before:** `ec5b870` clean; server Offline, no Conan process; 8-mod core exact (modlist order, all 8 pak hashes and sizes against the immutable record, no extra files); live DB = final core backup `2026-10-04_020607`; `quick_check` ok; singletons 1/1/1; host quiet (CPU 28%, game closed). Verified pre-P1 backup **`2026-10-04_022500`**; immutable plan `p1-nightterrors-pre`.
- **Import (production Local pipeline): PASS.** Source hash unchanged, installed = source, `modlist.txt` exactly 9 with Night Terrors last, world unchanged.
- **Boot:** true readiness 36.4 s; hold 660.6 s. Mount order = modlist order; `Order` 1008; 166 packages; no duplicates. Controller `BP_NightTerrors_ModController_C` registered and spawned once. RAM: private peak 9.16 GB, working set peak 7.43 GB, min free RAM 660 MB; host CPU mean 33%.
- **Complete-log analysis (snapshot `p1-nightterrors-1`, log SHA-256 `81F8F61FE358125A06370720DEF7F1AD5E689B8C840C58F8C74DA1735C0C80A3`): PASS, 0 unknown, 0 LoadErrors problems.** ITQoL 23 and AR 37 exact; mailbox x1; AR merge x2; Cannibal teardown 99 of exactly 99; spawn-table noise only the two accepted ids.
- **A scanner defect surfaced, was not a mod problem, and was fixed (`af0b25e`):** the live scan reported 16 unknown lines, all ordinary mount/registration lines naming the mod ("NightTerrors" contains "error", and the problem-word test was a substring match). They were Display-level lines, not errors. The fix masks mod stems only in that word test; Error/Fatal severity, severe markers and real problem words are unchanged (17 tests: the 10 exact lines are not problems; 7 real problems naming the same mod are still selected). The live harness result for this run therefore reads FAIL (exit 1) because the fix came after the run; the recorded boot was re-judged from its immutable snapshot. All earlier accepted boots still pass and the Shemite boot still fails on its real errors.
- **Independent diff against the accepted 8-mod boot (not the scanner):** 0 warning/error lines name Night Terrors; LoadErrors 60 vs 60 (none extra, none missing); Spawn 65 vs 65, DataTable 112 vs 112, NPC 52 vs 53, Stat, Save, Persistence, ModController, streaming counts identical; zero crash, assertion or world-partition lines; warnings/errors before shutdown 243 vs 244; the only new kinds were one wildlife AI-LOD line of an already-known kind and two `LogHttp` stats-upload warnings during shutdown.
- **Shutdown: NORMAL 177.3 s** (stop 179.3 s; RCON acknowledged, exit 0, no forced kill, no root/shipping/orphan); host CPU during the stop 37% (34-43%); silent phase 156.3 s.
- **World:** `quick_check` ok; ITQoL mailbox 1, ITQoL controller 1, AR controller 1; exactly one new object, the Night Terrors controller (+1 `actor_position`, +1 `mod_controllers`); no duplicates; `game_events` +24. Verified post-P1 backup **`2026-10-04_024312`**.

## Final full 8-mod core validation, quiet-host retry — 2026-10-04 01:3x-02:07 +07:00: PASS. CORE MODPACK V1 SERVER-SIDE = PASS

**Both parts passed: (A) the full validation and (B) the clean restart validation. Gameplay NOT YET VERIFIED (4F blocked by client authentication). Production world NOT CREATED. Shemite and Fantasy Races remain EXCLUDED / DEFERRED.**

- **Quiet-host precondition:** League of Legends closed (only a Riot launcher background service left); server Offline, no Conan process; 150 s of host CPU sampling: mean 27% (min 10%, max 60% from a transient caused by my own commands), 16-21% in the minutes before boot. The old orphan `findstr` pipeline (PID 28280, about 1.4 cores) was still running and was not touched; it was also present during the earlier quiet 181.7 s and 199.2 s stops. Other unknown/user processes were not terminated.
- **State before:** `16deaed` clean; modlist exactly the 8 accepted mods in order; all 8 pak hashes and sizes equal the immutable record, no extra Mods files; live DB `D2A0356D…7A73` identical to verified backup `2026-10-04_012406`; `quick_check` ok; singletons 1/1/1. Verified pre-final backup **`2026-10-04_013946`**; immutable plan `core8-final-retry-pre` (no new mod; catalog SHA-256 `767B4BCD…7818`, the same rule set as attempt 1).

| | A: full validation (`core8-final-2`) | B: clean restart (`core8-final-2-restart`) |
|---|---|---|
| True readiness (world ticking) | 36.4 s | 36.4 s |
| Online hold | 660.5 s | 301.7 s |
| Mount order / container Order | modlist order, 1000-1007 | modlist order, 1000-1007 |
| Complete boot/runtime/teardown analysis | PASS, 0 unknown | PASS, 0 unknown |
| ITQoL / AR LoadErrors | 23 / 37 exact | 23 / 37 exact |
| Mailbox x1; AR merge x2 | exact | exact |
| Cannibal teardown warnings | 99 of exactly 99, after teardown | 99 of exactly 99, after teardown |
| Spawn-table noise | only the two accepted ids (x2) | same |
| Crash / assertion / save / persistence error | none | none |
| Shutdown (gate) | 175.1 s NORMAL (stop 177.2 s) | 178.1 s NORMAL (stop 180.2 s) |
| Stop details | acknowledged, exit 0, no forced kill, no root/shipping/orphan | same |
| Host CPU during the stop | mean 36% (28-50%) | mean 36% (25-55%) |
| Host CPU whole run | mean 34% (17-70%) | mean 38% (24-58%) |
| Silent teardown phase | 154.8 s | 158.2 s |
| `quick_check` / mailbox / ITQoL ctrl / AR ctrl | ok / 1 / 1 / 1 | ok / 1 / 1 / 1 |
| RAM | private peak 9.16 GB, working set peak 7.53 GB, min free RAM 545 MB | private peak 9.17 GB, min free RAM 414 MB |
| Verified backup after | `2026-10-04_015607` | **`2026-10-04_020607` (FINAL)** |

Comparison: attempt 1 (host CPU 79% average from a game) stopped in 307.9 s with a 267.9 s silent phase; on the quiet host the same 8 mods stop in about 175-180 s with a 155-158 s silent phase, consistent with the earlier shutdown study. WAL-safe world checks were run on the verified backup copies (immutable read). Final live state: server OFFLINE, `modlist.txt` = the 8 mods, `game_0.db` 700,416 B with no WAL left, DB identical to the final backup. Snapshots: `core8-final-retry-pre`, `core8-final-2` (log SHA-256 `B1ADE0F035128FF10CCAB2F61A93C98AB8B2936AC87715698B32B06028C23EEE`), `core8-final-2-restart` (`76138DC49B57FFD96FEA4E8697BA753025EC2CC3E002DCAEBF8CEA6288E6FC85`). Evidence (gitignored): `artifacts/batch-d-20261003/CORE8-final2*`. **SAFE TO BEGIN BOSS/PVE MOD PHASE = YES (server-side).**

## Final full 8-mod core validation, attempt 1 — 2026-10-04 01:0x-01:24 +07:00: everything passed except the shutdown time (DEGRADED 307.9 s, host under load) (historical, superseded by the retry above)

**8-MOD CORE VALIDATION: FAIL on the stated criterion (shutdown must be NORMAL or WARNING). CORE MODPACK V1 SERVER-SIDE: NOT YET.** Every other check passed. The slow stop coincided with heavy host CPU load from a game running during the whole attempt, so it is attributed to the host, not to a mod, but that is an inference from the CPU samples and the earlier shutdown study, not a proof.

- **Before boot:** branch `ff9cd3f` clean; server Offline, no Conan process; `modlist.txt` exactly the 8 accepted mods in order; all 8 pak hashes and sizes equal the immutable record (`cannibal-run-2`), no extra files in Mods; live DB `0EE01DD4…042C` identical to backups `2026-10-04_004256`, `_001102`, `_001912`; `quick_check` ok; singletons 1/1/1. Verified pre-final backup **`2026-10-04_010449`**; immutable plan `core8-final-pre` (no new mod; catalog SHA-256 `767B4BCD…7818`, code head `ff9cd3f`).
- **Boot:** true readiness 71.3 s (game port bound, world frame 2; slower than the 35-42 s quiet runs because of host load). Runtime mount order = modlist order; no duplicate mounts.
- **Hold:** 661.7 s online. Log flat (147 warnings, 93 errors, same as the 8-mod boots), private memory plateaued at about 9.1 GB, working set peak 6.07 GB, min free RAM 347 MB, server responsive.
- **Complete boot/runtime/teardown analysis (snapshot `core8-final-1`, log SHA-256 `848971794C065BBFBB4A21A6BDD229BEB1E293ED8A97CD7EED3329059853E329`): PASS, 0 unknown lines.** ITQoL 23 and AR 37 LoadErrors exact; ITQoL mailbox x1; AR merge x2; Cannibal teardown set 99 of exactly 99 after teardown; no other LoadErrors; no crash, assertion, persistence or save error; the tightened spawn-table rule left only the two known healthy ids (x2).
- **Shutdown: 307.9 s = DEGRADED.** RCON acknowledged, exit code 0, no forced kill (graceful window exceeded, kept waiting because shutdown was proven), no root or shipping process, no orphan. For comparison the same 8 mods stopped in 199.2 s and 181.7 s on a quiet host. The log shows the usual single long silent teardown phase: 267.9 s here vs 157.5 s and 176.5 s. Host CPU averaged 79% over the run (52-94%) and 70-93% during the stop (about 25% on the quiet-host stops). That agrees with the earlier shutdown study (the phase is single-threaded base-game work that CPU contention stretches), but the same measurement was not taken during the Shemite run, so this does not by itself explain Shemite's 319.6 s.
- **World after the run:** `quick_check` ok (WAL-safe read of the verified copy); ITQoL mailbox 1, ITQoL controller 1, AR controller 1. Verified post-run backup **`2026-10-04_012406`** (hashes + `quick_check` ok; DB `D2A0356D…7A73`). The 8-mod state stays installed; no rollback was needed.
- **Not done:** the clean restart validation (a second boot) was skipped as unsafe after a DEGRADED stop and with the host still loaded. A retry belongs on a quiet host.
- Evidence (gitignored): `artifacts/batch-d-20261003/CORE8-*`; samples include a host-CPU column. Gameplay NOT verified; production world NOT CREATED.

## Operator decision — 2026-10-04: Shemite City State EXCLUDED / DEFERRED

Do not rerun Shemite; do not whitelist its 9 LoadErrors or its 6 spawn-table errors (`Catacomb_Wretch` x5, `Wildlife_SiptahTwoHornedRhino_Baby` x1). Reason: the run reached readiness and preserved world integrity but introduced those previously unvalidated errors and a DEGRADED 319.6 s shutdown; the spawn-table errors may affect gameplay/spawn behavior and are not sufficiently explained to accept as harmless. All Shemite evidence (snapshots `shemite-run-1-pre` and `shemite-run-1`, backups `2026-10-04_001912`, `_004137`, `_004207`, the retired pak archive, `artifacts/batch-d-20261003/D3-Shemite-*`) is kept for possible future investigation. Accepted server-side core: the 8 mods ending with Cannibal Captivity; Fantasy Races also excluded. The historical Shemite record below is unchanged.

## Shemite City State (9th mod) — 2026-10-04 00:2x-00:43 +07:00: STOP, rolled back (gate-blocked; INCONCLUSIVE)

**Result: the server loaded Shemite and ran stably, but two gates failed (new unvalidated LoadErrors; DEGRADED shutdown 319.6 s). Per the operator rule nothing was whitelisted: evidence preserved, rolled back to the verified PRE-SHEMITE 8-mod backup, STOP. 9-mod core validation NOT RUN.**

- **Source (path never guessed):** `C:\Users\vkkha\Downloads\mod conan\Shemite_City_State.pak`, 2,098,034,644 B, SHA-256 `FD1051425E046D8B3AA2A0C49616CF088F307C98AB4EF602FFB90516CCAA307C`. Valid UE pak (footer magic, version 12). Embedded `modinfo.json`: "Shemite City State: Enhanced - Version 2.1", `minimumVersion` Enhanced, `mainClient` 3755371705, `bRequiresLoadOnStartup` false, no dependency or companion field; Windows, WindowsServer and LinuxServer variants (WindowsServer is used). The Workshop page says the mod targets the base Exiles map only and may conflict with other map mods; none is installed.
- **Before import:** `d1c31ff` clean, server Offline, no Conan process, 8-mod modlist exact, live DB = backup `2026-10-04_001102` (`0EE01DD4…042C`), `quick_check` ok, singletons 1/1/1. Verified PRE-SHEMITE backup **`2026-10-04_001912`**. Immutable pre-batch plan `shemite-run-1-pre` (baseline, expected new mod, expected 9-mod order, catalog SHA-256 `E7D5C88E…4DD5`, code head `4e09aa6`, backup id).
- **Import (production Local pipeline, 72.7 s): PASS.** Source untouched (hash identical before/after), installed = source, `modlist.txt` exactly 9 with Shemite last, live DB unchanged.
- **Boot:** true readiness 41.6 s (game port bound, world frame 2). Runtime mount order = modlist order; container `Order` 1000-1008 (Shemite 1008); Shemite contributes 1,999 packages; no duplicate mounts; no mod-related problems. Controller `BP_Shemite_CS_Modcontroller_C` registered and freshly spawned. Hold 661.2 s online.
- **RAM (15 s samples):** working set peak 6.15 GB (settling to about 4.7-5.2 GB), private peak 9.39 GB (plateaued about 9.2-9.4 GB after about 2 minutes, 8.4 GB at stop), min free RAM 455 MB on a 16 GB host with a browser and other apps open. Server stayed responsive; no growth after the plateau. During the hold the log was flat (warnings 148, errors 99, none naming Shemite); no map-streaming, world-partition, crash, assertion, persistence or save difference from the 8-mod boot (keyword counts identical).
- **NEW LOADERRORS: 9, all Shemite's, no validated set.** 2 by path (`/Game/Mods/Shemite_City_State/BP/BP_Shemite_CS_Wall_Torch_01` -> `5218437D6432F52F`, `/Game/Mods/Shemite_City_State/Shemite_City_State_Level` -> `47B00C5B2A594119`) and 7 "package None" attributed by the missing id in Shemite's extracted container (`1444EFFEDA5DAB84` x2, `4C9BEFB8294DB028`, `5218437D6432F52F`, `5518CBA35A7DF4F9`, `D7A77EB84F1F6064`, `F8E70644D75330D3`). Same category as the ITQoL and AR dangling references, but new, so unknown. ITQoL 23 and AR 37 stayed exact; Cannibal's 99 teardown warnings still accepted 99 of 99.
- **NEW ERRORS (found by diffing against the 8-mod boot, not by the scanner):** 6 `SpawnTable: Error ... could not find weighted table with id` with ids absent from all 14 earlier logs: `Catacomb_Wretch` x5 (during the first minute online) and `Wildlife_SiptahTwoHornedRhino_Baby` x1 (at boot, in the same second as Shemite's LoadErrors). Probable, not proven, Shemite attribution (its content has catacomb-related strings, not these exact names). **Scanner gap:** the base-game noise rule matched any id and hid them; fixed in `c0ef218` (only the two healthy ids, `WarTestLongLeash` and `Wildlife_Siptah_Firstman_Warrior4`, are noise). Everything else matched the 8-mod boot (same kinds; warnings/errors before shutdown 250 vs 246; in teardown 1,542 vs 1,542). **Residual risk:** the other base-game noise kinds still match by kind with a variable part (item name, wildlife name, movie URL); their per-kind counts equalled the 8-mod boot here.
- **NEW WARNINGS naming Shemite:** none beyond the above.
- **Shutdown: 319.6 s = DEGRADED (300-600 s), blocks the next batch.** RCON acknowledged, exit code 0, no forced kill, no orphan, graceful window exceeded but the stop kept waiting because the shutdown was proven. Same shape as earlier stops (command, then one long silent base-game phase, then close) but longer: silent phase 276.6 s here vs 157.5 s (8-mod rerun) and 176.5 s (8-mod run 1); the 8-mod stops were 181.7 s and 199.2 s. Cause **not established**: Shemite content and host memory pressure (free RAM 450-1,100 MB) are both candidates; a quiet-host comparison would be needed to separate them.
- **World:** `quick_check` ok; ITQoL mailbox 1, ITQoL controller 1, AR controller 1; exactly one new Shemite object (the controller: +1 `actor_position`, +1 `mod_controllers` row; no Shemite placeables or buildings, no duplicates); `game_events` +25. WAL empty after stop.
- **Verified POST-SHEMITE backup `2026-10-04_004137`** (hashes + `quick_check` ok). Snapshot `shemite-run-1` (boot log SHA-256 `55FF21122182D65B845ACE79B00274499827E28D3D4A058982EA21647B6DB9EF`). Evidence copied to `artifacts/batch-d-20261003/D3-Shemite-*` (gitignored).
- **Rollback (verified):** restore of `2026-10-04_001912`, then production removal of `Shemite_City_State.pak` (retired to `E:\CSC-M3-Live\app-data\removed-mods\20261004-004313-346\`, hash equals the source). Live DB `0EE01DD4…042C` and the 8-mod `modlist.txt` equal the accepted state. Safety backups: pre-restore `2026-10-04_004207` (the Shemite world), pre-removal `2026-10-04_004256` (latest verified 8-mod backup).
- **Classification: INCONCLUSIVE (gate-blocked).** Not PASS: unknown LoadErrors and spawn-table errors, and a DEGRADED stop. Not shown BLOCKING: no crash, assertion, persistence, integrity, map-load or duplicate-object problem, and the shutdown cause is unproven. Decisions needed: whether to validate the 9 LoadErrors as a Shemite baseline, how to treat the 6 spawn-table errors, and how to review the shutdown (for example a quiet-host rerun). In-game behavior NOT verified.

## Cannibal Captivity controlled rerun — 2026-10-04 00:1x +07:00: ACCEPTED (server-side), KNOWN NON-BLOCKING teardown warning

**Cannibal Captivity is kept as the 8th mod. Classification: KNOWN NON-BLOCKING, server-side teardown warning only. In-game behavior NOT verified.**

Operator decision: keep the mod; the 99 teardown warnings are a candidate warning set pending one controlled reproduction. First, the analysis reproducibility gap was fixed (`c6bcef7`): every live batch now records an immutable snapshot (batch id, ordered modlist, each pak's name/SHA-256/size, the validated catalog in force, and the boot log's hash with a read-only copy) and `analyze-snapshot` replays from it. On real data both preserved runs now FAIL on exactly 99 lines from the recorded snapshot while Cannibal is not installed (the live-catalog analysis had said PASS).

Controlled rerun (`mod-boot --hold 600 --batch cannibal-run-2`): baseline verified first (live DB `1FD6089F…4793` and modlist `4D48BF24…2D86` identical to the seven-mod state, no Conan process); verified pre-batch backup `2026-10-03_235120`; production Local import (source = installed = catalog SHA-256 `DB6E3C299912E48E4DEC8293A58C8DEF1D881FDBDC44F1E67CE99A9BF348F04F`, order 8).

| Item | Run 1 (2026-10-03 23:24) | Rerun (2026-10-04 00:05) |
|---|---|---|
| True readiness | 39.6 s | 35.4 s |
| Online hold | 600.3 s | 600.5 s |
| Shutdown (gate) | 199.2 s NORMAL | 181.7 s NORMAL (stop 183.7 s) |
| Stop | RCON acked, exit 0, no kill, no orphan | same |
| `LogScript` "No world was found for object (/Game/Mods/Cannibal_Captivity/Base/CannibalCaptivityLevel…)" | 99 | **99** |
| Same object-path family (digits normalised) | 23 kinds | identical 23 kinds |
| Position | log lines 7541-7658, after PreExit | identical line positions, after PreExit |
| Before shutdown / before main-world teardown | 0 / 0 | **0 / 0** |
| Burst | 95 ms (frame 151) | 84 ms (frame 796; the frame counter is not part of the signature) |
| Other unknown warnings/errors | none | **none** |
| New LoadErrors | none (ITQoL 23, AR 37 exact) | none |
| `quick_check` / singletons (mailbox, ITQoL ctrl, AR ctrl) | ok / 1,1,1 | **ok / 1,1,1** |

All acceptance conditions held, so the set was accepted as `CANNIBAL-CAPTIVITY-TEARDOWN-NO-WORLD` (`8002636`): bound to the exact pak hash; the exact message and object-path family (strictly under `/Game/Mods/Cannibal_Captivity/Base/CannibalCaptivityLevel.CannibalCaptivityLevel:PersistentLevel.`); only after the main-world teardown begins; **exactly 99**, judged on the whole log and all-or-nothing. A 100th line, fewer than 99, a line before teardown, a changed path/message/severity, a changed hash, or any other Cannibal-named warning/error leaves the matching lines an unknown problem. There is no broad `Contains("Cannibal_Captivity")` rule. Tests 470/470, 0 skipped.

State after the rerun: server OFFLINE; accepted 8-mod load order (the seven plus `Cannibal_Captivity.pak`); `modlist.txt` matches; verified post-run backup **`2026-10-04_001102`** (hashes + `quick_check` ok; live DB identical; mailbox/ITQoL controller/AR controller 1/1/1). Snapshots: `E:\CSC-M3-Live\live-test\batch-snapshots\cannibal-run-1` (retroactive) and `cannibal-run-2` (log sha256 `287647168E3B2B2BB44D3319E0793C9A4220FE4BABDD24D68DF3FA789043C9A1`). Shemite City State NOT started.

## Cannibal Captivity (D2 slot, 8th mod) — 2026-10-03 23:2x +07:00: STOP, rolled back (historical, superseded by the rerun above)

**Result: FAIL / STOP on the complete-log scan (99 unknown lines). Rolled back and verified. Shutdown NORMAL. World intact.**

- Source `Cannibal_Captivity.pak` (Workshop 3765743138), 112,432,880 B, SHA-256 `DB6E3C299912E48E4DEC8293A58C8DEF1D881FDBDC44F1E67CE99A9BF348F04F`. Imported through the production Local pipeline: source = installed hash, source untouched, load order 8, `modlist.txt` matched, live world unchanged.
- Preconditions: server Offline, no Conan process; live DB and modlist hashes equal the verified pre-D1 seven-mod state; verified pre-batch backup `2026-10-03_232338` (hashes + `quick_check` ok).
- `mod-boot --hold 600`: readiness 39.6 s (game port bound, world frame 2). Eight mods mounted in modlist order, container `Order` 1000-1007, no duplicate mounts. `Cannibal_Captivity` contributes 44 packages. No new `LoadErrors` (ITQoL 23 and AR 37 exact; none unattributed). No crash or assertion; 600.3 s online hold completed. Known warnings only: ITQoL mailbox x1, AR merge x2. Extracted-mods cache 26.7 MB.
- Stop: RCON `shutdown` acknowledged, 199.2 s = **NORMAL**, exit 0, no forced kill, no orphan, no WAL/SHM. World gates: `quick_check` ok; ITQoL mailbox 1, ITQoL controller 1, AR controller 1.
- **Failing gate:** the complete current-boot scan (written for D1) found **99 UNKNOWN lines, all one message**: `LogScript: Warning: Script Msg: No world was found for object (/Game/Mods/Cannibal_Captivity/Base/CannibalCaptivityLevel.CannibalCaptivityLevel:PersistentLevel.<BP_Build…>) passed in to UEngine::GetWorldFromContextObject().` All 99 fall in one 95 ms burst at frame 151, 16:37:54.735-.830 UTC, inside the world teardown that began at 16:34:51 (log closed 16:38:08). None during boot or the 10-minute hold. They are warnings, not errors; they are flagged because the line names the mod (the same message without a mod path occurs ~1,314 more times in this log as base-game volume).
- **Not whitelisted.** Per the operator rule (any UNKNOWN = rollback and STOP; do not broaden exceptions) the batch was rolled back: `restore 2026-10-03_232338`, then `remove-local Cannibal_Captivity.pak` (retired to `E:\CSC-M3-Live\app-data\removed-mods\20261003-233932-400\`, hash matches). Live DB sha256 `1FD6089F…4793` and modlist `4D48BF24…2D86` equal the pre-batch baseline; WAL 0 B. Failed-batch world kept in the automatic pre-restore backup `2026-10-03_233922`; latest verified backup `2026-10-03_233931`.
- Evidence (gitignored `artifacts/batch-d-20261003/`): `D2-CannibalCaptivity-ConanSandbox.log` (SHA-256 `5A65E722481870964B8279AADC12CDEFE09006710C89A4B3916164B600FF5369`), harness output, the 99 lines.
- Caveat: re-running `analyze-boot` on the preserved log now passes, only because the scanner derives mod names from the current catalog and Cannibal Captivity was removed from it. It is not evidence of a pass.
- Shemite City State NOT started (the rule is to stop). In-game behavior NOT verified. Decision needed: whether to accept this exact teardown warning (suggested scope if accepted: exact message kind with the `/Game/Mods/Cannibal_Captivity/Base/CannibalCaptivityLevel` path prefix, bound to the file hash above, teardown only, count cap) or exclude Cannibal Captivity.

## Operator decision — 2026-10-03: Fantasy Races EXCLUDED; AR merge warning ACCEPTED

- **Fantasy Races Of Exiles is EXCLUDED / DEFERRED from Modpack V1.** It reached readiness and did not corrupt the world, but produced 15 novel `NPC: Error: Data: No stat templates found for StatModifier template None.` lines, absent from the prior healthy boots, which may affect NPC stats/spawn behavior. Gameplay impact cannot currently be ruled out, so it is excluded rather than accepting an unsafe warning. D1 is NOT re-run, the 15 lines are NOT whitelisted, and no further reverse-engineering is planned in this phase. The D1 logs and evidence stay preserved for possible future investigation. The D1 classification below (INCONCLUSIVE) and all earlier D1 text are historical and unchanged.
- **Ancient Realms `LogModController: Error: AModController::MergeDataTables - ToBeAddedDataTable is null` is ACCEPTED as KNOWN NON-BLOCKING** (pending confirmation below is resolved), only while the exact signature matches, `Ancient_Realms.pak` SHA-256 is `12F7E719…FD1A`, and at most 2 occur per boot. A third occurrence, signature drift or a changed hash fails. Not broadened.
- Accepted core: StackMe10K, Savage Paragon, Grit & Grease, Thrall Reputation, Improved Thralls & QoL, WO Riding Thralls, Ancient Realms. Next live batch: Cannibal Captivity (Workshop 3765743138), then Shemite City State (3755371705).

## Previous checkpoint — 2026-10-03 22:55 +07:00: D1 scanner fix and read-only re-analysis (server not started)

Took over Codex's uncommitted scanner WIP unchanged, built and tested it (SDK 8.0.425, build 0 warnings/errors), then fixed two problems found by running it read-only on real logs, committed as `d8908ee`. Tests 440/440, 0 skipped (410 prior + 30 new).

**Scanner.** Every `Error`/`Fatal` line of any category is now scanned regardless of mod names, every occurrence kept, malformed/unknown lines fail closed, and a final scan of the complete current-boot log (through shutdown) runs after the readiness scan. The WIP as written flagged 1,646 lines on the known-good seven-mod boot (1,414 `LogScript` + 110 `LogDataTable` warnings are base-game volume), so it would have failed every boot. Corrections: warnings are scanned only in NPC/spawn/stat/persistence/save/database/mod-controller/world categories (or when they name a mod); nine recorded base-game error kinds, each present in at least 10 of 11 healthy boot logs, are suppressed by exact message kind and reported as the `BaseGameErrorKindsSuppressed` fact. The D1 message and near-misses of those kinds are never suppressed (tested).

**Operator confirmation pending.** Widening the scan newly surfaced `LogModController: Error: AModController::MergeDataTables - ToBeAddedDataTable is null` (2 per boot). It was never in the Batch C set. It appears directly after the Ancient Realms controller registers, in 3 of 3 boots that mount `Ancient_Realms.pak` and 0 of 8 without it. It is now a hash-bound known warning with `MaxOccurrences = 2` (a third line fails). Revert it if the operator does not accept it.

**Read-only analysis (no server start, `analyze-boot` on preserved logs).**

| Log | Result |
|---|---|
| Seven-mod baseline boot (`ConanSandbox.log`, backup 142750) | PASS (exit 0); known ITQoL mailbox x1, AR merge x2; LoadErrors ITQoL 23 / AR 37 exact; quick_check ok; mailbox/ITQoL ctrl/AR ctrl 1/1/1 |
| D1 boot (backup 142725, SHA-256 = preserved artifact `AC751A4E…`) | FAIL by design: exactly 15 UNKNOWN lines (the NPC stat-template message); nothing else unknown; quick_check ok; singletons 1/1/1 |

**D1 evidence on the 15 lines.** Identical message x15, 07:24:54-58 UTC, inside the first ~4 s of world ticking while NPCs spawn; none names an NPC, package or asset. The message occurs 0 times in all 11 earlier healthy boot logs (no mods through the seven-mod run); every other Error kind in the D1 boot also occurs in those logs. The Fantasy Races container index lists `FROE_StatModifierTemplateDataTable`, `FROE_StatTemplateDataTable`, `FROE_NPCStatsAndGrowthsDataTable`, region/village spawn data tables and a goblin summon NPC, so attribution to it is technically plausible but circumstantial (the mod's controller was freshly spawned in this boot). The D1 world (backup 142725): quick_check ok, exactly 1 `FROE_BP_ModController`, +1 `actor_position` and +1 `mod_controllers` row versus baseline, no persisted FROE NPCs, no crash/assertion/persistence-error lines.

**D1 classification: INCONCLUSIVE.** Not CANDIDATE NON-BLOCKING because: (1) only one boot exists, so stability is unproven; (2) attribution is circumstantial, not proven; (3) the message concerns NPC stat-modifier lookup, and an effect on spawned NPC stats cannot be ruled out from the log. Not BLOCKING because there is no crash, integrity or persistence evidence. No exception added. D1 not re-run; D2/D3 not started; server never started in this phase.

## Previous checkpoint — 2026-10-03 14:29 +07:00: STOP B, D1 rolled back

Resumed from 0bb2b2a. Installed exact official Microsoft SDK 8.0.425 user-locally with SHA-512 verification. QA-019/020 safety build and tests passed (410/410, zero skipped), then merged to the live branch at 09cbf1a and rebuilt/retested with the same results. First-run WAL test sharing failure was confined to the Windows test fixture and corrected before merge.

D1 Fantasy Races Of Exiles (embedded Workshop 3780741325, Enhanced 1.0.6; 5,293,057 bytes; SHA-256 2E4D3BEEC95FCBB81A9622A42405D2C3694EE632446D89C57EA8A93E27667D4A) imported through production pipeline after verified backup 2026-10-03_142352. Eight mods mounted in order; true readiness about 39 seconds (world frame 2). Exact ITQoL 23 and Ancient Realms 37 LoadErrors passed.

**D1 FAIL / STOP B:** subsequent full-log review found 15 `NPC: Error: Data: No stat templates found for StatModifier template None.` messages at 14:24:54-58. The prior seven-mod log `ConanSandbox-backup-2026.10.02-20.34.59.log` has zero. Attribution is unproven. The generic null-table errors and Lamplighter warnings also occur in prior logs, so they are not evidence of a new D1 regression. The harness's earlier scan and exit 0 do not establish batch acceptance: generic errors after its readiness scan escaped its coverage. No new exception was added.

Shutdown: acknowledged, 72.5 seconds NORMAL, exit 0, no forced kill/orphan/WAL/SHM. quick_check and singleton gates passed. Production restore of 142352 succeeded; failed-D1 world preserved in automatic pre-restore backup 142725. Production removal archived the D1 pak and reconciled the seven-mod catalog. Latest backup 142750 verifies the restored world, quick_check=ok, singleton counts=1/1/1. Live DB and modlist hashes match pre-D1. No additional boot after rollback; D2/D3/final validation NOT STARTED.

Evidence: `artifacts/batch-d-20261003/D1-ConanSandbox.log` (SHA-256 AC751A4EA3782488809AEA012EF8608036AAAA58B417938E1DFFD75ED601522B), structured `E:\CSC-M3-Live\live-test\m3-live-log.jsonl`; see FINAL_REPORT.md and AGENT_HANDOFF.md for final state. Client untouched; production world NOT CREATED.

Status: **IN PROGRESS: checkpoint 4C/4D PASSED** on an existing dedicated server installation. Waiting for review before 4E (one Local mod).

- **4F CLIENT JOIN = BLOCKED BY CLIENT AUTHENTICATION** (see "Checkpoint 4F"). This is not a network failure, a server failure or a version mismatch.
- **4E is not blocked** by the client problem. It is verified server-side only.
- Deployment target: **private friends-only dedicated server over Radmin VPN** (see "Deployment target").
- "Standalone-first" applies to the **server/management side** only (see "Direction change", corrected 2026-10-02).
- Nothing in this report claims live verification beyond what is listed under "Live verified".

- Branch: `claude/m3-task4-live-windows` (base `0352f40`, QA-verified)
- Host: Windows 10 Pro 19045, per-user .NET SDK 8.0.425
- Harness: `tests/ConanServerControl.LiveHarness` (opt-in; requires `CSC_LIVE_TESTS=1` **and** the `.csc-live-test` marker)
- Raw step log (redacted JSONL): `E:\CSC-M3-Live\live-test\m3-live-log.jsonl`

## Workspace

```
E:\CSC-M3-Live\                 dedicated M3 workspace (414 GB free; separate drive from the client)
  .csc-live-test                harness marker (not a Conan requirement)
  steamcmd\                     optional SteamCMD (installed in 4A)
  server\                       dedicated server target (EMPTY: 4B blocked)
  live-test\                    harness log, SteamCMD logs
  app-data\                     isolated CONAN_SERVER_CONTROL_DATA (settings, backups, staging, logs, diagnostics\)
Protected, never written:       D:\conan exiles\Conan Exiles Enhanced, D:\conan exiles
```

Overlap validation passed: no overlap with protected locations, no drive root, and no nesting between the subfolders.

Guard refusals were verified live:
- A workspace under `D:\conan exiles` was rejected (exit 3).
- Live commands without `CSC_LIVE_TESTS=1` were refused (exit 4).

## Pre-live code fixes

| Fix | Result |
| --- | --- |
| Hard server-executable gate | Start is allowed only for `ConanSandboxServer.exe` outside the client folder. Everything else is blocked before any status change or process launch: `Run Me!.bat`, client `ConanSandbox.exe`, scripts, the `-Shipping` binary, unknown names. |
| QA-018 path normalization | Trailing separator, `/` vs `\` and case no longer change path identity. 8.3 short names are not expanded (documented limitation). |
| Live-test guard | Env var plus marker required; destructive harness actions are refused outside the marked workspace. |

## Steps

| # | Time (local) | Operation | Result | Duration | Live files changed |
| --- | --- | --- | --- | --- | --- |
| 4A-0 | 01:39 | Harness `init` (folders + marker) | PASS | – | `E:\CSC-M3-Live` only |
| 4A-1 | 01:39 | Diagnostics before | `NOT READY FOR SERVER LIVE TEST`, sole server blocker: `steamcmd.exe was not found in the configured SteamCMD folder` | <1 s | no |
| 4A-2 | 01:39 | `SteamCmdService.InstallAsync` (official `steamcmd.zip` from `steamcdn-a.akamaihd.net`, then `+quit`) | **PASS** | 10.7 s | `steamcmd\` only |
| 4A-3 | 01:40 | Diagnostics after | `READY FOR SERVER LIVE TEST` (SteamCMD PASS / FILES INSPECTED) | <1 s | no |
| 4B-1 | 01:40 | `SteamCmdService.InstallOrUpdateDedicatedServerAsync` | **FAIL** (exit -2) | 3.0 s | none (`server\` still empty) |
| 4B-2 | 02:2x | Retry on request (same harness) | **FAIL** (exit -2, same Fastly reset) | 3.1 s | none |
| 4B-3 | 02:35 | User-supplied `C:\Users\vkkha\Downloads\steamcmd.exe` (Valve-signed, valid) to `D:\conan exiles\Conan Exiles Dedicated Server` | 1st run: legacy self-update OK, exit 7, relaunch did not run. 2nd run: **FAIL** exit -2 (Fastly) | 8 s + 3 s | SteamCMD unpacked its own files into `Downloads`; server folder **not created** |
| 4B-4 | - | Official target set to `D:\conan exiles\Conan Exiles Dedicated Server` (sibling of the client). Harness `CSC_SERVER_DIR` + `LiveTestGuard.ValidateExternalServerDirectory`; `install-server` network preflight | **BLOCKED** at preflight (exit 5): `client-update.steamstatic.com` connection reset | <1 s | none (folder not created) |

The full 4B command was: `+force_install_dir "E:\CSC-M3-Live\server" +login anonymous +app_update 443030 validate +quit`.

### 4A details

- `E:\CSC-M3-Live\steamcmd\steamcmd.exe`, 4,407,448 bytes.
- SteamCMD's original 2013 bootstrapper downloaded its 43,472 KB update from the legacy host and exited with **code 7**. That is SteamCMD's "updated, relaunching" code, which the app already accepts.
- No SteamCMD process remained afterwards.

### 4B details: network-blocked

- SteamCMD's message, in the Vietnamese UI locale, means "Fatal Error: SteamCMD needs a network connection to update".
- From `steamcmd\logs\bootstrap_log.txt`:
  - the 2026 bootstrapper migrates to win64 using `https://client-update.steamstatic.com/steam_cmd_win64`
  - that host returns `http error 0` and `Unable to read and verify install manifest steam_cmd_win64.installed`
- Read-only reachability probes from this host:

| Host | CDN | Result |
| --- | --- | --- |
| `client-update.steamstatic.com` | Fastly | **TLS reset** (`curl: (35) Recv failure: Connection was reset`) |
| `cdn.steamstatic.com` | Fastly | **TLS reset** |
| `shared.fastly.steamstatic.com` | Fastly | **TLS reset** |
| `client-update.akamai.steamstatic.com` | Akamai | 200 |
| `steamcdn-a.akamaihd.net` | Akamai | 200 |
| `media.steampowered.com` | Valve legacy | 200 |
| `api.steampowered.com` | – | 200 |
| `steamcommunity.com` | – | 200 |

- **Classification:** OPTIONAL WORKSHOP / SERVER-AUTO-INSTALL INTEGRATION BLOCKED BY NETWORK. It is **not** a core product blocker.
- The app behaved correctly: a user-facing failure, a FAIL log entry, and nothing installed or deleted.
- No workarounds were attempted: no system or network changes, no VPN or proxy, no firewall or antivirus changes, no `steam.cfg`, no host overrides.
- SteamCMD stays installed in `E:\CSC-M3-Live\steamcmd` for optional future use.

## Checkpoint 4C/4D: real dedicated server (existing installation)

### Server and gate

- Server root: `D:\conan exiles\Conan Exiles Dedicated Server`, a sibling of the client `D:\conan exiles\Conan Exiles Enhanced`.
- `DedicatedServerLocator` picked `D:\conan exiles\Conan Exiles Dedicated Server\ConanSandboxServer.exe`, the install root (the first candidate).
  - This file is a 332,648-byte Unreal `BootstrapPackagedGame` launcher, signed **Funcom Oslo AS** (valid).
  - It spawns `ConanSandbox\Binaries\Win64\ConanSandboxServer-Win64-Shipping.exe` (186 MB, Funcom-signed) as a child process.
- Build `++exiles+release-beta-CL-378132` (beta branch). The standalone client is `++exiles+release-CL-377096`; the versions **differ**.
- Working directory: not set, so the executable's folder (the server root) is used.
- Gate: `DedicatedServer`, start allowed. Path overlap: PASS (siblings; neither is nested in the other).
- Configured through the harness with the same locator and gate logic as Settings "Use existing server installation". No SteamCMD, no `app_update`, nothing copied.

### Diagnostics before the first boot

`READY FOR SERVER LIVE TEST`, with no FAIL. Warnings, all expected before a first boot:
- the `Saved` folder doesn't exist yet
- no world yet
- RCON password not set
- the client has no `Mods` folder

SteamCMD is optional, and network-blocked for downloads.

### Live run (server log timestamps are UTC)

| Step | Result | Detail |
| --- | --- | --- |
| 7 first boot (old probe) | Process PASS, **readiness premature** | Online at 5.4 s on "game port 7777 bound". The log was still on engine frame 0, and the world only started ticking ~45 s later. |
| 7 stop (old harness) | **Aborted** | The harness's own `StateChanged` handler threw (empty timeline) inside `StopAsync`, leaving status `Stopping` with the server running. The server was later stopped through the app (attach + stop, forced after the timeout, no RCON configured). **Product fix:** subscribers are isolated. |
| 8 process identity | PASS | Bootstrap PID → `-Shipping` child, both under the server root. Arguments `-log -port=7777 -QueryPort=27015` come from the app. No client process at any point. |
| Signal timing (boot 2) | — | UDP 7777 at 5 s; RCON listen + `listplayers` reply at 5 s; query 27015 at 27 s; **log frame advancing at 34 s**. UDP 7778 is never bound (this Unreal 5 build only uses 7777). |
| Graceful command | — | `DoExit` was received but ignored (not in `RconCommandLog`); `exit` → "Couldn't find the command: exit"; RCON `help` lists **`Shutdown`**; `shutdown` → "Successfully executed: shutdown", exit code 0 after ~57 s, no WAL/SHM left. **Product fix:** default command is `shutdown`, graceful timeout raised from 30 s to 120 s. |
| 9 boot + stop (fixed app) | **PASS** | Online at 31 s ("World is ticking (server log frame 2)"). Graceful stop 65.3 s, exit code 0, no processes left. |
| 10 Start | **PASS** | Online at 32 s. |
| 10 Stop | **PASS** | 63.5 s, exit code 0 (RCON `shutdown`). |
| 10 Start | **PASS** | Online at 30.7 s. |
| 10 Restart | **PASS** | 89.6 s: Stopping → Offline (61 s) → Starting → Online (29 s). |
| 10 Stop | **PASS** | 59.6 s, exit code 0. Post-cycle check found no processes. |
| 11 world files | Enhanced | After a graceful stop: `game_0.db` only (WAL/SHM checkpointed away). After forced kills, `game_0.db-wal` / `-shm` remained. Conan also keeps `game_0_backup_1..4.db` and `game_0_upgrade_tags_%d30_1.db`. |
| 12 boot / stop | **PASS** | Online at 30 s (frame 4); graceful stop 62.2 s, exit code 0. |
| 12 cold backup | **PASS** | `2026-10-02_032514`: WorldType Enhanced, main `game_0.db` 667,648 B. |

**Step 12 backup verification detail:**
- SHA-256 `323575125C7086D45CEF68BD83D823DB7759B2892D1548D152D8B07B1AAD91C7`. The manifest recorded it, and an independent hash of the backup copy matches.
- `ManifestWritten` / `HashesVerified` / `SqliteVerified` are all True; `quick_check` = `ok`.
- The live world files were identical before and after the backup.

### Findings (not blocking)

1. The Conan server log reports `Autologin attempt failed, unable to register server!` (server-list registration). Not a blocker for the private Radmin target (see "Deployment target").
2. Readiness fix: `ConanSandbox.log` current-run frame 0 means not ready. Live Online times are now 30–32 s.
3. The backup copy's folder gains `game_0.db-shm` (32 KB) and `game_0.db-wal` (0 B) after verification. SQLite creates them when `quick_check` opens the **copy**. The copied `game_0.db` hash still matches the manifest, and the live world is untouched. A follow-up could open with `immutable=1` or verify a temporary copy.
4. The backup copies the whole `Saved` tree into `world\` (including `Config` and `Logs`) and also into `config\`, so the configuration is stored twice. This is pre-existing design.
5. Client/server build mismatch (beta vs live). Resolved by the version-matched server below; it is not the 4F blocker.
6. `configure-rcon` wrote `[RconPlugin]` into the throwaway server's `Saved\Config\WindowsServer\Game.ini`. Conan requires the RCON password in plaintext there. It is random, and the app stores it only with DPAPI.

### Safety

- Client: **0 files modified** under `D:\conan exiles\Conan Exiles Enhanced` since the session started.
- Nothing was written to the `D:\conan exiles` top level.
- SteamCMD was not used. Router and firewall were not modified.

## Version-matched dedicated server (CL-377096 / 2.2.2)

**Why a different server was needed.** The first test server was `++exiles+release-beta-CL-378132` (ProjectVersion **2.2.3**). That turned out to be the **current Steam public branch** (app manifest: no BetaKey, buildid `25639945`). "release-beta" is only Funcom's internal stream name. The standalone client is `++exiles+release-CL-377096` (ProjectVersion **2.2.2**), i.e. one patch older.

**The matched server.**
- Downloaded on another machine with `download_depot 443030 443031 236179869812429142` and placed at `D:\conan exiles\depot_443031`, a sibling of the client.
- Both server binaries report `++exiles+release-CL-377096` and are signed **Funcom Oslo AS** (valid). The server log reports `Build: ++exiles+release-CL-377096` and `ProjectVersion 2.2.2`, the same as the client log.
- `download_depot` fetched only depot 443031, so the Steamworks runtime from depot 1004 was missing. Six **Valve Corp.-signed** DLLs were copied, hash-verified, from the app's own SteamCMD folder: `steamclient(64).dll`, `tier0_s(64).dll`, `vstdlib_s(64).dll`. `steamwebrtc*.dll` (client voice) was not available and is not needed by the server. **No client file was used.**

**Gate and diagnostics.** Gate: allowed. External-directory validation: PASS. Diagnostics: `READY FOR SERVER LIVE TEST`, no FAIL. RCON was configured with `configure-rcon`, which now writes `Game.ini [RconPlugin]` before the first boot.

| Step | Result | Detail |
| --- | --- | --- |
| First boot | **PASS** | Online at 32.0 s ("World is ticking (server log frame 2)"). Bootstrap and `-Shipping` processes both under `depot_443031`. No client process. |
| Graceful stop | **PASS** | 64.9 s via RCON `shutdown`, exit code 0, no processes left. |
| Boot before backup | **PASS** | Online at 29.9 s; graceful stop 63.0 s, exit code 0. |
| Cold backup | **PASS** | `2026-10-02_044344`: Enhanced, `game_0.db` 643,072 B, SHA-256 `6F8A467C2DC5A41299EDFCEEA906288761B7CDC2757B9636730526484692C8D7`. Manifest written, hashes verified, `quick_check` = `ok`, live world unchanged by the backup. |

**Still observed:** `Autologin attempt failed, unable to register server!`, so the server does not appear in the public server list. This is **not a blocker** for the private Radmin target, provided authenticated clients can direct-connect (see "Deployment target"). Direct connect is part of 4F.

**Client:** 0 files modified. SteamCMD was not used on this machine for the download.

## Pre-4E vanilla client connectivity smoke test

- Checkpoint persisted first: `f695638` on `origin/claude/m3-task4-live-windows`.

**Server (`D:\conan exiles\depot_443031`, CL-377096 / 2.2.2).**
- Started through the app. Bootstrap PID 26028, `-Shipping` child PID 16688.
- **Online at 30.9 s** ("World is ticking (server log frame 2)").
- Endpoints owned by PID 16688, all on `0.0.0.0`:

  | Protocol | Port | Role |
  | --- | --- | --- |
  | UDP | 7777 | game |
  | UDP | 7778 | used by this build (the beta build did not bind it) |
  | UDP | 14001 | role unknown |
  | UDP | 27015 | query |
  | TCP | 25575 | RCON |

- LAN IPv4 `192.168.0.244` (Ethernet, Private). Also `26.84.226.21` (Radmin VPN).
- `ServerPassword` empty, `IsBattlEyeEnabled=False`.
- Online subsystem on the server: **Fls** (Funcom Live Services) / NULL, not Steam.
- Direct-connect target for a client on this PC: `127.0.0.1:7777`; from the LAN: `192.168.0.244:7777`.

**Client (manual, by the operator).**
- The standalone client started in **offline mode**. Its own log says `LogFuncomLiveServices: Error: Login failed: couldn't connect.`, and the UI says online play is unavailable and only single player works.
- Direct Connect lives under *Play Online*, which is not available in offline mode.

**Server side.** No connection attempt was logged after the baseline: no accept, pre-login, login or join lines.

**Conclusion.**
- Online play, including Direct Connect, requires the client to log in to Funcom Live Services, which authenticates through the client's platform.
- This client cannot log in. Getting past this would require bypassing an authentication/licensing mechanism, which this project will not implement or recommend.
- The legitimate path is a licensed client (e.g. Steam), which logs in to FLS normally.
- The server's own `Autologin attempt failed, unable to register server!` (server-browser registration) is not a blocker for the private Radmin target. Public registration is not a release criterion.
- Root cause and classification: see "Checkpoint 4F".
- Reachability check: the Funcom telemetry host `live.commontelem.flx.wintercloud.net` answers (HTTP 404), so this is not the same as the Fastly network block.

**New finding: graceful stop after a long uptime.**
- After an ~11-minute hold, RCON `shutdown` was received and world teardown began immediately.
- Conan's exit then exceeded the 120 s graceful timeout, and the app force-killed the server (exit code -1).
- `game_0.db-wal` (395 KB) was left behind. Committed data is recovered by SQLite on the next open, but the stop was not clean.
- Earlier short runs exited in ~57–63 s.
- Recommendation: treat "teardown started" as progress and allow a longer timeout (e.g. 300 s), or wait while the process is still in its exit sequence.
- Conan also writes its own rotating `game_0_backup_N.db` every ~5 minutes while running.

## Checkpoint 4F: client join — BLOCKED BY CLIENT AUTHENTICATION

**4F CLIENT JOIN = BLOCKED BY CLIENT AUTHENTICATION.**

Observed client: `D:\conan exiles\Conan Exiles Enhanced`, build `++exiles+release-CL-377096` (ProjectVersion 2.2.2). The session analysed is the operator's manual launch on 2026-10-02 at 05:01 local (`launcher.log`), 22:01 UTC in the client log.

**Observed behaviour.**
- The client enters FLS offline mode.
- Play Online, and Direct Connect under it, is unavailable. Only single player works.
- The server receives no player connection attempt (no accept, pre-login, login or join lines).
- Client/server version match is **not** the blocker: both are CL-377096 / 2.2.2.

**Evidence** (client log `ConanSandbox\Saved\Logs\ConanSandbox.log`, read-only):

| Line | Log text | Meaning |
| --- | --- | --- |
| 499–502 | `STEAM: Steam User is subscribed 1`, `Client API initialized 1`, `Created online subsystem instance for: STEAM` | The Steam API layer reports success. |
| 511–512 | `Created online subsystem instance for: Fls`, `Loaded subsystem for type [Fls]` | Online play goes through Funcom Live Services. |
| 533, 539 | `Build: ++exiles+release-CL-377096`, `Net CL: 377096` | Same build as the server. |
| 1713 | `Requested Message FlsOfflineMode` | The client falls back to offline mode. |
| 1801, 2652 | `LogFuncomLiveServices: Error: Error in Login: Steam auth token not available.` | **Root cause:** no platform authentication token. |
| 2904–2905 | `Error in Login: couldn't connect`, `Login failed: couldn't connect.` | FLS login fails; the client stays offline/single-player. |

**Steam-emulation artifacts in the client installation.**
- `Engine\Binaries\ThirdParty\Steamworks\Steamv164\Win64\` contains `steam_emu.ini` and `steam_api64.rne` next to `steam_api64.dll`. These are Steam-emulation artifacts, not part of the Steamworks runtime.
- The emulated layer reports a subscribed user, but it cannot produce the platform authentication token that FLS requires. That matches the log above.
- The launcher folder also holds a third-party distributor's shortcut and readme (`AnkerGames - Free Pre-installed PC Games.url`, `Read Me.txt`).
- The artifacts were listed by name only. Their contents were not opened, and they were **not modified**.

**Classification.**

| Candidate | Verdict | Why |
| --- | --- | --- |
| Network failure | **No** | The first login error is the missing Steam auth token, and network reachability cannot supply a token. A read-only probe on 2026-10-02 reached `services.live.exiles.wintercloud.net` (HTTP 404 at `/`, TLS in ~0.7 s). The telemetry host answers too. The same client session also logged FLS API timeouts (`GetBuildOverrides`, `GetActiveEvents`, PlayFab retries). They are secondary. |
| Server failure | **No** | The server was Online with the world ticking, and UDP 7777 / 27015 were bound on `0.0.0.0` by the server process. No join reached it because the client never got past FLS login. |
| Version mismatch | **No** | Client and server are both CL-377096 / 2.2.2. |
| Client authentication | **Yes** | `Steam auth token not available` → FLS login failed → `FlsOfflineMode`. |

**Policy.**
- No authentication or licensing bypass was attempted, and none will be: the emulation artifacts are untouched, there is no forced join from offline mode, and nothing patches Steam or FLS.
- Client bypass investigation is **closed**.
- Client files modified: **none**.

**Unblock condition.**
- 4F resumes only with a **legitimate Conan client session** that can obtain the platform authentication token FLS requires (for example a licensed Steam copy with Steam signed in).
- Radmin VPN provides the private network path. It does **not** replace FLS/platform authentication.
- When 4F resumes:
  - direct connect over Radmin to `<host Radmin IP>:7777` (this host's Radmin address was `26.84.226.21`)
  - apply a Client Mod Bundle
  - confirm the client `modlist.txt` format

## Deployment target: private friends-only over Radmin VPN

PROJECT DEPLOYMENT TARGET:
- private friends-only server
- approximately 5 players
- Radmin VPN virtual LAN
- direct connection over the Radmin/private IP, when the client is authenticated
- no public server browser requirement
- no public IP exposure requirement
- no router port forwarding requirement, unless explicitly requested later
- no UPnP requirement
- RCON must remain private/local
- public FLS server registration is **not** a release criterion

`Autologin attempt failed, unable to register server!` is therefore **NOT A BLOCKER** for the intended private deployment, provided authenticated clients can direct-connect. No project time is spent making the server public.

## Checkpoint 4E: not blocked by the client problem

4E (one Local `.pak` mod) is **NOT blocked** by the 4F client problem. It is verified server-side, and no client connection is required:
- backup before mutation (verified cold backup)
- transactional install
- `modlist.txt`
- server startup
- real readiness
- server log evidence that the mod loaded
- rollback verification

## Private Radmin deployment and long-run graceful stop (live)

**Deployment model.** Private friends-only over Radmin VPN; see HANDOFF "Deployment model". Live diagnostics:
- `network.private-vpn` = PASS: Radmin VPN `26.84.226.21`, friends direct-connect `26.84.226.21:7777`. Public registration and port forwarding are not used.
- `rcon.exposure` = WARNING: Conan listens on `0.0.0.0:25575`, the app connects to `127.0.0.1`. Never port-forward RCON.
- `GetLanIPv4` now skips VPN adapters and reports `192.168.0.244`; it previously returned the Radmin address.

**Long-run graceful stop with the 300 s default** (server CL-377096):

| Event | Time |
| --- | --- |
| Online | 32.9 s |
| Held online | 660 s |
| RCON `shutdown` received; `BeginTearingDown` | 22:38:12 UTC |
| `LogExit: Preparing to exit` | 22:40:44 (152 s later) |
| `LogExit: Exiting` | 22:41:00 |

- App `StopAsync`: **PASS in 171.8 s, exit code 0**, no processes left.
- World after stop: `game_0.db` only, no WAL/SHM. The WAL left by the previous forced kill was checkpointed by Conan on startup.
- Exit duration grows with uptime: ~57–65 s after short runs, >125 s and 171.8 s after ~11 minutes.
- 300 s covers what was observed. Longer uptimes are not yet measured; a future refinement could keep waiting while the log shows the exit sequence progressing.

## Pre-4E validation: graceful stop design and network address selection

Code: `0d54acc`, reviewing `2c56226` (flat 300 s wait) and `982a5c3` (LAN skips VPN adapters). Design details are in HANDOFF "Pre-4E validation".

**Automated.**
- `982a5c3` alone: build PASS, 293 / 293.
- `0d54acc`: build PASS (0 warnings, 0 errors), **313 / 313**, 0 skipped, run twice.

The 20 new cases in `M3PreE4StopAndNetworkTests` cover:
- Radmin is reported separately from the physical LAN (live host adapter set).
- Unrelated VPN and virtual adapters are never labelled Radmin or LAN, including an adapter renamed "Radmin VPN".
- No usable Radmin adapter → not detected: absent, Down, APIPA only, or enumeration failure.
- 26/8 is preferred.
- An acknowledged shutdown gets the extended window without a kill.
- Log progress without an RCON reply gets the extended window.
- No reply, a rejected command, or no RCON → 1 s short window, then kill (never the 300 s window).
- An acknowledged-but-hung shutdown is killed after the extended window.
- Offline is not reported until the child exits after the launcher, with a throwing `StateChanged` observer.
- A child that survives the kill → Error, never Offline.
- A real `cmd` → `ping` tree: the child is tracked after the launcher dies and killed.
- RCON to a silent server times out (no hang).
- The log probe ignores the previous run's exit lines and handles rotation.

**Live diagnostics (this host).**
- `network.private-vpn` = PASS: `PhysicalLanIPv4` `192.168.0.244`, `RadminVpnIPv4` `26.84.226.21`, `RecommendedRadminDirectConnect` `26.84.226.21:7777`, `SameLanDirectConnect` `192.168.0.244:7777`.
- The down TAP-Win32 and Bluetooth adapters (APIPA) and Teredo were ignored.

**Live 10-minute stop** (server `D:\conan exiles\depot_443031`, CL-377096; started and stopped through the app via the harness; no client started; local times):

| Item | Result |
| --- | --- |
| Start | Online at 33.4 s (launcher PID 27436, `-Shipping` child PID 7172) |
| Held online | 620.2 s (about 11 minutes of uptime at stop) |
| RCON `shutdown` sent | 06:07:22.863 |
| Shutdown acknowledgement | Reply `Successfully executed: shutdown`. Server logged receipt at 06:07:23.354; the app had the reply by 06:07:24.47 |
| Progress evidence | `LogCore: Engine exit requested` (seen 06:07:24.469) |
| Window used | **300 s extended** |
| World unload start (`PreExit Game`) | 06:07:23.365 |
| `LogExit: Preparing to exit` | 06:09:43.120 (quiet teardown of 139.8 s) |
| `Game engine shut down` / `Exiting` | 06:09:45.518 / 06:09:56.812 |
| Process tree exit | 06:09:57.195 (154.3 s after the command) |
| `StopAsync` | **PASS**, 157.6 s, Status Offline |
| Exit code | 0 |
| Forced kill | **NO** |
| `game_0.db-wal` / `-shm` after stop | **NO / NO** (`game_0.db` only) |
| Orphan server processes | **NO** |

## M3 Task 4E: one real Local .pak mod (live, PASS)

- Remote head before mutation: `581615d`. Working tree clean; no other session or process was active; server Offline.
- **Test mod:** `C:\Users\vkkha\Downloads\mod conan\WickProbe.pak`, 4,492,459 B, valid Unreal pak (footer magic, pak version 12), single file. SHA-256 `D7FE0EC099BC501AFB9F5A2BF18B9312FF100F37591F2ACF39AB08F5B465D79C`. Outside the server Mods folder.
- **Baseline:**
  - Diagnostics: READY, Dedicated Server PASS, Enhanced world present, no Mods folder / modlist, Radmin unchanged.
  - World: `game_0.db` 655,360 B, SHA-256 `ee5bed07…`, no WAL/SHM.
  - Client snapshot `pre-4E` (20 entries).
- **Backup before mutation:** `2026-10-02_063005`, manifest + SHA-256 + `quick_check` = ok. The import pipeline also made its own `pre-local-mod-import` cold backup.
- **Local import (PASS, 0.6 s, no SteamCMD, no Workshop):**
  - Installed `D:\conan exiles\depot_443031\ConanSandbox\Mods\WickProbe.pak`, SHA-256 identical to the source.
  - `modlist.txt` = `WickProbe.pak`, load order 1.
  - The source file is still present and unchanged.
- **Boot with mod (PASS):** Online at 31.3 s ("World is ticking"). Launcher PID 9708, `-Shipping` PID 2108. **Positive server-side load evidence**:
  - `LogModManager: Mounting mod pak file: …/Mods/WickProbe.pak`
  - Conan extracts `WickProbe-WindowsServer.pak/.utoc/.ucas` into `Saved\ExtractedMods`. The Enhanced mod `.pak` is a container of platform sub-paks.
  - `Mounted Pak file '…/ExtractedMods/WickProbe-WindowsServer.pak', mount point: '…/Content/Mods/WickProbe/'`
  - `Mod 'WickProbe' contributes 5 package(s)`, `AddActiveModControllerClass: /Game/Mods/WickProbe/BP_WickProbeController`, `Persistence: Spawning mod controller: BP_WickProbeController_C`
  - No warnings or errors mention the mod.
- **Stop with mod (PASS):**
  - RCON reply `Successfully executed: shutdown`; progress evidence `Engine exit requested`; extended 300 s window.
  - Total 65.4 s, exit code 0, no forced kill, launcher + child gone, no orphans, no WAL/SHM.
  - The world DB changed (SHA-256 `be018702…`), as expected for a mod controller.
- **World integrity after the mod (PASS):** cold backup `2026-10-02_063232`, `quick_check` = ok.
- **Removal (PASS):**
  - `pre-mod-removal` backup `2026-10-02_063246`.
  - `WickProbe.pak` was moved out of Mods to `app-data\removed-mods\20261002-063246-420\` (recoverable).
  - `modlist.txt` is now empty, unrelated files are unchanged, and the source is unchanged.
- **Boot after removal (PASS):**
  - Online at 31.3 s. No `Wick` lines, no missing-mod errors, no stale controller references.
  - Graceful stop 64.7 s, exit code 0, no forced kill, no WAL/SHM.
- **Client:** snapshot `post-4E` compared to `pre-4E`: 0 added / 0 removed / 0 changed.
- **Finding:** Conan does not delete its extraction cache `Saved\ExtractedMods\WickProbe-WindowsServer.*` (~1.5 MB) after the mod is removed. It is not mounted (not in the modlist). A future removal step could retire matching `ExtractedMods` files as well.
- **Product change made for this checkpoint** (`581615d`): removing a Local mod now retires its installed pak (SHA-256-gated move, never a delete).

## Modpack V1 Batch A + multi-mod load order (4E.2) — live, PASS

- Remote head before the test: `1e318d4`. Branch clean; no Conan process.
- Server `D:\conan exiles\depot_443031` (CL-377096). All steps went through the harness using the app's real services. No client, no Workshop, no SteamCMD.
- A concurrent docs-only session ("Twelve Legends removal and quest system") was active. Over a coordination message it confirmed it would not start the server, run the harness or touch `Mods`.

**Mods** (sources in `C:\Users\vkkha\Downloads\mod conan`, all valid pak v12 with a `-WindowsServer` sub-pak; installed to `ConanSandbox\Mods\<name>.pak`):

| Role | Mod | File | Size | SHA-256 (source = installed) |
| --- | --- | --- | --- | --- |
| A | StackMe10K (Nexus mod 3) | `StackMe10K.pak` | 4,641,754 B | `30F5DF54…5C8A0` |
| B | Savage Paragon (Workshop 3766043945; size matches) | `SavageParagon.pak` | 4,760,799 B | `5F2673D9…312B5` |
| C | Grit & Grease (Workshop 3801774752; size matches) | `GritandGrease.pak` | 68,049,336 B | `B5FA39CC…4ACCA` |

| Step | Result | Evidence |
| --- | --- | --- |
| Baseline | PASS | Diagnostics READY (0 FAIL). `Mods` held only an empty `modlist.txt`; empty catalog. Verified cold backup `2026-10-02_073104`, `quick_check` = ok. |
| Import ×3 (production Local pipeline) | PASS | Installed SHA-256 = source SHA-256; the sources are unchanged. Each import also took its own verified cold backup. |
| Initial order A, B, C (`MoveAsync`) | PASS | `modlist.txt` = `StackMe10K.pak \| SavageParagon.pak \| GritandGrease.pak`; `.pak` files untouched. |
| Boot 1 | **PASS** | Online 44.5 s. Mount sequence StackMe10K → SavageParagon → GritandGrease. Container `Order` 1000 / 1001 / 1002. Contributes 5 / 91 / 382 packages. No duplicates and no mod errors. |
| Reorder to C, A, B (`MoveAsync`) | PASS | `modlist.txt` = `GritandGrease.pak \| StackMe10K.pak \| SavageParagon.pak`. Every `.pak` has an unchanged SHA-256, size, creation and write time, so nothing was re-copied. |
| Boot 2 | **PASS** | Online 36.4 s. Mount sequence GritandGrease → StackMe10K → SavageParagon. Container `Order` G&G 1000, StackMe10K 1001, Paragon 1002. |
| Remove middle (StackMe10K) via `RemoveAsync` | **PASS** | Verified `pre-mod-removal` backup `2026-10-02_073932` (`quick_check` ok). Pak retired to `app-data\removed-mods\20261002-073932-695\StackMe10K.pak` (hash matches). `modlist.txt` = `GritandGrease.pak \| SavageParagon.pak`. Remaining hashes and the source are unchanged. |
| Boot 3 (`--expect-absent StackMe10K.pak`) | **PASS** | Online 40.6 s. G&G → Paragon (Order 1000 / 1001). No log line mentions StackMe10K. No missing-mod or stale-modlist errors. |
| Restore A (cumulative batches) | PASS | Re-imported (hash match), order back to A, B, C. |
| Boot 4 (Batch A final) | **PASS** | Online 46.5 s; same sequence and Order as Boot 1. `Persistence: Loading mod controller` for `StackMe10K_Modcontroller_C`, `BP_SavageParagon_ModController_C` and `BP_GritnGreaseModController_C`. No error or warning line mentions any of the three. |
| Final verified cold backup | PASS | `2026-10-02_074354`, `game_0.db` 667,648 B, `quick_check` = ok. |

Stops: all four were acknowledged RCON `shutdown`s, 300 s extended window, 66–68 s, exit code 0, **forced kill NO**, no WAL/SHM, **no orphan processes**.

**Runtime load order: PROVEN** for mount order and container priority.
- In both configurations the server's `Mounting mod pak file` sequence followed `modlist.txt` exactly.
- The IoStore container `Order` was reassigned by modlist position: first entry 1000, then +1 per entry.
- **Not exercised:** which mod wins an asset both override. None of the three is known to override the same asset, so later-entry-wins precedence is Unreal's documented behaviour for a higher `Order`, not observed here.

**ExtractedMods** (read-only):
- Conan extracts each mod's `-WindowsServer` sub-pak once (all mtimes 07:32:05) and reuses it on later boots.
- While StackMe10K was removed, `StackMe10K-WindowsServer.pak/.ucas/.utoc` (1,440,291 B) stayed in the cache but was not mounted. It is current again after the restore.
- `WickProbe-WindowsServer.*` (1,491,114 B) is still stale from 4E.
- Technical debt:
  - Removal does not retire extraction-cache files.
  - **Unverified risk:** whether Conan refreshes the cache when a Local `.pak` is replaced by a newer file of the same name.
- Nothing was deleted.

Other notes:
- **StackMe10K 10,000 stacks: IN-GAME BEHAVIOR NOT YET VERIFIED.** Server logs only show that it mounts and loads.
- Boot time with Batch A: 36–47 s (vanilla 30–33 s).
- **Client: 0 files changed** under `D:\conan exiles\Conan Exiles Enhanced` during the run.
- **Harness issues found and fixed during the run:**
  - **Log-offset bug** (fixed in this checkpoint): Conan rotates `ConanSandbox.log` on start, so the first Boot 1 analysis read past the new log's mount lines and reported 0 mounts (FAIL). The server itself had mounted all three mods. Now a changed first log line means "read from 0", and Boot 1 was re-run and passed.
  - **Shell quoting mistake** (operator side, before the successful imports): three `import-local` attempts were run with a wrong path. The pipeline rejected them at staging and nothing changed; three extra verified baseline backups were taken.
- Build PASS (0 warnings, 0 errors); `dotnet test` 315 / 315, 0 skipped.

## Modpack V1 Batch B — STAGING / PRE-PRODUCTION (server-side PASS with one known issue; in-game work BLOCKED)

- **Environment:** `D:\conan exiles\depot_443031` = **STAGING / PRE-PRODUCTION**. The world is a **TEST / VALIDATION WORLD**. No production save exists.
- **Code and repo:**
  - Validated code baseline `2aca0cf`.
  - The repo was at `bc9899a` (docs by the "Twelve Legends" session; its files were not touched).
- **Coordination:** the other session reported that its user instruction says Batch B must not start before the paks are validated read-only.
  - This session installed Batch B on its own user's explicit instruction, after the read-only validation below.
  - Further server actions are **paused** pending the user's confirmation.
- **Pre-change copies:**
  - `modlist.txt` and `Saved\Config\WindowsServer\*.ini` copied to `E:\CSC-M3-Live\live-test\batchB-pre-20261002-141001\` (SHA-256 recorded).
  - Verified cold backup `2026-10-02_141009` (`quick_check` = ok). The world hash equals the end of Batch A, so nothing changed it in between.
- **Server-setting discrepancy (not changed):** `ServerSettings.ini` has `ThrallDamageToNPCsMultiplier=0.5` (the Conan default), not the `0.300000` named as the server philosophy. Batch B only forbids increasing it, so it was left at 0.5. The operator decides whether to set 0.3.

**Paks**

All three are in `C:\Users\vkkha\Downloads\mod conan`. Each is a valid Unreal pak v12 with a `-WindowsServer` `.pak/.utoc/.ucas` payload, and its size equals the Workshop item size. Source SHA-256 = installed SHA-256, and the sources are untouched.

| Mod | File | Size | SHA-256 |
| --- | --- | --- | --- |
| Thrall Reputation (3787066846) | `ThrallReputation.pak` | 833,760 B | `5CE7A95D31400518DF31F72349FBB4D181D2D6D0771D970159B4DBF150DE10B9` |
| Improved Thralls & QoL (3758661389) | `ImprovedThrallsAndQoL.pak` | 154,632,086 B | `F35D9D927B4E76869D57DC7073B61FCF2215039B628343B28D6B0989A0B48272` |
| WO - Riding Thralls (3803149679) | `WO_RidingThralls.pak` | 78,896,983 B | `ACF569D38D7CB73E7523A096200AB1948E72C2783CB70953F43ADC2EFE7B34A8` |

**Load order (staging, after Batch B)**

Batch A is kept unchanged and Batch B is appended through the production pipeline. `MoveAsync` confirmed the order with no `.pak` rewritten.

1. `StackMe10K.pak`
2. `SavageParagon.pak`
3. `GritandGrease.pak`
4. `ThrallReputation.pak`
5. `ImprovedThrallsAndQoL.pak`
6. `WO_RidingThralls.pak`

No verified dependency required changing it. Savage Paragon's "load after any XP mod" does not apply: ITQoL's XP features (Inactive Follower XP, party Shared XP) are not XP-curve changes, and both are off by default.

**Boots**

| Boot | Result | Evidence |
| --- | --- | --- |
| B1 (5-minute hold) | **PASS** | Online 41.5 s. Mount sequence = modlist; container `Order` 1000–1005. Contributes 5 / 91 / 382 / 18 / 942 / 57 packages. New controllers spawned: `ReputationModController_C`, `MC_ImprovedThrallsAndQoL_C`, `WO_BP_RT_ModController_C`. No crash or crash loop over 5 minutes. Memory at readiness: 4.81 GB working set, 5.49 GB private. |
| B1 errors | **CORRECTED: 23 LoadErrors from ITQoL** (originally recorded as "none from the mods") | Every `Error:` / `Warning:` category is also in the vanilla log with the same messages (AIDataTable `WarTest*` rows, `ItemInventory`, `building` stability, `LogBaseSpawner`, `LevelStreaming` `/Game/Developers/...`, `BinkMoviePlayer`). Two `LogActor` warnings attach the ITQoL `Lamplighter_Sphere` component to NPCs. **Missed at the time:** see the correction note below. |
| B1 stop | PASS | Acknowledged `shutdown`, extended window. **191.4 s**: quiet teardown of 171 s, then exit at 189 s. Exit code 0, no forced kill, no WAL/SHM, no orphans. |
| B2 (restart) | Load PASS, **1 known issue** | Online 39.6 s, same sequence and Order. All three new controllers `Loading` from the save (no re-spawn, no duplicates). **Known issue:** `Persistence: Error: UConanBuildingPersistenceComponent::CreateHealthPool - DefaultObject not loaded: /Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_PL_ServerMailContainer`. |
| B2 stop | PASS | 70.0 s, exit code 0, no kill, no WAL, no orphans. |

**World integrity**

Read-only checks on copies of the verified backups `141009` → `142111` → `142352`:
- **pre-B → B1: only additions.**
  - Three mod controllers (ids 127–129).
  - **One ITQoL placeable**: `BP_PL_ServerMailContainer` (id 147, owner −1, hidden at z = −50000; property `MailboxChestSpawned`). It belongs to the mod's "Mailbox System".
  - Three properties and 23 `game_events` rows.
  - Nothing deleted; `quick_check` = ok.
- **B1 → B2:** only `game_events` +17. The mailbox is still a single object (id 147, same health rows), and the controllers are unchanged.
- **The TEST world has no player data:** `account`, `characters`, `item_inventory`, `guilds` and `follower_markers` are all 0. Player, base, inventory and follower integrity therefore has **nothing to verify yet**; it needs a client.
- Stale save rows: the 4E WickProbe controller (id 123) still has `mod_controllers` and `actor_position` rows after its removal. No error is logged.

**Configuration** (nothing was invented; no files or DB rows were edited for mod settings)

- **Thrall Reputation:** **No supported balance configuration found — using mod defaults.**
  - The author lists configuration options as a future improvement.
  - Assets show fixed tiers (`E_FriendshipTier` with a damage-bonus percentage per tier) and UI only (`W_ReputationBar`, `W_ThrallPartyList`).
  - No global buff was applied.
- **Improved Thralls & QoL:**
  - Settings exist only in its in-game admin UI (`DataCmd ImprovedThrallsQoL`; assets `DT_DefaultSettings`, `DT_GeneralSettings`, `E_AdminSettingsType`). No server file or RCON path is documented.
  - The author states "Everything is disabled by default!". No settings were persisted in the world after two boots, so the server runs compiled defaults.
  - Every requested OFF / 1.0 / 0 value is therefore met **by default (author claim, not seen in the UI)**. Every requested ON value is **NOT APPLIED** until an authenticated admin client is available.
  - All requested options exist in the current description, except a **global Thrall Base Stat Modifier**: only *Individual* Thrall Base Stat Modifiers exist (pets and golems have global ones).
- **Riding Thralls:**
  - Mode is per player, chosen at the craftable "Registrar" placeable in-game. Modes described: one mount per follower; shared mounts; the player's follower as **passenger on the player's mount** (closest to "Ride With Me").
  - **No mode is selected yet** (needs a client).
  - The mod changes no combat stats and adds no follower count.

**Blocked by 4F (no authenticated client):** client join and mod-mismatch check, all in-game checks for Thrall Reputation, ITQoL and Riding Thralls, and the Authority / Commander role-balance test (Step 8).

**Known issues and risks**
- ITQoL server mailbox `CreateHealthPool` persistence error on restart: the object persists and is not duplicated; in-game effect not yet verified.
- Stop time grew to 191 s with six mods after about 6 minutes of uptime. The 300 s extended window still held, but larger mods (Ancient Realms 497 MB, Shemite 2.1 GB) and longer uptimes may exceed it; re-measure in Batch C.
- `ThrallDamageToNPCsMultiplier` is 0.5 vs the stated 0.3 philosophy.
- The 4E WickProbe controller rows remain in the save.

**Correction (2026-10-02, after the Batch C review): Batch B boot logs contain 23 `LoadErrors` per boot.**

What was missed:
- Every Batch B boot (B1, B2 and the three restart cycles) logs 23 lines of `LoadErrors: While trying to load package None, a dependent package None (<id>) was not available`, each followed by `FPackageName: Unable to identify a valid mount point associated with skipped package None`.
- They reference 16 unique package IDs.
- Batch A boots: 0 such lines.
- The original scan searched for `Error:`. These lines read `LoadErrors:`, so they were missed and "none from the mods" was recorded.

Attribution to **Improved Thralls & QoL** (read-only byte search, confirmed independently by both sessions):
- All 16 IDs appear, as 8-byte little-endian values, only in `ImprovedThrallsAndQoL.pak` and its extracted `ImprovedThrallsAndQoL-WindowsServer.ucas`. They do not appear in the other five installed mods or in Ancient Realms.
- According to the "Twelve Legends" session's container check, none of the IDs is a real package in any mod or vanilla container. They are **dangling references**, not a missing dependency.
- Caveat: the log names no referencing asset ("package None"), so the attribution rests on the byte search.

Impact:
- The server still boots, all six mods mount, and the ITQoL controller loads.
- The in-game effect (some ITQoL feature or asset not loading) is **not verified**; it needs a client.
- The harness at that time did not flag `LoadErrors` lines. That is a harness gap, not a mod-boot PASS criterion that was met.

## Modpack V1 Batch B — restart stability (3 cycles) and acceptance

**Operator decision (2026-10-02):**
- Batch B is **ACCEPTED**: **SERVER-SIDE COMPATIBILITY: PASS**.
- **IN-GAME BEHAVIOR: NOT YET VERIFIED** (4F).
- **ITQOL MAILBOX ISSUE: KNOWN NON-BLOCKING WARNING.**
- Batch B stays installed; no rollback; Batch C not started.

### Restart cycles

- **Setup:** staging `D:\conan exiles\depot_443031` (TEST world) with the six-mod load order unchanged. Mods, ITQoL settings and the client were not touched.
- **Per cycle:**
  1. `mod-boot --hold 150`: Offline → Start → true readiness → about 150 s online → graceful stop.
  2. `cold-backup`: verified backup with `quick_check`.
  3. A separate read-only check of the backup copy (`immutable=1`): `quick_check` plus a count of the mailbox rows.
- **Baseline:** the B2 backup `142352`.

| Item | Cycle 1 | Cycle 2 | Cycle 3 |
| --- | --- | --- | --- |
| Readiness ("World is ticking") | 33.3 s | 33.2 s | 39.6 s |
| Mods loaded, modlist order, `Order` 1000–1005 | 6/6 | 6/6 | 6/6 |
| `BP_PL_ServerMailContainer` CreateHealthPool line | 1 (frame 0) | 1 (frame 0) | 1 (frame 0) |
| Mailbox actor rows (`buildings` / `buildable_health` / `properties`) | 1 (1 / 2 / 1) | 1 (1 / 2 / 1) | 1 (1 / 2 / 1) |
| Duplicate / missing mailbox | NO / NO | NO / NO | NO / NO |
| Other ITQoL errors | none | none | none |
| Other mod-specific errors / fatals | none / none | none / none | none / none |
| Stop (acknowledged RCON `shutdown`, exit 0, no WAL/SHM, no orphans) | PASS, 180.3 s | PASS, 177.0 s | PASS, 183.7 s |
| Forced kill | NO | NO | NO |
| Verified backup, `quick_check` | `143558` ok | `144252` ok | `144937` ok |

- The mailbox is the same object every cycle: actor 147 at (0, 0, −50000).
- 113 actors and 23 `mod_controllers` rows, unchanged.
- The only row-count change is the server's own `game_events` table (cycle 3: 302 → 325).

### Known non-blocking warning rule (harness)

`Core/LiveTesting/ModBootGates` holds **exactly one** rule, `ITQOL-MAILBOX-HEALTHPOOL`.
- **Exact message** (the line without its `[timestamp][frame]` prefix): `Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool - DefaultObject not loaded: /Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_PL_ServerMailContainer.BP_PL_ServerMailContainer_C`
- **Version-bound:** it applies only while the installed `ImprovedThrallsAndQoL.pak` SHA-256 is `F35D9D927B4E76869D57DC7073B61FCF2215039B628343B28D6B0989A0B48272`. A new ITQoL version must be validated again.
- `mod-boot` reports a matching line under `KnownNonBlockingWarnings`, not `ModRelatedProblems`.
- The boot **still FAILS** when:
  - any other ITQoL line, any other BP_PL line or any other mod error appears
  - the same message appears with a different ITQoL file
  - the **ITQoL mailbox gate** fails: after every `mod-boot` stop, the stopped world (read-only, immutable) must hold **exactly 1** `BP_PL_ServerMailContainer`. 0 = MISSING, 2 or more = DUPLICATE, unreadable = FAIL.
  - world integrity fails: `cold-backup` `quick_check` is unchanged.
- **Unit tests:** `M3ModBootGateTests`, 29 cases.
- **Live check on real data:** the new read-only `analyze-last-boot` command, run on the cycle 3 log and the stopped world.
  - Analysis PASS: `ModRelatedProblems` = none, `KnownNonBlockingWarnings` = `ITQOL-MAILBOX-HEALTHPOOL x1`.
  - Mailbox gate PASS (count 1).
  - The world file was unchanged, and no WAL/SHM was created.

### Shutdown duration metric (all later batches)

- Every stop now logs a `shutdown duration gate` entry. **240 s or more = HIGH RISK = FAIL**: stop before adding another batch. The graceful window is 300 s.
- **Batch B observed range: 177–184 s** after about 2.5 minutes of uptime. B1 was 191 s after about 6.5 minutes; B2 was 70 s after a short uptime.
- Batch A stops were 66–68 s.
- The extra time is a **silent ~160 s gap** after `BattlEyeClient: ClientLoadingScreenStopped` and before `LogExit: Preparing to exit`. No log lines appear in it and no mod is named, so the cause is not attributed.

### Known-good restore point pinned

- Pre-Batch-B backup `2026-10-02_141009`: Batch A world, `game_0.db` 667,648 B, SHA-256 `C6BC2052…8415`.
- **Copied** (robocopy, timestamps kept) to `E:\CSC-M3-Live\pinned-backups\2026-10-02_141009`, outside `app-data\backups`. Retention (keep latest 10 / keep 14 days) only lists and deletes inside `app-data\backups`.
- 87 files, 21,316,556 B. Every file's SHA-256, size and mtime equals the source, and the source was unchanged by the copy. The world hash matches the backup's own `metadata.json`.
- The copy is marked read-only. Per-file hashes and restore notes sit beside it: `2026-10-02_141009.SHA256SUMS.txt`, `README-PINNED.txt`.
- The original stays in `app-data\backups` until retention ages it out (about 2026-10-16).

### Build

`dotnet build -c Release --no-incremental`: 0 warnings, 0 errors. `dotnet test -c Release`: **344 / 344**, 0 skipped (315 + 29 `M3ModBootGateTests`).

## Modpack V1 Batch C — Ancient Realms Enhanced: test, investigation, rollback (STAGING)

**Operator status (2026-10-03):**
- Ancient Realms: **ROLLBACK COMPLETE · RETEST REQUIRED · COMPATIBILITY NOT YET ACCEPTED · NOT REJECTED.**
- Its load errors are **not** whitelisted. A retest runs only on a quiet host (see "Host load" below).
- Batch D is not started.

### Source and import

- **Source:** `C:\Users\vkkha\Downloads\mod conan\Ancient_Realms.pak`.
  - 496,900,005 B, equal to the Workshop item size.
  - SHA-256 **`12F7E7192043270FC5F2290C5989F8B8285494BA47A054646790B4A042D1FD1A`**.
  - Valid pak v12, unencrypted index, with a `-WindowsServer` `.pak/.ucas/.utoc` payload.
  - modinfo: Workshop `3755775098`, v0.1.260707, `minimumVersion` Enhanced.
  - No dependency keys, no Workshop Required items, no companion files.
- **Pre-Batch-C backup** `2026-10-02_152307` (`quick_check` ok), **pinned** at `E:\CSC-M3-Live\pinned-backups\2026-10-02_152307`. 102 files byte-identical to the source, read-only, plus `.SHA256SUMS.txt`.
- **Import (Local pipeline):** PASS.
  - Verified pipeline backup `152436`.
  - Installed SHA-256 = source SHA-256; the source is unchanged.
  - modlist = the 6 Batch B mods + `Ancient_Realms.pak` (Order 1006).

### Boots

All boots are on the staging TEST world.

| Boot | Mods | Readiness | AR errors: named / all AR-attributable | Peak working set / max private | Stop: shutdown sent → tree exit | Stop gate | Forced kill | World check |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| C1 (2026-10-02, 200 s hold) | 7 | 35.4 s | 7 / 37 | 6.8 / 9.1 GB | 15:29:10.6 → 15:32:09.9 | 180.6 s | NO | `153648`, `quick_check` ok |
| C2 (2026-10-02, 130 s hold) | 7 | 35.3 s | 7 / 37 | 8.1 / 9.0 GB | 15:39:52.3 → 15:42:43.4 | 172.2 s | NO | `154352`, `quick_check` ok |
| Investigation cycle 1 (2026-10-03, 150 s hold, **loaded host**) | 7 | **102.1 s** | 7 / 37 | 6.1 / 9.0 GB | 00:28:16.6 → 00:33:17.2 | **301.8 s** | **YES** (exit −1, WAL 671,592 B left) | `003449`: `quick_check` and `integrity_check` ok after WAL replay |
| Rollback check (2026-10-03, 150 s hold, **loaded host**, **without AR**) | 6 | 67.2 s | 0 / 0 | 6.6 / 9.0 GB | 00:42:40.5 → 00:47:41.2 | **301.7 s** | **YES** (exit −1, WAL 671,592 B left) | `004820`: `quick_check` and `integrity_check` ok after WAL replay |

- Every boot: all mods LOADED in modlist order; ITQoL mailbox gate = 1 (known warning ×1); no fatal or crash.
- Both forced kills came after `LogExit: Game engine shut down`, and the last world write preceded the teardown.

### Ancient Realms error evidence (kept for the retest decision)

**Exactly the same 7 named lines on every AR boot** (C1, C2, investigation cycle 1):

```
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/AR_BP_ModController, a dependent package None (35924C262CEDD960) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/AR_BP_ModController, a dependent package None (CFFAB4A08E3DB146) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/_MASTER/BP_PL_Decal_Floor_Master, a dependent package None (D0B5ED96C9B95112) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Brick/brick_03/BP_PL_Water_Well_Fountain, a dependent package None (FE8C96EB21683A73) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Ceramic/ceramic_02_white/BP_PL_Water_Well_Fountain_gold, a dependent package None (FE8C96EB21683A73) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Concrete/concrete_04/BP_PL_Water_Well_Fountain, a dependent package None (FE8C96EB21683A73) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Stone/stone_05/BP_PL_Water_Well_Fountain, a dependent package None (FE8C96EB21683A73) was not available.
```

- **Exactly the same 37 AR-attributable `LoadErrors` per AR boot** (identical multisets): the 7 named lines plus 30 `package None` lines. They reference 10 unique missing package IDs:
  - `1B6F0F85A6FACB01`, `1FCB1FA801D75583`, `35924C262CEDD960`, `5A19E15D92AF952`, `7C3C9C3D45215971`
  - `9F5919676AD734DE`, `CFFAB4A08E3DB146`, `D0B5ED96C9B95112`, `F6DA87602985C3AA`, `FE8C96EB21683A73`

  The 23 ITQoL-attributable lines (Batch B) are unchanged, and there are 0 unattributed `LoadErrors`.
- **Version-bound:** tied to `Ancient_Realms.pak` SHA-256 `12F7E719…FD1A`. The installed hash was re-checked on the day.
- **Dangling references, not a missing dependency:**
  - The 10 IDs are a real package entry in **no** container: AR's own three TOCs (4,953 / 7,222 / 4,953 entries), the 34 vanilla server `.utoc`, the 37 vanilla client `.utoc`, and all 13 local mod paks.
  - Their names are unresolved, because the mods' asset registries are compressed.
- **Referencing classes are present:** the `FPackageId` (CityHash64 of the lower-case UTF-16 package name) of all 6 referencing packages is in the AR server container. The building classes and the controller ship and load; only some of their dependencies are absent.
- **Maps:** AR ships three developer maps (`AlmostEmpty`, `icon_creation_map`, `test_building_map`). The server never referenced or streamed them, and there are no map or level errors.
- **Controller:** `AddActiveModControllerClass` → `Persistence: Spawning mod controller: AR_BP_ModController_C` (C1), then `Loading mod controller` (C2, cycle 1).
  - One actor and `mod_controllers` row 148, never duplicated.
- **No other AR error lines.** No crash. **No real save error**: the only save-like matches are vanilla `LogBinkMoviePlayer` title-movie failures, present in Batch B too. Persistence errors are the known ITQoL line only.
- Runtime errors after world start are identical in kind and count to Batch B.
- **SQLite:** every backup passes `quick_check`. After a forced kill, `integrity_check` also passes once the WAL is replayed (on a private temp copy). There are no duplicate actors, and the only world changes are AR additions plus `game_events`.

### Rollback (operator rule: any failed criterion → roll back)

Investigation cycle 1 failed **no forced kill** and **stop < 240 s**. Cycles 2–3 were not run.
1. `remove-local Ancient_Realms.pak`: verified `pre-mod-removal` backup `2026-10-03_003751`. The pak was retired to `app-data\removed-mods\20261003-003756-722\` (hash matches); modlist = the 6 Batch B mods; the source is unchanged.
2. `restore 2026-10-02_152307`: new harness command using the production `IBackupService.RestoreAsync`, which takes a pre-restore safety backup (`2026-10-03_003829`).
   - The restored `game_0.db` SHA-256 = backup (`20841D67…`); WAL after restore = 0 B.
   - World, `Saved\Config` and `modlist.txt` were restored.
3. **Verified:**
   - Boot: 6/6 mods; analysis PASS with the known ITQoL warning only.
   - Backup `004820`: 113 actors, 23 `mod_controllers` rows, mailbox 1, 0 AR actors, 0 duplicates, the same actor-class set as the pre-C baseline.

### Host load: the forced kills are not attributable to Ancient Realms

- The rollback check boot **without** AR shows the same forced kill (301.7 s) as AR cycle 1 (301.8 s).
- **Host state at that time:** League of Legends (game and client) running, plus Discord, Chrome and ChatGPT; about 6 GB RAM free. With no server running, the other session measured CPU 49–82% busy and a commit charge of 26 / 32 GB.
- **Effect on boots:** readiness rose from about 35 s to 67–102 s.
- **Effect on stops:** the silent stop gap (PreExit → `LogExit: Preparing to exit`) rose from about 160 s (2026-10-02 afternoon) to about 288 s.
- **Conclusion:** timing data from this host state is not comparable. The AR retest needs a quiet host, and the 300 s force-kill ceiling needs the policy change below.

### Notes

- **Staging config:** `ServerSettings.ini` `ThrallDamageToNPCsMultiplier` = 0.300000, set by the other session after the rollback (backup `2026-10-03_005048`). Restoring any older backup brings back 0.5, so re-apply 0.3 after a restore.
- **ExtractedMods:** AR's server files (14,622,398 B) stay in `Saved\ExtractedMods` after removal, unmounted; WickProbe is still stale too. Nothing was deleted.
- **Harness:** `restore <backupId>` (server Offline only; PASS = restored world hash equals the backup and no non-empty WAL).
- **Build:** 0 warnings / 0 errors; `dotnet test` 344 / 344, 0 skipped.

### Shutdown policy correction (operator, 2026-10-03)

The compatibility threshold and the force-kill ceiling are now separate:

| Time | Meaning |
| --- | --- |
| 240 s | HIGH RISK: the batch compatibility gate fails and the next batch is not started (unchanged) |
| 300 s | Graceful window. A shutdown **acknowledged by RCON or progressing in the current-boot log** is no longer killed here. The overrun is logged (`GracefulWindowExceeded`). |
| 600 s | Emergency ceiling (`AdvancedSettings.EmergencyStopCeilingSeconds`, a new key, so existing settings files load 600). The process tree is killed here if still alive. |

- With no acknowledgement and no shutdown progress, the 30 s short fallback is unchanged; nothing waits 600 s blindly.
- The ceiling is never below the graceful window.
- "Offline only when the whole process tree is gone" and the final tree kill are unchanged.
- Regression tests:
  - `M3PreE4StopAndNetworkTests` adds 6: an acknowledged stop that outlasts the window is not killed; log progress without an RCON reply continues; an unproven stop never waits for the ceiling; a ceiling below the window is raised; the defaults are 30/300/600; an old settings file loads 600. The hang-forever case is now killed at the ceiling, not the window.
  - `M3ModBootGateTests` adds 3: stops of 240 s or more still fail the batch gate, even with the 600 s ceiling.
- **Build:** 0 warnings / 0 errors; `dotnet test` **353 / 353**, 0 skipped.

### Quiet-host retest (2026-10-03 01:01–01:23): all 3 cycles PASS

**Host:**
- League of Legends closed; no other games; idle build servers stopped; no other Conan or harness session; 8.0 GB RAM free.
- Background load noted: a stuck `tasklist | findstr` pipeline (`findstr` PID 28280) has used about 2 of 8 threads since 2026-10-02 03:51. It was also present during every earlier baseline run and was left untouched.

**Setup:**
- Start state: the known-good pre-Batch-C world (post-rollback, verified backup `2026-10-03_010141`: 113 actors, 23 controllers, 0 duplicates; `ThrallDamageToNPCsMultiplier=0.3`).
- Ancient Realms reinstalled through the Local pipeline: backup `010158`; installed SHA-256 = source = `12F7E719…FD1A`; 7-mod test order.
- Each cycle: `mod-boot --hold 150` with the new stop policy, then a verified cold backup.

| Cycle | Readiness | AR named / attributable | Other AR errors | Shutdown ack / engine exit requested | Stop (gate) | Window exceeded | Forced kill | Peak working set / max private | Backup, `quick_check` | Persistence / save errors | Controller | Mailbox |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 36.5 s | 7 / 37 | 0 | YES 01:05:45.8 / 01:05:46.3 | 183.1 s | NO | NO | 7.66 / 9.06 GB | `010858` ok | known ITQoL only / 0 | spawned (row 148) | 1 |
| 2 | 36.5 s | 7 / 37 | 0 | YES 01:12:34.9 / 01:12:35.4 | 185.9 s | NO | NO | 8.45 / 9.01 GB | `011547` ok | known ITQoL only / 0 | loaded | 1 |
| 3 | 36.5 s | 7 / 37 | 0 | YES 01:19:16.1 / 01:19:16.6 | 195.9 s | NO | NO | 8.74 / 9.05 GB | `012238` ok | known ITQoL only / 0 | loaded | 1 |

- Every stop: exit code 0, no WAL/SHM left, no orphans.
- World changes across the cycles: the AR controller actor and `mod_controllers` row 148 (cycle 1), then `game_events` only. No duplicate or missing persistence objects.
- **Cross-boot comparison:** across all six AR boots (C1, C2, investigation cycle 1, retest 1–3), the 7 named and 37 attributable signatures are **identical multisets**. There are 0 other AR error lines, 0 unattributed `LoadErrors`, no crash, and no save errors.
- **Criteria met for reclassification.** The AR errors are eligible for a KNOWN NON-BLOCKING WARNING rule bound to `Ancient_Realms.pak` SHA-256 `12F7E719…FD1A`. **Not applied: the operator decides.**
- **Stop-time trend:** 183 → 186 → 196 s, still under 240 s but rising, with a margin of about 44 s. Watch it before Batch D.
- Staging now runs **7 mods** (Ancient Realms reinstalled).

### Batch C acceptance and warning classification (operator, 2026-10-03)

- **BATCH C SERVER-SIDE COMPATIBILITY: PASS**
- **ANCIENT REALMS GAMEPLAY: NOT YET VERIFIED**
- **MAP / BUILDING / COLLISION BEHAVIOR: NOT YET VERIFIED**
- **Reclassified as KNOWN NON-BLOCKING WARNING:** only the exact validated AR signatures, bound to `Ancient_Realms.pak` SHA-256 `12F7E7192043270FC5F2290C5989F8B8285494BA47A054646790B4A042D1FD1A`:
  - the 7 named lines above (`ANCIENT-REALMS-DANGLING-REF-1..7`)
  - the exact 37-entry per-boot `LoadErrors` multiset, including the 30 "package None" lines
- **No other Ancient Realms error is suppressed.**
- **The exception is invalidated by:**
  - any new AR signature, or an extra or missing occurrence
  - a changed file hash
  - a crash
  - a save or persistence error
  - a world-integrity failure (`quick_check`)
  - a missing or duplicate persistence object (the AR controller must exist exactly once in the stopped world)
- **Harness implementation:** `ModBootGates` plus the harness (exact known-warning rules, the attributed `LoadErrors` set gate, an AR controller gate, and tests). It is committed separately once built and tested. The build is deferred until the other session's shutdown-timing study ends, so the measurements are not disturbed.
- **Also recorded:**
  - ITQoL's 23 `LoadErrors` per boot (Batch B, `cc13afa`) are **PENDING OPERATOR CLASSIFICATION**: monitored exactly, not accepted.
  - With the new gate, any unattributed `LoadErrors`, or any from a mod without a validated set (Batch D onward), fail the boot.
- **Timing context:** the other session's first isolated data point, Batch A alone (3 mods, about 150 s hold), stopped in 210.2 s, longer than the 6- and 7-mod quiet runs (172–196 s). Stop time is not driven by mod count alone. The study's conclusions belong to that session.

## Direction change: standalone-first (corrected 2026-10-02)

After 4B the requirement was recorded as "players use **standalone** Conan clients (no Steam client, library or Workshop sync)". 4F showed that this is wrong for multiplayer. Corrected statement:

> Conan Server Control is standalone-first on the **SERVER/MANAGEMENT** side. SteamCMD and the Steam client are not required for normal server-management operations once a valid Dedicated Server installation exists.
>
> Local `.pak` mods and Client Mod Bundles may be managed independently of Workshop.
>
> However, multiplayer clients must use a legitimate Conan client session capable of obtaining the platform authentication token required by Funcom Live Services. Radmin VPN does not replace FLS/platform authentication.

SteamCMD remains **optional** infrastructure.

What was implemented and unit-tested (272 / 272 tests):
- **Local mod source.** Flow: `.pak` → validate → COPY to isolated staging → SHA-256 → locked pipeline (stop if running → verified cold backup → transactional commit with rollback → restart only if it was running) → `modlist.txt`.
  - The source file is only read.
  - SteamCMD is never used.
  - Manual update means replacing with a newer file of the same name.
- **Client Mod Bundle export.** `Mods\*.pak` + `Mods\modlist.txt` + `manifest.json` (load order, size, SHA-256, source type, optional Workshop ID) + `README.txt`.
  - No game files are included.
  - Export refuses client and server locations.
  - The bundle is assembled atomically.
  - A read-only client sync planner compares a bundle with a client folder. It never writes.
  - It distributes **only** `.pak` mod files, the modlist, manifest/hash metadata, and permitted configuration material.
  - It does **not** distribute the game client, provide authentication, replace a platform license, bypass FLS, or modify Steam authentication.
- **Existing dedicated server** is a first-class path (Settings → Use existing server installation). Readiness no longer requires SteamCMD when a valid existing `ConanSandboxServer.exe` is configured.

## Live verified so far

1. SteamCMD bootstrap install and self-update through the app's real `SteamCmdService` (4A).
2. The app's failure handling when SteamCMD cannot reach Valve's update CDN (4B: correct failure, no partial install).
3. Workspace guard, marker guard and layout rejection against real paths.
4. The real standalone client `D:\conan exiles\Conan Exiles Enhanced` was detected read-only (Task 3 and harness). Nothing in the client folder was written.
5. Existing dedicated server, version-matched CL-377096: first boot, real readiness, Start/Stop/Restart, graceful RCON `shutdown`, and a verified cold backup (4C/4D, sections above). `ConanSandboxServer.exe` was found at the install root.
6. 4F classification: the client is blocked at FLS authentication (see "Checkpoint 4F").
7. Pre-4E: acknowledgement-gated graceful stop after ~11 minutes of uptime (extended window, 154 s, exit 0, no kill, no WAL, no orphans), and Radmin/LAN address diagnostics (see "Pre-4E validation").

## Not live verified

- Dedicated server install through SteamCMD (4B): blocked by network. An existing installation is used instead.
- Local or Workshop mod on a real server, and server mod load evidence (4E). Not blocked by the client.
- Client Mod Bundle applied to a real client, and client join (4F): **BLOCKED BY CLIENT AUTHENTICATION**.
- Workshop update (4G): **NOT EXERCISED**
- Client `modlist.txt` format. The bundle writes one file name per line, the same as the server. This must be confirmed when 4F resumes with a legitimate client.

## Risks / findings for the next checkpoint

1. **Dedicated server source.** The official dedicated server is Steam app 443030 (free, anonymous SteamCMD), and that download is blocked from this network. Resolved for testing: a version-matched depot was downloaded on another machine (see "Version-matched dedicated server").
2. **Client authentication.** Confirmed: 4F is **blocked by client authentication** (see "Checkpoint 4F").
   - The observed client cannot obtain the platform token FLS requires, and its installation contains Steam-emulation artifacts.
   - The project will not bypass Steam/Funcom authentication. 4F stopped and reported.
3. **Mod redistribution.** Bundles redistribute mod files to players. Administrators are responsible for respecting mod authors' terms.
4. **Fresh-server backups.** The cold safety backup requires `ConanSandbox\Saved` to exist. On a server that has never booted, a mod import aborts at the backup step. This is the existing Task 2 policy and has not changed.

## Shutdown-timing study (STAGING, 2026-10-03) — non-randomized

**Operator conclusion (accepted 2026-10-03):** Conan Enhanced has a long base-game shutdown phase. It is predominantly single-threaded and highly sensitive to host CPU contention. **Shutdown duration is not attributed to mod count.**

**Setup**
- Server `D:\conan exiles\depot_443031`, staging TEST world, `ThrallDamageToNPCsMultiplier=0.300000`.
- Every run: boot → real readiness → **150 s hold** → RCON `shutdown`.
- Metric: `ShutdownSentAt` → `ProcessTreeExitedAt` (whole process tree gone).
- Configurations were switched only through the production catalog (`SetEnabledAsync` + `MoveAsync`; harness `set-mods`, which lives on the local worktree branch `wip/shutdown-timing` and is not merged). No `.pak` was copied or removed.
- Pre-study verified backup `2026-10-03_012832`, pinned read-only in `E:\CSC-M3-Live\pinned-backups\`. Restored through the app afterwards.
- Host: League of Legends closed. An orphaned `findstr` (PID 28280, started 2026-10-02 03:51, about 2 of 8 threads) ran throughout. Chrome and Discord were open.
- **Not randomized:** configurations ran in a fixed order, one block of three runs each. Batch A's runs drifted from 210 to 193 s, so order and warm-up effects are mixed with configuration effects.

**Results** (seconds, shutdown command → process-tree exit)

| Configuration | Runs | Median | Policy class (2026-10-03) |
| --- | --- | --- | --- |
| No mods | 171.0 / 169.7 / 170.1 | 170.1 | NORMAL |
| Batch A + Riding Thralls | 174.8 / 174.6 / 175.5 | 174.8 | NORMAL |
| Batch A + Thrall Reputation | 183.9 / 179.1 (run 2: startup hang, below); reruns 182.1 / 193.7 | 183.0 (4 runs) | NORMAL |
| Batch A + Ancient Realms | 194.4 / 181.1 / 183.3 | 183.3 | NORMAL |
| All 7 mods (control, after restore) | 175.9 | — | NORMAL |
| All 7 mods (other session's quiet-host retest, ~150 s hold) | 183.1 / 185.9 / 195.9 | 185.9 | NORMAL |
| Batch A (3 mods) | 210.2 / 199.8 / 193.3 | 199.8 | NORMAL |
| Batch A + Improved Thralls & QoL | 220.9 / 206.2 / 211.2 | 211.2 | NORMAL |

All study runs were acknowledged, with exit code 0, **no forced kill**, no WAL/SHM left, and no orphan processes.

Earlier data points under host load: 301.7 / 301.8 s, force-killed by the then-300 s policy, with League of Legends running and CPU at 49–82 % with the server off.

**What the server does during the long phase** (5 s process sampler, Batch A run 2)
- From `PreExit Game` until `LogExit: Preparing to exit`, the `-Shipping` process uses **exactly one CPU core continuously** (about 5.0 CPU-seconds per 5 s).
- Disk read/write counters are flat, working set and private memory are flat, and the log is silent. The game-thread frame counter stays frozen (for example `[898]` for 187 s).
- The network shows no wait pattern: one connection goes `CloseWait` mid-phase without effect.
- So the long phase is single-threaded CPU work on the game thread, not network or disk waiting. Any competing CPU load stretches it, which matches the 288 s phase seen while League of Legends was running.

**Interpretation limits**
- No mods vs mods: about 170 s vs about 175–211 s. Mods add at most about 40 s at this uptime, while the base game alone accounts for about 170 s.
- Differences between mod configurations are within the drift seen inside a single configuration's three runs (up to 17 s). **This study does not attribute a specific shutdown penalty to a specific mod.**
- Shutdown time grows with uptime (about 70 s after 40 s, about 170–210 s after 3 min, about 154 s vanilla after 11 min on 2026-10-02). Long production uptimes may be slower still. Not measured.

**Shutdown policy (operator decision, 2026-10-03)**

| Duration | Class | Action |
| --- | --- | --- |
| < 240 s | NORMAL / ACCEPTABLE | — |
| 240–300 s | WARNING | Investigate host load. Not a mod compatibility failure. |
| 300–600 s | DEGRADED | No new mod batch until reviewed. A graceful shutdown with positive progress continues. |
| ≥ 600 s | EMERGENCY ceiling | Process-tree kill if still alive. |

No-ack / no-progress shutdowns keep the short (30 s) unresponsive fallback. The stop behaviour is already in the product (`26091fa`).

The harness gate (`ModBootGates.EvaluateShutdownDuration`) follows this table on branch `claude/m3-task4-integration` (`29c74e5`); see "Integration branch" below.

**Batch A + Thrall Reputation startup hang: TRANSIENT / NOT REPRODUCED**
- Study run 2 (02:35:52) hung right after launch. The log stopped 2 s in, at `Loading asset registry state for mod 'SavageParagon'`, and readiness timed out after 600 s ("World still loading").
- The same configuration was rerun twice on 2026-10-03:
  - 43.9 s and 38.5 s, both "World is ticking", mod load PASS.
  - Stops 182.1 s and 193.7 s; exit 0, no kill, no WAL, no orphans.

**State after the study**
- The world was restored to the verified final backup `2026-10-03_033503` (live `game_0.db` byte-identical; SHA-256 `1FD6089F9A52E74A29FCB907225AD9D491F4B984BFE6562C252DFEA9F8264793`).
- `quick_check` and `integrity_check` = ok. ITQoL mailbox = 1, ITQoL controller = 1, Ancient Realms controller = 1, no duplicate controllers.
- 7 mods in the original order; `ThrallDamageToNPCsMultiplier=0.300000`.
- The orphaned `findstr` PID 28280 was **not** terminated: its CommandLine is unreadable and it predates the study, so it could not be confirmed as part of it.
- Batch D: not started.

## Integration branch `claude/m3-task4-integration` (2026-10-03)

- **Base:** `claude/m3-task4-live-windows` @ `7455e7d`.
- **Frozen sources, not modified:** `wip/mod-warning-classification` (`23007b7`) and `wip/shutdown-timing` (`d952d6d`). Both were committed unverified during a hand-off.

| Commit | Content |
| --- | --- |
| `61d6b50` | Exact validated mod warning gates (ported from `23007b7`, then built, tested and checked on real data). ITQoL (SHA-256 `F35D9D92…`): exact mailbox line, exact 23 LoadErrors, exactly 1 mailbox and 1 controller. Ancient Realms (SHA-256 `12F7E719…`): exact 37 LoadErrors (7 named + 30 "None"), exactly 1 controller. Everything else fails: a changed hash, set drift, unattributed LoadErrors, a missing/duplicate singleton, any crash/assertion/persistence error line (`SevereLogMarkers`), a world `quick_check` other than ok. New read-only `analyze-boot <log> <backupId>`. |
| `29c74e5` | Shutdown classes: NORMAL < 240 s, WARNING 240–300 s (both allow the next batch; never a mod compatibility failure), DEGRADED 300–600 s (blocks the next batch until reviewed), EMERGENCY ≥ 600 s. Stop behaviour unchanged from `26091fa`. |
| `dcf19db` | Live harness `set-mods` (controlled mod sets for staging tests; harness only) with tested input rules (`ModSetSelection`). |

**Validation**
- `dotnet build -c Release --no-incremental`: 0 warnings, 0 errors.
- `dotnet test -c Release`: **398 / 398**, 0 skipped.

**Read-only 7-mod check** (no server start): `analyze-boot` on the control boot log `ConanSandbox-backup-2026.10.02-20.34.59.log` (03:28:53) against backup `2026-10-03_033503`:
- Mount sequence = modlist (7 mods). ModRelatedProblems = none.
- Known non-blocking: `ITQOL-MAILBOX-HEALTHPOOL` ×1 and `ANCIENT-REALMS-DANGLING-REF-1..7` ×1 each.
- LoadErrors: ITQoL 23 = validated set (exact); Ancient Realms 37 = validated set (exact); unattributed none.
- `quick_check` ok. ITQoL mailbox 1, ITQoL controller 1, Ancient Realms controller 1 (all PASS); no duplicate controllers.

Batch D: not started.
