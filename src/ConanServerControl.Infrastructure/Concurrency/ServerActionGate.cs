using ConanServerControl.Core.Abstractions;

namespace ConanServerControl.Infrastructure.Concurrency;

public sealed class ServerActionGate : IServerActionGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _current;
    private Guid? _currentLeaseId;

    public bool IsBusy => _semaphore.CurrentCount == 0;

    public string? CurrentAction => _current;

    public Guid? CurrentLeaseId => _currentLeaseId;

    public bool Owns(IServerOperationLease? lease) =>
        lease is not null
        && !lease.IsDisposed
        && _currentLeaseId is { } id
        && id == lease.Id
        && IsBusy;

    public bool TryBegin(string action, out IServerOperationLease? lease)
    {
        if (_semaphore.Wait(0))
        {
            var created = new Lease(this, action);
            _current = action;
            _currentLeaseId = created.Id;
            lease = created;
            return true;
        }

        lease = null;
        return false;
    }

    public async Task<IServerOperationLease> WaitAsync(string action, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        var created = new Lease(this, action);
        _current = action;
        _currentLeaseId = created.Id;
        return created;
    }

    private void Release(Guid id)
    {
        if (_currentLeaseId != id)
        {
            return;
        }

        _current = null;
        _currentLeaseId = null;
        _semaphore.Release();
    }

    private sealed class Lease : IServerOperationLease
    {
        private readonly ServerActionGate _gate;
        private bool _disposed;

        public Lease(ServerActionGate gate, string action)
        {
            _gate = gate;
            Action = action;
            Id = Guid.NewGuid();
        }

        public Guid Id { get; }

        public string Action { get; }

        public bool IsDisposed => _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _gate.Release(Id);
        }
    }
}
