// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using WorkTrail.Services;

namespace WorkTrail.Application;

/// <summary>Coordinates bounded desktop capture work and its optional hardware context.</summary>
public sealed partial class WorkTrailApplication
{
    private async Task<SystemSnapshot> CaptureScreenshotHardwareAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = _settingsSnapshot.Value;
            var context = await _deviceContext.CaptureAsync(
                settings.OpenAiEnabled && settings.IncludeDeviceLocation, cancellationToken).ConfigureAwait(false);
            // Read hardware last so an optional slower location lookup cannot age the sensor sample before pixels.
            var snapshot = await CaptureAndRecordSystemSnapshotAsync(cancellationToken).ConfigureAwait(false);
            return snapshot with
            {
                DeviceContext = context,
                InformationalSchedule = ActiveHoursSchedule.BuildInformationalNote(settings.ActiveHours, snapshot.Timestamp)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Optional sensor failure is durable and visible; image capture is still allowed.
            _logger.LogWarning("Screenshot hardware collection failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            return new SystemSnapshot(DateTimeOffset.UtcNow, "error", [], ErrorCode: "collection-failed");
        }
    }

    private async Task<T> RunCaptureWorkAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _captureWorker.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The bounded worker keeps synchronous desktop capture and codecs off WinUI and prevents overlap.
            return await Task.Run(operation, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _captureWorker.Release();
        }
    }
}
