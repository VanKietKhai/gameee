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
    private readonly IAppPaths _paths;
    private readonly IActivityLog _activityLog;
    private readonly ILogger<WorkshopModService> _logger;

    public WorkshopModService(
        ISettingsService settings,
        ISteamCmdService steamCmd,
        IBackupService backups,
        IAppPaths paths,
        IActivityLog activityLog,
        ILogger<WorkshopModService> logger)
    {
        _settings = settings;
        _steamCmd = steamCmd;
        _backups = backups;
        _paths = paths;
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

    public Task UpdateAsync(long workshopId, CancellationToken cancellationToken = default)
    {
        var mod = _settings.Current.Mods.Mods.FirstOrDefault(m => m.WorkshopId == workshopId)
                  ?? throw new UserFacingException("Mod not found", $"Workshop ID {workshopId} is not in the list.");
        return DownloadAndStageAsync(mod, cancellationToken);
    }

    public async Task UpdateAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var mod in _settings.Current.Mods.Mods.Where(m => m.Enabled).ToArray())
        {
            await DownloadAndStageAsync(mod, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "Workshop metadata polling is not implemented yet. Installed mods: {Count}.",
            _settings.Current.Mods.Mods.Count);
        return Task.CompletedTask;
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
        var staging = Path.Combine(_paths.StagingDirectory, "workshop", mod.WorkshopId.ToString());

        // Prefer app staging directory via settings-relative path reconstructed from steamcmd dir parent.
        var install = _settings.Current.ServerPaths.ServerInstallDirectory
                      ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        if (string.IsNullOrWhiteSpace(install))
        {
            throw new UserFacingException(
                "Server install directory is not configured",
                "Workshop mods are copied into the dedicated server Mods folder.",
                "Set the dedicated server folder in Settings before downloading mods.");
        }

        await _steamCmd.DownloadWorkshopItemAsync(mod.WorkshopId, staging, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var pak = Directory.EnumerateFiles(staging, "*.pak", SearchOption.AllDirectories).FirstOrDefault();
        if (pak is null)
        {
            throw new UserFacingException(
                "Workshop download did not contain a .pak file",
                $"Workshop ID {mod.WorkshopId} downloaded, but no .pak was found.",
                "Keep the previously installed version. Check the SteamCMD log, then try again.");
        }

        var modsDir = Path.Combine(install, "ConanSandbox", "Mods");
        Directory.CreateDirectory(modsDir);
        var dest = Path.Combine(modsDir, Path.GetFileName(pak));
        var tempDest = dest + ".new";
        File.Copy(pak, tempDest, overwrite: true);
        if (File.Exists(dest))
        {
            File.Replace(tempDest, dest, dest + ".bak");
        }
        else
        {
            File.Move(tempDest, dest);
        }

        await _settings.UpdateAsync(s =>
        {
            var found = s.Mods.Mods.First(m => m.WorkshopId == mod.WorkshopId);
            found.LocalFileName = Path.GetFileName(dest);
            found.InstalledTimestamp = DateTimeOffset.UtcNow;
            found.UpdateAvailable = false;
            found.Error = null;
        }, cancellationToken).ConfigureAwait(false);

        await WriteModListAsync(cancellationToken).ConfigureAwait(false);
        await _activityLog.AddAsync("Mods", $"Installed/updated Workshop mod {mod.WorkshopId}.", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

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
