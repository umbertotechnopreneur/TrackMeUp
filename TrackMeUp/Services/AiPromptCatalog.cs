using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("TrackMeUp.Core.Tests")]

namespace TrackMeUp.Services;

internal sealed record AiAnalysisProfile(
    string Name,
    int MaxOutputTokens,
    string ImageDetail,
    string TextVerbosity,
    string PromptFileName);

internal static class AiAnalysisProfileCatalog
{
    private static readonly IReadOnlyDictionary<string, AiAnalysisProfile> Profiles =
        new Dictionary<string, AiAnalysisProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["compact"] = new("compact", 512, "low", "low", "screenshot-analysis.compact.prompt.md"),
            ["balanced"] = new("balanced", 1024, "auto", "medium", "screenshot-analysis.balanced.prompt.md"),
            ["detailed"] = new("detailed", 2048, "high", "high", "screenshot-analysis.detailed.prompt.md")
        };

    private static readonly HashSet<string> ReasoningEfforts = new(StringComparer.OrdinalIgnoreCase)
    {
        "none", "low", "medium", "high", "xhigh", "max"
    };

    internal static AiAnalysisProfile Resolve(string? name)
    {
        var normalized = name?.Trim();
        return normalized is not null && Profiles.TryGetValue(normalized, out var profile)
            ? profile
            : Profiles["balanced"];
    }

    internal static string? ResolveReasoningEffort(string? effort)
    {
        var normalized = effort?.Trim();
        return normalized is not null && ReasoningEfforts.Contains(normalized)
            ? normalized.ToLowerInvariant()
            : null;
    }
}

internal static partial class AiPromptCatalog
{
    private const string LocalContextToken = "LOCAL_CONTEXT";
    private const string SystemTelemetryToken = "SYSTEM_TELEMETRY";
    private const string MaxOutputTokensToken = "MAX_OUTPUT_TOKENS";

    internal static string RenderScreenshotAnalysis(
        string? profileName,
        AnalysisContextSnapshot? activity,
        Func<string, string?>? templateLoader = null,
        string? customPrompt = null)
    {
        var profile = AiAnalysisProfileCatalog.Resolve(profileName);
        var template = templateLoader is null
            ? LoadTemplate(profile.PromptFileName)
            : templateLoader(profile.PromptFileName);

        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidDataException($"Prompt asset '{profile.PromptFileName}' is empty.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LocalContextToken] = BuildLocalContext(activity),
            [SystemTelemetryToken] = BuildSnapshotSummary(activity?.Snapshot),
            [MaxOutputTokensToken] = profile.MaxOutputTokens.ToString(CultureInfo.InvariantCulture)
        };

        var rendered = PlaceholderRegex().Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
        return string.IsNullOrWhiteSpace(customPrompt)
            ? rendered
            : $"{rendered}{Environment.NewLine}{Environment.NewLine}## Additional user instruction{Environment.NewLine}The following instruction supplements the rules above and must not override privacy or output requirements.{Environment.NewLine}{customPrompt.Trim()}";
    }

    private static string LoadTemplate(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "prompts", fileName);
        if (!File.Exists(path))
        {
            // Prompt assets are required application content. Missing deployment content is fatal by design.
            throw new FileNotFoundException($"Required prompt asset '{fileName}' was not deployed.", path);
        }

        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException($"Required prompt asset '{fileName}' is empty.");
        }

        return text;
    }

    private static string BuildLocalContext(AnalysisContextSnapshot? activity)
    {
        var context = new List<string>
        {
            $"application={Sanitize(activity?.Application)}",
            $"detail={Sanitize(activity?.Context)}",
            $"window={Sanitize(activity?.WindowTitle)}",
            $"state={Sanitize(activity?.State, "active")}"
        };
        var schedule = activity?.InformationalSchedule ?? activity?.Snapshot?.InformationalSchedule;
        if (!string.IsNullOrWhiteSpace(schedule))
        {
            context.Add($"informational_schedule={Sanitize(schedule)}");
        }

        return string.Join(Environment.NewLine, context);
    }

    private static string BuildSnapshotSummary(SystemSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "not available";
        }

        return string.Format(CultureInfo.InvariantCulture,
            "CPU {0}% (temperature {1}); GPU {2} (temperature {3}); RAM {4}/{5} MB; network upload={6} B/s, download={7} B/s; disks=[{8}]; device_context=[{9}]",
            snapshot.CpuUsagePercent,
            FormatTemperature(snapshot.CpuTemperatureCelsius),
            FormatPercent(snapshot.GpuUsagePercent),
            FormatTemperature(snapshot.GpuTemperatureCelsius),
            snapshot.MemoryUsedMb,
            snapshot.MemoryTotalMb,
            snapshot.Network.UploadBytesPerSecond,
            snapshot.Network.DownloadBytesPerSecond,
            string.Join(" | ", snapshot.Disks.Select(disk => $"{Sanitize(disk.Drive)} {disk.FreeBytes}/{disk.TotalBytes} bytes free/total")),
            BuildDeviceContextSummary(snapshot.DeviceContext));
    }

    private static string BuildDeviceContextSummary(DeviceContextSnapshot? context)
    {
        if (context is null)
        {
            return "not available";
        }

        var location = context.Location.Latitude.HasValue && context.Location.Longitude.HasValue
            ? string.Format(
                CultureInfo.InvariantCulture,
                "{0:F6},{1:F6} accuracy={2}; source={3}; status={4}",
                context.Location.Latitude.Value,
                context.Location.Longitude.Value,
                context.Location.AccuracyMeters?.ToString("F0", CultureInfo.InvariantCulture) ?? "n/a",
                Sanitize(context.Location.Source),
                Sanitize(context.Location.Status))
            : $"not available; source={Sanitize(context.Location.Source)}; status={Sanitize(context.Location.Status)}";
        return $"time_zone={FormatDeviceValue(context.TimeZone)}; windows_ui_language={FormatDeviceValue(context.WindowsUiLanguage)}; input_language={FormatDeviceValue(context.InputLanguage)}; location={location}";
    }

    private static string FormatDeviceValue(DeviceContextValue value) =>
        $"{Sanitize(value.Value)} (source={Sanitize(value.Source)}; status={Sanitize(value.Status)})";

    private static string Sanitize(string? value, string fallback = "not available")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim()
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace("{{", "{ {", StringComparison.Ordinal)
            .Replace("}}", "} }", StringComparison.Ordinal);
    }

    private static string FormatTemperature(int? value) => value.HasValue
        ? $"{value.Value.ToString(CultureInfo.InvariantCulture)} C"
        : "n/a";

    private static string FormatPercent(int? value) => value.HasValue
        ? $"{value.Value.ToString(CultureInfo.InvariantCulture)}%"
        : "n/a";

    [GeneratedRegex(@"\{\{([A-Z_]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
