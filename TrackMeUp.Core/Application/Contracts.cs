using System.Text.Json.Serialization;
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
public sealed record CaptureScreenshotRequest(string Mode, bool Keep, bool Watermark);

/// <summary>Requests analysis of the latest activity context.</summary>
public sealed record AnalyzeCurrentActivityRequest(bool AllowCapture = true, string? Origin = null);

/// <summary>Contains a whitelist-bound settings patch.</summary>
public sealed record SettingsPatch(IReadOnlyDictionary<string, string?> Values);

/// <summary>Requests a retention preview or confirmed cleanup.</summary>
public sealed record RetentionRequest(bool Execute, bool Confirmed);

/// <summary>Requests the start of a focus session.</summary>
public sealed record StartFocusSessionRequest(string Objective);

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

/// <summary>Describes safe, non-secret AI configuration state.</summary>
public sealed record AiStatus(bool Enabled, string Provider, string Model, string Endpoint, string KeyVariable, bool HasKey, AnalysisCostGate CostGate);

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

    /// <summary>Gets a privacy-safe aggregate report for an inclusive local-date range.</summary>
    Task<OperationResult<ReportSnapshot>> GetReportAsync(ReportQuery query, CancellationToken cancellationToken);

    /// <summary>Starts a focus session.</summary>
    Task<OperationResult<FocusSessionState>> StartFocusSessionAsync(StartFocusSessionRequest request, CancellationToken cancellationToken);

    /// <summary>Gets the current focus-session state.</summary>
    Task<OperationResult<FocusSessionState>> GetFocusSessionAsync(CancellationToken cancellationToken);

    /// <summary>Stops the current focus session.</summary>
    Task<OperationResult<FocusSessionSummary?>> StopFocusSessionAsync(bool summarize, CancellationToken cancellationToken);

    /// <summary>Captures a current system snapshot.</summary>
    Task<OperationResult<SystemSnapshot>> CaptureSystemSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>Captures screenshots after privacy checks.</summary>
    Task<OperationResult<ScreenshotCaptureResult>> CaptureScreenshotAsync(CaptureScreenshotRequest request, CancellationToken cancellationToken);

    /// <summary>Gets the most recent retained screenshot.</summary>
    Task<OperationResult<string?>> GetLatestScreenshotAsync(CancellationToken cancellationToken);

    /// <summary>Opens the configured screenshot folder.</summary>
    Task<OperationResult<string>> OpenScreenshotFolderAsync(CancellationToken cancellationToken);

    /// <summary>Gets safe AI status.</summary>
    Task<OperationResult<AiStatus>> GetAiStatusAsync(CancellationToken cancellationToken);

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

    /// <summary>Lists context-provider plugins.</summary>
    Task<OperationResult<IReadOnlyList<PluginInfo>>> GetPluginsAsync(CancellationToken cancellationToken);

    /// <summary>Gets a context-provider plugin.</summary>
    Task<OperationResult<PluginInfo>> GetPluginAsync(string id, CancellationToken cancellationToken);

    /// <summary>Changes a plugin enabled state.</summary>
    Task<OperationResult<PluginInfo>> SetPluginEnabledAsync(string id, bool enabled, CancellationToken cancellationToken);

    /// <summary>Gets typed application settings.</summary>
    Task<OperationResult<AppSettings>> GetSettingsAsync(CancellationToken cancellationToken);

    /// <summary>Validates and persists an allowed settings patch.</summary>
    Task<OperationResult<AppSettings>> PatchSettingsAsync(SettingsPatch patch, CancellationToken cancellationToken);

    /// <summary>Gets startup registration state.</summary>
    Task<OperationResult<bool>> GetStartupStatusAsync(CancellationToken cancellationToken);

    /// <summary>Changes startup registration state.</summary>
    Task<OperationResult<bool>> SetStartupEnabledAsync(bool enabled, CancellationToken cancellationToken);

    /// <summary>Gets safe product links and metadata.</summary>
    Task<OperationResult<ProductInformation>> GetProductInformationAsync(CancellationToken cancellationToken);
}
