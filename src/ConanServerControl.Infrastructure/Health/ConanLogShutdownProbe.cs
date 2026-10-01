using System.Text;
using ConanServerControl.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace ConanServerControl.Infrastructure.Health;

/// <summary>
/// Reads the dedicated server's ConanSandbox.log for lines that only appear once a requested exit
/// is under way. Live-observed on CL-377096 after RCON <c>shutdown</c>: "Engine exit requested" and
/// "PreExit Game." within milliseconds, then a quiet teardown of up to minutes, then
/// "LogExit: Preparing to exit." / "Game engine shut down" / "Exiting.".
/// </summary>
public sealed class ConanLogShutdownProbe : IServerShutdownProbe
{
    private const int MaxScanBytes = 4 * 1024 * 1024;

    private static readonly string[] Markers =
    [
        "Engine exit requested",
        "PreExit Game",
        "LogExit: Preparing to exit",
        "LogExit: Game engine shut down",
        "LogExit: Exiting"
    ];

    private readonly ISettingsService _settings;
    private readonly ILogger<ConanLogShutdownProbe> _logger;

    public ConanLogShutdownProbe(ISettingsService settings, ILogger<ConanLogShutdownProbe> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public long Mark()
    {
        var path = LogPath();
        try
        {
            return path is not null && File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    public string? FindShutdownEvidence(long mark)
    {
        var path = LogPath();
        if (path is null)
        {
            return null;
        }

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var start = mark > stream.Length ? 0 : mark; // a shorter file means the log was rotated
            var length = (int)Math.Min(stream.Length - start, MaxScanBytes);
            if (length <= 0)
            {
                return null;
            }

            stream.Seek(start, SeekOrigin.Begin);
            var bytes = new byte[length];
            stream.ReadExactly(bytes);
            foreach (var line in Encoding.UTF8.GetString(bytes).Split('\n'))
            {
                if (Markers.Any(marker => line.Contains(marker, StringComparison.Ordinal)))
                {
                    var trimmed = line.Trim();
                    return trimmed.Length > 300 ? trimmed[..300] : trimmed;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read the server log for shutdown progress.");
        }

        return null;
    }

    internal static string? LogPathFor(Core.Settings.ServerPathSettings paths)
    {
        var install = paths.ServerInstallDirectory ?? paths.ServerWorkingDirectory;
        if (string.IsNullOrWhiteSpace(install) && !string.IsNullOrWhiteSpace(paths.ServerExecutablePath))
        {
            install = Path.GetDirectoryName(paths.ServerExecutablePath);
        }

        return string.IsNullOrWhiteSpace(install)
            ? null
            : Path.Combine(install, "ConanSandbox", "Saved", "Logs", "ConanSandbox.log");
    }

    private string? LogPath() => LogPathFor(_settings.Current.ServerPaths);
}
