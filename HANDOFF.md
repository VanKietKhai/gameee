# M3 — Task 2 Core Behaviour Handoff

## Baseline

130 / 130

Confirmed on `cursor/m3-live-windows-integration-a853` before Task 2 edits (`90ee3bd`): Release build 0 warnings / 0 errors; 130 tests passed.

## Server Readiness

`IServerReadinessProbe` is injected into `ServerProcessManager`. It is not wired into WPF ViewModels.

`StartCoreAsync` lifecycle is now:

Offline → Starting → process launched → probe loop → Online

The previous `Task.Delay(2s)` then Online path is gone.

Production probe (`EndpointServerReadinessProbe`): ready when the configured game UDP port is bound **or** (optional) RCON ping succeeds. RCON password is **not** required for basic readiness. Process-alive is owned by the process manager.

Test seam: `ImmediateReadyProbe` / `ScriptedReadinessProbe`.

## QA-011

Fixed.

- Process starts, probe becomes ready → Starting → Online
- Probe never ready + timeout → `ServerStatus.Unresponsive`, **not** Online
- Process exits during startup → startup failure (`Error`), not Online
- Transient not-ready then ready → Online
- Cancellation during readiness wait → `OperationCanceledException`, not Online
- Probe throws → logged, treated as not-ready, retried until ready or timeout; never fake Online
- Post-update `StartUnderLockAsync` uses the same probe; Online is not reported before ready

**Override vs Architect AC3-2:** Architect text said timeout → Online + Unresponsive. The Task 2 assignment requires timeout **must not** report Online. Implemented: `Status=Unresponsive`, `Health=ServerUnresponsive`, explicit `UserFacingException`. UI is not left at Starting.

## Cold Backup Ordering

`RunLockedAsync` now:

1. Acquire lease
2. Capture `wasRunning`
3. Stop if running and **confirm** Offline/Error (otherwise abort; no backup, no mutation)
4. Cold backup + hash + SQLite verify (when backup-before-update is enabled)
5. Server update and/or transactional mod commit
6. Start only if originally online **and** resulting state is safe
7. Same readiness probe; Online is required for a successful return-to-online

`UpdatePipelineStateMachine` allows `Stopping → Backup`. Happy path tests record `stop → backup → update → start`.

`StopCoreAsync` no longer claims stopped if the managed process is still alive after the force-stop wait.

Manual BACKUP NOW while Online: stop → verified cold backup → start (same lease). Offline: backup → verify → remain offline. A live SQLite copy is rejected (`EnsureServerStoppedForColdBackup`).

## Backup Manifest

Each completed backup `metadata.json` / `BackupRecord` includes:

- `CreatedAt`
- `WorldType` (`Enhanced` / `Legacy`)
- `MainDbFileName`
- `WorldFiles`: logical name, filename, size, SHA-256 of the **backup copy** (streaming `SHA256.HashData`)
- `ManifestWritten`, `HashesVerified`, `SqliteVerified`, `VerificationDetail`, `VerifiedAt`, `Succeeded`

Known files only via `ConanWorldFiles` (`game_0.db*` / `game.db*`). Unrelated `.db` files may still sit in the copied Saved tree but are not in the world manifest.

## SQLite Verification

`IBackupVerifier` / `SqliteBackupVerifier` opens the **copied** main DB read-only (`Microsoft.Data.Sqlite` `Mode=ReadOnly`) and runs `PRAGMA quick_check`. Success requires a single `ok` row.

Invalid when: main DB missing, cannot open, quick_check is not `ok`, hash/size mismatch, hashing throws.

WAL/SHM are copied when present and hashed; absence of WAL/SHM is not a failure. Live WAL/SHM are never deleted, checkpointed, VACUUMed, or migrated.

**"Backup completed"** is logged only when copy + manifest + hashes + SQLite verification all succeeded.

## Update Pipeline Changes

Update Everything remains one coordinated run: one stop, one cold verified safety backup, server update, mod batch (`ModBatchTransaction` / `RollbackResult` unchanged), one restart if originally online and safe, then readiness.

Recovery-required unverified rollback still leaves the server **offline**.

## Failure Semantics

| Failure | Mutation | Server state |
| --- | --- | --- |
| Cannot stop | none | unchanged (still running) |
| Backup copy / hash / `quick_check` fails | none | was-online **may** restart; was-offline stays offline. Log: `update aborted because safety backup failed.` |
| Server update fails | binaries may have changed | preserve backup; restart if was-online and not recovery-required |
| Mod commit fails | existing transactional rollback rules | restart only if rollback verified; otherwise recovery-required / OFFLINE |
| Post-update readiness fails | mutation already applied | process may exist; status is **not** Online; operation FAILED/unresponsive; no second start attempt |

## Files Changed

- `src/ConanServerControl.Core/Abstractions/IServerReadinessProbe.cs`
- `src/ConanServerControl.Core/Abstractions/IBackupVerifier.cs`
- `src/ConanServerControl.Core/Backups/BackupFileHasher.cs`
- `src/ConanServerControl.Core/Backups/ConanWorldFiles.cs`
- `src/ConanServerControl.Core/Models/WorkshopMod.cs` (`BackupRecord` / `BackupWorldFileRecord`)
- `src/ConanServerControl.Core/Settings/AppSettings.cs`
- `src/ConanServerControl.Core/AppConstants.cs`
- `src/ConanServerControl.Core/Updates/UpdatePipelineStateMachine.cs`
- `src/ConanServerControl.Infrastructure/Health/EndpointServerReadinessProbe.cs`
- `src/ConanServerControl.Infrastructure/Health/ServerHealthService.cs`
- `src/ConanServerControl.Infrastructure/ProcessManagement/ServerProcessManager.cs`
- `src/ConanServerControl.Infrastructure/Backups/BackupService.cs`
- `src/ConanServerControl.Infrastructure/Backups/SqliteBackupVerifier.cs`
- `src/ConanServerControl.Infrastructure/Updates/ServerUpdateService.cs`
- `src/ConanServerControl.Infrastructure/ServiceCollectionExtensions.cs`
- `src/ConanServerControl.Infrastructure/ConanServerControl.Infrastructure.csproj`
- tests listed below
- `STATUS.md`, `HANDOFF.md`

## Tests Added

Readiness: delayed success, timeout, process exit, transient failure, cancellation, probe exception, always-throw timeout, post-update not-Online-before-ready, UDP port probe, RCON not required.

Cold order: online Stop→Backup→Update→Start; Update Everything one cycle; offline no stop/start; backup failure blocks mutation and restarts previously-online server; cannot-stop aborts with no backup/update.

Backup verification: Enhanced main DB; Enhanced+WAL/SHM when present; Legacy; missing main DB; non-SQLite; quick_check/corrupt; hash manifest + mismatch revalidation; unrelated `.db` ignored; missing copied main; manual Online backup stop/backup/start.

Tiny temp SQLite fixtures only. No real Conan save. No skipped tests.

## Build

PASS
0 warnings
0 errors

## Tests

157 / 157

130 previous + Task 2 tests. 0 failed. 0 skipped.

## Architect Acceptance Criteria

Mapped to M3 ACs from `ARCHITECTURE_REVIEW.md` (branch `architect/review-2`, not merged):

| AC | Task 2 status |
| --- | --- |
| AC3-1 0 warnings; prior tests green; none skipped/weakened | **Met** (130 remain; 157 total) |
| AC3-2 QA-011 Starting until ready; timeout behaviour | **Met with Task 2 override:** timeout is **not** Online (assignment). Fake never-ready → Unresponsive. Probe ready → Online within poll interval. |
| AC3-3 online order Stop→Backup→work→Start; backup-fail-after-stop restarts, no mutation | **Met** |
| AC3-4 `game_0.db` set hashed; valid `quick_check` verified; corrupt/truncated invalid | **Met with Task 2 override:** invalid backup **fails the operation** and blocks mutation (assignment). Architect “Unverified but continue” is **not** used for safety backups. Failed verified backups keep metadata and are not logged as completed. |
| AC3-5 ProcessRunner cancel kills tree | **Met in Task 1** (unchanged) |
| AC3-6 RCON port migration + secret protector | **Met in Task 1**. Report redaction is Task 3. |
| AC3-7 QA-013 backup root | **Met in Task 1** |
| AC3-8 Integration diagnostics | **Deferred to Task 3** |
| AC3-9 LiveWindows skips/guards | **Deferred to Task 4** |
| AC3-10 QA-017 UPDATE EVERYTHING label | **Deferred to Task 3** (pipeline behaviour already single-cycle; offline stays offline) |
| AC3-11 LIVE LV-01…14 | **Deferred to Task 4** |

## Deferred to Task 3

IntegrationDiagnosticsService
Diagnostics UI
report redaction
standalone client path diagnostics
STARTING label / Verified badge / UPDATE EVERYTHING rename (QA-017 UI)

## Deferred to Task 4

real SteamCMD
real Conan Dedicated Server
real Workshop
real .pak
standalone client join/mod compatibility
attach-to-existing-process readiness
LiveWindows harness

## Known Limitations

- Production readiness uses game UDP bind and optional RCON ping, not a full “players can join” check.
- Attach-to-existing process still sets Online without probing.
- `ServerHealthService` 20-second “still starting” heuristic was removed; health now follows status + port/RCON.
- Delayed Restart backup-first was not redesigned (feature deferred).
- Linux agent did not launch `ConanSandboxServer.exe`, `ConanSandbox.exe`, `Run Me!.bat`, or SteamCMD. Standalone client `D:\conan exiles\` is client-only and was not referenced by server management.
