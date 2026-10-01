# Conan Server Control — Current Status

## Last Updated
2026-10-01 17:10 UTC

## Current Milestone
M3 — Live Windows Integration & Diagnostics, **Task 1 only** (settings + process correctness).

Task 2 (readiness, cold backup, SQLite verification), Task 3 (Integration Diagnostics UI), Task 4 (LiveWindows harness), Delayed Restart, Wait Until Empty, Web Admin expansion, `app_info_print`, and Update Available / Verify-All were **not** started.

## Implemented and Verified

- Stabilized update / backup / restore pipeline (QA-001…016) on `b422e1f`
- Canonical RCON port is `Rcon.Port`. Legacy `Server.RconPort` deserializes only; `JsonSettingsService.LoadAsync` copies it when `Rcon.Port` is still default (AC3-6)
- RCON password is settable from Settings (write-only PasswordBox). Stored only via `ISecretProtector` / DPAPI on Windows. Blank input does not clear an existing password; Clear is explicit (AC3-6)
- `ProcessRunner` kills the started process tree on timeout **and** on caller cancellation after start (AC3-5 / L-8)
- Backup destinations must pass `PathValidator.IsUnderRoot` against `BackupsDirectory` (QA-013 / AC3-7)
- `ConanWorldFiles` enumerates Enhanced `game_0.db*` and legacy `game.db*` that actually exist. No recursion, no live SQLite open, no deletes

## Implemented but Not Fully Verified

- SteamCMD / live Conan / RCON on a real Windows host (M3 Task 4)
- DPAPI protection of the RCON password (Linux tests use a fake/dev protector)

## Partially Implemented

- Same deferred items as stabilization, plus M3 Tasks 2–5

## Placeholder / Mock / Stub

- Server INI editor, tray, wait-until-empty, scheduled backup/update, first-run wizard
- Integration Diagnostics UI (Task 3)
- LiveWindows harness (Task 4)

## Known Bugs

- None remaining from the confirmed P0/P1 stabilization list
- QA-011 (Online after 2 s) is deferred to M3 Task 2
- QA-013 tautological path check is fixed in this task

## Build Status

Solution build:
PASS
0 warnings
0 errors

Tests:
130 passed
0 failed
130 total

(Pre-existing 108 remain green. 22 Task 1 tests added.)

## Current Architecture

Unchanged gate/lease/`ModBatchTransaction`/`RollbackResult`/Web auth. Task 1 only added settings migration, secret UI wiring, process-tree kill-on-cancel, backup-root validation, and the world-file model.

## Current Blockers

- No live Conan dedicated server or SteamCMD on this Linux agent

## Next Recommended Work

M3 Task 2 — Core behaviour: readiness + cold backup + backup verification

## Last Completed Task

M3 Task 1 foundation: RCON settings/password, ProcessRunner cancel, QA-013, ConanWorldFiles.

## Current Task

Hand off Task 1 (`HANDOFF.md`). Do not start Task 2 until accepted.
