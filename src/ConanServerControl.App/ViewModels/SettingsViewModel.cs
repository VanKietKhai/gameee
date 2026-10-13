using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConanServerControl.App.Services;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Notifications;
using ConanServerControl.Core.Security;
using ConanServerControl.Infrastructure.Notifications;

namespace ConanServerControl.App.ViewModels;

/// <summary>
/// Only the settings the operator changes day to day. Paths, ports, RCON, mods and backups are
/// handled by the operator's assistant, not through this window.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IUiDialogs _dialogs;
    private readonly IDiscordNotifier _discord;
    private readonly INetworkInfoService _network;

    public SettingsViewModel(ISettingsService settings, IUiDialogs dialogs, IDiscordNotifier discord, INetworkInfoService network)
    {
        _settings = settings;
        _dialogs = dialogs;
        _discord = discord;
        _network = network;
        LoadFromSettings();
    }

    [ObservableProperty] private bool startServerOnLaunch;
    [ObservableProperty] private bool startWithWindows;
    [ObservableProperty] private int scheduledRestartHours = 5;

    public int[] ScheduledRestartChoices { get; } = { 0, 3, 4, 5, 6, 8, 12 };
    [ObservableProperty] private bool restartAfterCrash = true;
    [ObservableProperty] private bool discordEnabled = true;
    [ObservableProperty] private bool useCloudflareWarp;
    [ObservableProperty] private string? discordWebhookInput;
    [ObservableProperty] private bool discordWebhookConfigured;
    [ObservableProperty] private bool notifyPlayerEvents = true;
    [ObservableProperty] private bool notifyUpdates = true;
    [ObservableProperty] private UpdateCheckInterval updateCheckInterval = UpdateCheckInterval.Hours2;
    [ObservableProperty] private AutomationMode updateAutomationMode = AutomationMode.Manual;
    [ObservableProperty] private bool remoteEnabled;
    [ObservableProperty] private string? remotePasswordInput;
    [ObservableProperty] private bool remotePasswordConfigured;
    [ObservableProperty] private string remoteAddress = string.Empty;

    public string RemotePasswordStatus => RemotePasswordConfigured ? "Đã đặt mật khẩu" : "Chưa đặt mật khẩu";

    partial void OnRemotePasswordConfiguredChanged(bool value) => OnPropertyChanged(nameof(RemotePasswordStatus));

    public UpdateCheckInterval[] UpdateCheckIntervals { get; } = Enum.GetValues<UpdateCheckInterval>();

    public AutomationMode[] AutomationModes { get; } = Enum.GetValues<AutomationMode>();

    public string DiscordWebhookStatus => DiscordWebhookConfigured ? "Đã lưu webhook" : "Chưa có webhook";

    partial void OnDiscordWebhookConfiguredChanged(bool value) => OnPropertyChanged(nameof(DiscordWebhookStatus));

    [RelayCommand]
    private async Task TestDiscordAsync()
    {
        if (!string.IsNullOrWhiteSpace(DiscordWebhookInput))
        {
            _dialogs.Alert("Discord", "Bấm Lưu cài đặt để lưu webhook mới trước khi gửi thử.");
            return;
        }

        var error = await _discord.SendTestAsync();
        _dialogs.Alert("Discord", error ?? "Đã gửi tin thử vào kênh Discord.");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _settings.UpdateAsync(s =>
        {
            s.General.StartServerWhenManagerLaunches = StartServerOnLaunch;
            s.General.StartManagerWithWindows = StartWithWindows;
            s.Advanced.RestartAfterCrash = RestartAfterCrash;
            s.Advanced.ScheduledRestartHours = ScheduledRestartHours;
            s.Notifications.DiscordEnabled = DiscordEnabled;
            s.Notifications.NotifyPlayerEvents = NotifyPlayerEvents;
            s.Notifications.NotifyUpdates = NotifyUpdates;
            s.Updates.CheckInterval = UpdateCheckInterval;
            s.Updates.AutomationMode = UpdateAutomationMode;
            s.SteamCmd.UseCloudflareWarp = UseCloudflareWarp;
            s.WebAdmin.Enabled = RemoteEnabled;
            if (RemoteEnabled)
            {
                // Phone access only through the private Tailscale network, never the LAN or internet.
                s.WebAdmin.BindMode = WebBindMode.Tailscale;
            }
        });

        var restartNeeded = false;
        if (!string.IsNullOrEmpty(RemotePasswordInput))
        {
            if (RemotePasswordInput.Length < 8)
            {
                _dialogs.Alert("Mật khẩu", "Mật khẩu điều khiển từ xa cần ít nhất 8 ký tự. Mật khẩu chưa được lưu.");
                return;
            }

            var hash = PasswordHasher.Hash(RemotePasswordInput);
            await _settings.UpdateSecretsAsync(sec => sec.WebAdminPasswordHash = hash);
            RemotePasswordInput = string.Empty;
            RemotePasswordConfigured = true;
            restartNeeded = true;
        }

        if (!string.IsNullOrWhiteSpace(DiscordWebhookInput))
        {
            var webhook = DiscordWebhookInput.Trim();
            if (DiscordWebhook.IsValidUrl(webhook))
            {
                await _settings.UpdateSecretsAsync(sec => sec.DiscordWebhookUrl = webhook);
                DiscordWebhookInput = string.Empty;
                DiscordWebhookConfigured = true;
            }
            else
            {
                _dialogs.Alert("Discord", "Webhook không hợp lệ (phải là https://discord.com/api/webhooks/...). Webhook cũ được giữ nguyên.");
                return;
            }
        }

        restartNeeded |= RemoteEnabled != _remoteEnabledAtLoad;
        _remoteEnabledAtLoad = RemoteEnabled;
        try
        {
            WindowsStartup.Apply(StartWithWindows);
        }
        catch (Exception ex)
        {
            _dialogs.Alert("Khởi động cùng Windows", $"Không ghi được mục khởi động: {ex.Message}");
        }

        _dialogs.Alert("Đã lưu", restartNeeded
            ? "Cài đặt đã được lưu. Tắt app rồi mở lại để áp dụng phần điều khiển từ xa."
            : "Cài đặt đã được lưu.");
    }

    private void LoadFromSettings()
    {
        var s = _settings.Current;
        StartServerOnLaunch = s.General.StartServerWhenManagerLaunches;
        StartWithWindows = WindowsStartup.IsEnabled();
        RestartAfterCrash = s.Advanced.RestartAfterCrash;
        ScheduledRestartHours = s.Advanced.ScheduledRestartHours;
        DiscordEnabled = s.Notifications.DiscordEnabled;
        DiscordWebhookInput = null;
        DiscordWebhookConfigured = !string.IsNullOrEmpty(_settings.Secrets.DiscordWebhookUrl);
        NotifyPlayerEvents = s.Notifications.NotifyPlayerEvents;
        NotifyUpdates = s.Notifications.NotifyUpdates;
        UpdateCheckInterval = s.Updates.CheckInterval;
        UpdateAutomationMode = s.Updates.AutomationMode;
        UseCloudflareWarp = s.SteamCmd.UseCloudflareWarp;
        RemoteEnabled = s.WebAdmin.Enabled;
        _remoteEnabledAtLoad = RemoteEnabled;
        RemotePasswordInput = null;
        RemotePasswordConfigured = !string.IsNullOrEmpty(_settings.Secrets.WebAdminPasswordHash);
        var tailscale = _network.GetTailscaleIPv4();
        RemoteAddress = tailscale is null
            ? "Chưa thấy Tailscale trên máy này. Cài Tailscale và đăng nhập, rồi mở lại app."
            : $"Trên điện thoại (bật Tailscale) mở: http://{tailscale}:{s.WebAdmin.Port}   - tên đăng nhập: {s.WebAdmin.Username}";
    }

    private bool _remoteEnabledAtLoad;
}

/// <summary>
/// "Open with Windows": a per-user Run entry (HKCU, no admin rights) that starts this exe at
/// sign-in. Only one copy runs anyway (single-instance guard in App).
/// </summary>
internal static class WindowsStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ConanServerControl";

    public static bool IsEnabled()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Apply(bool enabled)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        }
        else if (key.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName);
        }
    }
}
