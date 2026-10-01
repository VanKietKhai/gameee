using ConanServerControl.Core;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Mods;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Validation;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Workshop;

/// <summary>
/// Workshop catalog and ordering. SteamCMD download/sync is implemented;
/// remote Workshop metadata polling is Phase 2 and is marked clearly.
/// </summary>
public sealed class WorkshopModService : IWorkshopModService
{
    private readonly ISettingsService _settings;
    private readonly ISteamCmdService _steamCmd;
    private readonly IBackupService _backups;
    private readonly IServerActionGate _actionGate;
    private readonly IAppPaths _paths;
    private readonly ISteamWorkshopClient _workshopClient;
    private readonly IActivityLog _activityLog;
    private readonly ILogger<WorkshopModService> _logger;

    public WorkshopModService(
        ISettingsService settings,
        ISteamCmdService steamCmd,
        IBackupService backups,
        IServerActionGate actionGate,
        IAppPaths paths,
        ISteamWorkshopClient workshopClient,
        IActivityLog activityLog,
        ILogger<WorkshopModService> logger)
    {
        _settings = settings;
        _steamCmd = steamCmd;
        _backups = backups;
        _actionGate = actionGate;
        _paths = paths;
        _workshopClient = workshopClient;
        _activityLog = activityLog;
        _logger = logger;
    }

    public IReadOnlyList<WorkshopMod> Mods => _settings.Current.Mods.Mods.OrderBy(m => m.LoadOrder).ToArray();

    public async Task AddAsync(long workshopId, CancellationToken cancellationToken = default)
    {
        if (workshopId <= 0)
        {
            throw new UserFacingException("Invalid Workshop ID", WorkshopIdValidator.DescribeRule());
        }

        if (_settings.Current.Mods.Mods.Any(m => m.WorkshopId == workshopId))
        {
            throw new UserFacingException(
                "Mod already added",
                $"Workshop ID {workshopId} is already in the mod list.",
                "Enable it or move it in the Mods page instead of adding it again.");
        }

        var mod = new WorkshopMod
        {
            WorkshopId = workshopId,
            Name = $"Workshop {workshopId}",
            Enabled = true,
            LoadOrder = _settings.Current.Mods.Mods.Count + 1
        };

        await _settings.UpdateAsync(s => s.Mods.Mods.Add(mod), cancellationToken).ConfigureAwait(false);
        await _activityLog.AddAsync("Mods", $"Added Workshop mod {workshopId}.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await DownloadAndStageAsync(mod, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Workshop download for {Id} failed. The mod remains listed but not installed.", workshopId);
            await _settings.UpdateAsync(s =>
            {
                var found = s.Mods.Mods.FirstOrDefault(m => m.WorkshopId == workshopId);
                if (found is not null)
                {
                    found.Error = ex is UserFacingException ufe ? ufe.Message : ex.Message;
                }
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RemoveAsync(long workshopId, bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            throw new UserFacingException(
                "Confirm mod removal",
                "Removing a mod from an existing Conan world may permanently remove modded buildings, items, NPCs or other save data.",
                "Create a backup, then confirm removal. The manager will not remove a mod because Workshop is temporarily unavailable.");
        }

        if (_settings.Current.Backups.BackupBeforeModRemoval)
        {
            await _backups.BackupNowAsync("pre-mod-removal", cancellationToken).ConfigureAwait(false);
        }

        await _settings.UpdateAsync(s =>
        {
            s.Mods.Mods.RemoveAll(m => m.WorkshopId == workshopId);
            ModListGenerator.ApplySequentialOrder(s.Mods.Mods);
        }, cancellationToken).ConfigureAwait(false);

        await WriteModListAsync(cancellationToken).ConfigureAwait(false);
        await _activityLog.AddAsync("Mods", $"Removed Workshop mod {workshopId}.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetEnabledAsync(long workshopId, bool enabled, CancellationToken cancellationToken = default)
    {
        await _settings.UpdateAsync(s =>
        {
            var mod = s.Mods.Mods.FirstOrDefault(m => m.WorkshopId == workshopId)
                      ?? throw new UserFacingException("Mod not found", $"Workshop ID {workshopId} is not in the list.");
            mod.Enabled = enabled;
        }, cancellationToken).ConfigureAwait(false);

        await WriteModListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MoveAsync(long workshopId, int newIndex, CancellationToken cancellationToken = default)
    {
        await _settings.UpdateAsync(s =>
        {
            var ordered = s.Mods.Mods.OrderBy(m => m.LoadOrder).ToList();
            var current = ordered.FindIndex(m => m.WorkshopId == workshopId);
            if (current < 0)
            {
                throw new UserFacingException("Mod not found", $"Workshop ID {workshopId} is not in the list.");
            }

            var moved = ModListGenerator.Reorder(ordered, current, newIndex);
            s.Mods.Mods.Clear();
            s.Mods.Mods.AddRange(moved);
        }, cancellationToken).ConfigureAwait(false);

        await WriteModListAsync(cancellationToken).ConfigureAwait(false);
        await _activityLog.AddAsync("Mods", $"Changed mod load order for {workshopId}.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public Task UpdateAsync(long workshopId, CancellationToken cancellationToken = default) =>
        RunGatedAsync("Update selected mods", () => ApplyUpdatesAsync(new[] { workshopId }, cancellationToken));

    public Task UpdateAllAsync(CancellationToken cancellationToken = default) =>
        RunGatedAsync("Update mods", () => ApplyUpdatesAsync(workshopIds: null, cancellationToken));

    public async Task ApplyUpdatesAsync(IReadOnlyList<long>? workshopIds, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkshopMod> targets;
        if (workshopIds is null)
        {
            targets = _settings.Current.Mods.Mods.Where(m => m.Enabled).ToArray();
        }
        else
        {
            var wanted = workshopIds.ToHashSet();
            targets = _settings.Current.Mods.Mods.Where(m => wanted.Contains(m.WorkshopId)).ToArray();
            var missing = wanted.Except(targets.Select(m => m.WorkshopId)).ToArray();
            if (missing.Length > 0)
            {
                throw new UserFacingException("Mod not found", $"Workshop ID {missing[0]} is not in the list.");
            }
        }

        var staged = new List<(WorkshopMod Mod, StagedWorkshopPak Pak)>(targets.Count);
        try
        {
            foreach (var mod in targets)
            {
                var pak = await DownloadAndValidateAsync(mod, cancellationToken).ConfigureAwait(false);
                staged.Add((mod, pak));
            }

            foreach (var item in staged)
            {
                await CommitStagedAsync(item.Mod, item.Pak, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (var item in staged)
            {
                TryDeleteStaging(item.Pak.StagingDirectory);
            }
        }
    }

    private async Task RunGatedAsync(string action, Func<Task> work)
    {
        if (!_actionGate.TryBegin(action, out var lease) || lease is null)
        {
            throw new UserFacingException(
                "Another action is already running",
                $"Current action: {_actionGate.CurrentAction}",
                "Wait for it to finish before updating mods.");
        }

        try
        {
            await work().ConfigureAwait(false);
        }
        finally
        {
            lease.Dispose();
        }
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var mods = _settings.Current.Mods.Mods.ToArray();
        if (mods.Length == 0)
        {
            return;
        }

        var details = await _workshopClient.GetPublishedFileDetailsAsync(
                mods.Select(m => m.WorkshopId).ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        var byId = details.ToDictionary(d => d.WorkshopId);

        var updates = 0;
        await _settings.UpdateAsync(s =>
        {
            foreach (var mod in s.Mods.Mods)
            {
                mod.LastChecked = DateTimeOffset.UtcNow;
                if (!byId.TryGetValue(mod.WorkshopId, out var remote))
                {
                    mod.Error = "Steam did not return Workshop details for this ID. The installed copy was not removed.";
                    continue;
                }

                mod.Name = remote.Title;
                if (!string.IsNullOrWhiteSpace(remote.FileName) && string.IsNullOrWhiteSpace(mod.LocalFileName))
                {
                    mod.LocalFileName = remote.FileName;
                }

                mod.LatestWorkshopTimestamp = remote.TimeUpdated;
                mod.UpdateAvailable = WorkshopUpdateComparer.IsUpdateAvailable(mod.InstalledTimestamp, remote.TimeUpdated);
                mod.Error = null;
                if (mod.UpdateAvailable)
                {
                    updates++;
                }
            }
        }, cancellationToken).ConfigureAwait(false);

        if (updates > 0)
        {
            await _activityLog.AddAsync(
                    "Mods",
                    $"Workshop update detected: {updates} mod(s).",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation("Workshop check complete. {Updates} of {Total} mods need updates.", updates, mods.Length);
    }

    public string GetShareableModList()
    {
        var lines = new List<string>
        {
            "Conan Server Control — required Workshop mods",
            "Players need a licensed Conan Exiles client. Steam Workshop installs client mods.",
            "This manager does not distribute game files and does not bypass Steam DRM.",
            string.Empty
        };

        foreach (var mod in Mods)
        {
            var status = mod.Enabled ? "enabled" : "disabled";
            lines.Add($"{mod.LoadOrder}. {mod.Name} ({mod.WorkshopId}) [{status}] https://steamcommunity.com/sharedfiles/filedetails/?id={mod.WorkshopId}");
        }

        if (Mods.Count == 0)
        {
            lines.Add("(no mods configured)");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task DownloadAndStageAsync(WorkshopMod mod, CancellationToken cancellationToken)
    {
        var staged = await DownloadAndValidateAsync(mod, cancellationToken).ConfigureAwait(false);
        try
        {
            await CommitStagedAsync(mod, staged, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteStaging(staged.StagingDirectory);
        }
    }

    private async Task<StagedWorkshopPak> DownloadAndValidateAsync(WorkshopMod mod, CancellationToken cancellationToken)
    {
        var install = _settings.Current.ServerPaths.ServerInstallDirectory
                      ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        if (string.IsNullOrWhiteSpace(install))
        {
            throw new UserFacingException(
                "Server install directory is not configured",
                "Workshop mods are copied into the dedicated server Mods folder.",
                "Set the dedicated server folder in Settings before downloading mods.");
        }

        var staging = Path.Combine(_paths.StagingDirectory, "workshop", mod.WorkshopId.ToString());
        PrepareFreshStaging(staging);
        var startedUtc = DateTime.UtcNow;

        await _steamCmd.DownloadWorkshopItemAsync(mod.WorkshopId, staging, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!Directory.Exists(staging))
        {
            throw new UserFacingException(
                "Workshop download did not contain a .pak file",
                $"Workshop ID {mod.WorkshopId} downloaded, but the staging folder was missing.",
                "Keep the previously installed version. Check the SteamCMD log, then try again.");
        }

        var candidates = Directory.GetFiles(staging, "*.pak", SearchOption.AllDirectories);
        if (candidates.Length == 0)
        {
            throw new UserFacingException(
                "Workshop download did not contain a .pak file",
                $"Workshop ID {mod.WorkshopId} downloaded, but no .pak was found.",
                "Keep the previously installed version. Check the SteamCMD log, then try again.");
        }

        if (candidates.Length > 1)
        {
            var names = string.Join(", ", candidates.Select(Path.GetFileName));
            throw new UserFacingException(
                "Workshop download contained multiple .pak files",
                $"Workshop ID {mod.WorkshopId} produced {candidates.Length} .pak files ({names}). This manager installs one .pak per Workshop item and will not guess which file to use.",
                "Keep the previously installed version. Remove extra files from the Workshop item or install it manually.");
        }

        var pak = candidates[0];
        var info = new FileInfo(pak);
        if (info.Length <= 0)
        {
            throw new UserFacingException(
                "Workshop download produced an empty .pak file",
                $"Workshop ID {mod.WorkshopId} downloaded {info.Name} with 0 bytes.",
                "Keep the previously installed version. Check the SteamCMD log, then try again.");
        }

        if (info.LastWriteTimeUtc < startedUtc.AddSeconds(-5))
        {
            throw new UserFacingException(
                "Workshop download reused a leftover staging file",
                $"Workshop ID {mod.WorkshopId} did not produce a new .pak. {info.Name} is older than this download.",
                "Keep the previously installed version. Check the SteamCMD log, then try again.");
        }

        var fileName = info.Name;
        if (!PathValidator.IsSafeRelativeName(fileName))
        {
            throw new UserFacingException(
                "Workshop download contained an unsafe file name",
                fileName,
                "Keep the previously installed version.");
        }

        return new StagedWorkshopPak(mod.WorkshopId, pak, fileName, staging);
    }

    private async Task CommitStagedAsync(WorkshopMod mod, StagedWorkshopPak staged, CancellationToken cancellationToken)
    {
        var install = _settings.Current.ServerPaths.ServerInstallDirectory
                      ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        if (string.IsNullOrWhiteSpace(install))
        {
            throw new UserFacingException(
                "Server install directory is not configured",
                "Workshop mods are copied into the dedicated server Mods folder.",
                "Set the dedicated server folder in Settings before downloading mods.");
        }

        var modsDir = Path.Combine(install, "ConanSandbox", "Mods");
        Directory.CreateDirectory(modsDir);
        var dest = Path.Combine(modsDir, staged.FileName);
        var tempDest = dest + ".new";
        File.Copy(staged.PakPath, tempDest, overwrite: true);
        if (File.Exists(dest))
        {
            File.Replace(tempDest, dest, dest + ".bak");
        }
        else
        {
            File.Move(tempDest, dest, overwrite: true);
        }

        await _settings.UpdateAsync(s =>
        {
            var found = s.Mods.Mods.First(m => m.WorkshopId == mod.WorkshopId);
            found.LocalFileName = staged.FileName;
            found.InstalledTimestamp = DateTimeOffset.UtcNow;
            found.UpdateAvailable = false;
            found.Error = null;
        }, cancellationToken).ConfigureAwait(false);

        await WriteModListAsync(cancellationToken).ConfigureAwait(false);
        await _activityLog.AddAsync("Mods", $"Installed/updated Workshop mod {mod.WorkshopId}.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static void PrepareFreshStaging(string staging)
    {
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);
    }

    private void TryDeleteStaging(string staging)
    {
        try
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not delete Workshop staging directory {Path}", staging);
        }
    }

    private readonly record struct StagedWorkshopPak(
        long WorkshopId,
        string PakPath,
        string FileName,
        string StagingDirectory);

    private async Task WriteModListAsync(CancellationToken cancellationToken)
    {
        var install = _settings.Current.ServerPaths.ServerInstallDirectory
                      ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        if (string.IsNullOrWhiteSpace(install))
        {
            return;
        }

        var modsDir = Path.Combine(install, "ConanSandbox", "Mods");
        Directory.CreateDirectory(modsDir);
        var path = Path.Combine(modsDir, AppConstants.ModListFileName);
        var content = ModListGenerator.Generate(_settings.Current.Mods.Mods);
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }
}
