# Architecture (high level)

Conan Server Control is a local Windows manager. Players still use a licensed Steam copy of Conan Exiles. This application does not distribute the game client and does not bypass Steam DRM.

```text
Desktop (WPF)  ──hosts──►  Core + Infrastructure
        │                         │
        └──embeds──►  Web Admin (ASP.NET Core, opt-in, auth required)
```

| Project | Role |
| --- | --- |
| `ConanServerControl.App` | Dashboard, settings, diagnostics. Executable name: `ConanServerControl.exe` |
| `ConanServerControl.Core` | Models, validation, settings types, service interfaces |
| `ConanServerControl.Infrastructure` | SteamCMD, process start/stop, backups, SQLite activity log |
| `ConanServerControl.Web` | Phone-friendly Web Admin bound to localhost by default |
| `ConanServerControl.Tests` | Unit tests that do not need a live Conan install |

See the root [README.md](../README.md) for what is implemented today versus later phases.
