using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConanServerControl.App.Views;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Exceptions;
using ConanServerControl.Core.Models;
using ConanServerControl.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ConanServerControl.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly IServerProcessManager _server;

    public MainViewModel(
        IServiceProvider services,
        IServerProcessManager server,
        DashboardView dashboard,
        ISettingsService settings)
    {
        _services = services;
        _server = server;
        CurrentView = dashboard;
        CurrentPage = "Dashboard";
        Status = _server.State.Status;
        _server.StateChanged += (_, state) =>
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Status = state.Status;
                BusyText = state.ActionInProgress ? state.CurrentAction : null;
            }, DispatcherPriority.Background);

        if (!settings.Current.IsSetupComplete)
        {
            Banner = "Chưa cấu hình đường dẫn server. Nhờ trợ lý thiết lập giúp.";
        }
    }

    [ObservableProperty]
    private object? currentView;

    [ObservableProperty]
    private string currentPage = "Dashboard";

    [ObservableProperty]
    private ServerStatus status;

    [ObservableProperty]
    private string? banner;

    [ObservableProperty]
    private string? busyText;

    public string StatusText => Status.ToString().ToUpperInvariant();

    partial void OnStatusChanged(ServerStatus value) => OnPropertyChanged(nameof(StatusText));

    [RelayCommand]
    private void Navigate(string page)
    {
        CurrentPage = page;
        CurrentView = page switch
        {
            "Dashboard" => _services.GetRequiredService<DashboardView>(),
            "Settings" => _services.GetRequiredService<SettingsView>(),
            _ => CurrentView
        };
    }

    public static void ShowError(Exception ex)
    {
        if (ex is UserFacingException ufe)
        {
            MessageBox.Show(ufe.FormatForDisplay(), ufe.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(ex.Message, "Conan Server Control", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
