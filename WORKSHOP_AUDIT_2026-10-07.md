# Client Steam Workshop audit — 2026-10-07 (read-only)

Source: `D:\steamnew\steamapps\workshop\content\440900` (37 subscribed items) and `appworkshop_440900.acf` (Workshop update dates). Compatibility was read from the modinfo embedded in each `.pak`: Conan Exiles Enhanced 3.0.0 (CL-378787) accepts Dev Kit revision `[1002]` only. A pak with no Enhanced modinfo is a legacy (UE4-era) build and cannot load on this server. Nothing was deleted or changed; unsubscribing is done by the operator in Steam.

## Keep — Enhanced (revision 1002): 7

| Workshop ID | Mod | Workshop update | Note |
|---|---|---|---|
| 3719513784 | Simple Minimap (by Xevyr) v5.2.1 | 2026-09-15 | Byte-identical to the `Simple_Minimap.pak` in the validated 14-mod server/client pack |
| 3736778117 | Better Thrall ICONS (1.0.2) - Enhanced | 2026-09-16 | Cosmetic thrall icons; not on the server |
| 3729932396 | Better Tavern PATRONS (1.1.4) - Enhanced | 2026-09-16 | Tavern thralls; not on the server |
| 3721090132 | Tot ! Enhanced Sudo 1.3.80 (ModAdmin.pak) | 2026-10-06 | Admin tools; replaces legacy ModAdmin/TotAdmin |
| 3721096154 | Tot ! Enhanced Custom 1.10.59 | 2026-10-06 | Replaces legacy TotCustom |
| 3723101055 | Organizer Sorting Chest v1.4.4 | 2026-09-17 | Storage |
| 3722388367 | ModControlPanel (Enhanced) | 2026-09-15 | Replaces legacy ModControlPanel |

## Unsubscribe — 30 (29 legacy + 1 Enhanced feature duplicate)

Enhanced, but duplicates a server-pack mod (operator decision 2026-10-07):

| Workshop ID | Mod | Workshop update | Duplicates |
|---|---|---|---|
| 3720904511 | Better Thralls v3.5.0 (Enhanced) | 2026-10-03 | Improved Thralls & QoL |

Legacy, no Enhanced modinfo (29):

Older version of a mod you also have in Enhanced form (duplicates):

| Workshop ID | Pak | Workshop update | Replaced by |
|---|---|---|---|
| 2898150544 | Simple_Minimap.pak | 2026-04-15 | **old minimap** → 3719513784 |
| 931088249 | BetterThralls.pak | 2025-04-10 | 3720904511 |
| 3036057084 | ModAdmin.pak | 2026-04-21 | 3721090132 |
| 2850232250 | TotAdmin.pak | 2026-04-21 | 3721090132 |
| 2886779102 | TotCustom.pak | 2026-04-21 | 3721096154 |
| 1823412793 | ModControlPanel.pak | 2023-12-15 | 3722388367 |
| 3361295718 | ChestLabels.pak | 2024-11-13 | Chest Labels (Enhanced) already in the server pack |

Feature duplicates of mods in the server pack (and legacy):

| Workshop ID | Pak | Workshop update | Duplicates |
|---|---|---|---|
| 2236570677 | LitManItemStackAndContainerSizeRecompiled.pak | 2021-01-10 | StackMe10K |
| 1629644846 | KerozardsParagonLeveling.pak | 2025-04-02 | Savage Paragon |
| 3013994101 | Exiled_Heroic_lvl_System.pak | 2025-10-17 | Savage Paragon (levels) |
| 3220670719 | KATUI.pak (Extended Thrall Stats) | 2024-04-17 | Improved Thralls & QoL / Better Thralls |
| 3012140204 | Thralls_To_FORTY_Standard.pak | 2023-11-13 | Improved Thralls & QoL |

Other legacy items (cannot load on Enhanced 3.0.0):

2875171748 ArmorStats_in_Bench (2025-08-19), 3142920306 AttackSystemOverhaul (2026-06-12), 3248573436 Attic-Hair (2025-09-14), 2566191068 Better_Pets (2023-08-16), 2001044383 LCDA_gameplay / Dangerous Exile AHDS (2024-10-21), 3360639527 EnhancedArmoryandArsenal… (2026-05-08), 2872104059 Grim_Map (2025-04-26), 2794943951 HighmanesArsenal (2026-04-30), 2997899247 Mount_Combat_Restored (2025-03-29), 2974559563 Oreros_Dungeon_Overhauls (2026-06-14), 1224792245 Permadeath (2023-06-22), 880454836 Pippi (2024-04-03), 2723987721 Pythagoras_Support_Beams (2025-08-19), 2673599338 RP_Basics (2025-09-23), 2861512275 ShimasCompendium 1.1.69 (2025-12-09), 2811828807 Siptah_Pets (2024-05-06), 1797359985 UIMod_Hosav (2024-04-05), 877108545 UnlockableContainers (2025-03-25).

## Totals

37 subscribed → **7 kept** (Enhanced) + **30 to unsubscribe** (29 legacy + Better Thralls, which duplicates Improved Thralls & QoL on the server).

The server/client pack (14 mods) is unaffected: its paks are local copies, and its Simple Minimap is the same file as the kept 3719513784.
