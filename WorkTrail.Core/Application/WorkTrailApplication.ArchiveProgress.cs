// SPDX-License-Identifier: MIT

namespace WorkTrail.Application;

public sealed partial class WorkTrailApplication
{
    /// <inheritdoc />
    public Task<OperationResult<DataArchiveProgress?>> GetDataArchiveProgressAsync(DataArchiveProgressRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OperationId == Guid.Empty) throw new ArgumentException("An archive operation identity is required.", nameof(request));
        // No mutation gate: reads must stay responsive while the archive holds that gate.
        return Task.FromResult(OperationResult<DataArchiveProgress?>.Success("archive.progress", "", _archiveProgress.Read(request.OperationId)));
    }
}
