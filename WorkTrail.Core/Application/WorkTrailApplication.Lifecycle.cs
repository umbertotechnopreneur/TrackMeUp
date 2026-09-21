// SPDX-License-Identifier: MIT

using WorkTrail.Services;

namespace WorkTrail.Application;

/// <summary>Owns runtime disposal, cancellation and serialized application work.</summary>
public sealed partial class WorkTrailApplication
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelLiveWork();
        _runtimeTimerCancellation.Cancel();
        Task liveWorkDrained;
        lock (_liveWorkLock)
        {
            liveWorkDrained = _liveWorkCount == 0 ? Task.CompletedTask
                : (_liveWorkDrained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }
        await liveWorkDrained.ConfigureAwait(false);
        _liveWorkCancellation.Dispose();
        await _runtimeTimerTask.ConfigureAwait(false);
        _runtimeTimerCancellation.Dispose();
        await _screenshotReprocessing.DisposeAsync().ConfigureAwait(false);
        _tracking.DashboardStateChanged -= OnDashboardStateChanged;
        _tracking.TrackingStateChanged -= OnTrackingStateChanged;
        _tracking.RuntimeHealthChanged -= OnTrackingRuntimeHealthChanged;
        if (_pricingRefresh is not null)
        {
            await _pricingRefresh.DisposeAsync().ConfigureAwait(false);
        }
        _tracking.Dispose();
        await _search.DisposeAsync().ConfigureAwait(false);
        _captureWorker.Dispose();
        _manualScreenshotCaptureGate.Dispose();
        _systemSnapshotGate.Dispose();
        await _snapshot.DisposeAsync().ConfigureAwait(false);
        _worldClockOperations.Dispose();
        _mutations.Dispose();
    }

    private async Task<OperationResult<T>> MutateAsync<T>(Func<Task<OperationResult<T>>> operation, CancellationToken cancellationToken)
    {
        await _mutations.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            _mutations.Release();
        }
    }

    private async Task<T> RunCancellableLiveWorkAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        CancellationTokenSource linked;
        CancellationToken policyToken;
        lock (_liveWorkLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            policyToken = _liveWorkCancellation.Token;
            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, policyToken);
            _liveWorkCount++;
        }
        try { return await AiPolicyCancellation.RunAsync(() => operation(linked.Token), policyToken).ConfigureAwait(false); }
        finally
        {
            linked.Dispose();
            lock (_liveWorkLock)
            {
                if (--_liveWorkCount == 0) _liveWorkDrained?.TrySetResult(true);
            }
        }
    }

    private void CancelLiveWork()
    {
        CancellationTokenSource previous;
        lock (_liveWorkLock)
        {
            previous = _liveWorkCancellation;
            _liveWorkCancellation = new CancellationTokenSource();
        }
        previous.Cancel();
        previous.Dispose();
    }

    private async Task<T> RunLiveAnalysisOutsideMutationAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        // Visual work retains its own serialization boundary. The global command gate is not held
        // while waiting for that boundary or the provider; stop/disable can persist policy immediately.
        _mutations.Release();
        try
        {
            return await _screenshotReprocessing.RunLiveAnalysisAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await operation().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Reacquire even after cancellation: outer cleanup and mutation release still own this lease.
            await _mutations.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task RecoverScreenshotDeletionsAsync(CancellationToken cancellationToken)
    {
        var pending = _screenshotDeletions.Pending();
        if (pending.Count == 0) return;
        foreach (var deletion in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _screenshotDeletions.Execute(deletion);
        }
        await _search.SynchronizeAsync(cancellationToken).ConfigureAwait(false);
        foreach (var deletion in pending) _screenshotDeletions.Complete(deletion);
    }
}
