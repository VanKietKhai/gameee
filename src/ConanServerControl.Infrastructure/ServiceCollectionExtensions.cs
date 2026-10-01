using ConanServerControl.Core.Abstractions;
using ConanServerControl.Infrastructure.Backups;
using ConanServerControl.Infrastructure.Concurrency;
using ConanServerControl.Infrastructure.Data;
using ConanServerControl.Infrastructure.Detection;
using ConanServerControl.Infrastructure.Diagnostics;
using ConanServerControl.Infrastructure.Health;
using ConanServerControl.Infrastructure.Hosting;
using ConanServerControl.Infrastructure.Logging;
using ConanServerControl.Infrastructure.Paths;
using ConanServerControl.Infrastructure.ProcessManagement;
using ConanServerControl.Infrastructure.Rcon;
using ConanServerControl.Infrastructure.Security;
using ConanServerControl.Infrastructure.Settings;
using ConanServerControl.Infrastructure.Steam;
using ConanServerControl.Infrastructure.Updates;
using ConanServerControl.Infrastructure.Workshop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ConanServerControl.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConanServerControl(this IServiceCollection services, IAppPaths? paths = null)
    {
        var appPaths = paths ?? new AppPaths();
        appPaths.EnsureCreated();
        services.AddSingleton<IAppPaths>(appPaths);
        services.TryAddSingleton<ILiveLogBuffer, LiveLogBuffer>();
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IServerActionGate, ServerActionGate>();
        services.AddSingleton<IProcessStarter, SystemProcessStarter>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IServerProcessManager, ServerProcessManager>();
        services.AddSingleton<ISteamCmdService, SteamCmdService>();
        services.AddSingleton<ISteamWorkshopClient, SteamWorkshopClient>();
        services.AddSingleton<IRconService, RconService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IWorkshopModService, WorkshopModService>();
        services.AddSingleton<IServerUpdateService, ServerUpdateService>();
        services.AddSingleton<IDelayedRestartService, DelayedRestartService>();
        services.AddSingleton<IServerHealthService, ServerHealthService>();
        services.AddSingleton<IInstallDetector, InstallDetector>();
        services.AddSingleton<INetworkInfoService, NetworkInfoService>();
        services.AddSingleton<DiagnosticsService>();
        services.AddSingleton<IActivityLog, ActivityLogService>();

        services.AddHttpClient(SteamCmdService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ConanServerControl/0.1");
        });
        services.AddHttpClient(SteamWorkshopClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ConanServerControl/0.1");
        });

        services.AddDbContextFactory<AppDbContext>(options =>
        {
            options.UseSqlite($"Data Source={appPaths.DatabaseFilePath}");
        });

        services.AddHostedService<DatabaseInitializerHostedService>();
        services.AddHostedService<ServerMonitorHostedService>();
        return services;
    }

    public static IHostBuilder UseConanLogging(this IHostBuilder host, IAppPaths paths, ILiveLogBuffer liveLog)
    {
        Log.Logger = LoggingSetup.CreateLoggerConfiguration(paths, liveLog).CreateLogger();
        return host.UseSerilog();
    }
}
