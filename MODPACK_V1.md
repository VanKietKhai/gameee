# Target Modpack V1 — private Radmin Conan server

Status: **PLANNED. Metadata and test matrix only.** Nothing has been installed. Batch A waits for the local `.pak` paths.

- **Final load order: NOT DECLARED.** It is declared only after all ten mods have been tested together and the runtime evidence has been reviewed.
- **Deployment:** private friends-only dedicated server over Radmin VPN (`D:\conan exiles\depot_443031`, CL-377096).
- **Install path:** Local `.pak` files only, through the production Local Mod pipeline. No Workshop, no SteamCMD, client untouched, no public registration.

Sources (read-only, 2026-10-02):
- Steam Web API `GetPublishedFileDetails` (public data; all nine items returned `result=1`).
- The raw HTML of each Workshop page, checked for a "Required items" block.
- Workshop descriptions. These are the authors' claims and are not verified here.
- A local inspection of the WickStacks archive.

Workshop pages contain hidden template notices ("incompatible with Conan Exiles Enhanced", "removed from the community"). Their style is `display: none`, so they do not apply to these items. One automated page summary misreported them; they are ignored here.

## Mods

| # | Mod | Source / ID | Updated | Item size | Required items | Batch | Live-tested |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Thrall Reputation | Workshop `3787066846` | 2026-09-15 | 833,760 B | none | B | no |
| 2 | Savage Paragon | Workshop `3766043945` | 2026-09-23 | 4,760,799 B | none | A | no |
| 3 | Ancient Realms Enhanced (Work In Progress) | Workshop `3755775098` | 2026-09-15 | 496,900,005 B | none | C | no |
| 4 | Improved Thralls & QoL | Workshop `3758661389` | 2026-09-24 | 154,632,086 B | none | B | no |
| 5 | Fantasy Races Of Exiles | Workshop `3780741325` | 2026-09-15 | 5,293,057 B | none | D1 | no |
| 6 | [Enhanced] WO - Riding Thralls | Workshop `3803149679` | 2026-09-25 | 78,896,983 B | none | B | no |
| 7 | Shemite City State: Enhanced (v2.1) | Workshop `3755371705` | 2026-09-16 | 2,098,034,644 B | none | D3 | no |
| 8 | Cannibal Captivity v0.0.16 (Enhanced) | Workshop `3765743138` | 2026-09-29 | 112,432,880 B | none | D2 | no |
| 9 | Grit & Grease (Weapon Infusions) | Workshop `3801774752` | 2026-09-20 | 68,049,336 B | none | A | no |
| 10 | WickStacks | Nexus, Conan Exiles Enhanced mod 48 | archive 2026-09-19 | 4,492,459 B (`WickProbe.pak`) | unknown | A | **alone, 4E PASS** |

The Workshop item sizes total about 3.02 GB. When a local `.pak` arrives, its size is compared with the Workshop size as a version hint. A mismatch is recorded, not treated as a failure.

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
10. **WickStacks**
   - The Nexus page could not be fetched automatically (HTTP 403), so public metadata is **unverified**.
   - The local archive `C:\Users\vkkha\Downloads\mod conan\WickStacks 48 1 2026-09-19T22-02Z rcRAMvI71.zip` contains one file, `WickProbe.pak` (4,492,459 B, CRC32 `b035a5bf`).
   - It is byte-identical (CRC32 and SHA-256 `D7FE0EC0…D79C`) to `Downloads\mod conan\WickProbe.pak`, which 4E live-tested alone. Internal mod name: `WickProbe`.
   - Server container assets (read from the extracted `.utoc`): `BP_WickProbeController`, `DT_WickStackControl`, `DT_WickStackPatch`, `ItemTable`.
   - **Intended stack size 10,000: NOT VERIFIABLE server-side so far.** The 4E boot log has no ItemTable or stack lines. It needs an in-game check by an authenticated client, or the mod's documentation.

## Conflict matrix

`●` = the mod changes the area (author description or asset evidence). `◐` = touches it indirectly.

| Area | Wick | Paragon | G&G | ThrRep | ITQoL | Riding | AncR | FROE | Cannibal | Shemite |
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
| WickStacks × ITQoL Stack Size Multiplier | Two stack-size systems | **Rule: with WickStacks installed, the ITQoL Stack Size Multiplier stays disabled.** It is off by default; never enable it. |
| WickStacks × item-adding mods (G&G, Ancient Realms, Shemite) | The 10,000 stack may not apply to modded items, depending on the patch scope and order | In-game check later. Server logs cannot show stack sizes. |
| Thrall Reputation × ITQoL | Two follower party UIs; Thrall Reputation warns about stats-window changes | UI check needs an authenticated client (blocked, 4F). Server side: load evidence and errors only. |
| ITQoL × Riding Thralls | Both change follower movement and handling (summon, send home, inventory pick-up vs mounting and passengers) | Keep ITQoL follower features off initially; record load order in any report. |
| Savage Paragon × Thrall Reputation × ITQoL | Stacking follower buffs | **Rule: ITQoL Additional Follower Count and thrall stat modifiers stay disabled until balance testing.** |
| Savage Paragon × XP mods | Author: load after any XP mod | No XP-curve mod in V1 (ITQoL "Inactive Follower XP" is follower-only). Keep Paragon after ITQoL as a precaution until runtime evidence says otherwise. |
| FROE × Shemite × Cannibal Captivity | Overlapping NPC spawn tables and camp maps | Boot evidence and spawn-related errors. The FROE region blacklist is available if needed. |
| Shemite × Cannibal Captivity | Both change base-map content | Check map tiles (3F vs x1_y3/x1_y4) after D2/D3. |
| Ancient Realms / Shemite building sets × updates or removal | WIP pieces may disappear; removing a building mod breaks placed pieces | Verified cold backup before every change; never remove these from the live world without a decision. |

## Batch test plan

Batches are **cumulative**: earlier batches stay installed. The full ten-mod set is the last step.

| Step | Adds | Installed after |
| --- | --- | --- |
| A | WickStacks, Savage Paragon, Grit & Grease | 3 |
| B | Thrall Reputation, Improved Thralls & QoL, Riding Thralls | 6 |
| C | Ancient Realms Enhanced | 7 |
| D1 | Fantasy Races Of Exiles | 8 |
| D2 | Cannibal Captivity | 9 |
| D3 | Shemite City State | 10 |
| Review | All ten together: runtime evidence review, then propose a final load order | 10 |

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
| A | WickStacks was already loaded alone in 4E; now confirm it loads together with Paragon and G&G. Note: Paragon's multiplayer is "beta" by the author's own statement. |
| B | ITQoL loads with everything **default-off**. Stack Size Multiplier, Additional Follower Count and thrall stat modifiers must stay disabled. |
| C | Large item (497 MB): watch boot time, memory and extraction size. Back up before and after. |
| D1 | NPC spawn-table mod: watch spawn or DataTable errors in the boot log. |
| D2 | Crash risk reported after the 2026-09-15 update; watch for server crashes during boot and a 10-minute hold. |
| D3 | Map mod, 2.1 GB: watch boot time, memory (host has 16 GB; the vanilla server uses about 5.6–6.9 GB) and the ExtractedMods size. D: has 403.8 GB free. |
| Review | Mount sequence and IoStore container `Order=` for all ten. Propose an order only from that evidence plus the author constraints. |

## Configuration rules (V1)

- **WickStacks installed → ITQoL Stack Size Multiplier stays DISABLED.**
- **ITQoL Additional Follower Count stays DISABLED** until balance testing.
- **ITQoL thrall stat modifiers stay DISABLED** until balance testing. That covers individual thrall base stat modifiers, and, as a precaution, the pet and golem stat modifiers and the global weapon and armor base stat modifiers.

ITQoL is documented as default-off, so the rule holds as long as nobody changes its settings. Its settings are changed in-game (`DataCmd ImprovedThrallsQoL`), which needs an authenticated admin client. A server-side check is still open (see below).

## Open items

1. **WickStacks 10,000:** how to verify it (in-game check, or mod documentation). Server logs do not show stack sizes.
2. **ITQoL settings state:** where the mod persists its admin settings (probably the world database). A read-only inspection of a backup **copy** after Batch B could confirm "all off" without a client, if the storage is identifiable.
3. **Workshop mod `.pak` file names** are not in the public metadata. They become known when the local paths are provided.
4. Whether Batch A should also carry the 4E.2 multi-mod steps (explicit order, reorder, remove the middle mod, re-boot).

Harness commands for each step (from `d23e091`):
- `cold-backup`
- `import-local <path>`
- `reorder-local A.pak,B.pak,C.pak`
- `mod-boot [--hold S] [--expect-absent X.pak]`
- `remove-local X.pak`
- `extracted`
