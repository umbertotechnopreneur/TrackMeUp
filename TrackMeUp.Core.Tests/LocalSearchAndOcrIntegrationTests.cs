using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TrackMeUp.Application;
using TrackMeUp.Search;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class LocalSearchAndOcrIntegrationTests
{
    [Fact]
    public async Task SearchCoordinator_QueuesSearchAndSuggestionsBeforeSynchronousIndexWork()
    {
        var dataDirectory = CreateDataDirectory();
        try
        {
            var service = new ThreadRecordingSearchService();
            await using var coordinator = new LocalSearchCoordinator(new LocalStore(dataDirectory), service);

            var searchCallerThread = RunOnDedicatedThread(() => coordinator.SearchAsync(
                new SearchRequest { Text = "snapshot" },
                CancellationToken.None));
            var suggestionCallerThread = RunOnDedicatedThread(() => coordinator.SuggestAsync(
                new SearchSuggestionRequest { Text = "sna" },
                CancellationToken.None));

            Assert.NotEqual(searchCallerThread, service.SearchThreadId);
            Assert.NotEqual(suggestionCallerThread, service.SuggestionThreadId);
        }
        finally
        {
            DeleteDataDirectory(dataDirectory);
        }
    }

    [Fact]
    public void ScreenshotTextSnapshot_RoundTripsThroughSqliteAndGallery()
    {
        var dataDirectory = CreateDataDirectory();
        try
        {
            var store = new LocalStore(dataDirectory);
            var screenshotDirectory = Path.Combine(dataDirectory, "screenshots");
            Directory.CreateDirectory(screenshotDirectory);
            store.SaveSettings(store.LoadSettings() with { ScreenshotDirectory = screenshotDirectory });
            var capturedAt = DateTimeOffset.Now.AddSeconds(-1);
            var screenshotPath = CreateOwnedScreenshot(screenshotDirectory, 'a', capturedAt);
            var snapshot = CreateTextSnapshot(screenshotPath, "Riunione progetto TrackMeUp");

            store.UpsertScreenshotTextSnapshot(new string('a', 32), snapshot);

            var loaded = store.LoadScreenshotTextSnapshot(screenshotPath);
            var gallery = store.GetScreenshotGallery(DateOnly.FromDateTime(capturedAt.LocalDateTime.Date));
            var item = Assert.Single(gallery.Items);
            Assert.Equal(snapshot.SourceScreenshotPath, loaded?.SourceScreenshotPath);
            Assert.Equal(snapshot.Ocr.RawText, loaded?.Ocr.RawText);
            Assert.Equal(snapshot.Ocr.Lines[0].Words[0], loaded?.Ocr.Lines[0].Words[0]);
            Assert.Equal("Riunione progetto TrackMeUp", item.TextSnapshot?.Ocr.RawText);
            Assert.Equal(42, item.TextSnapshot?.Ocr.Lines[0].Words[0].X);
        }
        finally
        {
            DeleteDataDirectory(dataDirectory);
        }
    }

    [Fact]
    public void ActivitySchema_MigratesValidatedVersionThreeToCurrentVersion()
    {
        var dataDirectory = CreateDataDirectory();
        try
        {
            _ = new LocalStore(dataDirectory);
            var databasePath = Path.Combine(dataDirectory, "activity.sqlite3");
            using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    DROP TABLE ai_model_pricing;
                    DROP INDEX ix_screenshot_text_snapshots_capture;
                    DROP TABLE screenshot_text_snapshots;
                    PRAGMA user_version = 3;
                    """;
                command.ExecuteNonQuery();
            }

            _ = new LocalStore(dataDirectory);

            using var migrated = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            migrated.Open();
            using var version = migrated.CreateCommand();
            version.CommandText = "PRAGMA user_version;";
            Assert.Equal(5L, Convert.ToInt64(version.ExecuteScalar()));
            using var tables = migrated.CreateCommand();
            tables.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_schema
                WHERE type = 'table'
                  AND name IN ('screenshot_text_snapshots', 'ai_model_pricing');
                """;
            Assert.Equal(2L, Convert.ToInt64(tables.ExecuteScalar()));
        }
        finally
        {
            DeleteDataDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SearchCoordinator_IndexesRawFieldsWhenAiDescriptionIsMissing()
    {
        var dataDirectory = CreateDataDirectory();
        try
        {
            var store = new LocalStore(dataDirectory);
            var screenshotDirectory = Path.Combine(dataDirectory, "screenshots");
            Directory.CreateDirectory(screenshotDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotDirectory = screenshotDirectory,
                UiLanguage = "it"
            });
            var timestamp = DateTimeOffset.Now.AddSeconds(-1);
            store.AppendSample(new ActivitySample(
                timestamp,
                5,
                "active",
                "code",
                "Visual Studio Code",
                "Quarterly roadmap planning",
                "TrackMeUp - Pianificazione Q3",
                store.LoadSettings().InstallationId,
                3,
                1,
                new Dictionary<string, string>
                {
                    ["Project"] = "TrackMeUp",
                    [ActivityAttributeKeys.SpanLabel] = "Roadmap"
                }));
            var screenshotPath = CreateOwnedScreenshot(screenshotDirectory, 'b', timestamp);
            var textSnapshot = CreateTextSnapshot(screenshotPath, "Fattura marzo 2026") with
            {
                AiRefinement = new OcrAiRefinement(
                    "Fattura marzo 2026",
                    "it",
                    new OcrStructuredSummary(
                        "Riepilogo della fattura di marzo.",
                        ["Importo da verificare"],
                        ["Fornitore"],
                        ["Controllare la scadenza"]),
                    DateTimeOffset.UtcNow)
            };
            store.UpsertScreenshotTextSnapshot(new string('b', 32), textSnapshot);

            await using var coordinator = new LocalSearchCoordinator(
                store,
                new LocalSearchService(new SearchOptions
                {
                    IndexRootPath = Path.Combine(dataDirectory, "test-search")
                }));
            var indexed = await coordinator.RebuildAsync(CancellationToken.None);
            var activity = await coordinator.SearchAsync(new SearchRequest
            {
                Text = "roadmop",
                QueryLanguage = "en"
            }, CancellationToken.None);
            var screenshot = await coordinator.SearchAsync(new SearchRequest
            {
                Text = "fattura",
                QueryLanguage = "it"
            }, CancellationToken.None);

            Assert.True(indexed >= 2);
            Assert.Contains(activity.Hits, hit => hit.Document.Kind == "activity" && hit.Document.AiDescription is null);
            Assert.Contains(screenshot.Hits, hit =>
                hit.Document.Kind == "screenshot"
                && hit.Document.OcrRawText == "Fattura marzo 2026"
                && hit.Document.OcrStructuredSummary?.Contains("scadenza", StringComparison.OrdinalIgnoreCase) == true
                && hit.Document.AttributesRaw.TryGetValue(SearchAttributeKeys.MouseClicks, out var clicks)
                && clicks == "1");
        }
        finally
        {
            DeleteDataDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SearchCoordinator_IndexesDurableOcrAfterScreenshotArtifactIsRemoved()
    {
        var dataDirectory = CreateDataDirectory();
        try
        {
            var store = new LocalStore(dataDirectory);
            var screenshotDirectory = Path.Combine(dataDirectory, "screenshots");
            Directory.CreateDirectory(screenshotDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotDirectory = screenshotDirectory,
                SearchLanguage = "it"
            });
            var screenshotPath = CreateOwnedScreenshot(screenshotDirectory, 'd', DateTimeOffset.Now.AddSeconds(-1));
            var textSnapshot = CreateTextSnapshot(screenshotPath, "Preventivo cliente senza allegato");
            store.UpsertScreenshotTextSnapshot(new string('d', 32), textSnapshot);
            File.Delete(screenshotPath);

            await using var coordinator = new LocalSearchCoordinator(
                store,
                new LocalSearchService(new SearchOptions
                {
                    IndexRootPath = Path.Combine(dataDirectory, "test-search")
                }));
            var response = await coordinator.SearchAsync(new SearchRequest
            {
                Text = "preventivo",
                QueryLanguage = "it"
            }, CancellationToken.None);

            var hit = Assert.Single(response.Hits);
            Assert.Equal("screenshot-text", hit.Document.Kind);
            Assert.Equal(screenshotPath, hit.Document.CapturePath);
            Assert.Equal("Preventivo cliente senza allegato", hit.Document.OcrRawText);
            Assert.False(File.Exists(screenshotPath));
        }
        finally
        {
            DeleteDataDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task SearchCoordinator_AppliesPersistedSynonymAndTypoPreferencesToNextQuery()
    {
        var dataDirectory = CreateDataDirectory();
        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                SearchLanguage = "it",
                SearchSynonymsEnabled = false,
                SearchTypoToleranceEnabled = false
            });
            store.AppendSample(new ActivitySample(
                DateTimeOffset.Now,
                5,
                "active",
                "browser",
                "Browser",
                "Fatturazione computer aziendale",
                "Preventivo",
                store.LoadSettings().InstallationId,
                0,
                0));

            await using var coordinator = new LocalSearchCoordinator(
                store,
                new LocalSearchService(new SearchOptions
                {
                    IndexRootPath = Path.Combine(dataDirectory, "test-search"),
                    SynonymSets =
                    [
                        new SearchSynonymSet
                        {
                            Language = "it",
                            Terms = ["computer", "pc"]
                        }
                    ]
                }));

            Assert.Empty((await coordinator.SearchAsync(
                new SearchRequest { Text = "pc" },
                CancellationToken.None)).Hits);
            Assert.Empty((await coordinator.SearchAsync(
                new SearchRequest { Text = "fatturazone" },
                CancellationToken.None)).Hits);

            store.SaveSettings(store.LoadSettings() with
            {
                SearchSynonymsEnabled = true,
                SearchTypoToleranceEnabled = true
            });

            Assert.NotEmpty((await coordinator.SearchAsync(
                new SearchRequest { Text = "pc" },
                CancellationToken.None)).Hits);
            Assert.NotEmpty((await coordinator.SearchAsync(
                new SearchRequest { Text = "fatturazone" },
                CancellationToken.None)).Hits);
        }
        finally
        {
            DeleteDataDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task AiOcrRefinement_UsesDedicatedPromptAndPersistsStructuredResult()
    {
        var dataDirectory = CreateDataDirectory();
        const string apiKeyVariable = "TRACKMEUP_OCR_INTEGRATION_TEST_KEY";
        var previousApiKey = Environment.GetEnvironmentVariable(apiKeyVariable, EnvironmentVariableTarget.Process);
        try
        {
            Environment.SetEnvironmentVariable(apiKeyVariable, "test-ocr-refinement-key", EnvironmentVariableTarget.Process);
            var store = new LocalStore(dataDirectory);
            var screenshotDirectory = Path.Combine(dataDirectory, "screenshots");
            Directory.CreateDirectory(screenshotDirectory);
            var screenshotPath = CreateOwnedScreenshot(screenshotDirectory, 'c', DateTimeOffset.Now);
            var rawSnapshot = CreateTextSnapshot(screenshotPath, "Riunlone proggetto");
            store.UpsertScreenshotTextSnapshot(new string('c', 32), rawSnapshot);
            var capture = new ScreenshotCaptureResult(
                new string('c', 32),
                [screenshotPath],
                [screenshotPath],
                ScreenshotCaptureOrigins.Manual,
                [rawSnapshot]);
            var decoder = new RecordingOcrDecoder();
            var service = new OpenAiOcrRefinementService(store, decoder);
            var settings = store.LoadSettings() with
            {
                OpenAiEnabled = true,
                AiApiKeyName = apiKeyVariable,
                AiProvider = "openai",
                Model = "gpt-test"
            };

            var refined = await service.RefineAsync(capture, settings, CancellationToken.None);

            var result = Assert.Single(refined.TextSnapshots!);
            Assert.Contains("Riunlone proggetto", decoder.Prompt, StringComparison.Ordinal);
            Assert.Equal([screenshotPath], decoder.ScreenshotPaths);
            Assert.Equal("Riunione progetto", result.AiRefinement?.CorrectedText);
            Assert.Equal("Preparazione della riunione.", result.AiRefinement?.Summary.Overview);
            Assert.Equal("Riunione progetto", store.LoadScreenshotTextSnapshot(screenshotPath)?.AiRefinement?.CorrectedText);
        }
        finally
        {
            Environment.SetEnvironmentVariable(apiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            DeleteDataDirectory(dataDirectory);
        }
    }

    [Fact]
    public void OcrSettings_AreExplicitlyPatchableAndRestartBound()
    {
        var result = SettingsCatalog.Apply(
            new AppSettings(),
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["ocr.enabled"] = "false",
                ["ocr.language"] = "it"
            }));

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.OcrEnabled);
        Assert.Equal("it", result.Value.OcrLanguage);
        Assert.All(
            SettingsCatalog.Definitions.Where(definition => definition.Key.StartsWith("ocr.", StringComparison.Ordinal)),
            definition => Assert.True(definition.RequiresRestart));
    }

    [Fact]
    public void SearchSettings_ControlQueryBehaviorWithoutDisablingSearch()
    {
        var result = SettingsCatalog.Apply(
            new AppSettings(),
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["search.language"] = "it",
                ["search.synonyms"] = "false",
                ["search.typo_tolerance"] = "false"
            }));

        Assert.True(result.Succeeded);
        Assert.Equal("it", result.Value!.SearchLanguage);
        Assert.False(result.Value.SearchSynonymsEnabled);
        Assert.False(result.Value.SearchTypoToleranceEnabled);
        Assert.DoesNotContain(
            SettingsCatalog.Definitions,
            definition => string.Equals(definition.Key, "search.enabled", StringComparison.Ordinal));
        Assert.All(
            SettingsCatalog.Definitions.Where(definition => definition.Key.StartsWith("search.", StringComparison.Ordinal)),
            definition => Assert.False(definition.RequiresRestart));
    }

    private static ScreenshotTextSnapshot CreateTextSnapshot(string screenshotPath, string rawText) => new(
        screenshotPath,
        new OcrRawSnapshot(
            ScreenshotTextExtractionStatus.Succeeded,
            rawText,
            "it",
            null,
            DateTimeOffset.UtcNow,
            "test-ocr",
            1920,
            1080,
            [new OcrLineSnapshot(rawText, [new OcrWordSnapshot(rawText, 42, 24, 120, 20)])]));

    private static string CreateOwnedScreenshot(string directory, char captureCharacter, DateTimeOffset capturedAt)
    {
        var path = Path.Combine(
            directory,
            $"{new string(captureCharacter, 32)}_1.0.0_manual_monitor-1.webp");
        File.WriteAllBytes(path, [1, 2, 3]);
        File.SetLastWriteTimeUtc(path, capturedAt.UtcDateTime);
        return path;
    }

    private static string CreateDataDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"TrackMeUp-search-ocr-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static int RunOnDedicatedThread(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var callerThreadId = 0;
        Exception? failure = null;
        using var completed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            callerThreadId = Environment.CurrentManagedThreadId;
            try
            {
                operation().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                completed.Set();
            }
        })
        {
            IsBackground = true
        };
        thread.Start();
        Assert.True(completed.Wait(TimeSpan.FromSeconds(10)), "The queued search operation did not complete.");
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return callerThreadId;
    }

    private sealed class ThreadRecordingSearchService : ILocalSearchService
    {
        internal int SearchThreadId { get; private set; }

        internal int SuggestionThreadId { get; private set; }

        public Task UpsertAsync(SearchDocument document, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RebuildAsync(
            IEnumerable<SearchDocument> documents,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<SearchResponse> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken = default)
        {
            SearchThreadId = Environment.CurrentManagedThreadId;
            return Task.FromResult(new SearchResponse
            {
                TotalCount = 0,
                Offset = request.Offset
            });
        }

        public Task<ImmutableArray<SearchSuggestion>> SuggestAsync(
            SearchSuggestionRequest request,
            CancellationToken cancellationToken = default)
        {
            SuggestionThreadId = Environment.CurrentManagedThreadId;
            return Task.FromResult(ImmutableArray<SearchSuggestion>.Empty);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static void DeleteDataDirectory(string path)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class RecordingOcrDecoder : IAIDecoder
    {
        public string Provider => "test";

        internal string Prompt { get; private set; } = string.Empty;

        internal IReadOnlyList<string> ScreenshotPaths { get; private set; } = Array.Empty<string>();

        public Task<AiProviderResult> DecodeAsync(
            string prompt,
            IReadOnlyList<string> screenshotPaths,
            AppSettings settings,
            string apiKey,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            Prompt = prompt;
            ScreenshotPaths = screenshotPaths;
            const string response = """
                {
                  "items": [
                    {
                      "sourceIndex": 0,
                      "languageTag": "it",
                      "correctedText": "Riunione progetto",
                      "summary": {
                        "overview": "Preparazione della riunione.",
                        "keyPoints": ["Agenda"],
                        "entities": ["TrackMeUp"],
                        "actions": ["Confermare orario"]
                      }
                    }
                  ]
                }
                """;
            return Task.FromResult(new AiProviderResult(
                response,
                new AiUsageMetrics(10, 20, 30),
                "response-test",
                "request-test",
                settings.Model,
                "stop",
                200,
                5,
                4));
        }
    }
}
