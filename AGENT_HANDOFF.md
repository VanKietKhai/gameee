# Batch D handoff — Shemite STOP (rolled back), 8-mod baseline accepted

- CURRENT LOCAL TIME: 2026-10-04 00:5x +07:00.
- CURRENT BRANCH: claude/m3-task4-live-windows (primary checkout E:\github\gameee).
- CURRENT HEAD: the docs commit after c0ef218 (fix: stop hiding new spawn-table ids behind the base-game noise rule); before it 4e09aa6 (immutable pre-batch plan), d1c31ff, 24393ee, 8002636 (Cannibal teardown set), c6bcef7 (immutable batch-analysis snapshots).
- PRESERVED SAFETY BRANCH: codex/m3-batch-d-safety at abe3875 (original checkpoint 0bb2b2a). Its worktree is E:\github\gameee\.worktrees\batch-d.
- CURRENT STAGE: Shemite City State (9th mod) STOP, rolled back, INCONCLUSIVE (gate-blocked). The accepted state is the 8-mod baseline (Cannibal Captivity accepted 2026-10-04). 9-mod core validation NOT RUN; boss/PvE mods NOT started. Tests 473 passed, 0 failed, 0 skipped; build 0 warnings/errors.
- LAST COMPLETED CHECKPOINT: Shemite batch (mod-boot --hold 660 --batch shemite-run-1, pre-batch plan shemite-run-1-pre): import PASS, readiness 41.6 s, order 1008, 1,999 packages, 661 s hold stable, quick_check ok, singletons 1/1/1, one new controller object; BLOCKED by 9 new unvalidated Shemite LoadErrors, 6 new spawn-table errors (scanner gap fixed in c0ef218) and a DEGRADED 319.6 s shutdown (cause not established; host free RAM was 450-1,100 MB). Nothing whitelisted. Rolled back to PRE-SHEMITE backup 2026-10-04_001912 and verified (live DB 0EE01DD4... and 8-mod modlist identical to the accepted state). Failed-batch world: POST-SHEMITE backup 2026-10-04_004137 and pre-restore backup 2026-10-04_004207.
- CURRENT ACTIVE MODLIST: StackMe10K.pak -> SavageParagon.pak -> GritandGrease.pak -> ThrallReputation.pak -> ImprovedThrallsAndQoL.pak -> WO_RidingThralls.pak -> Ancient_Realms.pak -> Cannibal_Captivity.pak (modlist.txt and catalog agree, all eight enabled; Cannibal SHA-256 DB6E3C299912E48E4DEC8293A58C8DEF1D881FDBDC44F1E67CE99A9BF348F04F).
- LATEST VERIFIED BACKUP: 2026-10-04_012406 (8-mod world after the final-validation attempt 1; hashes + quick_check ok). Pre-final 2026-10-04_010449; accepted 8-mod baseline 2026-10-04_004256 / _001912 / _001102 (identical DB). Older: 2026-10-03_235120, 2026-10-03_232338, 2026-10-03_142750.
- SERVER STATE: OFFLINE, clean process-tree exit; no Conan or harness process. (An old orphan findstr PID 28280 from before this work is still running; not touched.)
- LAST SHUTDOWN CLASS: DEGRADED, 307.9 s (final 8-mod validation attempt 1; acknowledged, exit code 0, forced kill NO, orphan NO; host CPU 79% average). Earlier: DEGRADED 319.6 s (Shemite run); the accepted 8-mod stops on a quiet host were NORMAL (181.7 s and 199.2 s).
- ACCEPTED WARNING GATES (ModBootGates): ITQoL mailbox x1; 7 Ancient Realms dangling refs; Ancient Realms MergeDataTables-null (exact, AR hash, max 2); Cannibal teardown set CANNIBAL-CAPTIVITY-TEARDOWN-NO-WORLD (exact Cannibal hash, exact message and object-path family, after main-world teardown only, exactly 99, all-or-nothing). Fantasy Races EXCLUDED/DEFERRED (its 15 NPC stat-template lines are NOT whitelisted; evidence kept under artifacts/batch-d-20261003/ and backup 2026-10-03_142725).
- ANALYSIS: every mod-boot now writes an immutable snapshot (E:\CSC-M3-Live\live-test\batch-snapshots\<batch>, read-only, log hash-verified). Replay with `analyze-snapshot <dir> [backupId] [--current-catalog]`; analyze-boot is live-catalog based and says so. Snapshots cannibal-run-1 (retroactive) and cannibal-run-2 exist.
- OPERATOR DECISION 2026-10-04: Shemite City State EXCLUDED / DEFERRED (do not rerun; do not whitelist its 9 LoadErrors or 6 spawn-table errors; keep all evidence, snapshots, backups). Core V1 = the 8 mods; Fantasy Races also excluded.
- FINAL 8-MOD CORE VALIDATION, ATTEMPT 1 (2026-10-04 01:0x, batch core8-final-1, plan core8-final-pre): readiness 71.3 s, hold 661.7 s, complete-log analysis PASS (0 unknown; ITQoL 23, AR 37, mailbox x1, AR merge x2, Cannibal 99 teardown all exact), quick_check ok, singletons 1/1/1, exit 0, no kill, no orphan. FAILED the criterion shutdown NORMAL/WARNING: 307.9 s DEGRADED while host CPU averaged 79% (a game was running; the same 8 mods stopped in 181.7 s and 199.2 s on a quiet host). Pre-final backup 2026-10-04_010449; post-run backup 2026-10-04_012406 (latest verified). Clean-restart validation NOT run. 8-mod state stays installed; no rollback needed.
- NEXT ACTION: retry the final 8-mod validation on a quiet host (ask the operator to close games and heavy apps first), including the clean restart; or the operator may accept the attempt-1 result with the host-load attribution. CORE MODPACK V1 SERVER-SIDE = NOT YET until then. Do not start boss/PvE mods before the operator reviews. In-game behavior of every mod is NOT verified (4F blocked by client authentication).
- OPEN BLOCKERS: shutdown criterion of the final validation (host-load attributed, unproven).

## SDK and validation

Official Microsoft SDK 8.0.425 installed at C:\Users\vkkha\AppData\Local\Microsoft\dotnet-sdk-8.0.425 using the official win-x64 ZIP; SHA-512 verified against Microsoft release metadata. global.json and existing runtimes unchanged. Invoke this directory's dotnet.exe explicitly (system PATH still selects the runtime-only host).

The first full safety test run had 409 passes and one Windows sharing failure in the WAL fixture's File.ReadAllBytes. Replaced only the fixture's file read with ReadWrite/Delete sharing; final safety and merged suites both pass 410/410. Saved evidence: tests/ConanServerControl.Tests/TestResults/safety-resume-fixed.trx in safety worktree, and batch-d-merged.trx in primary checkout.

## D1 evidence and rollback

Source: C:\Users\vkkha\Downloads\mod conan\FantasyRacesOfExiles.pak. Embedded metadata confirms Workshop 3780741325, Enhanced, version 1.0.6. Size 5,293,057 bytes; SHA-256 2E4D3BEEC95FCBB81A9622A42405D2C3694EE632446D89C57EA8A93E27667D4A.

Production import created verified pre-D1 backups 142352 and 142353. Eight mods mounted in intended order; readiness at approximately 39 seconds: game port bound and world ticking (frame 2). Exact 23 ITQoL + 37 Ancient Realms LoadErrors passed. The harness scan at 14:24:53 missed subsequent NPC errors, so harness exit 0 is NOT D1 acceptance.

The generic MergeDataTables null-table error occurs twice in both D1 and the prior seven-mod boot; it is not newly attributable to D1. Lamplighter attachment warnings also existed previously. The new NPC error is the STOP B basis.

After clean shutdown, production restore of 2026-10-03_142352 passed. Production removal reconciled the catalog and retired the D1 pak to E:\CSC-M3-Live\app-data\removed-mods\20261003-142751-337\FantasyRacesOfExiles.pak; source and archived hashes match. Extraction cache retained, inactive.

Restored live DB hash: 1FD6089F9A52E74A29FCB907225AD9D491F4B984BFE6562C252DFEA9F8264793; equals pre-D1 and latest verified backup. WAL=0 bytes; paired SHM=32768 bytes, no WAL frames. Backup quick_check=ok; mailbox/controller/AR controller=1/1/1. Modlist hash equals pre-D1: 4D48BF240CA224591BA9050C05D4CDFB3873E2F33A8C2E58C124FD40D0CB2D86.

D1 log preserved at E:\github\gameee\artifacts\batch-d-20261003\D1-ConanSandbox.log (SHA-256 AC751A4EA3782488809AEA012EF8608036AAAA58B417938E1DFFD75ED601522B). Structured evidence remains E:\CSC-M3-Live\live-test\m3-live-log.jsonl.

No verified backups deleted. Client unchanged. Production world NOT CREATED. Pre-existing untracked AGENTS.md, codex_prompt.txt and .worktrees/ preserved. No further live tests after STOP B.
