// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Suggest;
using Lucene.Net.Search.Suggest.Analyzing;
using Lucene.Net.Store;
using Lucene.Net.Util;
using TrackMeUp.Search.Internal;

namespace TrackMeUp.Search;

/// <summary>
/// Implements mandatory local search with a versioned, reconstructible Lucene index.
/// </summary>
public sealed class LocalSearchService : ILocalSearchService
{
    private const LuceneVersion Version = LuceneVersion.LUCENE_48;
    private const string SchemaCommitKey = "trackmeup.search.schema";
    private const string SourceRevisionCommitKey = "trackmeup.search.source_revision";
    private readonly SearchOptions _options;
    private readonly LanguageAnalyzerCatalog _analyzers;
    private readonly SynonymCatalog _synonyms;
    private readonly FSDirectory _directory;
    private readonly StandardAnalyzer _suggestionAnalyzer;
    private readonly AnalyzingInfixSuggester _suggester;
    private readonly string _suggestionRevisionPath;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private IndexWriter _writer;
    private Exception? _fault;
    private long _committedSourceRevision;
    private long _committedSuggestionRevision = -1;
    private int _disposeStarted;

    /// <summary>Gets the current on-disk index schema version.</summary>
    public const int IndexSchemaVersion = 2;

    /// <summary>Gets the versioned directory name created below <see cref="SearchOptions.IndexRootPath"/>.</summary>
    public const string IndexDirectoryName = "lucene-v2";

    /// <summary>Gets the private directory name used by the infix suggestion index.</summary>
    public const string SuggestionIndexDirectoryName = "suggestions-v1";

    /// <summary>Gets the sidecar file that records the source revision represented by suggestions.</summary>
    public const string SuggestionRevisionFileName = "suggestions-v1.revision";

    /// <summary>
    /// Initializes a local search service and exclusively opens its Lucene writer.
    /// </summary>
    /// <param name="options">Validated storage, paging, synonym, and fuzzy-search options.</param>
    public LocalSearchService(SearchOptions options)
    {
        var rootPath = SearchValidation.ValidateOptions(options);
        _options = options;
        IndexPath = Path.Combine(rootPath, IndexDirectoryName);
        _suggestionRevisionPath = Path.Combine(rootPath, SuggestionRevisionFileName);

        LanguageAnalyzerCatalog? analyzers = null;
        FSDirectory? directory = null;
        FSDirectory? suggestionDirectory = null;
        StandardAnalyzer? suggestionAnalyzer = null;
        AnalyzingInfixSuggester? suggester = null;
        IndexWriter? writer = null;
        try
        {
            // Index files are derived data. Directory or writer failures are fatal and surfaced to the caller.
            System.IO.Directory.CreateDirectory(IndexPath);
            analyzers = new LanguageAnalyzerCatalog();
            directory = FSDirectory.Open(new DirectoryInfo(IndexPath));
            var indexExists = DirectoryReader.IndexExists(directory);
            if (indexExists)
            {
                _committedSourceRevision = ValidateExistingSchema(directory);
            }

            writer = CreateWriter(directory, analyzers);
            if (!indexExists)
            {
                StampAndCommit(writer, 0);
            }

            _analyzers = analyzers;
            _directory = directory;
            _writer = writer;
            _synonyms = new SynonymCatalog(options);

            var suggestionPath = Path.Combine(rootPath, SuggestionIndexDirectoryName);
            System.IO.Directory.CreateDirectory(suggestionPath);
            suggestionDirectory = FSDirectory.Open(new DirectoryInfo(suggestionPath));
            var suggestionIndexExists = DirectoryReader.IndexExists(suggestionDirectory);
            suggestionAnalyzer = new StandardAnalyzer(Version);
            suggester = new AnalyzingInfixSuggester(Version, suggestionDirectory, suggestionAnalyzer);
            _suggestionAnalyzer = suggestionAnalyzer;
            _suggester = suggester;
            _committedSuggestionRevision = suggestionIndexExists
                ? LoadSuggestionRevision(_suggestionRevisionPath)
                : -1;
        }
        catch
        {
            writer?.Rollback();
            directory?.Dispose();
            if (suggester is null)
            {
                suggestionDirectory?.Dispose();
            }

            suggestionAnalyzer?.Dispose();
            analyzers?.Dispose();
            _operationGate.Dispose();
            throw;
        }
    }

    /// <summary>Gets the absolute path of the versioned Lucene index.</summary>
    public string IndexPath { get; }

    /// <inheritdoc />
    public long CommittedSourceRevision => Interlocked.Read(ref _committedSourceRevision);

    /// <inheritdoc />
    public async Task ApplyBatchAsync(
        IReadOnlyCollection<SearchIndexMutation> mutations,
        long sourceRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        if (sourceRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRevision));
        }
        var prepared = new List<(string Id, Lucene.Net.Documents.Document? Document)>(mutations.Count);
        foreach (var mutation in mutations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(mutation);
            SearchValidation.ValidateMutationId(mutation.Id, _options);
            if (mutation.Document is not { } document)
            {
                prepared.Add((mutation.Id, null));
                continue;
            }

            SearchValidation.ValidateDocument(document, _options);
            if (!string.Equals(mutation.Id, document.Id, StringComparison.Ordinal))
            {
                throw new ArgumentException("An upsert mutation id must match its document id.", nameof(mutations));
            }

            prepared.Add((mutation.Id, SearchDocumentMapper.ToLucene(document)));
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();
            var committedRevision = CommittedSourceRevision;
            if (sourceRevision < committedRevision
                || (prepared.Count > 0 && sourceRevision == committedRevision))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceRevision),
                    "A non-empty batch must advance the source revision, and revisions cannot move backwards.");
            }

            if (prepared.Count == 0 && sourceRevision == committedRevision)
            {
                return;
            }

            CommitWrite(() =>
            {
                foreach (var mutation in prepared)
                {
                    var term = new Term(SearchFields.IdKey, mutation.Id);
                    if (mutation.Document is null)
                    {
                        _writer.DeleteDocuments(term);
                    }
                    else
                    {
                        _writer.UpdateDocument(term, mutation.Document);
                    }
                }
            }, sourceRevision);
            InvalidateSuggestions();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task RebuildAsync(
        IEnumerable<SearchDocument> documents,
        long sourceRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        if (sourceRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRevision));
        }

        var prepared = new List<Lucene.Net.Documents.Document>();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SearchValidation.ValidateDocument(document, _options);
            if (!identifiers.Add(document.Id))
            {
                throw new ArgumentException(
                    $"The rebuild source contains duplicate document id '{document.Id}'.",
                    nameof(documents));
            }

            prepared.Add(SearchDocumentMapper.ToLucene(document));
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();
            if (sourceRevision < CommittedSourceRevision)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceRevision),
                    "A rebuild source revision cannot move backwards.");
            }

            CommitWrite(() =>
            {
                _writer.DeleteAll();
                foreach (var document in prepared)
                {
                    _writer.AddDocument(document);
                }
            }, sourceRevision);
            InvalidateSuggestions();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SearchResponse> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var limit = SearchValidation.ValidateRequest(request, _options);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();

            var query = new SearchQueryBuilder(_options, _analyzers, _synonyms).Build(request);
            using var reader = DirectoryReader.Open(_directory);
            var searcher = new IndexSearcher(reader);
            var topDocuments = searcher.Search(query, checked(request.Offset + limit));
            var hits = ImmutableArray.CreateBuilder<SearchHit>(Math.Min(limit, topDocuments.ScoreDocs.Length));

            foreach (var scoreDocument in topDocuments.ScoreDocs.Skip(request.Offset).Take(limit))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = SearchDocumentMapper.FromLucene(searcher.Doc(scoreDocument.Doc));
                hits.Add(new SearchHit
                {
                    Document = request.IncludeTextContent ? document : WithoutTextContent(document),
                    Score = scoreDocument.Score,
                });
            }

            return new SearchResponse
            {
                Hits = hits.MoveToImmutable(),
                TotalCount = topDocuments.TotalHits,
                Offset = request.Offset,
            };
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ImmutableArray<SearchSuggestion>> SuggestAsync(
        SearchSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var limit = SearchValidation.ValidateSuggestionRequest(request, _options);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSuggestionsCurrent(cancellationToken);
            var results = _suggester.DoLookup(
                request.Text.Trim(),
                limit,
                allTermsRequired: false,
                doHighlight: false);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var suggestions = ImmutableArray.CreateBuilder<SearchSuggestion>(results.Count);
            foreach (var result in results)
            {
                if (string.IsNullOrWhiteSpace(result.Key) || !seen.Add(result.Key))
                {
                    continue;
                }

                suggestions.Add(new SearchSuggestion
                {
                    Text = result.Key,
                    Weight = Math.Max(0, result.Value)
                });
            }

            return suggestions.MoveToImmutable();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>
    /// Closes the exclusive writer and directory after any active operation completes.
    /// </summary>
    /// <returns>A task-like value representing disposal completion.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _writer.Dispose();
            _directory.Dispose();
            _suggester.Dispose();
            _suggestionAnalyzer.Dispose();
            _analyzers.Dispose();
        }
        finally
        {
            _operationGate.Release();
            GC.SuppressFinalize(this);
        }
    }

    private static IndexWriter CreateWriter(FSDirectory directory, LanguageAnalyzerCatalog analyzers)
    {
        var configuration = new IndexWriterConfig(Version, analyzers.IndexAnalyzer)
        {
            OpenMode = OpenMode.CREATE_OR_APPEND,
        };
        return new IndexWriter(directory, configuration);
    }

    private static SearchDocument WithoutTextContent(SearchDocument document) => document with
    {
        ProcessName = null,
        Context = null,
        WindowTitle = null,
        AttributesRaw = [],
        SpanLabels = [],
        OcrRawText = null,
        OcrCorrectedText = null,
        OcrStructuredSummary = null,
        AiDescription = null,
    };

    private static long ValidateExistingSchema(FSDirectory directory)
    {
        using var reader = DirectoryReader.Open(directory);
        var userData = reader.IndexCommit.UserData;
        if (!userData.TryGetValue(SchemaCommitKey, out var version) ||
            !string.Equals(version, IndexSchemaVersion.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The Lucene index does not declare supported schema version {IndexSchemaVersion}. Rebuild it from source data.");
        }

        if (!userData.TryGetValue(SourceRevisionCommitKey, out var revision)
            || !long.TryParse(revision, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedRevision)
            || parsedRevision < 0)
        {
            throw new InvalidDataException(
                "The Lucene index does not declare a valid non-negative source revision. Rebuild it from source data.");
        }

        return parsedRevision;
    }

    private static void StampAndCommit(IndexWriter writer, long sourceRevision)
    {
        writer.SetCommitData(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SchemaCommitKey] = IndexSchemaVersion.ToString(),
            [SourceRevisionCommitKey] = sourceRevision.ToString(CultureInfo.InvariantCulture),
        });
        writer.Commit();
    }

    private static long LoadSuggestionRevision(string path)
    {
        if (!File.Exists(path))
        {
            return -1;
        }

        var value = File.ReadAllText(path).Trim();
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var revision)
            && revision >= 0
                ? revision
                : -1;
    }

    private void EnsureSuggestionsCurrent(CancellationToken cancellationToken)
    {
        var sourceRevision = CommittedSourceRevision;
        if (Interlocked.Read(ref _committedSuggestionRevision) == sourceRevision)
        {
            return;
        }

        // Suggestions are derived data: a missing or stale revision is repaired lazily so
        // multiple source commits coalesce into one complete rebuild before the next lookup.
        RebuildSuggestionsFromMainIndex(sourceRevision, cancellationToken);
    }

    private void RebuildSuggestions(
        IEnumerable<SearchDocument> documents,
        long sourceRevision,
        CancellationToken cancellationToken)
    {
        var entries = BuildSuggestionEntries(documents, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _suggester.Build(new SuggestionInputEnumerator(entries));
        _suggester.Refresh();
        PersistSuggestionRevision(sourceRevision);
        Interlocked.Exchange(ref _committedSuggestionRevision, sourceRevision);
    }

    private void RebuildSuggestionsFromMainIndex(long sourceRevision, CancellationToken cancellationToken)
    {
        using var reader = DirectoryReader.Open(_directory);
        if (reader.NumDocs == 0)
        {
            RebuildSuggestions([], sourceRevision, cancellationToken);
            return;
        }

        var searcher = new IndexSearcher(reader);
        var topDocuments = searcher.Search(new MatchAllDocsQuery(), reader.NumDocs);
        var documents = new List<SearchDocument>(topDocuments.ScoreDocs.Length);
        foreach (var scoreDocument in topDocuments.ScoreDocs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            documents.Add(SearchDocumentMapper.FromLucene(searcher.Doc(scoreDocument.Doc)));
        }

        RebuildSuggestions(documents, sourceRevision, cancellationToken);
    }

    private static IReadOnlyList<SuggestionEntry> BuildSuggestionEntries(
        IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken)
    {
        var entries = new Dictionary<string, SuggestionEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var (value, weight) in EnumerateSuggestionValues(document))
            {
                var text = value.Trim();
                if (text.Length < 3)
                {
                    continue;
                }

                text = text.Length > 180 ? text[..180].TrimEnd() : text;
                var key = TextNormalization.ForAnalysis(text);
                if (key.Length < 3)
                {
                    continue;
                }

                if (entries.TryGetValue(key, out var existing))
                {
                    entries[key] = existing with { Weight = Math.Min(long.MaxValue, existing.Weight + weight) };
                }
                else
                {
                    entries[key] = new SuggestionEntry(text, weight);
                }
            }
        }

        return [.. entries.Values
            .OrderByDescending(entry => entry.Weight)
            .ThenBy(entry => entry.Text, StringComparer.OrdinalIgnoreCase)
        ];
    }

    private void PersistSuggestionRevision(long sourceRevision)
    {
        var temporaryPath = _suggestionRevisionPath + ".tmp";
        try
        {
            // The sidecar is replaced only after the suggestion index refresh succeeds. A crash
            // therefore leaves the previous revision visible and forces a safe rebuild on reopen.
            File.WriteAllText(temporaryPath, sourceRevision.ToString(CultureInfo.InvariantCulture));
            File.Move(temporaryPath, _suggestionRevisionPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void InvalidateSuggestions() => Interlocked.Exchange(ref _committedSuggestionRevision, -1);

    private static IEnumerable<(string Value, long Weight)> EnumerateSuggestionValues(SearchDocument document)
    {
        foreach (var value in new[]
        {
            document.Application,
            document.ProcessName,
            document.Context,
            document.WindowTitle,
            document.CaptureKind,
            document.CaptureOrigin,
            document.OcrRawText,
            document.OcrCorrectedText,
            document.OcrStructuredSummary,
            document.AiDescription
        })
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return (value, 1);
            }
        }

        foreach (var label in document.SpanLabels)
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                yield return (label, 2);
            }
        }

        foreach (var pair in document.AttributesRaw)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
            {
                yield return (pair.Key, 1);
            }

            if (!string.IsNullOrWhiteSpace(pair.Value))
            {
                yield return (pair.Value, 1);
            }
        }
    }

    private sealed record SuggestionEntry(string Text, long Weight);

    private sealed class SuggestionInputEnumerator(IReadOnlyList<SuggestionEntry> entries) : IInputEnumerator
    {
        private readonly IEnumerator<SuggestionEntry> _entries = entries.GetEnumerator();

        public BytesRef Current { get; private set; } = null!;

        public long Weight { get; private set; }

        public BytesRef? Payload => null;

        public bool HasPayloads => false;

        public ICollection<BytesRef>? Contexts => null;

        public bool HasContexts => false;

        public IComparer<BytesRef>? Comparer => null;

        public bool MoveNext()
        {
            if (!_entries.MoveNext())
            {
                return false;
            }

            Current = new BytesRef(_entries.Current.Text);
            Weight = _entries.Current.Weight;
            return true;
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose() => _entries.Dispose();
    }

    private void CommitWrite(Action mutation, long sourceRevision)
    {
        try
        {
            mutation();
            StampAndCommit(_writer, sourceRevision);
            Interlocked.Exchange(ref _committedSourceRevision, sourceRevision);
        }
        catch (Exception operationException)
        {
            try
            {
                // Rollback preserves the last explicit commit; reopening is the only supported recovery path.
                _writer.Rollback();
                _writer = CreateWriter(_directory, _analyzers);
            }
            catch (Exception recoveryException)
            {
                _fault = new AggregateException(
                    "The Lucene mutation failed and the last committed index could not be reopened.",
                    operationException,
                    recoveryException);
                throw _fault;
            }

            ExceptionDispatchInfo.Capture(operationException).Throw();
            throw;
        }
    }

    private void ThrowIfUnavailable()
    {
        if (Volatile.Read(ref _disposeStarted) != 0)
        {
            throw new ObjectDisposedException(nameof(LocalSearchService));
        }

        if (_fault is not null)
        {
            throw new InvalidOperationException("The local search service is faulted.", _fault);
        }
    }
}
