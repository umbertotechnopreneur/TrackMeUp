using System.Globalization;
using TrackMeUp.Services;

namespace TrackMeUp.Application;

/// <summary>Describes one public, non-secret setting that frontends may inspect or update.</summary>
public sealed record SettingDescriptor(
    string Key,
    string ValueType,
    string Description,
    bool RequiresRestart,
    IReadOnlyList<string> AllowedValues);

/// <summary>Provides the single public settings catalog and deterministic validation used by every frontend.</summary>
public static class SettingsCatalog
{
    private static readonly string[] BooleanValues = ["true", "false"];
    private static readonly string[] Providers = ["openai", "openrouter", "anthropic"];
    private static readonly string[] ApiKeyVariables = ["OPENAI_API_KEY", "TRACKMEUP_OPENAI_APIKEY", "OPENROUTER_API_KEY", "ANTHROPIC_API_KEY"];
    private static readonly string[] OutputDetails = ["compact", "balanced", "detailed"];
    private static readonly string[] ReasoningEfforts = ["auto", "none", "low", "medium", "high", "xhigh", "max"];
    private static readonly string[] Languages = ["system", "en", "it", "vi", "fr", "de", "es"];
    private static readonly string[] Themes = ["system", "light", "dark"];
    private static readonly string[] ScreenshotModes = ["all-screens", "active-window"];
    private static readonly string[] FlyoutAnchors = [FlyoutPositions.BottomCenter, FlyoutPositions.BottomLeft, FlyoutPositions.BottomRight, FlyoutPositions.TopLeft, FlyoutPositions.TopRight];
    private static readonly string[] TaskbarAnchors = [TaskbarWidgetPositions.Left, TaskbarWidgetPositions.Right];

    /// <summary>Gets all settings that are safe to expose and writable through WinUI or CLI.</summary>
    public static IReadOnlyList<SettingDescriptor> Definitions { get; } =
    [
        Boolean("screenshots.enabled", "Allow application-initiated screenshot capture."),
        Boolean("screenshots.keep", "Keep screenshots after analysis."),
        Boolean("screenshots.watermark", "Add the local audit watermark to retained screenshots."),
        Choice("screenshots.mode", "Select all displays or only the active window.", ScreenshotModes),
        Text("screenshots.directory", "Directory used for TrackMeUp screenshot artifacts.", "path"),
        Integer("screenshots.interval_minutes", "Minutes between scheduled eligible screenshots."),
        Boolean("ai.enabled", "Analyze every captured snapshot after privacy and cost checks."),
        Choice("ai.provider", "AI provider used for screenshot analysis.", Providers),
        Text("ai.model", "Provider model identifier."),
        Text("ai.endpoint", "HTTPS provider endpoint; loopback HTTP is allowed for local testing.", "uri"),
        Choice("ai.key_variable", "User environment variable that contains the API key.", ApiKeyVariables),
        Choice("ai.output_detail", "Screenshot analysis output and token-budget profile.", OutputDetails),
        Choice("ai.reasoning_effort", "OpenAI Responses reasoning effort; auto omits the field.", ReasoningEfforts),
        Text("ai.custom_prompt", "Optional user instruction appended after the built-in screenshot prompt; empty keeps only the built-in prompt.", "multiline"),
        Boolean("ai.include_device_location", "Include Windows-provided latitude and longitude in AI snapshots only when location access is available."),
        Integer("ai.daily_limit", "Maximum AI analyses per local day."),
        Decimal("ai.estimated_cost_per_analysis_usd", "Estimated cost used by the local guardrail."),
        Decimal("ai.estimated_cost_per_screenshot_usd", "Estimated screenshot cost used by the local guardrail."),
        Boolean("ai.show_cost_guardrail", "Include local cost guardrail state in status output."),
        Choice("language", "Application language.", Languages, requiresRestart: true),
        Choice("theme", "Application color theme.", Themes),
        Choice("position", "Player flyout anchor.", FlyoutAnchors),
        Boolean("taskbar.widget.visible", "Show the compact control in the Windows taskbar."),
        Choice("taskbar.widget.position", "Taskbar control anchor.", TaskbarAnchors),
        Text("activity.span_label", "Short local activity label, limited to 20 characters."),
        Text("active_hours.monday.active", "Informational Monday active period in HH:mm-HH:mm format.", "time_range"),
        Text("active_hours.monday.breaks", "Informational Monday breaks, comma-separated HH:mm-HH:mm ranges.", "time_ranges"),
        Text("active_hours.tuesday.active", "Informational Tuesday active period in HH:mm-HH:mm format.", "time_range"),
        Text("active_hours.tuesday.breaks", "Informational Tuesday breaks, comma-separated HH:mm-HH:mm ranges.", "time_ranges"),
        Text("active_hours.wednesday.active", "Informational Wednesday active period in HH:mm-HH:mm format.", "time_range"),
        Text("active_hours.wednesday.breaks", "Informational Wednesday breaks, comma-separated HH:mm-HH:mm ranges.", "time_ranges"),
        Text("active_hours.thursday.active", "Informational Thursday active period in HH:mm-HH:mm format.", "time_range"),
        Text("active_hours.thursday.breaks", "Informational Thursday breaks, comma-separated HH:mm-HH:mm ranges.", "time_ranges"),
        Text("active_hours.friday.active", "Informational Friday active period in HH:mm-HH:mm format.", "time_range"),
        Text("active_hours.friday.breaks", "Informational Friday breaks, comma-separated HH:mm-HH:mm ranges.", "time_ranges"),
        Text("active_hours.saturday.active", "Informational Saturday active period in HH:mm-HH:mm format.", "time_range"),
        Text("active_hours.saturday.breaks", "Informational Saturday breaks, comma-separated HH:mm-HH:mm ranges.", "time_ranges"),
        Text("active_hours.sunday.active", "Informational Sunday active period in HH:mm-HH:mm format.", "time_range"),
        Text("active_hours.sunday.breaks", "Informational Sunday breaks, comma-separated HH:mm-HH:mm ranges.", "time_ranges"),
        Boolean("startup.enabled", "Start TrackMeUp after Windows sign-in."),
        Boolean("tracking.start_on_launch", "Start tracking on the next application launch.", requiresRestart: true),
        Integer("retention.screenshots_days", "Days to retain TrackMeUp-owned screenshot artifacts."),
        Integer("retention.data_days", "Days to retain completed local activity files."),
        Boolean("digest.enabled", "Enable daily digest generation."),
        Text("digest.directory", "Optional daily digest output directory.", "path"),
        Boolean("focus.summary_enabled", "Generate a summary when a focus session stops."),
        Boolean("plugins.word.enabled", "Enable safe Microsoft Word context details."),
        Boolean("plugins.excel.enabled", "Enable safe Microsoft Excel context details."),
        Boolean("plugins.vscode.enabled", "Enable safe Visual Studio Code context details."),
        Boolean("plugins.browser.enabled", "Enable safe browser title context details.")
    ];

    /// <summary>Gets a safe setting value by its public key without using reflection.</summary>
    public static bool TryGetValue(AppSettings settings, string key, out object? value)
    {
        var normalizedKey = key.Trim().ToLowerInvariant();
        if (TryGetActiveHoursValue(settings, normalizedKey, out value))
        {
            return true;
        }

        value = normalizedKey switch
        {
            "screenshots.enabled" => settings.ScreenshotsEnabled,
            "screenshots.keep" => settings.KeepScreenshots,
            "screenshots.watermark" => settings.WatermarkScreenshots,
            "screenshots.mode" => settings.ScreenshotCaptureMode,
            "screenshots.directory" => settings.ScreenshotDirectory,
            "screenshots.interval_minutes" => settings.ScreenshotIntervalMinutes,
            "ai.enabled" => settings.OpenAiEnabled,
            "ai.provider" => settings.AiProvider,
            "ai.model" => settings.Model,
            "ai.endpoint" => settings.AiEndpoint,
            "ai.key_variable" => settings.AiApiKeyName,
            "ai.output_detail" => settings.AiOutputDetail,
            "ai.reasoning_effort" => settings.AiReasoningEffort,
            "ai.custom_prompt" => settings.AiCustomPrompt,
            "ai.include_device_location" => settings.IncludeDeviceLocation,
            "ai.daily_limit" => settings.OpenAiDailyLimit,
            "ai.estimated_cost_per_analysis_usd" => settings.EstimatedCostPerAnalysisUsd,
            "ai.estimated_cost_per_screenshot_usd" => settings.EstimatedCostPerScreenshotUsd,
            "ai.show_cost_guardrail" => settings.ShowCostGuardrailInStatus,
            "language" => settings.UiLanguage,
            "theme" => settings.Theme,
            "position" => settings.FlyoutPosition,
            "taskbar.widget.visible" => settings.TaskbarWidgetVisible,
            "taskbar.widget.position" => settings.TaskbarWidgetPosition,
            "activity.span_label" => settings.SpanLabel,
            "startup.enabled" => settings.StartWithWindows,
            "tracking.start_on_launch" => settings.StartTrackingOnLaunch,
            "retention.screenshots_days" => settings.ScreenshotRetentionDays,
            "retention.data_days" => settings.DataRetentionDays,
            "digest.enabled" => settings.DailyDigestEnabled,
            "digest.directory" => settings.DailyDigestDirectory,
            "focus.summary_enabled" => settings.FocusSessionSummaryEnabled,
            "plugins.word.enabled" => settings.EnableWordDetailPlugin,
            "plugins.excel.enabled" => settings.EnableExcelDetailPlugin,
            "plugins.vscode.enabled" => settings.EnableVsCodeDetailPlugin,
            "plugins.browser.enabled" => settings.EnableBrowserDetailPlugin,
            _ => null
        };

        return Definitions.Any(definition => definition.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Validates a patch as one transaction and returns a new settings snapshot without performing I/O.</summary>
    public static OperationResult<AppSettings> Apply(AppSettings settings, SettingsPatch patch)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(patch.Values);

        var issues = new List<ValidationIssue>();
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in patch.Values)
        {
            var key = pair.Key?.Trim();
            if (string.IsNullOrWhiteSpace(key) || !values.TryAdd(key, pair.Value))
            {
                issues.Add(Invalid(key ?? "settings"));
            }
        }

        var current = settings;
        if (values.TryGetValue("ai.provider", out var providerValue))
        {
            var provider = providerValue?.Trim().ToLowerInvariant();
            if (provider is not null && Providers.Contains(provider, StringComparer.Ordinal))
            {
                current = current with
                {
                    AiProvider = provider,
                    AiEndpoint = GetDefaultEndpoint(provider),
                    AiApiKeyName = GetDefaultApiKeyVariable(provider)
                };
            }
            else
            {
                issues.Add(Invalid("ai.provider"));
            }
        }

        foreach (var (rawKey, rawValue) in values)
        {
            var key = rawKey.ToLowerInvariant();
            if (key == "ai.provider")
            {
                continue;
            }

            var value = rawValue?.Trim();
            if (TryParseActiveHoursKey(key, out var day, out var isBreaks))
            {
                var valid = isBreaks
                    ? ActiveHoursSchedule.TryNormalizeBreakPeriods(value, out var normalizedPeriods)
                    : ActiveHoursSchedule.TryNormalizeActivePeriod(value, out normalizedPeriods);
                if (valid)
                {
                    current = current with { ActiveHours = ActiveHoursSchedule.Update(current.ActiveHours, day, isBreaks, normalizedPeriods) };
                }
                else
                {
                    issues.Add(Invalid(rawKey));
                }

                continue;
            }

            switch (key)
            {
                case "screenshots.enabled" when TryBoolean(value, out var screenshots): current = current with { ScreenshotsEnabled = screenshots }; break;
                case "screenshots.keep" when TryBoolean(value, out var keep): current = current with { KeepScreenshots = keep }; break;
                case "screenshots.watermark" when TryBoolean(value, out var watermark): current = current with { WatermarkScreenshots = watermark }; break;
                case "screenshots.mode" when Canonical(ScreenshotModes, value) is { } screenshotMode: current = current with { ScreenshotCaptureMode = screenshotMode }; break;
                case "screenshots.directory" when TryDirectory(value, allowEmpty: false, out var screenshotDirectory): current = current with { ScreenshotDirectory = screenshotDirectory }; break;
                case "screenshots.interval_minutes" when TryInteger(value, 1, 1440, out var screenshotIntervalMinutes): current = current with { ScreenshotIntervalMinutes = screenshotIntervalMinutes }; break;
                case "ai.enabled" when TryBoolean(value, out var enabled): current = current with { OpenAiEnabled = enabled }; break;
                case "ai.model" when !string.IsNullOrWhiteSpace(value) && value.Length <= 200: current = current with { Model = value }; break;
                case "ai.endpoint" when IsAllowedEndpoint(value): current = current with { AiEndpoint = value! }; break;
                case "ai.key_variable" when Canonical(ApiKeyVariables, value) is { } keyVariable: current = current with { AiApiKeyName = keyVariable }; break;
                case "ai.output_detail" when Contains(OutputDetails, value): current = current with { AiOutputDetail = value!.ToLowerInvariant() }; break;
                case "ai.reasoning_effort" when Contains(ReasoningEfforts, value): current = current with { AiReasoningEffort = value!.ToLowerInvariant() }; break;
                case "ai.custom_prompt" when TryNormalizeCustomPrompt(rawValue, out var customPrompt): current = current with { AiCustomPrompt = customPrompt }; break;
                case "ai.include_device_location" when TryBoolean(value, out var includeDeviceLocation): current = current with { IncludeDeviceLocation = includeDeviceLocation }; break;
                case "ai.daily_limit" when TryInteger(value, 0, 10_000, out var dailyLimit): current = current with { OpenAiDailyLimit = dailyLimit }; break;
                case "ai.estimated_cost_per_analysis_usd" when TryDecimal(value, 0m, 1_000m, out var analysisCost): current = current with { EstimatedCostPerAnalysisUsd = analysisCost }; break;
                case "ai.estimated_cost_per_screenshot_usd" when TryDecimal(value, 0m, 1_000m, out var screenshotCost): current = current with { EstimatedCostPerScreenshotUsd = screenshotCost }; break;
                case "ai.show_cost_guardrail" when TryBoolean(value, out var showGuardrail): current = current with { ShowCostGuardrailInStatus = showGuardrail }; break;
                case "language" when Canonical(Languages, value) is { } language: current = current with { UiLanguage = language }; break;
                case "theme" when Canonical(Themes, value) is { } theme: current = current with { Theme = theme }; break;
                case "position" when Canonical(FlyoutAnchors, value) is { } position: current = current with { FlyoutPosition = position }; break;
                case "taskbar.widget.visible" when TryBoolean(value, out var taskbarVisible): current = current with { TaskbarWidgetVisible = taskbarVisible }; break;
                case "taskbar.widget.position" when Canonical(TaskbarAnchors, value) is { } taskbarPosition: current = current with { TaskbarWidgetPosition = taskbarPosition }; break;
                case "activity.span_label" when value is not null && value.Length <= 20: current = current with { SpanLabel = value }; break;
                case "startup.enabled" when TryBoolean(value, out var startup): current = current with { StartWithWindows = startup }; break;
                case "tracking.start_on_launch" when TryBoolean(value, out var startOnLaunch): current = current with { StartTrackingOnLaunch = startOnLaunch }; break;
                case "retention.screenshots_days" when TryInteger(value, 0, 3650, out var screenshotDays): current = current with { ScreenshotRetentionDays = screenshotDays }; break;
                case "retention.data_days" when TryInteger(value, 0, 3650, out var dataDays): current = current with { DataRetentionDays = dataDays }; break;
                case "digest.enabled" when TryBoolean(value, out var digestEnabled): current = current with { DailyDigestEnabled = digestEnabled }; break;
                case "digest.directory" when TryDirectory(value, allowEmpty: true, out var digestDirectory): current = current with { DailyDigestDirectory = digestDirectory }; break;
                case "focus.summary_enabled" when TryBoolean(value, out var focusSummary): current = current with { FocusSessionSummaryEnabled = focusSummary }; break;
                case "plugins.word.enabled" when TryBoolean(value, out var word): current = current with { EnableWordDetailPlugin = word }; break;
                case "plugins.excel.enabled" when TryBoolean(value, out var excel): current = current with { EnableExcelDetailPlugin = excel }; break;
                case "plugins.vscode.enabled" when TryBoolean(value, out var vscode): current = current with { EnableVsCodeDetailPlugin = vscode }; break;
                case "plugins.browser.enabled" when TryBoolean(value, out var browser): current = current with { EnableBrowserDetailPlugin = browser }; break;
                default: issues.Add(Invalid(rawKey)); break;
            }
        }

        if (!ActiveHoursSchedule.IsValid(current.ActiveHours))
        {
            issues.Add(Invalid("active_hours"));
        }
        else
        {
            current = current with { ActiveHours = ActiveHoursSchedule.Normalize(current.ActiveHours) };
        }

        return issues.Count == 0
            ? OperationResult<AppSettings>.Success("settings.validated", "SettingsValidated", current)
            : new OperationResult<AppSettings>(false, "settings.validation.failed", "SettingsValidationFailed", null, issues);
    }

    /// <summary>Normalizes untrusted persisted settings to supported, safe values before application use.</summary>
    public static AppSettings NormalizePersisted(AppSettings settings, string defaultScreenshotDirectory)
    {
        var provider = Canonical(Providers, settings.AiProvider) ?? "openai";
        var endpoint = IsAllowedEndpoint(settings.AiEndpoint) ? settings.AiEndpoint.Trim() : GetDefaultEndpoint(provider);
        var keyVariable = Canonical(ApiKeyVariables, settings.AiApiKeyName) ?? GetDefaultApiKeyVariable(provider);
        var screenshotDirectory = TryDirectory(settings.ScreenshotDirectory, allowEmpty: false, out var normalizedScreenshotDirectory)
            ? normalizedScreenshotDirectory
            : Path.GetFullPath(defaultScreenshotDirectory);
        var digestDirectory = TryDirectory(settings.DailyDigestDirectory, allowEmpty: true, out var normalizedDigestDirectory)
            ? normalizedDigestDirectory
            : string.Empty;

        return settings with
        {
            Model = string.IsNullOrWhiteSpace(settings.Model) || settings.Model.Trim().Length > 200 ? "gpt-5.6" : settings.Model.Trim(),
            ScreenshotDirectory = screenshotDirectory,
            ScreenshotCaptureMode = Canonical(ScreenshotModes, settings.ScreenshotCaptureMode) ?? "all-screens",
            ScreenshotIntervalMinutes = settings.ScreenshotIntervalMinutes <= 0
                ? 15
                : Math.Min(settings.ScreenshotIntervalMinutes, 1440),
            AiProvider = provider,
            AiEndpoint = endpoint,
            AiApiKeyName = keyVariable,
            AiOutputDetail = Canonical(OutputDetails, settings.AiOutputDetail) ?? "balanced",
            AiReasoningEffort = Canonical(ReasoningEfforts, settings.AiReasoningEffort) ?? "auto",
            AiCustomPrompt = TryNormalizeCustomPrompt(settings.AiCustomPrompt, out var customPrompt) ? customPrompt : string.Empty,
            FlyoutPosition = Canonical(FlyoutAnchors, settings.FlyoutPosition) ?? FlyoutPositions.BottomCenter,
            UiLanguage = Canonical(Languages, settings.UiLanguage) ?? "system",
            Theme = Canonical(Themes, settings.Theme) ?? "system",
            TaskbarWidgetPosition = Canonical(TaskbarAnchors, settings.TaskbarWidgetPosition) ?? TaskbarWidgetPositions.Left,
            SpanLabel = settings.SpanLabel is { Length: <= 20 } ? settings.SpanLabel.Trim() : string.Empty,
            DailyDigestDirectory = digestDirectory,
            DataRetentionDays = Math.Clamp(settings.DataRetentionDays, 0, 3650),
            ScreenshotRetentionDays = Math.Clamp(settings.ScreenshotRetentionDays, 0, 3650),
            OpenAiDailyLimit = Math.Clamp(settings.OpenAiDailyLimit, 0, 10_000),
            OpenAiDailyCostUsd = Math.Max(0m, settings.OpenAiDailyCostUsd),
            EstimatedCostPerAnalysisUsd = Math.Clamp(settings.EstimatedCostPerAnalysisUsd, 0m, 1_000m),
            EstimatedCostPerScreenshotUsd = Math.Clamp(settings.EstimatedCostPerScreenshotUsd, 0m, 1_000m),
            ActiveHours = ActiveHoursSchedule.Normalize(settings.ActiveHours)
        };
    }

    /// <summary>Returns the built-in endpoint for a supported AI provider.</summary>
    public static string GetDefaultEndpoint(string? provider) => provider?.ToLowerInvariant() switch
    {
        "openrouter" => "https://openrouter.ai/api/v1/chat/completions",
        "anthropic" => "https://api.anthropic.com/v1/messages",
        _ => "https://api.openai.com/v1/responses"
    };

    /// <summary>Returns the built-in API-key environment variable for a supported AI provider.</summary>
    public static string GetDefaultApiKeyVariable(string? provider) => provider?.ToLowerInvariant() switch
    {
        "openrouter" => "OPENROUTER_API_KEY",
        "anthropic" => "ANTHROPIC_API_KEY",
        _ => "OPENAI_API_KEY"
    };

    /// <summary>Returns whether an environment-variable name is permitted for secret storage.</summary>
    public static bool IsAllowedApiKeyVariable(string? value) => Contains(ApiKeyVariables, value);

    private static SettingDescriptor Boolean(string key, string description, bool requiresRestart = false) => new(key, "boolean", description, requiresRestart, BooleanValues);

    private static SettingDescriptor Choice(string key, string description, IReadOnlyList<string> values, bool requiresRestart = false) => new(key, "choice", description, requiresRestart, values);

    private static SettingDescriptor Integer(string key, string description) => new(key, "integer", description, false, Array.Empty<string>());

    private static SettingDescriptor Decimal(string key, string description) => new(key, "decimal", description, false, Array.Empty<string>());

    private static SettingDescriptor Text(string key, string description, string valueType = "string") => new(key, valueType, description, false, Array.Empty<string>());

    private static ValidationIssue Invalid(string key) => new(key, "not_allowed_or_invalid", "SettingsFieldInvalid");

    private static bool Contains(IEnumerable<string> values, string? value) => value is not null && values.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static string? Canonical(IEnumerable<string> values, string? value) =>
        value is null ? null : values.FirstOrDefault(candidate => candidate.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool TryBoolean(string? value, out bool parsed)
    {
        if (bool.TryParse(value, out parsed))
        {
            return true;
        }

        parsed = value?.ToLowerInvariant() switch
        {
            "1" or "yes" or "on" => true,
            "0" or "no" or "off" => false,
            _ => false
        };
        return value?.ToLowerInvariant() is "1" or "yes" or "on" or "0" or "no" or "off";
    }

    private static bool TryInteger(string? value, int minimum, int maximum, out int parsed) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed >= minimum && parsed <= maximum;

    private static bool TryDecimal(string? value, decimal minimum, decimal maximum, out decimal parsed) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed) && parsed >= minimum && parsed <= maximum;

    private static bool TryDirectory(string? value, bool allowEmpty, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return allowEmpty;
        }

        try
        {
            path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim()));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool TryGetActiveHoursValue(AppSettings settings, string key, out object? value)
    {
        value = null;
        if (!TryParseActiveHoursKey(key, out var day, out var isBreaks))
        {
            return false;
        }

        var entry = ActiveHoursSchedule.Normalize(settings.ActiveHours)
            .Single(candidate => string.Equals(candidate.Day, day, StringComparison.Ordinal));
        value = isBreaks ? entry.BreakPeriods : entry.ActivePeriod;
        return true;
    }

    private static bool TryParseActiveHoursKey(string key, out string day, out bool isBreaks)
    {
        foreach (var candidate in ActiveHoursSchedule.Days)
        {
            if (key.Equals($"active_hours.{candidate}.active", StringComparison.OrdinalIgnoreCase))
            {
                day = candidate;
                isBreaks = false;
                return true;
            }

            if (key.Equals($"active_hours.{candidate}.breaks", StringComparison.OrdinalIgnoreCase))
            {
                day = candidate;
                isBreaks = true;
                return true;
            }
        }

        day = string.Empty;
        isBreaks = false;
        return false;
    }

    private static bool TryNormalizeCustomPrompt(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= 6_000 && normalized.IndexOf('\0') < 0;
    }

    private static bool IsAllowedEndpoint(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) || !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            return false;
        }

        return endpoint.Scheme == Uri.UriSchemeHttps || (endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback);
    }
}
