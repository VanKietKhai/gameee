# M3 Task 4 — Live Test Report

Status: **IN PROGRESS — paused after 4B.**

- The product direction changed to **standalone-first** (see "Direction change").
- No dedicated server has been booted yet.
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

## Direction change: standalone-first

After 4B, the product requirement was corrected:
- Players use **standalone** Conan clients (no Steam client, library or Workshop sync).
- SteamCMD is **optional** infrastructure.

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
- **Existing dedicated server** is a first-class path (Settings → Use existing server installation). Readiness no longer requires SteamCMD when a valid existing `ConanSandboxServer.exe` is configured.

## Live verified so far

1. SteamCMD bootstrap install and self-update through the app's real `SteamCmdService` (4A).
2. The app's failure handling when SteamCMD cannot reach Valve's update CDN (4B: correct failure, no partial install).
3. Workspace guard, marker guard and layout rejection against real paths.
4. The real standalone client `D:\conan exiles\Conan Exiles Enhanced` was detected read-only (Task 3 and harness). Nothing under `D:\conan exiles` was written.

## Not live verified

- Dedicated server install (4B), first boot and readiness (4C), Start/Stop/Restart (4C)
- Real world files and cold backup (4D)
- Local or Workshop mod on a real server, and server mod load evidence (4E)
- Client Mod Bundle applied to a real client, and standalone client join/compatibility (4F)
- Workshop update (4G): **NOT EXERCISED**
- Client `modlist.txt` format for standalone clients. The bundle writes one file name per line, the same as the server. This must be confirmed in 4F.
- The real location of `ConanSandboxServer.exe` in a dedicated server install. The locator accepts the install root and `ConanSandbox\Binaries\Win64`.

## Risks / findings for the next checkpoint

1. **Dedicated server source.** To my knowledge, the official dedicated server is distributed as Steam app 443030 (free, anonymous SteamCMD). That download is blocked from this network. A valid installation must come from another legitimate source (see HANDOFF: next required input).
2. **Standalone client authentication.** Conan Exiles dedicated servers normally authenticate joining players through Steam. Whether this standalone client can join a dedicated server is unknown.
   - The client folder's provenance cannot be determined by this tool. It contains a third-party shortcut (`AnkerGames - Free Pre-installed PC Games.url`).
   - If joining would require bypassing Steam/Funcom authentication, the project will not implement that, and 4F will stop and report.
3. **Mod redistribution.** Bundles redistribute mod files to players. Administrators are responsible for respecting mod authors' terms.
4. **Fresh-server backups.** The cold safety backup requires `ConanSandbox\Saved` to exist. On a server that has never booted, a mod import aborts at the backup step. This is the existing Task 2 policy and has not changed.
