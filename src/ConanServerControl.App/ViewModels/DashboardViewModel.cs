using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConanServerControl.App.Services;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;

namespace ConanServerControl.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IServerProcessManager _server;
    private readonly ISettingsService _settings;
    private readonly IActivityLog _activity;
    private readonly IUiDialogs _dialogs;
    private readonly INetworkInfoService _network;

    public DashboardViewModel(
        IServerProcessManager server,
        ISettingsService settings,
        IActivityLog activity,
        IUiDialogs dialogs,
        INetworkInfoService network)
    {
        _server = server;
        _settings = settings;
        _activity = activity;
        _dialogs = dialogs;
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
    [ObservableProperty] private string clientBuild = string.Empty;
    [ObservableProperty] private string modsSummary = "0 installed";
    [ObservableProperty] private string lastBackup = "Never";
    [ObservableProperty] private string lastUpdateCheck = "Never";
    [ObservableProperty] private string pipelineText = string.Empty;
    [ObservableProperty] private bool actionsLocked;
    [ObservableProperty] private string? errorText;
    [ObservableProperty] private string copyFeedback = string.Empty;

    /// <summary>Address friends type into Direct Connect: the advertised IP plus the game port.</summary>
    public string JoinAddress => $"{ServerIp}:{GamePort}";

    partial void OnServerIpChanged(string value) => OnPropertyChanged(nameof(JoinAddress));

    partial void OnGamePortChanged(string value) => OnPropertyChanged(nameof(JoinAddress));

    [RelayCommand]
    private async Task CopyJoinAddressAsync()
    {
        try
        {
            Clipboard.SetText(JoinAddress);
            CopyFeedback = $"Đã copy {JoinAddress}";
        }
        catch (Exception ex)
        {
            CopyFeedback = string.Empty;
            _dialogs.Alert("Copy", $"Không copy được vào clipboard:{Environment.NewLine}{ex.Message}");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(3));
        CopyFeedback = string.Empty;
    }

    public ObservableCollection<string> Activity { get; } = new();

    [RelayCommand]
    private Task StartAsync() => Run(() => _server.StartAsync());

    [RelayCommand]
    private Task StopAsync() => PlayersAgree("tắt") ? Run(() => _server.StopAsync()) : Task.CompletedTask;

    [RelayCommand]
    private Task RestartAsync() => PlayersAgree("khởi động lại") ? Run(() => _server.RestartAsync()) : Task.CompletedTask;

    private bool PlayersAgree(string action)
    {
        var count = _server.State.PlayerCount;
        return count == 0
            || _dialogs.Confirm("Đang có người chơi", $"Đang có {count} người chơi online. Vẫn {action} server ngay?");
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
        // Private friends-only deployment: friends connect over Radmin VPN when it is present.
        ServerIp = _network.GetRadminVpnIPv4() ?? _network.GetLanIPv4() ?? "127.0.0.1";
        StatusText = state.CrashLoopDetected ? "CRASH LOOP" : state.Status.ToString().ToUpperInvariant();
        StatusBrush = state.Status switch
        {
            ServerStatus.Online => "SuccessBrush",
            ServerStatus.Starting or ServerStatus.Restarting or ServerStatus.Updating => "WarningBrush",
            _ => "DangerBrush"
        };
        GamePort = server.GamePort.ToString();
        QueryPort = server.QueryPort.ToString();
        RconPort = _settings.Current.Rcon.Port.ToString();
        PlayerSummary = $"{state.PlayerCount} / {server.MaxPlayers}";
        PlayersList = state.Players.Count == 0
            ? "No players reported."
            : string.Join(Environment.NewLine, state.Players.Select(p => p.Name));
        Uptime = state.Uptime is null ? "—" : state.Uptime.Value.ToString(@"hh\:mm\:ss");
        Cpu = state.CpuUsagePercent is null ? "—" : $"{state.CpuUsagePercent:0.0}%";
        Ram = state.WorkingSetBytes is null ? "—" : $"{state.WorkingSetBytes.Value / 1024d / 1024d:0} MB";
        Version = state.InstalledBuild ?? "Unknown";
        LatestBuild = state.AvailableBuild is null
            ? "Steam: chưa kiểm tra"
            : state.ServerUpdateAvailable
                ? $"Steam có bản mới: {state.AvailableBuild}"
                : $"Steam: {state.AvailableBuild} (mới nhất)";
        ClientBuild = state.ClientInstalledBuild is null
            ? string.Empty
            : state.ClientUpdateAvailable
                ? $"Client {state.ClientInstalledBuild} - có bản mới {state.ClientAvailableBuild}, mở Steam để cập nhật"
                : $"Client {state.ClientInstalledBuild}";
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
}
