# Conan Server Control

Local Windows manager for a **Conan Exiles Dedicated Server**. It installs and updates the dedicated server through SteamCMD, keeps a Workshop mod list in a defined load order, backs up the world, restarts the process safely, and exposes a private phone-friendly Web Admin.

Conan Server Control does **not** bypass Steam DRM, does not distribute Conan client files, and does not give anyone the game without a valid license. Players join with the official Steam client. After you change server mods, they restart Conan Exiles and Steam Workshop updates their local copies.

Written layout, architecture, and safety rules (including paths this repo must never touch) live in [docs/](docs/).

---

## What currently works (Phase 1)

This repository is a real, compilable .NET 8 solution — not a design-only sketch.

| Area | Status |
| --- | --- |
| WPF desktop shell (dark theme, MVVM, sidebar) | Working |
| Dashboard Start / Stop / Restart | Working (manages `ConanSandboxServer.exe`) |
| Settings for executable, working dir, ports, Web Admin | Working |
| Diagnostics (SteamCMD path, server path, PID, data dirs) | Working |
| Serilog file + live GUI logs | Working |
| SteamCMD install / update / dedicated server `app_update 443030` | Working on Windows |
| SQLite activity log | Working |
| Backups of world/config/modlist + retention policy | Working |
| Web Admin login (PBKDF2), rate-limited, cookie session | Working when enabled |
| Phone-responsive dashboard (Start/Stop/Restart/Backup) | Working |
| Workshop ID validation, mod list generation, copy-to-share | Working |
| Source RCON client | Implemented; needs a running server + password |
| First-run wizard, tray icon, scheduled automation, INI editor, Workshop metadata polling | **Not implemented yet** (UI says so) |

If a button is a later phase, the app tells you. It does not pretend the feature succeeded.

---

## Requirements

- Windows 10 / 11 x64 (host PC)
- .NET 8 Desktop Runtime (SDK if you build from source)
- Disk space for SteamCMD + Conan Exiles Dedicated Server (App ID **443030**)
- Optional: [Tailscale](https://tailscale.com/) for phone access without opening router ports
- A licensed Conan Exiles client on each **player** PC (not required on the host if you only run the dedicated server)

You do not need Linux, Docker, or Steam Guard on the host when using SteamCMD anonymous login (supported for this dedicated server).

---

## Installation (from source)

```powershell
git clone <this-repo>
cd <repo>
dotnet restore ConanServerControl.sln
dotnet build ConanServerControl.sln -c Release
dotnet test ConanServerControl.sln -c Release
```

Run the desktop app (Windows):

```powershell
dotnet run --project src/ConanServerControl.App/ConanServerControl.App.csproj -c Release
```

The executable name is `ConanServerControl.exe`.

Web Admin only (same backend, useful for development):

```powershell
dotnet run --project src/ConanServerControl.Web/ConanServerControl.Web.csproj
```

---

## First run

1. Start **Conan Server Control**.
2. Open **Settings**.
3. Choose the Conan dedicated server executable (`ConanSandboxServer.exe`) **or** Detect installs.
4. Set the install folder SteamCMD should use.
5. Optionally click **Install SteamCMD** on Diagnostics (downloads Valve’s `steamcmd.zip`).
6. Set server name, ports, and a Web Admin password if you want phone control.
7. Save. Open **Dashboard** → **START SERVER**.

A guided 8-step wizard is planned; until then Settings + Diagnostics are the first-run path.

Default data directory (not the git repo):

```text
%ProgramData%\ConanServerControl\
```

Override with environment variable `CONAN_SERVER_CONTROL_DATA` if needed (tests use this).

---

## Installing Conan Exiles Dedicated Server

Steam App ID: **443030**.

1. Install SteamCMD from Diagnostics (default folder under the data directory, or `C:\ConanServerControl\steamcmd\` if you set that).
2. Set the server install directory in Settings.
3. Dashboard → **UPDATE SERVER** (or Updates page).

SteamCMD is invoked with `ProcessStartInfo` (no shell). Arguments are built only from settings — Web Admin cannot send arbitrary commands.

Anonymous SteamCMD login is used. Steam account passwords are not exposed through Web Admin.

---

## Ports

| Purpose | Default |
| --- | --- |
| Game | 7777 UDP |
| Query | 27015 UDP |
| RCON | 25575 TCP |
| Web Admin | 8080 TCP (localhost only unless you change bind mode) |

Forward game/query ports on your router if friends join over the internet. **Do not** forward Web Admin. This app never opens Windows Firewall or router ports by itself.

---

## Mods

1. Copy the Workshop ID from the Steam Workshop URL.
2. Mods page → paste ID → **ADD MOD**.
3. The manager downloads via SteamCMD (`workshop_download_item` for app **440900**), copies `.pak` files into `ConanSandbox\Mods`, and writes `modlist.txt` in UI order.

Load order in the UI **is** server load order. Changing it can affect saves; a backup is taken when you remove a mod.

**Copy Mod List** produces a shareable list of Workshop links. Players still install Conan through Steam. The manager will not distribute `.pak` files as a pirate client.

Workshop timestamp polling / “update available” badges are the next implementation step. Until then, **UPDATE MODS** tells you that honestly.

---

## Automatic updates

Settings store:

- Check interval (default 30 minutes)
- Automation mode: Manual / Scheduled / Automatic

Only **manual** update actions are fully wired in this build (`CHECK UPDATES`, `UPDATE SERVER`, delayed restart countdown). A background `ModUpdateMonitor` is not started yet.

Update pipeline states: Idle → Checking → Backup → Stopping → Updating Server → Validating → Starting → Health Check → Completed / Failed.

If SteamCMD fails, existing server files and saves are **not** deleted.

---

## Backups

Stored under `%ProgramData%\ConanServerControl\backups\yyyy-MM-dd_HHmmss\`:

- `world/`
- `config/`
- `modlist/`
- `metadata.json`

**BACKUP NOW** is available on the dashboard, backups page, and Web Admin. Restore refuses to run while the server is online and always writes a pre-restore safety copy.

Retention: keep latest 5/10/20/50/100 and optionally N days (unit tested).

---

## Web Admin and phone access

1. Settings → enable Web Admin, set username + password (PBKDF2-SHA256 hash, never plaintext).
2. Bind: **Localhost Only** (default), LAN, or custom.
3. Restart the manager so Kestrel listens.
4. Open `http://127.0.0.1:8080`.

Login is rate-limited. Sessions expire (default 30 minutes). APIs require the auth cookie. There is no arbitrary RCON or shell endpoint.

### Tailscale (recommended for phones)

This app does **not** install or configure Tailscale.

1. Install Tailscale on the Windows host: https://tailscale.com/download/windows  
2. Install Tailscale on iPhone / Android.  
3. Sign both into the same tailnet.  
4. Diagnostics shows a `100.x.x.x` address when a Tailscale adapter is detected.  
5. Phone browser: `http://100.x.x.x:8080` (bind mode must not be localhost-only — use LAN or bind the Tailscale IP).

Keep Web Admin off the public internet.

---

## Security recommendations

- Prefer SteamCMD anonymous login.
- Use a long unique Web Admin password.
- Leave bind on localhost unless you are on Tailscale or a trusted LAN.
- Do not reuse the server admin password as the Web Admin password if you can avoid it.
- Secrets on Windows are DPAPI-protected (`secrets.bin`).
- Administrative actions are written to the activity log (`Khải requested Restart Server`, and similar).

---

## Troubleshooting

| Symptom | What to do |
| --- | --- |
| “Conan server executable was not found” | Settings → browse to `ConanSandboxServer.exe` |
| “SteamCMD is not installed” | Diagnostics → Install SteamCMD |
| SteamCMD exit code ≠ 0 | Open `%ProgramData%\ConanServerControl\logs\steamcmd-*.log` |
| Web Admin will not start | Set a password in Settings; enable Web Admin; restart the manager |
| SERVER CRASH LOOP DETECTED | Automatic restart paused after 3 crashes in 10 minutes; fix the server, then start manually |
| Players cannot see mods | Copy the mod list; they must subscribe/install via Steam Workshop on a licensed client |

---

## Logs and data locations

```text
%ProgramData%\ConanServerControl\
    settings.json
    secrets.bin
    control.db
    logs\app-YYYYMMDD.log
    logs\server-YYYYMMDD.log
    logs\steamcmd-YYYYMMDD.log
    backups\
    steamcmd\
    staging\
```

Never commit live `data/`, `logs/`, or `backups/` from a running install.

---

## Documentation

- [docs/README.md](docs/README.md) — index
- [docs/repository-layout.md](docs/repository-layout.md) — folders in git vs live server/backup paths
- [docs/architecture.md](docs/architecture.md) — projects
- [docs/safety.md](docs/safety.md) — do not touch operator server/backup directories; no admin installs during repo setup

---

## Architecture

```text
ConanServerControl.sln
  src/ConanServerControl.App            WPF MVVM desktop host
  src/ConanServerControl.Core           models, settings, validation, interfaces
  src/ConanServerControl.Infrastructure SteamCMD, process manager, SQLite, backups
  src/ConanServerControl.Web            ASP.NET Core Web Admin
  tests/ConanServerControl.Tests        unit tests (no live Conan install required)
```

Dependency injection: `Microsoft.Extensions.DependencyInjection` + generic host.  
Logging: Serilog.  
HTTP: `IHttpClientFactory` for SteamCMD zip download.

---

## How to run tests

```powershell
dotnet test tests/ConanServerControl.Tests/ConanServerControl.Tests.csproj
```

Tests cover settings JSON, Workshop IDs, mod-order generation, backup retention, update-state transitions, path validation, password hashing, and process manager start/stop with a fake process.

---

## Next implementation step

**Phase 2 polish:** Workshop metadata polling (`ISteamRemoteStorage/GetPublishedFileDetails`), update-available badges, drag-and-drop load order, and the first-run wizard (import existing `modlist.txt`).

Then Phase 3–5: scheduled backups/updates, wait-until-empty, tray, Windows startup, full INI editor that preserves unknown keys.
