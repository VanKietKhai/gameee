using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConanServerControl.App.Services;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Notifications;
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

    public SettingsViewModel(ISettingsService settings, IUiDialogs dialogs, IDiscordNotifier discord)
    {
        _settings = settings;
        _dialogs = dialogs;
        _discord = discord;
        LoadFromSettings();
    }

    [ObservableProperty] private bool startServerOnLaunch;
    [ObservableProperty] private bool restartAfterCrash = true;
    [ObservableProperty] private bool discordEnabled = true;
    [ObservableProperty] private bool useCloudflareWarp;
    [ObservableProperty] private string? discordWebhookInput;
    [ObservableProperty] private bool discordWebhookConfigured;
    [ObservableProperty] private bool notifyPlayerEvents = true;
    [ObservableProperty] private bool notifyUpdates = true;
    [ObservableProperty] private UpdateCheckInterval updateCheckInterval = UpdateCheckInterval.Hours2;
    [ObservableProperty] private AutomationMode updateAutomationMode = AutomationMode.Manual;

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
            s.Advanced.RestartAfterCrash = RestartAfterCrash;
            s.Notifications.DiscordEnabled = DiscordEnabled;
            s.Notifications.NotifyPlayerEvents = NotifyPlayerEvents;
            s.Notifications.NotifyUpdates = NotifyUpdates;
            s.Updates.CheckInterval = UpdateCheckInterval;
            s.Updates.AutomationMode = UpdateAutomationMode;
            s.SteamCmd.UseCloudflareWarp = UseCloudflareWarp;
        });

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

        _dialogs.Alert("Đã lưu", "Cài đặt đã được lưu.");
    }

    private void LoadFromSettings()
    {
        var s = _settings.Current;
        StartServerOnLaunch = s.General.StartServerWhenManagerLaunches;
        RestartAfterCrash = s.Advanced.RestartAfterCrash;
        DiscordEnabled = s.Notifications.DiscordEnabled;
        DiscordWebhookInput = null;
        DiscordWebhookConfigured = !string.IsNullOrEmpty(_settings.Secrets.DiscordWebhookUrl);
        NotifyPlayerEvents = s.Notifications.NotifyPlayerEvents;
        NotifyUpdates = s.Notifications.NotifyUpdates;
        UpdateCheckInterval = s.Updates.CheckInterval;
        UpdateAutomationMode = s.Updates.AutomationMode;
        UseCloudflareWarp = s.SteamCmd.UseCloudflareWarp;
    }
}
