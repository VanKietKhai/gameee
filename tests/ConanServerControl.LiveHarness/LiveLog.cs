using System.Text.Json;
using ConanServerControl.Core.Diagnostics;

namespace ConanServerControl.LiveHarness;

/// <summary>
/// Append-only JSONL log of live-test steps. Every entry passes through the report redactor.
/// </summary>
internal sealed class LiveLog
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };
    private readonly string _path;
    private readonly DiagnosticReportRedactor _redactor;

    public LiveLog(string path, DiagnosticReportRedactor redactor)
    {
        _path = path;
        _redactor = redactor;
    }

    public string Path => _path;

    public void Write(
        string step,
        string operation,
        string result,
        TimeSpan? duration = null,
        IReadOnlyDictionary<string, string>? facts = null,
        string? excerpt = null,
        string? liveFilesChanged = null)
    {
        var entry = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTimeOffset.Now.ToString("o"),
            ["step"] = step,
            ["operation"] = operation,
            ["result"] = result,
            ["durationSeconds"] = duration is null ? null : Math.Round(duration.Value.TotalSeconds, 1),
            ["facts"] = facts?.ToDictionary(kv => kv.Key, kv => _redactor.Redact(kv.Value)),
            ["excerpt"] = _redactor.Redact(excerpt),
            ["liveFilesChanged"] = liveFilesChanged
        };
        var line = _redactor.Redact(JsonSerializer.Serialize(entry, Options))!;
        File.AppendAllText(_path, line + Environment.NewLine);

        Console.WriteLine();
        Console.WriteLine($"=== [{result}] {step} / {operation}{(duration is null ? string.Empty : $" ({duration.Value.TotalSeconds:0.0}s)")}");
        if (facts is not null)
        {
            foreach (var kv in facts)
            {
                Console.WriteLine($"    {kv.Key}: {_redactor.Redact(kv.Value)}");
            }
        }

        if (!string.IsNullOrWhiteSpace(excerpt))
        {
            Console.WriteLine("    --- excerpt ---");
            Console.WriteLine(_redactor.Redact(excerpt));
        }
    }
}
