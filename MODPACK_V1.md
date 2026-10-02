# Target Modpack V1 — private Radmin Conan server

Status: **IN PROGRESS. Batch A PASS** (2026-10-02, `2aca0cf`; details in `M3_LIVE_TEST_REPORT.md` "Modpack V1 Batch A").
- Installed on `depot_443031`: `StackMe10K.pak | SavageParagon.pak | GritandGrease.pak`, in that order.
- Final verified cold backup `2026-10-02_074354`, `quick_check` = ok.
- Runtime mount order and IoStore container `Order` follow `modlist.txt` (first entry 1000, then +1): **PROVEN**. Which mod wins an asset that two mods override was not exercised.
- Next: Batch B.

Operator decisions (2026-10-02):
- WickProbe / WickStacks is **not** part of the modpack. Mod #10 is **StackMe10K**.
- Batches are **cumulative**: A stays installed during B, A+B during C, and so on until the full set (now fifteen) is tested together.
- Batch A also performs the multi-mod / load-order checkpoint (4E.2).
- While StackMe10K is installed, the Improved Thralls & QoL Stack Size Multiplier stays **OFF**.

Operator decisions (2026-10-02, campaign):
- **"Twelve Legends" is removed from the plan.** Nothing of it existed on this host, so nothing was deleted.
- The server uses an admin-configured PvE campaign instead: see `CAMPAIGN_V1.md`. No Conan DevKit project.
- **Five mods are added** (#11–#15): Tot ! Enhanced Sudo and Thrall Wars Utilities (quest infrastructure); Night Terrors, PvE Plus Ambush and Thrall Wars Dungeon Mod (content).
- They are added to the ten, not replacing any. The modpack is now **fifteen** mods, tested in new cumulative batches E, F1 and F2.

- **Final load order: NOT DECLARED.** It is declared only after all fifteen mods have been tested together and the runtime evidence has been reviewed. The proposed starting order and the author constraints are in `CAMPAIGN_V1.md` section 3.
- **Deployment:** private friends-only dedicated server over Radmin VPN (`D:\conan exiles\depot_443031`, CL-377096).
  - This server is **STAGING / PRE-PRODUCTION** with a test/validation world.
  - The production save is not created yet; see `STATUS.md` "Deployment Model" for the creation criteria.
- **Install path:** Local `.pak` files only, through the production Local Mod pipeline. No Workshop, no SteamCMD, client untouched, no public registration.

Sources (read-only, 2026-10-02):
- Steam Web API `GetPublishedFileDetails` (public data; all nine items returned `result=1`).
- The raw HTML of each Workshop page, checked for a "Required items" block.
- Workshop descriptions. These are the authors' claims and are not verified here.
- A local, read-only inspection of the stack-mod archives (StackMe10K, WickStacks).

Workshop pages contain hidden template notices ("incompatible with Conan Exiles Enhanced", "removed from the community"). Their style is `display: none`, so they do not apply to these items. One automated page summary misreported them; they are ignored here.

## Mods

| # | Mod | Source / ID | Updated | Item size | Required items | Batch | Live-tested |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Thrall Reputation | Workshop `3787066846` | 2026-09-15 | 833,760 B | none | B | no |
| 2 | Savage Paragon | Workshop `3766043945` | 2026-09-23 | 4,760,799 B | none | A | Batch A PASS (server-side) |
| 3 | Ancient Realms Enhanced (Work In Progress) | Workshop `3755775098` | 2026-09-15 | 496,900,005 B | none | C | no |
| 4 | Improved Thralls & QoL | Workshop `3758661389` | 2026-09-24 | 154,632,086 B | none | B | no |
| 5 | Fantasy Races Of Exiles | Workshop `3780741325` | 2026-09-15 | 5,293,057 B | none | D1 | no |
| 6 | [Enhanced] WO - Riding Thralls | Workshop `3803149679` | 2026-09-25 | 78,896,983 B | none | B | no |
| 7 | Shemite City State: Enhanced (v2.1) | Workshop `3755371705` | 2026-09-16 | 2,098,034,644 B | none | D3 | no |
| 8 | Cannibal Captivity v0.0.16 (Enhanced) | Workshop `3765743138` | 2026-09-29 | 112,432,880 B | none | D2 | no |
| 9 | Grit & Grease (Weapon Infusions) | Workshop `3801774752` | 2026-09-20 | 68,049,336 B | none | A | Batch A PASS (server-side) |
| 10 | **StackMe10K** (replaces WickStacks, 2026-10-02) | Nexus, Conan Exiles Enhanced mod 3 | archive 2026-09-17 | 4,641,754 B (`StackMe10K.pak`) | unknown | A | Batch A PASS (server-side; 10,000 stacks not verified in-game) |
| 11 | **Tot ! Enhanced Sudo 1.3.78** | Workshop `3721090132` | 2026-09-20 | 55,825,374 B | none | E | no |
| 12 | **Thrall Wars Utilities** | Workshop `3721224296` | 2026-09-16 | 14,191,742 B | **Tot ! Enhanced Sudo (3721090132)** | E | no |
| 13 | **Night Terrors** | Workshop `3723538551` | 2026-09-15 | 10,219,405 B | none | F1 | no |
| 14 | **PvE Plus Ambush (Enhanced) - v1.0.5** | Workshop `3721274811` | 2026-09-15 | 5,145,777 B | none | F1 | no |
| 15 | **Thrall Wars Dungeon Mod** | Workshop `3722829382` | 2026-09-27 | 466,505,066 B | not visible (mature-content gate); the description lists none | F2 | no |

Items 11–15 were checked with the same Steam Web API call on 2026-10-02: all returned `result=1`, none banned. Their hidden "incompatible / removed" page notices are the same `display: none` templates as above.

The Workshop item sizes total about 3.02 GB for #1–#9, and about 3.57 GB with #11–#15. When a local `.pak` arrives, its size is compared with the Workshop size as a version hint. A mismatch is recorded, not treated as a failure.

### Per-mod notes

1. **Thrall Reputation**
   - Adds a follower party-list window (up to 5 followers).
   - Followers gain reputation while following the player, which grants a "Friends with Benefits" buff.
   - No configuration yet; the author lists it as a future improvement.
   - Author warning: mods that change the thrall stats window (level, health and XP bar) may conflict.
2. **Savage Paragon**
   - A Paragon track after level 60, saved per character on the server.
   - Several follower perks: attack speed, XP, damage sharing, HP regeneration, damage reduction.
   - Admin console commands are in a pinned guide.
   - The author calls multiplayer "beta" (tested in-editor) and asks for dedicated-server feedback.
   - Load-order note from the author: **load after any XP mod**.
3. **Ancient Realms Enhanced (WIP)**
   - Building pieces for large stone settlements.
   - The author says pieces may be reworked, replaced or removed between updates, and strongly recommends backups.
4. **Improved Thralls & QoL**
   - The author states **"Everything is disabled by default!"**
   - Settings: admin `DataCmd ImprovedThrallsQoL`, user `DataCmd ImprovedThrallsQoLUser` (in-game).
   - Relevant features: Stack Size Multiplier; Ignore Stack Size Multiplier in selected containers; Container Space/Slot Multiplier; Additional Follower Count; individual thrall base stat modifiers; global pet and golem stat modifiers; global weapon and armor base stat modifiers; follower party system and custom UI; pick up, summon and send followers home; item and feat bans.
5. **Fantasy Races Of Exiles**
   - Changes NPC spawn tables and adds race crafters and dancers.
   - Admin settings window `dc FROESettings` (server-wide): global spawn off, plus a region blacklist (`R01, R02, …`).
   - Author warning: may conflict with mods that edit the same spawn tables.
6. **Riding Thralls**
   - Followers use mounts and can ride as passengers.
   - Per-player mode, set at a craftable "Registrar" placeable.
   - Tested by the author on a 2.2 dedicated server.
   - May conflict with mods that heavily modify followers, mounts or passengers. Known multiplayer visual desync caveats.
7. **Shemite City State**
   - A **map mod on the base Exiled Lands**, adding a walled city, dungeon, learnable building set, statues and NPCs.
   - Modifies `Heightmap_x1_y4`, `Gameplay_NPCs_Desert_West`, `Wildlife_split081`, `Camps-NPC_x1_y3-2`, `Cinematic_x1_y4` and `Camps-NPC_x1_y4-2`.
   - Not compatible with other map mods that change the same heightmaps.
   - The author warns about performance on lower-end hardware. It is the largest item (2.1 GB).
8. **Cannibal Captivity**
   - A destructible cannibal camp east of the collapsed bridge (3F), with thrall cages and treasure.
   - **Risk:** a Workshop comment reported game crashes since the 2026-09-15 game update. The author re-cooked the mod on 2026-09-29 (the current version).
9. **Grit & Grease**
   - Weapon resin infusions that add elemental damage as a percentage of weapon damage.
   - Feats in the survival tab; crafted at the alchemy bench (level 10+).
   - The author says it is designed for dedicated servers.
10. **StackMe10K** (the operator's choice on 2026-10-02, replacing WickStacks)
   - Local file: `C:\Users\vkkha\Downloads\mod conan\StackMe10K.pak`.
     - 4,641,754 B, SHA-256 `30F5DF542826145FC4B1619296DD135A1370A13CFFBF66DF83E317F52885C8A0`, CRC32 `11f33854`.
     - Valid Unreal pak, version 12.
   - Byte-identical (CRC32) to the only file in `StackMe10K 3 1 2026-09-17T12-27Z 5Mov28s9h.zip` (Nexus mod 3, file 1).
   - Enhanced container with `StackMe10K-WindowsServer`, `-LinuxServer` and `-Windows` (client) sub-paks. Internal paths `/Game/Mods/StackMe10K/`, `StackMe10K_Modcontroller`, and an `ItemTable` reference.
   - The Nexus page blocks automated fetches (HTTP 403). A search snippet describes it as "max stack size to 10,000 for 2,000+ items"; this is unverified.
   - **10,000 stack behaviour: IN-GAME BEHAVIOR NOT YET VERIFIED.** Server logs are not evidence of stack sizes: the earlier stack mod's 4E boot log had no ItemTable or stack lines. Server-side tests can show only that the mod mounts and loads. Verifying the behaviour needs an in-game check by an authenticated client.
   - **Why WickStacks was dropped:** its archive `WickStacks 48 1 2026-09-19T22-02Z rcRAMvI71.zip` contains only `WickProbe.pak`. That file is byte-identical to the file 4E tested alone (SHA-256 `D7FE0EC0…D79C`, internal name `WickProbe`; assets `BP_WickProbeController`, `DT_WickStackControl`, `DT_WickStackPatch`, `ItemTable`). WickProbe is no longer part of V1. Its stale `Saved\ExtractedMods\WickProbe-WindowsServer.*` files stay as observed technical debt.
11. **Tot ! Enhanced Sudo** (quest infrastructure)
   - Common API for other mods:
     - CharVars / GlobVars (numeric, string and boolean data per character or global), viewable and editable in the admin panel
     - a permissions API (it can delegate selected admin functions to moderators)
     - a logger
     - a backup/export interface (only for mods that implement it)
   - Admin panel `Shift+U` / `datacmd sudoexile`; user settings `Shift+Alt+U` / `datacmd clientconfig`.
   - **"Single Player/Coop is NOT supported"**: dedicated server only.
   - Author's load-order advice: put Tot mods at the **bottom** of the modlist. Crash signature of a bad order: `Failed to find function ... in Tot_CommonLibrary_C`.
12. **Thrall Wars Utilities** (quest infrastructure; same author as #15)
   - Quest/script builder, Quest Log manager, Utility NPC (runs quests/scripts, acts as merchant), social NPCs and merchants.
   - Trigger system, utility/interaction/PvP zones, spawners, reward and loot boxes, kits, warp system, transport portal, utility door, global variable lever, climbing-blocker zone, cloud-hosted lore book.
   - **Requires Sudo** (it uses Sudo's CharVar manager; listed under "Required items").
   - Author: "Tot custom" must load after TWU. Tot custom is not in V1.
   - Its documentation is on Discord only, so every capability the campaign relies on is UNVERIFIED until shown in-game.
13. **Night Terrors** (optional content)
   - Random night encounters (demons, corrupted valkyries, undead) for players level 10+, between dusk and dawn, outside bases and land claims.
   - Difficulty rises with level; level 60+ unlocks the top tiers and a low chance of world-boss minions.
   - Demons dissipate after about 5 real-time minutes.
   - Author note: encounters **can occur inside some dungeons**.
   - Adds strong thralls (Corrupted Valkyries; named Kinarra and Aleatha), hellforged/blighted weapons, Sigils of Minevra, and Lesser Valkyrie summons from a T3 Ymir altar.
   - Plushies disable ambushes for one player.
   - Admin: `dc nightterrors admin`.
   - "Replaces no data"; adds to tables with ID sequence 1013446XX.
   - Uninstalling removes its weapons and valkyries.
14. **PvE Plus Ambush (Enhanced)** (optional content)
   - A per-player ambush timer for players level 9+; enemies scale with level.
   - Admin `DataCMD PvEAmbushConfig`: time between ambushes, warning time, spawn distance, enemy count, despawner, and an above-60 multiplier curve.
   - Settings apply only after the **next** rotation.
   - Enhanced disables the Insert console key by default. The client fix is `ConsoleKeys=Insert` in `Input.ini`.
   - Only Exiled Lands spawns are active.
15. **Thrall Wars Dungeon Mod** (content; raids)
   - **9 raid bosses, Normal and Hard mode.** Admin-placeable recipe journals and quests, an advanced door system, a gem system (`Shift+T`), the Age of Conan-style critical rating / critical damage system, 20 named thralls, T4-crafted gear.
   - Also: a PvP capture system, lockpicking and pickpocketing, traps.
   - Author: "in rapid transition to UE5 … still many missing textures".
   - Item IDs 298454001–298455000 and 189221–189500.
   - The Workshop page is behind Steam's mature-content gate, so its "Required items" block could not be read without signing in. The description names no dependency.

## Conflict matrix

`●` = the mod changes the area (author description or asset evidence). `◐` = touches it indirectly.

| Area | SM10K | Paragon | G&G | ThrRep | ITQoL | Riding | AncR | FROE | Cannibal | Shemite |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Item stack sizes / ItemTable | ● | | ◐ items | | ● multiplier | | ◐ items | | | ◐ items |
| Follower stats / buffs | | ● | | ● buff | ● modifiers | | | | | |
| Follower count | | | | ◐ UI max 5 | ● | | | | | |
| Follower UI (party / stat window) | | | | ● | ● | | | | | |
| Follower behaviour / mounts | | ◐ | | | ● | ● | | | | |
| XP / levels | | ● | | | ◐ follower XP | | | | | |
| NPC spawns | | | | | | | | ● | ● camp | ● |
| Base-map terrain / gameplay maps | | | | | | | | | ● 3F | ● x1_y3, x1_y4 |
| Building pieces | | | | | ◐ limits | ◐ Registrar | ● | | | ● |
| Weapon damage | | ● | ● | | ● global | | | | | |
| Per-character / world save data | ? | ● | ● | ● | ● | ● | ● | ● | ● | ● |

Specific pair risks to watch:

| Pair | Risk | Mitigation / test |
| --- | --- | --- |
| StackMe10K × ITQoL Stack Size Multiplier | Two stack-size systems | **Rule: with StackMe10K installed, the ITQoL Stack Size Multiplier stays disabled.** It is off by default; never enable it. |
| StackMe10K × item-adding mods (G&G, Ancient Realms, Shemite) | The 10,000 stack may not apply to modded items, depending on the patch scope and order | In-game check later. Server logs cannot show stack sizes. |
| Thrall Reputation × ITQoL | Two follower party UIs; Thrall Reputation warns about stats-window changes | UI check needs an authenticated client (blocked, 4F). Server side: load evidence and errors only. |
| ITQoL × Riding Thralls | Both change follower movement and handling (summon, send home, inventory pick-up vs mounting and passengers) | Keep ITQoL follower features off initially; record load order in any report. |
| Savage Paragon × Thrall Reputation × ITQoL | Stacking follower buffs | **Rule: ITQoL Additional Follower Count and thrall stat modifiers stay disabled until balance testing.** |
| Savage Paragon × XP mods | Author: load after any XP mod | No XP-curve mod in V1 (ITQoL "Inactive Follower XP" is follower-only). Keep Paragon after ITQoL as a precaution until runtime evidence says otherwise. |
| FROE × Shemite × Cannibal Captivity | Overlapping NPC spawn tables and camp maps | Boot evidence and spawn-related errors. The FROE region blacklist is available if needed. |
| Shemite × Cannibal Captivity | Both change base-map content | Check map tiles (3F vs x1_y3/x1_y4) after D2/D3. |
| Ancient Realms / Shemite building sets × updates or removal | WIP pieces may disappear; removing a building mod breaks placed pieces | Verified cold backup before every change; never remove these from the live world without a decision. |

Additions #11–#15 (2026-10-02):

| Area | Sudo | TWU | NightT | Ambush | TW Dungeon |
| --- | --- | --- | --- | --- | --- |
| Per-character / global variables | ● | ● uses Sudo | | | |
| NPC spawns (dynamic) | | ◐ spawners | ● night | ● per-player | ● raid bosses |
| Thralls / followers | | | ● valkyries | | ● 20 named |
| Weapon damage / crit | | | ◐ weapons | | ● crit system |
| Teleport / doors / zones | | ● warp, portal, door | | | ● doors |
| Items / ItemTable | | ◐ kits | ● 1013446XX | | ● 298454001+ / 189221+ |
| Per-character / world save data | ● | ● | ● | ● | ● |

| Pair | Risk | Mitigation / test |
| --- | --- | --- |
| TWU × Sudo | Hard dependency; an outdated Sudo API packed by another mod can crash | Sudo at the bottom of the list; grep each boot log for `Tot_CommonLibrary_C` errors |
| TW Dungeon × Savage Paragon × Grit & Grease | Three damage/crit systems stack (TW crit rating, Paragon Evisceration +15% crit / +50% crit damage, G&G elemental %) | Campaign reward caps on **effective** damage (`CAMPAIGN_V1.md` 9.2) |
| Night Terrors × PvE Plus Ambush × FROE × Cannibal | Overlapping dynamic and static hostile spawns; an ambush and a night terror can hit one player together | Campaign test D-1 (solo, levels 10–20, at night) |
| Night Terrors × dungeons and raid arenas | Encounters can spawn in some dungeons (author) | Campaign test X-1 (each boss with Night Terrors suppressed and on) |
| ITQoL portal spell / TWU warp × raid gates | Teleports could bypass gated arenas | Keep ITQoL sorcery off; campaign test G-1 |
| TW lockpicking × gated doors | A lockpick might open a raid gate | Campaign test G-1 |
| TW PvP capture × `PVPEnabled=False` | The capture system might act on players | In-game check |
| Night Terrors thralls × Authority balance | Corrupted Valkyries (author stats above the Cimmerian Berserker) from level 10, outside the campaign gates | Campaign test D-2 |

## Batch test plan

Batches are **cumulative**: earlier batches stay installed. The full fifteen-mod set is the last step.

| Step | Adds | Installed after |
| --- | --- | --- |
| A | StackMe10K, Savage Paragon, Grit & Grease | 3 |
| B | Thrall Reputation, Improved Thralls & QoL, Riding Thralls | 6 |
| C | Ancient Realms Enhanced | 7 |
| D1 | Fantasy Races Of Exiles | 8 |
| D2 | Cannibal Captivity | 9 |
| D3 | Shemite City State | 10 |
| E | Tot ! Enhanced Sudo, Thrall Wars Utilities (always together: TWU requires Sudo) | 12 |
| F1 | Night Terrors, PvE Plus Ambush | 14 |
| F2 | Thrall Wars Dungeon Mod | 15 |
| Review | All fifteen together: runtime evidence review, then declare the final load order (starting point: `CAMPAIGN_V1.md` section 3) | 15 |

Gates for **every** step. All are required; a FAIL stops the plan.

1. Server Offline; no Conan process tree.
2. Verified cold backup: manifest, SHA-256, SQLite `quick_check` = ok.
3. Import each `.pak` through the production Local pipeline. Source SHA-256 = installed SHA-256, and the source is untouched.
4. `modlist.txt` exactly matches the configured order. The order is set through the app (`reorder-local`), never by editing the file.
5. Real server boot and real readiness ("World is ticking").
6. Positive current-boot evidence for **every** installed mod:
   - `Mounting mod pak file`
   - the extracted container mounted
   - `contributes N package(s)`
   - no duplicate mounts, no missing-mod, dependency or mod errors (`mod-boot` analysis)
7. Clean shutdown: acknowledged, exit code 0, no forced kill, no WAL/SHM, no orphan processes.
8. Verified cold backup after the stop, SQLite `quick_check` = ok.
9. Record: boot time, server working set at readiness, `Saved\ExtractedMods` size, local `.pak` size vs Workshop size.

Step-specific checks:

| Step | Extra checks |
| --- | --- |
| A | First live boot for StackMe10K, Paragon and G&G, plus the multi-mod / load-order checkpoint below. Note: Paragon's multiplayer is "beta" by the author's own statement. |
| B | ITQoL loads with everything **default-off**. Stack Size Multiplier, Additional Follower Count and thrall stat modifiers must stay disabled. |
| C | Large item (497 MB): watch boot time, memory and extraction size. Back up before and after. |
| D1 | NPC spawn-table mod: watch spawn or DataTable errors in the boot log. |
| D2 | Crash risk reported after the 2026-09-15 update; watch for server crashes during boot and a 10-minute hold. |
| D3 | Map mod, 2.1 GB: watch boot time, memory (host has 16 GB; the vanilla server uses about 5.6–6.9 GB) and the ExtractedMods size. D: has 403.8 GB free. |
| E | `modlist.txt` puts TWU above Sudo, with Sudo last. No `Failed to find function … Tot_CommonLibrary_C` in the log. The in-game Sudo panel and TWU builder checks wait for an authenticated client. |
| F1 | Table additions only (Night Terrors ID 1013446XX): watch DataTable errors. Server logs cannot show the dynamic spawns; that needs a client. |
| F2 | 466 MB, mid-transition to UE5: watch boot time, memory, ExtractedMods size, and DataTable / item-ID errors (298454001–298455000, 189221–189500). |
| Review | Mount sequence and IoStore container `Order=` for all fifteen. Declare an order only from that evidence plus the author constraints. |

### Batch A: multi-mod / load-order checkpoint (4E.2)

Roles: **A = StackMe10K, B = Savage Paragon, C = Grit & Grease.** Every step uses the per-batch gates above.

1. **Baseline:** server Offline, no process tree, diagnostics, `Mods` and `modlist.txt` recorded, verified cold backup (`quick_check` = ok). Stop if the backup verification fails.
2. **Import** A, B and C through the production Local pipeline. Record source, source SHA-256, installed path and installed SHA-256; the hashes must match, and the sources must stay untouched.
3. **Initial explicit order** A, B, C via `reorder-local`. `modlist.txt` must match exactly.
4. **Boot 1:** real readiness, and positive current-boot load evidence for all three (no missing-mod, dependency or duplicate-mount errors). Record the runtime mount sequence and container `Order=`. Clean stop.
5. **Reorder** to C, A, B through the app (never by hand). `modlist.txt` must change accordingly, `.pak` hashes must be unchanged, and nothing may be re-copied.
6. **Boot 2:** load evidence for all three. Does the log mount sequence follow the new order? If it cannot be shown, record **NOT PROVEN**. Clean stop.
7. **Remove the middle entry** (A = StackMe10K at that point) through the app:
   - verified `pre-mod-removal` backup
   - the `.pak` archived to `removed-mods`
   - the source untouched
   - `modlist.txt` = C, B
   - remaining hashes unchanged
8. **Boot 3** with `--expect-absent StackMe10K.pak`: the removed mod is not mounted, B and C load, and there are no stale or missing-mod errors. Clean stop.
9. **ExtractedMods:** observe read-only and record stale artifacts per removed mod as technical debt. Nothing is deleted.
10. **Restore Batch A** (batches are cumulative): re-import StackMe10K, set the order, boot, positive evidence for all three, clean stop, verified cold backup (`quick_check` = ok).
11. `dotnet build` and `dotnet test` (no skipped tests). Then stop for review before Batch B.

## Configuration rules (V1)

- **StackMe10K installed → ITQoL Stack Size Multiplier stays DISABLED.**
- **ITQoL Additional Follower Count stays DISABLED** until balance testing.
- **ITQoL thrall stat modifiers stay DISABLED** until balance testing. That covers individual thrall base stat modifiers, and, as a precaution, the pet and golem stat modifiers and the global weapon and armor base stat modifiers.

ITQoL is documented as default-off, so the rule holds as long as nobody changes its settings. Its settings are changed in-game (`DataCmd ImprovedThrallsQoL`), which needs an authenticated admin client. A server-side check is still open (see below).

## Open items

1. **StackMe10K 10,000:** IN-GAME BEHAVIOR NOT YET VERIFIED. It needs an authenticated client (4F is blocked); server logs are not evidence of it.
2. **ITQoL settings state:** where the mod persists its admin settings (probably the world database). A read-only inspection of a backup **copy** after Batch B could confirm "all off" without a client, if the storage is identifiable.
3. **Workshop mod `.pak` file names** are not in the public metadata. They become known when the local paths are provided.
4. ~~Whether Batch A carries the 4E.2 steps~~: resolved, yes (see "Batch A: multi-mod / load-order checkpoint").
5. ~~Local paths for Savage Paragon and Grit & Grease~~: resolved; installed in Batch A. All ten V1 `.pak` files are in `C:\Users\vkkha\Downloads\mod conan`. For the additions, still needed: **Sudo and Thrall Wars Utilities**.
   - `NightTerrors.pak`, `PvEPlusAmbush.pak` and `SlaveWarsServer.pak` (the Thrall Wars Dungeon Mod; internal roots `SlaveWars…`) are in `C:\Users\vkkha\Downloads\mod conan`.
   - Each matches its Workshop item size to the byte (checked 2026-10-02, read-only).
6. Thrall Wars Dungeon Mod "Required items": not readable (mature-content gate). Confirm from the `.pak` contents or the author's Discord before F2.

Harness commands for each step (from `d23e091`):
- `cold-backup`
- `import-local <path>`
- `reorder-local A.pak,B.pak,C.pak`
- `mod-boot [--hold S] [--expect-absent X.pak]`
- `remove-local X.pak`
- `extracted`
