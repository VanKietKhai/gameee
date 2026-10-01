# Conan Server Control — Current Status

## Last Updated
2026-10-01 18:30 UTC

## Current Milestone
M3 — Live Windows Integration & Diagnostics, **Task 2 complete** (core behaviour).

Task 3 (Integration Diagnostics UI), Task 4 (LiveWindows harness), Delayed Restart, Wait Until Empty, Web Admin expansion, `app_info_print`, and Update Available / Verify-All were **not** started.

## Implemented and Verified

- Task 1 foundation (RCON, ProcessRunner cancel, QA-013, ConanWorldFiles)
- **QA-011:** `IServerReadinessProbe` + `StartCoreAsync` wait. Online requires a successful probe. A 2-second delay is not readiness.
- Configurable `Advanced.StartupReadyTimeoutSeconds` (default 600) and poll interval. Timeout → `Unresponsive`, **not** Online.
- Update / mod / everything pipeline: capture `wasRunning` → stop if required → confirm stopped → **cold verified backup** → mutate → start only if originally online → same readiness probe.
- Backup of known Conan world files records SHA-256 of the **backup copies**, writes a manifest, and runs read-only `PRAGMA quick_check` on the copied main DB.
- Safety backup failure aborts before SteamCMD / mod commit. Previously-online servers may be started again; originally-offline servers stay offline.

## Implemented but Not Fully Verified

- SteamCMD / live Conan / RCON on a real Windows host (M3 Task 4)
- Production `EndpointServerReadinessProbe` against a real dedicated-server port/RCON (unit-tested with a bound UDP port and fakes)

## Partially Implemented

- Manual BACKUP NOW while Online now stops, verifies a cold backup, then starts (same readiness path). Scheduled / delayed-restart backup-first is unchanged and still deferred with Delayed Restart.

## Placeholder / Mock / Stub

- Server INI editor, tray, wait-until-empty, scheduled backup/update, first-run wizard
- Integration Diagnostics UI (Task 3)
- LiveWindows harness (Task 4)

## Known Bugs

- None remaining from the confirmed P0/P1 stabilization list
- QA-011 (Online after 2 s) is **fixed** in this task
- Attach-to-existing-process still marks Online without the readiness probe (live attach is Task 4)

## Build Status

Solution build:
PASS
0 warnings
0 errors

Tests:
157 passed
0 failed
157 total
0 skipped

(Pre-existing 130 remain green. Task 2 added readiness, cold-order, and backup-verification tests.)

## Current Architecture

Unchanged gate/lease/`ModBatchTransaction`/`RollbackResult`/Web auth. Task 2 added mockable `IServerReadinessProbe` / `IBackupVerifier`, cold Stop→Backup pipeline order, and verified backup metadata.

## Current Blockers

- No live Conan dedicated server or SteamCMD on this Linux agent
- Standalone client at `D:\conan exiles\` is client-only and is not used by server management

## Next Recommended Work

M3 Task 3 — Integration Diagnostics & standalone-client-aware diagnostics

## Last Completed Task

M3 Task 2 core behaviour: readiness (QA-011), cold verified safety backups, pipeline abort-before-mutation.

## Current Task

Hand off Task 2 (`HANDOFF.md`). Do not start Task 3 until accepted.
