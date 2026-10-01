using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConanServerControl.Core.Diagnostics;

/// <summary>
/// Serializes an integration report to JSON or Markdown text. Callers must pass a
/// redactor; the output is redacted both structurally and as a final text pass.
/// </summary>
public static class DiagnosticReportFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ToJson(IntegrationDiagnosticsReport report, DiagnosticReportRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(redactor);
        var json = JsonSerializer.Serialize(redactor.Redact(report), JsonOptions);
        return redactor.Redact(json)!;
    }

    public static string ToText(IntegrationDiagnosticsReport report, DiagnosticReportRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(redactor);
        var safe = redactor.Redact(report);
        var sb = new StringBuilder();

        sb.AppendLine("# Conan Server Control - Integration Diagnostics");
        sb.AppendLine();
        sb.AppendLine($"- Created: {safe.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"- App version: {safe.AppVersion}");
        sb.AppendLine($"- OS: {safe.OsDescription}");
        sb.AppendLine($"- Server install directory: {safe.ServerInstallDirectory ?? "(not configured)"}");
        sb.AppendLine($"- Standalone client root: {safe.StandaloneClientRoot ?? "(not configured)"}");
        sb.AppendLine();
        sb.AppendLine("> Configuration checks only. Nothing in this report was live verified:");
        sb.AppendLine("> SteamCMD download, server boot, Workshop download, mod load and player join were not exercised.");
        sb.AppendLine();

        AppendReadiness(sb, safe.ServerLiveTest);
        AppendReadiness(sb, safe.ClientCompatibilityTest);

        foreach (var category in OrderedCategories(safe.Checks))
        {
            sb.AppendLine($"## {category}");
            sb.AppendLine();
            foreach (var check in safe.Checks.Where(c => c.Category == category))
            {
                sb.AppendLine($"### [{StatusLabel(check.Status)}] {check.Name}");
                sb.AppendLine();
                sb.AppendLine($"- Id: `{check.Id}`");
                sb.AppendLine($"- Evidence: {EvidenceLabel(check.Evidence)}");
                sb.AppendLine($"- Summary: {check.Summary}");
                if (!string.IsNullOrWhiteSpace(check.Details))
                {
                    sb.AppendLine($"- Details: {check.Details.Replace(Environment.NewLine, " / ", StringComparison.Ordinal)}");
                }

                if (!string.IsNullOrWhiteSpace(check.SuggestedAction))
                {
                    sb.AppendLine($"- Suggested action: {check.SuggestedAction}");
                }

                foreach (var fact in check.Facts)
                {
                    sb.AppendLine($"  - {fact.Key}: {fact.Value}");
                }

                sb.AppendLine();
            }
        }

        return redactor.Redact(sb.ToString())!;
    }

    public static string StatusLabel(DiagnosticStatus status) => status switch
    {
        DiagnosticStatus.Pass => "PASS",
        DiagnosticStatus.Warning => "WARNING",
        DiagnosticStatus.Fail => "FAIL",
        DiagnosticStatus.NotConfigured => "NOT CONFIGURED",
        DiagnosticStatus.NotTested => "NOT TESTED",
        _ => status.ToString().ToUpperInvariant()
    };

    public static string EvidenceLabel(DiagnosticEvidence evidence) => evidence switch
    {
        DiagnosticEvidence.ConfigurationChecked => "CONFIG CHECKED",
        DiagnosticEvidence.FilesystemInspected => "FILES INSPECTED",
        DiagnosticEvidence.RecordedResult => "FROM RECORD",
        DiagnosticEvidence.RuntimeObserved => "RUNTIME OBSERVED",
        DiagnosticEvidence.NotExercised => "NOT EXERCISED",
        DiagnosticEvidence.LiveVerified => "LIVE VERIFIED",
        _ => evidence.ToString().ToUpperInvariant()
    };

    public static IEnumerable<string> OrderedCategories(IEnumerable<DiagnosticCheckResult> checks)
    {
        var present = checks.Select(c => c.Category).Distinct().ToList();
        foreach (var known in DiagnosticCategories.DisplayOrder.Where(present.Contains))
        {
            yield return known;
        }

        foreach (var other in present.Where(p => !DiagnosticCategories.DisplayOrder.Contains(p)))
        {
            yield return other;
        }
    }

    private static void AppendReadiness(StringBuilder sb, LiveTestReadiness readiness)
    {
        sb.AppendLine($"## {readiness.Headline}");
        sb.AppendLine();
        foreach (var blocker in readiness.Blockers)
        {
            sb.AppendLine($"- BLOCKER: {blocker}");
        }

        foreach (var note in readiness.Notes)
        {
            sb.AppendLine($"- Note: {note}");
        }

        sb.AppendLine();
    }
}
