# M3 — Task 4 Handoff (in progress): Guarded Live Integration + Standalone-First Pivot

Branch: `claude/m3-task4-live-windows`, from the QA-verified `0352f4040208fc48590b15e92a4933c08e8b7405`.

Live details are in **`M3_LIVE_TEST_REPORT.md`**. Task 3's handoff is in git history (`0352f40`).

## Baseline

- 203 / 203 on the QA revision. Release build 0 warnings / 0 errors.

## Commits on this branch

| Commit | Content |
| --- | --- |
| `5b4fea0` | Hard server-executable gate; QA-018 path normalization; `LiveTestGuard` |
| `4e128d4` | Opt-in guarded live harness (`tests/ConanServerControl.LiveHarness`) |
| `84c5bd8` | Standalone-first: Local `.pak` mods, Client Mod Bundle, optional SteamCMD, existing-server support |
| (docs) | `M3_LIVE_TEST_REPORT.md`, `STATUS.md`, `HANDOFF.md` |

## Hard executable gate (QA-018 / wrong-path risk)

- `Core/Diagnostics/ServerExecutableGate`: Start is allowed **only** for `ConanSandboxServer.exe` that is not inside the standalone client folder.
- These are **blocked**: `Run Me!.bat`, client `ConanSandbox.exe` / `-Shipping`, `.bat/.cmd/.ps1/.vbs/.lnk`, `ConanSandboxServer-Win64-Shipping.exe`, unknown names, empty.
- The block is enforced in `ServerProcessManager.StartCoreAsync` before any status change or process start (the previous behaviour only warned). It throws `UserFacingException("Server start blocked")`.
- Diagnostics `server.executable` uses the same gate (`StartAllowed` fact).

## QA-018 path normalization

- `PathValidator.NormalizeFullPath`, `PathsEqual` and `Overlaps` were added.
- `IsUnderRoot` now compares normalized full paths, so `X`, `X\`, `x/` and different case are the same directory. A sibling such as `X2` is still not under `X`.
- Limitation: Windows 8.3 short names are not expanded.

## Live harness and guard

- `Core/LiveTesting/LiveTestGuard`:
  - layout `<root>\{steamcmd,server,live-test,app-data}`
  - overlap validation (protected client and launcher folder, drive root, sibling nesting)
  - `CSC_LIVE_TESTS=1` **and** the `.csc-live-test` marker required
  - `EnsureInsideMarkedWorkspace` for destructive actions
- Harness commands, one checkpoint per run: `plan`, `init`, `diag`, `install-steamcmd`, `install-server`, `boot`, `cycle`, `backup`, `add-mod`, `update-mod`, `mod-boot`, `client-snapshot`, `client-compare`.
  - It uses the real DI graph (hosted services are not started) and refuses to start when any foreign Conan server process exists.
  - It logs redacted JSONL to `live-test\m3-live-log.jsonl`.
- The harness is a console project (`IsTestProject=false`), so `dotnet test` never runs it.

## Live checkpoints

| Checkpoint | Result |
| --- | --- |
| 4A SteamCMD | **PASS**. `E:\CSC-M3-Live\steamcmd\steamcmd.exe`, 10.7 s. Diagnostics went from NOT READY (SteamCMD the sole blocker) to READY. |
| 4B Dedicated server install | **BLOCKED BY NETWORK** (exit -2, 3 s). Valve's Fastly CDN hosts get a TLS reset from this network; Akamai hosts work. Nothing was installed or deleted. Reclassified as optional integration, not a core blocker. |
| 4C–4G | Not started (no dedicated server available). |

## Standalone-first architecture (new)

**Mod source model.**
- `ModSourceType { Workshop = 0, Local = 1 }` on `WorkshopMod`. A missing value means Workshop, so old settings are backwards compatible (tested).
- Local mods have `WorkshopId = 0`, `LocalSourcePath` (metadata only) and `Sha256`.
- Identity is `ModKeys`: `workshop:<id>` or `local:<file.pak>`. A Local mod's identity is its `.pak` file name, which is also its `modlist.txt` and Mods-folder identity.

**Local mod import / manual update.**
- `IModCatalogService`, implemented by `WorkshopModService` (`WorkshopModService.Local.cs`):
  - `StageLocalPakAsync` checks: absolute path, file (not folder), `.pak`, safe name, non-zero size, not already inside the server Mods folder or staging.
  - It then copies (`FileMode.CreateNew`) to `%DATA%\staging\local\<guid>\` and hashes both the source and the copy. If the source changed during the copy, staging is rejected.
- `IServerUpdateService.ImportLocalModAsync` / `ReplaceLocalModAsync`:
  1. Stage first, so a bad file never stops the server.
  2. Run the existing `RunLockedAsync`: stop if running → verified cold backup (`pre-local-mod-import` / `pre-local-mod-update`) → commit → restart only if it was running.
- `CommitLocalPakAsync` uses `ModBatchTransaction`: rollback copy, replace, **post-replace SHA-256 check**, persist, `modlist.txt`. Any failure rolls back the file and reverts the catalog entry.
- Refusals:
  - duplicate file name (any source)
  - a *different* unmanaged file already in Mods (never overwritten)
  - replacing a Workshop mod from a local file
  - a replacement with a different file name
- Local mods show as `LOCAL (manual update)`. They are skipped by `ApplyUpdatesAsync(null)` (Update All) and `CheckForUpdatesAsync`, so nothing pretends an automatic update exists.
- Safety fix on the Workshop path: a download whose `.pak` name belongs to another mod is rejected before any live change.
- Key-based `RemoveAsync` / `SetEnabledAsync` / `MoveAsync(string modKey)`. The `long` overloads delegate and reject `workshopId <= 0`.

**Client Mod Bundle.**
- `IClientModBundleService` / `ClientModBundleService`. The default parent is `%DATA%\client-bundles`.
- Output `ConanClientModBundle-<stamp>\`:
  - `Mods\*.pak` and `Mods\modlist.txt` (enabled mods in load order)
  - `manifest.json` (bundleVersion, createdAt, generator, notice, and per mod: loadOrder, fileName, sizeBytes, sha256, sourceType, workshopId or null, name)
  - `README.txt`
- Every copy is verified by SHA-256. The bundle is assembled in a hidden `.partial` folder and renamed at the end. On failure only that partial folder is removed.
- Export refuses any folder overlapping the standalone client, its launcher folder, or the dedicated server install, including trailing-separator variants. No game files are included.
- `ClientModSyncPlanner` (via `PlanClientSync`) is **read-only**. Per mod it reports Copy / Replace / UpToDate, plus extra client paks and whether `modlist.txt` matches. It rejects unsafe manifest names.
- **Nothing was written to the real client.**

**Existing dedicated server and optional SteamCMD.**
- `DedicatedServerLocator.Find(root)` checks `root\ConanSandboxServer.exe`, then `ConanSandbox\Binaries\Win64\ConanSandboxServer.exe`, and never returns `-Shipping`. It is used by diagnostics, `InstallDetector` (which previously could pick the `-Shipping` exe that the gate now blocks) and the harness.
- Settings has a new **Use existing server installation** button. It picks the folder, then locates the exe and checks it with the gate before setting the paths.
- Readiness: `READY FOR SERVER LIVE TEST` = (valid existing server exe **or** usable SteamCMD) **and** safe workspace / backup root / settings. When there is no source, a single blocker names both options.
- Diagnostics wording now says SteamCMD and Workshop are optional. New `mods.sources` check: counts Local and Workshop mods, and warns when enabled Workshop mods exist without SteamCMD.

**UI.**
- Mods page: **IMPORT LOCAL .PAK**, **REPLACE LOCAL .PAK**, **EXPORT CLIENT BUNDLE**. The input box accepts a Workshop ID or a local `.pak` name. The listing shows the source.
- Settings: **Use existing server installation**.
- The Mods page was rendered in the real app against an isolated data directory.

## Tests added

- `M3LiveSafetyTests` (33 test cases): gate (8 blocked inputs, allowed, trailing-slash client root ×4), process manager blocks `Run Me!.bat` and the client exe with 0 launches, QA-018 equality and sibling cases, drive root, layout validation, env + marker, destructive refusal.
- `M3StandaloneFirstTests` (36 test cases):
  - Local import: happy path with an untouched source and no SteamCMD; 5 invalid inputs; source inside Mods; duplicate name; unmanaged-file protection.
  - Pipeline: offline order backup → commit; online stop → backup → start; backup failure blocks the commit.
  - Replace: success, different name rejected, rollback restores file and catalog, Workshop not replaceable.
  - Local mods untouched by Update All and update checks; Workshop/Local collision rejected; key ops and long-0 rejection; shareable list.
  - Key parsing; old-settings compatibility.
  - Bundle: contents, order, manifest and hashes, server untouched; protected locations; atomic failure.
  - Sync plan read-only (Copy / Replace / UpToDate / extras / in sync); unsafe manifest.
  - Locator; readiness ×3; diagnostics end-to-end ×3.

## Build

`dotnet build -c Release --no-incremental`: **0 warnings, 0 errors** (6 projects including the harness).

## Tests

`dotnet test -c Release`: **272 / 272** passed, 0 failed, 0 skipped. That is the 203 baseline plus 33 pre-live safety cases plus 36 standalone-first cases. No existing test was deleted, skipped or weakened.

## Known limitations

- No dedicated server boot yet. Every server, world, backup, mod-load and client item is **not live verified**.
- The client `modlist.txt` format (one file name per line, like the server) is an assumption until 4F.
- The real `ConanSandboxServer.exe` location in a dedicated server install has not been seen live. The locator supports both candidate locations.
- The cold safety backup requires an existing `ConanSandbox\Saved` folder, so importing a mod into a never-booted server aborts at the backup step (Task 2 policy, unchanged).
- Workshop mod download and automatic server install/update need SteamCMD and Valve's Fastly CDN, which this network blocks.
- Attach-to-existing-process still matches any `ConanSandboxServer` process system-wide. The harness refuses to start if one exists.
- The Web Admin has no Local-mod or bundle endpoints (WPF only).

## Next required input (before 4C first boot)

A **valid existing Conan Exiles Dedicated Server installation** containing `ConanSandboxServer.exe`.

Legitimate options:
1. Run SteamCMD `app_update 443030` (free, anonymous) on a network or machine where Valve's CDN is reachable, then copy the resulting folder to this PC.
2. Copy an existing dedicated server install you already have from another machine.
3. Fix the Fastly reachability on this network, outside this tool, and retry 4B.

Constraints:
- Place the install outside `D:\conan exiles\`, for example `E:\CSC-M3-Live\server`. Then use **Use existing server installation**, or tell me the path so the harness can validate it with the gate.
- The standalone game client (`ConanSandbox.exe` / `Run Me!.bat`) is **not** a dedicated server and will be refused.

Then 4C runs as planned: first boot on a throwaway world, readiness, Start/Stop/Restart, cold backup (4D), one Local mod (4E). 4F client observation is subject to the authentication risk in the live report.
