# Mod #14 — campaign order (operator source, 2026-10-07)

The operator chose the community guide **"Map of Dungeons/Bosses/Story in suggested Order of progression WIP 3"** (r/ConanExiles, https://www.reddit.com/r/ConanExiles/comments/10h656z/; credits conanprogression.carrd.co and conanexiles.fandom.com) as the campaign order. It replaces the earlier provisional order, which missed 9 stops and put Midnight Grove / Temple of Frost / Black Keep and the Wine Cellar in the wrong places.

The guide is community-made and may predate Enhanced 2.2.3. **Every boss is still traced in the Dev Kit (spawner → class → location) before its quest can grant credit.** Map cells were read by eye from the guide's map (±1 cell).

| # | Stop | Proposed quest target | Map cell | Guide level | Status |
|---|---|---|---|---|---|
| 0 | Prelude: Nunu the Cannibal, Shaman's Rise | story hint only | F4 | — | not a quest |
| 1 | The Dregs | Abysmal Remnant | D4 | ~20–25 | MQ01; traced in the Dev Kit, runtime kill pending |
| 2 | Tower of Bats | Albino Bat Demon (needs Staff of the Triumvirate) | F5 | ~25–30 | to trace |
| 3 | Gallaman's Tomb | Giant Crocodile (Jagged Scourgestone) | H4 | — | to trace |
| 4 | Captain's Quarters | Gall o' the Spear-din **or** Hekkr Waverunner (Shattered Scourgestone) | N7 | — | operator decision |
| 5 | The Passage | Sand Reaper Hive Queen, boss version (Broken Scourgestone) | K9 | — | to trace |
| 6 | Palace of the Witch Queen | The Witch Queen | O5 | — | to trace |
| 7 | Sandswept Ruins | hand 3 Scourgestone pieces to Petruso → Heart of the Sands | L4 | ~30–40 | not a boss; operator decision |
| 8 | The Barrow King (Mounds of the Dead) | Barrow King | A11 | ~30–40 | to trace |
| 9 | Midnight Grove | Werewolf Impisi | D8 | — | hints added (MQ06 data) |
| 10 | Children of Jhil | several caves | H10 | — | not a single boss; operator decision |
| 11 | Temple of Frost | Hrungnir of the Frost | E14 | 50–55 | to trace |
| 12 | The Black Keep | Kinscourge | F11–F12 | ~55 | to trace |
| 13 | The Arena | Undead Dragon (Sinkhole) | H6 | — | to trace |
| 14 | The Unnamed City | 7 bosses (Guardian of the Flame, Commander, Watcher, Winged Death, Gravewalker, Red Mother, Brute) | D6 (bosses C5–D7) | — | operator decision: all or one |
| 15 | Well of Skelos | The Degenerate | G14 (Dragonmouth entrance F12) | 60 | to trace |
| 16 | The Scorpion Den | Ancient Scorpion Queen | B6 | — | to trace |
| 17 | The Sunken City | final boss not named in the guide | O5 | — | look up in the Dev Kit |
| 18 | The Wine Cellar | Thag (final boss) | B7 | — | to trace |
| 19 | Warmaker's Sanctuary | Champion of the Warmaker (last dungeon) | B7–B8 | — | to trace |
| 20 | Chaosmouth | forge The Keystone (story end) | F8 | — | display-only; the mod must **never** ask to remove the bracelet (that ends the game and deletes the character) |

Data changes so far: `mapGrid` and `hints` fields (catalog-validated); MQ01 `mapGrid` D4 and recommended level 20–25; MQ06 (Midnight Grove) `mapGrid` D8 and 7 entry/survival hints. The full re-sequencing of `main-quests.provisional.v2.json` waits for the operator decisions above.
