using System;
using System.Collections.Generic;

namespace TrackMeUp;

public sealed record ActivitySample(
    DateTimeOffset Timestamp,
    int DurationSeconds,
    string State,
    string ProcessName,
    string Application,
    string Context,
    string WindowTitle,
    string InstallationId,
    long KeyPresses,
    long MouseClicks,
    IReadOnlyDictionary<string, string>? Attributes = null);

public static class FlyoutPositions
{
    public const string BottomCenter = "bottom-center";
    public const string BottomLeft = "bottom-left";
    public const string BottomRight = "bottom-right";
    public const string TopLeft = "top-left";
    public const string TopRight = "top-right";
}

/// <summary>Defines the supported anchors for the compact taskbar control.</summary>
public static class TaskbarWidgetPositions
{
    /// <summary>Places the control near the left edge of the taskbar.</summary>
    public const string Left = "left";

    /// <summary>Places the control in the middle of the taskbar.</summary>
    public const string Center = "center";

    /// <summary>Places the control before the notification area.</summary>
    public const string Right = "right";
}

public sealed record AppSettings(
    string InstallationId = "",
    string Model = "gpt-5.6",
    bool KeepScreenshots = false,
    bool StartWithWindows = false,
    bool AutomaticAnalysis = false,
    string ScreenshotDirectory = "",
    string ScreenshotCaptureMode = "all-screens",
    bool WatermarkScreenshots = true,
    string AiProvider = "openai",
    string AiEndpoint = "https://api.openai.com/v1/responses",
    string AiApiKeyName = "OPENAI_API_KEY",
    string AiOutputDetail = "balanced",
    string AiReasoningEffort = "auto",
    string FlyoutPosition = "bottom-center",
    string UiLanguage = "en",
    string Theme = "system",
    bool OpenAiEnabled = false,
    bool ScreenshotsEnabled = false,
    bool EnableWordDetailPlugin = true,
    bool EnableExcelDetailPlugin = true,
    bool EnableVsCodeDetailPlugin = true,
    bool EnableBrowserDetailPlugin = true,
    string PrivacyProcessNames = "",
    string PrivacyWindowTitles = "",
    string PrivacyWindowHints = "",
    bool DailyDigestEnabled = true,
    string DailyDigestDirectory = "",
    int DataRetentionDays = 30,
    int ScreenshotRetentionDays = 30,
    int OpenAiDailyLimit = 20,
    int AutomaticAnalysisIntervalMinutes = 15,
    decimal OpenAiDailyCostUsd = 0m,
    decimal EstimatedCostPerAnalysisUsd = 0.02m,
    decimal EstimatedCostPerScreenshotUsd = 0.003m,
    bool ShowCostGuardrailInStatus = true,
    string LastDailyDigestDate = "",
    bool FocusSessionSummaryEnabled = true,
    bool StartTrackingOnLaunch = false,
    string TaskbarWidgetPosition = TaskbarWidgetPositions.Left);

public sealed record AiAnalysis(
    DateTimeOffset Timestamp,
    string Application,
    string Context,
    string Summary,
    string InstallationId,
    string? ScreenshotPaths,
    SystemSnapshot? Snapshot = null);

public sealed record ApplicationSummary(string Application, long ActiveSeconds);

public sealed record DailySummary(long ActiveSeconds, long IdleSeconds, long KeyPresses, long MouseClicks, IReadOnlyList<ApplicationSummary> Applications);

public sealed record DailyActivityWindow(string Application, string Context, long ActiveSeconds);

public sealed record FocusSessionState(
    string? Objective,
    bool IsActive,
    DateTimeOffset? StartedAt,
    TimeSpan Elapsed,
    long ActiveSeconds,
    long IdleSeconds,
    long KeyPresses,
    long MouseClicks,
    string? PrimaryApplication);

public sealed record FocusSessionSummary(
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string Objective,
    long ActiveSeconds,
    long IdleSeconds,
    long KeyPresses,
    long MouseClicks,
    string? PrimaryApplication);

public sealed record AnalysisCostGate(
    bool Allowed,
    string? Reason,
    decimal EstimatedCostUsd,
    int DailyAnalysisCount,
    decimal ProjectedDailyCostUsd);

public sealed record DashboardState(
    string StatusLabel,
    string CurrentContext,
    long TotalKeyPresses,
    long TotalMouseClicks,
    long ActiveSeconds,
    double Intensity,
    bool IsTracking,
    DateTimeOffset? LastSampleTimestamp);

public sealed record LastSessionState(
    DateTimeOffset? Timestamp,
    string Application,
    string Context,
    string InstallationId,
    IReadOnlyDictionary<string, string>? Attributes,
    string? ScreenshotPath);

public sealed record DiskSnapshotState(
    string Drive,
    string FileSystem,
    long TotalBytes,
    long FreeBytes);

public sealed record NetworkSnapshotState(long UploadBytesPerSecond, long DownloadBytesPerSecond);

public sealed record SystemSnapshot(
    DateTimeOffset Timestamp,
    int CpuUsagePercent,
    int? CpuTemperatureCelsius,
    int? GpuTemperatureCelsius,
    int? GpuUsagePercent,
    long MemoryUsedMb,
    long MemoryTotalMb,
    int? GpuMemoryUsedMb,
    NetworkSnapshotState Network,
    IReadOnlyList<DiskSnapshotState> Disks);

public sealed record AnalysisContextSnapshot(
    string Application,
    string Context,
    string WindowTitle,
    string State,
    IReadOnlyDictionary<string, string>? Attributes,
    SystemSnapshot? Snapshot = null);
