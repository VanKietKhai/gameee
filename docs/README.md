# Documentation

This folder is the written layout for **Conan Server Control** (the Conan Exiles dedicated-server manager).

| File | Topic |
| --- | --- |
| [repository-layout.md](repository-layout.md) | Folders in this git repository |
| [architecture.md](architecture.md) | Projects and responsibilities |
| [safety.md](safety.md) | Paths this repo must never touch; admin / install rules |

Live dedicated-server files and world backups are **not** stored here. They belong in the operator’s configured directories (for example a Windows install folder and a backup folder chosen in Settings). This repository does not create, scan, or modify those trees.
