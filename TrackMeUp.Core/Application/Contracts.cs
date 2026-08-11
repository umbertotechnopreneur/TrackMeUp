using System.Text.Json.Serialization;
using TrackMeUp.Search;
using TrackMeUp.Services;

namespace TrackMeUp.Application;

/// <summary>Describes a validation error without coupling callers to a presentation technology.</summary>
public sealed record ValidationIssue(string Field, string Code, string MessageKey);

/// <summary>Provides the stable result contract returned by every mutating application operation.</summary>
public sealed record OperationResult<T>(
    bool Succeeded,
    string Code,
    string MessageKey,
    T? Value,
    IReadOnlyList<ValidationIssue> Issues)
{
    /// <summary>Creates a successful result.</summary>
    public static OperationResult<T> Success(string code, string messageKey, T? value = default) => new(true, code, messageKey, value, Array.Empty<ValidationIssue>());

    /// <summary>Creates a failed result.</summary>
    public static OperationResult<T> Failure(string code, string messageKey, params ValidationIssue[] issues) => new(false, code, messageKey, default, issues);
}

/// <summary>Requests that tracking is started with optional launch constraints.</summary>
public sealed record StartTrackingRequest(bool SafeMode = false, string? Source = null);

/// <summary>Requests screenshot capture without passing presentation objects into the application layer.</summary>
/// <param name="Mode">The explicit capture mode, or <see langword="null"/> to use the persisted application setting.</param>
/// <param name="Keep">Whether retained screenshot artifacts should remain after optional analysis.</param>
/// <param name="Watermark">Whether the capture may include the configured watermark.</param>
/// <param name="CaptureOrigin">The stable origin recorded with the capture.</param>
/// <param name="DeferAiAnalysis">Whether AI analysis must wait for an explicit later request.</param>
public sealed record CaptureScreenshotRequest(
    string? Mode,
    bool Keep,
    bool Watermark,
    string CaptureOrigin,
    bool DeferAiAnalysis = false);

/// <summary>Requests AI analysis for an already captured screenshot without taking a second capture.</summary>
public sealed record AnalyzeCapturedScreenshotRequest(
    ScreenshotCaptureResult Capture,
    bool KeepCapture,
    string Origin = "snapshot.manual");

/// <summary>Requests retained screenshots for one inclusive local calendar date.</summary>
public sealed record ScreenshotGalleryRequest(DateOnly Date);

/// <summary>Provides the local snapshot counts and text-reading capability shown before opening search.</summary>
public sealed record SearchAvailability(
    int TotalSnapshotCount,
    int TodaySnapshotCount,
    bool TextReadingEnabled);

/// <summary>Identifies the outcome of local screenshot text extraction.</summary>
public enum ScreenshotTextExtractionStatus
{
    /// <summary>OCR was explicitly disabled and no image I/O was performed.</summary>
    Disabled,

    /// <summary>OCR completed but found no readable text.</summary>
    NoText,

    /// <summary>OCR completed and returned raw text data.</summary>
    Succeeded,

    /// <summary>OCR was enabled but the local recognizer could not complete extraction.</summary>
    Failed
}

/// <summary>Describes one raw OCR word and its image-relative pixel bounds.</summary>
public sealed record OcrWordSnapshot(
    string Text,
    double X,
    double Y,
    double Width,
    double Height);

/// <summary>Describes one raw OCR line and all words returned by the local recognizer.</summary>
public sealed record OcrLineSnapshot(
    string Text,
    IReadOnlyList<OcrWordSnapshot> Words);

/// <summary>Contains the complete local OCR output retained for one screenshot artifact.</summary>
public sealed record OcrRawSnapshot(
    ScreenshotTextExtractionStatus Status,
    string RawText,
    string? LanguageTag,
    double? TextAngleDegrees,
    DateTimeOffset ExtractedAt,
    string Engine,
    uint? PixelWidth,
    uint? PixelHeight,
    IReadOnlyList<OcrLineSnapshot> Lines,
    string? FailureCode = null);

/// <summary>Contains an AI-produced, structured summary of locally extracted OCR text.</summary>
public sealed record OcrStructuredSummary(
    string Overview,
    IReadOnlyList<string> KeyPoints,
    IReadOnlyList<string> Entities,
    IReadOnlyList<string> Actions);

/// <summary>Contains the optional AI correction of OCR text produced by a dedicated enrichment request.</summary>
public sealed record OcrAiRefinement(
    string CorrectedText,
    string? LanguageTag,
    OcrStructuredSummary Summary,
    DateTimeOffset RefinedAt);

/// <summary>Associates raw local OCR and optional AI refinement with one screenshot source artifact.</summary>
public sealed record ScreenshotTextSnapshot(
    string SourceScreenshotPath,
    OcrRawSnapshot Ocr,
    OcrAiRefinement? AiRefinement = null);

/// <summary>Describes one retained screenshot that can be rendered by a presentation surface.</summary>
/// <param name="CapturedAt">The capture timestamp stored with the retained screenshot artifact.</param>
/// <param name="Path">The absolute path of the retained screenshot artifact.</param>
/// <param name="ForegroundApplication">The closest foreground application observed during the capture interval.</param>
/// <param name="CaptureKind">The stable capture-kind identifier parsed from the owned artifact name.</param>
/// <param name="CaptureOrigin">The stable manual or scheduled capture origin.</param>
/// <param name="SpanLabels">Distinct consecutive activity labels sampled during the capture interval.</param>
/// <param name="AiDescriptionMarkdown">The newest persisted AI description that references this exact artifact, formatted as Markdown.</param>
/// <param name="AiAnalyzedAt">The timestamp of the associated AI analysis, or <see langword="null"/> when no successful result exists.</param>
/// <param name="ActivityIndex">A 0-100 historical interval index based on durable input, active-time, CPU, and GPU telemetry.</param>
/// <param name="TextSnapshot">The local OCR snapshot and optional AI refinement associated with this artifact.</param>
/// <param name="ForegroundWindowTitle">The closest foreground window title observed during the capture interval.</param>
/// <param name="ScreenIndex">The one-based monitor index parsed from the retained artifact name, or <see langword="null"/> for active-window captures.</param>
/// <param name="ScreenName">A stable display label derived from <paramref name="ScreenIndex"/> when available.</param>
/// <param name="MouseClicks">The number of durable mouse clicks observed during the capture interval, or <see langword="null"/> when no activity samples overlap it.</param>
/// <param name="CpuUsagePercent">The average CPU usage persisted for the capture interval, or <see langword="null"/> when telemetry was unavailable.</param>
/// <param name="GpuUsagePercent">The average GPU usage persisted for the capture interval, or <see langword="null"/> when telemetry was unavailable.</param>
public sealed record ScreenshotGalleryItem(
    DateTimeOffset CapturedAt,
    string Path,
    string ForegroundApplication,
    string CaptureKind,
    string CaptureOrigin,
    IReadOnlyList<ActivityLabelSample>? SpanLabels = null,
    string? AiDescriptionMarkdown = null,
    DateTimeOffset? AiAnalyzedAt = null,
    int? ActivityIndex = null,
    ScreenshotTextSnapshot? TextSnapshot = null,
    string? ForegroundWindowTitle = null,
    int? ScreenIndex = null,
    string? ScreenName = null,
    long? MouseClicks = null,
    int? CpuUsagePercent = null,
    int? GpuUsagePercent = null);

/// <summary>Contains telemetry averaged between the previous retained screenshot and the current capture.</summary>
public sealed record ScreenshotIntervalTelemetry(
    DateTimeOffset IntervalStartedAt,
    DateTimeOffset CapturedAt,
    int? CpuUsagePercent,
    int? GpuUsagePercent);

/// <summary>Describes one distinct local activity label observed during a screenshot interval.</summary>
public sealed record ActivityLabelSample(DateTimeOffset SampledAt, string Label);

/// <summary>Contains the retained screenshot projection for one local calendar date.</summary>
public sealed record ScreenshotGallery(
    DateOnly Date,
    IReadOnlyList<ScreenshotGalleryItem> Items);

/// <summary>Identifies one persisted top-level window placement in physical pixels.</summary>
public sealed record WindowState(
    int X,
    int Y,
    int Width,
    int Height,
    string MonitorDeviceName);

/// <summary>Defines the minimum usable logical dimensions for a window surface.</summary>
public readonly record struct WindowMinimumSize(int Width, int Height);

/// <summary>Describes a monitor work area in physical pixels.</summary>
public readonly record struct WindowWorkArea(int X, int Y, int Width, int Height)
{
    /// <summary>Gets the exclusive right edge of the work area.</summary>
    public int Right => checked(X + Width);

    /// <summary>Gets the exclusive bottom edge of the work area.</summary>
    public int Bottom => checked(Y + Height);
}

/// <summary>Calculates safe window bounds without depending on a windowing API.</summary>
public static class WindowStateCalculator
{
    /// <summary>Clamps a saved placement to the supplied monitor work area and minimum usable size.</summary>
    public static WindowState ClampToWorkArea(
        WindowState state,
        WindowWorkArea workArea,
        string monitorDeviceName,
        int minimumWidth = 1,
        int minimumHeight = 1)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea), "A monitor work area must have positive dimensions.");
        }

        if (state.Width <= 0 ||
            state.Height <= 0 ||
            minimumWidth <= 0 ||
            minimumHeight <= 0 ||
            string.IsNullOrWhiteSpace(state.MonitorDeviceName))
        {
            throw new InvalidOperationException("Persisted window state is invalid.");
        }

        var boundedMinimumWidth = Math.Min(minimumWidth, workArea.Width);
        var boundedMinimumHeight = Math.Min(minimumHeight, workArea.Height);
        var width = Math.Clamp(state.Width, boundedMinimumWidth, workArea.Width);
        var height = Math.Clamp(state.Height, boundedMinimumHeight, workArea.Height);
        var x = Math.Clamp(state.X, workArea.X, workArea.Right - width);
        var y = Math.Clamp(state.Y, workArea.Y, workArea.Bottom - height);
        return state with
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            MonitorDeviceName = string.IsNullOrWhiteSpace(monitorDeviceName) ? throw new ArgumentException("Monitor device name is required.", nameof(monitorDeviceName)) : monitorDeviceName
        };
    }
}

/// <summary>Requests analysis of the latest activity context.</summary>
public sealed record AnalyzeCurrentActivityRequest(bool AllowCapture = true, string? Origin = null);

/// <summary>Contains a whitelist-bound settings patch.</summary>
public sealed record SettingsPatch(IReadOnlyDictionary<string, string?> Values);

/// <summary>Stable identifiers for the four one-click startup profiles.</summary>
public static class QuickSetupProfileIds
{
    /// <summary>Enables AI-provider features and automatic screenshots.</summary>
    public const string Complete = "complete";

    /// <summary>Enables AI-provider features without automatic screenshots.</summary>
    public const string Assisted = "assisted";

    /// <summary>Keeps automatic screenshots locally with AI-provider features disabled.</summary>
    public const string LocalRecord = "local-record";

    /// <summary>Keeps only the local activity timeline enabled.</summary>
    public const string EssentialOffline = "essential-offline";
}

/// <summary>Requests one validated Quick Setup profile and an explicit Windows-startup preference.</summary>
public sealed record QuickSetupProfileRequest(string ProfileId, bool StartWithWindows);

/// <summary>Requests a retention preview or confirmed cleanup.</summary>
public sealed record RetentionRequest(bool Execute, bool Confirmed);

/// <summary>Requests a dated daily digest without exposing presentation-specific arguments.</summary>
public sealed record GenerateDailyDigestRequest(DateOnly Date, bool Open);

/// <summary>Describes the runtime reachable through the local IPC host.</summary>
public sealed record RuntimeHealth(
    string ProductVersion,
    int ProtocolVersion,
    string InstallationFingerprint,
    bool IsRuntimeOwner,
    IReadOnlyList<string> Capabilities,
    ObservabilityHealth? Observability = null);

/// <summary>Describes non-secret logging and remote-error-reporting diagnostics for the active host.</summary>
public sealed record ObservabilityHealth(
    bool ConsoleLoggingEnabled,
    bool FileLoggingEnabled,
    string SentryStatus,
    bool SendsDefaultPii);

/// <summary>Describes one persisted privacy rule.</summary>
public sealed record PrivacyRule(string Id, string Type, string Value);

/// <summary>Describes a configured application-context provider.</summary>
public sealed record PluginInfo(string Id, string Name, bool Enabled, string Description);

/// <summary>Describes a retention candidate without deleting it.</summary>
public sealed record RetentionPreview(int FileCount, long TotalBytes, IReadOnlyList<string> Paths);

/// <summary>Describes the configured data-retention policy.</summary>
public sealed record RetentionStatus(int DataRetentionDays, int ScreenshotRetentionDays, string ScreenshotDirectory);

/// <summary>Requires both destructive confirmations before an atomic application reset can be prepared.</summary>
public sealed record AtomicResetRequest(bool FirstConfirmation, bool FinalConfirmation);

/// <summary>Contains the validated local targets needed by the runtime owner to reset and relaunch TrackMeUp.</summary>
public sealed record AtomicResetPlan(string DataDirectory, string ScreenshotDirectory, string ExecutablePath);

/// <summary>Describes safe, non-secret AI configuration state.</summary>
public sealed record AiStatus(bool Enabled, string Provider, string Model, string Endpoint, string KeyVariable, bool HasKey, bool CanEnable, AnalysisCostGate CostGate);

/// <summary>Contains a simplified cached provider price for presentation surfaces.</summary>
public sealed record AiPricingCostRow(string Model, decimal InputUsdPerMillionTokens, decimal OutputUsdPerMillionTokens);

/// <summary>Contains simplified provider pricing plus daily and month-to-date local usage costs.</summary>
public sealed record AiPricingOverview(
    DateTimeOffset? LastSynchronizedAt,
    int PriceRowCount,
    int DisplayedModelCount,
    decimal? EstimatedCostTodayUsd,
    int EstimatedCostTodayRequestCount,
    decimal? ActualCostTodayUsd,
    int ActualCostTodayRequestCount,
    long TodayInputTokens,
    long TodayOutputTokens,
    long TodayTotalTokens,
    DateOnly CurrentMonthStart,
    DateOnly CurrentMonthEnd,
    decimal? EstimatedCostCurrentMonthUsd,
    decimal? ActualCostCurrentMonthUsd,
    IReadOnlyList<AiPricingCostRow> Models);

/// <summary>Contains the safe, displayable output of an explicit AI connection check.</summary>
public sealed record AiConnectionTestResult(string Provider, string Model, string Output, long ElapsedMilliseconds);

/// <summary>Defines the non-secret text exchanged by the AI provider connection probe.</summary>
public static class AiConnectionTestProtocol
{
    /// <summary>Gets the exact prompt sent by the bounded text-only connection probe.</summary>
    public const string Prompt = "Reply with exactly: TrackMeUp connection confirmed.";
}

/// <summary>Classifies a non-secret notification that an application frontend may present.</summary>
public enum ApplicationNotificationSeverity
{
    /// <summary>Provides neutral product information.</summary>
    Information,

    /// <summary>Highlights a recoverable configuration or runtime condition.</summary>
    Warning,

    /// <summary>Reports an operation that could not complete.</summary>
    Error
}

/// <summary>Describes one localized, non-secret notification emitted by the shared runtime.</summary>
public sealed record ApplicationNotification(
    Guid Id,
    DateTimeOffset CreatedAt,
    ApplicationNotificationSeverity Severity,
    string TitleKey,
    string MessageKey,
    string Code,
    string? Detail = null);

/// <summary>Provides product metadata and safe external links.</summary>
public sealed record BuildInformation(
    int SchemaVersion,
    string SemVer,
    string PackageVersion,
    DateTimeOffset BuiltAtUtc,
    DateTimeOffset BuiltAtLocal,
    string MachineName,
    string GitCommit,
    string GitCommitShort,
    bool GitDirty,
    string Configuration,
    string Platform,
    string RuntimeIdentifier);

/// <summary>Provides product metadata, build provenance and safe external links.</summary>
public sealed record ProductInformation(string Name, string License, string RepositoryUrl, string AuthorUrl, BuildInformation Build);

/// <summary>Signals a runtime state transition to all presentation clients.</summary>
public sealed record RuntimeStateChangedEventArgs(DashboardState Dashboard, string Code);

/// <summary>Exposes every frontend capability through UI-independent requests and result DTOs.</summary>
public interface ITrackMeUpApplication : IAsyncDisposable
{
    /// <summary>Occurs after tracking state or dashboard data changes.</summary>
    event EventHandler<RuntimeStateChangedEventArgs>? RuntimeStateChanged;

    /// <summary>Gets runtime health and supported capabilities.</summary>
    Task<OperationResult<RuntimeHealth>> GetRuntimeHealthAsync(CancellationToken cancellationToken);

    /// <summary>Starts tracking.</summary>
    Task<OperationResult<DashboardState>> StartTrackingAsync(StartTrackingRequest request, CancellationToken cancellationToken);

    /// <summary>Pauses tracking.</summary>
    Task<OperationResult<DashboardState>> PauseTrackingAsync(CancellationToken cancellationToken);

    /// <summary>Toggles tracking state.</summary>
    Task<OperationResult<DashboardState>> ToggleTrackingAsync(CancellationToken cancellationToken);

    /// <summary>Gets the current dashboard state.</summary>
    Task<OperationResult<DashboardState>> GetDashboardAsync(CancellationToken cancellationToken);

    /// <summary>Gets the latest recorded session state.</summary>
    Task<OperationResult<LastSessionState?>> GetLastSessionAsync(CancellationToken cancellationToken);

    /// <summary>Gets today's activity summary.</summary>
    Task<OperationResult<DailySummary>> GetTodaySummaryAsync(CancellationToken cancellationToken);

    /// <summary>Searches every locally available activity, screenshot, OCR, and AI text field.</summary>
    Task<OperationResult<SearchResponse>> SearchAsync(SearchRequest request, CancellationToken cancellationToken);

    /// <summary>Returns type-ahead suggestions from the separate local suggestion index.</summary>
    Task<OperationResult<IReadOnlyList<SearchSuggestion>>> GetSearchSuggestionsAsync(SearchSuggestionRequest request, CancellationToken cancellationToken);

    /// <summary>Gets the retained snapshot counts and local text-reading availability before search opens.</summary>
    Task<OperationResult<SearchAvailability>> GetSearchAvailabilityAsync(CancellationToken cancellationToken);

    /// <summary>Rebuilds the mandatory local search index from durable source data.</summary>
    Task<OperationResult<int>> RebuildSearchIndexAsync(CancellationToken cancellationToken);

    /// <summary>Gets a privacy-safe aggregate report for an inclusive local-date range.</summary>
    Task<OperationResult<ReportSnapshot>> GetReportAsync(ReportQuery query, CancellationToken cancellationToken);

    /// <summary>Captures a current system snapshot.</summary>
    Task<OperationResult<SystemSnapshot>> CaptureSystemSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>Captures screenshots after privacy checks.</summary>
    Task<OperationResult<ScreenshotCaptureResult>> CaptureScreenshotAsync(CaptureScreenshotRequest request, CancellationToken cancellationToken);

    /// <summary>Captures a manual screenshot and starts its runtime-owned deletion window.</summary>
    Task<OperationResult<PendingManualScreenshotState>> CaptureManualScreenshotAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the manual screenshot that is still inside its runtime-owned deletion window.</summary>
    Task<OperationResult<bool>> DeletePendingManualScreenshotAsync(CancellationToken cancellationToken);

    /// <summary>Analyzes an existing screenshot capture after its temporary deletion window has elapsed.</summary>
    Task<OperationResult<AiAnalysis>> AnalyzeCapturedScreenshotAsync(AnalyzeCapturedScreenshotRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes all local image artifacts belonging to one retained screenshot capture.</summary>
    Task<OperationResult<string>> DeleteScreenshotAsync(string screenshotPath, CancellationToken cancellationToken);

    /// <summary>Deletes local snapshot-analysis records associated with one retained screenshot capture.</summary>
    Task<OperationResult<string>> DeleteSnapshotAsync(string screenshotPath, CancellationToken cancellationToken);

    /// <summary>Gets the most recent retained screenshot.</summary>
    Task<OperationResult<string?>> GetLatestScreenshotAsync(CancellationToken cancellationToken);

    /// <summary>Gets the retained screenshot gallery for one local calendar date.</summary>
    Task<OperationResult<ScreenshotGallery>> GetScreenshotGalleryAsync(DateOnly date, CancellationToken cancellationToken);

    /// <summary>Gets the retained screenshot gallery for the most recent local calendar date that contains a capture.</summary>
    Task<OperationResult<ScreenshotGallery>> GetLatestScreenshotGalleryAsync(CancellationToken cancellationToken);

    /// <summary>Saves one retained screenshot to a user-selected destination.</summary>
    Task<OperationResult<string>> SaveScreenshotAsync(string screenshotPath, string destinationPath, CancellationToken cancellationToken);

    /// <summary>Requests the Windows Share UI for one retained screenshot.</summary>
    Task<OperationResult<string>> ShareScreenshotAsync(string screenshotPath, long windowHandle, CancellationToken cancellationToken);

    /// <summary>Opens the newest application log with the Windows shell.</summary>
    Task<OperationResult<bool>> OpenApplicationLogAsync(CancellationToken cancellationToken);

    /// <summary>Creates a bounded redacted application-log copy and opens the Windows Share UI.</summary>
    Task<OperationResult<bool>> ShareApplicationLogAsync(long windowHandle, CancellationToken cancellationToken);

    /// <summary>Opens the configured screenshot folder.</summary>
    Task<OperationResult<string>> OpenScreenshotFolderAsync(CancellationToken cancellationToken);

    /// <summary>Opens an explicitly supplied screenshot folder without persisting it.</summary>
    Task<OperationResult<string>> OpenScreenshotFolderAsync(string directory, CancellationToken cancellationToken);

    /// <summary>Gets safe AI status.</summary>
    Task<OperationResult<AiStatus>> GetAiStatusAsync(CancellationToken cancellationToken);

    /// <summary>Gets simplified cached OpenAI pricing plus daily and month-to-date local usage cost.</summary>
    Task<OperationResult<AiPricingOverview>> GetAiPricingOverviewAsync(CancellationToken cancellationToken);

    /// <summary>Sends a minimal non-image prompt to verify the persisted AI connection.</summary>
    Task<OperationResult<AiConnectionTestResult>> TestAiConnectionAsync(CancellationToken cancellationToken);

    /// <summary>Atomically takes pending user-facing notifications from the shared runtime.</summary>
    Task<OperationResult<IReadOnlyList<ApplicationNotification>>> DrainApplicationNotificationsAsync(CancellationToken cancellationToken);

    /// <summary>Gets the validated model catalog available to presentation clients.</summary>
    Task<OperationResult<AiModelCatalogSnapshot>> GetAiModelCatalogAsync(CancellationToken cancellationToken);

    /// <summary>Changes the enabled AI state.</summary>
    Task<OperationResult<AiStatus>> SetAiEnabledAsync(bool enabled, CancellationToken cancellationToken);

    /// <summary>Updates non-secret AI settings.</summary>
    Task<OperationResult<AppSettings>> ConfigureAiAsync(SettingsPatch patch, CancellationToken cancellationToken);

    /// <summary>Stores an API key only in the specified user environment variable.</summary>
    Task<OperationResult<string>> SetAiKeyAsync(string keyVariable, string secret, CancellationToken cancellationToken);

    /// <summary>Runs an immediate, policy-enforced AI analysis.</summary>
    Task<OperationResult<AiAnalysis>> AnalyzeCurrentActivityAsync(AnalyzeCurrentActivityRequest request, CancellationToken cancellationToken);

    /// <summary>Generates today's report.</summary>
    Task<OperationResult<string>> GenerateTodayReportAsync(string? outputDirectory, bool open, CancellationToken cancellationToken);

    /// <summary>Generates the daily digest for the requested local date.</summary>
    Task<OperationResult<string>> GenerateDailyDigestAsync(DateOnly date, bool open, CancellationToken cancellationToken);

    /// <summary>Opens the reports folder.</summary>
    Task<OperationResult<string>> OpenReportsFolderAsync(CancellationToken cancellationToken);

    /// <summary>Launches the WinUI frontend for the shared runtime.</summary>
    Task<OperationResult<string>> OpenUserInterfaceAsync(CancellationToken cancellationToken);

    /// <summary>Lists privacy rules.</summary>
    Task<OperationResult<IReadOnlyList<PrivacyRule>>> GetPrivacyRulesAsync(CancellationToken cancellationToken);

    /// <summary>Adds a privacy rule.</summary>
    Task<OperationResult<PrivacyRule>> AddPrivacyRuleAsync(string type, string value, CancellationToken cancellationToken);

    /// <summary>Removes a privacy rule.</summary>
    Task<OperationResult<bool>> RemovePrivacyRuleAsync(string id, CancellationToken cancellationToken);

    /// <summary>Tests the latest context against privacy rules.</summary>
    Task<OperationResult<bool>> TestCurrentPrivacyAsync(CancellationToken cancellationToken);

    /// <summary>Gets retention policy status.</summary>
    Task<OperationResult<RetentionStatus>> GetRetentionStatusAsync(CancellationToken cancellationToken);

    /// <summary>Previews retention candidates without changing files.</summary>
    Task<OperationResult<RetentionPreview>> PreviewRetentionAsync(CancellationToken cancellationToken);

    /// <summary>Executes a confirmed retention cleanup.</summary>
    Task<OperationResult<RetentionPreview>> RunRetentionAsync(RetentionRequest request, CancellationToken cancellationToken);

    /// <summary>Stops data-producing services and prepares a complete local-data reset after two explicit confirmations.</summary>
    Task<OperationResult<AtomicResetPlan>> PrepareAtomicResetAsync(AtomicResetRequest request, CancellationToken cancellationToken);

    /// <summary>Lists context-provider plugins.</summary>
    Task<OperationResult<IReadOnlyList<PluginInfo>>> GetPluginsAsync(CancellationToken cancellationToken);

    /// <summary>Gets a context-provider plugin.</summary>
    Task<OperationResult<PluginInfo>> GetPluginAsync(string id, CancellationToken cancellationToken);

    /// <summary>Changes a plugin enabled state.</summary>
    Task<OperationResult<PluginInfo>> SetPluginEnabledAsync(string id, bool enabled, CancellationToken cancellationToken);

    /// <summary>Gets typed application settings.</summary>
    Task<OperationResult<AppSettings>> GetSettingsAsync(CancellationToken cancellationToken);

    /// <summary>Applies one complete Quick Setup profile as a single validated settings transaction.</summary>
    Task<OperationResult<AppSettings>> ApplyQuickSetupProfileAsync(QuickSetupProfileRequest request, CancellationToken cancellationToken);

    /// <summary>Validates and persists an allowed settings patch.</summary>
    Task<OperationResult<AppSettings>> PatchSettingsAsync(SettingsPatch patch, CancellationToken cancellationToken);

    /// <summary>Restores one window placement using the current monitor topology.</summary>
    Task<OperationResult<WindowState?>> RestoreWindowStateAsync(string windowKey, long windowHandle, CancellationToken cancellationToken);

    /// <summary>Persists one window placement read from its native window handle.</summary>
    Task<OperationResult<WindowState>> SaveWindowStateAsync(string windowKey, long windowHandle, CancellationToken cancellationToken);

    /// <summary>Gets startup registration state.</summary>
    Task<OperationResult<bool>> GetStartupStatusAsync(CancellationToken cancellationToken);

    /// <summary>Changes startup registration state.</summary>
    Task<OperationResult<bool>> SetStartupEnabledAsync(bool enabled, CancellationToken cancellationToken);

    /// <summary>Gets safe product links and metadata.</summary>
    Task<OperationResult<ProductInformation>> GetProductInformationAsync(CancellationToken cancellationToken);

    /// <summary>Opens one allowlisted product link selected by semantic key.</summary>
    Task<OperationResult<bool>> OpenProductLinkAsync(string linkKey, CancellationToken cancellationToken);
}
