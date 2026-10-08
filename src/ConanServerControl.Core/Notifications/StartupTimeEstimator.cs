using ConanServerControl.Core.Models;

namespace ConanServerControl.Core.Notifications;

/// <summary>
/// Measures how long the dedicated server took from launch to ready, using the activity log
/// lines written by the process manager, and describes it for players.
/// </summary>
public static class StartupTimeEstimator
{
    public const string LaunchedPrefix = "Server process launched";
    public const string ReadyMessage = "Server is ready.";

    private static readonly TimeSpan MinPlausible = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxPlausible = TimeSpan.FromMinutes(20);

    /// <summary>Launch-to-ready durations in chronological order.</summary>
    public static IReadOnlyList<TimeSpan> MeasureDurations(IEnumerable<ActivityLogEntry> entries)
    {
        var durations = new List<TimeSpan>();
        DateTimeOffset? launchedAt = null;
        foreach (var entry in entries.OrderBy(e => e.Timestamp).ThenBy(e => e.Id))
        {
            if (entry.Message.StartsWith(LaunchedPrefix, StringComparison.Ordinal))
            {
                launchedAt = entry.Timestamp;
            }
            else if (entry.Message == ReadyMessage && launchedAt is not null)
            {
                var duration = entry.Timestamp - launchedAt.Value;
                if (duration >= MinPlausible && duration <= MaxPlausible)
                {
                    durations.Add(duration);
                }

                launchedAt = null;
            }
            else if (entry.Message is "Server stopped." or "Server process exited unexpectedly.")
            {
                launchedAt = null;
            }
        }

        return durations;
    }

    /// <summary>
    /// For example "Thường mất khoảng 40 giây, có lúc tới 2 phút." from the last
    /// <paramref name="sampleSize"/> starts.
    /// </summary>
    public static string Describe(IReadOnlyList<TimeSpan> durations, int sampleSize = 10)
    {
        var recent = durations.Skip(Math.Max(0, durations.Count - sampleSize)).ToArray();
        if (recent.Length == 0)
        {
            return "Vui lòng chờ một lát.";
        }

        var sorted = recent.OrderBy(d => d).ToArray();
        var median = sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : TimeSpan.FromTicks((sorted[sorted.Length / 2 - 1].Ticks + sorted[sorted.Length / 2].Ticks) / 2);
        var max = sorted[^1];
        var typical = Format(median);
        var worst = Format(max);
        var spread = max - median;
        return spread >= TimeSpan.FromSeconds(20) && max.TotalSeconds > median.TotalSeconds * 1.5 && worst != typical
            ? $"Thường mất khoảng {typical}, có lúc tới {worst}."
            : $"Thường mất khoảng {typical}.";
    }

    /// <summary>Rounds up: under a minute to the next 5 seconds, otherwise to the next whole minute.</summary>
    public static string Format(TimeSpan duration)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds));
        if (seconds < 60)
        {
            var rounded = (int)Math.Ceiling(seconds / 5d) * 5;
            return rounded >= 60 ? "1 phút" : $"{rounded} giây";
        }

        return $"{(int)Math.Ceiling(seconds / 60d)} phút";
    }
}
