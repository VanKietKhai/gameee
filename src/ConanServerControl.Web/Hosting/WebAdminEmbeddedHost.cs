using ConanServerControl.Core.Abstractions;
using ConanServerControl.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Web.Hosting;

/// <summary>
/// Starts Kestrel in-process using the same Core/Infrastructure service instances as the desktop app.
/// </summary>
public sealed class WebAdminEmbeddedHost : IHostedService
{
    private readonly IServiceProvider _root;
    private readonly ISettingsService _settings;
    private readonly ILogger<WebAdminEmbeddedHost> _logger;
    private WebApplication? _web;

    public WebAdminEmbeddedHost(IServiceProvider root, ISettingsService settings, ILogger<WebAdminEmbeddedHost> logger)
    {
        _root = root;
        _settings = settings;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _settings.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!_settings.Current.WebAdmin.Enabled)
        {
            _logger.LogInformation("Web Admin is disabled. Kestrel will not listen.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.Secrets.WebAdminPasswordHash))
        {
            _logger.LogWarning("Web Admin is enabled but no password hash is stored. HTTP listener was not started. Set a Web Admin password in Settings.");
            return;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>(),
            ContentRootPath = AppContext.BaseDirectory
        });

        Forward<IServerProcessManager>(builder.Services);
        Forward<ISettingsService>(builder.Services);
        Forward<IBackupService>(builder.Services);
        Forward<IActivityLog>(builder.Services);
        Forward<ILiveLogBuffer>(builder.Services);
        Forward<IRconService>(builder.Services);
        Forward<IServerUpdateService>(builder.Services);
        Forward<IDelayedRestartService>(builder.Services);
        Forward<INetworkInfoService>(builder.Services);
        Forward<ConanServerControl.Core.Notifications.IAdminAnnouncer>(builder.Services);
        Forward<IWorkshopModService>(builder.Services);
        Forward<DiagnosticsService>(builder.Services);
        builder.Services.AddConanWebAdmin();

        var bind = DiagnosticsService.ResolveBindAddress(_settings.Current.WebAdmin);
        var url = $"http://{bind}:{_settings.Current.WebAdmin.Port}";
        builder.WebHost.UseUrls(url);

        _web = builder.Build();
        _web.MapConanWebAdmin();
        await _web.StartAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Web Admin listening at {Url}", url);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_web is not null)
        {
            await _web.StopAsync(cancellationToken).ConfigureAwait(false);
            await _web.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void Forward<T>(IServiceCollection services) where T : class
    {
        services.AddSingleton(_root.GetRequiredService<T>());
    }
}
