using System.Collections.Immutable;

namespace TrackMeUp.Search;

/// <summary>
/// Provides mandatory local full-text indexing and retrieval independent of the UI and AI pipeline.
/// </summary>
public interface ILocalSearchService : IAsyncDisposable
{
    /// <summary>Gets the authoritative source revision stored in the latest Lucene commit.</summary>
    long CommittedSourceRevision { get; }

    /// <summary>
    /// Applies ordered upserts and deletes with one Lucene commit and invalidates suggestions for lazy repair.
    /// </summary>
    /// <param name="mutations">The stable-document mutations to apply.</param>
    /// <param name="sourceRevision">The durable source revision represented after the batch commits.</param>
    /// <param name="cancellationToken">A token observed while preparing mutations and before commit.</param>
    Task ApplyBatchAsync(
        IReadOnlyCollection<SearchIndexMutation> mutations,
        long sourceRevision,
        CancellationToken cancellationToken = default);

    /// <summary>Rebuilds the complete index and commits the exact authoritative source revision.</summary>
    /// <param name="documents">The authoritative documents from which to rebuild the index.</param>
    /// <param name="sourceRevision">The durable source revision represented by the complete snapshot.</param>
    /// <param name="cancellationToken">A token observed while preparing documents and before commit.</param>
    Task RebuildAsync(
        IEnumerable<SearchDocument> documents,
        long sourceRevision,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a programmatically constructed, ranked query against the latest committed index.
    /// </summary>
    /// <param name="request">The text, filters, and pagination to apply.</param>
    /// <param name="cancellationToken">A token observed before the synchronous Lucene read begins.</param>
    /// <returns>The matching page and total count.</returns>
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns prefix suggestions from the separate, incrementally maintained suggestion index.
    /// </summary>
    /// <param name="request">The partial query and result limit.</param>
    /// <param name="cancellationToken">A token observed before the synchronous Lucene read begins.</param>
    Task<ImmutableArray<SearchSuggestion>> SuggestAsync(SearchSuggestionRequest request, CancellationToken cancellationToken = default);
}
