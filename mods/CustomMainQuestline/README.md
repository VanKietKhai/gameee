# Mod #14 — Custom Main Questline (Phase 14A: feasibility + quest engine foundation)

Status: **design + tested reference engine only. No `.pak` exists and none was faked.** The 13-mod staging pack is frozen and unchanged. The production world has NOT been created.

Baseline: Conan Exiles Enhanced **2.2.3 / CL-378132** (release-beta), dedicated server `ProjectVersion 2.2.3`, engine 5.8.2-378132.

## 1. Authoring toolchain audit (read-only) — BLOCKED

| Needed | Found on this host |
|---|---|
| Conan Exiles Enhanced DevKit (Unreal Editor build matching the game) | **Not installed.** The Epic Games Launcher is present but has no installed apps. There is no `.uproject`, no `UnrealEditor.exe` and no Steam DevKit app manifest. |
| Cook/package tools (UnrealPak / IoStore packaging through the DevKit) | **Not installed** |
| Existing mod #14 source project | **None** (a drive sweep found no Conan mod source projects; `CAMPAIGN_V1.md` recorded the same) |
| Target payload format | The 13 accepted mods are a single `.pak` that contains `<Mod>-WindowsServer.pak/.utoc/.ucas` and a client payload, with metadata `devkitRevisionNumber 1002`, `minimumVersion Enhanced`. Mod #14 must be cooked by the Enhanced DevKit in the same format; it cannot be hand-assembled. |
| Licensed test client | Still required. The local client folder contains Steam-emulator configuration and is not used. |

**What must be installed before any `.pak` work:**
1. The official **Conan Exiles Enhanced DevKit** from Funcom, at the build that matches the target game. Note that the server and client are a `release-beta` CL-378132 build; confirm a DevKit exists for it, or decide to target the matching live release. It needs ample disk space on a drive other than C:, which is nearly full.
2. A **licensed Steam Conan Exiles client** for in-game QA. The vertical slice needs a real player kill, reconnect and restart.

Until then, hook/class names for NPC death, player identity, party/proximity, UI widgets and map markers **cannot be resolved inside the editor** and are not guessed here.

### Phase 14B, Step 1: DevKit availability (2026-10-05) — STOPPED before install

**Official source:**
- **Conan Exiles Enhanced Dev Kit** (Funcom), free, distributed **only through the Epic Games Store / Epic Games Launcher**. It needs an Epic account sign-in and a free "Get".
- The store page could not be read automatically (HTTP 403), so the current build or revision was **not** confirmed from an official page.

**Version vs. the target build:**
- A third-party build guide (not authoritative) reports the Dev Kit engine as `UE 5.6.1-366792 (++exiles+release)`.
- The staging game and server are `5.8.2-378132 (++exiles+release-beta)`.
- **No evidence was found of a Dev Kit for the 2.2.3 release-beta CL-378132 build.** Funcom's public patch notes describe Dev Kit updates without revision numbers, and they do not mention a 2.2.3 Dev Kit.

**Nearest compatible revision:**
- All 13 accepted mods embed `devkitRevisionNumber 1002`, and all 13 load cleanly on the 2.2.3 / CL-378132 server (the v223 smoke, full and restart runs).
- So revision-1002 Dev Kit output is *empirically accepted* by this server.
- An exact Dev Kit match for 2.2.3 beta is **unproven**; compatibility is not assumed.

**Size, drive and packaging:**
- Install size is reported as ~24 GB (store requirement) up to ~160–250 GB once installed with shader and derived-data caches.
- Recommended drive: **E:**, which has the most free space. C: is nearly full.
- The official packaging path is the Dev Kit's own `RunUAT BuildMod` (cook + pak to IoStore for the Windows, WindowsServer and LinuxServer platforms). Third-party "headless" pipelines are **not** used.

**Decision needed (operator):**
1. Install the official Epic Dev Kit on E: (a large install, so it needs approval), and confirm in the launcher that its revision writes `devkitRevisionNumber 1002` (or note a newer one).
2. Decide whether mod #14 targets the beta CL-378132 build (Dev Kit match unproven) or the matching live release.

Steps 2–8 (project, Remnant verification through Dev Kit references, Quest 01, packaging, persistence, server validation) are **NOT RUN**.

**Update (2026-10-05 18:21): install started by the operator.**
- The operator started the official Epic install of **Conan Exiles Enhanced Dev Kit**, AppVersion **377800**.
- The target is `D:\epic\CEUE5Devkit` (D:, not the recommended E:; D: had ~280 GB free). The full install is ~182 GB.
- At 18:21, ~65.5 GB was staged and the install was still incomplete.
- CL-377800 sits between the 2.2.2 server (CL-377096) and the 2.2.3 beta (CL-378132). So it is the nearest official Dev Kit, but it is **not** an exact match for the staging build. Compatibility will be judged by the mod's embedded `devkitRevisionNumber` and a staging boot, not assumed.
- Step 2 starts only after the launcher reports the install as complete.

## 2. Evidence gathered from the 2.2.3 build (read-only)

- **Asset names.** The server's IoStore `.utoc` directory indexes are readable, so asset *names* can be listed. Asset *contents* (DataTables, Blueprint graphs, localization) sit compressed in `.ucas` and need the DevKit or proper tooling.
- **Persistence.** The world DB persists actor/controller properties in `properties(object_id, name='Class_C.Property', value BLOB)`, including a **mod controller** (`MC_ImprovedThrallsAndQoL_C`). Mod controllers are singleton actors recorded in `mod_controllers` and `actor_position`. This is the game-managed persistence path mod #14 will use; there is no raw SQL.
- **Player identity.** `characters(playerId TEXT, id, char_name, level, guild, …)` and `account(platformId)`. Progress keys on `playerId`, not on guild/clan.
- **NPC death in logs.** The server log already prints `KillCharacterWithRagdoll_Implementation … Name: <Class>_C_<n> CharacterName: <display>` for NPC deaths. This is the verification path for boss identities: one staged kill reveals the exact class and display name.

## 3. EXACT ABYSMAL REMNANT TARGET — **UNRESOLVED**

The 2.2.3 asset names prove the content exists:
- the `AbysmalRemnant` VFX folder (`NS_AbysmalRemnant_VomitCone/_Projectile/_Splash/…`);
- Abyssal Remnant weapons, `trophy_abyssal_remnant` and `icon_a03c1_head_abyssalremnant` (content set **A3C1**, Darkened Dregs);
- the Dregs dungeon controller `BP_DarkDregsDungeonController` and the boss-fight buff `BP_AC_Buff_AcidBath_DarkDregsBossFight`.

No Blueprint is named after the Remnant. Ranked candidates, **not verified**:
1. `BP_NPC_Wildlife_SewerAbomination`. Its acid puke and spit attack kit matches the Remnant VFX, and there is a matching `BP_PL_W_Trophy_DarkDregs_Abomination`.
2. The **Nahjef** encounter (`HumanoidNPCCharacter_20percentbigger_boss_Nahjef`, `DT_NPC_Nahjef`, `BP_NahjefAIController`, spawned by `BP_BossDarkDregs_Nahjef_SpawnRequest`). This looks like a *separate* humanoid Dregs boss.

To verify: one staged kill on staging (licensed client), then read the server log line `Name:`/`CharacterName:`, then record the exact class path. Until then the Quest 01 target stays `Unverified` and **cannot grant credit**; the engine enforces this.

## 4. Quest data model (data-driven)

There is one record per main quest (maps 1:1 to a DataTable row): `QuestId, ActId, Sequence, DisplayName, Description, Target{Type, DisplayName, TargetClassPath, Verification, Candidates}, PreviousQuestId, NextQuestId, MinimumLevel, RecommendedLevelMin/Max, RecommendedPartyMin/Max, LocationName, Marker{Label,X,Y,Z,AreaRadius}, Reward{RewardId, Entries[Type,Id,Amount], Placeholder}, CompletionCreditRadius, CampaignWeight, Enabled`.

- Provisional campaign: [`data/main-quests.provisional.v1.json`](data/main-quests.provisional.v1.json). It has 11 quests across Act I–V and the Epilogue, with **every target `Unverified`, no class paths, placeholder XP rewards** and a 5,000-unit (50 m) default credit radius.
- The catalog **fails closed**. Any malformed record rejects the whole catalog: duplicate id or sequence, dangling, asymmetric or cyclic links, unreachable quests, zero or negative weight, credit radius out of range (0 < r ≤ 200 m), bad level or party ranges, a verified target without a class, an invalid reward, unknown JSON fields, or the wrong data schema version.

## 5. Persistence model

- Per-player record `PlayerQuestState{SchemaVersion=1, PlayerId, CurrentQuestId, Completed[{QuestId, CompletedAtUtc, Source}], TrackedQuestId}`. It is held by the mod #14 controller as persisted properties, keyed by `playerId`.
- The state is saved immediately after every change, so it survives death, logout and reconnect, restart, graceful shutdown and backup/restore, because it lives in the world DB through the game's own persistence.
- Versioning: a newer, unknown, unreadable or incomplete save is **refused, not reset**. Explicit migrations run from version N to N+1. Completed quests that a later catalog no longer contains are **kept**. `Reconcile` re-anchors a removed current quest without discarding completed progress.

## 6. Multiplayer credit model (server-authoritative)

When a boss dies, the server builds `BossDeathEvent{EventId, DeadClassPath, location, nearby players}`. Then:
1. Find the quest whose **verified** target class matches **exactly** (no substring or case folding); otherwise do nothing.
2. Credit each distinct nearby player only if all of these hold:
   - the player's current quest is that quest and it is not already completed;
   - the previous quest is completed;
   - the player's level is at least the quest's MinimumLevel;
   - the player is within the quest's credit radius of the death location;
   - with the optional `ProximityAndParticipation` rule, the player is also flagged as a participant (they or their follower damaged the boss).
3. **No killing blow is required.** A thrall kill still credits the eligible owner and party.
4. Complete, unlock the next quest, emit notifications, and persist every changed player.

Guarantees:
- A future boss can never skip progression.
- A duplicate death event is ignored by event id.
- A second kill of the same boss cannot complete the quest again.
- Progress is per player, never per clan.

## 7. UI plan (after the engine is in-game and tested)

- **Main Quest panel:** Act, current quest, target/boss name, description, location, recommended level and party, reward, overall campaign % (completed weight ÷ total enabled weight, never level-based), status, and a **Track Quest** toggle.
- **Banners:** `QUEST COMPLETE`, `NEW BOSS UNLOCKED`, `CAMPAIGN COMPLETE`.
- The panel reads only the replicated quest state; it never decides progression.

## 8. Marker plan

- The marker shows only when the tracked current quest is *available* (level gate met, previous quest complete).
- It points to the general dungeon or encounter area (`AreaRadius`), not the boss's exact coordinates.
- It moves to the next quest on completion, and it is restored from saved state alone after reconnect or restart.
- Simple Minimap integration is **optional**: an adapter that is disabled if the mod is absent or changes. Simple Minimap itself is never patched. Without it, the quest panel still shows the location text.

## 9. Admin / QA tools (staging only)

`Inspect`, `SetCurrentQuest`, `CompleteCurrentQuest`, `ResetProgress` and the progress printout require an admin caller. Normal players get an authorization error. In-game, they are to be bound to the server's admin check, never to an unrestricted chat command.

## 10. Automated tests — `tests/MainQuestline.Reference.Tests` (53 passing)

- **Credit rules:** previous-quest gate, level gate, correct boss, wrong boss, exact (not substring or case-folded) class match, future boss cannot skip, multiple eligible nearby players without a killing blow, ineligible and out-of-radius players, the participation rule, duplicate death event, a player listed twice, unverified targets never crediting, and the final quest completing the campaign.
- **Progress:** weighted campaign %, 10 quests with 5 done = 50%, and removed quests kept but not counted.
- **Markers:** marker show/hide/restore.
- **Admin tools:** rejected for players, working for admins.
- **Persistence:** reconcile without data loss, 16 malformed-catalog cases, save round trip with schema version 1, rejected unreadable or incomplete saves, newer schema refused, and foreign schema writes refused.
- **Provisional data:** the provisional data file loads and has no verified targets, and malformed data files fail closed.

The engine is the **executable specification** for the Blueprint/DataTable implementation. It does not run inside Conan.

## 11. First vertical slice (Quest 01) — BLOCKED

It needs:
- the DevKit (§1);
- the verified Abysmal Remnant class (§3);
- a licensed client for QA.

When these exist, the slice is: mod controller with persisted per-player state, NPC-death hook, eligibility via this engine's rules, the Q01 marker, the panel and banner, admin tools, unlocking the Q02 placeholder, then the reconnect and restart persistence checks. Before the first live install of mod #14, take a fresh verified cold backup. Mod #14 is appended as #14 on staging only, and any new mod #14 log line stays unknown until reviewed.
