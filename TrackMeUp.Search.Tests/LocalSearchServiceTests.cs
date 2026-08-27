using System.Collections.Immutable;
using TrackMeUp.Search;
using Xunit;

namespace TrackMeUp.Search.Tests;

public sealed class LocalSearchServiceTests
{
    [Fact]
    public async Task ApplyBatchAsync_AppliesOrderedUpsertsAndDeletes()
    {
        await using var harness = new SearchHarness();
        await harness.Service.RebuildAsync(
        [
            CreateDocument("replace-me") with { Context = "old context" },
            CreateDocument("delete-me") with { Context = "obsolete context" }
        ]);

        await harness.Service.ApplyBatchAsync(
        [
            SearchIndexMutation.Upsert(CreateDocument("replace-me") with { Context = "new context" }),
            SearchIndexMutation.Delete("delete-me")
        ], 7);

        var replacement = await harness.Service.SearchAsync(new SearchRequest { Text = "new context", QueryLanguage = "en" });
        var deleted = await harness.Service.SearchAsync(new SearchRequest { Text = "obsolete context", QueryLanguage = "en" });
        Assert.Equal("replace-me", Assert.Single(replacement.Hits).Document.Id);
        Assert.Empty(deleted.Hits);
        Assert.Equal(7, harness.Service.CommittedSourceRevision);
    }

    [Fact]
    public void IndexSchema_IsVersionedForTheExpandedLanguageAnalyzerContract()
    {
        Assert.Equal(2, LocalSearchService.IndexSchemaVersion);
        Assert.Equal("lucene-v2", LocalSearchService.IndexDirectoryName);
    }

    [Fact]
    public async Task SuggestAsync_UsesSeparateInfixIndexForThreeCharacterQueries()
    {
        await using var harness = new SearchHarness();
        await harness.Service.RebuildAsync(
        [
            CreateDocument("suggestion-one") with
            {
                Application = "Visual Studio Code",
                WindowTitle = "SearchWindow.xaml",
                OcrRawText = "Planning the next release"
            },
            CreateDocument("suggestion-two") with
            {
                Application = "Microsoft Teams",
                WindowTitle = "Release planning"
            }
        ]);

        var suggestions = await harness.Service.SuggestAsync(new SearchSuggestionRequest
        {
            Text = "vis",
            Limit = 8
        });

        Assert.Contains(suggestions, suggestion =>
            string.Equals(suggestion.Text, "Visual Studio Code", StringComparison.OrdinalIgnoreCase)
            && suggestion.Weight > 0);
        await Assert.ThrowsAsync<ArgumentException>(() => harness.Service.SuggestAsync(new SearchSuggestionRequest
        {
            Text = "vi",
            Limit = 8
        }));
    }

    [Fact]
    public async Task SearchAsync_SearchesEveryRawFieldWithoutAiDescription()
    {
        await using var harness = new SearchHarness();
        var document = CreateDocument("raw-fields") with
        {
            Application = "LibreOffice Calc",
            ProcessName = "soffice.bin",
            Context = "Bilancio trimestrale",
            WindowTitle = "Forecast Q3",
            AttributesRaw = ImmutableDictionary<string, string?>.Empty
                .Add("Workbook", "RevenuePlan.xlsx"),
            SpanLabels = ["Finance review"],
            CaptureKind = "active-window",
            CaptureOrigin = "manual-capture",
            CapturePath = @"C:\captures\quarterly-budget.png",
            OcrRawText = "Totale vendite Europa",
            OcrCorrectedText = "Totale delle vendite europee",
            OcrStructuredSummary = "Categoria: ricavi",
            AiDescription = null,
        };
        await harness.Service.UpsertAsync(document);

        var queries = new[]
        {
            "libreoffice",
            "soffice",
            "bilancio",
            "forecast",
            "revenueplan",
            "finance review",
            "active-window",
            "manual-capture",
            @"C:\captures\quarterly-budget.png",
            "vendite europa",
            "vendite europee",
            "categoria ricavi",
        };

        foreach (var query in queries)
        {
            var response = await harness.Service.SearchAsync(new SearchRequest
            {
                Text = query,
                QueryLanguage = "it",
            });

            Assert.True(
                response.Hits.Any(hit => hit.Document.Id == document.Id),
                $"Expected query '{query}' to match the raw-fields document.");
        }

        var stored = Assert.Single((await harness.Service.SearchAsync(new SearchRequest
        {
            Text = "revenueplan",
            QueryLanguage = "it",
        })).Hits).Document;
        Assert.Null(stored.AiDescription);
        Assert.Equal("RevenuePlan.xlsx", stored.AttributesRaw["Workbook"]);
        Assert.Equal(new[] { "Finance review" }, stored.SpanLabels.ToArray());
        Assert.Equal("Totale vendite Europa", stored.OcrRawText);
    }

    [Fact]
    public async Task SearchAsync_NormalizesCaseAndDiacritics()
    {
        await using var harness = new SearchHarness();
        await harness.Service.UpsertAsync(CreateDocument("diacritics") with
        {
            Language = "it-IT",
            Context = "Riunione al Caffè già confermata",
        });

        var response = await harness.Service.SearchAsync(new SearchRequest
        {
            Text = "CAFFE GIA",
            QueryLanguage = "IT",
        });

        Assert.Equal("diacritics", Assert.Single(response.Hits).Document.Id);
    }

    [Fact]
    public async Task SearchAsync_UsesAllSupportedLanguageAnalyzers()
    {
        await using var harness = new SearchHarness();
        var cases = new[]
        {
            (Language: "it-IT", Indexed: "fatture", Query: "fattura"),
            (Language: "en-US", Indexed: "reports", Query: "report"),
            (Language: "fr-FR", Indexed: "factures", Query: "facture"),
            (Language: "de-DE", Indexed: "Rechnungen", Query: "Rechnung"),
            (Language: "es-ES", Indexed: "facturas", Query: "factura"),
            (Language: "vi-VN", Indexed: "cuộc họp điện thoại", Query: "CUOC HOP DIEN THOAI"),
            (Language: "zh-Hans", Indexed: "项目会议", Query: "项目"),
            (Language: "ko-KR", Indexed: "프로젝트회의", Query: "프로젝트"),
            (Language: "pt-PT", Indexed: "faturas", Query: "fatura"),
            (Language: "pt-BR", Indexed: "relatórios", Query: "relatório"),
        };

        foreach (var item in cases)
        {
            await harness.Service.UpsertAsync(CreateDocument($"language-{item.Language}") with
            {
                Language = item.Language,
                Context = item.Indexed,
            });
        }

        foreach (var item in cases)
        {
            var response = await harness.Service.SearchAsync(new SearchRequest
            {
                Text = item.Query,
                QueryLanguage = item.Language,
            });
            Assert.Contains(response.Hits, hit => hit.Document.Id == $"language-{item.Language}");
        }
    }

    [Fact]
    public async Task SearchAsync_UsesUnicodeFallbackForUnknownLanguage()
    {
        await using var harness = new SearchHarness();
        await harness.Service.UpsertAsync(CreateDocument("unicode-fallback") with
        {
            Language = "pl",
            Context = "Zażółć gęślą jaźń",
        });

        var response = await harness.Service.SearchAsync(new SearchRequest
        {
            Text = "ZAZOLC GESLA",
            QueryLanguage = "pl",
        });

        Assert.Equal("unicode-fallback", Assert.Single(response.Hits).Document.Id);
    }

    [Fact]
    public async Task SearchAsync_ExpandsConfiguredSynonymsAtQueryTime()
    {
        await using var harness = new SearchHarness(options => options with
        {
            SynonymSets =
            [
                new SearchSynonymSet
                {
                    Language = "it",
                    Terms = ["computer", "pc", "calcolatore"],
                },
            ],
        });
        await harness.Service.UpsertAsync(CreateDocument("synonym") with
        {
            Language = "it",
            Context = "Configurazione del computer aziendale",
        });

        var response = await harness.Service.SearchAsync(new SearchRequest
        {
            Text = "pc",
            QueryLanguage = "it",
        });

        Assert.Equal("synonym", Assert.Single(response.Hits).Document.Id);
    }

    [Fact]
    public async Task SearchAsync_CanDisableConfiguredSynonymsPerRequest()
    {
        await using var harness = new SearchHarness(options => options with
        {
            SynonymSets =
            [
                new SearchSynonymSet
                {
                    Language = "it",
                    Terms = ["computer", "pc"],
                },
            ],
        });
        await harness.Service.UpsertAsync(CreateDocument("synonym-disabled") with
        {
            Language = "it",
            Context = "computer aziendale",
        });

        var response = await harness.Service.SearchAsync(new SearchRequest
        {
            Text = "pc",
            QueryLanguage = "it",
            EnableSynonyms = false,
        });

        Assert.Empty(response.Hits);
    }

    [Fact]
    public async Task SearchAsync_MatchesControlledTypo()
    {
        await using var harness = new SearchHarness();
        await harness.Service.UpsertAsync(CreateDocument("typo") with
        {
            Language = "it",
            WindowTitle = "Fatturazione cliente",
        });

        var response = await harness.Service.SearchAsync(new SearchRequest
        {
            Text = "fatturazone",
            QueryLanguage = "it",
        });

        Assert.Equal("typo", Assert.Single(response.Hits).Document.Id);
    }

    [Fact]
    public async Task SearchAsync_CanDisableTypoTolerancePerRequest()
    {
        await using var harness = new SearchHarness();
        await harness.Service.UpsertAsync(CreateDocument("typo-disabled") with
        {
            Language = "it",
            WindowTitle = "Fatturazione cliente",
        });

        var response = await harness.Service.SearchAsync(new SearchRequest
        {
            Text = "fatturazone",
            QueryLanguage = "it",
            EnableFuzzyMatching = false,
        });

        Assert.Empty(response.Hits);
    }

    [Fact]
    public async Task SearchAsync_RanksExactThenSynonymThenFuzzyMatches()
    {
        await using var harness = new SearchHarness(options => options with
        {
            SynonymSets =
            [
                new SearchSynonymSet
                {
                    Language = "en",
                    Terms = ["notebook", "laptop"],
                },
            ],
        });
        await harness.Service.RebuildAsync(
        [
            CreateDocument("exact") with { Context = "notebook" },
            CreateDocument("synonym") with { Context = "laptop" },
            CreateDocument("fuzzy") with { Context = "notebokk" },
        ]);

        var response = await harness.Service.SearchAsync(new SearchRequest
        {
            Text = "notebook",
            QueryLanguage = "en",
        });

        Assert.Equal(new[] { "exact", "synonym", "fuzzy" }, response.Hits.Select(hit => hit.Document.Id).ToArray());
        Assert.True(response.Hits[0].Score > response.Hits[1].Score);
        Assert.True(response.Hits[1].Score > response.Hits[2].Score);
    }

    [Theory]
    [InlineData("cat", "cot")]
    [InlineData("12345", "12346")]
    [InlineData("report-final.docx", "report-finel.docx")]
    public async Task SearchAsync_DoesNotFuzzyMatchUnsafeTerms(string indexed, string query)
    {
        await using var harness = new SearchHarness();
        await harness.Service.UpsertAsync(CreateDocument("not-fuzzy") with { Context = indexed });

        var response = await harness.Service.SearchAsync(new SearchRequest { Text = query });

        Assert.Empty(response.Hits);
    }

    [Fact]
    public async Task SearchAsync_AppliesKindAndDateFilters()
    {
        await using var harness = new SearchHarness();
        var start = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        await harness.Service.RebuildAsync(
        [
            CreateDocument("before", "activity", start) with { Context = "budget" },
            CreateDocument("match", "screenshot", start.AddHours(2)) with { Context = "budget" },
            CreateDocument("exclusive-end", "screenshot", start.AddHours(3)) with { Context = "budget" },
            CreateDocument("wrong-kind", "activity", start.AddHours(2)) with { Context = "budget" },
        ]);

        var response = await harness.Service.SearchAsync(new SearchRequest
        {
            Text = "budget",
            Kinds = ImmutableHashSet.Create("SCREENSHOT"),
            FromInclusive = start.AddHours(1),
            ToExclusive = start.AddHours(3),
        });

        Assert.Equal("match", Assert.Single(response.Hits).Document.Id);
    }

    [Fact]
    public async Task SearchAsync_CanReturnMetadataOnlyForBoundedUiTransport()
    {
        await using var harness = new SearchHarness();
        await harness.Service.UpsertAsync(CreateDocument("metadata-only", "screenshot") with
        {
            Application = "Teams",
            CapturePath = @"C:\captures\meeting.png",
            OcrRawText = new string('x', 10_000),
            AiDescription = "Private analysis body"
        });

        var response = await harness.Service.SearchAsync(new SearchRequest
        {
            Text = "private",
            Kinds = ImmutableHashSet.Create("screenshot"),
            IncludeTextContent = false,
            Limit = 20
        });

        var document = Assert.Single(response.Hits).Document;
        Assert.Equal("metadata-only", document.Id);
        Assert.Equal("Teams", document.Application);
        Assert.Equal(@"C:\captures\meeting.png", document.CapturePath);
        Assert.Null(document.OcrRawText);
        Assert.Null(document.AiDescription);
        Assert.Empty(document.AttributesRaw);
        Assert.Empty(document.SpanLabels);
    }

    [Fact]
    public async Task UpsertAsync_ReplacesDocumentWithSameCaseSensitiveId()
    {
        await using var harness = new SearchHarness();
        await harness.Service.UpsertAsync(CreateDocument("replace") with { Context = "obsolete" });
        await harness.Service.UpsertAsync(CreateDocument("replace") with { Context = "current" });

        Assert.Empty((await harness.Service.SearchAsync(new SearchRequest { Text = "obsolete" })).Hits);
        Assert.Equal(
            "replace",
            Assert.Single((await harness.Service.SearchAsync(new SearchRequest { Text = "current" })).Hits).Document.Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCommittedDocument()
    {
        await using var harness = new SearchHarness();
        await harness.Service.UpsertAsync(CreateDocument("delete") with { Context = "erasable" });

        await harness.Service.DeleteAsync("delete");

        Assert.Empty((await harness.Service.SearchAsync(new SearchRequest { Text = "erasable" })).Hits);
    }

    [Fact]
    public async Task RebuildAsync_ReplacesCompleteIndex()
    {
        await using var harness = new SearchHarness();
        await harness.Service.UpsertAsync(CreateDocument("stale") with { Context = "legacyterm" });

        await harness.Service.RebuildAsync(
        [
            CreateDocument("fresh-one") with { Context = "modernterm" },
            CreateDocument("fresh-two") with { Context = "modernterm" },
        ]);

        Assert.Empty((await harness.Service.SearchAsync(new SearchRequest { Text = "legacyterm" })).Hits);
        var response = await harness.Service.SearchAsync(new SearchRequest { Text = "modernterm" });
        Assert.Equal(2, response.TotalCount);
        Assert.Equal(
            new[] { "fresh-one", "fresh-two" },
            response.Hits.Select(hit => hit.Document.Id).Order().ToArray());
    }

    [Fact]
    public async Task Index_IsReopenedFromExplicitCommit()
    {
        var root = SearchHarness.CreateRoot();
        var options = new SearchOptions { IndexRootPath = root };
        await using (var first = new LocalSearchService(options))
        {
            await first.UpsertAsync(CreateDocument("persisted") with { Context = "durableterm" });
        }

        await using (var second = new LocalSearchService(options))
        {
            var response = await second.SearchAsync(new SearchRequest { Text = "durableterm" });
            Assert.Equal("persisted", Assert.Single(response.Hits).Document.Id);
        }

        SearchHarness.DeleteRoot(root);
    }

    [Fact]
    public async Task ConcurrentUpserts_AreSerializedAndVisible()
    {
        await using var harness = new SearchHarness();
        await Task.WhenAll(Enumerable.Range(0, 20).Select(index =>
            harness.Service.UpsertAsync(CreateDocument($"concurrent-{index}") with
            {
                Context = $"parallel marker {index}",
            })));

        var response = await harness.Service.SearchAsync(new SearchRequest
        {
            Kinds = ImmutableHashSet.Create("activity"),
            Limit = 25,
        });

        Assert.Equal(20, response.TotalCount);
        Assert.Equal(20, response.Hits.Length);
    }

    [Fact]
    public void Constructor_RejectsInvalidOptions()
    {
        Assert.Throws<ArgumentException>(() => new LocalSearchService(new SearchOptions
        {
            IndexRootPath = "relative-path",
        }));

        Assert.Throws<ArgumentOutOfRangeException>(() => new LocalSearchService(new SearchOptions
        {
            IndexRootPath = SearchHarness.CreateRoot(),
            FuzzyMaxEdits = 3,
        }));

        Assert.Throws<ArgumentException>(() => new LocalSearchService(new SearchOptions
        {
            IndexRootPath = SearchHarness.CreateRoot(),
            SynonymSets =
            [
                new SearchSynonymSet { Language = "it", Terms = ["solo"] },
            ],
        }));
    }

    [Fact]
    public async Task Operations_RejectInvalidDocumentsAndRequests()
    {
        await using var harness = new SearchHarness();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Service.UpsertAsync(CreateDocument(" ")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Service.SearchAsync(new SearchRequest()));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Service.SearchAsync(new SearchRequest
            {
                Text = "valid",
                FromInclusive = DateTimeOffset.UtcNow,
                ToExclusive = DateTimeOffset.UtcNow.AddDays(-1),
            }));
    }

    private static SearchDocument CreateDocument(
        string id,
        string kind = "activity",
        DateTimeOffset? timestamp = null)
    {
        return new SearchDocument
        {
            Id = id,
            Kind = kind,
            Timestamp = timestamp ?? new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
            Language = "en",
        };
    }

    private sealed class SearchHarness : IAsyncDisposable
    {
        private static readonly string TestRoot = Path.Combine(Path.GetTempPath(), "TrackMeUp.Search.Tests");

        internal SearchHarness(Func<SearchOptions, SearchOptions>? configure = null)
        {
            Root = CreateRoot();
            var options = new SearchOptions { IndexRootPath = Root };
            Service = new LocalSearchService(configure?.Invoke(options) ?? options);
        }

        internal string Root { get; }

        internal LocalSearchService Service { get; }

        internal static string CreateRoot()
        {
            return Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
        }

        internal static void DeleteRoot(string root)
        {
            var expectedPrefix = Path.GetFullPath(TestRoot).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var resolvedRoot = Path.GetFullPath(root);
            if (!resolvedRoot.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to delete a directory outside the test root.");
            }

            if (Directory.Exists(resolvedRoot))
            {
                Directory.Delete(resolvedRoot, recursive: true);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Service.DisposeAsync();
            DeleteRoot(Root);
        }
    }
}
