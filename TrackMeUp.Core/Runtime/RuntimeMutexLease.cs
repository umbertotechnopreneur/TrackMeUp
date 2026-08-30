// SPDX-License-Identifier: MIT

namespace TrackMeUp.Runtime;

/// <summary>
/// Keeps named-mutex acquisition and release on one dedicated thread because Windows mutex ownership is thread-affine.
/// </summary>
internal sealed class RuntimeMutexLease : IDisposable
{
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly ManualResetEventSlim _release = new(false);
    private readonly Thread _ownerThread;
    private Exception? _failure;
    private bool _disposed;

    internal RuntimeMutexLease(string mutexName)
    {
        _ownerThread = new Thread(() => Own(mutexName))
        {
            IsBackground = true,
            Name = "TrackMeUp runtime mutex"
        };
        _ownerThread.Start();
        _ready.Wait();
        if (_failure is not null)
        {
            Dispose();
            throw new InvalidOperationException("Unable to acquire the TrackMeUp runtime mutex.", _failure);
        }
    }

    internal bool Acquired { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _release.Set();
        _ownerThread.Join();
        _release.Dispose();
        _ready.Dispose();
    }

    private void Own(string mutexName)
    {
        try
        {
            // Windows mutex ownership is bound to this thread; release must run here as well.
            using var mutex = new Mutex(false, mutexName);
            try
            {
                Acquired = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                Acquired = true;
            }

            _ready.Set();
            if (!Acquired)
            {
                return;
            }

            _release.Wait();
            mutex.ReleaseMutex();
        }
        catch (Exception exception)
        {
            // Acquisition failures are surfaced synchronously by the constructor; there is no ownership fallback.
            _failure = exception;
            _ready.Set();
        }
    }
}
