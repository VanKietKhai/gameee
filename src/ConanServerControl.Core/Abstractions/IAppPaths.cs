using ConanServerControl.Core.Settings;

namespace ConanServerControl.Core.Abstractions;

public interface IAppPaths
{
    string DataDirectory { get; }

    string LogsDirectory { get; }

    string BackupsDirectory { get; }

    string SettingsFilePath { get; }

    string SecretsFilePath { get; }

    string DatabaseFilePath { get; }

    string SteamCmdDefaultDirectory { get; }

    string StagingDirectory { get; }

    void EnsureCreated();
}

public interface ISettingsService
{
    AppSettings Current { get; }

    ProtectedSecrets Secrets { get; }

    event EventHandler? Changed;

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default);

    Task UpdateSecretsAsync(Action<ProtectedSecrets> mutate, CancellationToken cancellationToken = default);
}

public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedPayload);
}

public interface ILiveLogBuffer
{
    event EventHandler<LogEntryEventArgs>? EntryAdded;

    IReadOnlyList<Core.Models.LogEntry> Snapshot(int maxEntries = 500);

    void Append(Core.Models.LogEntry entry);
}

public sealed class LogEntryEventArgs : EventArgs
{
    public required Core.Models.LogEntry Entry { get; init; }
}

public interface IActivityLog
{
    Task AddAsync(string category, string message, string? actor = null, string? details = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Core.Models.ActivityLogEntry>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default);
}

public interface IServerOperationLease : IDisposable
{
    Guid Id { get; }

    string Action { get; }

    bool IsDisposed { get; }
}

public interface IServerActionGate
{
    bool IsBusy { get; }

    string? CurrentAction { get; }

    Guid? CurrentLeaseId { get; }

    bool TryBegin(string action, out IServerOperationLease? lease);

    Task<IServerOperationLease> WaitAsync(string action, CancellationToken cancellationToken = default);

    bool Owns(IServerOperationLease? lease);
}
