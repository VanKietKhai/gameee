# Final report — resumed Batch D run

- FINAL STATUS: STOP B. D1 failed full-log review; rollback to the seven-mod state completed and verified.
- REPORT LOCAL TIME: 2026-10-03 14:29 +07:00.
- CURRENT BRANCH/HEAD: claude/m3-task4-live-windows, validated merge 09cbf1a; this report is recorded in a subsequent documentation commit.
- PRESERVED CHECKPOINT: codex/m3-batch-d-safety contains 0bb2b2a and validated successor abe3875.
- SDK: exact 8.0.425 installed user-locally from official Microsoft ZIP, SHA-512 verified; global.json and existing SDK/runtime installations unchanged.
- TEST COUNT: 410 passed, 0 failed, 0 skipped on safety branch and again after merge. Both final builds: 0 warnings, 0 errors.
- D1 RESULT: FAIL / STOP B. Fantasy Races 1.0.6 mounted as mod 8; true readiness about 39 s; exact known LoadErrors passed. Full log then showed 15 new NPC stat-template errors.
- D2 RESULT: NOT STARTED because D1 failed.
- D3 RESULT: NOT STARTED because D1 failed.
- FINAL 10-MOD RESULT: NOT STARTED.
- FINAL MODLIST: StackMe10K.pak -> SavageParagon.pak -> GritandGrease.pak -> ThrallReputation.pak -> ImprovedThrallsAndQoL.pak -> WO_RidingThralls.pak -> Ancient_Realms.pak.
- LATEST VERIFIED BACKUP: 2026-10-03_142750, restored seven-mod world. Rollback source: pre-D1 2026-10-03_142352. Failed-D1 world preserved in pre-restore safety backup 2026-10-03_142725.
- LATEST QUICK_CHECK: ok; mailbox/controller/AR controller=1/1/1. Live restored DB hash matches verified backup; WAL empty, paired SHM only.
- LATEST SHUTDOWN RESULT: 72.5 seconds, NORMAL, acknowledged, exit 0, no forced kill, no orphan, no WAL/SHM immediately after stop.
- SERVER CURRENT STATE: OFFLINE.
- ROLLBACKS: production restore of 142352; production removal of FantasyRacesOfExiles.pak restored seven-mod catalog. Package recoverable from removed-mods archive; original source intact.
- KNOWN WARNINGS: exact hash-bound ITQoL mailbox/23 LoadErrors and Ancient Realms/37 LoadErrors exceptions unchanged.
- OPEN BLOCKERS: "NPC: Error: Data: No stat templates found for StatModifier template None." occurred 15 times in D1, zero in the pre-D1 seven-mod reference log. Cause/attribution unproven. The harness scans too early to catch these later generic errors; its exit 0 was overridden by full-log review. The existing generic MergeDataTables null-table errors and Lamplighter warnings must not be misrepresented as new D1 findings.
- GAMEPLAY VALIDATION STATUS: NOT VERIFIED; authentication/FLS blocker unchanged.
- PRODUCTION WORLD STATUS: NOT CREATED.
- NEXT HUMAN ACTION: review the new NPC error and authorize investigation of D1 and the harness scan coverage before another live validation attempt.

## Evidence

D1 source identity confirmed from embedded metadata: Workshop 3780741325, Enhanced, version 1.0.6; SHA-256 2E4D3BEEC95FCBB81A9622A42405D2C3694EE632446D89C57EA8A93E27667D4A.

Full D1 log: artifacts/batch-d-20261003/D1-ConanSandbox.log, SHA-256 AC751A4EA3782488809AEA012EF8608036AAAA58B417938E1DFFD75ED601522B. Original structured harness evidence: E:\CSC-M3-Live\live-test\m3-live-log.jsonl. Test evidence: safety-resume-fixed.trx in safety worktree and batch-d-merged.trx in primary checkout.

Official SDK archive: https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.425/dotnet-sdk-8.0.425-win-x64.zip. Release hash source: https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json.

No repeated Batch A/B/C boot tests, no new warning suppression, no deleted verified backups, no client modifications, no public exposure, no production-world creation.
