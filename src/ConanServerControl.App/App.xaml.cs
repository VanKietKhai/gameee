using System.Windows;
using ConanServerControl.App.Services;
using ConanServerControl.App.ViewModels;
using ConanServerControl.App.Views;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Infrastructure;
using ConanServerControl.Infrastructure.Logging;
using ConanServerControl.Infrastructure.Notifications;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.Updates;
using ConanServerControl.Web.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ConanServerControl.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            MessageBox.Show(args.Exception.Message, "Conan Server Control", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var paths = new AppPaths();
        paths.EnsureCreated();
        var liveLog = new LiveLogBuffer();

        _host = Host.CreateDefaultBuilder(e.Args)
            .UseConanLogging(paths, liveLog)
            .ConfigureServices(services =>
            {
                services.AddSingleton<ILiveLogBuffer>(liveLog);
                services.AddConanServerControl(paths);
                services.AddHostedService<WebAdminEmbeddedHost>();
                // Notifiers and the update watcher run only in the desktop app, never twice.
                services.AddSingleton<DiscordWebhookNotifier>();
                services.AddSingleton<IDiscordNotifier>(sp => sp.GetRequiredService<DiscordWebhookNotifier>());
                services.AddHostedService(sp => sp.GetRequiredService<DiscordWebhookNotifier>());
                services.AddHostedService<PlayerActivityService>();
                services.AddHostedService<UpdateWatchService>();
                services.AddSingleton<IUiDialogs, WpfDialogService>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<ServerViewModel>();
                services.AddSingleton<ModsViewModel>();
                services.AddSingleton<PlayersViewModel>();
                services.AddSingleton<BackupsViewModel>();
                services.AddSingleton<UpdatesViewModel>();
                services.AddSingleton<LogsViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<DiagnosticsViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<DashboardView>();
                services.AddSingleton<ServerView>();
                services.AddSingleton<ModsView>();
                services.AddSingleton<PlayersView>();
                services.AddSingleton<BackupsView>();
                services.AddSingleton<UpdatesView>();
                services.AddSingleton<LogsView>();
                services.AddSingleton<SettingsView>();
                services.AddSingleton<DiagnosticsView>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();
        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }
}
