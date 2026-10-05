# Mod #14 — Dev Kit checklists (prepared 2026-10-05, NOT RUN)

Nothing here has been run. The Dev Kit (AppVersion 377800) is still installing. Each step lists what to record and when to stop. **No Unreal asset is created before Part A passes**, except the minimal probe mod in A4.

Rules for every step:
- The Dev Kit is used read-only against game content. **Never edit or resave a Funcom asset.**
- Evidence goes under `evidence/` as text exports or screenshots, with the date.
- The staging server is touched **only** after the operator hands over control. That means one controlling session, server offline, no orphan processes, and a fresh verified cold backup.
- A broken chain, an ambiguity or an unexpected result is **recorded and reported, never guessed around.**

## Part A — Compatibility gate (must PASS before any campaign work)

| # | Step | Record | Stop if |
|---|---|---|---|
| A1 | The install finished | Launcher manifest no longer `bIsIncompleteInstall`; AppVersion; install path (`D:\epic\CEUE5Devkit`); size on disk | Install failed or incomplete |
| A2 | Dev Kit build identity | The editor's About dialog (engine version + branch + changelist). `Engine/Build/Build.version` (Major/Minor/Patch, Changelist, BranchName). The project file's engine association. | The identity can't be read |
| A3 | Compare with the target | Dev Kit engine version, branch and CL vs the server's `5.8.2-378132 (++exiles+release-beta)`. Write the exact difference down. | — (a mismatch is expected; A6 decides) |
| A4 | Minimal probe mod | Build `MQ14_CompatProbe` with the Dev Kit's **official** mod packaging (BuildMod), for the client and WindowsServer targets. Contents: one server-side mod controller whose BeginPlay writes a single log line, `MQ14 compat probe loaded`. Nothing else: no DataTables, no UI, no persistence. | Packaging fails |
| A5 | Inspect the package | The outer `.pak` layout vs the 13 accepted mods (inner `<Mod>-WindowsServer.pak/.utoc/.ucas` plus the client payload). The embedded modinfo: `devkitRevisionNumber`, `minimumVersion`, the other fields. SHA-256 and size. | The layout differs in an unexplained way. **Never edit metadata to match.** |
| A6 | Server acceptance (staging, operator handover) | 1. Pre-checks: server offline, no orphans, fresh verified cold backup. <br>2. Install the probe through the Local Mod pipeline, appended after the 13. <br>3. Boot and confirm: the probe mounts exactly once; its log line appears; its controller registers once. <br>4. No new LoadErrors outside the 13-mod baseline; nothing unknown is accepted without review. <br>5. NORMAL shutdown, exit 0, no orphan; `quick_check` ok; no duplicate objects. | The server refuses the mod or reports a Dev Kit/build mismatch → **STOP and report**. Any crash, integrity failure or unclassified error → STOP. |
| A7 | Back to baseline | Remove the probe through the pipeline. Boot the 13-mod pack, which must match the accepted 2.2.3 baseline exactly. Verify the backup. | The baseline doesn't come back clean |

**Gate result:** PASS only if A1–A7 all pass. The probe never goes to production.

## Part B — Quest 01: Abysmal Remnant identity trace

**Current candidate:** `BP_NPC_Wildlife_SewerAbomination`. **Status: Unverified.** Not accepted on its name alone.

**Starting points:** the 2.2.3 asset paths in [`evidence/dregs-asset-paths-2.2.3.txt`](evidence/dregs-asset-paths-2.2.3.txt), read from the server's `.utoc` indexes. These are names only.

| # | Link | Where to look in the Dev Kit | Record | Pass when |
|---|---|---|---|---|
| B1 | **The Dregs** level | `/Game/Maps/ConanSandbox/Gameplay/Gameplay_Dungeon_Sewer` and `…/Gameplay_Dungeon_Sewer_Blackout`, plus `Sewer_EntranceExits` | Which gameplay sublevel the 2.2.3 Exiled Lands map actually streams (the original or `_Blackout`), and why | Exactly one active sublevel identified |
| B2 | **Dungeon/encounter controller** | Placed instances of `/Game/Systems/EncounterControllers/BP_DarkDregsDungeonController` in that sublevel, plus any other controllers placed there | Instance name and location, and what it activates (spawners, spawn requests, doors, buff volumes such as `BP_AC_Buff_AcidBath_DarkDregsBossFight`) | The controller → boss-room link is explicit |
| B3 | **Spawn request / spawner** | What the controller (or the boss room) uses to spawn the boss. `BP_BossDarkDregs_Nahjef_SpawnRequest` belongs to **Nahjef** (`BP_BossDarkDregsNahjef_Controller`), so find the Remnant's own request or placed spawner. | The exact spawner/request asset and its configured class or DataTable row | One spawn source for the Remnant |
| B4 | **Spawned NPC class** | The class the spawn source actually instantiates | Full object path, e.g. `/Game/Characters/NPCs/sewer_abomination/Blueprints/BP_NPC_Wildlife_SewerAbomination.BP_NPC_Wildlife_SewerAbomination_C`, **or** a child class or data-driven variant | An exact class path, not a folder or name match |
| B5 | **Inheritance and uniqueness** | Class Settings → parent chain down to the native base. Reference Viewer → **every** referencer of the class. | The full parent chain. Siblings: `BP_NPC_Wildlife_LavaWurm` shares the folder, so is it a child or a reskin? Every other place that spawns this class. | Known: either the class is unique to the Dregs boss, **or** Quest 01 also needs an encounter/location constraint (a spec change to record) |
| B6 | **Data and display-name mapping** | The NPC's data row (character/NPC DataTable) and the localization string table behind its name | Table, row name, localization key and the **exact** display text. The asset names use both "Abysmal" (VFX) and "Abyssal Remnant" (weapon meshes); record which one the NPC shows. | The display name traces to the B4 class with no gap |
| B7 | **Cross-check** | `BP_PL_W_Trophy_DarkDregs_Abomination`: the item that places it → the loot table that drops that item → the NPC whose loot uses that table. Also `P_sewerboss_acidtrail` / `large_slam_sewerboss_Cue` usage. | The chain from trophy to NPC | It agrees with B4 (if not: report the conflict) |
| B8 | **Death event source** | The server-side death path for the B4 class: the function that logs `KillCharacterWithRagdoll_Implementation`, plus any **supported** notification a mod can bind to without patching vanilla (a death delegate on the base character, a game-mode kill event, or the kill tracking that vanilla Journey steps use) | The hook's name and owner. What it provides: dead class, location, killer, thrall owner, damage participants. Whether it fires once per death. | A server-side hook that delivers every `BossDeathEvent` field |
| B9 | **Decision** | — | A summary of B1–B8 in `evidence/` | **Verified** only if B1→B6 is unbroken, B5 uniqueness is handled and B8 has a hook. Then set `targetClassPath`, `verification: Verified` and a `verificationNote` citing the evidence. Otherwise it stays **Unverified** and the blocker is reported. |

**Secondary confirmation**, later, with a licensed client on staging:
1. Make one kill.
2. Read the server log `Name:` / `CharacterName:` line.
3. It must equal B4 and B6.

A mismatch reopens B.

## Part C — UI entry point and input (after Part A)

| # | Step | Record |
|---|---|---|
| C1 | A supported Conan Enhanced menu/tab extension point | Is there a documented or Dev Kit-exposed way for a mod to add a menu tab or entry? A lead to **read**, never copy: Improved Thralls & QoL ships `WBP_ENHANCED_KeyBindings` / `WBP_ENHANCED_KeyBindButton`, so see how (and whether) it registers in the Enhanced UI. |
| C2 | Vanilla input bindings | The Dev Kit's input mapping contexts / input settings: every vanilla key binding. |
| C3 | Mod input bindings | Default keys of Improved Thralls & QoL's hotkey system (`E_HotkeyBindFunctions`, `Str_HotkeyBind`), Simple Minimap (`SM_IMC_UseMouseContext` / `SM_IA_UseMouse`), and anything else Part C2 shows. Update [`INPUT_CONFLICT_AUDIT.md`](INPUT_CONFLICT_AUDIT.md). |
| C4 | Choose | Menu entry if C1 offers a supported hook; otherwise the standalone panel on a configurable key from the audit's candidates. Confirm in a licensed client. |
