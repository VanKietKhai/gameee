# Batch D handoff — Cannibal Captivity ACCEPTED, Shemite next

- CURRENT LOCAL TIME: 2026-10-04 00:2x +07:00.
- CURRENT BRANCH: claude/m3-task4-live-windows (primary checkout E:\github\gameee).
- CURRENT HEAD: the docs commit after 8002636 (feat: accept the validated Cannibal Captivity teardown warning set); before it c6bcef7 (immutable batch-analysis snapshots) and a4d63e8/310c701 (operator decisions, first Cannibal STOP).
- PRESERVED SAFETY BRANCH: codex/m3-batch-d-safety at abe3875 (original checkpoint 0bb2b2a). Its worktree is E:\github\gameee\.worktreesatch-d.
- CURRENT STAGE: Cannibal Captivity (8th mod) ACCEPTED server-side after a controlled rerun. Shemite City State is next and NOT started. Tests 470 passed, 0 failed, 0 skipped; build 0 warnings/errors.
- LAST COMPLETED CHECKPOINT: Cannibal Captivity rerun (mod-boot --hold 600 --batch cannibal-run-2): readiness 35.4 s, 600.5 s hold, shutdown 181.7 s NORMAL, exit 0, no kill/orphan, quick_check ok, mailbox/ITQoL ctrl/AR ctrl 1/1/1; exactly 99 teardown warnings, identical to run 1, none before shutdown, no other unknown line.
- CURRENT ACTIVE MODLIST: StackMe10K.pak -> SavageParagon.pak -> GritandGrease.pak -> ThrallReputation.pak -> ImprovedThrallsAndQoL.pak -> WO_RidingThralls.pak -> Ancient_Realms.pak -> Cannibal_Captivity.pak (modlist.txt and catalog agree, all eight enabled; Cannibal SHA-256 DB6E3C299912E48E4DEC8293A58C8DEF1D881FDBDC44F1E67CE99A9BF348F04F).
- LATEST VERIFIED BACKUP: 2026-10-04_001102 (post-rerun 8-mod world; hashes + quick_check ok). Seven-mod rollback point before Cannibal: 2026-10-03_235120 (rerun) / 2026-10-03_232338 (first run). Older: 2026-10-03_142750.
- SERVER STATE: OFFLINE, clean process-tree exit; no Conan or harness process. (An old orphan findstr PID 28280 from before this work is still running; not touched.)
- LAST SHUTDOWN CLASS: NORMAL, 181.7 s, acknowledged, exit code 0, forced kill NO, orphan NO.
- ACCEPTED WARNING GATES (ModBootGates): ITQoL mailbox x1; 7 Ancient Realms dangling refs; Ancient Realms MergeDataTables-null (exact, AR hash, max 2); Cannibal teardown set CANNIBAL-CAPTIVITY-TEARDOWN-NO-WORLD (exact Cannibal hash, exact message and object-path family, after main-world teardown only, exactly 99, all-or-nothing). Fantasy Races EXCLUDED/DEFERRED (its 15 NPC stat-template lines are NOT whitelisted; evidence kept under artifacts/batch-d-20261003/ and backup 2026-10-03_142725).
- ANALYSIS: every mod-boot now writes an immutable snapshot (E:\CSC-M3-Live\live-testatch-snapshots\<batch>, read-only, log hash-verified). Replay with `analyze-snapshot <dir> [backupId] [--current-catalog]`; analyze-boot is live-catalog based and says so. Snapshots cannibal-run-1 (retroactive) and cannibal-run-2 exist.
- NEXT ACTION: report to the operator, then (on approval) Shemite City State (Workshop 3755371705): same cycle, rollback and STOP on any unknown error, integrity failure or DEGRADED/EMERGENCY shutdown. In-game behavior of every mod, including Cannibal Captivity, is NOT verified (4F blocked by client authentication).
- OPEN BLOCKERS: none for the 8-mod baseline.

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
