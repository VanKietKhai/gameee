# Dev Kit results — 2026-10-05 (main session, staging takeover)

All work below is read-only against Funcom content and third-party mods; mod paks were read from copies in the session scratchpad. No game, server or mod file was changed.

## A1–A3: Dev Kit identity (PASS for A1/A2; A3 recorded)

| Item | Value |
|---|---|
| Launcher manifest | "Conan Exiles Enhanced Dev Kit", AppVersion **377800**, `D:\epic\CEUE5Devkit`, `bIsIncompleteInstall=false`, 182,287,132,076 B |
| `Engine/Build/Build.version` | **5.8.2**, Changelist **377800**, CompatibleChangelist 377800, BranchName `++exiles+release`, licensee + promoted build |
| Launcher | `RunDevKit.bat` → `UnrealEditor.exe UE4\ConanSandbox.uproject -ModDevKit` |
| Project mod revision | `UE4/Config/DefaultGame.ini`: **`ModVersion=1002`** |
| Mod tooling | Plugin `DreamworldMods` (Funcom Oslo AS) + automation `UE4/Build/ModDevKit.Automation/BuildMod.cs` (cook → per-platform pak + IoStore for Windows, WindowsServer, LinuxServer → outer `<Mod>.pak` with `modinfo.json` + `manifest.json`) |
| Target server | `5.8.2-378132+++exiles+release-beta` |

**A3 difference:** same engine version (5.8.2); Dev Kit CL 377800 (`release`) vs server CL 378132 (`release-beta`), 332 changelists apart.

**Server-side compatibility rule (found):** the mod runtime has an explicit check (`GetModCompatibilityStatusFromRevisionSnapshot`, statuses Verified / OutOfDate / Incompatible / Unknown). The accepted 2.2.3 boots log `SetCompatibleDevkitVersions: set 1 version(s) from 1 input string(s): [1002]`. All accepted mods carry `devkitRevisionNumber 1002`, `devkitSnapshotId 0`, `minimumVersion Enhanced`. So a Dev Kit that writes revision 1002 matches what this server declares compatible; this is **confirmed only when A5 shows the probe's real modinfo** and A6 shows the server mounting it.

## C2/C3: input bindings

- **Vanilla** (`DefaultInput.ini`, legacy Action/Axis mappings; no Enhanced Input assets in vanilla content): keyboard keys used include E, R, Q, X, F, C, W/A/S/D, V, T, O, M, J, Z, Y, P, N, L, K, H, G, B, number row 1–8, Tab, Escape, Enter, BackSpace, SpaceBar, LeftShift/LeftControl/LeftAlt, arrows, PageUp/PageDown (message pages), **Insert (AdminPanel)**, **F2 (Feedback)**, NumLock, numpad +/−/×. **No F6, F7, F8 or F9.**
- **Improved Thralls & QoL** (client container decompressed from a copy): a "Hotkey Bindings" settings section with **19 bindable functions**: ThrallUseMelee, ThrallUseRange, ThrallUseTruncheon, ThrallUseHealing, AdminSettingsUI, UserSettingsUI, ThrallManagementUI, ThrallBehaviourToPassive/Defensive/Aggressive, InventoryBlacklistUI, ThrallWhistle, ThrallSendHome, FollowerUIScrollUP/DOWN, PartyUIScrollUP/DOWN, SearchInNearbyContainers. Players bind them in-game ("Press any key to bind / ESC to cancel"). Key names present in its packages: **F2** and **Enter** (player-controller component) and the text **"F9"** (settings view). Which function owns which default cannot be read from cooked data without a schema.
- **Result:** **F7 and F8** are unused by vanilla and by every binding observed in the 13 mods. Still not chosen; a licensed-client check with all 13 mods is required.

## C1: Conan Enhanced menu hook

- No supported "add a tab to the Enhanced menu" API was found. The game module exposes `UI/ModMenu/ModDetailsWidgetBase` and `/Game/UI/Widgets/Mods/WBP_ModMenu`, which is the **mod browser** (list/install mods), not an extension point.
- Improved Thralls & QoL's `WBP_ENHANCED_*` widgets are its own widget kit (buttons, checkboxes, item icons…), opened through its own hotkeys and radial menu — **not** an Enhanced-menu registration.
- Supported extension mechanisms that do exist: `ModController` ("Inherit from this class to have your own ModController", with a persistence component that saves/loads it) and mod/DLC component classes (`FDreamworldMods::RegisterModAndDLCComponentClasses`; e.g. ITQoL's `AC_…_FunCombatPlayerController`). Chest Labels adds a toggle to the radial menu, so the radial menu may be a second supported entry point (to inspect).
- **Decision per operator rule:** no supported Enhanced-menu tab hook → **fallback: standalone UMG panel on a configurable key** (radial-menu entry to be evaluated as an extra way in). No vanilla widget patching.

## B: Quest 01 trace (Dev Kit editor + asset files, read-only)

Method: the GUI Dev Kit editor ran a read-only Python script (`-ExecutePythonScript`, Python enabled by command line only; no asset saved). It exported the DataTable rows and the placed actors of both Dregs sublevels. Map name tables and the localization manifest were read from the uncooked `.umap`/`.uasset` files.

| Step | Finding | Status |
|---|---|---|
| B1 Dregs level | **Two separate dungeons, about 990 m apart.** `Gameplay_Dungeon_Sewer` = **The Dregs** (boss-room spawner at x≈−137,112, y≈375,791, z≈−21,557). `Gameplay_Dungeon_Sewer_Blackout` = the **Darkened Dregs** (Nahjef boss room at x≈−38,251, y≈371,968). Nahjef does not replace the Remnant; they are different encounters. Vanilla Journey: "The Dregs dungeon can be found on the western end of the river in the south." | resolved (static); runtime confirmation pending |
| B2 Controller | The Dregs places `BP_DungeonController` (class `BP_JhabbalSagDungeonController_C`, empty `BossController`). The Darkened Dregs places `BP_DarkDregsDungeonController` → `BossController` = `BP_BossDarkDregsNahjef_Controller`. The Remnant is **not** behind an encounter controller. | resolved |
| B3 Spawn source | The Dregs boss room: `NPCTerritorySpawner` **`D_S_SewerBoss1_1`** + `BP_ManualSpawnPoint` **`D_S_SewerBoss_ManualSpawnPoint1_1`** + `BP_CampOwner` **`D_S_SewerBoss_CampOwner1`**, territory `Territories/Dungeons/Sewer/BossRoom`. The level references the spawn row **`Wildlife_SewerAbomination`** (also a row in `WeightedSpawnTableRow`). Among Maps/Systems/DLC, **only** `Gameplay_Dungeon_Sewer` references that row. | resolved (row on spawner component not yet read property-by-property) |
| B4 Class | `SpawnDataTable` row `Wildlife_SewerAbomination` → `NPCClass` = **`/Game/Characters/NPCs/sewer_abomination/blueprints/BP_NPC_Wildlife_SewerAbomination.BP_NPC_Wildlife_SewerAbomination_C`**, `StatTemplate` "Animal No Knockback", `NpcTags` **`Npc.Boss`, `Npc.Dungeon`, `Npc.Dungeon.Dregs`**. | resolved |
| B5 Inheritance / uniqueness | `BP_NPC_Wildlife_SewerAbomination_C` → `/Game/Characters/BaseBPWildlife.BaseBPWildlife_C` → native `/Script/ConanSandbox.ConanCharacter`. It is the only `SpawnDataTable` row with this class. `BP_NPC_Wildlife_LavaWurm` (same folder) is a different class, row `Wildlife_LavaWorm`, tag `Npc.Boss.Miniboss`. Note: the path string uses lowercase `blueprints` in the DataTable and `Blueprints` on disk; the runtime class path must be taken from the server log (case-sensitive compare in the engine spec). | resolved (static) |
| B6 Display name | `SpawnDataTable.Wildlife_SewerAbomination.Name` = **"Abyssal Remnant"**. Journey `DT_ExilesJourney.DestroyAbyssalRemnant` ("Destroy the Abysmal Remnant", Chapter V, `MapRestriction` ConanSandbox). Items and trophy say "Abysmal Remnant". **The nameplate the server logs as `CharacterName` should be "Abyssal Remnant".** | resolved |
| B7 Cross-check | The class references `ACH_DefeatSewerAbomination` and Journey row `DestroyAbyssalRemnant`; battle pass `DUNGEON_DREGS` = "Defeat the Abyssal Remnant". | agrees |
| B8 Death source | Vanilla credits the Journey step from this class. Mod-side hook not chosen yet (candidates: death event on the character via a mod component, or the existing kill path that logs `KillCharacterWithRagdoll_Implementation`). | OPEN |

**Status: strong static evidence; still `Unverified`.** Needed before `Verified`: B8 hook, and secondary runtime confirmation (one staged kill, server log `Name:`/`CharacterName:` = the B4 class and "Abyssal Remnant").

**Spec impact:** the quest's target display name should be "Abyssal Remnant" (what players see on the boss); "Abysmal Remnant" is the item/journey spelling. Operator to confirm which spelling the UI shows.

## Tooling incident (Dev Kit)

`UnrealEditor-Cmd.exe … -run=pythonscript` crashed twice with **`EXCEPTION_STACK_OVERFLOW` in `UnrealEditor-AssetRegistry.dll`** about 8 minutes after start, during the commandlet's own asset-registry scan (before any script ran; no asset written). No junctions/symlink loops; max content depth 14. The Dev Kit binaries were not modified. The **GUI editor works**: first start 26 min (14,111 shaders compiled locally; shared DDC unreachable), asset gather ~129 s CPU, no overflow; second start 7.5 min. The read-only trace above ran there. Raw extract: [`quest01-trace-extract-2026-10-05.json`](quest01-trace-extract-2026-10-05.json).
Risk still open: `BuildMod -Cook` runs a commandlet and may hit the same overflow (retest in progress).

## A4–A7: compatibility probe on 2.2.3 / CL-378132 — **FAIL (STOP)** (2026-10-06)

| Step | Result |
|---|---|
| A4 build | Mod `MQ14CompatProbe` was created in the Dev Kit UI. Its only content is `BP_MQ14CompatProbeController`, an empty child of `/Script/DreamworldMods.ModController`. Built with the official `RunUAT BuildMod -Cook -Pak -Compress -FinalPak` (the same command the "Build mod" button runs; the cook commandlet now passes its registry scan): BUILD SUCCESSFUL, 9.3 min. |
| A5 inspect | `MQ14CompatProbe.pak` 271,512 B, SHA-256 `051c7543…6876`. Layout identical to accepted mods: Windows/WindowsServer/LinuxServer pak+ucas+utoc, plus `manifest.json` and `modinfo.json`. Embedded modinfo: `devkitRevisionNumber 1002`, `devkitSnapshotId 0`, `minimumVersion Enhanced`. The Dev Kit itself wrote 1002 at Build time (a new mod starts at 0). **No metadata edited.** **PASS** |
| A6 pre-checks | Quiet host, no `release-hold`, no WAL. Pre-probe cold backup **`2026-10-06_020925`**: hashes, SQLite and manifest verified, live world unchanged. Probe imported as #14 through the Local mod pipeline (source = installed SHA, load order 14, 13 mods unchanged, world unchanged). |
| A6 boot `mq14-probe-1` | The server mounted the probe and logged `Persistence: Spawning mod controller: BP_MQ14CompatProbeController_C` after the 32 existing controllers, then `MatchStarting`. **The log then stopped after the 2nd NavData warning (02:10:32), during world load.** The baseline continues for ~15 s and reaches readiness. No crash dump. No readiness; the harness **force-stopped** the process tree after ~10 min (exit 1). The forced kill left a non-empty WAL (766,352 B). **FAIL — unexplained hang** |
| Evidence | `E:\CSC-M3-Live\live-test\mq14-probe\evidence-forced-kill\` (read-only): server log, `game_0.db`, `-wal`, `-shm`. Forced-kill world also kept as backup `2026-10-06_022151`. Harness outputs `01`–`06` are in the same folder. |
| A7 recovery | `remove-local MQ14CompatProbe.pak`: PASS, 13 mods exact. `restore 2026-10-06_020925`: PASS; world equals the pre-probe backup (`43a2c2d1…`), WAL 0. |
| A7 control boot `mq14-baseline-after-probe` (13 mods, no probe) | Readiness OK; load analysis PASS, with only the exact known LoadErrors (ITQoL 23, Ancient Realms 37, Simple Minimap 1) and no unattributed ones; graceful RCON stop NORMAL 189.3 s, exit 0, no orphan; quick_check PASS; ITQoL mailbox/controller and Ancient Realms controller gates PASS. **Environment healthy.** |

**Conclusion:** the hang appears **only with the probe** (one run each, same host and session conditions; 6.1 GB RAM free before the control boot). The most likely cause is the Dev Kit/build gap: a ModController child cooked by Dev Kit CL-377800 (`++exiles+release`) spawning on server CL-378132 (`++exiles+release-beta`). This is not proven. Per the operator rule, **STOP: Quest 01 is not started.**

Possible next steps (operator decision):
1. Re-run the probe once more to rule out a one-off hang (same pre-probe backup and restore procedure).
2. Wait for a Dev Kit matching the 2.2.3 beta, or move staging to the live release that matches Dev Kit 377800.
3. Probe variant without a ModController (e.g., a single DataTable), to isolate whether the hang is specific to the controller spawn.

Stale files to note: `Saved\ExtractedMods\MQ14CompatProbe-WindowsServer.*` may remain (like the earlier WickProbe). They are not mounted without the pak.

## Differential probe isolation (operator plan, 2026-10-06)

Staging moved to the operator's new licensed Steam install: server `D:\steamnew\steamapps\common\Conan Exiles Dedicated Server` (CL-378132, same build). It was rebuilt through the harness (RCON config → vanilla boot → restore 2026-10-06_020925 → 13 × replace-local) and validated: `steamnew-13mod-validation-2` PASS, NORMAL 180.9 s. The first validation run showed one vanilla `Exile_Priest_4_Nordheimer` spawn-table line and a DEGRADED 394 s stop; neither repeated, and nothing was whitelisted.

FAILED CONTROLLER PROBE #1 evidence is labelled and preserved (`live-test/mq14-probe/evidence-forced-kill`, backup 2026-10-06_022151). Do not rerun it before isolation is complete.

### PROBE A — DATA ONLY: **PASS**

| Item | Value |
|---|---|
| Package | `MQ14ProbeA.pak` 38,297 B, SHA-256 `525e94e1…e818`; built via the Dev Kit GUI "Build mod" |
| Content | One `CurveFloat` (`MQ14ProbeA_InertCurve`), no dependencies; no controller, Blueprint, hook, persistence, UI or NPC reference |
| Metadata | `devkitRevisionNumber 1002` (written by the Dev Kit), snapshot 0, `minimumVersion Enhanced`, no Workshop ID; standard 11-file layout |
| Pre-probe backup | `2026-10-06_165631` (verified) |
| Readiness | 36.3 s (`mq14-probeA-1`), mounted as #14, asset registry loaded |
| Unknowns | none (load analysis PASS, current and complete log; no unattributed LoadErrors) |
| Shutdown | NORMAL 179.3 s, exit 0, no forced kill, no orphan |
| Integrity | quick_check PASS; ITQoL mailbox/controller and Ancient Realms controller gates PASS |
| Rollback | probe removed, `2026-10-06_165631` restored, exact 13-mod baseline |
| Baseline after | `mq14-baseline-after-probeA` PASS, NORMAL 167.7 s |

**Meaning:** the Dev Kit 377800 package and toolchain (and a missing Workshop ID) are accepted by CL-378132. The failing layer of probe #1 is controller-related (B/C/D), not generic package compatibility.

### PROBE B — EMPTY MODCONTROLLER: **PASS**

| Item | Value |
|---|---|
| Definition (before packaging) | `tools/devkit/probeB_definition.json` (`probeB_doc.py`, read-only). `BP_MQ14ProbeBController`, parent `/Script/DreamworldMods.ModController`; only inherited components (`Sprite` BillboardComponent, `PersistenceComponent` ActorPersistenceComponent); no own components; CDO replication flags equal the base CDO (replicates, always relevant, net load on client). No graph logic added. |
| Package | `MQ14ProbeB.pak` 271,245 B, SHA-256 `0E63F87FB16B49AC495703A5A29C67C1E1BF977893C8D14AD3ACDF582D34AF29`; built 2026-10-06 22:49–23:09 with the Dev Kit GUI "Build mod" (operated through Computer use) |
| Metadata | `devkitRevisionNumber 1002` (written by the Dev Kit), snapshot 0, `minimumVersion Enhanced`, no Workshop ID; standard 11-file layout. Not edited. |
| Pre-probe backup | `2026-10-06_232037` (verified) |
| Boot `mq14-probeB-1` | Mounted as #14 (order 1013). `AddActiveModControllerClass … BP_MQ14ProbeBController_C`, then `Persistence: Spawning mod controller: BP_MQ14ProbeBController_C`. **Readiness 42.2 s** (port bound, world ticking). Hold 180.7 s. |
| Unknowns | none (load analysis PASS on the current and complete log; no unattributed LoadErrors). The 35 `FPackageName … skipped package None` lines equal Probe A's count (base-game noise). |
| Shutdown | RCON graceful, acknowledged; **NORMAL 183.2 s**, exit 0, no forced kill, no orphan |
| Integrity | quick_check PASS; ITQoL mailbox/controller and Ancient Realms controller gates PASS |
| Rollback | probe removed, `2026-10-06_232037` restored (WAL 0), exact 13-mod baseline |

**Meaning:** an empty ModController child built by Dev Kit 377800 spawns and runs on CL-378132. So the probe #1 hang was not caused by the controller class or its spawn alone. Probe #1 had the same content type and nearly the same size (271,512 B); it ran on the old server folder. Next isolation step: Probe C (Probe B + exactly one BeginPlay log node).

### PROBE C — MODCONTROLLER + ONE BEGINPLAY LOG NODE: **PASS**

| Item | Value |
|---|---|
| Definition | `tools/devkit/probeC_definition.json`. `BP_MQ14ProbeCController` (parent `/Script/DreamworldMods.ModController`), created by `probeC_create.py` exactly like Probe B, then `probeC_logic.py` added exactly one node: `Event BeginPlay → KismetSystemLibrary::PrintString("MQ14ProbeC BeginPlay", PrintToScreen=false, PrintToLog=true)`. The default `Event Tick` ghost node stays unconnected. No variables. Compile: 0 errors, 0 warnings. Graph built with the Dev Kit's editor Python API (`BlueprintGraphEditor`), which is the editor's own Blueprint authoring path. |
| Package | `MQ14ProbeC.pak` 272,817 B, SHA-256 `32670603922D10F282B9336F956EDC590E4A61EF818E600AD13449B9115F7963`; GUI "Build mod" 2026-10-06 23:56–00:06 |
| Metadata | `devkitRevisionNumber 1002` (Dev Kit), snapshot 0, `minimumVersion Enhanced`; standard 11-file layout. Not edited. |
| Pre-probe backup | `2026-10-07_000719` (verified) |
| Boot `mq14-probeC-1` | Controller spawned once, then `LogBlueprintUserMessages: [BP_MQ14ProbeCController_C_…] MQ14ProbeC BeginPlay` (20 s later, during world load). Readiness 43.1 s. Hold 181.4 s. |
| Unknowns | none (current and complete log); 35 `FPackageName` noise lines, same as A and B |
| Shutdown | NORMAL 217.1 s, exit 0, no forced kill, no orphan (the idle Dev Kit editor stayed open during this run) |
| Integrity | quick_check PASS; ITQoL and Ancient Realms gates PASS |
| Rollback | probe removed, `2026-10-07_000719` restored, exact 13-mod baseline |

**Meaning:** Blueprint logic in a Dev Kit 377800 ModController executes on CL-378132. **Compatibility gate A4–A7 is PASS** through the differential probes A (data), B (empty controller) and C (controller + logic). FAILED CONTROLLER PROBE #1 stays an unexplained one-off on the old server folder; it is kept as evidence and not rerun.

## MOD #14 QUEST 01 VERTICAL SLICE — staging boot PASS, installed (2026-10-07)

| Item | Value |
|---|---|
| Mod | `MQ14MainQuest` (created with the Dev Kit "Create a new mod"); controller `BP_MQ14MainQuestController` built by `tools/devkit/mq14_build_controller.py` (headless `-ExecutePythonScript`), definition `tools/devkit/mq14_controller_definition.json`; `MQ01_CompletedIds` SaveGame flag set in the Blueprint editor (CPF_SaveGame) |
| Behaviour | Server: every 10 s, scans ConanCharacter and binds `SignalOnKilled` on actors whose exact class path is `/Game/Characters/NPCs/sewer_abomination/blueprints/BP_NPC_Wildlife_SewerAbomination.BP_NPC_Wildlife_SewerAbomination_C`; on death inside 120 m of `D_S_SewerBoss1` credits every player pawn within 50 m once per character StableId, sets the persistence dirty flag, sends `ClientHUDShowNotification`; publishes completed PlayerStates (replicated). Client: F7 → `ClientShowRichMessageBox` (Vietnamese Main Quest panel). |
| First build | GUI Build mod: cook **failed** (exit 3, "cook stalled … garbage collection") because a hard class reference pulled the boss Blueprint into the cook. Fixed by matching the class path string instead. |
| Package | `RunUAT BuildMod -Cook -Pak -Compress -FinalPak` (the commands the button runs) with the editor closed, 7.5 min: `MQ14MainQuest.pak` 285,283 B, SHA-256 `585F52AAAE44ED0EC3C51DCDDA10F77BB83BCD079A97657357710770A0EE1C1D`, `devkitRevisionNumber 1002` (written into modinfo.json by the Dev Kit's own SaveModInfo during the GUI build; not hand-edited), 11 files |
| Staging boot `mq14-q01-1` | backup `2026-10-07_090633`; mounted #14; `Persistence: Spawning mod controller: BP_MQ14MainQuestController_C`; `MQ14 MainQuest controller started (server); tracking MQ01 Abysmal Remnant`; readiness 35.4 s; 0 unattributed LoadErrors; no script errors during 180 s (≈18 scans); NORMAL 164.3 s; integrity gates PASS; rolled back |
| Baseline after | `mq14-baseline-after-q01` PASS, NORMAL 174.4 s |
| Installed | Staging server: backup `2026-10-07_092110` verified, `import-local` → 14 mods (SHA match). Client `D:\steamnew\...\Conan Exiles\ConanSandbox\Mods`: pak copied, modlist appended (backup `modlist.txt.bak-before-mq14-20261007`); client and server modlists identical. |
| NOT yet verified | A real Abysmal Remnant kill (credit, banner, F7 panel, persistence after restart). Target stays Unverified in the data until that runtime check. |

Comparison with accepted controllers (metadata only, no decompiling): Ancient Realms' `AR_BP_ModController` has nearly the same name map as probe #1 (PersistenceComponent, DefaultSceneRoot, SimpleConstructionScript, ModDataTableOperations). Chest Labels and Simple Minimap add `AdditionalClassComponents`. Every accepted mod has the same inner layout (`AssetRegistry.bin` + `ModCompat.bin`).

## QUEST 01 RUNTIME VERIFICATION — PASS (2026-10-07, Conan 3.0.0 CL-378787, licensed client)

| Check | Evidence |
|---|---|
| Join | `Join succeeded: Khari#73998` (04:14 UTC) |
| Boss identity (B4/B6 runtime) | `KillCharacterWithRagdoll_Implementation. KillerNameInput: khari CauseOfDeath: Poison. IsThrall: 0 Name: BP_NPC_Wildlife_SewerAbomination_C_2147353117 CharacterName: Abyssal Remnant` |
| Credit | `MQ14 MQ01 boss death in The Dregs; crediting nearby players` → `MQ14 MQ01 COMPLETE character=198` (no killing blow needed: death by poison) |
| Client | Operator confirmed the HUD banner and F7 showing completion |
| Persistence | Server restarted through the desktop app (11:33): `Persistence: Loading mod controller: BP_MQ14MainQuestController_C`; after rejoining (04:36 UTC) F7 still shows completion (operator) |
| Data | MQ01 target set to `Verified` with the exact class path; display name corrected to the in-game "Abyssal Remnant" |

Other login attempts in the same session (one other Steam account, two `NULL:<INVALID>` ids) failed PreLogin because they lacked the server's 14-mod set; not related to Mod #14.
