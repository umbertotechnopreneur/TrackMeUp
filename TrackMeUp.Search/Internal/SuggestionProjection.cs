// SPDX-License-Identifier: MIT

using System.Globalization;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace TrackMeUp.Search.Internal;

/// <summary>Maintains aggregate suggestions in the same atomic commit as their source documents.</summary>
internal static class SuggestionProjection
{
    internal const string Key = "suggestion_key";
    private const string Text = "suggestion_text";
    private const string Weight = "suggestion_weight";
    private const string Kind = "suggestion_kind";
    private static Query SuggestionsOnly => new TermQuery(new Term(Kind, "suggestion"));

    internal static Query SourcesOnly(Query query) => new BooleanQuery
    {
        { query, Occur.MUST }, { SuggestionsOnly, Occur.MUST_NOT }
    };

    internal static Dictionary<string, Entry> Entries(IEnumerable<SearchDocument> documents)
    {
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var document in documents)
        foreach (var (value, weight) in Values(document))
        {
            var text = value.Trim();
            text = text.Length > 180 ? text[..180].TrimEnd() : text;
            var key = TextNormalization.ForAnalysis(text);
            if (key.Length < 3) continue;
            entries[key] = entries.TryGetValue(key, out var existing)
                ? existing with { Weight = checked(existing.Weight + weight) } : new Entry(text, weight);
        }
        return entries;
    }

    internal static void Update(IndexWriter writer, IndexSearcher searcher,
        IReadOnlyList<(string Id, Document? Document)> changes)
    {
        var deltas = new Dictionary<string, Entry>(StringComparer.Ordinal);
        void Add(Dictionary<string, Entry> values, int sign)
        {
            foreach (var (key, entry) in values)
            {
                deltas.TryGetValue(key, out var previous);
                deltas[key] = new Entry(entry.Text, checked((previous?.Weight ?? 0) + sign * entry.Weight));
            }
        }
        foreach (var change in changes)
        {
            var old = searcher.Search(new TermQuery(new Term(SearchFields.IdKey, change.Id)), 1);
            if (old.ScoreDocs.Length != 0)
                Add(Entries([SearchDocumentMapper.FromLucene(searcher.Doc(old.ScoreDocs[0].Doc))]), -1);
            if (change.Document is not null) Add(Entries([SearchDocumentMapper.FromLucene(change.Document)]), 1);
        }
        foreach (var (key, delta) in deltas)
        {
            if (delta.Weight == 0) continue;
            var old = searcher.Search(new TermQuery(new Term(Key, key)), 1);
            var previous = old.ScoreDocs.Length == 0 ? null : searcher.Doc(old.ScoreDocs[0].Doc);
            var weight = checked((previous is null ? 0 : long.Parse(previous.Get(Weight), CultureInfo.InvariantCulture)) + delta.Weight);
            if (weight < 0) throw new InvalidDataException("Suggestion reference counts are inconsistent with source documents.");
            if (weight == 0) writer.DeleteDocuments(new Term(Key, key));
            else writer.UpdateDocument(new Term(Key, key), ToDocument(key, new Entry(previous?.Get(Text) ?? delta.Text, weight)));
        }
    }

    internal static Document ToDocument(string key, Entry entry) => new()
    {
        new StringField(Kind, "suggestion", Field.Store.NO),
        new StringField(Key, key, Field.Store.NO),
        new TextField(Text, entry.Text, Field.Store.YES),
        new StoredField(Weight, entry.Weight.ToString(CultureInfo.InvariantCulture)),
        new NumericDocValuesField(Weight, entry.Weight)
    };

    internal static IReadOnlyList<SearchSuggestion> Lookup(IndexSearcher searcher, Analyzer analyzer, string text, int limit)
    {
        var tokens = new List<string>();
        using (var stream = analyzer.GetTokenStream(Text, new StringReader(text)))
        {
            var term = stream.AddAttribute<ICharTermAttribute>();
            stream.Reset();
            while (stream.IncrementToken()) tokens.Add(term.ToString());
            stream.End();
        }
        if (tokens.Count == 0) return [];
        var matches = new BooleanQuery();
        for (var index = 0; index < tokens.Count; index++)
            matches.Add(index == tokens.Count - 1 && !char.IsWhiteSpace(text[^1])
                ? new PrefixQuery(new Term(Text, tokens[index])) : new TermQuery(new Term(Text, tokens[index])), Occur.SHOULD);
        var query = new BooleanQuery { { SuggestionsOnly, Occur.MUST }, { matches, Occur.MUST } };
        var hits = searcher.Search(query, limit, new Sort(new SortField(Weight, SortFieldType.INT64, reverse: true)));
        return hits.ScoreDocs.Select(hit => searcher.Doc(hit.Doc)).Select(document => new SearchSuggestion
        {
            Text = document.Get(Text), Weight = long.Parse(document.Get(Weight), CultureInfo.InvariantCulture)
        }).ToArray();
    }

    private static IEnumerable<(string Value, long Weight)> Values(SearchDocument document)
    {
        foreach (var value in new[] { document.Application, document.ProcessName, document.Context, document.WindowTitle,
            document.CaptureKind, document.CaptureOrigin, document.OcrRawText, document.OcrCorrectedText,
            document.OcrStructuredSummary, document.AiDescription })
            if (!string.IsNullOrWhiteSpace(value)) yield return (value, 1);
        foreach (var label in document.SpanLabels)
            if (!string.IsNullOrWhiteSpace(label)) yield return (label, 2);
        foreach (var pair in document.AttributesRaw)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key)) yield return (pair.Key, 1);
            if (!string.IsNullOrWhiteSpace(pair.Value)) yield return (pair.Value, 1);
        }
    }

    internal sealed record Entry(string Text, long Weight);
}
