using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConanServerControl.App.Services;
using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Diagnostics;
using ConanServerControl.Infrastructure.Diagnostics;

namespace ConanServerControl.App.ViewModels;

/// <summary>
/// Thin presentation wrapper over <see cref="IIntegrationDiagnosticsService"/>.
/// All diagnostic logic lives in the service; this only maps results for display.
/// </summary>
public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly IIntegrationDiagnosticsService _integration;
    private readonly DiagnosticsService _diagnostics;
    private readonly ISteamCmdService _steamCmd;
    private readonly IUiDialogs _dialogs;
    private IntegrationDiagnosticsReport? _lastReport;

    public DiagnosticsViewModel(
        IIntegrationDiagnosticsService integration,
        DiagnosticsService diagnostics,
        ISteamCmdService steamCmd,
        IUiDialogs dialogs)
    {
        _integration = integration;
        _diagnostics = diagnostics;
        _steamCmd = steamCmd;
        _dialogs = dialogs;
        RunCommand.Execute(null);
    }

    public ObservableCollection<DiagnosticCategoryViewModel> Categories { get; } = new();

    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private string lastRunText = "Not run yet";
    [ObservableProperty] private string environmentSummary = string.Empty;
    [ObservableProperty] private LiveReadinessViewModel? serverReadiness;
    [ObservableProperty] private LiveReadinessViewModel? clientReadiness;
    [ObservableProperty] private string? lastExportPath;

    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        try
        {
            RefreshEnvironment();
            var report = await _integration.RunAsync();
            _lastReport = report;
            Categories.Clear();
            foreach (var category in DiagnosticReportFormatter.OrderedCategories(report.Checks))
            {
                Categories.Add(new DiagnosticCategoryViewModel(
                    category,
                    report.Checks.Where(c => c.Category == category).Select(c => new DiagnosticRowViewModel(c)).ToArray()));
            }

            ServerReadiness = new LiveReadinessViewModel(report.ServerLiveTest);
            ClientReadiness = new LiveReadinessViewModel(report.ClientCompatibilityTest);
            LastRunText = $"Last run {report.CreatedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss} - read-only configuration checks, nothing live verified";
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        try
        {
            if (_lastReport is null)
            {
                await RunAsync();
            }

            if (_lastReport is null)
            {
                return;
            }

            var export = await _integration.ExportAsync(_lastReport);
            LastExportPath = export.JsonPath;
            _dialogs.Alert(
                "Diagnostics report exported",
                $"Secrets were redacted. Paths are included for troubleshooting.{Environment.NewLine}{Environment.NewLine}{export.JsonPath}{Environment.NewLine}{export.TextPath}");
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    [RelayCommand]
    private void OpenReportFolder()
    {
        var folder = _integration.ReportDirectory;
        if (!Directory.Exists(folder))
        {
            _dialogs.Alert("No reports yet", "Export a diagnostics report first.");
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }

    [RelayCommand]
    private async Task InstallSteamCmdAsync()
    {
        try
        {
            await _steamCmd.InstallAsync();
            _dialogs.Alert("SteamCMD", "SteamCMD installed.");
            await RunAsync();
        }
        catch (Exception ex)
        {
            MainViewModel.ShowError(ex);
        }
    }

    private void RefreshEnvironment()
    {
        var snap = _diagnostics.Capture();
        EnvironmentSummary =
            $"Application data: {snap.ApplicationDataDirectory}{Environment.NewLine}" +
            $"Logs: {snap.LogsDirectory}{Environment.NewLine}" +
            $"Backups: {snap.BackupsDirectory}{Environment.NewLine}" +
            $"Settings: {snap.SettingsFilePath}{Environment.NewLine}" +
            $"Server process: {snap.ServerStatus}  PID: {snap.ProcessId?.ToString() ?? "-"}{Environment.NewLine}" +
            $"Web Admin URL: {snap.WebAdminUrl}{Environment.NewLine}" +
            $"Tailscale IPv4: {snap.TailscaleIPv4 ?? "not detected"}{Environment.NewLine}" +
            $"OS: {snap.OperatingSystem}{Environment.NewLine}" +
            $"Runtime: {snap.Runtime}";
    }
}

public sealed class DiagnosticCategoryViewModel
{
    public DiagnosticCategoryViewModel(string name, IReadOnlyList<DiagnosticRowViewModel> rows)
    {
        Name = name;
        Rows = rows;
    }

    public string Name { get; }

    public IReadOnlyList<DiagnosticRowViewModel> Rows { get; }
}

public sealed class DiagnosticRowViewModel
{
    public DiagnosticRowViewModel(DiagnosticCheckResult check)
    {
        Name = check.Name;
        Status = check.Status.ToString();
        StatusLabel = DiagnosticReportFormatter.StatusLabel(check.Status);
        EvidenceLabel = DiagnosticReportFormatter.EvidenceLabel(check.Evidence);
        Summary = check.Summary;
        Details = check.Details;
        SuggestedAction = check.SuggestedAction;
    }

    public string Name { get; }

    /// <summary>Enum name; drives the badge colour in XAML.</summary>
    public string Status { get; }

    public string StatusLabel { get; }

    public string EvidenceLabel { get; }

    public string Summary { get; }

    public string? Details { get; }

    public string? SuggestedAction { get; }

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    public bool HasSuggestedAction => !string.IsNullOrWhiteSpace(SuggestedAction);
}

public sealed class LiveReadinessViewModel
{
    public LiveReadinessViewModel(LiveTestReadiness readiness)
    {
        Headline = readiness.Headline;
        IsReady = readiness.IsReady;
        Blockers = readiness.Blockers;
        Notes = readiness.Notes;
    }

    public string Headline { get; }

    public bool IsReady { get; }

    public IReadOnlyList<string> Blockers { get; }

    public IReadOnlyList<string> Notes { get; }
}
