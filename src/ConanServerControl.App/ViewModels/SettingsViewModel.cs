using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConanServerControl.App.Services;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Diagnostics;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Mods;
using ConanServerControl.Core.Security;
using ConanServerControl.Core.Settings;
using ConanServerControl.Core.Validation;
using ConanServerControl.Infrastructure.Diagnostics;

namespace ConanServerControl.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IUiDialogs _dialogs;
    private readonly IInstallDetector _detector;

    public SettingsViewModel(ISettingsService settings, IUiDialogs dialogs, IInstallDetector detector)
    {
        _settings = settings;
        _dialogs = dialogs;
        _detector = detector;
        LoadFromSettings();
    }

    [ObservableProperty] private string? serverExecutablePath;
    [ObservableProperty] private string? serverWorkingDirectory;
    [ObservableProperty] private string? serverInstallDirectory;
    [ObservableProperty] private string additionalArguments = "-log";
    [ObservableProperty] private string? steamCmdDirectory;
    [ObservableProperty] private string? standaloneClientRoot;
    [ObservableProperty] private string serverName = "Conan Dedicated Server";
    [ObservableProperty] private int maxPlayers = 10;
    [ObservableProperty] private int gamePort = 7777;
    [ObservableProperty] private int queryPort = 27015;
    [ObservableProperty] private int rconPort = 25575;
    [ObservableProperty] private string? rconPasswordInput;
    [ObservableProperty] private bool rconPasswordConfigured;
    [ObservableProperty] private bool webAdminEnabled;
    [ObservableProperty] private int webAdminPort = 8080;
    [ObservableProperty] private string webAdminUsername = "admin";
    [ObservableProperty] private string? webAdminPassword;
    [ObservableProperty] private bool startServerOnLaunch;
    [ObservableProperty] private bool restartAfterCrash = true;
    [ObservableProperty] private string bindMode = "LocalhostOnly";

    [RelayCommand]
    private void BrowseExecutable()
    {
        var path = _dialogs.PickFile("Conan Server|ConanSandboxServer*.exe|Executables|*.exe", "Choose Conan dedicated server executable");
        if (path is not null)
        {
            ServerExecutablePath = path;
            ServerWorkingDirectory ??= Path.GetDirectoryName(path);
            ServerInstallDirectory ??= FindInstallRoot(path);
        }
    }

    [RelayCommand]
    private void BrowseWorkingDirectory()
    {
        var path = _dialogs.PickFolder("Choose server working directory");
        if (path is not null)
        {
            ServerWorkingDirectory = path;
        }
    }

    [RelayCommand]
    private void BrowseInstallDirectory()
    {
        var path = _dialogs.PickFolder("Choose dedicated server install folder");
        if (path is not null)
        {
            ServerInstallDirectory = path;
        }
    }

    [RelayCommand]
    private void BrowseSteamCmd()
    {
        var path = _dialogs.PickFolder("Choose SteamCMD folder");
        if (path is not null)
        {
            SteamCmdDirectory = path;
        }
    }

    [RelayCommand]
    private void UseExistingServer()
    {
        var folder = _dialogs.PickFolder("Choose the existing Conan Dedicated Server folder (contains ConanSandboxServer.exe)");
        if (folder is null)
        {
            return;
        }

        var exe = DedicatedServerLocator.Find(folder);
        if (exe is null)
        {
            _dialogs.Alert(
                "Dedicated server not found",
                $"ConanSandboxServer.exe was not found in:{Environment.NewLine}{folder}{Environment.NewLine}{Environment.NewLine}Looked for:{Environment.NewLine}" +
                string.Join(Environment.NewLine, DedicatedServerLocator.CandidatePaths(folder)) +
                $"{Environment.NewLine}{Environment.NewLine}This must be the Dedicated Server, not the game client (ConanSandbox.exe / Run Me!.bat).");
            return;
        }

        var gate = ServerExecutableGate.Evaluate(exe, StandaloneClientRoot);
        if (!gate.Allowed)
        {
            _dialogs.Alert("This folder cannot be used as the dedicated server", $"{gate.Reason}{Environment.NewLine}{exe}");
            return;
        }

        ServerExecutablePath = exe;
        ServerInstallDirectory = folder;
        ServerWorkingDirectory = null;
        _dialogs.Alert("Existing dedicated server selected", $"{exe}{Environment.NewLine}{Environment.NewLine}Click Save settings to keep it. SteamCMD is not required for an existing installation.");
    }

    [RelayCommand]
    private void BrowseStandaloneClient()
    {
        var path = _dialogs.PickFolder("Choose the standalone Conan client folder (contains ConanSandbox.exe)");
        if (path is not null)
        {
            StandaloneClientRoot = path;
        }
    }

    [RelayCommand]
    private void Detect()
    {
        var found = _detector.Detect();
        if (found.DedicatedServerExecutable is not null)
        {
            ServerExecutablePath = found.DedicatedServerExecutable;
        }

        if (found.DedicatedServerDirectory is not null)
        {
            ServerInstallDirectory = found.DedicatedServerDirectory;
            ServerWorkingDirectory ??= found.DedicatedServerDirectory;
        }

        if (found.SteamCmdDirectory is not null)
        {
            SteamCmdDirectory = found.SteamCmdDirectory;
        }

        _dialogs.Alert("Detection",
            $"Steam: {found.SteamDirectory ?? "not found"}{Environment.NewLine}" +
            $"SteamCMD: {found.SteamCmdDirectory ?? "not found"}{Environment.NewLine}" +
            $"Dedicated server: {found.DedicatedServerExecutable ?? "not found"}{Environment.NewLine}" +
            $"Client (optional): {found.ClientDirectory ?? "not required"}");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _settings.UpdateAsync(s =>
        {
            s.ServerPaths.ServerExecutablePath = ServerExecutablePath;
            s.ServerPaths.ServerWorkingDirectory = ServerWorkingDirectory;
            s.ServerPaths.ServerInstallDirectory = ServerInstallDirectory;
            s.ServerPaths.AdditionalArguments = AdditionalArguments;
            s.SteamCmd.InstallDirectory = SteamCmdDirectory;
            s.Client.RootDirectory = string.IsNullOrWhiteSpace(StandaloneClientRoot) ? null : StandaloneClientRoot.Trim();
            s.Server.ServerName = ServerName;
            s.Server.MaxPlayers = MaxPlayers;
            s.Server.GamePort = GamePort;
            s.Server.QueryPort = QueryPort;
            s.Rcon.Port = RconPort;
            s.WebAdmin.Enabled = WebAdminEnabled;
            s.WebAdmin.Port = WebAdminPort;
            s.WebAdmin.Username = WebAdminUsername;
            s.WebAdmin.BindMode = BindMode switch
            {
                "LAN" => WebBindMode.Lan,
                "Custom" => WebBindMode.Custom,
                _ => WebBindMode.LocalhostOnly
            };
            s.General.StartServerWhenManagerLaunches = StartServerOnLaunch;
            s.Advanced.RestartAfterCrash = RestartAfterCrash;
            s.IsSetupComplete = !string.IsNullOrWhiteSpace(ServerExecutablePath);
        });

        if (!string.IsNullOrWhiteSpace(WebAdminPassword))
        {
            var hash = PasswordHasher.Hash(WebAdminPassword);
            await _settings.UpdateSecretsAsync(sec => sec.WebAdminPasswordHash = hash);
            WebAdminPassword = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(RconPasswordInput))
        {
            var password = RconPasswordInput;
            await _settings.UpdateSecretsAsync(sec => sec.RconPassword = password);
            RconPasswordInput = string.Empty;
            RconPasswordConfigured = true;
        }

        _dialogs.Alert("Settings saved", "Settings were written to the application data directory. If you enabled Web Admin, restart Conan Server Control so the HTTP listener binds the new port.");
    }

    public string RconPasswordStatus => RconPasswordConfigured ? "Password configured" : "Not configured";

    partial void OnRconPasswordConfiguredChanged(bool value) => OnPropertyChanged(nameof(RconPasswordStatus));

    [RelayCommand]
    private async Task ClearRconPasswordAsync()
    {
        if (!_dialogs.Confirm(
                "Clear RCON password?",
                "Remove the stored RCON password? Graceful stop and player broadcasts via RCON will be unavailable until a new password is set."))
        {
            return;
        }

        await _settings.UpdateSecretsAsync(sec => sec.RconPassword = null);
        RconPasswordInput = string.Empty;
        RconPasswordConfigured = false;
    }

    private void LoadFromSettings()
    {
        var s = _settings.Current;
        ServerExecutablePath = s.ServerPaths.ServerExecutablePath;
        ServerWorkingDirectory = s.ServerPaths.ServerWorkingDirectory;
        ServerInstallDirectory = s.ServerPaths.ServerInstallDirectory;
        AdditionalArguments = s.ServerPaths.AdditionalArguments;
        SteamCmdDirectory = s.SteamCmd.InstallDirectory;
        StandaloneClientRoot = s.Client.RootDirectory;
        ServerName = s.Server.ServerName;
        MaxPlayers = s.Server.MaxPlayers;
        GamePort = s.Server.GamePort;
        QueryPort = s.Server.QueryPort;
        RconPort = s.Rcon.Port;
        RconPasswordInput = null;
        RconPasswordConfigured = !string.IsNullOrEmpty(_settings.Secrets.RconPassword);
        WebAdminEnabled = s.WebAdmin.Enabled;
        WebAdminPort = s.WebAdmin.Port;
        WebAdminUsername = s.WebAdmin.Username;
        StartServerOnLaunch = s.General.StartServerWhenManagerLaunches;
        RestartAfterCrash = s.Advanced.RestartAfterCrash;
        BindMode = s.WebAdmin.BindMode.ToString();
    }

    private static string? FindInstallRoot(string executablePath)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(executablePath) ?? executablePath);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "ConanSandbox")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Path.GetDirectoryName(executablePath);
    }
}

public partial class LogsViewModel : ObservableObject
{
    public LogsViewModel(ILiveLogBuffer buffer)
    {
        Lines = new System.Collections.ObjectModel.ObservableCollection<string>(
            buffer.Snapshot(300).Select(Format));
        buffer.EntryAdded += (_, args) =>
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Lines.Add(Format(args.Entry));
                while (Lines.Count > 500)
                {
                    Lines.RemoveAt(0);
                }
            });
    }

    public System.Collections.ObjectModel.ObservableCollection<string> Lines { get; }

    private static string Format(LogEntry entry) =>
        $"{entry.Timestamp.ToLocalTime():HH:mm:ss} [{entry.Level}] {entry.Source}: {entry.Message}";
}

public partial class ModsViewModel : ObservableObject
{
    private readonly IWorkshopModService _mods;
    private readonly IModCatalogService _catalog;
    private readonly IClientModBundleService _bundles;
    private readonly IServerUpdateService _updates;
    private readonly IUiDialogs _dialogs;

    public ModsViewModel(
        IWorkshopModService mods,
        IModCatalogService catalog,
        IClientModBundleService bundles,
        IServerUpdateService updates,
        IUiDialogs dialogs)
    {
        _mods = mods;
        _catalog = catalog;
        _bundles = bundles;
        _updates = updates;
        _dialogs = dialogs;
        Reload();
    }

    [ObservableProperty] private string workshopId = string.Empty;
    [ObservableProperty] private string listing = string.Empty;
    [ObservableProperty] private string shareable = string.Empty;

    [RelayCommand]
    private async Task AddAsync()
    {
        if (!WorkshopIdValidator.TryParse(WorkshopId, out var id))
        {
            _dialogs.Alert("Invalid Workshop ID", WorkshopIdValidator.DescribeRule());
            return;
        }

        try
        {
            await _mods.AddAsync(id);
            WorkshopId = string.Empty;
            Reload();
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
            Reload();
        }
    }

    [RelayCommand]
    private async Task ImportLocalAsync()
    {
        var path = _dialogs.PickFile("Conan mod package (*.pak)|*.pak", "Choose a local mod .pak to import");
        if (path is null)
        {
            return;
        }

        try
        {
            await _updates.ImportLocalModAsync(path);
            Reload();
            _dialogs.Alert("Local mod imported",
                $"{System.IO.Path.GetFileName(path)} was copied, verified (SHA-256) and installed in the server Mods folder. Your original file was not changed.");
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
            Reload();
        }
    }

    [RelayCommand]
    private async Task ReplaceLocalAsync()
    {
        if (!TrySelectedKey(out var key) || !key.StartsWith(ModKeys.LocalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _dialogs.Alert("Select a local mod", "Type the local mod's .pak file name shown in the list, then click Replace local .pak.");
            return;
        }

        var path = _dialogs.PickFile("Conan mod package (*.pak)|*.pak", "Choose the newer .pak (same file name)");
        if (path is null)
        {
            return;
        }

        try
        {
            await _updates.ReplaceLocalModAsync(key, path);
            Reload();
            _dialogs.Alert("Local mod updated", "The newer file was verified and installed. Your original file was not changed.");
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
            Reload();
        }
    }

    [RelayCommand]
    private async Task ExportClientBundleAsync()
    {
        var folder = _dialogs.PickFolder("Choose where to create the client mod bundle (not inside a game client)");
        if (folder is null)
        {
            return;
        }

        try
        {
            var export = await _bundles.ExportAsync(folder);
            _dialogs.Alert("Client mod bundle exported",
                $"{export.Manifest.Mods.Count} mod(s) exported to:{Environment.NewLine}{export.BundleDirectory}{Environment.NewLine}{Environment.NewLine}" +
                "It contains only mod files, modlist.txt, manifest.json (SHA-256) and README.txt. Give it to players with standalone clients; nothing was copied into any client.");
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (!TrySelectedKey(out var key))
        {
            _dialogs.Alert("Select a mod", "Enter the Workshop ID or local .pak name to remove.");
            return;
        }

        if (!_dialogs.Confirm(
                "Remove mod?",
                "Removing a mod from an existing Conan world may permanently remove modded buildings, items, NPCs or other save data. A backup will be created first."))
        {
            return;
        }

        try
        {
            await _catalog.RemoveAsync(key, confirmed: true);
            Reload();
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    [RelayCommand]
    private void CopyList()
    {
        Shareable = _mods.GetShareableModList();
        System.Windows.Clipboard.SetText(Shareable);
        _dialogs.Alert("Mod list copied", "Players need a licensed Conan Exiles client. Standalone clients do not receive mods automatically; use Export client bundle.");
    }

    [RelayCommand]
    private async Task EnableAsync()
    {
        if (!TrySelectedKey(out var key))
        {
            return;
        }

        try
        {
            await _catalog.SetEnabledAsync(key, true);
            Reload();
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    [RelayCommand]
    private async Task DisableAsync()
    {
        if (!TrySelectedKey(out var key))
        {
            return;
        }

        try
        {
            await _catalog.SetEnabledAsync(key, false);
            Reload();
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    [RelayCommand]
    private async Task MoveUpAsync()
    {
        if (!TrySelectedKey(out var key))
        {
            return;
        }

        if (!_dialogs.Confirm(
                "Change load order?",
                "Changing mod load order can affect saves and mod compatibility. A backup is recommended before restarting the server."))
        {
            return;
        }

        try
        {
            var current = _mods.Mods.ToList();
            var index = current.FindIndex(m => ModKeys.Matches(m, key));
            if (index <= 0)
            {
                return;
            }

            await _catalog.MoveAsync(key, index - 1);
            Reload();
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    [RelayCommand]
    private async Task MoveDownAsync()
    {
        if (!TrySelectedKey(out var key))
        {
            return;
        }

        if (!_dialogs.Confirm(
                "Change load order?",
                "Changing mod load order can affect saves and mod compatibility. A backup is recommended before restarting the server."))
        {
            return;
        }

        try
        {
            var current = _mods.Mods.ToList();
            var index = current.FindIndex(m => ModKeys.Matches(m, key));
            if (index < 0 || index >= current.Count - 1)
            {
                return;
            }

            await _catalog.MoveAsync(key, index + 1);
            Reload();
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        try
        {
            await _mods.CheckForUpdatesAsync();
            Reload();
            var needing = _mods.Mods.Count(m => m.UpdateAvailable);
            _dialogs.Alert("Workshop check", needing == 0
                ? "No Workshop updates were flagged. The server was not restarted."
                : $"{needing} mod(s) have updates available. Use UPDATE ALL when you are ready. The server was not restarted.");
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
            Reload();
        }
    }

    [RelayCommand]
    private async Task UpdateSelectedAsync()
    {
        if (!TrySelectedId(out var id))
        {
            return;
        }

        try
        {
            await _updates.UpdateSelectedModsAsync(id);
            Reload();
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
            Reload();
        }
    }

    [RelayCommand]
    private async Task UpdateAllAsync()
    {
        try
        {
            await _updates.UpdateModsAsync(restartAfter: false);
            Reload();
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
            Reload();
        }
    }

    [RelayCommand]
    private void OpenWorkshop()
    {
        if (!TrySelectedId(out var id))
        {
            return;
        }

        var url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={id}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private bool TrySelectedId(out long id)
    {
        if (WorkshopIdValidator.TryParse(WorkshopId, out id))
        {
            return true;
        }

        _dialogs.Alert("Select a Workshop mod", "Enter the Workshop ID shown in the list first. Local mods are updated with Replace local .pak.");
        return false;
    }

    private bool TrySelectedKey(out string key)
    {
        if (ModKeys.TryParseUserInput(WorkshopId, out key))
        {
            return true;
        }

        _dialogs.Alert("Select a mod", "Enter the Workshop ID or the local .pak file name shown in the list first.");
        return false;
    }

    private void Reload()
    {
        Listing = string.Join(Environment.NewLine, _mods.Mods.Select(m =>
            m.SourceType == ModSourceType.Local
                ? $"{(m.Enabled ? "[x]" : "[ ]")} {m.Name}  LOCAL (manual update)  File: {m.LocalFileName}  Order: {m.LoadOrder}  SHA-256: {Short(m.Sha256)}  {m.Error}"
                : $"{(m.Enabled ? "[x]" : "[ ]")} {m.Name}  Workshop ID: {m.WorkshopId}  Order: {m.LoadOrder}  {(m.UpdateAvailable ? "Update available" : "Installed")}  {m.Error}"));
        if (string.IsNullOrWhiteSpace(Listing))
        {
            Listing = "No mods yet. Import a local .pak (no SteamCMD needed), or paste a Steam Workshop ID and click Add Workshop mod (needs the optional SteamCMD).";
        }

        Shareable = _mods.GetShareableModList();
    }

    private static string Short(string? hash) =>
        string.IsNullOrEmpty(hash) ? "-" : hash.Length > 12 ? hash[..12] + "..." : hash;
}

public partial class BackupsViewModel : ObservableObject
{
    private readonly IBackupService _backups;
    private readonly IUiDialogs _dialogs;

    public BackupsViewModel(IBackupService backups, IUiDialogs dialogs)
    {
        _backups = backups;
        _dialogs = dialogs;
        _ = RefreshAsync();
    }

    [ObservableProperty] private string listing = "Loading...";
    [ObservableProperty] private string backupId = string.Empty;

    [RelayCommand]
    private async Task BackupAsync()
    {
        try
        {
            var record = await _backups.BackupNowAsync("manual");
            _dialogs.Alert("Backup created", record.DirectoryPath);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (string.IsNullOrWhiteSpace(BackupId))
        {
            _dialogs.Alert("Restore", "Enter a backup id (folder name) first.");
            return;
        }

        if (!_dialogs.Confirm("Restore backup?", "The server must be offline. A safety backup of the current world is created first."))
        {
            return;
        }

        try
        {
            await _backups.RestoreAsync(BackupId.Trim(), startAfter: false);
            _dialogs.Alert("Restore", "Backup restored. Start the server from the Dashboard when you are ready.");
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var items = await _backups.ListAsync();
        Listing = items.Count == 0
            ? "No backups yet."
            : string.Join(Environment.NewLine, items.Select(b => $"{b.Id}  {b.Reason}  {b.CreatedAt:g}  {(b.SizeBytes / 1024d / 1024d):0.0} MB"));
    }
}

public partial class UpdatesViewModel : ObservableObject
{
    private readonly IServerUpdateService _updates;
    private readonly IDelayedRestartService _delayed;
    private readonly IUiDialogs _dialogs;

    public UpdatesViewModel(IServerUpdateService updates, IDelayedRestartService delayed, IUiDialogs dialogs)
    {
        _updates = updates;
        _delayed = delayed;
        _dialogs = dialogs;
    }

    [ObservableProperty] private string summary = "Click Check Updates. Latest Steam build comparison is partial in this build.";
    [ObservableProperty] private string countdown = "No delayed restart is scheduled.";

    [RelayCommand]
    private async Task CheckAsync()
    {
        try
        {
            var result = await _updates.CheckAsync();
            Summary = result.Summary;
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        try
        {
            await _updates.UpdateAsync(restartAfter: false);
            Summary = "Update pipeline finished.";
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    [RelayCommand]
    private async Task Delay10Async()
    {
        await _delayed.StartAsync(new DelayedRestartRequest { Delay = RestartDelay.Minutes10, BackupFirst = true });
        Countdown = "Restart scheduled in 10 minutes. Cancel if players need more time.";
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await _delayed.CancelAsync();
        Countdown = "Delayed restart cancelled.";
        _dialogs.Alert("Cancelled", "The restart countdown was cancelled.");
    }
}

public partial class PlayersViewModel : ObservableObject
{
    private readonly IRconService _rcon;

    public PlayersViewModel(IRconService rcon)
    {
        _rcon = rcon;
        Listing = "Player monitoring uses RCON. Set an RCON password in Settings (stored with DPAPI on Windows). This page will stay empty until the server is online.";
    }

    [ObservableProperty] private string listing = string.Empty;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var players = await _rcon.GetPlayersAsync();
            Listing = players.Count == 0
                ? "No players returned. The server may be empty, still starting, or RCON is not configured."
                : string.Join(Environment.NewLine, players.Select(p => p.Name));
        }
        catch (Exception ex)
        {
            Listing = ex.Message;
        }
    }
}

public partial class ServerViewModel : ObservableObject
{
    public ServerViewModel(ISettingsService settings)
    {
        Notes =
            "Server INI editing (Engine.ini / Game.ini / ServerSettings.ini) is not implemented yet." + Environment.NewLine +
            "Unknown Conan keys will not be overwritten when that editor lands." + Environment.NewLine + Environment.NewLine +
            $"Current name: {settings.Current.Server.ServerName}" + Environment.NewLine +
            $"Ports: game {settings.Current.Server.GamePort}, query {settings.Current.Server.QueryPort}, RCON {settings.Current.Rcon.Port}" + Environment.NewLine +
            "Use Settings for name, ports, executable, and working directory in this build.";
    }

    public string Notes { get; }
}
