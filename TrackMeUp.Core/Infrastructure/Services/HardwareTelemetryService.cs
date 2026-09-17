// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace TrackMeUp.Services;

/// <summary>Serializes the sole isolated hardware collector and shares immutable snapshots across consumers.</summary>
public sealed class HardwareTelemetryService : IHardwareTelemetryService
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FailureRetryInterval = TimeSpan.FromSeconds(10);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _trackingGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string _helperPath;
    private readonly ILogger<HardwareTelemetryService> _logger;
    private readonly TimeProvider _time;
    private readonly Func<CancellationToken, ValueTask<SystemSnapshot>>? _testReader;
    private CancellationTokenSource? _polling;
    private Task? _pollTask;
    private NamedPipeServerStream? _pipe;
    private Process? _helper;
    private SystemSnapshot? _snapshot;
    private DateTimeOffset _lastAttempt;
    private bool _advanced;
    private bool _disposed;

    /// <summary>Creates the application-owned collector with the packaged helper beside the executable.</summary>
    public HardwareTelemetryService(ILogger<HardwareTelemetryService>? logger = null)
        : this(Path.Combine(AppContext.BaseDirectory, "Hardware", "TrackMeUp.Hardware.exe"), TimeProvider.System, logger) { }

    internal HardwareTelemetryService(string helperPath, TimeProvider timeProvider, ILogger<HardwareTelemetryService>? logger = null,
        Func<CancellationToken, ValueTask<SystemSnapshot>>? reader = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helperPath);
        _helperPath = Path.GetFullPath(helperPath);
        _time = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? NullLogger<HardwareTelemetryService>.Instance;
        _testReader = reader;
    }

    /// <summary>Returns a recent immutable reading or collects one with a strict process/IPC deadline.</summary>
    public async ValueTask<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _time.GetUtcNow();
            var retry = _snapshot?.Status is "error" or "unavailable" or "unsupported" ? FailureRetryInterval : SampleInterval;
            if (_snapshot is not null && now - _lastAttempt < retry) return _snapshot;
            _lastAttempt = now;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            deadline.CancelAfter(_pipe is null ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(5));
            try
            {
                var snapshot = _testReader is null
                    ? await ReadCollectorAsync(deadline.Token).ConfigureAwait(false)
                    : await _testReader(deadline.Token).ConfigureAwait(false);
                SystemSnapshotValidator.Validate(snapshot);
                // Own every collection so no consumer can mutate the snapshot seen by another consumer.
                _snapshot = snapshot with
                {
                    Devices = Array.AsReadOnly(snapshot.Devices.Select(device => device with
                    {
                        Sensors = Array.AsReadOnly(device.Sensors.ToArray())
                    }).ToArray())
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _lifetime.IsCancellationRequested)
            {
                await StopCollectorAsync().ConfigureAwait(false);
                throw;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or Win32Exception or UnauthorizedAccessException or OperationCanceledException or System.Text.Json.JsonException)
            {
                // Optional telemetry failure is visible in the snapshot; screenshot capture remains available.
                await StopCollectorAsync().ConfigureAwait(false);
                var code = exception switch
                {
                    FileNotFoundException => "helper-missing",
                    OperationCanceledException => "collector-timeout",
                    UnauthorizedAccessException => "collector-access-denied",
                    _ => "collector-failed"
                };
                _logger.LogWarning("Hardware telemetry unavailable. ErrorCode={ErrorCode} ExceptionType={ExceptionType}", code, exception.GetType().Name);
                _snapshot = new SystemSnapshot(_time.GetUtcNow(), "unavailable", [], GetDriverStatus(), code, now);
            }
            return _snapshot;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Polls while tracking is active, and cancels periodic work when tracking stops.</summary>
    public async ValueTask SetTrackingAsync(bool isTracking, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _trackingGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (isTracking && _polling is null)
            {
                _polling = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                _pollTask = PollAsync(_polling.Token);
            }
            else if (!isTracking && _polling is not null)
            {
                await _polling.CancelAsync().ConfigureAwait(false);
                if (_pollTask is not null) await _pollTask.ConfigureAwait(false);
                _polling.Dispose();
                _polling = null;
                _pollTask = null;
            }
        }
        finally { _trackingGate.Release(); }
    }

    /// <summary>Explicitly requests Windows elevation for this collector session after checking the prerequisite.</summary>
    public async Task EnableAdvancedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64) throw new PlatformNotSupportedException("Advanced hardware telemetry requires x64.");
        if (GetDriverStatus() == "not-installed") throw new InvalidOperationException("PawnIO is not installed.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_advanced && _pipe is { IsConnected: true }) return;
            await StopCollectorAsync().ConfigureAwait(false);
            _advanced = true;
            _snapshot = null;
            try
            {
                // UAC consent is requested only through this explicit application action.
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(60));
                await StartCollectorAsync(deadline.Token).ConfigureAwait(false);
            }
            catch
            {
                _advanced = false;
                await StopCollectorAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    /// <summary>Stops polling, closes the pipe and releases the only helper owned by this service.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _lifetime.CancelAsync().ConfigureAwait(false);
        if (_pollTask is not null) await _pollTask.ConfigureAwait(false);
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await StopCollectorAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
        _polling?.Dispose();
        _lifetime.Dispose();
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(SampleInterval, _time);
            while (!cancellationToken.IsCancellationRequested)
            {
                await CaptureAsync(cancellationToken).ConfigureAwait(false);
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false)) break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (_disposed) { }
    }

    private async ValueTask<SystemSnapshot> ReadCollectorAsync(CancellationToken cancellationToken)
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return new SystemSnapshot(_time.GetUtcNow(), "unsupported", [], "unsupported-architecture", "collector-architecture-unsupported");
        if (_pipe is null) await StartCollectorAsync(cancellationToken).ConfigureAwait(false);
        await HardwareTelemetryProtocol.WriteAsync(_pipe!, new HardwareCollectorRequest(HardwareTelemetryProtocol.Version, "sample"), cancellationToken).ConfigureAwait(false);
        return await HardwareTelemetryProtocol.ReadAsync<SystemSnapshot>(_pipe!, cancellationToken).ConfigureAwait(false);
    }

    private async Task StartCollectorAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_helperPath)) throw new FileNotFoundException("The packaged hardware collector is missing.");
        var pipeName = "TrackMeUp.Hardware." + Guid.NewGuid().ToString("N");
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new UnauthorizedAccessException("Windows user identity is unavailable.");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(user);
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
        // A same-user ACL permits this user's elevated token without granting other users access.
        _pipe = NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous, 4096, 4096, security);
        var start = new ProcessStartInfo
        {
            FileName = _helperPath,
            WorkingDirectory = Path.GetDirectoryName(_helperPath)!,
            UseShellExecute = _advanced,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        start.ArgumentList.Add("--pipe");
        start.ArgumentList.Add(pipeName);
        start.ArgumentList.Add("--parent");
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (_advanced)
        {
            start.Verb = "runas";
            start.ArgumentList.Add("--advanced");
        }
        var launchTask = Task.Run(() => Process.Start(start), cancellationToken);
        try
        {
            _helper = await launchTask.WaitAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new IOException("The hardware collector could not be started.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Windows consent can outlive this request: release the gate now and observe the
            // late launch without retaining its process handle. Its closed pipe expires on connect.
            _ = launchTask.ContinueWith(completed =>
            {
                if (completed.Status == TaskStatus.RanToCompletion) completed.Result?.Dispose();
                else if (completed.IsFaulted) _ = completed.Exception;
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
        await _pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        HardwareTelemetryProtocol.VerifyClientProcess(_pipe, _helper.Id);
        var hello = await HardwareTelemetryProtocol.ReadAsync<HardwareCollectorHello>(_pipe, cancellationToken).ConfigureAwait(false);
        if (hello.Version != HardwareTelemetryProtocol.Version || _advanced && !hello.Elevated)
            throw new InvalidDataException("Hardware collector version or privilege mode is invalid.");
    }

    private async Task StopCollectorAsync()
    {
        // A disconnected advanced session never causes a later background poll to request UAC.
        _advanced = false;
        if (_pipe is not null)
        {
            // Closing the parent pipe is terminal for the helper, including an elevated helper.
            await _pipe.DisposeAsync().ConfigureAwait(false);
            _pipe = null;
        }
        if (_helper is not null)
        {
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
                await _helper.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Terminate only the exact child process this service launched, never unrelated sensor applications.
                try { if (!_helper.HasExited) _helper.Kill(); }
                catch (Win32Exception) { /* An elevated child will exit on pipe disconnect; the watchdog bounds blocked reads. */ }
            }
            catch (InvalidOperationException) { /* The child already exited before process observation. */ }
            finally { _helper.Dispose(); _helper = null; }
        }
    }

    private static string GetDriverStatus()
    {
        using var registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");
        return key?.GetValue("DisplayVersion") is string version && Version.TryParse(version, out _) ? "available" : "not-installed";
    }
}
