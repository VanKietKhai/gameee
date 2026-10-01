using System.Text.RegularExpressions;

namespace ConanServerControl.Core.Diagnostics;

/// <summary>
/// Removes secrets from diagnostic text before it is displayed or exported.
/// Diagnostic checks are written so they never include secrets; this is the
/// second line of defence. Paths are intentionally left visible.
/// </summary>
public sealed class DiagnosticReportRedactor
{
    public const string Placeholder = "[REDACTED]";

    /// <summary>Secrets shorter than this are only redacted as whole tokens to avoid shredding normal text.</summary>
    private const int SubstringMinimumLength = 4;

    // key=value / key: value pairs whose key names a secret (launch args, ini lines, connection strings).
    private static readonly Regex SecretKeyValue = new(
        @"(?<key>[A-Za-z0-9_\-\.]*(password|passwd|pwd|secret|token|apikey|api_key|sessionid|session_id|cookie|authorization|credential)[A-Za-z0-9_\-\.]*)(?<sep>\s*[=:]\s*)(?<value>""[^""]*""|'[^']*'|[^\s;&,""']+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly string[] _secrets;

    public DiagnosticReportRedactor(IEnumerable<string?>? knownSecrets)
    {
        _secrets = (knownSecrets ?? Array.Empty<string?>())
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(s => s.Length)
            .ToArray();
    }

    public string? Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = text;
        foreach (var secret in _secrets)
        {
            if (secret.Length >= SubstringMinimumLength)
            {
                result = result.Replace(secret, Placeholder, StringComparison.Ordinal);
            }
            else
            {
                result = Regex.Replace(
                    result,
                    $@"(?<![A-Za-z0-9]){Regex.Escape(secret)}(?![A-Za-z0-9])",
                    Placeholder,
                    RegexOptions.CultureInvariant);
            }
        }

        return SecretKeyValue.Replace(result, m =>
            IsNonSecretFlag(m.Groups["value"].Value)
                ? m.Value
                : m.Groups["key"].Value + m.Groups["sep"].Value + Placeholder);
    }

    // "PasswordConfigured: YES" style status flags carry no secret; known secret values
    // equal to one of these words are still removed by the known-secret pass above.
    private static bool IsNonSecretFlag(string value) =>
        value is Placeholder ||
        value.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("NO", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("false", StringComparison.OrdinalIgnoreCase);

    public DiagnosticCheckResult Redact(DiagnosticCheckResult check)
    {
        ArgumentNullException.ThrowIfNull(check);
        return check with
        {
            Name = Redact(check.Name)!,
            Summary = Redact(check.Summary)!,
            Details = Redact(check.Details),
            SuggestedAction = Redact(check.SuggestedAction),
            Facts = check.Facts.ToDictionary(kv => kv.Key, kv => Redact(kv.Value) ?? string.Empty)
        };
    }

    public LiveTestReadiness Redact(LiveTestReadiness readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        return readiness with
        {
            Blockers = readiness.Blockers.Select(b => Redact(b)!).ToArray(),
            Notes = readiness.Notes.Select(n => Redact(n)!).ToArray()
        };
    }

    public IntegrationDiagnosticsReport Redact(IntegrationDiagnosticsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report with
        {
            ServerInstallDirectory = Redact(report.ServerInstallDirectory),
            StandaloneClientRoot = Redact(report.StandaloneClientRoot),
            Checks = report.Checks.Select(Redact).ToArray(),
            ServerLiveTest = Redact(report.ServerLiveTest),
            ClientCompatibilityTest = Redact(report.ClientCompatibilityTest)
        };
    }
}
