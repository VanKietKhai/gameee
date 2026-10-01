# Handoff to CONAN QA — QA-016 Retest

## QA-016

FIXED

## Failure Scenario

Update Everything with two Workshop mods:

1. Both mods downloaded and validated in staging.
2. Live commit began.
3. Mod 1 live replacement succeeded (NEW).
4. Mod 2 live replacement failed (destination was not a replaceable file).
5. The previously online dedicated server was started again.

Resulting live state was a mixed set: Mod1 = NEW, Mod2 = OLD. That is not acceptable. A multi-mod live commit must never start the server on a half-applied batch.

## New Transaction Behavior

`ApplyUpdatesAsync` now treats live replacement as one transaction (`ModBatchTransaction`):

1. **Stage** each target into a fresh `staging/workshop/<id>` folder and validate (exactly one non-empty current `.pak`).
2. **Re-validate** every staged pak before any live mutation.
3. **Prepare rollback copies** of every existing live target into an isolated directory: `staging/mod-update/<operation-id>/rollback/`. Stale rollback folders are never reused. The live file is copied, not deleted, so the original remains recoverable.
4. **Commit** replacements one at a time (`copy to *.new`, then `File.Replace` / `File.Move`). Settings, `modlist.txt`, and “installed” activity lines are written only after every live file succeeds.
5. **On any replacement failure**, restore every already-replaced pak from its rollback copy and verify the original live files exist. Cancellation is ignored once live mutation begins; the batch either finishes commit or finishes rollback.
6. **Server restart safety** (overrides ONLINE-before → ONLINE-after when the live set is unsafe):
   - Live commit fails **and** rollback verifies: previous mod set is restored. If the server was originally ONLINE it may be restarted onto that restored set. Log/activity: `Mod update failed. Previous mod set restored. Restarting server with previous versions.` The operation still returns FAILED.
   - Live commit fails **and** rollback fails or cannot be verified: **do not start** the server. Leave it OFFLINE. Title `Mod update failed — recovery required`. Log/activity: `Mod update failed and rollback was incomplete. Server was left offline to prevent starting with an inconsistent mod set.`
   - If the server was originally OFFLINE, it stays OFFLINE after a failed commit.

Steam dedicated-server binaries are **not** rolled back. If `app_update` succeeded and the mod live commit then failed, the overall operation is still FAILED. That partial state is logged. Rollback copies are deleted only after a fully successful batch (or a verified rollback); an incomplete rollback keeps the operation directory for recovery.

## Files Changed

- `src/ConanServerControl.Core/Exceptions/ModBatchCommitException.cs` — rollback-completed vs recovery-required
- `src/ConanServerControl.Infrastructure/Workshop/ModBatchTransaction.cs` — isolated rollback dir, replace, rollback, verify
- `src/ConanServerControl.Infrastructure/Workshop/WorkshopModService.cs` — transactional `ApplyUpdatesAsync`
- `src/ConanServerControl.Infrastructure/Updates/ServerUpdateService.cs` — restart only when the restored set is safe
- `src/ConanServerControl.Infrastructure/ConanServerControl.Infrastructure.csproj` — `InternalsVisibleTo` for the TEST D rollback-failure seam
- `tests/ConanServerControl.Tests/QaActionGateAndPipelineTests.cs` — existing QA-016 regression kept
- `tests/ConanServerControl.Tests/Qa016ModBatchTransactionTests.cs` — TEST A–F
- `STATUS.md`
- `HANDOFF.md`

Previous QA-001…012 / QA-008 / QA-009 / QA-010 fixes were not modified except as required for this commit/restart path.

## Regression Tests

Existing QA-016 case kept (not weakened): `Partial_live_commit_must_not_restart_onto_a_mixed_mod_set`.

Added:

- TEST A — 2 mods, Mod2 replace throws → both OLD, operation FAILED
- TEST B — 3 mods, Mod3 fails → all OLD, operation FAILED
- TEST C — replace fails, rollback succeeds, originally ONLINE → old set restored, server may return ONLINE, operation FAILED
- TEST D — replace fails, rollback also fails, originally ONLINE → server stays OFFLINE, recovery-required status/log
- TEST E — all replacements succeed → all NEW, rollback cleaned, success
- TEST F — originally OFFLINE, commit failure + successful rollback → remains OFFLINE

Do not delete the `QA-KnownFailure` traits; they remain as issue tags.

## Build

PASS
0 warnings
0 errors

## Tests

104 passed
0 failed
104 total

(Baseline was 98 total / 97 passed / 1 failed. The failing QA-016 case now passes. Six additional QA-016 cases were added. None of the previous tests were deleted, skipped, or weakened.)

## Live Windows Tests Still Required

SteamCMD:
Windows host, real `steamcmd.exe`, `app_update 443030`, Workshop download of a known item.

Real Conan server:
Start / stop / Update Everything with two installed mods. Confirm a forced live-replace failure (locked file, ACL, or destination conflict) restores every pak and does not start on a mixed set. Confirm incomplete rollback leaves the process stopped.

Workshop:
Item with one `.pak`; Update Everything with two mods where the second live path cannot be replaced.

Web Admin:
Login, CSRF header on Update Mods/Server from the real `site.js`, 401 when signed out.

## Known Limitations

- Steam dedicated-server binaries are not rolled back if they update successfully and a later mod commit fails. A pre-update backup is still taken when that setting is on.
- SteamCMD is still not killed when the HTTP request is aborted (Architect F-9 remainder).
- Pre-update backup still runs before stop (live SQLite world).
- Enable/Disable/Move still rewrite `modlist.txt` without the destructive gate.
- Update All still re-downloads every enabled mod.
- No live SteamCMD / Conan / Workshop verification on this Linux agent.
- TEST D uses an internal `AfterLiveReplacementForTests` seam to destroy rollback copies; production code does not branch on “is test”.

Do **not** start Delayed Restart, Wait Until Empty, `app_info_print`, First Run Wizard, new Web Admin features, or the Update Available vs Verify All split until this QA-016 retest PASSes.
