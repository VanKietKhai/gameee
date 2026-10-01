using ConanServerControl.Core.Abstractions;

namespace ConanServerControl.Infrastructure.Concurrency;

public sealed class ServerActionGate : IServerActionGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _current;

    public bool IsBusy => _semaphore.CurrentCount == 0;

    public string? CurrentAction => _current;

    public bool TryBegin(string action, out IDisposable? lease)
    {
        if (_semaphore.Wait(0))
        {
            _current = action;
            lease = new Lease(this, action);
            return true;
        }

        lease = null;
        return false;
    }

    public async Task<IDisposable> WaitAsync(string action, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        _current = action;
        return new Lease(this, action);
    }

    private void Release(string action)
    {
        if (string.Equals(_current, action, StringComparison.Ordinal))
        {
            _current = null;
        }

        _semaphore.Release();
    }

    private sealed class Lease : IDisposable
    {
        private readonly ServerActionGate _gate;
        private readonly string _action;
        private bool _disposed;

        public Lease(ServerActionGate gate, string action)
        {
            _gate = gate;
            _action = action;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _gate.Release(_action);
        }
    }
}
