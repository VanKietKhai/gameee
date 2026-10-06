# Mod #14 Dev Kit / probe tooling

Helper scripts used for the Mod #14 compatibility gate. They are read-only, apart from the explicitly named creation scripts.

| File | Purpose |
|---|---|
| `probeA_create.py` / `probeB_create.py` / `create_probe.py` | Editor Python (`-ExecutePythonScript` or `py <file>` in the editor Cmd box). Each creates the single probe asset in the **active** mod and refuses to run otherwise. |
| `probeB_doc.py` | Editor Python, read-only. Documents the Probe B controller (parent, interfaces, replication, tick, components, graph) and the official DLC controllers' parents. Writes `probeB_definition.json` next to itself. |
| `probe_trace.py` | Editor Python, read-only. Exports the Quest 01 trace (SpawnDataTable rows, Journey row, Dregs map actors). |
| `probe_cycle.ps1` | One staging probe cycle through the harness: precheck (offline, exact 13 mods) → verified cold backup → import as #14 → mod-boot → remove → restore → exact 13. Paths target the Steam install `D:\steamnew\...`. |
| `iostore_strings.py`, `uasset_names.py`, `modinfo_dump.py`, `find_strings.py`, `utoc_dir_scan.py` | Read-only inspectors for paks, IoStore containers, uasset name tables and binaries. Use them on copies only. |

Active mod switching: `RunUAT.bat -NoCompile SetActiveMod -Mod=<Mod> -Project=D:/epic/CEUE5Devkit/UE4/ConanSandbox.uproject -ScriptDir=D:/epic/CEUE5Devkit/UE4/`.
Builds must use the Dev Kit GUI **Build mod** button: that step writes `devkitRevisionNumber` 1002. A command-line `BuildMod` copies `modinfo.json` verbatim.
