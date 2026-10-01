using ConanServerControl.Core.Diagnostics;

namespace ConanServerControl.Core.Abstractions;

/// <summary>
/// Read-only integration diagnostics for SteamCMD, the dedicated server, the optional
/// standalone client, world/mod files, network/RCON settings and backups.
/// <para>
/// <see cref="RunAsync"/> never starts, stops, installs or downloads anything, never takes
/// <see cref="IServerActionGate"/>, never opens the live world database, and never writes.
/// Only <see cref="ExportAsync"/> writes, and only under <see cref="ReportDirectory"/>.
/// </para>
/// </summary>
public interface IIntegrationDiagnosticsService
{
    /// <summary>Directory under the application data folder that receives exported reports.</summary>
    string ReportDirectory { get; }

    Task<IntegrationDiagnosticsReport> RunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a redacted JSON and Markdown copy of <paramref name="report"/> under
    /// <see cref="ReportDirectory"/>. Never deletes existing reports.
    /// </summary>
    Task<DiagnosticReportExport> ExportAsync(
        IntegrationDiagnosticsReport report,
        CancellationToken cancellationToken = default);
}

public sealed record DiagnosticReportExport(string JsonPath, string TextPath);
