# Multiplayer test checklist — 13-mod staging gameplay test pack

**Test world only (STAGING).** Pack: `TEST_PACK_13MOD_STAGING.md` (13 mods, frozen 2026-10-05). World backup at the start of testing: `2026-10-05_014347`. Production world: NOT CREATED. Custom Main Questline: PAUSED.

How to use: tick a box only when it was really observed in game. Add the tester, date and any bug-report reference next to the item (template: `BUG_REPORT_TEMPLATE.md`). Server-side compatibility is already PASS; this checklist covers **in-game behaviour**, which is NOT YET VERIFIED.

Before each session (host):
- [ ] Every tester verified their client pack (13 × `OK` from the README check)
- [ ] Server started through the harness, true readiness reached, RCON reply confirmed
- [ ] Session batch id noted: ______________

## Connection
- [ ] All players join successfully
- [ ] 2 players in the same area
- [ ] 3–5 players in the same area
- [ ] Logout / reconnect
- [ ] Server restart / reconnect

## Simple Minimap
- [ ] Minimap renders
- [ ] Zoom works
- [ ] Map markers work
- [ ] POIs display correctly
- [ ] No UI corruption

## Player DBNO
- [ ] Player reaches 0 HP near a teammate
- [ ] Player enters the downed state
- [ ] Teammate revives
- [ ] Downed timer expiry causes a normal death
- [ ] Revive after repeated downs
- [ ] Reconnect after the DBNO test

## Chest Labels
- [ ] Rename a chest
- [ ] Label renders
- [ ] Multiple labelled chests
- [ ] Logout/relogin retains names
- [ ] Server restart retains names

## WO Riding Thralls
- [ ] Mount behaviour works
- [ ] Follower/thrall behaviour works
- [ ] No attachment/animation problem
- [ ] Mount survives reconnect/restart

## PvE
- [ ] Night Terrors encounters
- [ ] PvE Plus ambushes
- [ ] Normal NPC camps
- [ ] Dungeon / boss combat
- [ ] No broken spawn
- [ ] No invisible NPC
- [ ] No abnormal stat behaviour

## Persistence
- [ ] Build a structure
- [ ] Place storage
- [ ] Put items into a chest
- [ ] Capture/place a thrall
- [ ] Move a mount
- [ ] Logout
- [ ] Restart the server
- [ ] Verify everything remains

## Other accepted mods (spot checks)
- [ ] StackMe10K: stacks up to 10,000 where expected (ITQoL Stack Size Multiplier stays disabled)
- [ ] Savage Paragon / Thrall Reputation / Improved Thralls & QoL: follower stats, reputation and QoL features behave sensibly
- [ ] Grit & Grease: weapon infusions apply
- [ ] Ancient Realms: building pieces place, collide and persist
- [ ] Cannibal Captivity: camp and captivity content loads without broken NPCs

## Performance
- [ ] 1 player
- [ ] 2 players
- [ ] 3 players
- [ ] 4 players
- [ ] 5 players
- [ ] Note lag / stutter / server instability (time, player count, location): ______________

## After each session (host)
- [ ] End the hold (`release-hold`), graceful RCON stop: NORMAL or WARNING, exit 0, no forced kill, no orphan
- [ ] Complete-log analysis reviewed; any new warning/error classified (not whitelisted automatically)
- [ ] `quick_check` ok, controller/singleton counts 1, no duplicate persistence objects
- [ ] Verified post-session backup id: ______________
