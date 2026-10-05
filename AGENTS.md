# Conan Server Control - Codex Instructions

## Repository
Work only from this repository unless the current task explicitly requires
access to the staging Conan server or backup paths.

Primary repo:
E:\github\gameee

## Safety
Never:
- expose RCON publicly
- configure port forwarding or UPnP
- delete verified backups
- run git reset --hard
- run git clean
- force-push
- manually edit Conan SQLite save data
- continue after unexplained integrity corruption

## Git
Before changing anything:
- git fetch
- git status
- git branch --show-current

After every major successful checkpoint:
- update project docs
- commit
- push
- keep the working tree clean

Do not overwrite or discard unrelated work.

## Build and tests
Before merging or starting live Batch validation:
- dotnet build
- dotnet test

Do not ignore unexplained failures.

## Live server
Only one agent/session may control the live Conan server at a time.

Before a live operation:
- confirm server is offline when required
- confirm no orphan Conan server processes
- take/verify the required backup

Never inspect a live world database as though it were safely stopped.

## Batch progression
PASS:
continue.

KNOWN validated warning:
continue only according to the committed exact warning gates.

UNKNOWN error, crash, integrity failure, unclassified LoadErrors,
duplicate persistence objects, DEGRADED/EMERGENCY shutdown:
stop progression and document the blocker.

## Production
The current world is STAGING / PRE-PRODUCTION.

Do not create the production campaign world without explicit user approval.

Do not promote the staging test world to production.

## Persistent status
Maintain AGENT_HANDOFF.md during unattended multi-step work.

It should record:
- current branch and HEAD
- current stage
- last completed checkpoint
- active modlist
- latest verified backup
- server state
- last shutdown classification
- next action
- blockers