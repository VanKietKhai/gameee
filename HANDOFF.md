# Handoff to CONAN QA

## Feature Implemented

Phase 2 continuation: Workshop update detection plus real UPDATE MODS / UPDATE EVERYTHING pipelines.

P1 fix: server update no longer deadlocks on `IServerActionGate`. Orchestrated stop/start uses `StartUnderLockAsync` / `StopUnderLockAsync`.

## Files Changed

- `src/ConanServerControl.Core/Abstractions/IServerProcessManager.cs`
- `src/ConanServerControl.Core/Mods/WorkshopUpdateComparer.cs`
- `src/ConanServerControl.Core/Updates/UpdatePipelineStateMachine.cs`
- `src/ConanServerControl.Infrastructure/ProcessManagement/ServerProcessManager.cs`
- `src/ConanServerControl.Infrastructure/Updates/ServerUpdateService.cs`
- `src/ConanServerControl.Infrastructure/Workshop/WorkshopModService.cs`
- `src/ConanServerControl.Infrastructure/Workshop/SteamWorkshopClient.cs`
- `src/ConanServerControl.Infrastructure/ServiceCollectionExtensions.cs`
- `src/ConanServerControl.App/ViewModels/DashboardViewModel.cs`
- `src/ConanServerControl.App/ViewModels/SettingsViewModel.cs` (ModsViewModel commands)
- `src/ConanServerControl.App/Views/ModsView.xaml`
- `src/ConanServerControl.Web/WebAdminExtensions.cs`
- `src/ConanServerControl.Web/wwwroot/index.html`
- `tests/ConanServerControl.Tests/WorkshopUpdateTests.cs`
- `tests/ConanServerControl.Tests/ServerUpdatePipelineTests.cs`
- `STATUS.md`

## Expected Behavior

- **CHECK UPDATES** reads installed ACF build id (if present) and Steam Workshop `GetPublishedFileDetails` timestamps. It does **not** restart the server.
- Mods with a newer `time_updated` than last successful install are marked **Update available**.
- **UPDATE MODS** / Web `POST /api/server/update-mods`: backup (if enabled) → stop if running (under the same action lease) → SteamCMD workshop download per enabled mod → copy `.pak` via staging → write `modlist.txt` → start if it was running.
- **UPDATE SERVER** / **UPDATE EVERYTHING** can stop and start while the update lease is held (regression covered by unit test).
- Failed Workshop HTTP lookup does not delete installed mods.
- Dashboard **UPDATE MODS** is no longer an alert-only stub.

## Manual Test Steps

1. Configure a dedicated server install folder and SteamCMD on Windows.
2. Add a known Conan Workshop ID on Mods. Confirm SteamCMD download + `.pak` in `ConanSandbox\Mods` and `modlist.txt`.
3. Click **CHECK UPDATES**. Confirm names/timestamps fill in. Server process must stay in its previous state.
4. With the server **online**, click **UPDATE SERVER**. Confirm it stops, SteamCMD runs, then starts. It must not show “another action is already running” from the same pipeline.
5. Click **UPDATE MODS** with the server online. Confirm backup + stop + SteamCMD workshop + start.
6. Web Admin: **Update Mods** and **Update Server** buttons require login and call the new APIs.
7. Enable / Disable / Move Up / Move Down require a Workshop ID in the text box; Move warns about load order.

## Automated Tests

29 passed (`dotnet test tests/ConanServerControl.Tests/ConanServerControl.Tests.csproj -c Release`).

New coverage:

- Workshop timestamp comparison
- `CheckForUpdatesAsync` with a fake Steam client
- `ServerUpdateService.UpdateAsync` stop+start while holding the action gate (fake process + fake SteamCMD)

## Known Limitations

- Latest **dedicated server** Steam build id is still not compared (no Steam Web API key). `app_update` still refreshes files.
- `UpdateAllAsync` re-downloads every enabled mod, not only those flagged.
- Live SteamCMD / Unreal process not verified on this Linux agent.
- First-run wizard, INI editor, tray, wait-until-empty still not implemented.
- Mods UI is still a text list + Workshop ID field, not drag-and-drop.

## Areas QA Should Attack

- Update pipeline while the server is **online** (gate deadlock regression)
- Workshop check with invalid IDs, offline Steam, and a working installed `.pak` that must remain
- Load-order move then restart — `modlist.txt` order vs UI order
- Web Admin Update Mods/Server without CSRF issues / while another action is running
- Backup created before mod update; restore still refuses a running server

## Questions for CONAN ARCHITECT

- How should “latest dedicated server build” be obtained without a Steam Web API key? Options already considered: parse SteamCMD `app_info_print 443030` output; optional operator-supplied API key; treat SteamCMD `app_update` as the source of truth and skip comparison.
- Should `UpdateAllAsync` only download mods with `UpdateAvailable`, or always refresh every enabled mod (current)?
