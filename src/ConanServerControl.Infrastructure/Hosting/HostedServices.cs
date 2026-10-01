using ConanServerControl.Core.Abstractions;
using ConanServerControl.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Hosting;

public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IAppPaths _paths;
    private readonly ISettingsService _settings;
    private readonly ILogger<DatabaseInitializerHostedService> _logger;

    public DatabaseInitializerHostedService(
        IDbContextFactory<AppDbContext> factory,
        IAppPaths paths,
        ISettingsService settings,
        ILogger<DatabaseInitializerHostedService> logger)
    {
        _factory = factory;
        _paths = paths;
        _settings = settings;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _paths.EnsureCreated();
        await _settings.LoadAsync(cancellationToken).ConfigureAwait(false);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Application data directory: {Dir}", _paths.DataDirectory);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ServerMonitorHostedService : BackgroundService
{
    private readonly IServerProcessManager _server;
    private readonly IServerHealthService _health;
    private readonly ISettingsService _settings;
    private readonly ILogger<ServerMonitorHostedService> _logger;

    public ServerMonitorHostedService(
        IServerProcessManager server,
        IServerHealthService health,
        ISettingsService settings,
        ILogger<ServerMonitorHostedService> logger)
    {
        _server = server;
        _health = health;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.Current.General.StartServerWhenManagerLaunches)
        {
            try
            {
                await _server.StartAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Automatic start on launch failed.");
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _health.CheckAsync(stoppingToken).ConfigureAwait(false);
                _server.State.Health = result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Health check failed.");
            }

            var seconds = Math.Clamp(_settings.Current.Advanced.HealthCheckIntervalSeconds, 3, 120);
            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken).ConfigureAwait(false);
        }
    }
}
