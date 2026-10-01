# Handoff to CONAN QA — QA-016 Final Retest

## Root Cause

`FilesEqual` / rollback verification exceptions escaped the rollback safety boundary.

`TryRollback` only caught `IOException`, `UnauthorizedAccessException`, and `InvalidOperationException` around **restore** copies. `VerifyOriginals` then called `FilesEqual`, which can throw (`UnauthorizedAccessException` on an unreadable rollback copy, `IOException`, missing file during compare, etc.). That exception left `TryRollback`, never became `ModBatchCommitException` with recovery-required, and landed in `ServerUpdateService`'s generic catch.

That generic catch treated “no batch failure object” as CASE 1 (safe previous set) and restarted a previously online server. Live state could remain mixed (Mod1 NEW / Mod2 OLD) with no recovery-required reporting.

## Fix

Rollback restore, verification, and file comparison now share one fail-closed boundary:

- `TryRollback` returns `RollbackResult` (`Attempted`, `Succeeded`, `Verified`, `Error`) and does not throw.
- Any exception during restore, `VerifyOriginals`, or `FilesEqual` becomes an unverified/failed rollback.
- `ApplyUpdatesAsync` also wraps `TryRollback` and any unexpected exception after live mutation into `ModBatchCommitException` with `RecoveryRequired`.
- `ServerUpdateService` restarts only when there was **no** live-mod mutation, or when rollback was **positively** attempted, completed, and verified.

## Restart Safety Rule

Restart is permitted only when:

- the failure happened **before** live mod mutation (previous live set untouched), or
- `rollbackAttempted && rollbackCompleted && rollbackVerified` (previous live set proven restored).

It is **not** permitted merely because rollback did not return `false`.

## Recovery Required Rule

Any rollback failure, verification failure, or rollback/verification exception leaves the server **OFFLINE**.

Title: `Mod update failed — recovery required`

Log: `Mod update failed and rollback could not be verified. Server was left offline to prevent starting with an inconsistent mod set.`

The underlying exception is written to structured logs. The operation reports FAILED. It never reports update completed or rollback completed unless verification succeeded.

## Tests

QA regression kept unchanged: `Rollback_verify_throw_leaves_previously_online_server_offline`.

Also covering:

- TEST 1 / A / C — rollback succeeds and verifies; originally ONLINE server may restart; operation FAILED
- TEST 2 / D — rollback returns/verifies false; server OFFLINE; recovery required
- TEST 3 — rollback verification throws (QA chmod / unreadable copy); server OFFLINE; recovery required; no restart
- TEST 4 — rollback copy I/O throws (exclusive lock); server OFFLINE; recovery required
- TEST 5 — verified rollback then restart itself fails; operation remains FAILED
- TEST 6 — failure before live mutation; originally ONLINE server may restart; not recovery-required

## Build

PASS
0 warnings
0 errors

## Tests

108 passed
0 failed
108 total

(Baseline was 105 total / 104 passed / 1 failed. The failing QA verification-throw case now passes. Additional TEST 4–6 were added. No tests were deleted, skipped, or weakened.)

## Remaining Live Tests

Windows host, real `steamcmd.exe`, locked or ACL-denied `.pak` during live commit. Confirm a verification/IO throw leaves ConanSandboxServer stopped and surfaces recovery required. Confirm a fully verified rollback may restart onto the old set and still reports FAILED.

Do **not** start Delayed Restart, Wait Until Empty, `app_info_print`, wizard, new Web Admin work, or the Update Available split until this retest PASSes.
