# Mod #14 — Main Quest tab: design (Phase 14B, design-only)

Status: **design + tested reference projection. No Unreal widget, Blueprint, struct or DataTable asset exists yet, and none is faked.** The UMG work starts after the Dev Kit is installed and the minimal-package compatibility gate passes (README §1).

The executable specification is in `src/MainQuestline.Reference`:
- `MainQuestView.cs` holds the tab's view model and the read-only projection, `BuildView` / `Detail`.
- `CampaignEngine.SetTrackedQuest` handles Track Quest on the server.
- `QuestNotification` / `NotificationKind` / `MilestoneKind` form the banner contract.

All of this is covered by `tests/MainQuestline.Reference.Tests/MainQuestViewTests.cs`.

## 1. Principles

1. **The server decides, the UI displays.**
   - Credit, completion, unlocks and tracking changes happen only in the server-side quest engine.
   - The tab is a pure function of the player's replicated `PlayerQuestState` plus the quest/act DataTables.
   - The tests prove that building the view never changes the state.
2. **A real campaign tab, not only popups.** The banners (§6) are a separate, lightweight HUD layer that works while the tab is closed.
3. **No fragile vanilla patching.** Integration goes through a supported hook, or a standalone widget (§7).
4. **The UI works without Simple Minimap.** The minimap adapter is optional (§8).

## 2. Wireframe

**Visual mockup:** [Main Quest Tab Mockup](https://claude.ai/artifact/LqogeEnkUB7EyX7WyHzBmb) (private until shared). Its source is in [`mockup/`](mockup/).
- The boards: the interactive tab with an ACTIVE quest; a LOCKED quest (Partial); a COMPLETED quest; and the HUD banner queue.
- Every value comes from `data/main-quests.provisional.v2.json`, with the player at MQ05 and level 48.
- The marker line reads "no map marker": the provisional quests carry no verified marker coordinates, which is the projection's real output.

```
+----------------------------------------------------------------------------------------------+
|  MAIN QUEST                                                        Campaign Progress  36%    |
|                                                                    [#######-------------]    |
+---------------------------------+------------------------------------------------------------+
|  ACTS                           |  ACT III — FROZEN NORTH                                    |
|                                 |                                                            |
|  [v] ACT I — Survival     100%  |  Current Quest:  The Black Keep                 [ ACTIVE ] |
|  [v] ACT II — Ancient B.  100%  |  Boss:           Kinscourge                                |
|  [>] ACT III — Frozen N.   50%  |                                                            |
|        v Temple of Frost        |  Defeat the Kinscourge.                    <- Objective    |
|        > The Black Keep    *    |  Defeat Kinscourge at The Black Keep.      <- Description  |
|  [ ] ACT IV — Dark Powers   0%  |                                                            |
|  [ ] ACT V — Endgame        0%  |  Location:            The Black Keep                       |
|  [ ] Epilogue               0%  |  Recommended Level:   40–50                                |
|                                 |  Recommended Party:   2–5                                  |
|  ----------------------------   |  Reward:              1000 XP  (placeholder)               |
|  COMPLETED / HISTORY            |                                                            |
|   Abysmal Remnant  · Act I      |  [ TRACK QUEST ]   Marker: shown                           |
|   Witch Queen      · Act II     |                                                            |
|   Barrow King      · Act II     |  (status note, e.g. "Reach level 35 to begin.")            |
|   Temple of Frost boss · III    |                                                            |
+---------------------------------+------------------------------------------------------------+
  [v] completed act   [>] active act   [ ] locked act   * tracked quest
```

These values are the exact output of the projection for "MQ01–MQ04 complete, MQ05 current, level 48" on the provisional data. A test asserts them. The 58% in the brief was illustrative: 4 of 11 equal weights is 36%.

A **locked** quest selected in the left column (default `Partial` disclosure):

```
|  ACT IV — DARK POWERS                                                  [ LOCKED ] |
|  Quest:              Midnight Grove                                               |
|  Boss:               ???                                                          |
|  Location:           ???                                                          |
|  Recommended Level:  50–55        Recommended Party: 3–5                          |
|                                                                                   |
|  Complete the previous Main Quest to unlock this objective.                       |
```

## 3. States

### Quest status (derived, never stored)

| Status | Rule | Panel |
|---|---|---|
| `Completed` | The quest id is in `Completed` | Everything is shown. No Track button. |
| `Active` | It is the `CurrentQuestId` and not completed | Everything is shown, with the Track button. `Available` is false below `MinimumLevel`, and the status note then reads "Reach level N to begin." |
| `Locked` | Anything else | Masked by `LockedDisclosure`. The hint is always shown. No Track button. |

### Act status

| Status | Rule |
|---|---|
| `Completed` | Every enabled quest in the act is done. |
| `Active` | The act holds the current quest, or some of its quests are completed. |
| `Locked` | Otherwise. |

Act headers are never hidden.

### Locked disclosure (per quest, `lockedDisclosure` in the data)

| Value | Locked quest shows |
|---|---|
| `Full` | Everything (still not trackable) |
| `Partial` (default) | Title, recommended level and party. Boss, location, description, objective and reward are `null`, so the widget renders `???`. |
| `Hidden` | `???` title, act name and hint only |

Hidden fields are `null` in the view model, not just visually hidden, so a widget binding cannot leak them by accident.

**Limit:** the DataTable ships inside the client `.pak`, so disclosure is a *presentation* choice, not a secret from someone who extracts the pak. If real secrecy is wanted later, the server would replicate a masked view instead of the client building it. **Operator decision (2026-10-05): default `Partial`.**
- The act and the quest slot may be visible; the boss, location and reward stay hidden until the quest unlocks.
- No hidden data on the client is treated as secret.
- The server-authoritative quest state remains the source of truth.

## 4. Progress

Progress is based on weights, never on level. Percentages are **floored**, so a bar shows 100% only when everything is complete; 2 of 3 shows 66%, not 67%.
- **Campaign %** = completed enabled weight ÷ total enabled weight.
- **Act %** = the same, within the act.
- A completed quest that was later removed or disabled is **kept in History**, marked *retired*. It counts toward neither percentage.

The catalog validates acts **fail-closed** (data schema v2). It rejects:
- an unknown or duplicate `actId`;
- a duplicate act `order`;
- a blank act name;
- an act with no enabled quests;
- act order going backwards along the quest chain, i.e. interleaved acts.

## 5. Data bindings (view model → widget)

| Widget | Binds to |
|---|---|
| Header progress bar and label | `MainQuestView.CampaignProgress` / `CampaignProgressPercent`; `CampaignComplete` switches it to a "Campaign complete" state |
| Act list rows | `Acts[]`: `DisplayName`, `Status`, `ProgressPercent`, `CompletedQuests/TotalQuests` |
| Quest rows under an expanded act | `Acts[].Quests[]`: `Title`, `Status`, `IsTracked` |
| Main panel (default) | `Current` (null when the campaign is complete) |
| Main panel (row selected) | `Detail(questId)`. Selection is local UI state only |
| Main panel fields | `ActDisplayName`, `Title`, `BossName`, `Objective`, `Description`, `Location`, `RecommendedLevel`, `RecommendedParty`, `Reward[]` and `RewardIsPlaceholder`, `Status`, `StatusNote` |
| Track button | Visible when `CanTrack`; toggled when `IsTracked` |
| Marker line | `Tracking.MarkerVisible`, `Tracking.MarkerHiddenReason` |
| History list | `History[]`: `Title`, `BossName`, `ActDisplayName`, `CompletedAtUtc`, `Retired` |
| Staging/QA badge | `CreditEnabled == false` → "target unverified, no credit". Admins on staging only |

**Data flow:**
1. The server's mod controller owns each player's `PlayerQuestState` and persists it in the world DB.
2. It replicates that state to the **owning client only**.
3. On every replication update, the client rebuilds the view with the projection.
4. The tab never caches state of its own.

## 6. Track Quest

1. The button sends `Server_SetTrackedQuest(QuestId | None)`.
2. The server runs `CampaignEngine.SetTrackedQuest`:
   - `None` untracks.
   - Only the player's current, not-yet-completed quest can be tracked.
   - Anything else is rejected and nothing changes.
3. The server persists the state if `Changed` is true, then replicates it.
4. The button shows a pending state until the replicated state comes back. It does not flip optimistically.

Tracking only drives the marker and never affects progression. On completion:
- a tracked quest hands tracking to the next quest;
- a player who untracked stays untracked.

## 7. Notifications (event contract)

| Kind | Title | Subtitle | When |
|---|---|---|---|
| `QuestComplete` | QUEST COMPLETE | quest title | every completion |
| `CampaignMilestone` + `ActComplete` | CAMPAIGN MILESTONE | "<Act> complete" | the completion finished an act (not the last act) |
| `NewBossUnlocked` | NEW BOSS UNLOCKED | next boss name | a next quest exists |
| `CampaignMilestone` + `CampaignComplete` | CAMPAIGN MILESTONE | "Campaign complete" | the final quest; this replaces the act milestone and the unlock |

**Order:** QUEST COMPLETE → CAMPAIGN MILESTONE → NEW BOSS UNLOCKED.

**Payload:**
- `NotificationId`
- `PlayerId`
- `Kind`
- `Milestone`
- `QuestId`
- `ActId`
- `Title`
- `Subtitle`

**Delivery:**
- Each banner goes as a reliable Client RPC to the owning player.
- A HUD banner queue, separate from the tab, shows the banners in order and works whether or not the tab is open.
- The client drops a repeated `NotificationId`. The id is deterministic per completion: `source|player|kind[:milestone]|quest`.
- A duplicate boss-death event produces no banners.
- Banners are **not persisted or replayed**. After a reconnect, the tab shows the state, which is the record.

## 8. Marker and Simple Minimap

- The marker shows only when the tracked quest is the *available* current quest.
- The marker points to the general area (`AreaRadius`), never the exact boss position.
- The marker is restored from saved state alone.
- The quest system owns its marker. A Simple Minimap adapter is **optional**: it is disabled if Simple Minimap is absent or its interface changes, and Simple Minimap itself is never patched.
- Without the adapter, the tab still shows the location and the marker state.

## 9. Opening the tab (operator decision 2026-10-05; the entry point is confirmed in the Dev Kit)

1. **Preferred:** a tab in the Conan Enhanced menu, if the Dev Kit exposes a **supported** registration hook. To investigate: the Enhanced menu widgets and any mod-facing extension points.
2. **Fallback:** a standalone UMG panel opened by a **configurable** key binding.
3. **Not allowed:** patching vanilla widgets directly, unless it is proven unavoidable and approved first.

**Hotkey not chosen yet.** The pre-Dev Kit audit of the 13 mods is in [`INPUT_CONFLICT_AUDIT.md`](INPUT_CONFLICT_AUDIT.md):
- Simple Minimap holds F1, and uses Shift+click on the map.
- The console uses `~` and Insert.
- Improved Thralls & QoL has its own rebindable hotkey system with unknown defaults.

Candidates are recorded there. The final choice waits for Dev Kit steps C1–C3 ([`DEVKIT_CHECKLIST.md`](DEVKIT_CHECKLIST.md)) and a licensed-client check.

## 10. Planned Unreal assets (names only; NOT created)

| Asset | Purpose |
|---|---|
| `WBP_MQ_MainQuestTab` | Root panel (layout of §2) |
| `WBP_MQ_ActRow` / `WBP_MQ_QuestRow` | Left column |
| `WBP_MQ_QuestDetail` | Main panel |
| `WBP_MQ_History` | History list |
| `WBP_MQ_BannerQueue` / `WBP_MQ_Banner` | HUD notifications (§7) |
| `S_MQ_QuestView`, `S_MQ_ActView`, `S_MQ_Notification` | Structs mirroring the reference records |
| `DT_MQ_Acts`, `DT_MQ_Quests` | DataTables generated from `data/main-quests.provisional.v2.json` |

## 11. Open decisions

- ~~The default `LockedDisclosure`~~: decided, `Partial`, not treated as secrecy (§3).
- The tab entry point: decided as preference order (§9); confirm the supported hook in the Dev Kit (checklist C1).
- The key binding, after the conflict check (§9).
- The banner duration and stacking limit. This is a cosmetic choice, to make in-game.
