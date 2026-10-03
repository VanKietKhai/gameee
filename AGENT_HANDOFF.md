# Batch D handoff — STOP B

- CURRENT LOCAL TIME: 2026-10-03 14:29 +07:00.
- CURRENT BRANCH: claude/m3-task4-live-windows (primary checkout E:\github\gameee).
- CURRENT HEAD: d8908ee (scanner fix) on top of 982d117; later commits on this branch are the harness read-only-path tweak and this handoff. Takeover by Claude at 2026-10-03 22:55 +07:00 preserved Codex's WIP as-is and extended it.
- PRESERVED SAFETY BRANCH: codex/m3-batch-d-safety at abe3875, containing original checkpoint 0bb2b2a. Its worktree is E:\github\gameee\.worktrees\batch-d.
- CURRENT STAGE: STOP B after D1; rollback completed and verified. Do not start D2/D3.
- LAST COMPLETED CHECKPOINT: QA-019/020 compiled and tested; merged branch compiled and tested; both full runs 410 passed, 0 failed, 0 skipped, builds 0 warnings/errors. D1 boot/stop completed, then failed full-log review.
- CURRENT ACTIVE MODLIST: StackMe10K.pak -> SavageParagon.pak -> GritandGrease.pak -> ThrallReputation.pak -> ImprovedThrallsAndQoL.pak -> WO_RidingThralls.pak -> Ancient_Realms.pak. Catalog and modlist agree, all seven enabled.
- LATEST VERIFIED BACKUP: 2026-10-03_142750 (restored seven-mod world; manifest/hash verification and quick_check=ok). Known pre-D1 restore point: 2026-10-03_142352. Failed-D1 state preserved by automatic pre-restore backup 2026-10-03_142725.
- SERVER STATE: OFFLINE, clean process-tree exit; no root/shipping/orphan/harness process.
- LAST SHUTDOWN CLASS: NORMAL, 72.5 seconds, acknowledged, exit code 0, forced kill NO, orphan NO, no WAL/SHM left after D1 stop.
- OPERATOR DECISION 2026-10-03: Fantasy Races EXCLUDED / DEFERRED from V1 (do not re-run, do not whitelist the 15 lines, keep D1 evidence). AR MergeDataTables-null ACCEPTED as known non-blocking (exact signature, AR hash 12F7E719..., max 2 per boot).
- NEXT ACTION: live batch Cannibal Captivity (Workshop 3765743138) as the 8th mod; if PASS, then Shemite City State; any UNKNOWN error / integrity failure / DEGRADED shutdown = rollback and STOP. Scanner gap is fixed (d8908ee, 440/440 tests).
- D1 CLASSIFICATION: INCONCLUSIVE. 15 identical NPC stat-template lines, 0 in all 11 earlier healthy boots, only novel Error kind in the D1 boot; Fantasy Races ships StatModifier/StatTemplate/NPC-stat data tables (plausible, unproven attribution); one boot only; stat effect unknown; no crash/integrity/persistence evidence (D1 world quick_check ok, 1 FROE controller). Not a known warning. Expect any D1 rerun to FAIL the gate on those 15 lines unless the operator accepts them in committed code.
- OPEN BLOCKERS: none for the seven-mod baseline. D1 is closed by exclusion (historical classification INCONCLUSIVE).

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
