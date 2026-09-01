// SPDX-License-Identifier: MIT

using TrackMeUp.Services;

namespace TrackMeUp.Application;

/// <summary>
/// Coordinates world-clock queries and mutations while the application facade owns serialization.
/// </summary>
internal sealed class WorldClockApplicationService : IDisposable
{
    private readonly WorldClockService _worldClocks;
    private readonly SettingsSnapshot _settingsSnapshot;
    private readonly Action<string, string> _setApiKey;
    private readonly Action<AppSettings> _persistSettings;

    internal WorldClockApplicationService(
        WorldClockService worldClocks,
        SettingsSnapshot settingsSnapshot,
        Action<string, string> setApiKey,
        Action<AppSettings> persistSettings)
    {
        _worldClocks = worldClocks ?? throw new ArgumentNullException(nameof(worldClocks));
        _settingsSnapshot = settingsSnapshot ?? throw new ArgumentNullException(nameof(settingsSnapshot));
        _setApiKey = setApiKey ?? throw new ArgumentNullException(nameof(setApiKey));
        _persistSettings = persistSettings ?? throw new ArgumentNullException(nameof(persistSettings));
    }

    internal OperationResult<WorldClockCityCatalog> GetCatalog() =>
        OperationResult<WorldClockCityCatalog>.Success(
            "world_clocks.catalog.loaded",
            "WorldClocksCatalogLoaded",
            _worldClocks.GetCatalog());

    internal async Task<OperationResult<WorldClockSnapshot>> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsSnapshot.Value;
        // Optional provider failures are represented by WorldClockService in the returned weather status;
        // local clock projection remains available, while cancellation and unexpected failures propagate.
        var snapshot = await _worldClocks.BuildCurrentSnapshotAsync(
            settings.WorldClockCityIds,
            settings.WorldClockWeatherEnabled,
            cancellationToken).ConfigureAwait(false);
        return OperationResult<WorldClockSnapshot>.Success(
            "world_clocks.loaded",
            "WorldClocksLoaded",
            snapshot);
    }

    internal OperationResult<WorldClockSnapshot> Convert(WorldClockConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var snapshot = _worldClocks.BuildSnapshotForLocalTime(
                _settingsSnapshot.Value.WorldClockCityIds,
                request);
            return OperationResult<WorldClockSnapshot>.Success(
                "world_clocks.converted",
                "WorldClocksConverted",
                snapshot);
        }
        catch (WorldClockConversionException exception)
        {
            var field = exception.ValidationCode is "reference_not_selected" or "not_found"
                ? "referenceCityId"
                : "referenceLocalTime";
            return OperationResult<WorldClockSnapshot>.Failure(
                exception.Code,
                exception.MessageKey,
                new ValidationIssue(field, exception.ValidationCode, exception.MessageKey));
        }
    }

    internal string NormalizeAndValidateCityId(string cityId)
    {
        var normalizedId = cityId?.Trim().ToLowerInvariant() ?? string.Empty;
        _worldClocks.ValidateCityId(normalizedId);
        return normalizedId;
    }

    internal OperationResult<WorldClockSelectionState> AddValidated(string normalizedId)
    {
        var selection = WorldClockSelection.NormalizePersisted(_settingsSnapshot.Value.WorldClockCityIds).ToList();
        if (selection.Contains(normalizedId, StringComparer.Ordinal))
        {
            return OperationResult<WorldClockSelectionState>.Failure(
                "world_clocks.duplicate",
                "WorldClocksDuplicate",
                new ValidationIssue("cityId", "duplicate", "WorldClocksDuplicate"));
        }

        if (selection.Count >= WorldClockSelection.MaximumClocks)
        {
            return OperationResult<WorldClockSelectionState>.Failure(
                "world_clocks.maximum_reached",
                "WorldClocksMaximumReached",
                new ValidationIssue("cityId", "maximum_reached", "WorldClocksMaximumReached"));
        }

        selection.Add(normalizedId);
        _persistSettings(_settingsSnapshot.Value with { WorldClockCityIds = selection });
        return OperationResult<WorldClockSelectionState>.Success(
            "world_clocks.added",
            "WorldClocksAdded",
            new WorldClockSelectionState(selection.ToArray(), WorldClockSelection.MaximumClocks));
    }

    internal OperationResult<WorldClockSelectionState> Remove(string cityId)
    {
        var normalizedId = cityId?.Trim().ToLowerInvariant() ?? string.Empty;
        var selection = WorldClockSelection.NormalizePersisted(_settingsSnapshot.Value.WorldClockCityIds).ToList();
        if (!selection.Remove(normalizedId))
        {
            return OperationResult<WorldClockSelectionState>.Failure(
                "world_clocks.not_found",
                "WorldClocksNotFound",
                new ValidationIssue("cityId", "not_found", "WorldClocksNotFound"));
        }

        _persistSettings(_settingsSnapshot.Value with { WorldClockCityIds = selection });
        return OperationResult<WorldClockSelectionState>.Success(
            "world_clocks.removed",
            "WorldClocksRemoved",
            new WorldClockSelectionState(selection.ToArray(), WorldClockSelection.MaximumClocks));
    }

    internal async Task<OperationResult<string>> SetWeatherKeyAsync(
        string secret,
        CancellationToken cancellationToken)
    {
        var secretValue = secret ?? string.Empty;
        if (!OpenWeatherCurrentProvider.IsPlausibleApiKey(secretValue))
        {
            secretValue = string.Empty;
            return OperationResult<string>.Failure(
                "world_clocks.weather.key.invalid",
                "WorldClockWeatherKeyInvalid",
                new ValidationIssue("secret", "invalid", "WorldClockWeatherKeyInvalid"));
        }

        try
        {
            var validation = await _worldClocks.ValidateWeatherApiKeyAsync(
                secretValue,
                cancellationToken).ConfigureAwait(false);
            if (validation == WorldClockWeatherApiKeyValidation.Rejected)
            {
                return OperationResult<string>.Failure(
                    "world_clocks.weather.key.rejected",
                    "WorldClockWeatherKeyRejected",
                    new ValidationIssue("secret", "rejected", "WorldClockWeatherKeyRejected"));
            }

            if (validation == WorldClockWeatherApiKeyValidation.Unavailable)
            {
                return OperationResult<string>.Failure(
                    "world_clocks.weather.key.validation_unavailable",
                    "WorldClockWeatherKeyValidationUnavailable");
            }

            if (validation is not (WorldClockWeatherApiKeyValidation.Accepted
                or WorldClockWeatherApiKeyValidation.RateLimited))
            {
                throw new InvalidDataException($"Unsupported weather-key validation result '{validation}'.");
            }

            // Only a provider-accepted key reaches the Windows environment store; it is never persisted or logged elsewhere.
            // Environment-write failures propagate because there is no safe fallback that could claim the key was stored.
            _setApiKey(OpenWeatherCurrentProvider.ApiKeyEnvironmentVariable, secretValue);
            _worldClocks.InvalidateCurrentWeatherConfiguration();
            return OperationResult<string>.Success(
                validation == WorldClockWeatherApiKeyValidation.RateLimited
                    ? "world_clocks.weather.key.stored_rate_limited"
                    : "world_clocks.weather.key.stored",
                "WorldClockWeatherKeyStored",
                OpenWeatherCurrentProvider.ApiKeyEnvironmentVariable);
        }
        finally
        {
            secretValue = string.Empty;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _worldClocks.Dispose();
}
