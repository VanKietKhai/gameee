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

## Mod #14 controller build (2026-10-07)

`mq14_build_controller.py` builds `/Game/Mods/MQ14MainQuest/BP_MQ14MainQuestController` with the editor's `BlueprintGraphEditor` API and writes `mq14_controller_definition.json`. Run it **once per editor session**, headless:

```
UnrealEditor.exe "D:\epic\CEUE5Devkit\UE4\ConanSandbox.uproject" -ModDevKit -ExecutePythonScript="E:/github/gameee/mods/CustomMainQuestline/tools/devkit/mq14_build_controller.py"
```

The Dev Kit exits by itself when the script ends, discarding anything unsaved. The script saves only after a clean compile. To rebuild, delete `UE4\Content\Mods\MQ14MainQuest\Local\BP_MQ14MainQuestController.uasset` first. Running the builder twice in one session, or rebuilding in place, crashed the editor inside `BlueprintEditorLibrary`. Python remote execution does not start in this Dev Kit build.

Facts found while building: the persistence call is `Dreamworld|Persistence|Setdirtyflag` (ActorPersistenceComponent); the death hook is `State|BindEventtoSignalonKilled` (ConanCharacter `SignalOnKilled(Character, Killer)`); const-ref string/text parameters (`ClientHUDShowNotification`, `ClientShowRichMessageBox`) need `MakeLiteralString`/`MakeLiteralText`; `GetAllActorsOfClass` and `GetActorStableId` are impure and must sit in the exec chain. The variable SaveGame flag cannot be set from Python; it is set in the Blueprint editor's variable Details panel.
