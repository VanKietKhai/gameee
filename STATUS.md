# Conan Server Control — Current Status

## Last Updated
2026-10-02

## Current Milestone
M3 — Live Windows Integration & Diagnostics, **Task 3 complete** (Integration Diagnostics).

Task 4 (guarded LiveWindows integration) has **not** started. Delayed Restart, Wait Until Empty, Web Admin expansion, `app_info_print`, and Update Available / Verify-All are also not started.

## Implemented and Verified

- Task 1 foundation: RCON, ProcessRunner cancel, QA-013, ConanWorldFiles.
- Task 2: readiness probe (QA-011), cold verified backups, abort-before-mutation.
- **Task 3:** read-only `IIntegrationDiagnosticsService` covers:
  - System
  - SteamCMD
  - Dedicated Server
  - Standalone Client
  - Network
  - RCON
  - World / Saves
  - Mods
  - Backups

  Results are PASS / WARNING / FAIL / NOT CONFIGURED / NOT TESTED, each with an explicit evidence label. Nothing is reported as live verified.
- The standalone client `ConanSandbox.exe` and the `Run Me!.bat` launcher are rejected (FAIL) as the dedicated-server executable.
- Optional, configurable standalone client root (`Client.RootDirectory`). It is detected read-only and never required for server readiness.
- Readiness verdicts: server live test, and client compatibility test.
- Redacted JSON + Markdown export to `%DATA%\diagnostics`. Tests prove that known secrets and the protected blob never appear.
- The Diagnostics page shows per-category badges, evidence labels, readiness at the bottom, export, and open folder.
- Verified on this Windows host:
  - against the real client root `D:\conan exiles\Conan Exiles Enhanced`, read-only, with 0 file changes
  - in the running app, using an isolated data directory

## Implemented but Not Fully Verified

- SteamCMD / live Conan / RCON / Workshop on a real Windows host (M3 Task 4).
- Production `EndpointServerReadinessProbe` against a real dedicated server.

## Partially Implemented

- Manual BACKUP NOW while Online: stop → verified cold backup → start. Scheduled / delayed-restart backup-first is still deferred.

## Placeholder / Mock / Stub

- Server INI editor, tray, wait-until-empty, scheduled backup/update, first-run wizard.
- LiveWindows harness (Task 4).
- QA-017 `UPDATE EVERYTHING` rename, Dashboard `STARTING` label, Backups Verified badge (architect Task 3 UI items; not in this assignment).

## Known Bugs

- None remaining from the confirmed P0/P1 stabilization list.
- Attach-to-existing-process still marks Online without the readiness probe (Task 4).
- Pre-existing: the Settings Web Admin password field is a plain TextBox, so it is visible while typing.

## Build Status

Solution build (`dotnet build -c Release`, SDK 8.0.425):
PASS
0 warnings
0 errors

Tests (`dotnet test -c Release`):
203 passed
0 failed
203 total
0 skipped

The 157 earlier tests remain green. On Windows, 2 of them first failed because of host-specific test-harness behaviour; these were fixed without weakening them (see HANDOFF Baseline). Task 3 added 46 test cases.

## Current Architecture

Unchanged gate/lease/`ModBatchTransaction`/`RollbackResult`/Web auth/readiness/cold-backup pipeline.

Task 3 is additive:

- `Core/Diagnostics` holds the models, classifier, readiness calculator, redactor, and formatter.
- `Infrastructure/Diagnostics/IntegrationDiagnosticsService` runs the checks. It never takes the gate, never starts or stops anything, never executes SteamCMD, never opens the live DB, and never writes except on explicit export.

## Current Blockers

- SteamCMD is not installed on this host. This is the only server-live readiness blocker reported by diagnostics.
- No dedicated server is installed yet. That is acceptable, because the live test installs into a safe workspace.
- The host has only a per-user .NET 8 SDK/ASP.NET Core runtime (`%LOCALAPPDATA%\Microsoft\dotnet`). Running the App needs `DOTNET_ROOT` pointing there, or a system-wide runtime.

## Next Recommended Work

M3 Task 4 — guarded Live Windows integration.

## Last Completed Task

M3 Task 3: Integration Diagnostics & standalone-client-aware diagnostics.

## Current Task

Hand off Task 3 (`HANDOFF.md`). Do not start Task 4 until accepted.
