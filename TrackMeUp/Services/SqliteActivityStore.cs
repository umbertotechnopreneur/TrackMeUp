using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TrackMeUp.Application;

namespace TrackMeUp.Services;

/// <summary>Persists activity history and privacy-safe AI telemetry in the single local SQLite store.</summary>
internal sealed class SqliteActivityStore
{
    internal const string DatabaseFileName = "activity.sqlite3";
    private const int SchemaVersion = 5;
    private const long FixedEstimatedRowBytes = 96;
    private static readonly SchemaColumn[] ExpectedActivityColumns =
    [
        new("id", "INTEGER", false, 1),
        new("timestamp_utc_ticks", "INTEGER", true, 0),
        new("start_utc_ticks", "INTEGER", true, 0),
        new("timestamp_offset_minutes", "INTEGER", true, 0),
        new("duration_seconds", "INTEGER", true, 0),
        new("state", "TEXT", true, 0),
        new("process_name", "TEXT", true, 0),
        new("application", "TEXT", true, 0),
        new("context", "TEXT", true, 0),
        new("window_title", "TEXT", true, 0),
        new("installation_id", "TEXT", true, 0),
        new("key_presses", "INTEGER", true, 0),
        new("mouse_clicks", "INTEGER", true, 0),
        new("attributes_json", "TEXT", false, 0),
        new("estimated_bytes", "INTEGER", true, 0)
    ];

    private static readonly string[] ExpectedAiRequestUsageColumns =
    [
        "attempt_id", "correlation_id", "occurred_utc_ticks", "completed_utc_ticks", "origin", "request_kind",
        "provider", "endpoint_host", "requested_model", "returned_model", "provider_response_id", "provider_request_id",
        "http_status", "elapsed_ms", "provider_processing_ms", "image_count", "prompt_characters", "max_output_tokens",
        "input_tokens", "output_tokens", "total_tokens", "cached_input_tokens", "cache_write_tokens",
        "cache_creation_input_tokens", "cache_read_input_tokens", "reasoning_tokens", "thinking_tokens",
        "reported_cost_microusd", "reported_upstream_cost_microusd", "cost_source", "finish_reason", "success", "failure_code"
    ];

    private static readonly string[] ExpectedAiAnalysisResultColumns =
    [
        "correlation_id", "attempt_id", "timestamp_utc_ticks", "snapshot_utc_ticks", "application", "context", "summary",
        "installation_id", "origin", "informational_schedule", "screenshot_paths", "image_count"
    ];

    private static readonly SchemaColumn[] ExpectedScreenshotTextSnapshotColumns =
    [
        new("artifact_identity", "TEXT", true, 1),
        new("capture_id", "TEXT", true, 0),
        new("source_path", "TEXT", true, 0),
        new("extracted_utc_ticks", "INTEGER", true, 0),
        new("snapshot_json", "TEXT", true, 0),
        new("updated_utc_ticks", "INTEGER", true, 0)
    ];

    private static readonly SchemaColumn[] ExpectedAiModelPricingColumns =
    [
        new("provider", "TEXT", true, 1),
        new("model", "TEXT", true, 2),
        new("service_tier", "TEXT", true, 3),
        new("context_window", "TEXT", true, 4),
        new("currency", "TEXT", true, 0),
        new("input_microusd_per_million", "INTEGER", true, 0),
        new("cached_input_microusd_per_million", "INTEGER", false, 0),
        new("cache_write_microusd_per_million", "INTEGER", false, 0),
        new("output_microusd_per_million", "INTEGER", true, 0),
        new("source_url", "TEXT", true, 0),
        new("source_retrieved_utc_ticks", "INTEGER", true, 0)
    ];

    private static readonly HashSet<string> ExpectedActivityIndexes =
    [
        "ix_activity_samples_start",
        "ix_activity_samples_timestamp"
    ];

    private static readonly HashSet<string> ExpectedAiRequestUsageIndexes =
    [
        "sqlite_autoindex_ai_request_usage_1",
        "ix_ai_request_usage_occurred",
        "ix_ai_request_usage_correlation"
    ];

    private static readonly HashSet<string> ExpectedAiAnalysisResultIndexes =
    [
        "sqlite_autoindex_ai_analysis_results_1",
        "sqlite_autoindex_ai_analysis_results_2",
        "ix_ai_analysis_results_timestamp"
    ];

    private static readonly HashSet<string> ExpectedScreenshotTextSnapshotIndexes =
    [
        "sqlite_autoindex_screenshot_text_snapshots_1",
        "ix_screenshot_text_snapshots_capture"
    ];

    private static readonly HashSet<string> ExpectedAiModelPricingIndexes =
    [
        "sqlite_autoindex_ai_model_pricing_1"
    ];

    private static readonly HashSet<string> ExpectedVersion3ApplicationSchemaObjects =
    [
        "activity_samples",
        "ix_activity_samples_start",
        "ix_activity_samples_timestamp",
        "ai_request_usage",
        "ix_ai_request_usage_occurred",
        "ix_ai_request_usage_correlation",
        "ai_analysis_results",
        "ix_ai_analysis_results_timestamp",
        "ai_analysis_search",
        "ai_analysis_search_data",
        "ai_analysis_search_idx",
        "ai_analysis_search_content",
        "ai_analysis_search_docsize",
        "ai_analysis_search_config"
    ];

    private static readonly HashSet<string> ExpectedVersion4ApplicationSchemaObjects =
    [
        .. ExpectedVersion3ApplicationSchemaObjects,
        "screenshot_text_snapshots",
        "ix_screenshot_text_snapshots_capture"
    ];

    private static readonly HashSet<string> ExpectedApplicationSchemaObjects =
    [
        .. ExpectedVersion4ApplicationSchemaObjects,
        "ai_model_pricing"
    ];

    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>Initializes and validates the only supported activity-history schema.</summary>
    internal SqliteActivityStore(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
        var databaseExisted = File.Exists(_databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();

        InitializeSchema(databaseExisted);
    }

    /// <summary>Gets the absolute path of the SQLite activity database.</summary>
    internal string DatabasePath => _databasePath;

    /// <summary>Appends one complete activity sample in a single SQLite statement.</summary>
    internal void Append(ActivitySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (sample.DurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sample), "Activity duration must be positive.");
        }

        var attributesJson = sample.Attributes is null ? null : JsonSerializer.Serialize(sample.Attributes, _json);
        var timestampUtcTicks = sample.Timestamp.UtcDateTime.Ticks;
        var durationTicks = checked((long)sample.DurationSeconds * TimeSpan.TicksPerSecond);
        var estimatedBytes = EstimateBytes(sample, attributesJson);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO activity_samples (
                timestamp_utc_ticks, start_utc_ticks, timestamp_offset_minutes, duration_seconds, state, process_name,
                application, context, window_title, installation_id, key_presses, mouse_clicks, attributes_json, estimated_bytes)
            VALUES (
                $timestamp, $start, $offset, $duration, $state, $process,
                $application, $context, $windowTitle, $installation, $keyPresses, $mouseClicks, $attributes, $estimatedBytes);
            """;
        command.Parameters.AddWithValue("$timestamp", timestampUtcTicks);
        command.Parameters.AddWithValue("$start", checked(timestampUtcTicks - durationTicks));
        command.Parameters.AddWithValue("$offset", checked((int)sample.Timestamp.Offset.TotalMinutes));
        command.Parameters.AddWithValue("$duration", sample.DurationSeconds);
        command.Parameters.AddWithValue("$state", sample.State);
        command.Parameters.AddWithValue("$process", sample.ProcessName);
        command.Parameters.AddWithValue("$application", sample.Application);
        command.Parameters.AddWithValue("$context", sample.Context);
        command.Parameters.AddWithValue("$windowTitle", sample.WindowTitle);
        command.Parameters.AddWithValue("$installation", sample.InstallationId);
        command.Parameters.AddWithValue("$keyPresses", sample.KeyPresses);
        command.Parameters.AddWithValue("$mouseClicks", sample.MouseClicks);
        command.Parameters.AddWithValue("$attributes", attributesJson is null ? DBNull.Value : attributesJson);
        command.Parameters.AddWithValue("$estimatedBytes", estimatedBytes);
        command.ExecuteNonQuery();
    }

    /// <summary>Upserts the complete local OCR snapshot and optional AI refinement for one owned screenshot artifact.</summary>
    internal void UpsertScreenshotTextSnapshot(
        string artifactIdentity,
        string captureId,
        ScreenshotTextSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(artifactIdentity) || string.IsNullOrWhiteSpace(captureId))
        {
            throw new ArgumentException("Screenshot text persistence requires artifact and capture identifiers.");
        }

        ArgumentNullException.ThrowIfNull(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.SourceScreenshotPath)
            || string.IsNullOrWhiteSpace(snapshot.Ocr.Engine)
            || snapshot.Ocr.Lines is null)
        {
            throw new ArgumentException("Screenshot text snapshot is incomplete.", nameof(snapshot));
        }

        var payload = JsonSerializer.Serialize(snapshot, _json);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO screenshot_text_snapshots (
                artifact_identity, capture_id, source_path, extracted_utc_ticks, snapshot_json, updated_utc_ticks)
            VALUES ($identity, $captureId, $sourcePath, $extractedAt, $snapshot, $updatedAt)
            ON CONFLICT(artifact_identity) DO UPDATE SET
                capture_id = excluded.capture_id,
                source_path = excluded.source_path,
                extracted_utc_ticks = excluded.extracted_utc_ticks,
                snapshot_json = excluded.snapshot_json,
                updated_utc_ticks = excluded.updated_utc_ticks;
            """;
        command.Parameters.AddWithValue("$identity", artifactIdentity);
        command.Parameters.AddWithValue("$captureId", captureId);
        command.Parameters.AddWithValue("$sourcePath", snapshot.SourceScreenshotPath);
        command.Parameters.AddWithValue("$extractedAt", snapshot.Ocr.ExtractedAt.UtcDateTime.Ticks);
        command.Parameters.AddWithValue("$snapshot", payload);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.UtcDateTime.Ticks);
        command.ExecuteNonQuery();
    }

    /// <summary>Loads the persisted text snapshot for one screenshot artifact identity.</summary>
    internal ScreenshotTextSnapshot? LoadScreenshotTextSnapshot(string artifactIdentity)
    {
        if (string.IsNullOrWhiteSpace(artifactIdentity))
        {
            throw new ArgumentException("Screenshot artifact identity is required.", nameof(artifactIdentity));
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT snapshot_json FROM screenshot_text_snapshots WHERE artifact_identity = $identity;";
        command.Parameters.AddWithValue("$identity", artifactIdentity);
        var payload = command.ExecuteScalar() as string;
        return payload is null
            ? null
            : JsonSerializer.Deserialize<ScreenshotTextSnapshot>(payload, _json)
                ?? throw new InvalidDataException("Persisted screenshot text snapshot is invalid.");
    }

    /// <summary>Visits every persisted screenshot text snapshot for deterministic search-index rebuilds.</summary>
    internal void VisitScreenshotTextSnapshots(
        CancellationToken cancellationToken,
        Action<string, string, ScreenshotTextSnapshot> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT artifact_identity, capture_id, snapshot_json
            FROM screenshot_text_snapshots
            ORDER BY artifact_identity;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = JsonSerializer.Deserialize<ScreenshotTextSnapshot>(reader.GetString(2), _json)
                ?? throw new InvalidDataException("Persisted screenshot text snapshot is invalid.");
            visitor(reader.GetString(0), reader.GetString(1), snapshot);
        }
    }

    /// <summary>Deletes the text snapshot associated with one screenshot artifact identity.</summary>
    internal int DeleteScreenshotTextSnapshot(string artifactIdentity)
    {
        if (string.IsNullOrWhiteSpace(artifactIdentity))
        {
            throw new ArgumentException("Screenshot artifact identity is required.", nameof(artifactIdentity));
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM screenshot_text_snapshots WHERE artifact_identity = $identity;";
        command.Parameters.AddWithValue("$identity", artifactIdentity);
        return command.ExecuteNonQuery();
    }

    /// <summary>Appends one standalone AI provider attempt without persisting provider error text or request content.</summary>
    internal void AppendStandaloneAiRequest(AiRequestUsageRecord request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        InsertAiRequest(connection, transaction, request);
        transaction.Commit();
    }

    /// <summary>Appends one successful AI attempt, result, and search index entry in one SQLite transaction.</summary>
    internal void AppendSuccessfulAiRequestAndAnalysis(AiRequestUsageRecord request, AiAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(analysis);
        if (!request.Success || string.IsNullOrWhiteSpace(analysis.CorrelationId)
            || !string.Equals(request.CorrelationId, analysis.CorrelationId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Successful AI request and analysis must share a non-empty correlation identifier.");
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        InsertAiRequest(connection, transaction, request);
        InsertAiAnalysisResult(connection, transaction, request, analysis);
        transaction.Commit();
    }

    /// <summary>Lists sanitized AI request telemetry inside a half-open UTC interval for aggregate reporting.</summary>
    internal IReadOnlyList<AiRequestUsageRecord> ListAiRequestUsage(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        var results = new List<AiRequestUsageRecord>();
        VisitAiUsage(fromUtc, toUtc, CancellationToken.None, results.Add);
        return results;
    }

    /// <summary>Replaces every cached AI pricing row for one provider inside a single transaction.</summary>
    internal void ReplaceAiModelPricing(string provider, IReadOnlyList<AiModelPricing> prices)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("AI pricing provider is required.", nameof(provider));
        }

        ArgumentNullException.ThrowIfNull(prices);
        if (prices.Count == 0)
        {
            throw new ArgumentException("AI pricing refresh must contain at least one row.", nameof(prices));
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        foreach (var price in prices)
        {
            ValidateAiModelPricing(normalizedProvider, price);
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM ai_model_pricing WHERE provider = $provider;";
        delete.Parameters.AddWithValue("$provider", normalizedProvider);
        delete.ExecuteNonQuery();

        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO ai_model_pricing (
                provider, model, service_tier, context_window, currency, input_microusd_per_million,
                cached_input_microusd_per_million, cache_write_microusd_per_million,
                output_microusd_per_million, source_url, source_retrieved_utc_ticks)
            VALUES (
                $provider, $model, $serviceTier, $contextWindow, $currency, $input,
                $cachedInput, $cacheWrite, $output, $sourceUrl, $sourceRetrievedAt);
            """;
        var providerParameter = insert.Parameters.Add("$provider", SqliteType.Text);
        var modelParameter = insert.Parameters.Add("$model", SqliteType.Text);
        var serviceTierParameter = insert.Parameters.Add("$serviceTier", SqliteType.Text);
        var contextWindowParameter = insert.Parameters.Add("$contextWindow", SqliteType.Text);
        var currencyParameter = insert.Parameters.Add("$currency", SqliteType.Text);
        var inputParameter = insert.Parameters.Add("$input", SqliteType.Integer);
        var cachedInputParameter = insert.Parameters.Add("$cachedInput", SqliteType.Integer);
        var cacheWriteParameter = insert.Parameters.Add("$cacheWrite", SqliteType.Integer);
        var outputParameter = insert.Parameters.Add("$output", SqliteType.Integer);
        var sourceUrlParameter = insert.Parameters.Add("$sourceUrl", SqliteType.Text);
        var sourceRetrievedAtParameter = insert.Parameters.Add("$sourceRetrievedAt", SqliteType.Integer);

        foreach (var price in prices)
        {
            providerParameter.Value = normalizedProvider;
            modelParameter.Value = price.Model.Trim();
            serviceTierParameter.Value = price.ServiceTier.Trim().ToLowerInvariant();
            contextWindowParameter.Value = price.ContextWindow.Trim().ToLowerInvariant();
            currencyParameter.Value = price.Currency.Trim().ToLowerInvariant();
            inputParameter.Value = ToMicroUsd(price.InputUsdPerMillionTokens)!.Value;
            cachedInputParameter.Value = ToDbValue(ToMicroUsd(price.CachedInputUsdPerMillionTokens));
            cacheWriteParameter.Value = ToDbValue(ToMicroUsd(price.CacheWriteUsdPerMillionTokens));
            outputParameter.Value = ToMicroUsd(price.OutputUsdPerMillionTokens)!.Value;
            sourceUrlParameter.Value = price.SourceUrl.Trim();
            sourceRetrievedAtParameter.Value = price.SourceRetrievedAt.UtcDateTime.Ticks;
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>Lists the cached AI model pricing rows for one provider.</summary>
    internal IReadOnlyList<AiModelPricing> ListAiModelPricing(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("AI pricing provider is required.", nameof(provider));
        }

        var results = new List<AiModelPricing>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT provider, model, service_tier, context_window, currency, input_microusd_per_million,
                   cached_input_microusd_per_million, cache_write_microusd_per_million,
                   output_microusd_per_million, source_url, source_retrieved_utc_ticks
            FROM ai_model_pricing
            WHERE provider = $provider
            ORDER BY provider, model, service_tier, context_window;
            """;
        command.Parameters.AddWithValue("$provider", provider.Trim().ToLowerInvariant());
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadAiModelPricing(reader));
        }

        return results;
    }

    /// <summary>Gets the newest retrieved-at timestamp for one cached AI pricing provider.</summary>
    internal DateTimeOffset? GetLatestAiModelPricingRetrievedAt(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("AI pricing provider is required.", nameof(provider));
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(source_retrieved_utc_ticks) FROM ai_model_pricing WHERE provider = $provider;";
        command.Parameters.AddWithValue("$provider", provider.Trim().ToLowerInvariant());
        return ReadScalarNullableLong(command) is { } ticks ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;
    }

    /// <summary>Counts successful analysis results in a half-open UTC interval.</summary>
    internal int CountAiAnalysisResults(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM ai_analysis_results
            WHERE timestamp_utc_ticks >= $from
              AND timestamp_utc_ticks < $to;
            """;
        command.Parameters.AddWithValue("$from", fromUtc.UtcDateTime.Ticks);
        command.Parameters.AddWithValue("$to", toUtc.UtcDateTime.Ticks);
        return checked(Convert.ToInt32(command.ExecuteScalar()));
    }

    /// <summary>Loads the latest AI analysis from the current SQLite store.</summary>
    internal AiAnalysis? LoadLatestAiAnalysis()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT correlation_id, timestamp_utc_ticks, application, context, summary, installation_id,
                   screenshot_paths, origin, informational_schedule
            FROM ai_analysis_results
            ORDER BY timestamp_utc_ticks DESC, correlation_id DESC
            LIMIT 1;
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return ReadAiAnalysis(reader);
    }

    /// <summary>Visits every persisted successful AI analysis for search-index rebuilds.</summary>
    internal void VisitAllAiAnalyses(CancellationToken cancellationToken, Action<AiAnalysis> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT correlation_id, timestamp_utc_ticks, application, context, summary, installation_id,
                   screenshot_paths, origin, informational_schedule
            FROM ai_analysis_results
            ORDER BY timestamp_utc_ticks, correlation_id;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            visitor(ReadAiAnalysis(reader));
        }
    }

    /// <summary>Loads the newest successful analysis that references each requested screenshot path.</summary>
    internal IReadOnlyDictionary<string, AiAnalysis> LoadLatestAiAnalysesForScreenshots(IEnumerable<string> screenshotPaths)
    {
        ArgumentNullException.ThrowIfNull(screenshotPaths);
        var requestedPaths = screenshotPaths
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var analyses = new Dictionary<string, AiAnalysis>(StringComparer.OrdinalIgnoreCase);
        if (requestedPaths.Count == 0)
        {
            return analyses;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT correlation_id, timestamp_utc_ticks, application, context, summary, installation_id,
                   screenshot_paths, origin, informational_schedule
            FROM ai_analysis_results
            WHERE screenshot_paths IS NOT NULL
            ORDER BY timestamp_utc_ticks DESC, correlation_id DESC;
            """;
        using var reader = command.ExecuteReader();
        while (analyses.Count < requestedPaths.Count && reader.Read())
        {
            var referencedPaths = EnumerateScreenshotPaths(ReadNullableString(reader, 6))
                .Select(Path.GetFullPath)
                .Where(requestedPaths.Contains)
                .Where(path => !analyses.ContainsKey(path))
                .ToArray();
            if (referencedPaths.Length == 0)
            {
                continue;
            }

            var analysis = ReadAiAnalysis(reader);
            foreach (var referencedPath in referencedPaths)
            {
                analyses.Add(referencedPath, analysis);
            }
        }

        return analyses;
    }

    /// <summary>Deletes snapshot-analysis records that reference one retained screenshot capture.</summary>
    internal int DeleteAiAnalysesReferencingScreenshot(string screenshotPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(screenshotPath);
        var normalizedPath = Path.GetFullPath(screenshotPath);
        var correlationIds = new List<string>();

        using var connection = OpenConnection();
        using (var select = connection.CreateCommand())
        {
            select.CommandText = "SELECT correlation_id, screenshot_paths FROM ai_analysis_results WHERE screenshot_paths IS NOT NULL;";
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                var paths = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (ContainsScreenshotPath(paths, normalizedPath))
                {
                    correlationIds.Add(reader.GetString(0));
                }
            }
        }

        if (correlationIds.Count == 0)
        {
            return 0;
        }

        using var transaction = connection.BeginTransaction();
        using var deleteSearch = connection.CreateCommand();
        deleteSearch.Transaction = transaction;
        deleteSearch.CommandText = "DELETE FROM ai_analysis_search WHERE correlation_id = $correlationId;";
        using var deleteResults = connection.CreateCommand();
        deleteResults.Transaction = transaction;
        deleteResults.CommandText = "DELETE FROM ai_analysis_results WHERE correlation_id = $correlationId;";
        foreach (var correlationId in correlationIds)
        {
            deleteSearch.Parameters.Clear();
            deleteSearch.Parameters.AddWithValue("$correlationId", correlationId);
            deleteSearch.ExecuteNonQuery();
            deleteResults.Parameters.Clear();
            deleteResults.Parameters.AddWithValue("$correlationId", correlationId);
            deleteResults.ExecuteNonQuery();
        }

        transaction.Commit();
        return correlationIds.Count;
    }

    private static bool ContainsScreenshotPath(string? screenshotPaths, string normalizedPath)
    {
        return EnumerateScreenshotPaths(screenshotPaths)
            .Any(path => string.Equals(Path.GetFullPath(path), normalizedPath, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static IEnumerable<string> EnumerateScreenshotPaths(string? screenshotPaths) =>
        screenshotPaths?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? Array.Empty<string>();

    /// <summary>Streams activity and AI usage from one SQLite read transaction and therefore one database snapshot.</summary>
    internal void VisitReportData(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken,
        Action<ReportSourceSample> activityVisitor,
        Action<AiRequestUsageRecord> aiUsageVisitor)
    {
        ArgumentNullException.ThrowIfNull(activityVisitor);
        ArgumentNullException.ThrowIfNull(aiUsageVisitor);
        ValidateInterval(fromUtc, toUtc, "report");

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        VisitReportOverlapping(connection, transaction, fromUtc, toUtc, cancellationToken, activityVisitor);
        VisitAiUsage(connection, transaction, fromUtc, toUtc, cancellationToken, aiUsageVisitor);
        transaction.Commit();
    }

    /// <summary>Streams only the activity fields required for aggregate reports.</summary>
    internal void VisitReportOverlapping(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken,
        Action<ReportSourceSample> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ValidateInterval(fromUtc, toUtc, "activity");
        using var connection = OpenConnection();
        VisitReportOverlapping(connection, null, fromUtc, toUtc, cancellationToken, visitor);
    }

    private static void VisitReportOverlapping(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken,
        Action<ReportSourceSample> visitor)
    {
        using var command = connection.CreateCommand();
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        command.CommandText = """
            SELECT timestamp_utc_ticks, timestamp_offset_minutes, duration_seconds, state, application, key_presses, mouse_clicks
            FROM activity_samples
            WHERE timestamp_utc_ticks > $from AND start_utc_ticks < $to
            ORDER BY start_utc_ticks, timestamp_utc_ticks, id;
            """;
        command.Parameters.AddWithValue("$from", fromUtc.UtcDateTime.Ticks);
        command.Parameters.AddWithValue("$to", toUtc.UtcDateTime.Ticks);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timestamp = new DateTimeOffset(reader.GetInt64(0), TimeSpan.Zero)
                .ToOffset(TimeSpan.FromMinutes(reader.GetInt32(1)));
            visitor(new ReportSourceSample(
                timestamp,
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt64(5),
                reader.GetInt64(6)));
        }
    }

    /// <summary>Streams sanitized AI request telemetry inside a half-open UTC interval.</summary>
    internal void VisitAiUsage(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken,
        Action<AiRequestUsageRecord> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ValidateInterval(fromUtc, toUtc, "AI usage");
        using var connection = OpenConnection();
        VisitAiUsage(connection, null, fromUtc, toUtc, cancellationToken, visitor);
    }

    private static void VisitAiUsage(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken,
        Action<AiRequestUsageRecord> visitor)
    {
        using var command = connection.CreateCommand();
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        command.CommandText = """
            SELECT
                attempt_id, correlation_id, occurred_utc_ticks, completed_utc_ticks, origin, request_kind, provider,
                endpoint_host, requested_model, returned_model, provider_response_id, provider_request_id, http_status,
                elapsed_ms, provider_processing_ms, image_count, prompt_characters, max_output_tokens, input_tokens,
                output_tokens, total_tokens, cached_input_tokens, cache_write_tokens, cache_creation_input_tokens,
                cache_read_input_tokens, reasoning_tokens, thinking_tokens, reported_cost_microusd,
                reported_upstream_cost_microusd, finish_reason, success, failure_code
            FROM ai_request_usage
            WHERE occurred_utc_ticks >= $from AND occurred_utc_ticks < $to
            ORDER BY occurred_utc_ticks, attempt_id;
            """;
        command.Parameters.AddWithValue("$from", fromUtc.UtcDateTime.Ticks);
        command.Parameters.AddWithValue("$to", toUtc.UtcDateTime.Ticks);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            visitor(ReadAiRequestUsage(reader));
        }
    }

    private static void ValidateInterval(DateTimeOffset fromUtc, DateTimeOffset toUtc, string description)
    {
        if (toUtc <= fromUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(toUtc), $"The {description} query interval must be positive.");
        }
    }

    /// <summary>Streams every activity sample overlapping the supplied half-open UTC interval exactly once.</summary>
    internal void VisitOverlapping(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken, Action<ActivitySample> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        if (toUtc <= fromUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(toUtc), "The activity query interval must be positive.");
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                timestamp_utc_ticks, timestamp_offset_minutes, duration_seconds, state, process_name, application,
                context, window_title, installation_id, key_presses, mouse_clicks, attributes_json
            FROM activity_samples
            WHERE timestamp_utc_ticks > $from AND start_utc_ticks < $to
            ORDER BY start_utc_ticks, timestamp_utc_ticks, id;
            """;
        command.Parameters.AddWithValue("$from", fromUtc.UtcDateTime.Ticks);
        command.Parameters.AddWithValue("$to", toUtc.UtcDateTime.Ticks);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            visitor(ReadSample(reader));
        }
    }

    /// <summary>Visits every retained activity sample with its stable SQLite identifier for search indexing.</summary>
    internal void VisitAllActivitySamples(CancellationToken cancellationToken, Action<long, ActivitySample> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                timestamp_utc_ticks, timestamp_offset_minutes, duration_seconds, state, process_name, application,
                context, window_title, installation_id, key_presses, mouse_clicks, attributes_json, id
            FROM activity_samples
            ORDER BY timestamp_utc_ticks, id;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            visitor(reader.GetInt64(12), ReadSample(reader));
        }
    }

    /// <summary>Loads the most recently completed activity sample.</summary>
    internal ActivitySample? LoadLatest()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                timestamp_utc_ticks, timestamp_offset_minutes, duration_seconds, state, process_name, application,
                context, window_title, installation_id, key_presses, mouse_clicks, attributes_json
            FROM activity_samples
            ORDER BY timestamp_utc_ticks DESC, id DESC
            LIMIT 1;
            """;
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadSample(reader) : null;
    }

    /// <summary>Counts expired activity and AI rows without changing them.</summary>
    internal (int Count, long Bytes) GetRetentionPreview(DateTimeOffset cutoffUtc)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM activity_samples WHERE timestamp_utc_ticks < $cutoff),
                (SELECT COALESCE(SUM(estimated_bytes), 0) FROM activity_samples WHERE timestamp_utc_ticks < $cutoff),
                (SELECT COUNT(*)
                 FROM ai_request_usage AS request
                 WHERE request.occurred_utc_ticks < $cutoff
                   AND NOT EXISTS (
                       SELECT 1
                       FROM ai_analysis_results AS result
                       WHERE result.attempt_id = request.attempt_id
                         AND result.timestamp_utc_ticks >= $cutoff)),
                (SELECT COUNT(*) FROM ai_analysis_results WHERE timestamp_utc_ticks < $cutoff),
                (SELECT COALESCE(SUM(
                    length(provider) + length(endpoint_host) + length(requested_model) + length(origin) + length(request_kind)), 0)
                 FROM ai_request_usage AS request
                 WHERE request.occurred_utc_ticks < $cutoff
                   AND NOT EXISTS (
                       SELECT 1
                       FROM ai_analysis_results AS result
                       WHERE result.attempt_id = request.attempt_id
                         AND result.timestamp_utc_ticks >= $cutoff)),
                (SELECT COALESCE(SUM(length(application) + length(context) + length(summary)), 0)
                 FROM ai_analysis_results WHERE timestamp_utc_ticks < $cutoff);
            """;
        command.Parameters.AddWithValue("$cutoff", cutoffUtc.UtcDateTime.Ticks);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("SQLite did not return a retention aggregate.");
        }

        return (
            checked(reader.GetInt32(0) + reader.GetInt32(2) + reader.GetInt32(3)),
            checked(reader.GetInt64(1) + reader.GetInt64(4) + reader.GetInt64(5)));
    }

    /// <summary>Deletes expired activity and AI rows, including their derived full-text search documents.</summary>
    internal int ApplyRetention(DateTimeOffset cutoffUtc)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var cutoff = cutoffUtc.UtcDateTime.Ticks;
        var removedAiResults = ExecuteDelete(connection, transaction, """
            DELETE FROM ai_analysis_search
            WHERE correlation_id IN (
                SELECT correlation_id FROM ai_analysis_results WHERE timestamp_utc_ticks < $cutoff);
            """, cutoff);
        _ = removedAiResults; // FTS documents are derived data and are not counted as separate retained records.
        var removedResults = ExecuteDelete(connection, transaction, """
            DELETE FROM ai_analysis_results WHERE timestamp_utc_ticks < $cutoff;
            """, cutoff);
        var removedRequests = ExecuteDelete(connection, transaction, """
            DELETE FROM ai_request_usage
            WHERE occurred_utc_ticks < $cutoff
              AND NOT EXISTS (
                  SELECT 1 FROM ai_analysis_results
                  WHERE ai_analysis_results.attempt_id = ai_request_usage.attempt_id);
            """, cutoff);
        var removedActivity = ExecuteDelete(connection, transaction, "DELETE FROM activity_samples WHERE timestamp_utc_ticks < $cutoff;", cutoff);
        transaction.Commit();
        return checked(removedActivity + removedRequests + removedResults);
    }

    private void InitializeSchema(bool databaseExisted)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_databasePath.ToUpperInvariant())))[..32];
        using var schemaMutex = new Mutex(false, $"Local\\TrackMeUp.ActivityDb.{fingerprint}");
        var acquired = false;
        try
        {
            try
            {
                acquired = schemaMutex.WaitOne(TimeSpan.FromSeconds(10));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
            {
                throw new TimeoutException("Timed out while initializing the activity database schema.");
            }

            using var connection = OpenConnection();
            var version = ReadSchemaVersion(connection);
            if (version == 0)
            {
                if (databaseExisted || ReadApplicationSchemaObjects(connection).Count > 0)
                {
                    throw new InvalidOperationException(
                        "An unversioned activity database is not supported; remove it before starting TrackMeUp.");
                }

                CreateSchema(connection);
                version = ReadSchemaVersion(connection);
            }

            if (version == 3)
            {
                // Version 3 is validated before the one-way migration so unsupported databases are never mutated.
                ValidateVersion3Schema(connection);
                MigrateVersion3To4(connection);
                version = ReadSchemaVersion(connection);
            }

            if (version == 4)
            {
                // Version 4 is validated before adding the local pricing cache table.
                ValidateVersion4Schema(connection);
                MigrateVersion4To5(connection);
                version = ReadSchemaVersion(connection);
            }

            if (version != SchemaVersion)
            {
                throw new InvalidOperationException($"Unsupported activity database schema version {version}; expected {SchemaVersion}.");
            }

            ValidateSchema(connection);
            using var journal = connection.CreateCommand();
            journal.CommandText = "PRAGMA journal_mode = WAL;";
            journal.ExecuteScalar();
        }
        finally
        {
            if (acquired)
            {
                schemaMutex.ReleaseMutex();
            }
        }
    }

    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ActivitySchemaSql + AiSchemaSql + ScreenshotTextSchemaSql + AiPricingSchemaSql + $"PRAGMA user_version = {SchemaVersion};";
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void MigrateVersion3To4(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ScreenshotTextSchemaSql + "PRAGMA user_version = 4;";
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void MigrateVersion4To5(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = AiPricingSchemaSql + "PRAGMA user_version = 5;";
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void ValidateVersion3Schema(SqliteConnection connection)
    {
        ValidateBaseSchema(connection);
        var schemaObjects = ReadApplicationSchemaObjects(connection);
        if (!schemaObjects.SetEquals(ExpectedVersion3ApplicationSchemaObjects))
        {
            throw new InvalidOperationException("The activity database contains unsupported schema objects.");
        }
    }

    private static void ValidateVersion4Schema(SqliteConnection connection)
    {
        ValidateBaseSchema(connection);
        ValidateScreenshotTextSchema(connection);
        var schemaObjects = ReadApplicationSchemaObjects(connection);
        if (!schemaObjects.SetEquals(ExpectedVersion4ApplicationSchemaObjects))
        {
            throw new InvalidOperationException("The activity database contains unsupported schema objects.");
        }
    }

    private static void ValidateSchema(SqliteConnection connection)
    {
        ValidateBaseSchema(connection);
        ValidateScreenshotTextSchema(connection);
        ValidateCreateStatement(connection, "ai_model_pricing", AiPricingSchemaSql);

        var actualAiModelPricingColumns = ReadColumns(connection, "ai_model_pricing");
        if (!actualAiModelPricingColumns.SequenceEqual(ExpectedAiModelPricingColumns))
        {
            throw new InvalidOperationException("The AI pricing schema does not match the supported schema.");
        }

        var aiModelPricingIndexes = ReadIndexes(connection, "ai_model_pricing");
        if (!aiModelPricingIndexes.SetEquals(ExpectedAiModelPricingIndexes))
        {
            throw new InvalidOperationException("The AI pricing indexes do not match the supported schema.");
        }

        var schemaObjects = ReadApplicationSchemaObjects(connection);
        if (!schemaObjects.SetEquals(ExpectedApplicationSchemaObjects))
        {
            throw new InvalidOperationException("The activity database contains unsupported schema objects.");
        }
    }

    private static void ValidateScreenshotTextSchema(SqliteConnection connection)
    {
        ValidateCreateStatement(connection, "screenshot_text_snapshots", ScreenshotTextSchemaSql);
        var actualScreenshotTextColumns = ReadColumns(connection, "screenshot_text_snapshots");
        if (!actualScreenshotTextColumns.SequenceEqual(ExpectedScreenshotTextSnapshotColumns))
        {
            throw new InvalidOperationException("The screenshot text snapshot schema does not match the supported schema.");
        }

        var screenshotTextIndexes = ReadIndexes(connection, "screenshot_text_snapshots");
        if (!screenshotTextIndexes.SetEquals(ExpectedScreenshotTextSnapshotIndexes))
        {
            throw new InvalidOperationException("The screenshot text snapshot indexes do not match the supported schema.");
        }
    }

    private static void ValidateBaseSchema(SqliteConnection connection)
    {
        ValidateCreateStatement(connection, "activity_samples", ActivitySchemaSql);
        ValidateCreateStatement(connection, "ai_request_usage", AiSchemaSql);
        ValidateCreateStatement(connection, "ai_analysis_results", AiSchemaSql);
        ValidateCreateStatement(connection, "ai_analysis_search", AiSchemaSql);

        var actualActivityColumns = ReadColumns(connection, "activity_samples");
        if (!actualActivityColumns.SequenceEqual(ExpectedActivityColumns))
        {
            throw new InvalidOperationException("The activity database schema does not match the supported greenfield schema.");
        }

        ValidateColumnNames(connection, "ai_request_usage", ExpectedAiRequestUsageColumns);
        ValidateColumnNames(connection, "ai_analysis_results", ExpectedAiAnalysisResultColumns);

        var activityIndexes = ReadIndexes(connection, "activity_samples");
        if (!activityIndexes.SetEquals(ExpectedActivityIndexes))
        {
            throw new InvalidOperationException("The activity database indexes do not match the supported greenfield schema.");
        }

        var requestIndexes = ReadIndexes(connection, "ai_request_usage");
        if (!requestIndexes.SetEquals(ExpectedAiRequestUsageIndexes))
        {
            throw new InvalidOperationException("The AI usage indexes do not match the supported schema.");
        }

        var resultIndexes = ReadIndexes(connection, "ai_analysis_results");
        if (!resultIndexes.SetEquals(ExpectedAiAnalysisResultIndexes))
        {
            throw new InvalidOperationException("The AI result indexes do not match the supported schema.");
        }
    }

    private static void ValidateCreateStatement(SqliteConnection connection, string objectName, string schemaSql)
    {
        var marker = objectName == "ai_analysis_search"
            ? $"CREATE VIRTUAL TABLE {objectName}"
            : $"CREATE TABLE {objectName}";
        var start = schemaSql.IndexOf(marker, StringComparison.Ordinal);
        var end = start < 0 ? -1 : schemaSql.IndexOf(';', start);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException($"The expected SQL for {objectName} is invalid.");
        }

        var expected = NormalizeSchemaSql(schemaSql[start..end]);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE name = $name;";
        command.Parameters.AddWithValue("$name", objectName);
        var actual = command.ExecuteScalar() as string;
        if (actual is null || !string.Equals(NormalizeSchemaSql(actual), expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The {objectName} schema does not match the supported greenfield schema.");
        }
    }

    private static string NormalizeSchemaSql(string sql) => new(
        sql.Where(character => !char.IsWhiteSpace(character) && character != ';').ToArray());

    private static HashSet<string> ReadApplicationSchemaObjects(SqliteConnection connection)
    {
        var objects = new HashSet<string>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_schema WHERE name NOT LIKE 'sqlite_%';";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            objects.Add(reader.GetString(0));
        }

        return objects;
    }

    private static List<SchemaColumn> ReadColumns(SqliteConnection connection, string table)
    {
        var actualColumns = new List<SchemaColumn>();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            actualColumns.Add(new SchemaColumn(
                reader.GetString(1),
                reader.GetString(2).ToUpperInvariant(),
                reader.GetInt32(3) == 1,
                reader.GetInt32(5)));
        }

        return actualColumns;
    }

    private static void ValidateColumnNames(SqliteConnection connection, string table, IReadOnlyList<string> expected)
    {
        var actual = ReadColumns(connection, table).Select(column => column.Name).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"The {table} table does not match the supported AI telemetry schema.");
        }
    }

    private static HashSet<string> ReadIndexes(SqliteConnection connection, string table)
    {
        var indexes = new HashSet<string>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_list({table});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            indexes.Add(reader.GetString(1));
        }

        return indexes;
    }

    private static void InsertAiRequest(SqliteConnection connection, SqliteTransaction transaction, AiRequestUsageRecord request)
    {
        ValidateAiRequest(request);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ai_request_usage (
                attempt_id, correlation_id, occurred_utc_ticks, completed_utc_ticks, origin, request_kind, provider,
                endpoint_host, requested_model, returned_model, provider_response_id, provider_request_id, http_status,
                elapsed_ms, provider_processing_ms, image_count, prompt_characters, max_output_tokens, input_tokens,
                output_tokens, total_tokens, cached_input_tokens, cache_write_tokens, cache_creation_input_tokens,
                cache_read_input_tokens, reasoning_tokens, thinking_tokens, reported_cost_microusd,
                reported_upstream_cost_microusd, cost_source, finish_reason, success, failure_code)
            VALUES (
                $attemptId, $correlationId, $occurred, $completed, $origin, $requestKind, $provider,
                $endpointHost, $requestedModel, $returnedModel, $providerResponseId, $providerRequestId, $httpStatus,
                $elapsed, $providerProcessing, $imageCount, $promptCharacters, $maxOutputTokens, $inputTokens,
                $outputTokens, $totalTokens, $cachedInputTokens, $cacheWriteTokens, $cacheCreationInputTokens,
                $cacheReadInputTokens, $reasoningTokens, $thinkingTokens, $reportedCost, $reportedUpstreamCost,
                $costSource, $finishReason, $success, $failureCode);
            """;
        Add(command, "$attemptId", request.AttemptId);
        Add(command, "$correlationId", request.CorrelationId);
        Add(command, "$occurred", request.OccurredAt.UtcDateTime.Ticks);
        Add(command, "$completed", request.CompletedAt?.UtcDateTime.Ticks);
        Add(command, "$origin", request.Origin);
        Add(command, "$requestKind", request.RequestKind);
        Add(command, "$provider", request.Provider);
        Add(command, "$endpointHost", request.EndpointHost);
        Add(command, "$requestedModel", request.RequestedModel);
        Add(command, "$returnedModel", request.ReturnedModel);
        Add(command, "$providerResponseId", request.ProviderResponseId);
        Add(command, "$providerRequestId", request.ProviderRequestId);
        Add(command, "$httpStatus", request.HttpStatusCode);
        Add(command, "$elapsed", request.ElapsedMilliseconds);
        Add(command, "$providerProcessing", request.ProviderProcessingMilliseconds);
        Add(command, "$imageCount", request.ImageCount);
        Add(command, "$promptCharacters", request.PromptCharacters);
        Add(command, "$maxOutputTokens", request.MaxOutputTokens);
        Add(command, "$inputTokens", request.Usage.InputTokens);
        Add(command, "$outputTokens", request.Usage.OutputTokens);
        Add(command, "$totalTokens", request.Usage.TotalTokens);
        Add(command, "$cachedInputTokens", request.Usage.CachedInputTokens);
        Add(command, "$cacheWriteTokens", request.Usage.CacheWriteTokens);
        Add(command, "$cacheCreationInputTokens", request.Usage.CacheCreationInputTokens);
        Add(command, "$cacheReadInputTokens", request.Usage.CacheReadInputTokens);
        Add(command, "$reasoningTokens", request.Usage.ReasoningTokens);
        Add(command, "$thinkingTokens", request.Usage.ThinkingTokens);
        Add(command, "$reportedCost", ToMicroUsd(request.Usage.ReportedCostUsd));
        Add(command, "$reportedUpstreamCost", ToMicroUsd(request.Usage.ReportedUpstreamCostUsd));
        Add(command, "$costSource", request.Usage.ReportedCostUsd.HasValue ? "provider" : "unavailable");
        Add(command, "$finishReason", request.FinishReason);
        Add(command, "$success", request.Success ? 1 : 0);
        Add(command, "$failureCode", request.FailureCode);
        command.ExecuteNonQuery();
    }

    private static void InsertAiAnalysisResult(SqliteConnection connection, SqliteTransaction transaction, AiRequestUsageRecord request, AiAnalysis analysis)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ai_analysis_results (
                correlation_id, attempt_id, timestamp_utc_ticks, snapshot_utc_ticks, application, context, summary,
                installation_id, origin, informational_schedule, screenshot_paths, image_count)
            VALUES (
                $correlationId, $attemptId, $timestamp, $snapshotTimestamp, $application, $context, $summary,
                $installationId, $origin, $schedule, $screenshotPaths, $imageCount);

            INSERT INTO ai_analysis_search (correlation_id, application, context, summary)
            VALUES ($correlationId, $application, $context, $summary);
            """;
        Add(command, "$correlationId", analysis.CorrelationId);
        Add(command, "$attemptId", request.AttemptId);
        Add(command, "$timestamp", analysis.Timestamp.UtcDateTime.Ticks);
        Add(command, "$snapshotTimestamp", analysis.Snapshot?.Timestamp.UtcDateTime.Ticks);
        Add(command, "$application", analysis.Application);
        Add(command, "$context", analysis.Context);
        Add(command, "$summary", analysis.Summary);
        Add(command, "$installationId", analysis.InstallationId);
        Add(command, "$origin", analysis.Origin ?? request.Origin);
        Add(command, "$schedule", analysis.InformationalSchedule);
        Add(command, "$screenshotPaths", analysis.ScreenshotPaths);
        Add(command, "$imageCount", request.ImageCount);
        command.ExecuteNonQuery();
    }

    private static void ValidateAiRequest(AiRequestUsageRecord request)
    {
        if (string.IsNullOrWhiteSpace(request.AttemptId) || string.IsNullOrWhiteSpace(request.CorrelationId)
            || string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.RequestKind)
            || string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.EndpointHost)
            || string.IsNullOrWhiteSpace(request.RequestedModel) || request.ImageCount < 0
            || request.PromptCharacters < 0 || request.MaxOutputTokens < 0)
        {
            throw new ArgumentException("AI usage record contains invalid required metadata.", nameof(request));
        }
    }

    private static void ValidateAiModelPricing(string provider, AiModelPricing price)
    {
        if (!string.Equals(price.Provider, provider, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(price.Model)
            || string.IsNullOrWhiteSpace(price.ServiceTier)
            || string.IsNullOrWhiteSpace(price.ContextWindow)
            || !string.Equals(price.Currency, "usd", StringComparison.OrdinalIgnoreCase)
            || price.InputUsdPerMillionTokens < 0m
            || price.CachedInputUsdPerMillionTokens is < 0m
            || price.CacheWriteUsdPerMillionTokens is < 0m
            || price.OutputUsdPerMillionTokens < 0m
            || string.IsNullOrWhiteSpace(price.SourceUrl))
        {
            throw new ArgumentException("AI pricing row contains invalid required metadata.", nameof(price));
        }
    }

    private static AiRequestUsageRecord ReadAiRequestUsage(SqliteDataReader reader)
    {
        var usage = new AiUsageMetrics(
            ReadNullableLong(reader, 18), ReadNullableLong(reader, 19), ReadNullableLong(reader, 20),
            ReadNullableLong(reader, 21), ReadNullableLong(reader, 22), ReadNullableLong(reader, 23),
            ReadNullableLong(reader, 24), ReadNullableLong(reader, 25), ReadNullableLong(reader, 26),
            FromMicroUsd(ReadNullableLong(reader, 27)), FromMicroUsd(ReadNullableLong(reader, 28)));
        return new AiRequestUsageRecord(
            reader.GetString(0), reader.GetString(1), new DateTimeOffset(reader.GetInt64(2), TimeSpan.Zero),
            ReadNullableLong(reader, 3) is { } completed ? new DateTimeOffset(completed, TimeSpan.Zero) : null,
            reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8),
            ReadNullableString(reader, 9), ReadNullableString(reader, 10), ReadNullableString(reader, 11),
            ReadNullableInt(reader, 12), ReadNullableLong(reader, 13), ReadNullableLong(reader, 14), reader.GetInt32(15),
            reader.GetInt32(16), reader.GetInt32(17), usage, ReadNullableString(reader, 29), reader.GetInt32(30) == 1,
            ReadNullableString(reader, 31));
    }

    private static AiModelPricing ReadAiModelPricing(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        FromMicroUsd(reader.GetInt64(5))!.Value,
        FromMicroUsd(ReadNullableLong(reader, 6)),
        FromMicroUsd(ReadNullableLong(reader, 7)),
        FromMicroUsd(reader.GetInt64(8))!.Value,
        reader.GetString(9),
        new DateTimeOffset(reader.GetInt64(10), TimeSpan.Zero));

    private ActivitySample ReadSample(SqliteDataReader reader)
    {
        var timestampUtc = new DateTimeOffset(reader.GetInt64(0), TimeSpan.Zero);
        var timestamp = timestampUtc.ToOffset(TimeSpan.FromMinutes(reader.GetInt32(1)));
        IReadOnlyDictionary<string, string>? attributes = null;
        if (!reader.IsDBNull(11))
        {
            attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(11), _json)
                ?? throw new InvalidOperationException("Stored activity attributes are invalid.");
        }

        return new ActivitySample(
            timestamp, reader.GetInt32(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.GetString(7), reader.GetString(8), reader.GetInt64(9), reader.GetInt64(10), attributes);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
        return connection;
    }

    private static int ExecuteDelete(SqliteConnection connection, SqliteTransaction transaction, string sql, long cutoff)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$cutoff", cutoff);
        return command.ExecuteNonQuery();
    }

    private static void Add(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static object ToDbValue(long? value) => value.HasValue ? value.Value : DBNull.Value;

    private static long? ToMicroUsd(decimal? amount)
    {
        if (!amount.HasValue)
        {
            return null;
        }

        if (amount.Value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        return decimal.ToInt64(decimal.Round(amount.Value * 1_000_000m, 0, MidpointRounding.AwayFromZero));
    }

    private static decimal? FromMicroUsd(long? amount) => amount.HasValue ? amount.Value / 1_000_000m : null;

    private static long? ReadScalarNullableLong(SqliteCommand command)
    {
        var value = command.ExecuteScalar();
        return value is null || value == DBNull.Value ? null : Convert.ToInt64(value);
    }

    private static AiAnalysis ReadAiAnalysis(SqliteDataReader reader) => new(
        new DateTimeOffset(reader.GetInt64(1), TimeSpan.Zero),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        ReadNullableString(reader, 6),
        Snapshot: null,
        CorrelationId: reader.GetString(0),
        Origin: reader.GetString(7),
        InformationalSchedule: ReadNullableString(reader, 8));

    private static long? ReadNullableLong(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

    private static int? ReadNullableInt(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static string? ReadNullableString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static long EstimateBytes(ActivitySample sample, string? attributesJson)
    {
        return FixedEstimatedRowBytes
            + Encoding.UTF8.GetByteCount(sample.State)
            + Encoding.UTF8.GetByteCount(sample.ProcessName)
            + Encoding.UTF8.GetByteCount(sample.Application)
            + Encoding.UTF8.GetByteCount(sample.Context)
            + Encoding.UTF8.GetByteCount(sample.WindowTitle)
            + Encoding.UTF8.GetByteCount(sample.InstallationId)
            + (attributesJson is null ? 0 : Encoding.UTF8.GetByteCount(attributesJson));
    }

    private const string ActivitySchemaSql = """
        CREATE TABLE activity_samples (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp_utc_ticks INTEGER NOT NULL,
            start_utc_ticks INTEGER NOT NULL,
            timestamp_offset_minutes INTEGER NOT NULL,
            duration_seconds INTEGER NOT NULL CHECK (duration_seconds > 0),
            state TEXT NOT NULL,
            process_name TEXT NOT NULL,
            application TEXT NOT NULL,
            context TEXT NOT NULL,
            window_title TEXT NOT NULL,
            installation_id TEXT NOT NULL,
            key_presses INTEGER NOT NULL CHECK (key_presses >= 0),
            mouse_clicks INTEGER NOT NULL CHECK (mouse_clicks >= 0),
            attributes_json TEXT NULL,
            estimated_bytes INTEGER NOT NULL CHECK (estimated_bytes > 0)
        );
        CREATE INDEX ix_activity_samples_start ON activity_samples (start_utc_ticks, timestamp_utc_ticks);
        CREATE INDEX ix_activity_samples_timestamp ON activity_samples (timestamp_utc_ticks);
        """;

    private const string AiSchemaSql = """
        CREATE TABLE ai_request_usage (
            attempt_id TEXT PRIMARY KEY,
            correlation_id TEXT NOT NULL,
            occurred_utc_ticks INTEGER NOT NULL,
            completed_utc_ticks INTEGER NULL,
            origin TEXT NOT NULL,
            request_kind TEXT NOT NULL,
            provider TEXT NOT NULL,
            endpoint_host TEXT NOT NULL,
            requested_model TEXT NOT NULL,
            returned_model TEXT NULL,
            provider_response_id TEXT NULL,
            provider_request_id TEXT NULL,
            http_status INTEGER NULL CHECK (http_status BETWEEN 100 AND 599),
            elapsed_ms INTEGER NULL CHECK (elapsed_ms >= 0),
            provider_processing_ms INTEGER NULL CHECK (provider_processing_ms >= 0),
            image_count INTEGER NOT NULL CHECK (image_count >= 0),
            prompt_characters INTEGER NOT NULL CHECK (prompt_characters >= 0),
            max_output_tokens INTEGER NOT NULL CHECK (max_output_tokens >= 0),
            input_tokens INTEGER NULL CHECK (input_tokens >= 0),
            output_tokens INTEGER NULL CHECK (output_tokens >= 0),
            total_tokens INTEGER NULL CHECK (total_tokens >= 0),
            cached_input_tokens INTEGER NULL CHECK (cached_input_tokens >= 0),
            cache_write_tokens INTEGER NULL CHECK (cache_write_tokens >= 0),
            cache_creation_input_tokens INTEGER NULL CHECK (cache_creation_input_tokens >= 0),
            cache_read_input_tokens INTEGER NULL CHECK (cache_read_input_tokens >= 0),
            reasoning_tokens INTEGER NULL CHECK (reasoning_tokens >= 0),
            thinking_tokens INTEGER NULL CHECK (thinking_tokens >= 0),
            reported_cost_microusd INTEGER NULL CHECK (reported_cost_microusd >= 0),
            reported_upstream_cost_microusd INTEGER NULL CHECK (reported_upstream_cost_microusd >= 0),
            cost_source TEXT NOT NULL CHECK (cost_source IN ('provider', 'unavailable')),
            finish_reason TEXT NULL,
            success INTEGER NOT NULL CHECK (success IN (0, 1)),
            failure_code TEXT NULL
        );
        CREATE INDEX ix_ai_request_usage_occurred ON ai_request_usage (occurred_utc_ticks);
        CREATE INDEX ix_ai_request_usage_correlation ON ai_request_usage (correlation_id, occurred_utc_ticks);

        CREATE TABLE ai_analysis_results (
            correlation_id TEXT PRIMARY KEY,
            attempt_id TEXT NOT NULL UNIQUE,
            timestamp_utc_ticks INTEGER NOT NULL,
            snapshot_utc_ticks INTEGER NULL,
            application TEXT NOT NULL,
            context TEXT NOT NULL,
            summary TEXT NOT NULL,
            installation_id TEXT NOT NULL,
            origin TEXT NOT NULL,
            informational_schedule TEXT NULL,
            screenshot_paths TEXT NULL,
            image_count INTEGER NOT NULL CHECK (image_count >= 0),
            FOREIGN KEY (attempt_id) REFERENCES ai_request_usage(attempt_id) ON DELETE CASCADE
        );
        CREATE INDEX ix_ai_analysis_results_timestamp ON ai_analysis_results (timestamp_utc_ticks);

        -- FTS provides local keyword candidate retrieval; synonym and typo resolution remains an application-level future concern.
        CREATE VIRTUAL TABLE ai_analysis_search USING fts5(
            correlation_id UNINDEXED,
            application,
            context,
            summary,
            tokenize = 'unicode61 remove_diacritics 2'
        );
        """;

    private const string ScreenshotTextSchemaSql = """
        CREATE TABLE screenshot_text_snapshots (
            artifact_identity TEXT NOT NULL PRIMARY KEY,
            capture_id TEXT NOT NULL,
            source_path TEXT NOT NULL,
            extracted_utc_ticks INTEGER NOT NULL,
            snapshot_json TEXT NOT NULL,
            updated_utc_ticks INTEGER NOT NULL
        );
        CREATE INDEX ix_screenshot_text_snapshots_capture
            ON screenshot_text_snapshots (capture_id, artifact_identity);
        """;

    private const string AiPricingSchemaSql = """
        CREATE TABLE ai_model_pricing (
            provider TEXT NOT NULL,
            model TEXT NOT NULL,
            service_tier TEXT NOT NULL,
            context_window TEXT NOT NULL,
            currency TEXT NOT NULL CHECK (currency = 'usd'),
            input_microusd_per_million INTEGER NOT NULL CHECK (input_microusd_per_million >= 0),
            cached_input_microusd_per_million INTEGER NULL CHECK (cached_input_microusd_per_million >= 0),
            cache_write_microusd_per_million INTEGER NULL CHECK (cache_write_microusd_per_million >= 0),
            output_microusd_per_million INTEGER NOT NULL CHECK (output_microusd_per_million >= 0),
            source_url TEXT NOT NULL,
            source_retrieved_utc_ticks INTEGER NOT NULL,
            PRIMARY KEY (provider, model, service_tier, context_window)
        );
        """;

    private readonly record struct SchemaColumn(string Name, string Type, bool NotNull, int PrimaryKeyOrder);
}

/// <summary>Contains the minimal activity projection needed to build aggregate reports.</summary>
internal sealed record ReportSourceSample(
    DateTimeOffset Timestamp,
    int DurationSeconds,
    string State,
    string Application,
    long KeyPresses,
    long MouseClicks);
