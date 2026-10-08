using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;
using ConanServerControl.Core.Settings;

namespace ConanServerControl.Infrastructure.Diagnostics;

public sealed class DiagnosticsService
{
    private readonly IAppPaths _paths;
    private readonly ISettingsService _settings;
    private readonly ISteamCmdService _steamCmd;
    private readonly IServerProcessManager _server;
    private readonly INetworkInfoService _network;

    public DiagnosticsService(
        IAppPaths paths,
        ISettingsService settings,
        ISteamCmdService steamCmd,
        IServerProcessManager server,
        INetworkInfoService network)
    {
        _paths = paths;
        _settings = settings;
        _steamCmd = steamCmd;
        _server = server;
        _network = network;
    }

    public DiagnosticsSnapshot Capture()
    {
        var exe = _settings.Current.ServerPaths.ServerExecutablePath ?? string.Empty;
        var cwd = _settings.Current.ServerPaths.ServerWorkingDirectory ?? string.Empty;
        var steam = _steamCmd.ExecutablePath ?? string.Empty;
        var bind = ResolveBindAddress(_settings.Current.WebAdmin);
        var url = _network.GetWebAdminUrl(bind, _settings.Current.WebAdmin.Port);

        return new DiagnosticsSnapshot
        {
            ApplicationDataDirectory = _paths.DataDirectory,
            LogsDirectory = _paths.LogsDirectory,
            BackupsDirectory = _paths.BackupsDirectory,
            SettingsFilePath = _paths.SettingsFilePath,
            SteamCmdPath = steam,
            SteamCmdExists = _steamCmd.IsInstalled,
            ConanServerExecutablePath = exe,
            ConanServerExecutableExists = !string.IsNullOrWhiteSpace(exe) && File.Exists(exe),
            ConanServerWorkingDirectory = cwd,
            ConanServerWorkingDirectoryExists = !string.IsNullOrWhiteSpace(cwd) && Directory.Exists(cwd),
            ServerStatus = _server.State.Status,
            ProcessId = _server.State.ProcessId,
            OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            TailscaleIPv4 = _network.GetTailscaleIPv4(),
            WebAdminUrl = url
        };
    }

    public static string ResolveBindAddress(WebAdminSettings web)
    {
        return web.BindMode switch
        {
            Core.Models.WebBindMode.Lan => "0.0.0.0",
            Core.Models.WebBindMode.Custom when !string.IsNullOrWhiteSpace(web.CustomBindAddress) => web.CustomBindAddress,
            Core.Models.WebBindMode.Tailscale => Health.NetworkInfoService.FindTailscaleIPv4() ?? "127.0.0.1",
            _ => "127.0.0.1"
        };
    }
}
