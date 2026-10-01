# M3 Task 4 — Live Test Report

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
