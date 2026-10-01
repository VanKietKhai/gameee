using ConanServerControl.Core.Abstractions;
using Serilog;
using Serilog.Events;

namespace ConanServerControl.Infrastructure.Logging;

public static class LoggingSetup
{
    public static LoggerConfiguration CreateLoggerConfiguration(IAppPaths paths, ILiveLogBuffer liveLog)
    {
        paths.EnsureCreated();
        var appLog = Path.Combine(paths.LogsDirectory, "app-.log");
        var serverLog = Path.Combine(paths.LogsDirectory, "server-.log");
        var steamLog = Path.Combine(paths.LogsDirectory, "steamcmd-.log");

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Sink(new LiveLogSink(liveLog))
            .WriteTo.Console()
            .WriteTo.File(
                appLog,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e =>
                    e.Properties.TryGetValue("SourceContext", out var ctx) &&
                    ctx.ToString().Contains("ServerProcess", StringComparison.Ordinal))
                .WriteTo.File(
                    serverLog,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14))
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e =>
                    e.Properties.TryGetValue("SourceContext", out var ctx) &&
                    ctx.ToString().Contains("SteamCmd", StringComparison.Ordinal))
                .WriteTo.File(
                    steamLog,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14));
    }
}
