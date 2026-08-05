using TrackMeUp.Application;

namespace TrackMeUp.Services;

/// <summary>Builds privacy-safe reports from the SQLite activity history.</summary>
public sealed class ReportAggregationService
{
    /// <summary>Gets the maximum inclusive local-date range accepted by a report query.</summary>
    public const int MaximumRangeDays = 366;

    private const int ContractVersion = 2;
    private const int DefaultApplicationLimit = 12;
    private readonly LocalStore _store;

    /// <summary>Initializes an aggregate report service over the shared local store.</summary>
    public ReportAggregationService(LocalStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Builds an aggregate report, returning validation issues for invalid user ranges.</summary>
    public OperationResult<ReportSnapshot> Build(ReportQuery query, CancellationToken cancellationToken) =>
        Build(query, cancellationToken, DefaultApplicationLimit);

    /// <summary>Builds an aggregate report with a caller-selected application limit.</summary>
    internal OperationResult<ReportSnapshot> Build(
        ReportQuery query,
        CancellationToken cancellationToken,
        int applicationLimit)
    {
        if (query is null)
        {
            return OperationResult<ReportSnapshot>.Failure(
                "report.query.required",
                "ReportQueryRequired",
                new ValidationIssue("query", "required", "ReportQueryRequired"));
        }

        if (query.From > query.ToInclusive)
        {
            return OperationResult<ReportSnapshot>.Failure(
                "report.range.invalid",
                "ReportRangeInvalid",
                new ValidationIssue("toInclusive", "before_from", "ReportRangeInvalid"));
        }

        var dayCount = query.ToInclusive.DayNumber - query.From.DayNumber + 1;
        if (dayCount > MaximumRangeDays)
        {
            return OperationResult<ReportSnapshot>.Failure(
                "report.range.too_large",
                "ReportRangeTooLarge",
                new ValidationIssue("toInclusive", "maximum_366_days", "ReportRangeTooLarge"));
        }

        if (query.ToInclusive == DateOnly.MaxValue)
        {
            return OperationResult<ReportSnapshot>.Failure(
                "report.range.invalid",
                "ReportRangeInvalid",
                new ValidationIssue("toInclusive", "out_of_range", "ReportRangeInvalid"));
        }

        if (applicationLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(applicationLimit));
        }

        var timeZoneResult = ResolveTimeZone(query.TimeZoneId);
        if (timeZoneResult.TimeZone is null)
        {
            return OperationResult<ReportSnapshot>.Failure(
                "report.time_zone.invalid",
                "ReportTimeZoneInvalid",
                new ValidationIssue("timeZoneId", "invalid", "ReportTimeZoneInvalid"));
        }

        DateTimeOffset fromUtc;
        DateTimeOffset toUtc;
        try
        {
            fromUtc = ConvertLocalBoundaryToUtc(query.From, timeZoneResult.TimeZone);
            toUtc = ConvertLocalBoundaryToUtc(query.ToInclusive.AddDays(1), timeZoneResult.TimeZone);
        }
        catch (ArgumentException)
        {
            return OperationResult<ReportSnapshot>.Failure(
                "report.range.invalid",
                "ReportRangeInvalid",
                new ValidationIssue("range", "unrepresentable", "ReportRangeInvalid"));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var aggregation = new AggregationState(query.From, dayCount, timeZoneResult.TimeZone, fromUtc, toUtc);
        var aiUsage = new AiUsageAccumulator();

        // Both forward-only readers share one SQLite read transaction; no raw activity sample crosses this boundary.
        _store.VisitReportData(
            fromUtc,
            toUtc,
            cancellationToken,
            aggregation.AddSample,
            aiUsage.Add);

        var snapshot = aggregation.BuildSnapshot(
            query.ToInclusive,
            timeZoneResult.TimeZone.Id,
            applicationLimit,
            aiUsage.Build());
        return OperationResult<ReportSnapshot>.Success("report.loaded", "ReportLoaded", snapshot);
    }

    private static (TimeZoneInfo? TimeZone, string? Error) ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return (TimeZoneInfo.Local, null);
        }

        try
        {
            return (TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim()), null);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return (null, exception.GetType().Name);
        }
    }

    private static DateTimeOffset ConvertLocalBoundaryToUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            // The larger ambiguous offset selects the first occurrence of a repeated local boundary.
            var offset = timeZone.GetAmbiguousTimeOffsets(local).Max();
            return new DateTimeOffset(local, offset).ToUniversalTime();
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone));
    }

    private sealed class AggregationState
    {
        private readonly DateOnly _from;
        private readonly int _dayCount;
        private readonly TimeZoneInfo _timeZone;
        private readonly long _fromUtcTicks;
        private readonly long _toUtcTicks;
        private readonly Dictionary<DateOnly, DayAccumulator> _days;
        private readonly Dictionary<(int DayOfWeek, int Hour), HourAccumulator> _hours;
        private readonly Dictionary<string, long> _applicationTicks = new(StringComparer.OrdinalIgnoreCase);
        private long? _coverageStartTicks;
        private long _coverageEndTicks;
        private long _coveredTicks;
        private DateTimeOffset? _firstSampleAt;
        private DateTimeOffset? _lastSampleAt;
        private int _sampleCount;

        internal AggregationState(
            DateOnly from,
            int dayCount,
            TimeZoneInfo timeZone,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc)
        {
            _from = from;
            _dayCount = dayCount;
            _timeZone = timeZone;
            _fromUtcTicks = fromUtc.UtcDateTime.Ticks;
            _toUtcTicks = toUtc.UtcDateTime.Ticks;
            _days = Enumerable.Range(0, dayCount)
                .ToDictionary(offset => from.AddDays(offset), _ => new DayAccumulator());
            _hours = Enumerable.Range(0, 7)
                .SelectMany(day => Enumerable.Range(0, 24).Select(hour => (day, hour)))
                .ToDictionary(key => key, _ => new HourAccumulator());
        }

        internal void AddSample(ReportSourceSample sample)
        {
            var originalDurationTicks = checked((long)sample.DurationSeconds * TimeSpan.TicksPerSecond);
            var originalEndTicks = sample.Timestamp.UtcDateTime.Ticks;
            var originalStartTicks = checked(originalEndTicks - originalDurationTicks);
            var clippedStartTicks = Math.Max(originalStartTicks, _fromUtcTicks);
            var clippedEndTicks = Math.Min(originalEndTicks, _toUtcTicks);
            if (clippedStartTicks >= clippedEndTicks)
            {
                return;
            }

            _sampleCount++;
            if (_firstSampleAt is null || sample.Timestamp < _firstSampleAt.Value)
            {
                _firstSampleAt = sample.Timestamp;
            }

            if (_lastSampleAt is null || sample.Timestamp > _lastSampleAt.Value)
            {
                _lastSampleAt = sample.Timestamp;
            }

            AddCoverage(clippedStartTicks, clippedEndTicks);
            var segments = SplitIntoLocalHourSegments(clippedStartTicks, clippedEndTicks, _timeZone);
            var includedDurationTicks = clippedEndTicks - clippedStartTicks;
            var includedKeyPresses = ScaleCount(sample.KeyPresses, includedDurationTicks, originalDurationTicks);
            var includedMouseClicks = ScaleCount(sample.MouseClicks, includedDurationTicks, originalDurationTicks);
            var keyPresses = AllocateCount(includedKeyPresses, segments, includedDurationTicks);
            var mouseClicks = AllocateCount(includedMouseClicks, segments, includedDurationTicks);
            var sampleDates = new HashSet<DateOnly>();
            var isActive = string.Equals(sample.State, "active", StringComparison.OrdinalIgnoreCase);
            var isIdle = string.Equals(sample.State, "idle", StringComparison.OrdinalIgnoreCase);

            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                if (!_days.TryGetValue(segment.Bucket.Date, out var day))
                {
                    throw new InvalidOperationException("An activity segment fell outside the normalized report range.");
                }

                var durationTicks = segment.EndTicks - segment.StartTicks;
                day.TrackedTicks += durationTicks;
                day.KeyPresses += keyPresses[index];
                day.MouseClicks += mouseClicks[index];
                if (isActive)
                {
                    day.ActiveTicks += durationTicks;
                }
                else if (isIdle)
                {
                    day.IdleTicks += durationTicks;
                }

                var hour = _hours[(segment.Bucket.DayOfWeek, segment.Bucket.Hour)];
                hour.TrackedTicks += durationTicks;
                hour.ObservationDates.Add(segment.Bucket.Date);
                if (isActive)
                {
                    hour.ActiveTicks += durationTicks;
                }
                else if (isIdle)
                {
                    hour.IdleTicks += durationTicks;
                }

                sampleDates.Add(segment.Bucket.Date);
            }

            foreach (var date in sampleDates)
            {
                _days[date].SampleCount++;
            }

            if (isActive)
            {
                var application = string.IsNullOrWhiteSpace(sample.Application) ? "Unknown" : sample.Application.Trim();
                _applicationTicks[application] = _applicationTicks.GetValueOrDefault(application) + includedDurationTicks;
            }
        }

        internal ReportSnapshot BuildSnapshot(
            DateOnly toInclusive,
            string timeZoneId,
            int applicationLimit,
            AiUsageSummary aiUsage)
        {
            FinishCoverage();
            var calendar = _days
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value.ToCalendarCell(pair.Key))
                .ToArray();
            var hours = _hours
                .OrderBy(pair => pair.Key.DayOfWeek)
                .ThenBy(pair => pair.Key.Hour)
                .Select(pair => pair.Value.ToHourCell(pair.Key.DayOfWeek, pair.Key.Hour))
                .ToArray();
            var trend = calendar
                .Select(day => new ReportTrendBucket(
                    day.Date,
                    day.Date,
                    day.ActiveSeconds,
                    day.IdleSeconds,
                    day.TrackedSeconds,
                    day.KeyPresses,
                    day.MouseClicks,
                    day.HasData))
                .ToArray();
            var applications = BuildApplications(applicationLimit);
            var activeSeconds = calendar.Sum(day => day.ActiveSeconds);
            var idleSeconds = calendar.Sum(day => day.IdleSeconds);
            var trackedSeconds = calendar.Sum(day => day.TrackedSeconds);
            var requestedSeconds = (_toUtcTicks - _fromUtcTicks) / TimeSpan.TicksPerSecond;
            var coveredSeconds = _coveredTicks / TimeSpan.TicksPerSecond;
            var coverageRatio = requestedSeconds == 0
                ? 0d
                : Math.Clamp(coveredSeconds / (double)requestedSeconds, 0d, 1d);

            return new ReportSnapshot(
                ContractVersion,
                new ReportRange(_from, toInclusive, timeZoneId, _dayCount),
                new ReportTotals(
                    activeSeconds,
                    idleSeconds,
                    trackedSeconds,
                    calendar.Sum(day => day.KeyPresses),
                    calendar.Sum(day => day.MouseClicks),
                    calendar.Count(day => day.ActiveSeconds > 0)),
                calendar,
                hours,
                trend,
                applications,
                new ReportDataQuality(
                    _sampleCount > 0,
                    _firstSampleAt,
                    _lastSampleAt,
                    _sampleCount,
                    coveredSeconds,
                    requestedSeconds,
                    coverageRatio),
                aiUsage);
        }

        private IReadOnlyList<ReportApplicationSlice> BuildApplications(int applicationLimit)
        {
            var ordered = _applicationTicks
                .Select(pair => new ReportApplicationSlice(pair.Key, pair.Value / TimeSpan.TicksPerSecond))
                .Where(slice => slice.ActiveSeconds > 0)
                .OrderByDescending(slice => slice.ActiveSeconds)
                .ThenBy(slice => slice.Application, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (ordered.Length <= applicationLimit)
            {
                return ordered;
            }

            var result = ordered.Take(applicationLimit).ToList();
            result.Add(new ReportApplicationSlice("Other", ordered.Skip(applicationLimit).Sum(slice => slice.ActiveSeconds)));
            return result;
        }

        private void AddCoverage(long startTicks, long endTicks)
        {
            if (_coverageStartTicks is null)
            {
                _coverageStartTicks = startTicks;
                _coverageEndTicks = endTicks;
                return;
            }

            if (startTicks <= _coverageEndTicks)
            {
                _coverageEndTicks = Math.Max(_coverageEndTicks, endTicks);
                return;
            }

            _coveredTicks += _coverageEndTicks - _coverageStartTicks.Value;
            _coverageStartTicks = startTicks;
            _coverageEndTicks = endTicks;
        }

        private void FinishCoverage()
        {
            if (_coverageStartTicks is null)
            {
                return;
            }

            _coveredTicks += _coverageEndTicks - _coverageStartTicks.Value;
            _coverageStartTicks = null;
            _coverageEndTicks = 0;
        }
    }

    private static List<ActivitySegment> SplitIntoLocalHourSegments(
        long startTicks,
        long endTicks,
        TimeZoneInfo timeZone)
    {
        var segments = new List<ActivitySegment>(2);
        var currentTicks = startTicks;
        while (currentTicks < endTicks)
        {
            var bucket = GetBucket(currentTicks, timeZone);
            var probeEndTicks = Math.Min(currentTicks + TimeSpan.TicksPerHour, endTicks);
            long segmentEndTicks;
            if (GetBucket(probeEndTicks - 1, timeZone) == bucket)
            {
                segmentEndTicks = probeEndTicks;
            }
            else
            {
                var low = currentTicks + 1;
                var high = probeEndTicks - 1;
                while (low < high)
                {
                    var middle = low + ((high - low) / 2);
                    if (GetBucket(middle, timeZone) == bucket)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                segmentEndTicks = low;
            }

            segments.Add(new ActivitySegment(currentTicks, segmentEndTicks, bucket));
            currentTicks = segmentEndTicks;
        }

        return segments;
    }

    private static LocalBucket GetBucket(long utcTicks, TimeZoneInfo timeZone)
    {
        var utc = new DateTimeOffset(utcTicks, TimeSpan.Zero);
        var local = TimeZoneInfo.ConvertTime(utc, timeZone);
        return new LocalBucket(DateOnly.FromDateTime(local.DateTime), (int)local.DayOfWeek, local.Hour);
    }

    private static long ScaleCount(long count, long includedTicks, long originalTicks) =>
        includedTicks == originalTicks
            ? count
            : decimal.ToInt64(decimal.Round((decimal)count * includedTicks / originalTicks, 0, MidpointRounding.AwayFromZero));

    private static long[] AllocateCount(
        long total,
        IReadOnlyList<ActivitySegment> segments,
        long totalTicks)
    {
        var result = new long[segments.Count];
        long allocated = 0;
        for (var index = 0; index < segments.Count - 1; index++)
        {
            var segmentTicks = segments[index].EndTicks - segments[index].StartTicks;
            result[index] = decimal.ToInt64(decimal.Floor((decimal)total * segmentTicks / totalTicks));
            allocated += result[index];
        }

        if (segments.Count > 0)
        {
            result[^1] = total - allocated;
        }

        return result;
    }

    private readonly record struct LocalBucket(DateOnly Date, int DayOfWeek, int Hour);

    private readonly record struct ActivitySegment(long StartTicks, long EndTicks, LocalBucket Bucket);

    private sealed class DayAccumulator
    {
        internal long ActiveTicks { get; set; }
        internal long IdleTicks { get; set; }
        internal long TrackedTicks { get; set; }
        internal long KeyPresses { get; set; }
        internal long MouseClicks { get; set; }
        internal int SampleCount { get; set; }

        internal ReportCalendarCell ToCalendarCell(DateOnly date) => new(
            date,
            ActiveTicks / TimeSpan.TicksPerSecond,
            IdleTicks / TimeSpan.TicksPerSecond,
            TrackedTicks / TimeSpan.TicksPerSecond,
            KeyPresses,
            MouseClicks,
            SampleCount,
            SampleCount > 0);
    }

    private sealed class HourAccumulator
    {
        internal long ActiveTicks { get; set; }
        internal long IdleTicks { get; set; }
        internal long TrackedTicks { get; set; }
        internal HashSet<DateOnly> ObservationDates { get; } = [];

        internal ReportHourCell ToHourCell(int dayOfWeek, int hour) => new(
            dayOfWeek,
            hour,
            MeanSeconds(ActiveTicks),
            MeanSeconds(IdleTicks),
            MeanSeconds(TrackedTicks),
            ObservationDates.Count,
            ObservationDates.Count > 0);

        private long MeanSeconds(long ticks) => ObservationDates.Count == 0
            ? 0
            : checked((long)Math.Round(
                ticks / (double)TimeSpan.TicksPerSecond / ObservationDates.Count,
                MidpointRounding.AwayFromZero));
    }

    private sealed class AiUsageAccumulator
    {
        private readonly Dictionary<string, AiUsageSliceAccumulator> _providers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AiUsageSliceAccumulator> _origins = new(StringComparer.OrdinalIgnoreCase);
        private int _requestCount;
        private int _successfulRequestCount;
        private int _failedRequestCount;
        private long _inputTokens;
        private long _outputTokens;
        private long _totalTokens;
        private long _cachedInputTokens;
        private long _reasoningTokens;
        private long _thinkingTokens;
        private decimal _actualCostUsd;
        private int _actualCostRequestCount;

        internal void Add(AiRequestUsageRecord request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var inputTokens = request.Usage.InputTokens ?? 0;
            var outputTokens = request.Usage.OutputTokens ?? 0;
            var totalTokens = request.Usage.TotalTokens ?? checked(inputTokens + outputTokens);
            var actualCostUsd = request.Usage.ReportedCostUsd;

            checked
            {
                _requestCount++;
                if (request.Success)
                {
                    _successfulRequestCount++;
                }
                else
                {
                    _failedRequestCount++;
                }

                _inputTokens += inputTokens;
                _outputTokens += outputTokens;
                _totalTokens += totalTokens;
                // Providers use different names for the same cache-read input counter; never double-count it.
                _cachedInputTokens += request.Usage.CachedInputTokens
                    ?? request.Usage.CacheReadInputTokens
                    ?? 0;
                _reasoningTokens += request.Usage.ReasoningTokens ?? 0;
                _thinkingTokens += request.Usage.ThinkingTokens ?? 0;
                if (actualCostUsd.HasValue)
                {
                    _actualCostUsd += actualCostUsd.Value;
                    _actualCostRequestCount++;
                }
            }

            AddSlice(_providers, NormalizeLabel(request.Provider), inputTokens, outputTokens, totalTokens, actualCostUsd);
            AddSlice(_origins, NormalizeLabel(request.Origin), inputTokens, outputTokens, totalTokens, actualCostUsd);
        }

        internal AiUsageSummary Build() => _requestCount == 0
            ? AiUsageSummary.Empty
            : new AiUsageSummary(
                _requestCount,
                _successfulRequestCount,
                _failedRequestCount,
                _inputTokens,
                _outputTokens,
                _totalTokens,
                _cachedInputTokens,
                _reasoningTokens,
                _thinkingTokens,
                _actualCostRequestCount == 0 ? null : _actualCostUsd,
                _actualCostRequestCount,
                BuildSlices(_providers),
                BuildSlices(_origins));

        private static void AddSlice(
            IDictionary<string, AiUsageSliceAccumulator> slices,
            string label,
            long inputTokens,
            long outputTokens,
            long totalTokens,
            decimal? actualCostUsd)
        {
            if (!slices.TryGetValue(label, out var slice))
            {
                slice = new AiUsageSliceAccumulator(label);
                slices.Add(label, slice);
            }

            slice.Add(inputTokens, outputTokens, totalTokens, actualCostUsd);
        }

        private static IReadOnlyList<AiUsageSlice> BuildSlices(
            IReadOnlyDictionary<string, AiUsageSliceAccumulator> slices) => slices.Values
            .OrderByDescending(slice => slice.RequestCount)
            .ThenBy(slice => slice.Label, StringComparer.OrdinalIgnoreCase)
            .Select(slice => slice.Build())
            .ToArray();

        private static string NormalizeLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var normalized = value.Trim();
            // Keep SQLite metadata from making the versioned renderer payload invalid.
            return normalized.Length <= 256 ? normalized : normalized[..256];
        }
    }

    private sealed class AiUsageSliceAccumulator(string label)
    {
        private decimal _actualCostUsd;
        private int _actualCostRequestCount;

        internal string Label { get; } = label;

        internal int RequestCount { get; private set; }

        internal long InputTokens { get; private set; }

        internal long OutputTokens { get; private set; }

        internal long TotalTokens { get; private set; }

        internal void Add(
            long inputTokens,
            long outputTokens,
            long totalTokens,
            decimal? actualCostUsd)
        {
            checked
            {
                RequestCount++;
                InputTokens += inputTokens;
                OutputTokens += outputTokens;
                TotalTokens += totalTokens;
                if (actualCostUsd.HasValue)
                {
                    _actualCostUsd += actualCostUsd.Value;
                    _actualCostRequestCount++;
                }
            }
        }

        internal AiUsageSlice Build() => new(
            Label,
            RequestCount,
            InputTokens,
            OutputTokens,
            TotalTokens,
            _actualCostRequestCount == 0 ? null : _actualCostUsd);
    }
}
