# Repository layout

This git repository holds **source, tests, and documentation only**.

```text
ConanServerControl.sln
.gitignore
README.md
docs/                          this documentation
src/
  ConanServerControl.App/      WPF desktop host
  ConanServerControl.Core/     domain, settings, interfaces
  ConanServerControl.Infrastructure/
  ConanServerControl.Web/      local Web Admin
tests/
  ConanServerControl.Tests/
scripts/                       developer build helpers
data/                          empty placeholder (runtime data is not committed)
logs/                          empty placeholder
backups/                       empty placeholder
```

Runtime data after you run the manager on Windows lives under `%ProgramData%\ConanServerControl\` (or `CONAN_SERVER_CONTROL_DATA`). That location is outside this repository.

## Out of scope for this repo tree

These are **not** part of the git project and must not be committed or rewritten from repo initialization:

- A dedicated server install directory (example on some hosts: `D:\ConanServer`)
- A world-backup directory (example on some hosts: `D:\ConanBackups`)
- SteamCMD itself after it is downloaded to the data directory
- Conan Exiles client files
