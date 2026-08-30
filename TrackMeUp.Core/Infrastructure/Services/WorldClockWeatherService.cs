// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Globalization;
using System.Security;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackMeUp.Application;

namespace TrackMeUp.Services;

internal interface IWorldClockWeatherProvider
{
    string Name { get; }

    string ConfigurationState { get; }

    bool IsConfigured { get; }

    Task<WorldClockWeatherObservation> GetCurrentAsync(
        WorldClockWeatherLocation location,
        CancellationToken cancellationToken);
}

internal sealed record WorldClockWeatherLocation(
    string CityId,
    double Latitude,
    double Longitude);

internal sealed record WorldClockWeatherObservation(
    double TemperatureCelsius,
    string ConditionKey,
    DateTimeOffset ObservedAtUtc);

internal sealed record WorldClockWeatherLoadResult(
    IReadOnlyDictionary<string, WorldClockWeather> Observations,
    WorldClockWeatherStatus Status);

internal readonly record struct WorldClockWeatherCacheEntry(
    WorldClockWeatherObservation Observation,
    DateTimeOffset CachedAtUtc,
    long Generation);

/// <summary>Owns source observations for a strict, timer-backed in-memory retention window.</summary>
internal sealed class WorldClockWeatherCache : IDisposable
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _retention;
    private readonly object _lifecycleGate = new();
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private long _nextGeneration;
    private int _disposed;

    internal WorldClockWeatherCache(TimeProvider timeProvider, TimeSpan retention)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _retention = retention > TimeSpan.Zero && retention != Timeout.InfiniteTimeSpan
            ? retention
            : throw new ArgumentOutOfRangeException(nameof(retention));
    }

    internal int Count => _entries.Count;

    internal bool TryGet(string cityId, out WorldClockWeatherCacheEntry entry)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_entries.TryGetValue(cityId, out var current))
        {
            entry = new WorldClockWeatherCacheEntry(
                current.Observation,
                current.CachedAtUtc,
                current.Generation);
            return true;
        }

        entry = default;
        return false;
    }

    internal void Set(
        string cityId,
        WorldClockWeatherObservation observation,
        DateTimeOffset cachedAtUtc)
    {
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var generation = Interlocked.Increment(ref _nextGeneration);
            var timer = _timeProvider.CreateTimer(
                static state =>
                {
                    var expiration = (ExpirationState)state!;
                    expiration.Owner.Remove(expiration.CityId, expiration.Generation);
                },
                new ExpirationState(this, cityId, generation),
                _retention,
                Timeout.InfiniteTimeSpan);
            var replacement = new Entry(observation, cachedAtUtc, generation, timer);
            Entry? previous = null;
            try
            {
                _entries.AddOrUpdate(
                    cityId,
                    replacement,
                    (_, current) =>
                    {
                        previous = current;
                        return replacement;
                    });
            }
            catch
            {
                timer.Dispose();
                throw;
            }

            previous?.ExpirationTimer.Dispose();
        }
    }

    internal void Remove(string cityId, long generation)
    {
        if (_entries.TryGetValue(cityId, out var current)
            && current.Generation == generation
            && ((ICollection<KeyValuePair<string, Entry>>)_entries).Remove(
                new KeyValuePair<string, Entry>(cityId, current)))
        {
            current.ExpirationTimer.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            foreach (var pair in _entries.ToArray())
            {
                if (((ICollection<KeyValuePair<string, Entry>>)_entries).Remove(pair))
                {
                    pair.Value.ExpirationTimer.Dispose();
                }
            }
        }
    }

    private sealed record Entry(
        WorldClockWeatherObservation Observation,
        DateTimeOffset CachedAtUtc,
        long Generation,
        ITimer ExpirationTimer);

    private sealed record ExpirationState(
        WorldClockWeatherCache Owner,
        string CityId,
        long Generation);
}

/// <summary>Loads bounded, source-backed current weather without making clocks depend on network availability.</summary>
internal sealed class WorldClockWeatherService : IDisposable
{
    internal static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(12);
    internal static readonly TimeSpan MaximumObservationAge = TimeSpan.FromMinutes(45);
    internal static readonly TimeSpan MaximumFutureSkew = TimeSpan.FromMinutes(5);

    private readonly IWorldClockWeatherProvider _provider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly WorldClockWeatherCache _cache;
    private readonly ConcurrentDictionary<string, Lazy<Task<WeatherFetchOutcome>>> _inFlight = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _serviceCancellation = new();
    private readonly CancellationToken _serviceToken;
    private int _disposed;

    internal WorldClockWeatherService(
        IWorldClockWeatherProvider provider,
        TimeProvider? timeProvider = null,
        ILogger? logger = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger.Instance;
        _cache = new WorldClockWeatherCache(_timeProvider, CacheDuration);
        _serviceToken = _serviceCancellation.Token;
    }

    internal int CachedObservationCount => _cache.Count;

    internal async Task<WorldClockWeatherLoadResult> LoadCurrentAsync(
        IReadOnlyList<WorldClockWeatherLocation> locations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locations);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (locations.Count is < 1 or > WorldClockSelection.MaximumClocks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(locations),
                $"Current weather requires between 1 and {WorldClockSelection.MaximumClocks} selected cities.");
        }

        if (!_provider.IsConfigured)
        {
            var configurationUnavailable = _provider.ConfigurationState == "environment-unavailable";
            return new WorldClockWeatherLoadResult(
                new Dictionary<string, WorldClockWeather>(StringComparer.Ordinal),
                new WorldClockWeatherStatus(
                    _provider.Name,
                    configurationUnavailable ? "unavailable" : "disabled",
                    _provider.ConfigurationState,
                    locations.Count,
                    0));
        }

        var outcomes = await Task.WhenAll(locations.Select(location =>
            LoadLocationAsync(location, cancellationToken))).ConfigureAwait(false);
        var observations = outcomes
            .Where(static outcome => outcome.Weather is not null)
            .ToDictionary(
                static outcome => outcome.CityId,
                static outcome => outcome.Weather!,
                StringComparer.Ordinal);
        var reasonCodes = outcomes
            .Where(static outcome => outcome.Weather is null)
            .Select(static outcome => outcome.ReasonCode)
            .Where(static reason => reason is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var state = observations.Count switch
        {
            var count when count == locations.Count => "available",
            > 0 => "partial",
            _ => "unavailable"
        };
        var reasonCode = reasonCodes.Length switch
        {
            0 => null,
            1 => reasonCodes[0],
            _ => "mixed-failures"
        };
        return new WorldClockWeatherLoadResult(
            observations,
            new WorldClockWeatherStatus(
                _provider.Name,
                state,
                reasonCode,
                locations.Count,
                observations.Count));
    }

    internal WorldClockWeatherLoadResult RevalidateForSnapshot(
        WorldClockWeatherLoadResult result,
        DateTimeOffset snapshotInstantUtc)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Observations.Count == 0)
        {
            return result;
        }

        var observations = result.Observations
            .Select(pair => new KeyValuePair<string, WorldClockWeather>(
                pair.Key,
                pair.Value with
                {
                    IsFresh = IsFreshAt(pair.Value.ObservedAtUtc, snapshotInstantUtc)
                }))
            .Where(static pair => pair.Value.IsFresh)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        if (observations.Count == result.Observations.Count)
        {
            return result;
        }

        var state = observations.Count switch
        {
            var count when count == result.Status.RequestedCities => "available",
            > 0 => "partial",
            _ => "unavailable"
        };
        var reasonCode = result.Status.ReasonCode switch
        {
            null => "stale-observation",
            "stale-observation" => "stale-observation",
            _ => "mixed-failures"
        };
        return new WorldClockWeatherLoadResult(
            observations,
            result.Status with
            {
                State = state,
                ReasonCode = reasonCode,
                AvailableObservations = observations.Count
            });
    }

    private async Task<WeatherFetchOutcome> LoadLocationAsync(
        WorldClockWeatherLocation location,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        if (TryGetCached(location.CityId, now, out var cached))
        {
            return new WeatherFetchOutcome(location.CityId, ToContract(cached), null);
        }

        var candidate = new Lazy<Task<WeatherFetchOutcome>>(
            () => FetchLocationAsync(location, _serviceToken),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var active = _inFlight.GetOrAdd(location.CityId, candidate);
        var sharedTask = active.Value;
        _ = sharedTask.ContinueWith(
            _ => RemoveInFlight(location.CityId, active),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return await sharedTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<WeatherFetchOutcome> FetchLocationAsync(
        WorldClockWeatherLocation location,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (TryGetCached(location.CityId, now, out var cached))
        {
            return new WeatherFetchOutcome(location.CityId, ToContract(cached), null);
        }

        try
        {
            // Every waiter shares this optional request; caller cancellation only stops that caller's wait.
            var observation = await _provider.GetCurrentAsync(location, cancellationToken).ConfigureAwait(false);
            now = _timeProvider.GetUtcNow();
            if (!IsFreshAt(observation.ObservedAtUtc, now))
            {
                return new WeatherFetchOutcome(location.CityId, null, "stale-observation");
            }

            _cache.Set(location.CityId, observation, now);
            return new WeatherFetchOutcome(location.CityId, ToContract(observation), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Log only a stable city identifier and exception type; provider URIs can contain the API key.
            _logger.LogWarning(
                "Current weather request failed. CityId={CityId} ExceptionType={ExceptionType}",
                location.CityId,
                exception.GetType().Name);
            return new WeatherFetchOutcome(location.CityId, null, "request-failed");
        }
    }

    private void RemoveInFlight(
        string cityId,
        Lazy<Task<WeatherFetchOutcome>> operation) =>
        ((ICollection<KeyValuePair<string, Lazy<Task<WeatherFetchOutcome>>>>)_inFlight).Remove(
            new KeyValuePair<string, Lazy<Task<WeatherFetchOutcome>>>(cityId, operation));

    private bool TryGetCached(
        string cityId,
        DateTimeOffset now,
        out WorldClockWeatherObservation observation)
    {
        if (_cache.TryGet(cityId, out var entry))
        {
            var cacheAge = now - entry.CachedAtUtc;
            if (cacheAge >= TimeSpan.Zero
                && cacheAge <= CacheDuration
                && IsFreshAt(entry.Observation.ObservedAtUtc, now))
            {
                observation = entry.Observation;
                return true;
            }

            _cache.Remove(cityId, entry.Generation);
        }

        observation = null!;
        return false;
    }

    private static bool IsFreshAt(DateTimeOffset observedAtUtc, DateTimeOffset nowUtc)
    {
        var age = nowUtc.ToUniversalTime() - observedAtUtc.ToUniversalTime();
        return age >= -MaximumFutureSkew && age <= MaximumObservationAge;
    }

    private static WorldClockWeather ToContract(WorldClockWeatherObservation observation) =>
        new(
            observation.TemperatureCelsius,
            observation.ConditionKey,
            observation.ObservedAtUtc.ToUniversalTime(),
            IsFresh: true);

    private sealed record WeatherFetchOutcome(
        string CityId,
        WorldClockWeather? Weather,
        string? ReasonCode);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _serviceCancellation.Cancel();
        _cache.Dispose();
        _serviceCancellation.Dispose();
    }
}

/// <summary>Calls OpenWeather Current Weather with selected catalog coordinates and an environment-only key.</summary>
internal sealed class OpenWeatherCurrentProvider : IWorldClockWeatherProvider
{
    internal const string ApiKeyEnvironmentVariable = "TRACKMEUP_OPENWEATHER_API_KEY";
    internal static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(8);
    internal const int MaximumResponseBytes = 64 * 1024;
    private const string Endpoint = "https://api.openweathermap.org/data/2.5/weather";
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly TimeSpan _requestTimeout;

    private OpenWeatherCurrentProvider(
        HttpClient httpClient,
        string? apiKey,
        string configurationState,
        TimeSpan requestTimeout)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        ConfigurationState = configurationState;
        _requestTimeout = requestTimeout > TimeSpan.Zero && requestTimeout != Timeout.InfiniteTimeSpan
            ? requestTimeout
            : throw new ArgumentOutOfRangeException(nameof(requestTimeout));
    }

    /// <inheritdoc />
    public string Name => "openweather";

    /// <inheritdoc />
    public string ConfigurationState { get; }

    /// <inheritdoc />
    public bool IsConfigured => _apiKey is not null && ConfigurationState == "configured";

    internal static OpenWeatherCurrentProvider CreateFromEnvironment(HttpClient? httpClient = null)
    {
        var (apiKey, state) = ReadEnvironmentConfiguration();
        return new OpenWeatherCurrentProvider(
            httpClient ?? SharedHttpClient,
            apiKey,
            state,
            DefaultRequestTimeout);
    }

    internal static OpenWeatherCurrentProvider CreateForTests(
        HttpClient httpClient,
        string? apiKey,
        TimeSpan? requestTimeout = null) =>
        new(
            httpClient,
            apiKey,
            string.IsNullOrWhiteSpace(apiKey) ? "missing-api-key" : "configured",
            requestTimeout ?? DefaultRequestTimeout);

    /// <inheritdoc />
    public async Task<WorldClockWeatherObservation> GetCurrentAsync(
        WorldClockWeatherLocation location,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("The current-weather provider is not configured.");
        }

        var latitude = location.Latitude.ToString("0.######", CultureInfo.InvariantCulture);
        var longitude = location.Longitude.ToString("0.######", CultureInfo.InvariantCulture);
        var key = Uri.EscapeDataString(_apiKey!);
        var requestUri = new Uri($"{Endpoint}?lat={latitude}&lon={longitude}&appid={key}&units=metric");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);
        var requestToken = timeout.Token;
        // The API key is used only for this direct HTTPS request and is never logged or persisted.
        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                requestToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Current weather provider returned HTTP {(int)response.StatusCode}.");
            }

            var payload = await ReadBoundedContentAsync(response.Content, requestToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(
                payload,
                new JsonDocumentOptions { MaxDepth = 32 });
            return ParseObservation(document.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Hide the key-bearing request URI from the optional service's failure surface.
            throw new InvalidOperationException("Current weather provider request timed out.");
        }
        catch (HttpRequestException)
        {
            // Hide the key-bearing request URI from the optional service's failure surface.
            throw new InvalidOperationException("Current weather provider request failed.");
        }
    }

    private static async Task<byte[]> ReadBoundedContentAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumResponseBytes)
        {
            throw new InvalidDataException("Current weather response exceeds the supported size.");
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var remainingWithOverflowByte = MaximumResponseBytes - checked((int)buffer.Length) + 1;
            var read = await stream.ReadAsync(
                chunk.AsMemory(0, Math.Min(chunk.Length, remainingWithOverflowByte)),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > MaximumResponseBytes)
            {
                throw new InvalidDataException("Current weather response exceeds the supported size.");
            }

            buffer.Write(chunk, 0, read);
        }
    }

    internal static WorldClockWeatherObservation ParseObservation(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("main", out var main)
            || main.ValueKind != JsonValueKind.Object
            || !main.TryGetProperty("temp", out var temperatureElement)
            || !temperatureElement.TryGetDouble(out var temperature)
            || !double.IsFinite(temperature)
            || temperature is < -150d or > 100d)
        {
            throw new InvalidDataException("Current weather response has an invalid metric temperature.");
        }

        if (!root.TryGetProperty("dt", out var timestampElement)
            || !timestampElement.TryGetInt64(out var unixTimestamp))
        {
            throw new InvalidDataException("Current weather response has no observation timestamp.");
        }

        DateTimeOffset observedAtUtc;
        try
        {
            observedAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("Current weather response has an invalid observation timestamp.", exception);
        }

        if (!root.TryGetProperty("weather", out var weather)
            || weather.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Current weather response has no condition identifiers.");
        }

        var conditionIds = new List<int>();
        foreach (var condition in weather.EnumerateArray())
        {
            if (condition.ValueKind != JsonValueKind.Object
                || !condition.TryGetProperty("id", out var identifier)
                || identifier.ValueKind != JsonValueKind.Number
                || !identifier.TryGetInt32(out var conditionId))
            {
                throw new InvalidDataException("Current weather response contains an invalid condition identifier.");
            }

            conditionIds.Add(conditionId);
        }

        if (conditionIds.Count == 0)
        {
            throw new InvalidDataException("Current weather response has no valid condition identifiers.");
        }

        return new WorldClockWeatherObservation(
            temperature,
            MapCondition(conditionIds),
            observedAtUtc);
    }

    internal static string MapCondition(IReadOnlyList<int> conditionIds)
    {
        ArgumentNullException.ThrowIfNull(conditionIds);
        if (conditionIds.Count == 0)
        {
            throw new ArgumentException("At least one weather condition identifier is required.", nameof(conditionIds));
        }

        if (conditionIds.Any(static id => !IsSupportedCondition(id)))
        {
            throw new InvalidDataException("Current weather response contains an unsupported condition identifier.");
        }

        if (conditionIds.Any(static id => IsLightning(id)))
        {
            return "lightning";
        }

        var hasRain = conditionIds.Any(static id => IsRain(id));
        var hasSnow = conditionIds.Any(static id => IsSnow(id));
        var hasMixedPrecipitation = conditionIds.Any(static id => IsMixedPrecipitation(id));
        if ((hasRain && hasSnow) || hasMixedPrecipitation)
        {
            return "mixed-precipitation";
        }

        if (hasSnow)
        {
            return "snow";
        }

        if (hasRain)
        {
            return "rain";
        }

        if (conditionIds.Any(static id => id is 701 or 741))
        {
            return "fog";
        }

        if (conditionIds.Any(static id => id is >= 801 and <= 804))
        {
            return "cloudy";
        }

        if (conditionIds.All(static id => id == 800))
        {
            return "clear";
        }

        throw new InvalidDataException("Current weather response contains an unsupported condition combination.");
    }

    private static bool IsSupportedCondition(int id) =>
        IsLightning(id)
        || IsRain(id)
        || IsSnow(id)
        || IsMixedPrecipitation(id)
        || id is 701 or 741 or >= 800 and <= 804;

    private static bool IsLightning(int id) => id is
        200 or 201 or 202 or 210 or 211 or 212 or 221 or 230 or 231 or 232;

    private static bool IsRain(int id) => id is
        300 or 301 or 302 or 310 or 311 or 312 or 313 or 314 or 321 or
        500 or 501 or 502 or 503 or 504 or 511 or 520 or 521 or 522 or 531;

    private static bool IsSnow(int id) => id is 600 or 601 or 602 or 620 or 621 or 622;

    private static bool IsMixedPrecipitation(int id) => id is 611 or 612 or 613 or 615 or 616;

    private static (string? ApiKey, string State) ReadEnvironmentConfiguration()
    {
        try
        {
            foreach (var target in new[]
                     {
                         EnvironmentVariableTarget.Process,
                         EnvironmentVariableTarget.User,
                         EnvironmentVariableTarget.Machine
                     })
            {
                var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable, target);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    return (apiKey, "configured");
                }
            }

            return (null, "missing-api-key");
        }
        catch (SecurityException)
        {
            // Optional weather stays disabled when Windows denies environment access; clocks remain local.
            return (null, "environment-unavailable");
        }
        catch (UnauthorizedAccessException)
        {
            // Optional weather stays disabled when Windows denies environment access; clocks remain local.
            return (null, "environment-unavailable");
        }
    }
}
