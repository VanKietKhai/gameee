# M3 Task 4 — Live Test Report

## Latest checkpoint — 2026-10-03 14:29 +07:00: STOP B, D1 rolled back

Resumed from 0bb2b2a. Installed exact official Microsoft SDK 8.0.425 user-locally with SHA-512 verification. QA-019/020 safety build and tests passed (410/410, zero skipped), then merged to the live branch at 09cbf1a and rebuilt/retested with the same results. First-run WAL test sharing failure was confined to the Windows test fixture and corrected before merge.

D1 Fantasy Races Of Exiles (embedded Workshop 3780741325, Enhanced 1.0.6; 5,293,057 bytes; SHA-256 2E4D3BEEC95FCBB81A9622A42405D2C3694EE632446D89C57EA8A93E27667D4A) imported through production pipeline after verified backup 2026-10-03_142352. Eight mods mounted in order; true readiness about 39 seconds (world frame 2). Exact ITQoL 23 and Ancient Realms 37 LoadErrors passed.

**D1 FAIL / STOP B:** subsequent full-log review found 15 `NPC: Error: Data: No stat templates found for StatModifier template None.` messages at 14:24:54-58. The prior seven-mod log `ConanSandbox-backup-2026.10.02-20.34.59.log` has zero. Attribution is unproven. The generic null-table errors and Lamplighter warnings also occur in prior logs, so they are not evidence of a new D1 regression. The harness's earlier scan and exit 0 do not establish batch acceptance: generic errors after its readiness scan escaped its coverage. No new exception was added.

Shutdown: acknowledged, 72.5 seconds NORMAL, exit 0, no forced kill/orphan/WAL/SHM. quick_check and singleton gates passed. Production restore of 142352 succeeded; failed-D1 world preserved in automatic pre-restore backup 142725. Production removal archived the D1 pak and reconciled the seven-mod catalog. Latest backup 142750 verifies the restored world, quick_check=ok, singleton counts=1/1/1. Live DB and modlist hashes match pre-D1. No additional boot after rollback; D2/D3/final validation NOT STARTED.

Evidence: `artifacts/batch-d-20261003/D1-ConanSandbox.log` (SHA-256 AC751A4EA3782488809AEA012EF8608036AAAA58B417938E1DFFD75ED601522B), structured `E:\CSC-M3-Live\live-test\m3-live-log.jsonl`; see FINAL_REPORT.md and AGENT_HANDOFF.md for final state. Client untouched; production world NOT CREATED.

Status: **IN PROGRESS: checkpoint 4C/4D PASSED** on an existing dedicated server installation. Waiting for review before 4E (one Local mod).

- **4F CLIENT JOIN = BLOCKED BY CLIENT AUTHENTICATION** (see "Checkpoint 4F"). This is not a network failure, a server failure or a version mismatch.
- **4E is not blocked** by the client problem. It is verified server-side only.
- Deployment target: **private friends-only dedicated server over Radmin VPN** (see "Deployment target").
- "Standalone-first" applies to the **server/management side** only (see "Direction change", corrected 2026-10-02).
- Nothing in this report claims live verification beyond what is listed under "Live verified".

- Branch: `claude/m3-task4-live-windows` (base `0352f40`, QA-verified)
- Host: Windows 10 Pro 19045, per-user .NET SDK 8.0.425
- Harness: `tests/ConanServerControl.LiveHarness` (opt-in; requires `CSC_LIVE_TESTS=1` **and** the `.csc-live-test` marker)
- Raw step log (redacted JSONL): `E:\CSC-M3-Live\live-test\m3-live-log.jsonl`

## Workspace

```
E:\CSC-M3-Live\                 dedicated M3 workspace (414 GB free; separate drive from the client)
  .csc-live-test                harness marker (not a Conan requirement)
  steamcmd\                     optional SteamCMD (installed in 4A)
  server\                       dedicated server target (EMPTY: 4B blocked)
  live-test\                    harness log, SteamCMD logs
  app-data\                     isolated CONAN_SERVER_CONTROL_DATA (settings, backups, staging, logs, diagnostics\)
Protected, never written:       D:\conan exiles\Conan Exiles Enhanced, D:\conan exiles
```

Overlap validation passed: no overlap with protected locations, no drive root, and no nesting between the subfolders.

Guard refusals were verified live:
- A workspace under `D:\conan exiles` was rejected (exit 3).
- Live commands without `CSC_LIVE_TESTS=1` were refused (exit 4).

## Pre-live code fixes

| Fix | Result |
| --- | --- |
| Hard server-executable gate | Start is allowed only for `ConanSandboxServer.exe` outside the client folder. Everything else is blocked before any status change or process launch: `Run Me!.bat`, client `ConanSandbox.exe`, scripts, the `-Shipping` binary, unknown names. |
| QA-018 path normalization | Trailing separator, `/` vs `\` and case no longer change path identity. 8.3 short names are not expanded (documented limitation). |
| Live-test guard | Env var plus marker required; destructive harness actions are refused outside the marked workspace. |

## Steps

| # | Time (local) | Operation | Result | Duration | Live files changed |
| --- | --- | --- | --- | --- | --- |
| 4A-0 | 01:39 | Harness `init` (folders + marker) | PASS | – | `E:\CSC-M3-Live` only |
| 4A-1 | 01:39 | Diagnostics before | `NOT READY FOR SERVER LIVE TEST`, sole server blocker: `steamcmd.exe was not found in the configured SteamCMD folder` | <1 s | no |
| 4A-2 | 01:39 | `SteamCmdService.InstallAsync` (official `steamcmd.zip` from `steamcdn-a.akamaihd.net`, then `+quit`) | **PASS** | 10.7 s | `steamcmd\` only |
| 4A-3 | 01:40 | Diagnostics after | `READY FOR SERVER LIVE TEST` (SteamCMD PASS / FILES INSPECTED) | <1 s | no |
| 4B-1 | 01:40 | `SteamCmdService.InstallOrUpdateDedicatedServerAsync` | **FAIL** (exit -2) | 3.0 s | none (`server\` still empty) |
| 4B-2 | 02:2x | Retry on request (same harness) | **FAIL** (exit -2, same Fastly reset) | 3.1 s | none |
| 4B-3 | 02:35 | User-supplied `C:\Users\vkkha\Downloads\steamcmd.exe` (Valve-signed, valid) to `D:\conan exiles\Conan Exiles Dedicated Server` | 1st run: legacy self-update OK, exit 7, relaunch did not run. 2nd run: **FAIL** exit -2 (Fastly) | 8 s + 3 s | SteamCMD unpacked its own files into `Downloads`; server folder **not created** |
| 4B-4 | - | Official target set to `D:\conan exiles\Conan Exiles Dedicated Server` (sibling of the client). Harness `CSC_SERVER_DIR` + `LiveTestGuard.ValidateExternalServerDirectory`; `install-server` network preflight | **BLOCKED** at preflight (exit 5): `client-update.steamstatic.com` connection reset | <1 s | none (folder not created) |

The full 4B command was: `+force_install_dir "E:\CSC-M3-Live\server" +login anonymous +app_update 443030 validate +quit`.

### 4A details

- `E:\CSC-M3-Live\steamcmd\steamcmd.exe`, 4,407,448 bytes.
- SteamCMD's original 2013 bootstrapper downloaded its 43,472 KB update from the legacy host and exited with **code 7**. That is SteamCMD's "updated, relaunching" code, which the app already accepts.
- No SteamCMD process remained afterwards.

### 4B details: network-blocked

- SteamCMD's message, in the Vietnamese UI locale, means "Fatal Error: SteamCMD needs a network connection to update".
- From `steamcmd\logs\bootstrap_log.txt`:
  - the 2026 bootstrapper migrates to win64 using `https://client-update.steamstatic.com/steam_cmd_win64`
  - that host returns `http error 0` and `Unable to read and verify install manifest steam_cmd_win64.installed`
- Read-only reachability probes from this host:

| Host | CDN | Result |
| --- | --- | --- |
| `client-update.steamstatic.com` | Fastly | **TLS reset** (`curl: (35) Recv failure: Connection was reset`) |
| `cdn.steamstatic.com` | Fastly | **TLS reset** |
| `shared.fastly.steamstatic.com` | Fastly | **TLS reset** |
| `client-update.akamai.steamstatic.com` | Akamai | 200 |
| `steamcdn-a.akamaihd.net` | Akamai | 200 |
| `media.steampowered.com` | Valve legacy | 200 |
| `api.steampowered.com` | – | 200 |
| `steamcommunity.com` | – | 200 |

- **Classification:** OPTIONAL WORKSHOP / SERVER-AUTO-INSTALL INTEGRATION BLOCKED BY NETWORK. It is **not** a core product blocker.
- The app behaved correctly: a user-facing failure, a FAIL log entry, and nothing installed or deleted.
- No workarounds were attempted: no system or network changes, no VPN or proxy, no firewall or antivirus changes, no `steam.cfg`, no host overrides.
- SteamCMD stays installed in `E:\CSC-M3-Live\steamcmd` for optional future use.

## Checkpoint 4C/4D: real dedicated server (existing installation)

### Server and gate

- Server root: `D:\conan exiles\Conan Exiles Dedicated Server`, a sibling of the client `D:\conan exiles\Conan Exiles Enhanced`.
- `DedicatedServerLocator` picked `D:\conan exiles\Conan Exiles Dedicated Server\ConanSandboxServer.exe`, the install root (the first candidate).
  - This file is a 332,648-byte Unreal `BootstrapPackagedGame` launcher, signed **Funcom Oslo AS** (valid).
  - It spawns `ConanSandbox\Binaries\Win64\ConanSandboxServer-Win64-Shipping.exe` (186 MB, Funcom-signed) as a child process.
- Build `++exiles+release-beta-CL-378132` (beta branch). The standalone client is `++exiles+release-CL-377096`; the versions **differ**.
- Working directory: not set, so the executable's folder (the server root) is used.
- Gate: `DedicatedServer`, start allowed. Path overlap: PASS (siblings; neither is nested in the other).
- Configured through the harness with the same locator and gate logic as Settings "Use existing server installation". No SteamCMD, no `app_update`, nothing copied.

### Diagnostics before the first boot

`READY FOR SERVER LIVE TEST`, with no FAIL. Warnings, all expected before a first boot:
- the `Saved` folder doesn't exist yet
- no world yet
- RCON password not set
- the client has no `Mods` folder

SteamCMD is optional, and network-blocked for downloads.

### Live run (server log timestamps are UTC)

| Step | Result | Detail |
| --- | --- | --- |
| 7 first boot (old probe) | Process PASS, **readiness premature** | Online at 5.4 s on "game port 7777 bound". The log was still on engine frame 0, and the world only started ticking ~45 s later. |
| 7 stop (old harness) | **Aborted** | The harness's own `StateChanged` handler threw (empty timeline) inside `StopAsync`, leaving status `Stopping` with the server running. The server was later stopped through the app (attach + stop, forced after the timeout, no RCON configured). **Product fix:** subscribers are isolated. |
| 8 process identity | PASS | Bootstrap PID → `-Shipping` child, both under the server root. Arguments `-log -port=7777 -QueryPort=27015` come from the app. No client process at any point. |
| Signal timing (boot 2) | — | UDP 7777 at 5 s; RCON listen + `listplayers` reply at 5 s; query 27015 at 27 s; **log frame advancing at 34 s**. UDP 7778 is never bound (this Unreal 5 build only uses 7777). |
| Graceful command | — | `DoExit` was received but ignored (not in `RconCommandLog`); `exit` → "Couldn't find the command: exit"; RCON `help` lists **`Shutdown`**; `shutdown` → "Successfully executed: shutdown", exit code 0 after ~57 s, no WAL/SHM left. **Product fix:** default command is `shutdown`, graceful timeout raised from 30 s to 120 s. |
| 9 boot + stop (fixed app) | **PASS** | Online at 31 s ("World is ticking (server log frame 2)"). Graceful stop 65.3 s, exit code 0, no processes left. |
| 10 Start | **PASS** | Online at 32 s. |
| 10 Stop | **PASS** | 63.5 s, exit code 0 (RCON `shutdown`). |
| 10 Start | **PASS** | Online at 30.7 s. |
| 10 Restart | **PASS** | 89.6 s: Stopping → Offline (61 s) → Starting → Online (29 s). |
| 10 Stop | **PASS** | 59.6 s, exit code 0. Post-cycle check found no processes. |
| 11 world files | Enhanced | After a graceful stop: `game_0.db` only (WAL/SHM checkpointed away). After forced kills, `game_0.db-wal` / `-shm` remained. Conan also keeps `game_0_backup_1..4.db` and `game_0_upgrade_tags_%d30_1.db`. |
| 12 boot / stop | **PASS** | Online at 30 s (frame 4); graceful stop 62.2 s, exit code 0. |
| 12 cold backup | **PASS** | `2026-10-02_032514`: WorldType Enhanced, main `game_0.db` 667,648 B. |

**Step 12 backup verification detail:**
- SHA-256 `323575125C7086D45CEF68BD83D823DB7759B2892D1548D152D8B07B1AAD91C7`. The manifest recorded it, and an independent hash of the backup copy matches.
- `ManifestWritten` / `HashesVerified` / `SqliteVerified` are all True; `quick_check` = `ok`.
- The live world files were identical before and after the backup.

### Findings (not blocking)

1. The Conan server log reports `Autologin attempt failed, unable to register server!` (server-list registration). Not a blocker for the private Radmin target (see "Deployment target").
2. Readiness fix: `ConanSandbox.log` current-run frame 0 means not ready. Live Online times are now 30–32 s.
3. The backup copy's folder gains `game_0.db-shm` (32 KB) and `game_0.db-wal` (0 B) after verification. SQLite creates them when `quick_check` opens the **copy**. The copied `game_0.db` hash still matches the manifest, and the live world is untouched. A follow-up could open with `immutable=1` or verify a temporary copy.
4. The backup copies the whole `Saved` tree into `world\` (including `Config` and `Logs`) and also into `config\`, so the configuration is stored twice. This is pre-existing design.
5. Client/server build mismatch (beta vs live). Resolved by the version-matched server below; it is not the 4F blocker.
6. `configure-rcon` wrote `[RconPlugin]` into the throwaway server's `Saved\Config\WindowsServer\Game.ini`. Conan requires the RCON password in plaintext there. It is random, and the app stores it only with DPAPI.

### Safety

- Client: **0 files modified** under `D:\conan exiles\Conan Exiles Enhanced` since the session started.
- Nothing was written to the `D:\conan exiles` top level.
- SteamCMD was not used. Router and firewall were not modified.

## Version-matched dedicated server (CL-377096 / 2.2.2)

**Why a different server was needed.** The first test server was `++exiles+release-beta-CL-378132` (ProjectVersion **2.2.3**). That turned out to be the **current Steam public branch** (app manifest: no BetaKey, buildid `25639945`). "release-beta" is only Funcom's internal stream name. The standalone client is `++exiles+release-CL-377096` (ProjectVersion **2.2.2**), i.e. one patch older.

**The matched server.**
- Downloaded on another machine with `download_depot 443030 443031 236179869812429142` and placed at `D:\conan exiles\depot_443031`, a sibling of the client.
- Both server binaries report `++exiles+release-CL-377096` and are signed **Funcom Oslo AS** (valid). The server log reports `Build: ++exiles+release-CL-377096` and `ProjectVersion 2.2.2`, the same as the client log.
- `download_depot` fetched only depot 443031, so the Steamworks runtime from depot 1004 was missing. Six **Valve Corp.-signed** DLLs were copied, hash-verified, from the app's own SteamCMD folder: `steamclient(64).dll`, `tier0_s(64).dll`, `vstdlib_s(64).dll`. `steamwebrtc*.dll` (client voice) was not available and is not needed by the server. **No client file was used.**

**Gate and diagnostics.** Gate: allowed. External-directory validation: PASS. Diagnostics: `READY FOR SERVER LIVE TEST`, no FAIL. RCON was configured with `configure-rcon`, which now writes `Game.ini [RconPlugin]` before the first boot.

| Step | Result | Detail |
| --- | --- | --- |
| First boot | **PASS** | Online at 32.0 s ("World is ticking (server log frame 2)"). Bootstrap and `-Shipping` processes both under `depot_443031`. No client process. |
| Graceful stop | **PASS** | 64.9 s via RCON `shutdown`, exit code 0, no processes left. |
| Boot before backup | **PASS** | Online at 29.9 s; graceful stop 63.0 s, exit code 0. |
| Cold backup | **PASS** | `2026-10-02_044344`: Enhanced, `game_0.db` 643,072 B, SHA-256 `6F8A467C2DC5A41299EDFCEEA906288761B7CDC2757B9636730526484692C8D7`. Manifest written, hashes verified, `quick_check` = `ok`, live world unchanged by the backup. |

**Still observed:** `Autologin attempt failed, unable to register server!`, so the server does not appear in the public server list. This is **not a blocker** for the private Radmin target, provided authenticated clients can direct-connect (see "Deployment target"). Direct connect is part of 4F.

**Client:** 0 files modified. SteamCMD was not used on this machine for the download.

## Pre-4E vanilla client connectivity smoke test

- Checkpoint persisted first: `f695638` on `origin/claude/m3-task4-live-windows`.

**Server (`D:\conan exiles\depot_443031`, CL-377096 / 2.2.2).**
- Started through the app. Bootstrap PID 26028, `-Shipping` child PID 16688.
- **Online at 30.9 s** ("World is ticking (server log frame 2)").
- Endpoints owned by PID 16688, all on `0.0.0.0`:

  | Protocol | Port | Role |
  | --- | --- | --- |
  | UDP | 7777 | game |
  | UDP | 7778 | used by this build (the beta build did not bind it) |
  | UDP | 14001 | role unknown |
  | UDP | 27015 | query |
  | TCP | 25575 | RCON |

- LAN IPv4 `192.168.0.244` (Ethernet, Private). Also `26.84.226.21` (Radmin VPN).
- `ServerPassword` empty, `IsBattlEyeEnabled=False`.
- Online subsystem on the server: **Fls** (Funcom Live Services) / NULL, not Steam.
- Direct-connect target for a client on this PC: `127.0.0.1:7777`; from the LAN: `192.168.0.244:7777`.

**Client (manual, by the operator).**
- The standalone client started in **offline mode**. Its own log says `LogFuncomLiveServices: Error: Login failed: couldn't connect.`, and the UI says online play is unavailable and only single player works.
- Direct Connect lives under *Play Online*, which is not available in offline mode.

**Server side.** No connection attempt was logged after the baseline: no accept, pre-login, login or join lines.

**Conclusion.**
- Online play, including Direct Connect, requires the client to log in to Funcom Live Services, which authenticates through the client's platform.
- This client cannot log in. Getting past this would require bypassing an authentication/licensing mechanism, which this project will not implement or recommend.
- The legitimate path is a licensed client (e.g. Steam), which logs in to FLS normally.
- The server's own `Autologin attempt failed, unable to register server!` (server-browser registration) is not a blocker for the private Radmin target. Public registration is not a release criterion.
- Root cause and classification: see "Checkpoint 4F".
- Reachability check: the Funcom telemetry host `live.commontelem.flx.wintercloud.net` answers (HTTP 404), so this is not the same as the Fastly network block.

**New finding: graceful stop after a long uptime.**
- After an ~11-minute hold, RCON `shutdown` was received and world teardown began immediately.
- Conan's exit then exceeded the 120 s graceful timeout, and the app force-killed the server (exit code -1).
- `game_0.db-wal` (395 KB) was left behind. Committed data is recovered by SQLite on the next open, but the stop was not clean.
- Earlier short runs exited in ~57–63 s.
- Recommendation: treat "teardown started" as progress and allow a longer timeout (e.g. 300 s), or wait while the process is still in its exit sequence.
- Conan also writes its own rotating `game_0_backup_N.db` every ~5 minutes while running.

## Checkpoint 4F: client join — BLOCKED BY CLIENT AUTHENTICATION

**4F CLIENT JOIN = BLOCKED BY CLIENT AUTHENTICATION.**

Observed client: `D:\conan exiles\Conan Exiles Enhanced`, build `++exiles+release-CL-377096` (ProjectVersion 2.2.2). The session analysed is the operator's manual launch on 2026-10-02 at 05:01 local (`launcher.log`), 22:01 UTC in the client log.

**Observed behaviour.**
- The client enters FLS offline mode.
- Play Online, and Direct Connect under it, is unavailable. Only single player works.
- The server receives no player connection attempt (no accept, pre-login, login or join lines).
- Client/server version match is **not** the blocker: both are CL-377096 / 2.2.2.

**Evidence** (client log `ConanSandbox\Saved\Logs\ConanSandbox.log`, read-only):

| Line | Log text | Meaning |
| --- | --- | --- |
| 499–502 | `STEAM: Steam User is subscribed 1`, `Client API initialized 1`, `Created online subsystem instance for: STEAM` | The Steam API layer reports success. |
| 511–512 | `Created online subsystem instance for: Fls`, `Loaded subsystem for type [Fls]` | Online play goes through Funcom Live Services. |
| 533, 539 | `Build: ++exiles+release-CL-377096`, `Net CL: 377096` | Same build as the server. |
| 1713 | `Requested Message FlsOfflineMode` | The client falls back to offline mode. |
| 1801, 2652 | `LogFuncomLiveServices: Error: Error in Login: Steam auth token not available.` | **Root cause:** no platform authentication token. |
| 2904–2905 | `Error in Login: couldn't connect`, `Login failed: couldn't connect.` | FLS login fails; the client stays offline/single-player. |

**Steam-emulation artifacts in the client installation.**
- `Engine\Binaries\ThirdParty\Steamworks\Steamv164\Win64\` contains `steam_emu.ini` and `steam_api64.rne` next to `steam_api64.dll`. These are Steam-emulation artifacts, not part of the Steamworks runtime.
- The emulated layer reports a subscribed user, but it cannot produce the platform authentication token that FLS requires. That matches the log above.
- The launcher folder also holds a third-party distributor's shortcut and readme (`AnkerGames - Free Pre-installed PC Games.url`, `Read Me.txt`).
- The artifacts were listed by name only. Their contents were not opened, and they were **not modified**.

**Classification.**

| Candidate | Verdict | Why |
| --- | --- | --- |
| Network failure | **No** | The first login error is the missing Steam auth token, and network reachability cannot supply a token. A read-only probe on 2026-10-02 reached `services.live.exiles.wintercloud.net` (HTTP 404 at `/`, TLS in ~0.7 s). The telemetry host answers too. The same client session also logged FLS API timeouts (`GetBuildOverrides`, `GetActiveEvents`, PlayFab retries). They are secondary. |
| Server failure | **No** | The server was Online with the world ticking, and UDP 7777 / 27015 were bound on `0.0.0.0` by the server process. No join reached it because the client never got past FLS login. |
| Version mismatch | **No** | Client and server are both CL-377096 / 2.2.2. |
| Client authentication | **Yes** | `Steam auth token not available` → FLS login failed → `FlsOfflineMode`. |

**Policy.**
- No authentication or licensing bypass was attempted, and none will be: the emulation artifacts are untouched, there is no forced join from offline mode, and nothing patches Steam or FLS.
- Client bypass investigation is **closed**.
- Client files modified: **none**.

**Unblock condition.**
- 4F resumes only with a **legitimate Conan client session** that can obtain the platform authentication token FLS requires (for example a licensed Steam copy with Steam signed in).
- Radmin VPN provides the private network path. It does **not** replace FLS/platform authentication.
- When 4F resumes:
  - direct connect over Radmin to `<host Radmin IP>:7777` (this host's Radmin address was `26.84.226.21`)
  - apply a Client Mod Bundle
  - confirm the client `modlist.txt` format

## Deployment target: private friends-only over Radmin VPN

PROJECT DEPLOYMENT TARGET:
- private friends-only server
- approximately 5 players
- Radmin VPN virtual LAN
- direct connection over the Radmin/private IP, when the client is authenticated
- no public server browser requirement
- no public IP exposure requirement
- no router port forwarding requirement, unless explicitly requested later
- no UPnP requirement
- RCON must remain private/local
- public FLS server registration is **not** a release criterion

`Autologin attempt failed, unable to register server!` is therefore **NOT A BLOCKER** for the intended private deployment, provided authenticated clients can direct-connect. No project time is spent making the server public.

## Checkpoint 4E: not blocked by the client problem

4E (one Local `.pak` mod) is **NOT blocked** by the 4F client problem. It is verified server-side, and no client connection is required:
- backup before mutation (verified cold backup)
- transactional install
- `modlist.txt`
- server startup
- real readiness
- server log evidence that the mod loaded
- rollback verification

## Private Radmin deployment and long-run graceful stop (live)

**Deployment model.** Private friends-only over Radmin VPN; see HANDOFF "Deployment model". Live diagnostics:
- `network.private-vpn` = PASS: Radmin VPN `26.84.226.21`, friends direct-connect `26.84.226.21:7777`. Public registration and port forwarding are not used.
- `rcon.exposure` = WARNING: Conan listens on `0.0.0.0:25575`, the app connects to `127.0.0.1`. Never port-forward RCON.
- `GetLanIPv4` now skips VPN adapters and reports `192.168.0.244`; it previously returned the Radmin address.

**Long-run graceful stop with the 300 s default** (server CL-377096):

| Event | Time |
| --- | --- |
| Online | 32.9 s |
| Held online | 660 s |
| RCON `shutdown` received; `BeginTearingDown` | 22:38:12 UTC |
| `LogExit: Preparing to exit` | 22:40:44 (152 s later) |
| `LogExit: Exiting` | 22:41:00 |

- App `StopAsync`: **PASS in 171.8 s, exit code 0**, no processes left.
- World after stop: `game_0.db` only, no WAL/SHM. The WAL left by the previous forced kill was checkpointed by Conan on startup.
- Exit duration grows with uptime: ~57–65 s after short runs, >125 s and 171.8 s after ~11 minutes.
- 300 s covers what was observed. Longer uptimes are not yet measured; a future refinement could keep waiting while the log shows the exit sequence progressing.

## Pre-4E validation: graceful stop design and network address selection

Code: `0d54acc`, reviewing `2c56226` (flat 300 s wait) and `982a5c3` (LAN skips VPN adapters). Design details are in HANDOFF "Pre-4E validation".

**Automated.**
- `982a5c3` alone: build PASS, 293 / 293.
- `0d54acc`: build PASS (0 warnings, 0 errors), **313 / 313**, 0 skipped, run twice.

The 20 new cases in `M3PreE4StopAndNetworkTests` cover:
- Radmin is reported separately from the physical LAN (live host adapter set).
- Unrelated VPN and virtual adapters are never labelled Radmin or LAN, including an adapter renamed "Radmin VPN".
- No usable Radmin adapter → not detected: absent, Down, APIPA only, or enumeration failure.
- 26/8 is preferred.
- An acknowledged shutdown gets the extended window without a kill.
- Log progress without an RCON reply gets the extended window.
- No reply, a rejected command, or no RCON → 1 s short window, then kill (never the 300 s window).
- An acknowledged-but-hung shutdown is killed after the extended window.
- Offline is not reported until the child exits after the launcher, with a throwing `StateChanged` observer.
- A child that survives the kill → Error, never Offline.
- A real `cmd` → `ping` tree: the child is tracked after the launcher dies and killed.
- RCON to a silent server times out (no hang).
- The log probe ignores the previous run's exit lines and handles rotation.

**Live diagnostics (this host).**
- `network.private-vpn` = PASS: `PhysicalLanIPv4` `192.168.0.244`, `RadminVpnIPv4` `26.84.226.21`, `RecommendedRadminDirectConnect` `26.84.226.21:7777`, `SameLanDirectConnect` `192.168.0.244:7777`.
- The down TAP-Win32 and Bluetooth adapters (APIPA) and Teredo were ignored.

**Live 10-minute stop** (server `D:\conan exiles\depot_443031`, CL-377096; started and stopped through the app via the harness; no client started; local times):

| Item | Result |
| --- | --- |
| Start | Online at 33.4 s (launcher PID 27436, `-Shipping` child PID 7172) |
| Held online | 620.2 s (about 11 minutes of uptime at stop) |
| RCON `shutdown` sent | 06:07:22.863 |
| Shutdown acknowledgement | Reply `Successfully executed: shutdown`. Server logged receipt at 06:07:23.354; the app had the reply by 06:07:24.47 |
| Progress evidence | `LogCore: Engine exit requested` (seen 06:07:24.469) |
| Window used | **300 s extended** |
| World unload start (`PreExit Game`) | 06:07:23.365 |
| `LogExit: Preparing to exit` | 06:09:43.120 (quiet teardown of 139.8 s) |
| `Game engine shut down` / `Exiting` | 06:09:45.518 / 06:09:56.812 |
| Process tree exit | 06:09:57.195 (154.3 s after the command) |
| `StopAsync` | **PASS**, 157.6 s, Status Offline |
| Exit code | 0 |
| Forced kill | **NO** |
| `game_0.db-wal` / `-shm` after stop | **NO / NO** (`game_0.db` only) |
| Orphan server processes | **NO** |

## M3 Task 4E: one real Local .pak mod (live, PASS)

- Remote head before mutation: `581615d`. Working tree clean; no other session or process was active; server Offline.
- **Test mod:** `C:\Users\vkkha\Downloads\mod conan\WickProbe.pak`, 4,492,459 B, valid Unreal pak (footer magic, pak version 12), single file. SHA-256 `D7FE0EC099BC501AFB9F5A2BF18B9312FF100F37591F2ACF39AB08F5B465D79C`. Outside the server Mods folder.
- **Baseline:**
  - Diagnostics: READY, Dedicated Server PASS, Enhanced world present, no Mods folder / modlist, Radmin unchanged.
  - World: `game_0.db` 655,360 B, SHA-256 `ee5bed07…`, no WAL/SHM.
  - Client snapshot `pre-4E` (20 entries).
- **Backup before mutation:** `2026-10-02_063005`, manifest + SHA-256 + `quick_check` = ok. The import pipeline also made its own `pre-local-mod-import` cold backup.
- **Local import (PASS, 0.6 s, no SteamCMD, no Workshop):**
  - Installed `D:\conan exiles\depot_443031\ConanSandbox\Mods\WickProbe.pak`, SHA-256 identical to the source.
  - `modlist.txt` = `WickProbe.pak`, load order 1.
  - The source file is still present and unchanged.
- **Boot with mod (PASS):** Online at 31.3 s ("World is ticking"). Launcher PID 9708, `-Shipping` PID 2108. **Positive server-side load evidence**:
  - `LogModManager: Mounting mod pak file: …/Mods/WickProbe.pak`
  - Conan extracts `WickProbe-WindowsServer.pak/.utoc/.ucas` into `Saved\ExtractedMods`. The Enhanced mod `.pak` is a container of platform sub-paks.
  - `Mounted Pak file '…/ExtractedMods/WickProbe-WindowsServer.pak', mount point: '…/Content/Mods/WickProbe/'`
  - `Mod 'WickProbe' contributes 5 package(s)`, `AddActiveModControllerClass: /Game/Mods/WickProbe/BP_WickProbeController`, `Persistence: Spawning mod controller: BP_WickProbeController_C`
  - No warnings or errors mention the mod.
- **Stop with mod (PASS):**
  - RCON reply `Successfully executed: shutdown`; progress evidence `Engine exit requested`; extended 300 s window.
  - Total 65.4 s, exit code 0, no forced kill, launcher + child gone, no orphans, no WAL/SHM.
  - The world DB changed (SHA-256 `be018702…`), as expected for a mod controller.
- **World integrity after the mod (PASS):** cold backup `2026-10-02_063232`, `quick_check` = ok.
- **Removal (PASS):**
  - `pre-mod-removal` backup `2026-10-02_063246`.
  - `WickProbe.pak` was moved out of Mods to `app-data\removed-mods\20261002-063246-420\` (recoverable).
  - `modlist.txt` is now empty, unrelated files are unchanged, and the source is unchanged.
- **Boot after removal (PASS):**
  - Online at 31.3 s. No `Wick` lines, no missing-mod errors, no stale controller references.
  - Graceful stop 64.7 s, exit code 0, no forced kill, no WAL/SHM.
- **Client:** snapshot `post-4E` compared to `pre-4E`: 0 added / 0 removed / 0 changed.
- **Finding:** Conan does not delete its extraction cache `Saved\ExtractedMods\WickProbe-WindowsServer.*` (~1.5 MB) after the mod is removed. It is not mounted (not in the modlist). A future removal step could retire matching `ExtractedMods` files as well.
- **Product change made for this checkpoint** (`581615d`): removing a Local mod now retires its installed pak (SHA-256-gated move, never a delete).

## Modpack V1 Batch A + multi-mod load order (4E.2) — live, PASS

- Remote head before the test: `1e318d4`. Branch clean; no Conan process.
- Server `D:\conan exiles\depot_443031` (CL-377096). All steps went through the harness using the app's real services. No client, no Workshop, no SteamCMD.
- A concurrent docs-only session ("Twelve Legends removal and quest system") was active. Over a coordination message it confirmed it would not start the server, run the harness or touch `Mods`.

**Mods** (sources in `C:\Users\vkkha\Downloads\mod conan`, all valid pak v12 with a `-WindowsServer` sub-pak; installed to `ConanSandbox\Mods\<name>.pak`):

| Role | Mod | File | Size | SHA-256 (source = installed) |
| --- | --- | --- | --- | --- |
| A | StackMe10K (Nexus mod 3) | `StackMe10K.pak` | 4,641,754 B | `30F5DF54…5C8A0` |
| B | Savage Paragon (Workshop 3766043945; size matches) | `SavageParagon.pak` | 4,760,799 B | `5F2673D9…312B5` |
| C | Grit & Grease (Workshop 3801774752; size matches) | `GritandGrease.pak` | 68,049,336 B | `B5FA39CC…4ACCA` |

| Step | Result | Evidence |
| --- | --- | --- |
| Baseline | PASS | Diagnostics READY (0 FAIL). `Mods` held only an empty `modlist.txt`; empty catalog. Verified cold backup `2026-10-02_073104`, `quick_check` = ok. |
| Import ×3 (production Local pipeline) | PASS | Installed SHA-256 = source SHA-256; the sources are unchanged. Each import also took its own verified cold backup. |
| Initial order A, B, C (`MoveAsync`) | PASS | `modlist.txt` = `StackMe10K.pak \| SavageParagon.pak \| GritandGrease.pak`; `.pak` files untouched. |
| Boot 1 | **PASS** | Online 44.5 s. Mount sequence StackMe10K → SavageParagon → GritandGrease. Container `Order` 1000 / 1001 / 1002. Contributes 5 / 91 / 382 packages. No duplicates and no mod errors. |
| Reorder to C, A, B (`MoveAsync`) | PASS | `modlist.txt` = `GritandGrease.pak \| StackMe10K.pak \| SavageParagon.pak`. Every `.pak` has an unchanged SHA-256, size, creation and write time, so nothing was re-copied. |
| Boot 2 | **PASS** | Online 36.4 s. Mount sequence GritandGrease → StackMe10K → SavageParagon. Container `Order` G&G 1000, StackMe10K 1001, Paragon 1002. |
| Remove middle (StackMe10K) via `RemoveAsync` | **PASS** | Verified `pre-mod-removal` backup `2026-10-02_073932` (`quick_check` ok). Pak retired to `app-data\removed-mods\20261002-073932-695\StackMe10K.pak` (hash matches). `modlist.txt` = `GritandGrease.pak \| SavageParagon.pak`. Remaining hashes and the source are unchanged. |
| Boot 3 (`--expect-absent StackMe10K.pak`) | **PASS** | Online 40.6 s. G&G → Paragon (Order 1000 / 1001). No log line mentions StackMe10K. No missing-mod or stale-modlist errors. |
| Restore A (cumulative batches) | PASS | Re-imported (hash match), order back to A, B, C. |
| Boot 4 (Batch A final) | **PASS** | Online 46.5 s; same sequence and Order as Boot 1. `Persistence: Loading mod controller` for `StackMe10K_Modcontroller_C`, `BP_SavageParagon_ModController_C` and `BP_GritnGreaseModController_C`. No error or warning line mentions any of the three. |
| Final verified cold backup | PASS | `2026-10-02_074354`, `game_0.db` 667,648 B, `quick_check` = ok. |

Stops: all four were acknowledged RCON `shutdown`s, 300 s extended window, 66–68 s, exit code 0, **forced kill NO**, no WAL/SHM, **no orphan processes**.

**Runtime load order: PROVEN** for mount order and container priority.
- In both configurations the server's `Mounting mod pak file` sequence followed `modlist.txt` exactly.
- The IoStore container `Order` was reassigned by modlist position: first entry 1000, then +1 per entry.
- **Not exercised:** which mod wins an asset both override. None of the three is known to override the same asset, so later-entry-wins precedence is Unreal's documented behaviour for a higher `Order`, not observed here.

**ExtractedMods** (read-only):
- Conan extracts each mod's `-WindowsServer` sub-pak once (all mtimes 07:32:05) and reuses it on later boots.
- While StackMe10K was removed, `StackMe10K-WindowsServer.pak/.ucas/.utoc` (1,440,291 B) stayed in the cache but was not mounted. It is current again after the restore.
- `WickProbe-WindowsServer.*` (1,491,114 B) is still stale from 4E.
- Technical debt:
  - Removal does not retire extraction-cache files.
  - **Unverified risk:** whether Conan refreshes the cache when a Local `.pak` is replaced by a newer file of the same name.
- Nothing was deleted.

Other notes:
- **StackMe10K 10,000 stacks: IN-GAME BEHAVIOR NOT YET VERIFIED.** Server logs only show that it mounts and loads.
- Boot time with Batch A: 36–47 s (vanilla 30–33 s).
- **Client: 0 files changed** under `D:\conan exiles\Conan Exiles Enhanced` during the run.
- **Harness issues found and fixed during the run:**
  - **Log-offset bug** (fixed in this checkpoint): Conan rotates `ConanSandbox.log` on start, so the first Boot 1 analysis read past the new log's mount lines and reported 0 mounts (FAIL). The server itself had mounted all three mods. Now a changed first log line means "read from 0", and Boot 1 was re-run and passed.
  - **Shell quoting mistake** (operator side, before the successful imports): three `import-local` attempts were run with a wrong path. The pipeline rejected them at staging and nothing changed; three extra verified baseline backups were taken.
- Build PASS (0 warnings, 0 errors); `dotnet test` 315 / 315, 0 skipped.

## Modpack V1 Batch B — STAGING / PRE-PRODUCTION (server-side PASS with one known issue; in-game work BLOCKED)

- **Environment:** `D:\conan exiles\depot_443031` = **STAGING / PRE-PRODUCTION**. The world is a **TEST / VALIDATION WORLD**. No production save exists.
- **Code and repo:**
  - Validated code baseline `2aca0cf`.
  - The repo was at `bc9899a` (docs by the "Twelve Legends" session; its files were not touched).
- **Coordination:** the other session reported that its user instruction says Batch B must not start before the paks are validated read-only.
  - This session installed Batch B on its own user's explicit instruction, after the read-only validation below.
  - Further server actions are **paused** pending the user's confirmation.
- **Pre-change copies:**
  - `modlist.txt` and `Saved\Config\WindowsServer\*.ini` copied to `E:\CSC-M3-Live\live-test\batchB-pre-20261002-141001\` (SHA-256 recorded).
  - Verified cold backup `2026-10-02_141009` (`quick_check` = ok). The world hash equals the end of Batch A, so nothing changed it in between.
- **Server-setting discrepancy (not changed):** `ServerSettings.ini` has `ThrallDamageToNPCsMultiplier=0.5` (the Conan default), not the `0.300000` named as the server philosophy. Batch B only forbids increasing it, so it was left at 0.5. The operator decides whether to set 0.3.

**Paks**

All three are in `C:\Users\vkkha\Downloads\mod conan`. Each is a valid Unreal pak v12 with a `-WindowsServer` `.pak/.utoc/.ucas` payload, and its size equals the Workshop item size. Source SHA-256 = installed SHA-256, and the sources are untouched.

| Mod | File | Size | SHA-256 |
| --- | --- | --- | --- |
| Thrall Reputation (3787066846) | `ThrallReputation.pak` | 833,760 B | `5CE7A95D31400518DF31F72349FBB4D181D2D6D0771D970159B4DBF150DE10B9` |
| Improved Thralls & QoL (3758661389) | `ImprovedThrallsAndQoL.pak` | 154,632,086 B | `F35D9D927B4E76869D57DC7073B61FCF2215039B628343B28D6B0989A0B48272` |
| WO - Riding Thralls (3803149679) | `WO_RidingThralls.pak` | 78,896,983 B | `ACF569D38D7CB73E7523A096200AB1948E72C2783CB70953F43ADC2EFE7B34A8` |

**Load order (staging, after Batch B)**

Batch A is kept unchanged and Batch B is appended through the production pipeline. `MoveAsync` confirmed the order with no `.pak` rewritten.

1. `StackMe10K.pak`
2. `SavageParagon.pak`
3. `GritandGrease.pak`
4. `ThrallReputation.pak`
5. `ImprovedThrallsAndQoL.pak`
6. `WO_RidingThralls.pak`

No verified dependency required changing it. Savage Paragon's "load after any XP mod" does not apply: ITQoL's XP features (Inactive Follower XP, party Shared XP) are not XP-curve changes, and both are off by default.

**Boots**

| Boot | Result | Evidence |
| --- | --- | --- |
| B1 (5-minute hold) | **PASS** | Online 41.5 s. Mount sequence = modlist; container `Order` 1000–1005. Contributes 5 / 91 / 382 / 18 / 942 / 57 packages. New controllers spawned: `ReputationModController_C`, `MC_ImprovedThrallsAndQoL_C`, `WO_BP_RT_ModController_C`. No crash or crash loop over 5 minutes. Memory at readiness: 4.81 GB working set, 5.49 GB private. |
| B1 errors | **CORRECTED: 23 LoadErrors from ITQoL** (originally recorded as "none from the mods") | Every `Error:` / `Warning:` category is also in the vanilla log with the same messages (AIDataTable `WarTest*` rows, `ItemInventory`, `building` stability, `LogBaseSpawner`, `LevelStreaming` `/Game/Developers/...`, `BinkMoviePlayer`). Two `LogActor` warnings attach the ITQoL `Lamplighter_Sphere` component to NPCs. **Missed at the time:** see the correction note below. |
| B1 stop | PASS | Acknowledged `shutdown`, extended window. **191.4 s**: quiet teardown of 171 s, then exit at 189 s. Exit code 0, no forced kill, no WAL/SHM, no orphans. |
| B2 (restart) | Load PASS, **1 known issue** | Online 39.6 s, same sequence and Order. All three new controllers `Loading` from the save (no re-spawn, no duplicates). **Known issue:** `Persistence: Error: UConanBuildingPersistenceComponent::CreateHealthPool - DefaultObject not loaded: /Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_PL_ServerMailContainer`. |
| B2 stop | PASS | 70.0 s, exit code 0, no kill, no WAL, no orphans. |

**World integrity**

Read-only checks on copies of the verified backups `141009` → `142111` → `142352`:
- **pre-B → B1: only additions.**
  - Three mod controllers (ids 127–129).
  - **One ITQoL placeable**: `BP_PL_ServerMailContainer` (id 147, owner −1, hidden at z = −50000; property `MailboxChestSpawned`). It belongs to the mod's "Mailbox System".
  - Three properties and 23 `game_events` rows.
  - Nothing deleted; `quick_check` = ok.
- **B1 → B2:** only `game_events` +17. The mailbox is still a single object (id 147, same health rows), and the controllers are unchanged.
- **The TEST world has no player data:** `account`, `characters`, `item_inventory`, `guilds` and `follower_markers` are all 0. Player, base, inventory and follower integrity therefore has **nothing to verify yet**; it needs a client.
- Stale save rows: the 4E WickProbe controller (id 123) still has `mod_controllers` and `actor_position` rows after its removal. No error is logged.

**Configuration** (nothing was invented; no files or DB rows were edited for mod settings)

- **Thrall Reputation:** **No supported balance configuration found — using mod defaults.**
  - The author lists configuration options as a future improvement.
  - Assets show fixed tiers (`E_FriendshipTier` with a damage-bonus percentage per tier) and UI only (`W_ReputationBar`, `W_ThrallPartyList`).
  - No global buff was applied.
- **Improved Thralls & QoL:**
  - Settings exist only in its in-game admin UI (`DataCmd ImprovedThrallsQoL`; assets `DT_DefaultSettings`, `DT_GeneralSettings`, `E_AdminSettingsType`). No server file or RCON path is documented.
  - The author states "Everything is disabled by default!". No settings were persisted in the world after two boots, so the server runs compiled defaults.
  - Every requested OFF / 1.0 / 0 value is therefore met **by default (author claim, not seen in the UI)**. Every requested ON value is **NOT APPLIED** until an authenticated admin client is available.
  - All requested options exist in the current description, except a **global Thrall Base Stat Modifier**: only *Individual* Thrall Base Stat Modifiers exist (pets and golems have global ones).
- **Riding Thralls:**
  - Mode is per player, chosen at the craftable "Registrar" placeable in-game. Modes described: one mount per follower; shared mounts; the player's follower as **passenger on the player's mount** (closest to "Ride With Me").
  - **No mode is selected yet** (needs a client).
  - The mod changes no combat stats and adds no follower count.

**Blocked by 4F (no authenticated client):** client join and mod-mismatch check, all in-game checks for Thrall Reputation, ITQoL and Riding Thralls, and the Authority / Commander role-balance test (Step 8).

**Known issues and risks**
- ITQoL server mailbox `CreateHealthPool` persistence error on restart: the object persists and is not duplicated; in-game effect not yet verified.
- Stop time grew to 191 s with six mods after about 6 minutes of uptime. The 300 s extended window still held, but larger mods (Ancient Realms 497 MB, Shemite 2.1 GB) and longer uptimes may exceed it; re-measure in Batch C.
- `ThrallDamageToNPCsMultiplier` is 0.5 vs the stated 0.3 philosophy.
- The 4E WickProbe controller rows remain in the save.

**Correction (2026-10-02, after the Batch C review): Batch B boot logs contain 23 `LoadErrors` per boot.**

What was missed:
- Every Batch B boot (B1, B2 and the three restart cycles) logs 23 lines of `LoadErrors: While trying to load package None, a dependent package None (<id>) was not available`, each followed by `FPackageName: Unable to identify a valid mount point associated with skipped package None`.
- They reference 16 unique package IDs.
- Batch A boots: 0 such lines.
- The original scan searched for `Error:`. These lines read `LoadErrors:`, so they were missed and "none from the mods" was recorded.

Attribution to **Improved Thralls & QoL** (read-only byte search, confirmed independently by both sessions):
- All 16 IDs appear, as 8-byte little-endian values, only in `ImprovedThrallsAndQoL.pak` and its extracted `ImprovedThrallsAndQoL-WindowsServer.ucas`. They do not appear in the other five installed mods or in Ancient Realms.
- According to the "Twelve Legends" session's container check, none of the IDs is a real package in any mod or vanilla container. They are **dangling references**, not a missing dependency.
- Caveat: the log names no referencing asset ("package None"), so the attribution rests on the byte search.

Impact:
- The server still boots, all six mods mount, and the ITQoL controller loads.
- The in-game effect (some ITQoL feature or asset not loading) is **not verified**; it needs a client.
- The harness at that time did not flag `LoadErrors` lines. That is a harness gap, not a mod-boot PASS criterion that was met.

## Modpack V1 Batch B — restart stability (3 cycles) and acceptance

**Operator decision (2026-10-02):**
- Batch B is **ACCEPTED**: **SERVER-SIDE COMPATIBILITY: PASS**.
- **IN-GAME BEHAVIOR: NOT YET VERIFIED** (4F).
- **ITQOL MAILBOX ISSUE: KNOWN NON-BLOCKING WARNING.**
- Batch B stays installed; no rollback; Batch C not started.

### Restart cycles

- **Setup:** staging `D:\conan exiles\depot_443031` (TEST world) with the six-mod load order unchanged. Mods, ITQoL settings and the client were not touched.
- **Per cycle:**
  1. `mod-boot --hold 150`: Offline → Start → true readiness → about 150 s online → graceful stop.
  2. `cold-backup`: verified backup with `quick_check`.
  3. A separate read-only check of the backup copy (`immutable=1`): `quick_check` plus a count of the mailbox rows.
- **Baseline:** the B2 backup `142352`.

| Item | Cycle 1 | Cycle 2 | Cycle 3 |
| --- | --- | --- | --- |
| Readiness ("World is ticking") | 33.3 s | 33.2 s | 39.6 s |
| Mods loaded, modlist order, `Order` 1000–1005 | 6/6 | 6/6 | 6/6 |
| `BP_PL_ServerMailContainer` CreateHealthPool line | 1 (frame 0) | 1 (frame 0) | 1 (frame 0) |
| Mailbox actor rows (`buildings` / `buildable_health` / `properties`) | 1 (1 / 2 / 1) | 1 (1 / 2 / 1) | 1 (1 / 2 / 1) |
| Duplicate / missing mailbox | NO / NO | NO / NO | NO / NO |
| Other ITQoL errors | none | none | none |
| Other mod-specific errors / fatals | none / none | none / none | none / none |
| Stop (acknowledged RCON `shutdown`, exit 0, no WAL/SHM, no orphans) | PASS, 180.3 s | PASS, 177.0 s | PASS, 183.7 s |
| Forced kill | NO | NO | NO |
| Verified backup, `quick_check` | `143558` ok | `144252` ok | `144937` ok |

- The mailbox is the same object every cycle: actor 147 at (0, 0, −50000).
- 113 actors and 23 `mod_controllers` rows, unchanged.
- The only row-count change is the server's own `game_events` table (cycle 3: 302 → 325).

### Known non-blocking warning rule (harness)

`Core/LiveTesting/ModBootGates` holds **exactly one** rule, `ITQOL-MAILBOX-HEALTHPOOL`.
- **Exact message** (the line without its `[timestamp][frame]` prefix): `Persistence: Error: Code: UConanBuildingPersistenceComponent::CreateHealthPool - DefaultObject not loaded: /Game/Mods/ImprovedThrallsAndQoL/Mailbox/BP_PL_ServerMailContainer.BP_PL_ServerMailContainer_C`
- **Version-bound:** it applies only while the installed `ImprovedThrallsAndQoL.pak` SHA-256 is `F35D9D927B4E76869D57DC7073B61FCF2215039B628343B28D6B0989A0B48272`. A new ITQoL version must be validated again.
- `mod-boot` reports a matching line under `KnownNonBlockingWarnings`, not `ModRelatedProblems`.
- The boot **still FAILS** when:
  - any other ITQoL line, any other BP_PL line or any other mod error appears
  - the same message appears with a different ITQoL file
  - the **ITQoL mailbox gate** fails: after every `mod-boot` stop, the stopped world (read-only, immutable) must hold **exactly 1** `BP_PL_ServerMailContainer`. 0 = MISSING, 2 or more = DUPLICATE, unreadable = FAIL.
  - world integrity fails: `cold-backup` `quick_check` is unchanged.
- **Unit tests:** `M3ModBootGateTests`, 29 cases.
- **Live check on real data:** the new read-only `analyze-last-boot` command, run on the cycle 3 log and the stopped world.
  - Analysis PASS: `ModRelatedProblems` = none, `KnownNonBlockingWarnings` = `ITQOL-MAILBOX-HEALTHPOOL x1`.
  - Mailbox gate PASS (count 1).
  - The world file was unchanged, and no WAL/SHM was created.

### Shutdown duration metric (all later batches)

- Every stop now logs a `shutdown duration gate` entry. **240 s or more = HIGH RISK = FAIL**: stop before adding another batch. The graceful window is 300 s.
- **Batch B observed range: 177–184 s** after about 2.5 minutes of uptime. B1 was 191 s after about 6.5 minutes; B2 was 70 s after a short uptime.
- Batch A stops were 66–68 s.
- The extra time is a **silent ~160 s gap** after `BattlEyeClient: ClientLoadingScreenStopped` and before `LogExit: Preparing to exit`. No log lines appear in it and no mod is named, so the cause is not attributed.

### Known-good restore point pinned

- Pre-Batch-B backup `2026-10-02_141009`: Batch A world, `game_0.db` 667,648 B, SHA-256 `C6BC2052…8415`.
- **Copied** (robocopy, timestamps kept) to `E:\CSC-M3-Live\pinned-backups\2026-10-02_141009`, outside `app-data\backups`. Retention (keep latest 10 / keep 14 days) only lists and deletes inside `app-data\backups`.
- 87 files, 21,316,556 B. Every file's SHA-256, size and mtime equals the source, and the source was unchanged by the copy. The world hash matches the backup's own `metadata.json`.
- The copy is marked read-only. Per-file hashes and restore notes sit beside it: `2026-10-02_141009.SHA256SUMS.txt`, `README-PINNED.txt`.
- The original stays in `app-data\backups` until retention ages it out (about 2026-10-16).

### Build

`dotnet build -c Release --no-incremental`: 0 warnings, 0 errors. `dotnet test -c Release`: **344 / 344**, 0 skipped (315 + 29 `M3ModBootGateTests`).

## Modpack V1 Batch C — Ancient Realms Enhanced: test, investigation, rollback (STAGING)

**Operator status (2026-10-03):**
- Ancient Realms: **ROLLBACK COMPLETE · RETEST REQUIRED · COMPATIBILITY NOT YET ACCEPTED · NOT REJECTED.**
- Its load errors are **not** whitelisted. A retest runs only on a quiet host (see "Host load" below).
- Batch D is not started.

### Source and import

- **Source:** `C:\Users\vkkha\Downloads\mod conan\Ancient_Realms.pak`.
  - 496,900,005 B, equal to the Workshop item size.
  - SHA-256 **`12F7E7192043270FC5F2290C5989F8B8285494BA47A054646790B4A042D1FD1A`**.
  - Valid pak v12, unencrypted index, with a `-WindowsServer` `.pak/.ucas/.utoc` payload.
  - modinfo: Workshop `3755775098`, v0.1.260707, `minimumVersion` Enhanced.
  - No dependency keys, no Workshop Required items, no companion files.
- **Pre-Batch-C backup** `2026-10-02_152307` (`quick_check` ok), **pinned** at `E:\CSC-M3-Live\pinned-backups\2026-10-02_152307`. 102 files byte-identical to the source, read-only, plus `.SHA256SUMS.txt`.
- **Import (Local pipeline):** PASS.
  - Verified pipeline backup `152436`.
  - Installed SHA-256 = source SHA-256; the source is unchanged.
  - modlist = the 6 Batch B mods + `Ancient_Realms.pak` (Order 1006).

### Boots

All boots are on the staging TEST world.

| Boot | Mods | Readiness | AR errors: named / all AR-attributable | Peak working set / max private | Stop: shutdown sent → tree exit | Stop gate | Forced kill | World check |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| C1 (2026-10-02, 200 s hold) | 7 | 35.4 s | 7 / 37 | 6.8 / 9.1 GB | 15:29:10.6 → 15:32:09.9 | 180.6 s | NO | `153648`, `quick_check` ok |
| C2 (2026-10-02, 130 s hold) | 7 | 35.3 s | 7 / 37 | 8.1 / 9.0 GB | 15:39:52.3 → 15:42:43.4 | 172.2 s | NO | `154352`, `quick_check` ok |
| Investigation cycle 1 (2026-10-03, 150 s hold, **loaded host**) | 7 | **102.1 s** | 7 / 37 | 6.1 / 9.0 GB | 00:28:16.6 → 00:33:17.2 | **301.8 s** | **YES** (exit −1, WAL 671,592 B left) | `003449`: `quick_check` and `integrity_check` ok after WAL replay |
| Rollback check (2026-10-03, 150 s hold, **loaded host**, **without AR**) | 6 | 67.2 s | 0 / 0 | 6.6 / 9.0 GB | 00:42:40.5 → 00:47:41.2 | **301.7 s** | **YES** (exit −1, WAL 671,592 B left) | `004820`: `quick_check` and `integrity_check` ok after WAL replay |

- Every boot: all mods LOADED in modlist order; ITQoL mailbox gate = 1 (known warning ×1); no fatal or crash.
- Both forced kills came after `LogExit: Game engine shut down`, and the last world write preceded the teardown.

### Ancient Realms error evidence (kept for the retest decision)

**Exactly the same 7 named lines on every AR boot** (C1, C2, investigation cycle 1):

```
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/AR_BP_ModController, a dependent package None (35924C262CEDD960) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/AR_BP_ModController, a dependent package None (CFFAB4A08E3DB146) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/_MASTER/BP_PL_Decal_Floor_Master, a dependent package None (D0B5ED96C9B95112) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Brick/brick_03/BP_PL_Water_Well_Fountain, a dependent package None (FE8C96EB21683A73) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Ceramic/ceramic_02_white/BP_PL_Water_Well_Fountain_gold, a dependent package None (FE8C96EB21683A73) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Concrete/concrete_04/BP_PL_Water_Well_Fountain, a dependent package None (FE8C96EB21683A73) was not available.
LoadErrors: While trying to load package /Game/Mods/Ancient_Realms/Buildings/Stone/stone_05/BP_PL_Water_Well_Fountain, a dependent package None (FE8C96EB21683A73) was not available.
```

- **Exactly the same 37 AR-attributable `LoadErrors` per AR boot** (identical multisets): the 7 named lines plus 30 `package None` lines. They reference 10 unique missing package IDs:
  - `1B6F0F85A6FACB01`, `1FCB1FA801D75583`, `35924C262CEDD960`, `5A19E15D92AF952`, `7C3C9C3D45215971`
  - `9F5919676AD734DE`, `CFFAB4A08E3DB146`, `D0B5ED96C9B95112`, `F6DA87602985C3AA`, `FE8C96EB21683A73`

  The 23 ITQoL-attributable lines (Batch B) are unchanged, and there are 0 unattributed `LoadErrors`.
- **Version-bound:** tied to `Ancient_Realms.pak` SHA-256 `12F7E719…FD1A`. The installed hash was re-checked on the day.
- **Dangling references, not a missing dependency:**
  - The 10 IDs are a real package entry in **no** container: AR's own three TOCs (4,953 / 7,222 / 4,953 entries), the 34 vanilla server `.utoc`, the 37 vanilla client `.utoc`, and all 13 local mod paks.
  - Their names are unresolved, because the mods' asset registries are compressed.
- **Referencing classes are present:** the `FPackageId` (CityHash64 of the lower-case UTF-16 package name) of all 6 referencing packages is in the AR server container. The building classes and the controller ship and load; only some of their dependencies are absent.
- **Maps:** AR ships three developer maps (`AlmostEmpty`, `icon_creation_map`, `test_building_map`). The server never referenced or streamed them, and there are no map or level errors.
- **Controller:** `AddActiveModControllerClass` → `Persistence: Spawning mod controller: AR_BP_ModController_C` (C1), then `Loading mod controller` (C2, cycle 1).
  - One actor and `mod_controllers` row 148, never duplicated.
- **No other AR error lines.** No crash. **No real save error**: the only save-like matches are vanilla `LogBinkMoviePlayer` title-movie failures, present in Batch B too. Persistence errors are the known ITQoL line only.
- Runtime errors after world start are identical in kind and count to Batch B.
- **SQLite:** every backup passes `quick_check`. After a forced kill, `integrity_check` also passes once the WAL is replayed (on a private temp copy). There are no duplicate actors, and the only world changes are AR additions plus `game_events`.

### Rollback (operator rule: any failed criterion → roll back)

Investigation cycle 1 failed **no forced kill** and **stop < 240 s**. Cycles 2–3 were not run.
1. `remove-local Ancient_Realms.pak`: verified `pre-mod-removal` backup `2026-10-03_003751`. The pak was retired to `app-data\removed-mods\20261003-003756-722\` (hash matches); modlist = the 6 Batch B mods; the source is unchanged.
2. `restore 2026-10-02_152307`: new harness command using the production `IBackupService.RestoreAsync`, which takes a pre-restore safety backup (`2026-10-03_003829`).
   - The restored `game_0.db` SHA-256 = backup (`20841D67…`); WAL after restore = 0 B.
   - World, `Saved\Config` and `modlist.txt` were restored.
3. **Verified:**
   - Boot: 6/6 mods; analysis PASS with the known ITQoL warning only.
   - Backup `004820`: 113 actors, 23 `mod_controllers` rows, mailbox 1, 0 AR actors, 0 duplicates, the same actor-class set as the pre-C baseline.

### Host load: the forced kills are not attributable to Ancient Realms

- The rollback check boot **without** AR shows the same forced kill (301.7 s) as AR cycle 1 (301.8 s).
- **Host state at that time:** League of Legends (game and client) running, plus Discord, Chrome and ChatGPT; about 6 GB RAM free. With no server running, the other session measured CPU 49–82% busy and a commit charge of 26 / 32 GB.
- **Effect on boots:** readiness rose from about 35 s to 67–102 s.
- **Effect on stops:** the silent stop gap (PreExit → `LogExit: Preparing to exit`) rose from about 160 s (2026-10-02 afternoon) to about 288 s.
- **Conclusion:** timing data from this host state is not comparable. The AR retest needs a quiet host, and the 300 s force-kill ceiling needs the policy change below.

### Notes

- **Staging config:** `ServerSettings.ini` `ThrallDamageToNPCsMultiplier` = 0.300000, set by the other session after the rollback (backup `2026-10-03_005048`). Restoring any older backup brings back 0.5, so re-apply 0.3 after a restore.
- **ExtractedMods:** AR's server files (14,622,398 B) stay in `Saved\ExtractedMods` after removal, unmounted; WickProbe is still stale too. Nothing was deleted.
- **Harness:** `restore <backupId>` (server Offline only; PASS = restored world hash equals the backup and no non-empty WAL).
- **Build:** 0 warnings / 0 errors; `dotnet test` 344 / 344, 0 skipped.

### Shutdown policy correction (operator, 2026-10-03)

The compatibility threshold and the force-kill ceiling are now separate:

| Time | Meaning |
| --- | --- |
| 240 s | HIGH RISK: the batch compatibility gate fails and the next batch is not started (unchanged) |
| 300 s | Graceful window. A shutdown **acknowledged by RCON or progressing in the current-boot log** is no longer killed here. The overrun is logged (`GracefulWindowExceeded`). |
| 600 s | Emergency ceiling (`AdvancedSettings.EmergencyStopCeilingSeconds`, a new key, so existing settings files load 600). The process tree is killed here if still alive. |

- With no acknowledgement and no shutdown progress, the 30 s short fallback is unchanged; nothing waits 600 s blindly.
- The ceiling is never below the graceful window.
- "Offline only when the whole process tree is gone" and the final tree kill are unchanged.
- Regression tests:
  - `M3PreE4StopAndNetworkTests` adds 6: an acknowledged stop that outlasts the window is not killed; log progress without an RCON reply continues; an unproven stop never waits for the ceiling; a ceiling below the window is raised; the defaults are 30/300/600; an old settings file loads 600. The hang-forever case is now killed at the ceiling, not the window.
  - `M3ModBootGateTests` adds 3: stops of 240 s or more still fail the batch gate, even with the 600 s ceiling.
- **Build:** 0 warnings / 0 errors; `dotnet test` **353 / 353**, 0 skipped.

### Quiet-host retest (2026-10-03 01:01–01:23): all 3 cycles PASS

**Host:**
- League of Legends closed; no other games; idle build servers stopped; no other Conan or harness session; 8.0 GB RAM free.
- Background load noted: a stuck `tasklist | findstr` pipeline (`findstr` PID 28280) has used about 2 of 8 threads since 2026-10-02 03:51. It was also present during every earlier baseline run and was left untouched.

**Setup:**
- Start state: the known-good pre-Batch-C world (post-rollback, verified backup `2026-10-03_010141`: 113 actors, 23 controllers, 0 duplicates; `ThrallDamageToNPCsMultiplier=0.3`).
- Ancient Realms reinstalled through the Local pipeline: backup `010158`; installed SHA-256 = source = `12F7E719…FD1A`; 7-mod test order.
- Each cycle: `mod-boot --hold 150` with the new stop policy, then a verified cold backup.

| Cycle | Readiness | AR named / attributable | Other AR errors | Shutdown ack / engine exit requested | Stop (gate) | Window exceeded | Forced kill | Peak working set / max private | Backup, `quick_check` | Persistence / save errors | Controller | Mailbox |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 36.5 s | 7 / 37 | 0 | YES 01:05:45.8 / 01:05:46.3 | 183.1 s | NO | NO | 7.66 / 9.06 GB | `010858` ok | known ITQoL only / 0 | spawned (row 148) | 1 |
| 2 | 36.5 s | 7 / 37 | 0 | YES 01:12:34.9 / 01:12:35.4 | 185.9 s | NO | NO | 8.45 / 9.01 GB | `011547` ok | known ITQoL only / 0 | loaded | 1 |
| 3 | 36.5 s | 7 / 37 | 0 | YES 01:19:16.1 / 01:19:16.6 | 195.9 s | NO | NO | 8.74 / 9.05 GB | `012238` ok | known ITQoL only / 0 | loaded | 1 |

- Every stop: exit code 0, no WAL/SHM left, no orphans.
- World changes across the cycles: the AR controller actor and `mod_controllers` row 148 (cycle 1), then `game_events` only. No duplicate or missing persistence objects.
- **Cross-boot comparison:** across all six AR boots (C1, C2, investigation cycle 1, retest 1–3), the 7 named and 37 attributable signatures are **identical multisets**. There are 0 other AR error lines, 0 unattributed `LoadErrors`, no crash, and no save errors.
- **Criteria met for reclassification.** The AR errors are eligible for a KNOWN NON-BLOCKING WARNING rule bound to `Ancient_Realms.pak` SHA-256 `12F7E719…FD1A`. **Not applied: the operator decides.**
- **Stop-time trend:** 183 → 186 → 196 s, still under 240 s but rising, with a margin of about 44 s. Watch it before Batch D.
- Staging now runs **7 mods** (Ancient Realms reinstalled).

### Batch C acceptance and warning classification (operator, 2026-10-03)

- **BATCH C SERVER-SIDE COMPATIBILITY: PASS**
- **ANCIENT REALMS GAMEPLAY: NOT YET VERIFIED**
- **MAP / BUILDING / COLLISION BEHAVIOR: NOT YET VERIFIED**
- **Reclassified as KNOWN NON-BLOCKING WARNING:** only the exact validated AR signatures, bound to `Ancient_Realms.pak` SHA-256 `12F7E7192043270FC5F2290C5989F8B8285494BA47A054646790B4A042D1FD1A`:
  - the 7 named lines above (`ANCIENT-REALMS-DANGLING-REF-1..7`)
  - the exact 37-entry per-boot `LoadErrors` multiset, including the 30 "package None" lines
- **No other Ancient Realms error is suppressed.**
- **The exception is invalidated by:**
  - any new AR signature, or an extra or missing occurrence
  - a changed file hash
  - a crash
  - a save or persistence error
  - a world-integrity failure (`quick_check`)
  - a missing or duplicate persistence object (the AR controller must exist exactly once in the stopped world)
- **Harness implementation:** `ModBootGates` plus the harness (exact known-warning rules, the attributed `LoadErrors` set gate, an AR controller gate, and tests). It is committed separately once built and tested. The build is deferred until the other session's shutdown-timing study ends, so the measurements are not disturbed.
- **Also recorded:**
  - ITQoL's 23 `LoadErrors` per boot (Batch B, `cc13afa`) are **PENDING OPERATOR CLASSIFICATION**: monitored exactly, not accepted.
  - With the new gate, any unattributed `LoadErrors`, or any from a mod without a validated set (Batch D onward), fail the boot.
- **Timing context:** the other session's first isolated data point, Batch A alone (3 mods, about 150 s hold), stopped in 210.2 s, longer than the 6- and 7-mod quiet runs (172–196 s). Stop time is not driven by mod count alone. The study's conclusions belong to that session.

## Direction change: standalone-first (corrected 2026-10-02)

After 4B the requirement was recorded as "players use **standalone** Conan clients (no Steam client, library or Workshop sync)". 4F showed that this is wrong for multiplayer. Corrected statement:

> Conan Server Control is standalone-first on the **SERVER/MANAGEMENT** side. SteamCMD and the Steam client are not required for normal server-management operations once a valid Dedicated Server installation exists.
>
> Local `.pak` mods and Client Mod Bundles may be managed independently of Workshop.
>
> However, multiplayer clients must use a legitimate Conan client session capable of obtaining the platform authentication token required by Funcom Live Services. Radmin VPN does not replace FLS/platform authentication.

SteamCMD remains **optional** infrastructure.

What was implemented and unit-tested (272 / 272 tests):
- **Local mod source.** Flow: `.pak` → validate → COPY to isolated staging → SHA-256 → locked pipeline (stop if running → verified cold backup → transactional commit with rollback → restart only if it was running) → `modlist.txt`.
  - The source file is only read.
  - SteamCMD is never used.
  - Manual update means replacing with a newer file of the same name.
- **Client Mod Bundle export.** `Mods\*.pak` + `Mods\modlist.txt` + `manifest.json` (load order, size, SHA-256, source type, optional Workshop ID) + `README.txt`.
  - No game files are included.
  - Export refuses client and server locations.
  - The bundle is assembled atomically.
  - A read-only client sync planner compares a bundle with a client folder. It never writes.
  - It distributes **only** `.pak` mod files, the modlist, manifest/hash metadata, and permitted configuration material.
  - It does **not** distribute the game client, provide authentication, replace a platform license, bypass FLS, or modify Steam authentication.
- **Existing dedicated server** is a first-class path (Settings → Use existing server installation). Readiness no longer requires SteamCMD when a valid existing `ConanSandboxServer.exe` is configured.

## Live verified so far

1. SteamCMD bootstrap install and self-update through the app's real `SteamCmdService` (4A).
2. The app's failure handling when SteamCMD cannot reach Valve's update CDN (4B: correct failure, no partial install).
3. Workspace guard, marker guard and layout rejection against real paths.
4. The real standalone client `D:\conan exiles\Conan Exiles Enhanced` was detected read-only (Task 3 and harness). Nothing in the client folder was written.
5. Existing dedicated server, version-matched CL-377096: first boot, real readiness, Start/Stop/Restart, graceful RCON `shutdown`, and a verified cold backup (4C/4D, sections above). `ConanSandboxServer.exe` was found at the install root.
6. 4F classification: the client is blocked at FLS authentication (see "Checkpoint 4F").
7. Pre-4E: acknowledgement-gated graceful stop after ~11 minutes of uptime (extended window, 154 s, exit 0, no kill, no WAL, no orphans), and Radmin/LAN address diagnostics (see "Pre-4E validation").

## Not live verified

- Dedicated server install through SteamCMD (4B): blocked by network. An existing installation is used instead.
- Local or Workshop mod on a real server, and server mod load evidence (4E). Not blocked by the client.
- Client Mod Bundle applied to a real client, and client join (4F): **BLOCKED BY CLIENT AUTHENTICATION**.
- Workshop update (4G): **NOT EXERCISED**
- Client `modlist.txt` format. The bundle writes one file name per line, the same as the server. This must be confirmed when 4F resumes with a legitimate client.

## Risks / findings for the next checkpoint

1. **Dedicated server source.** The official dedicated server is Steam app 443030 (free, anonymous SteamCMD), and that download is blocked from this network. Resolved for testing: a version-matched depot was downloaded on another machine (see "Version-matched dedicated server").
2. **Client authentication.** Confirmed: 4F is **blocked by client authentication** (see "Checkpoint 4F").
   - The observed client cannot obtain the platform token FLS requires, and its installation contains Steam-emulation artifacts.
   - The project will not bypass Steam/Funcom authentication. 4F stopped and reported.
3. **Mod redistribution.** Bundles redistribute mod files to players. Administrators are responsible for respecting mod authors' terms.
4. **Fresh-server backups.** The cold safety backup requires `ConanSandbox\Saved` to exist. On a server that has never booted, a mod import aborts at the backup step. This is the existing Task 2 policy and has not changed.

## Shutdown-timing study (STAGING, 2026-10-03) — non-randomized

**Operator conclusion (accepted 2026-10-03):** Conan Enhanced has a long base-game shutdown phase. It is predominantly single-threaded and highly sensitive to host CPU contention. **Shutdown duration is not attributed to mod count.**

**Setup**
- Server `D:\conan exiles\depot_443031`, staging TEST world, `ThrallDamageToNPCsMultiplier=0.300000`.
- Every run: boot → real readiness → **150 s hold** → RCON `shutdown`.
- Metric: `ShutdownSentAt` → `ProcessTreeExitedAt` (whole process tree gone).
- Configurations were switched only through the production catalog (`SetEnabledAsync` + `MoveAsync`; harness `set-mods`, which lives on the local worktree branch `wip/shutdown-timing` and is not merged). No `.pak` was copied or removed.
- Pre-study verified backup `2026-10-03_012832`, pinned read-only in `E:\CSC-M3-Live\pinned-backups\`. Restored through the app afterwards.
- Host: League of Legends closed. An orphaned `findstr` (PID 28280, started 2026-10-02 03:51, about 2 of 8 threads) ran throughout. Chrome and Discord were open.
- **Not randomized:** configurations ran in a fixed order, one block of three runs each. Batch A's runs drifted from 210 to 193 s, so order and warm-up effects are mixed with configuration effects.

**Results** (seconds, shutdown command → process-tree exit)

| Configuration | Runs | Median | Policy class (2026-10-03) |
| --- | --- | --- | --- |
| No mods | 171.0 / 169.7 / 170.1 | 170.1 | NORMAL |
| Batch A + Riding Thralls | 174.8 / 174.6 / 175.5 | 174.8 | NORMAL |
| Batch A + Thrall Reputation | 183.9 / 179.1 (run 2: startup hang, below); reruns 182.1 / 193.7 | 183.0 (4 runs) | NORMAL |
| Batch A + Ancient Realms | 194.4 / 181.1 / 183.3 | 183.3 | NORMAL |
| All 7 mods (control, after restore) | 175.9 | — | NORMAL |
| All 7 mods (other session's quiet-host retest, ~150 s hold) | 183.1 / 185.9 / 195.9 | 185.9 | NORMAL |
| Batch A (3 mods) | 210.2 / 199.8 / 193.3 | 199.8 | NORMAL |
| Batch A + Improved Thralls & QoL | 220.9 / 206.2 / 211.2 | 211.2 | NORMAL |

All study runs were acknowledged, with exit code 0, **no forced kill**, no WAL/SHM left, and no orphan processes.

Earlier data points under host load: 301.7 / 301.8 s, force-killed by the then-300 s policy, with League of Legends running and CPU at 49–82 % with the server off.

**What the server does during the long phase** (5 s process sampler, Batch A run 2)
- From `PreExit Game` until `LogExit: Preparing to exit`, the `-Shipping` process uses **exactly one CPU core continuously** (about 5.0 CPU-seconds per 5 s).
- Disk read/write counters are flat, working set and private memory are flat, and the log is silent. The game-thread frame counter stays frozen (for example `[898]` for 187 s).
- The network shows no wait pattern: one connection goes `CloseWait` mid-phase without effect.
- So the long phase is single-threaded CPU work on the game thread, not network or disk waiting. Any competing CPU load stretches it, which matches the 288 s phase seen while League of Legends was running.

**Interpretation limits**
- No mods vs mods: about 170 s vs about 175–211 s. Mods add at most about 40 s at this uptime, while the base game alone accounts for about 170 s.
- Differences between mod configurations are within the drift seen inside a single configuration's three runs (up to 17 s). **This study does not attribute a specific shutdown penalty to a specific mod.**
- Shutdown time grows with uptime (about 70 s after 40 s, about 170–210 s after 3 min, about 154 s vanilla after 11 min on 2026-10-02). Long production uptimes may be slower still. Not measured.

**Shutdown policy (operator decision, 2026-10-03)**

| Duration | Class | Action |
| --- | --- | --- |
| < 240 s | NORMAL / ACCEPTABLE | — |
| 240–300 s | WARNING | Investigate host load. Not a mod compatibility failure. |
| 300–600 s | DEGRADED | No new mod batch until reviewed. A graceful shutdown with positive progress continues. |
| ≥ 600 s | EMERGENCY ceiling | Process-tree kill if still alive. |

No-ack / no-progress shutdowns keep the short (30 s) unresponsive fallback. The stop behaviour is already in the product (`26091fa`).

The harness gate (`ModBootGates.EvaluateShutdownDuration`) follows this table on branch `claude/m3-task4-integration` (`29c74e5`); see "Integration branch" below.

**Batch A + Thrall Reputation startup hang: TRANSIENT / NOT REPRODUCED**
- Study run 2 (02:35:52) hung right after launch. The log stopped 2 s in, at `Loading asset registry state for mod 'SavageParagon'`, and readiness timed out after 600 s ("World still loading").
- The same configuration was rerun twice on 2026-10-03:
  - 43.9 s and 38.5 s, both "World is ticking", mod load PASS.
  - Stops 182.1 s and 193.7 s; exit 0, no kill, no WAL, no orphans.

**State after the study**
- The world was restored to the verified final backup `2026-10-03_033503` (live `game_0.db` byte-identical; SHA-256 `1FD6089F9A52E74A29FCB907225AD9D491F4B984BFE6562C252DFEA9F8264793`).
- `quick_check` and `integrity_check` = ok. ITQoL mailbox = 1, ITQoL controller = 1, Ancient Realms controller = 1, no duplicate controllers.
- 7 mods in the original order; `ThrallDamageToNPCsMultiplier=0.300000`.
- The orphaned `findstr` PID 28280 was **not** terminated: its CommandLine is unreadable and it predates the study, so it could not be confirmed as part of it.
- Batch D: not started.

## Integration branch `claude/m3-task4-integration` (2026-10-03)

- **Base:** `claude/m3-task4-live-windows` @ `7455e7d`.
- **Frozen sources, not modified:** `wip/mod-warning-classification` (`23007b7`) and `wip/shutdown-timing` (`d952d6d`). Both were committed unverified during a hand-off.

| Commit | Content |
| --- | --- |
| `61d6b50` | Exact validated mod warning gates (ported from `23007b7`, then built, tested and checked on real data). ITQoL (SHA-256 `F35D9D92…`): exact mailbox line, exact 23 LoadErrors, exactly 1 mailbox and 1 controller. Ancient Realms (SHA-256 `12F7E719…`): exact 37 LoadErrors (7 named + 30 "None"), exactly 1 controller. Everything else fails: a changed hash, set drift, unattributed LoadErrors, a missing/duplicate singleton, any crash/assertion/persistence error line (`SevereLogMarkers`), a world `quick_check` other than ok. New read-only `analyze-boot <log> <backupId>`. |
| `29c74e5` | Shutdown classes: NORMAL < 240 s, WARNING 240–300 s (both allow the next batch; never a mod compatibility failure), DEGRADED 300–600 s (blocks the next batch until reviewed), EMERGENCY ≥ 600 s. Stop behaviour unchanged from `26091fa`. |
| `dcf19db` | Live harness `set-mods` (controlled mod sets for staging tests; harness only) with tested input rules (`ModSetSelection`). |

**Validation**
- `dotnet build -c Release --no-incremental`: 0 warnings, 0 errors.
- `dotnet test -c Release`: **398 / 398**, 0 skipped.

**Read-only 7-mod check** (no server start): `analyze-boot` on the control boot log `ConanSandbox-backup-2026.10.02-20.34.59.log` (03:28:53) against backup `2026-10-03_033503`:
- Mount sequence = modlist (7 mods). ModRelatedProblems = none.
- Known non-blocking: `ITQOL-MAILBOX-HEALTHPOOL` ×1 and `ANCIENT-REALMS-DANGLING-REF-1..7` ×1 each.
- LoadErrors: ITQoL 23 = validated set (exact); Ancient Realms 37 = validated set (exact); unattributed none.
- `quick_check` ok. ITQoL mailbox 1, ITQoL controller 1, Ancient Realms controller 1 (all PASS); no duplicate controllers.

Batch D: not started.
