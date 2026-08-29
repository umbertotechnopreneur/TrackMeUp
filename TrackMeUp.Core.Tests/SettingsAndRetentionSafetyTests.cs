using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class SettingsAndRetentionSafetyTests
{
    [Fact]
    public void Apply_UsesOneTransactionalCatalogForAiTuning()
    {
        var original = new AppSettings(AiProvider: "anthropic");
        var patch = new SettingsPatch(new Dictionary<string, string?>
        {
            ["ai.provider"] = "OpenAI",
            ["ai.model"] = "gpt-5.6",
            ["ai.output_detail"] = "DETAILED",
            ["ai.reasoning_effort"] = "XHIGH"
        });

        var result = SettingsCatalog.Apply(original, patch);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal("openai", result.Value.AiProvider);
        Assert.Equal("OPENAI_API_KEY", result.Value.AiApiKeyName);
        Assert.Equal("https://api.openai.com/v1/responses", result.Value.AiEndpoint);
        Assert.Equal("detailed", result.Value.AiOutputDetail);
        Assert.Equal("xhigh", result.Value.AiReasoningEffort);
    }

    [Fact]
    public void Apply_RejectsTheWholePatchWhenOneValueIsInvalid()
    {
        var original = new AppSettings(Theme: "system");
        var patch = new SettingsPatch(new Dictionary<string, string?>
        {
            ["theme"] = "dark",
            ["ai.endpoint"] = "http://remote.example.invalid/v1"
        });

        var result = SettingsCatalog.Apply(original, patch);

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.Equal("system", original.Theme);
        Assert.Contains(result.Issues, issue => issue.Field == "ai.endpoint");
    }

    [Fact]
    public void Apply_PersistsCustomPromptAndInformationalWeeklyHours()
    {
        var result = SettingsCatalog.Apply(
            new AppSettings(),
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["ai.custom_prompt"] = "Prioritize short summaries.",
                ["active_hours.monday.active"] = "09:00-18:00",
                ["active_hours.monday.breaks"] = "13:00-14:00, 16:30-16:45",
                ["ai.include_device_location"] = "true"
            }));

        Assert.True(result.Succeeded);
        var settings = Assert.IsType<AppSettings>(result.Value);
        Assert.Equal("Prioritize short summaries.", settings.AiCustomPrompt);
        Assert.True(settings.IncludeDeviceLocation);
        var monday = Assert.Single(settings.ActiveHours!, day => day.Day == "monday");
        Assert.Equal("09:00-18:00", monday.ActivePeriod);
        Assert.Equal("13:00-14:00, 16:30-16:45", monday.BreakPeriods);
    }

    [Fact]
    public void Apply_PersistsScheduledScreenshotInterval()
    {
        var result = SettingsCatalog.Apply(
            new AppSettings(),
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["screenshots.interval_minutes"] = "15"
            }));

        Assert.True(result.Succeeded);
        Assert.Equal(15, result.Value?.ScreenshotIntervalMinutes);
    }

    [Fact]
    public void ScreenshotDetailsPanePreference_RoundTripsThroughTheSettingsCatalog()
    {
        var defaults = Assert.IsType<AppSettings>(
            JsonSerializer.Deserialize<AppSettings>("{}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var opened = SettingsCatalog.Apply(
            defaults,
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["screenshots.details_pane_open"] = "true"
            }));

        Assert.False(defaults.ScreenshotDetailsPaneOpen);
        Assert.True(opened.Succeeded);
        var openSettings = Assert.IsType<AppSettings>(opened.Value);
        Assert.True(openSettings.ScreenshotDetailsPaneOpen);
        Assert.True(SettingsCatalog.TryGetValue(openSettings, "screenshots.details_pane_open", out var storedPreference));
        Assert.Equal(true, storedPreference);

        var restored = Assert.IsType<AppSettings>(JsonSerializer.Deserialize<AppSettings>(
            JsonSerializer.Serialize(openSettings, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.True(restored.ScreenshotDetailsPaneOpen);

        var closed = SettingsCatalog.Apply(
            restored,
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["screenshots.details_pane_open"] = "false"
            }));
        Assert.True(closed.Succeeded);
        Assert.False(closed.Value?.ScreenshotDetailsPaneOpen);

        var invalid = SettingsCatalog.Apply(
            defaults,
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["screenshots.details_pane_open"] = "yes"
            }));
        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Issues, issue => issue.Field == "screenshots.details_pane_open");
    }

    [Fact]
    public void AiMonthlySpendPreference_IsOptInAndRoundTripsThroughTheSettingsCatalog()
    {
        var defaults = Assert.IsType<AppSettings>(
            JsonSerializer.Deserialize<AppSettings>("{}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var enabled = SettingsCatalog.Apply(
            defaults,
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["ai.show_monthly_spend"] = "true"
            }));

        Assert.False(defaults.ShowAiMonthlySpend);
        Assert.True(enabled.Succeeded);
        var settings = Assert.IsType<AppSettings>(enabled.Value);
        Assert.True(settings.ShowAiMonthlySpend);
        Assert.True(SettingsCatalog.TryGetValue(settings, "ai.show_monthly_spend", out var storedPreference));
        Assert.Equal(true, storedPreference);

        var restored = Assert.IsType<AppSettings>(JsonSerializer.Deserialize<AppSettings>(
            JsonSerializer.Serialize(settings, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.True(restored.ShowAiMonthlySpend);

        var disabled = SettingsCatalog.Apply(
            restored,
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["ai.show_monthly_spend"] = "false"
            }));
        Assert.True(disabled.Succeeded);
        Assert.False(disabled.Value?.ShowAiMonthlySpend);

        var invalid = SettingsCatalog.Apply(
            defaults,
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["ai.show_monthly_spend"] = "yes"
            }));
        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Issues, issue => issue.Field == "ai.show_monthly_spend");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("on")]
    [InlineData("off")]
    [InlineData("TRUE")]
    [InlineData("False")]
    public void Apply_RejectsNonCanonicalBooleanValues(string value)
    {
        var result = SettingsCatalog.Apply(
            new AppSettings(),
            new SettingsPatch(new Dictionary<string, string?> { ["screenshots.enabled"] = value }));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Field == "screenshots.enabled");
    }

    [Fact]
    public void Apply_RestrictsDailyAiProcessingLimitToFourHundred()
    {
        var accepted = SettingsCatalog.Apply(
            new AppSettings(),
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["ai.daily_limit"] = SettingsCatalog.MaximumAiDailyLimit.ToString()
            }));
        var rejected = SettingsCatalog.Apply(
            new AppSettings(),
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["ai.daily_limit"] = (SettingsCatalog.MaximumAiDailyLimit + 1).ToString()
            }));

        Assert.True(accepted.Succeeded);
        Assert.Equal(400, accepted.Value?.OpenAiDailyLimit);
        Assert.False(rejected.Succeeded);
        Assert.Contains(rejected.Issues, issue => issue.Field == "ai.daily_limit");
    }

    [Fact]
    public void SettingsWithoutAnInterval_UseTheFifteenMinuteScheduleDefault()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{}", new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(15, settings?.ScreenshotIntervalMinutes);
    }

    [Fact]
    public void Apply_PersistsTheBoundedLocalSpanLabel()
    {
        var result = SettingsCatalog.Apply(
            new AppSettings(),
            new SettingsPatch(new Dictionary<string, string?> { ["activity.span_label"] = "  Project brief  " }));

        var settings = Assert.IsType<AppSettings>(result.Value);
        Assert.Equal("Project brief", settings.SpanLabel);
        Assert.True(SettingsCatalog.TryGetValue(settings, "activity.span_label", out var storedLabel));
        Assert.Equal("Project brief", storedLabel);

        var invalid = SettingsCatalog.Apply(
            settings,
            new SettingsPatch(new Dictionary<string, string?> { ["activity.span_label"] = new string('x', 21) }));
        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Issues, issue => issue.Field == "activity.span_label");
    }

    [Fact]
    public void TaskbarWidget_IsHiddenByDefaultAndCanBeEnabledAtAValidatedPosition()
    {
        var defaults = Assert.IsType<AppSettings>(JsonSerializer.Deserialize<AppSettings>("{}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var result = SettingsCatalog.Apply(
            defaults,
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["taskbar.widget.visible"] = "true",
                ["taskbar.widget.position"] = "right"
            }));

        Assert.False(defaults.TaskbarWidgetVisible);
        Assert.True(result.Succeeded);
        var settings = Assert.IsType<AppSettings>(result.Value);
        Assert.True(settings.TaskbarWidgetVisible);
        Assert.Equal(TaskbarWidgetPositions.Right, settings.TaskbarWidgetPosition);
        Assert.True(SettingsCatalog.TryGetValue(settings, "taskbar.widget.visible", out var visible));
        Assert.Equal(true, visible);

        var invalidPosition = SettingsCatalog.Apply(
            settings,
            new SettingsPatch(new Dictionary<string, string?> { ["taskbar.widget.position"] = "center" }));
        Assert.False(invalidPosition.Succeeded);
        Assert.Contains(invalidPosition.Issues, issue => issue.Field == "taskbar.widget.position");
    }

    [Fact]
    public void Apply_RejectsBreakOutsideItsInformationalActivePeriod()
    {
        var result = SettingsCatalog.Apply(
            new AppSettings(),
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["active_hours.monday.active"] = "09:00-18:00",
                ["active_hours.monday.breaks"] = "19:00-19:30"
            }));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Field == "active_hours");
    }

    [Fact]
    public void InformationalSchedule_UsesTheDeviceLocalWeekdayForUtcSnapshots()
    {
        var deviceTimeZone = TimeZoneInfo.CreateCustomTimeZone("test-device", TimeSpan.FromHours(7), "Test", "Test");
        var note = ActiveHoursSchedule.BuildInformationalNote(
            [new ActiveHoursDay("monday", "09:00-18:00", "13:00-14:00")],
            new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero),
            deviceTimeZone);

        Assert.Equal("Monday: planned active hours 09:00-18:00; planned breaks 13:00-14:00. This is informational only.", note);
    }

    [Fact]
    public void NormalizePersisted_ClampsAndReplacesUnsupportedValues()
    {
        var normalized = SettingsCatalog.NormalizePersisted(
            new AppSettings(
                AiProvider: "unsupported",
                AiEndpoint: "http://remote.example.invalid/v1",
                AiApiKeyName: "SECRET_FROM_SETTINGS",
                AiOutputDetail: "unbounded",
                AiReasoningEffort: "extreme",
                OpenAiDailyLimit: 10_000,
                ScreenshotIntervalMinutes: 50_000,
                DataRetentionDays: -50,
                ScreenshotRetentionDays: 50_000),
            Path.Combine(Path.GetTempPath(), "TrackMeUp", "screenshots"));

        Assert.Equal("openai", normalized.AiProvider);
        Assert.Equal("https://api.openai.com/v1/responses", normalized.AiEndpoint);
        Assert.Equal("OPENAI_API_KEY", normalized.AiApiKeyName);
        Assert.Equal("balanced", normalized.AiOutputDetail);
        Assert.Equal("auto", normalized.AiReasoningEffort);
        Assert.Equal(400, normalized.OpenAiDailyLimit);
        Assert.Equal(1440, normalized.ScreenshotIntervalMinutes);
        Assert.Equal(0, normalized.DataRetentionDays);
        Assert.Equal(3650, normalized.ScreenshotRetentionDays);
    }

    [Fact]
    public void NormalizePersisted_UsesAllDayEveryDayOnFirstRun()
    {
        var normalized = SettingsCatalog.NormalizePersisted(
            new AppSettings(),
            Path.Combine(Path.GetTempPath(), "TrackMeUp", "screenshots"));

        Assert.All(normalized.ActiveHours!, day =>
        {
            Assert.Equal("00:00-24:00", day.ActivePeriod);
            Assert.Empty(day.BreakPeriods);
        });
    }

    [Fact]
    public void ActiveHours_AllDayEndBoundaryIncludesTheFinalSlot()
    {
        var schedule = ActiveHoursSchedule.Normalize(null);

        Assert.True(ActiveHoursSchedule.HasAnyActivePeriod(schedule));
        Assert.True(ActiveHoursSchedule.IsWithinActiveHours(
            schedule,
            new DateTimeOffset(2026, 8, 3, 23, 59, 0, TimeSpan.FromHours(7))));
    }

    [Fact]
    public void NormalizePersisted_PreservesAnExplicitlyClearedSchedule()
    {
        var cleared = ActiveHoursSchedule.Days.Select(day => new ActiveHoursDay(day)).ToArray();
        var normalized = SettingsCatalog.NormalizePersisted(
            new AppSettings(ActiveHours: cleared),
            Path.Combine(Path.GetTempPath(), "TrackMeUp", "screenshots"));

        Assert.False(ActiveHoursSchedule.HasAnyActivePeriod(normalized.ActiveHours));
        Assert.All(normalized.ActiveHours!, day => Assert.Empty(day.ActivePeriod));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef_1.2.3_manual_monitor-1.webp", true)]
    [InlineData("0123456789abcdef0123456789abcdef_1.2.3_scheduled_active-window-raw.webp", true)]
    [InlineData("0123456789abcdef0123456789abcdef_1.2.3_manual_monitor-2.png", true)]
    [InlineData("0123456789abcdef0123456789abcdef_1.2.3_monitor-1.webp", false)]
    [InlineData("family-photo.webp", false)]
    [InlineData("0123456789abcdef0123456789abcdef_notes.webp", false)]
    [InlineData("0123456789abcdef0123456789abcdef_1.2.3_monitor-0.webp", false)]
    public void ScreenshotOwnership_IsFailClosed(string fileName, bool expected)
    {
        Assert.Equal(expected, ScreenCaptureService.IsOwnedArtifact(fileName));
    }

    [Fact]
    public void ScreenshotRetention_UsesPersistedCaptureTimeInsteadOfFileModificationTime()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalStore(dataDirectory);
            var settings = store.LoadSettings();
            var captureId = Guid.NewGuid().ToString("N");
            var screenshotPath = Path.Combine(
                dataDirectory,
                $"{captureId}_1.2.3_manual_monitor-1.webp");
            File.WriteAllBytes(screenshotPath, [1, 2, 3]);
            File.SetLastWriteTimeUtc(screenshotPath, DateTime.UtcNow);
            var capturedAt = DateTimeOffset.UtcNow.AddDays(-60);
            store.RegisterScreenshotCapture(
                captureId,
                settings.InstallationId,
                capturedAt,
                ScreenshotCaptureOrigins.Manual);

            var timestamps = store.LoadScreenshotCaptureTimes([screenshotPath], CancellationToken.None);

            Assert.Equal(capturedAt.UtcDateTime.Ticks, timestamps[screenshotPath].UtcDateTime.Ticks);
            Assert.True(File.GetLastWriteTimeUtc(screenshotPath) > capturedAt.UtcDateTime);
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ConcurrentFirstLaunch_UsesOneInstallationIdentity()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var loads = Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => new LocalStore(dataDirectory).LoadSettings().InstallationId));

            var installationIds = await Task.WhenAll(loads);
            var persisted = JsonSerializer.Deserialize<AppSettings>(
                await File.ReadAllTextAsync(Path.Combine(dataDirectory, "appsettings.json")),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.Single(installationIds.Distinct(StringComparer.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(installationIds[0]));
            Assert.Equal(installationIds[0], persisted?.InstallationId);
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Settings_FailFastWhenPersistedJsonIsMalformed()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(Path.Combine(dataDirectory, "appsettings.json"), "{ invalid json");

            Assert.Throws<JsonException>(() => new LocalStore(dataDirectory).LoadSettings());
            Assert.True(File.Exists(Path.Combine(dataDirectory, "appsettings.json")));
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void DataRetention_RemovesOnlyExpiredActivityRowsFromSQLite()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalStore(dataDirectory);
            store.AppendSample(Sample(DateTimeOffset.UtcNow.AddDays(-10), "expired"));
            store.AppendSample(Sample(DateTimeOffset.UtcNow, "current"));

            var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
            var databasePath = Path.Combine(dataDirectory, "activity.sqlite3");
            Assert.Contains(databasePath, store.GetRetentionCandidates(cutoff));
            var preview = store.GetRetentionPreview(cutoff);
            Assert.Equal(1, preview.RecordCount);
            Assert.True(preview.TotalBytes > 0);

            var removed = store.ApplyRetention(cutoff);

            Assert.Equal(1, removed);
            Assert.True(File.Exists(databasePath));
            Assert.False(File.Exists(Path.Combine(dataDirectory, "activity.jsonl")));
            Assert.Equal("current", store.LoadLatestSample()?.Context);
            Assert.Empty(store.GetRetentionCandidates(cutoff));
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }

        static ActivitySample Sample(DateTimeOffset timestamp, string context) => new(
            timestamp,
            5,
            "active",
            "test",
            "Test",
            context,
            "Test window",
            "test-installation",
            0,
            0);
    }

    [Fact]
    public void ActivityMonitor_PropagatesPersistenceFailures()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalStore(dataDirectory);
            using var hooks = new InputHookService();
            using var monitor = new ActivityMonitorService(store, hooks);
            using (var connection = new SqliteConnection($"Data Source={Path.Combine(dataDirectory, "activity.sqlite3")};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DROP TABLE activity_samples;";
                command.ExecuteNonQuery();
            }

            var sample = new ActivitySample(
                DateTimeOffset.UtcNow,
                5,
                "active",
                "test",
                "Test",
                "context",
                "window",
                "test-installation",
                0,
                0);

            Assert.Throws<SqliteException>(() => monitor.PersistSample(sample));
            Assert.Null(monitor.CurrentSample);
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

}
