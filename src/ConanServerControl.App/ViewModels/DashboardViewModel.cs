using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConanServerControl.App.Services;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Diagnostics;

namespace ConanServerControl.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IServerProcessManager _server;
    private readonly ISettingsService _settings;
    private readonly IBackupService _backups;
    private readonly IServerUpdateService _updates;
    private readonly IDelayedRestartService _delayed;
    private readonly IActivityLog _activity;
    private readonly IUiDialogs _dialogs;
    private readonly IAppPaths _paths;
    private readonly DiagnosticsService _diagnostics;
    private readonly INetworkInfoService _network;

    public DashboardViewModel(
        IServerProcessManager server,
        ISettingsService settings,
        IBackupService backups,
        IServerUpdateService updates,
        IDelayedRestartService delayed,
        IActivityLog activity,
        IUiDialogs dialogs,
        IAppPaths paths,
        DiagnosticsService diagnostics,
        INetworkInfoService network)
    {
        _server = server;
        _settings = settings;
        _backups = backups;
        _updates = updates;
        _delayed = delayed;
        _activity = activity;
        _dialogs = dialogs;
        _paths = paths;
        _diagnostics = diagnostics;
        _network = network;
        ApplyState(_server.State);
        _server.StateChanged += (_, state) =>
            Application.Current?.Dispatcher.BeginInvoke(() => ApplyState(state));
        _ = ReloadActivityAsync();
    }

    [ObservableProperty] private string serverName = "Conan Dedicated Server";
    [ObservableProperty] private string serverIp = "127.0.0.1";
    [ObservableProperty] private string statusText = "OFFLINE";
    [ObservableProperty] private string statusBrush = "DangerBrush";
    [ObservableProperty] private string gamePort = "7777";
    [ObservableProperty] private string queryPort = "27015";
    [ObservableProperty] private string rconPort = "25575";
    [ObservableProperty] private string playerSummary = "0 / 10";
    [ObservableProperty] private string playersList = "No player query yet. RCON player list is used when configured.";
    [ObservableProperty] private string uptime = "—";
    [ObservableProperty] private string cpu = "—";
    [ObservableProperty] private string ram = "—";
    [ObservableProperty] private string version = "Unknown";
    [ObservableProperty] private string latestBuild = "Not checked";
    [ObservableProperty] private string modsSummary = "0 installed";
    [ObservableProperty] private string lastBackup = "Never";
    [ObservableProperty] private string lastUpdateCheck = "Never";
    [ObservableProperty] private string pipelineText = string.Empty;
    [ObservableProperty] private bool actionsLocked;
    [ObservableProperty] private string? errorText;

    public ObservableCollection<string> Activity { get; } = new();

    [RelayCommand]
    private Task StartAsync() => Run(() => _server.StartAsync());

    [RelayCommand]
    private Task StopAsync() => Run(() => _server.StopAsync());

    [RelayCommand]
    private Task RestartAsync() => Run(() => _server.RestartAsync());

    [RelayCommand]
    private async Task DelayedRestartAsync()
    {
        if (!_dialogs.Confirm("Delayed restart", "Warn players and restart in 10 minutes? You can cancel from the Updates page."))
        {
            return;
        }

        await Run(() => _delayed.StartAsync(new DelayedRestartRequest
        {
            Delay = RestartDelay.Minutes10,
            BackupFirst = true,
            Reason = "dashboard-delayed-restart"
        }));
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        await Run(async () =>
        {
            var result = await _updates.CheckAsync();
            LatestBuild = result.AvailableBuild ?? result.Summary;
            Version = result.InstalledBuild ?? Version;
            _dialogs.Alert("Update check", result.Summary);
        });
    }

    [RelayCommand]
    private Task UpdateServerAsync() => Run(() => _updates.UpdateAsync(restartAfter: true));

    [RelayCommand]
    private Task UpdateModsAsync()
    {
        _dialogs.Alert("Update mods", "Workshop metadata polling and bulk mod update automation is not finished yet. You can still add Workshop IDs on the Mods page and download them with SteamCMD.");
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task UpdateEverythingAsync() => Run(async () =>
    {
        await _updates.UpdateAsync(restartAfter: false);
        _dialogs.Alert("Update everything", "Server files were updated. Automatic Workshop update-all plus restart pipeline will land in the next implementation step.");
        await _server.StartAsync();
    });

    [RelayCommand]
    private Task BackupNowAsync() => Run(async () =>
    {
        var record = await _backups.BackupNowAsync("dashboard");
        LastBackup = record.CreatedAt.ToLocalTime().ToString("g");
        _dialogs.Alert("Backup", $"Backup created:{Environment.NewLine}{record.DirectoryPath}");
    });

    [RelayCommand]
    private void OpenServerFolder()
    {
        var dir = _settings.Current.ServerPaths.ServerInstallDirectory
                  ?? _settings.Current.ServerPaths.ServerWorkingDirectory;
        OpenDirectory(dir, "The dedicated server folder is not configured. Set it in Settings.");
    }

    [RelayCommand]
    private void OpenLogs() => OpenDirectory(_paths.LogsDirectory, "Logs directory was not created yet.");

    [RelayCommand]
    private void OpenWebAdmin()
    {
        if (!_settings.Current.WebAdmin.Enabled)
        {
            _dialogs.Alert("Web Admin", "Enable Web Admin and set a password on the Settings page, then restart the manager.");
            return;
        }

        var snap = _diagnostics.Capture();
        try
        {
            Process.Start(new ProcessStartInfo { FileName = snap.WebAdminUrl, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogs.Alert("Web Admin", $"Could not open {snap.WebAdminUrl}{Environment.NewLine}{ex.Message}");
        }
    }

    private async Task Run(Func<Task> work)
    {
        try
        {
            ActionsLocked = true;
            await work();
            await ReloadActivityAsync();
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
        finally
        {
            ActionsLocked = false;
        }
    }

    private void ApplyState(ServerRuntimeState state)
    {
        var server = _settings.Current.Server;
        ServerName = server.ServerName;
        ServerIp = _network.GetLanIPv4() ?? "127.0.0.1";
        StatusText = state.CrashLoopDetected ? "CRASH LOOP" : state.Status.ToString().ToUpperInvariant();
        StatusBrush = state.Status switch
        {
            ServerStatus.Online => "SuccessBrush",
            ServerStatus.Starting or ServerStatus.Restarting or ServerStatus.Updating => "WarningBrush",
            _ => "DangerBrush"
        };
        GamePort = server.GamePort.ToString();
        QueryPort = server.QueryPort.ToString();
        RconPort = server.RconPort.ToString();
        PlayerSummary = $"{state.PlayerCount} / {server.MaxPlayers}";
        PlayersList = state.Players.Count == 0
            ? "No players reported."
            : string.Join(Environment.NewLine, state.Players.Select(p => p.Name));
        Uptime = state.Uptime is null ? "—" : state.Uptime.Value.ToString(@"hh\:mm\:ss");
        Cpu = state.CpuUsagePercent is null ? "—" : $"{state.CpuUsagePercent:0.0}%";
        Ram = state.WorkingSetBytes is null ? "—" : $"{state.WorkingSetBytes.Value / 1024d / 1024d:0} MB";
        Version = state.InstalledBuild ?? "Unknown";
        LatestBuild = state.AvailableBuild ?? "Not checked";
        ModsSummary = $"{_settings.Current.Mods.Mods.Count} installed, {_settings.Current.Mods.Mods.Count(m => m.UpdateAvailable)} updates";
        LastBackup = state.LastBackupAt?.ToLocalTime().ToString("g") ?? LastBackup;
        LastUpdateCheck = state.LastUpdateCheckAt?.ToLocalTime().ToString("g") ?? LastUpdateCheck;
        ActionsLocked = state.ActionInProgress;
        ErrorText = state.CrashLoopDetected
            ? state.LastErrorGuidance ?? state.LastError
            : state.LastError;
        PipelineText = state.CurrentAction ?? string.Empty;
    }

    private async Task ReloadActivityAsync()
    {
        try
        {
            var items = await _activity.GetRecentAsync(12);
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Activity.Clear();
                foreach (var item in items.Reverse())
                {
                    Activity.Add($"{item.Timestamp.ToLocalTime():HH:mm} {item.Message}");
                }
            });
        }
        catch
        {
            // Activity table may not exist until the hosted initializer finishes.
        }
    }

    private void OpenDirectory(string? path, string missingMessage)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            _dialogs.Alert("Folder not found", missingMessage);
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }
}
