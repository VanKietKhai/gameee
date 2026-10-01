using ConanServerControl.Core.Abstractions;
using ConanServerControl.Core.Models;

namespace ConanServerControl.Infrastructure.Logging;

public sealed class LiveLogBuffer : ILiveLogBuffer
{
    private const int Capacity = 2000;
    private readonly object _sync = new();
    private readonly Queue<LogEntry> _entries = new();

    public event EventHandler<LogEntryEventArgs>? EntryAdded;

    public IReadOnlyList<LogEntry> Snapshot(int maxEntries = 500)
    {
        lock (_sync)
        {
            return _entries.TakeLast(maxEntries).ToArray();
        }
    }

    public void Append(LogEntry entry)
    {
        lock (_sync)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > Capacity)
            {
                _entries.Dequeue();
            }
        }

        EntryAdded?.Invoke(this, new LogEntryEventArgs { Entry = entry });
    }
}

public sealed class LiveLogSink : Serilog.Core.ILogEventSink
{
    private readonly ILiveLogBuffer _buffer;

    public LiveLogSink(ILiveLogBuffer buffer)
    {
        _buffer = buffer;
    }

    public void Emit(Serilog.Events.LogEvent logEvent)
    {
        var source = "App";
        if (logEvent.Properties.TryGetValue("SourceContext", out var ctx))
        {
            source = ctx.ToString().Trim('"');
            var lastDot = source.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < source.Length - 1)
            {
                source = source[(lastDot + 1)..];
            }
        }

        _buffer.Append(new LogEntry
        {
            Timestamp = logEvent.Timestamp,
            Level = logEvent.Level.ToString(),
            Source = source,
            Message = logEvent.RenderMessage()
        });
    }
}
