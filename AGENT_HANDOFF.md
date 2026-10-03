# Unattended Batch D handoff

## FINAL RESUME POINTER — 2026-10-03 14:30 +07:00

This STOP F was resolved. Authoritative current handoff: E:\github\gameee\AGENT_HANDOFF.md and FINAL_REPORT.md, branch claude/m3-task4-live-windows at 982d117 (pushed). SDK 8.0.425 installed user-locally; safety and merged builds clean, 410/410 tests, zero skipped. Original 0bb2b2a remains in history. Do not repeat SDK installation or integration.

Current stage is STOP B after D1: 15 new NPC stat-template errors appeared after readiness. D1 stopped normally (72.5 s), production rollback completed, seven-mod catalog/order restored. Latest verified seven-mod backup 2026-10-03_142750; pre-D1 restore point 142352; failed-D1 safety copy 142725. quick_check=ok, singleton counts=1/1/1, no running server/root/shipping/orphan/harness processes. D2/D3/final validation not started. Next action is human review of D1 error and harness scan gap. Production world NOT CREATED.

All content below is historical and superseded by the primary-checkout final handoff.

## Resume checkpoint (2026-10-03, approximately 14:20 +07:00)

- CURRENT BRANCH: codex/m3-batch-d-safety; preserved checkpoint 0bb2b2a remains in history.
- CURRENT HEAD: 0bb2b2a plus the Windows WAL-test sharing fix recorded by the next commit.
- CURRENT STAGE: Phase 1 safety validation PASS; ready for merge into claude/m3-task4-live-windows.
- LAST COMPLETED CHECKPOINT: SDK 8.0.425 installed from Microsoft's official ZIP into C:\Users\vkkha\AppData\Local\Microsoft\dotnet-sdk-8.0.425; SHA-512 matches official release metadata. Existing runtimes and global.json unchanged.
- BUILD/TEST: build 0 warnings, 0 errors; full suite 410 passed, 0 failed, 0 skipped. First run found a Windows sharing violation in the WAL fixture's File.ReadAllBytes; fixed by opening the fixture WAL with ReadWrite/Delete sharing, without changing production behavior. Final evidence: tests/ConanServerControl.Tests/TestResults/safety-resume-fixed.trx.
- CURRENT ACTIVE MODLIST / LATEST VERIFIED BACKUP / LAST SHUTDOWN CLASS: unchanged from the saved seven-mod checkpoint below; no live operation yet.
- SERVER STATE: no Conan server/root/shipping/harness process in elevated Win32_Process audit.
- NEXT ACTION: commit/push safety validation, merge into the live development branch, rebuild/retest, then validate sources and staging before D1.
- OPEN BLOCKERS: none at this checkpoint. Latest user resume request supersedes the previous run's elapsed 09:00 target.

The following sections preserve the earlier STOP F record as history.

- CURRENT LOCAL TIME: 2026-10-03T06:59:43.412125+07:00
- CURRENT BRANCH: codex/m3-batch-d-safety
- CURRENT HEAD: 46f8d02 (saved implementation checkpoint; the following documentation-only commit records this HEAD).
- WORKTREE: E:\github\gameee\.worktrees\batch-d
- CURRENT STAGE: STOP F, Phase 1 validation blocked by unavailable .NET SDK.
- LAST COMPLETED CHECKPOINT: Existing integration 2659af6 preserved; Git/process/modlist audit completed. QA-019/020 draft fixes and 12 regression cases added, not compiled or tested.
- CURRENT ACTIVE MODLIST: StackMe10K.pak, SavageParagon.pak, GritandGrease.pak, ThrallReputation.pak, ImprovedThrallsAndQoL.pak, WO_RidingThralls.pak, Ancient_Realms.pak (in this order).
- LATEST VERIFIED BACKUP: E:\CSC-M3-Live\app-data\backups\2026-10-03_033503. Manifest world SHA/size reverified; read-only quick_check=ok; mailbox/controller/AR controller counts=1/1/1. Main DB SHA-256 1FD6089F9A52E74A29FCB907225AD9D491F4B984BFE6562C252DFEA9F8264793. Live main DB hash matches. WAL=0 bytes; paired SHM=32768 bytes contains no WAL frames to replay. No backup or world files changed.
- SERVER STATE: OFFLINE, read-only Win32_Process audit found no Conan server/root/shipping/orphan or harness processes. No live operation performed in this run.
- LAST SHUTDOWN CLASS: NORMAL, historical 05:26:27 +07:00 record: 194.8 seconds; forced kill NO, orphan NO, Offline. No new shutdown.
- NEXT ACTION: Locate/restore the existing per-user SDK 8.0.425, then review the saved draft diff against 2659af6 and run dotnet build and dotnet test. Only after all tests pass may the safety branch be merged to claude/m3-task4-live-windows, rebuilt/retested, and Phase 2 pre-D1 validation begin. Do not repeat integration creation or completed Batch A/B/C.
- OPEN BLOCKERS: dotnet on PATH resolves to C:\Program Files\dotnet\dotnet.exe and reports no SDKs. Both build and test fail at SDK resolution; no tests executed. The documented per-user SDK was not located in checked standard directories. No SDK installation or global.json change attempted.

## Preserved Git state

Primary checkout remains claude/m3-task4-live-windows at 7455e7d. Its pre-existing untracked AGENTS.md, codex_prompt.txt, and .worktrees/ were preserved. Other registered worktrees were untouched. Fetch succeeded after sandbox elevation for Git metadata access. Safety branch started clean at 2659af6; no earlier safety changes existed.

## Draft implementation (NOT validated / NOT safe to merge yet)

QA-019: stopped-world reader checks process absence before file access and again before opening, refuses non-empty WAL and unpaired non-empty SHM as INCONCLUSIVE, and opens immutable read-only only for a checkpointed DB. A paired empty WAL has no frames, so leftover SHM is harmless. mod-boot short-circuits world gates after failed stop, and singleton reads short-circuit after an integrity failure. No WAL replay, deletion, repair, or live DB modification is performed.

QA-020: every LoadErrors marker is retained; only the full canonical message parses, with 1-16 hexadecimal ID digits. Malformed entries retain raw evidence and explicitly fail analysis, without entering attribution. Known hash/set gates are unchanged.

Regression cases added: seven stopped-world cases and five malformed LoadErrors cases. The full diff was statically reviewed; git diff --check passed. Compilation and test behavior remain unverified.
