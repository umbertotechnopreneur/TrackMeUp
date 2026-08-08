using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
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
    private readonly SearchOptions _options;
    private readonly LanguageAnalyzerCatalog _analyzers;
    private readonly SynonymCatalog _synonyms;
    private readonly FSDirectory _directory;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private IndexWriter _writer;
    private Exception? _fault;
    private int _disposeStarted;

    /// <summary>Gets the current on-disk index schema version.</summary>
    public const int IndexSchemaVersion = 1;

    /// <summary>Gets the versioned directory name created below <see cref="SearchOptions.IndexRootPath"/>.</summary>
    public const string IndexDirectoryName = "lucene-v1";

    /// <summary>
    /// Initializes a local search service and exclusively opens its Lucene writer.
    /// </summary>
    /// <param name="options">Validated storage, paging, synonym, and fuzzy-search options.</param>
    public LocalSearchService(SearchOptions options)
    {
        var rootPath = SearchValidation.ValidateOptions(options);
        _options = options;
        IndexPath = Path.Combine(rootPath, IndexDirectoryName);

        LanguageAnalyzerCatalog? analyzers = null;
        FSDirectory? directory = null;
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
                ValidateExistingSchema(directory);
            }

            writer = CreateWriter(directory, analyzers);
            if (!indexExists)
            {
                StampAndCommit(writer);
            }

            _analyzers = analyzers;
            _directory = directory;
            _writer = writer;
            _synonyms = new SynonymCatalog(options);
        }
        catch
        {
            writer?.Rollback();
            directory?.Dispose();
            analyzers?.Dispose();
            _operationGate.Dispose();
            throw;
        }
    }

    /// <summary>Gets the absolute path of the versioned Lucene index.</summary>
    public string IndexPath { get; }

    /// <inheritdoc />
    public async Task UpsertAsync(SearchDocument document, CancellationToken cancellationToken = default)
    {
        SearchValidation.ValidateDocument(document, _options);
        var luceneDocument = SearchDocumentMapper.ToLucene(document);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();
            CommitWrite(() => _writer.UpdateDocument(new Term(SearchFields.IdKey, document.Id), luceneDocument));
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfUnavailable();
            cancellationToken.ThrowIfCancellationRequested();
            CommitWrite(() => _writer.DeleteDocuments(new Term(SearchFields.IdKey, id)));
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task RebuildAsync(
        IEnumerable<SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);

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
            CommitWrite(() =>
            {
                _writer.DeleteAll();
                foreach (var document in prepared)
                {
                    _writer.AddDocument(document);
                }
            });
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
        AttributesRaw = ImmutableDictionary<string, string?>.Empty,
        SpanLabels = [],
        OcrRawText = null,
        OcrCorrectedText = null,
        OcrStructuredSummary = null,
        AiDescription = null,
    };

    private static void ValidateExistingSchema(FSDirectory directory)
    {
        using var reader = DirectoryReader.Open(directory);
        var userData = reader.IndexCommit.UserData;
        if (!userData.TryGetValue(SchemaCommitKey, out var version) ||
            !string.Equals(version, IndexSchemaVersion.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The Lucene index does not declare supported schema version {IndexSchemaVersion}. Rebuild it from source data.");
        }
    }

    private static void StampAndCommit(IndexWriter writer)
    {
        writer.SetCommitData(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SchemaCommitKey] = IndexSchemaVersion.ToString(),
        });
        writer.Commit();
    }

    private void CommitWrite(Action mutation)
    {
        try
        {
            mutation();
            StampAndCommit(_writer);
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
