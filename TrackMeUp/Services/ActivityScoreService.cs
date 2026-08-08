using System.Collections.Generic;

namespace TrackMeUp.Services;

/// <summary>
/// Aggregates the latest thirty one-minute activity buckets for the live player score.
/// </summary>
public sealed class ActivityScoreService
{
    /// <summary>Gets the fixed number of one-minute bars retained by the compact player.</summary>
    public const int WindowMinutes = 30;

    private const int MaximumSnapshotIntervalMinutes = WindowMinutes / 2;
    private readonly object _gate = new();
    private readonly Dictionary<DateTimeOffset, MinuteAggregate> _minutes = new();

    /// <summary>Records keyboard, click, and active-time contributions from one durable activity sample.</summary>
    public void RecordSample(ActivitySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        lock (_gate)
        {
            var aggregate = GetOrCreate(TruncateToMinute(sample.Timestamp));
            aggregate.KeyPresses += sample.KeyPresses;
            aggregate.MouseClicks += sample.MouseClicks;
            if (string.Equals(sample.State, "active", StringComparison.OrdinalIgnoreCase))
            {
                aggregate.ActiveSeconds = Math.Min(60, aggregate.ActiveSeconds + sample.DurationSeconds);
            }
        }
    }

    /// <summary>Records the low-weight system telemetry captured by the application facade.</summary>
    public void RecordSystemSnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_gate)
        {
            var aggregate = GetOrCreate(TruncateToMinute(snapshot.Timestamp));
            aggregate.CpuUsagePercent = Math.Clamp(snapshot.CpuUsagePercent, 0, 100);
            aggregate.GpuUsagePercent = snapshot.GpuUsagePercent is { } gpuUsage
                ? Math.Clamp(gpuUsage, 0, 100)
                : null;
        }
    }

    /// <summary>Builds a trailing thirty-minute immutable rendering state at one-minute resolution.</summary>
    public ActivityScoreState GetState(int screenshotIntervalMinutes, DateTimeOffset? windowEndUtc = null)
    {
        var windowEnd = TruncateToMinute((windowEndUtc ?? DateTimeOffset.UtcNow).ToUniversalTime());
        var windowStart = windowEnd.AddMinutes(-(WindowMinutes - 1));
        var intervalMinutes = Math.Clamp(screenshotIntervalMinutes, 1, MaximumSnapshotIntervalMinutes);

        lock (_gate)
        {
            Trim(windowStart);
            var minutes = new List<ActivityScoreMinute>(WindowMinutes);
            for (var index = 0; index < WindowMinutes; index++)
            {
                var minute = windowStart.AddMinutes(index);
                var aggregate = GetOrCreate(minute);
                minutes.Add(new ActivityScoreMinute(
                    minute,
                    CalculateScore(aggregate),
                    aggregate.KeyPresses,
                    aggregate.MouseClicks,
                    aggregate.ActiveSeconds,
                    aggregate.CpuUsagePercent,
                    aggregate.GpuUsagePercent));
            }

            var latestStart = windowEnd.AddMinutes(-(intervalMinutes - 1));
            var previousStart = latestStart.AddMinutes(-intervalMinutes);
            return new ActivityScoreState(
                windowStart,
                windowEnd,
                intervalMinutes,
                minutes,
                minutes[^1].Score,
                BuildInterval(minutes, previousStart, latestStart.AddMinutes(-1)),
                BuildInterval(minutes, latestStart, windowEnd));
        }
    }

    private MinuteAggregate GetOrCreate(DateTimeOffset minuteUtc)
    {
        if (_minutes.TryGetValue(minuteUtc, out var aggregate))
        {
            return aggregate;
        }

        aggregate = new MinuteAggregate();
        _minutes.Add(minuteUtc, aggregate);
        return aggregate;
    }

    private void Trim(DateTimeOffset oldestMinute)
    {
        var stale = _minutes.Keys.Where(minute => minute < oldestMinute).ToArray();
        foreach (var minute in stale)
        {
            _minutes.Remove(minute);
        }
    }

    private static ActivityScoreInterval BuildInterval(
        IEnumerable<ActivityScoreMinute> minutes,
        DateTimeOffset fromInclusive,
        DateTimeOffset toInclusive)
    {
        var keys = 0L;
        var clicks = 0L;
        foreach (var minute in minutes)
        {
            if (minute.MinuteUtc < fromInclusive || minute.MinuteUtc > toInclusive)
            {
                continue;
            }

            keys += minute.KeyPresses;
            clicks += minute.MouseClicks;
        }

        return new ActivityScoreInterval(keys, clicks);
    }

    private static int CalculateScore(MinuteAggregate aggregate)
    {
        // Input is deliberately dominant; CPU and GPU together can add at most six points.
        var inputContribution = Math.Min(86d, (aggregate.KeyPresses * 0.55d) + (aggregate.MouseClicks * 3.5d));
        var activeContribution = aggregate.ActiveSeconds > 0 ? 8d : 0d;
        var cpuContribution = aggregate.CpuUsagePercent * 0.04d;
        var gpuContribution = (aggregate.GpuUsagePercent ?? 0) * 0.02d;
        return (int)Math.Round(Math.Clamp(inputContribution + activeContribution + cpuContribution + gpuContribution, 0d, 100d));
    }

    private static DateTimeOffset TruncateToMinute(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, TimeSpan.Zero);
    }

    private sealed class MinuteAggregate
    {
        public long KeyPresses { get; set; }
        public long MouseClicks { get; set; }
        public int ActiveSeconds { get; set; }
        public int CpuUsagePercent { get; set; }
        public int? GpuUsagePercent { get; set; }
    }
}
