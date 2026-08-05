using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
                DataRetentionDays: -50,
                ScreenshotRetentionDays: 50_000,
                AutomaticAnalysisIntervalMinutes: 0),
            Path.Combine(Path.GetTempPath(), "TrackMeUp", "screenshots"));

        Assert.Equal("openai", normalized.AiProvider);
        Assert.Equal("https://api.openai.com/v1/responses", normalized.AiEndpoint);
        Assert.Equal("OPENAI_API_KEY", normalized.AiApiKeyName);
        Assert.Equal("balanced", normalized.AiOutputDetail);
        Assert.Equal("auto", normalized.AiReasoningEffort);
        Assert.Equal(0, normalized.DataRetentionDays);
        Assert.Equal(3650, normalized.ScreenshotRetentionDays);
        Assert.Equal(1, normalized.AutomaticAnalysisIntervalMinutes);
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef_1.2.3_monitor-1.webp", true)]
    [InlineData("0123456789abcdef0123456789abcdef_1.2.3_active-window-raw.webp", true)]
    [InlineData("0123456789abcdef0123456789abcdef_1.2.3_monitor-2.png", true)]
    [InlineData("family-photo.webp", false)]
    [InlineData("0123456789abcdef0123456789abcdef_notes.webp", false)]
    [InlineData("0123456789abcdef0123456789abcdef_1.2.3_monitor-0.webp", false)]
    public void ScreenshotOwnership_IsFailClosed(string fileName, bool expected)
    {
        Assert.Equal(expected, ScreenCaptureService.IsOwnedArtifact(fileName));
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
