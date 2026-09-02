// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Search;
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
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private IndexWriter _writer;
    private Exception? _fault;
    private long _committedSourceRevision;
    private int _disposeStarted;

    /// <summary>Gets the current on-disk index schema version.</summary>
    public const int IndexSchemaVersion = 3;

    /// <summary>Gets the versioned directory name created below <see cref="SearchOptions.IndexRootPath"/>.</summary>
    public const string IndexDirectoryName = "lucene-v3";

    /// <summary>
    /// Initializes a local search service and exclusively opens its Lucene writer.
    /// </summary>
    /// <param name="options">Validated storage, paging, synonym, and fuzzy-search options.</param>
    public LocalSearchService(SearchOptions options)
    {
        var rootPath = SearchValidation.ValidateOptions(options);
        _options = options;
        IndexPath = Path.Combine(rootPath, IndexDirectoryName);
        RemoveSupersededIndexes(rootPath);

        LanguageAnalyzerCatalog? analyzers = null;
        FSDirectory? directory = null;
        StandardAnalyzer? suggestionAnalyzer = null;
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

            suggestionAnalyzer = new StandardAnalyzer(Version);
            _suggestionAnalyzer = suggestionAnalyzer;
        }
        catch
        {
            writer?.Rollback();
            directory?.Dispose();
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

            var uniqueChanges = prepared.GroupBy(change => change.Id, StringComparer.Ordinal).Select(group => group.Last()).ToList();
            using var reader = DirectoryReader.Open(_directory);
            CommitWrite(() =>
            {
                SuggestionProjection.Update(_writer, new IndexSearcher(reader), uniqueChanges);
                foreach (var mutation in uniqueChanges)
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
                var identifiers = new HashSet<string>(StringComparer.Ordinal);
                var suggestions = new Dictionary<string, SuggestionProjection.Entry>(StringComparer.Ordinal);
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

                    // Add each mapped document directly to the writer. Holding a second list of
                    // every Lucene document was the largest avoidable allocation during a full
                    // OCR index rebuild.
                    _writer.AddDocument(SearchDocumentMapper.ToLucene(document));
                    foreach (var (key, entry) in SuggestionProjection.Entries([document]))
                        suggestions[key] = suggestions.TryGetValue(key, out var previous)
                            ? previous with { Weight = checked(previous.Weight + entry.Weight) } : entry;
                }
                foreach (var (key, entry) in suggestions)
                    _writer.AddDocument(SuggestionProjection.ToDocument(key, entry));
            }, sourceRevision);
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

            var query = SuggestionProjection.SourcesOnly(new SearchQueryBuilder(_options, _analyzers, _synonyms).Build(request));
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
            using var reader = DirectoryReader.Open(_directory);
            return SuggestionProjection.Lookup(new IndexSearcher(reader), _suggestionAnalyzer, request.Text, limit).ToImmutableArray();
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

    private static void RemoveSupersededIndexes(string rootPath)
    {
        // Explicit migration: old derived indexes are discarded, never read as a compatibility fallback.
        foreach (var name in new[] { "lucene-v2", "suggestions-v1", "suggestions-v1.revision", "suggestions-v1.revision.tmp" })
        {
            var path = Path.GetFullPath(Path.Combine(rootPath, name));
            var prefix = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The obsolete index path escaped its root.");
            if (!File.Exists(path) && !System.IO.Directory.Exists(path)) continue;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Derived index migration does not follow links.");
            if (System.IO.Directory.Exists(path)) System.IO.Directory.Delete(path, recursive: true);
            else File.Delete(path);
        }
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
