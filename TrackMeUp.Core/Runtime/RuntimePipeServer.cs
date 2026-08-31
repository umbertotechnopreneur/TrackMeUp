// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using TrackMeUp.Application;

namespace TrackMeUp.Runtime;

/// <summary>Accepts same-user runtime connections and isolates the lifetime of each request.</summary>
internal sealed class RuntimePipeServer
{
    private readonly RuntimeEndpoint _endpoint;
    private readonly RuntimeRequestDispatcher _dispatcher;
    private readonly ILogger _logger;
    private readonly Action<AtomicResetPlan> _atomicResetPrepared;
    private readonly ConcurrentDictionary<int, Task> _activeRequests = new();
    private int _requestSequence;

    internal RuntimePipeServer(
        RuntimeEndpoint endpoint,
        RuntimeRequestDispatcher dispatcher,
        ILogger logger,
        Action<AtomicResetPlan> atomicResetPrepared)
    {
        _endpoint = endpoint;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _atomicResetPrepared = atomicResetPrepared ?? throw new ArgumentNullException(nameof(atomicResetPrepared));
    }

    /// <summary>Serves connections until host shutdown cancels the accept loop.</summary>
    internal async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // CurrentUserOnly is the OS boundary that prevents another Windows user from calling the local runtime.
            var pipe = new NamedPipeServerStream(
                _endpoint.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
                TrackRequest(HandleConnectionAsync(pipe, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await pipe.DisposeAsync();
                throw;
            }
            catch (Exception exception)
            {
                await pipe.DisposeAsync();
                // A transient accept failure is isolated; the next loop iteration remains the documented fallback.
                _logger.LogWarning("Runtime pipe acceptance failed; continuing to serve requests. ExceptionType={ExceptionType}", exception.GetType().Name);
            }
        }
    }

    /// <summary>Waits for the requests accepted before shutdown to finish.</summary>
    internal async Task DrainRequestsAsync()
    {
        var activeRequests = _activeRequests.Values.ToArray();
        if (activeRequests.Length > 0)
        {
            await Task.WhenAll(activeRequests);
        }
    }

    private void TrackRequest(Task requestTask)
    {
        var requestId = Interlocked.Increment(ref _requestSequence);
        _activeRequests[requestId] = requestTask;
        _ = requestTask.ContinueWith(
            completedTask =>
            {
                _activeRequests.TryRemove(requestId, out _);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken shutdownToken)
    {
        await using (pipe)
        {
            try
            {
                var request = await RuntimeProtocol.ReadAsync<RuntimeRequestEnvelope>(pipe, shutdownToken);
                using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
                var disconnectMonitor = MonitorDisconnectAsync(pipe, requestCancellation);
                RuntimeResponseEnvelope response;
                try
                {
                    response = await _dispatcher.DispatchAsync(request, requestCancellation.Token);
                }
                finally
                {
                    requestCancellation.Cancel();
                    await disconnectMonitor;
                }

                await RuntimeProtocol.WriteAsync(pipe, response, shutdownToken);
                if (request.Operation == RuntimeOperationCatalog.GetWireName(RuntimeOperation.AppAtomicResetV1)
                    && response.Succeeded
                    && response.Payload is AtomicResetPlan resetPlan)
                {
                    // The runtime owner begins shutdown only after the destructive-operation result reaches the caller.
                    _atomicResetPrepared(resetPlan);
                }
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                // Host shutdown cancels every active request before releasing runtime ownership.
            }
            catch (OperationCanceledException)
            {
                // Client disconnect/timeout cancellation stops long-running reads without affecting the host.
            }
            catch (Exception exception)
            {
                // Invalid/disconnected clients are isolated so the long-lived local runtime remains available.
                _logger.LogWarning("Runtime pipe request failed; continuing to serve requests. ExceptionType={ExceptionType}", exception.GetType().Name);
            }
        }
    }

    private static async Task MonitorDisconnectAsync(
        NamedPipeServerStream pipe,
        CancellationTokenSource requestCancellation)
    {
        var probe = new byte[1];
        try
        {
            _ = await pipe.ReadAsync(probe, requestCancellation.Token);
            requestCancellation.Cancel();
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
            // Normal completion cancels the pending disconnect read before the response is written.
        }
        catch (IOException)
        {
            // A disconnected client cancels its operation; the server continues accepting other requests.
            requestCancellation.Cancel();
        }
    }
}
