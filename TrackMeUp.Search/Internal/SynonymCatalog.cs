using System.Collections.Immutable;

namespace TrackMeUp.Search.Internal;

internal sealed class SynonymCatalog
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, ImmutableArray<string>>> _byLanguage;
    private readonly int _maximumExpansions;

    internal SynonymCatalog(SearchOptions options)
    {
        _maximumExpansions = options.MaxSynonymExpansions;
        var mutable = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.Ordinal);

        foreach (var set in options.SynonymSets)
        {
            var language = TextNormalization.NormalizeLanguage(set.Language);
            if (!mutable.TryGetValue(language, out var languageMap))
            {
                languageMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
                mutable.Add(language, languageMap);
            }

            var normalizedTerms = set.Terms
                .Select(TextNormalization.ForAnalysis)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var term in normalizedTerms)
            {
                if (!languageMap.TryGetValue(term, out var expansions))
                {
                    expansions = new HashSet<string>(StringComparer.Ordinal);
                    languageMap.Add(term, expansions);
                }

                foreach (var candidate in normalizedTerms)
                {
                    if (!string.Equals(candidate, term, StringComparison.Ordinal))
                    {
                        expansions.Add(candidate);
                    }
                }
            }
        }

        _byLanguage = mutable.ToDictionary(
            language => language.Key,
            language => (IReadOnlyDictionary<string, ImmutableArray<string>>)language.Value.ToDictionary(
                term => term.Key,
                term => term.Value.Order(StringComparer.Ordinal).ToImmutableArray(),
                StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    internal ImmutableArray<string> Expand(string query, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return [];
        }

        var normalizedLanguage = TextNormalization.NormalizeLanguage(language);
        if (!_byLanguage.TryGetValue(normalizedLanguage, out var languageMap))
        {
            var primaryLanguage = TextNormalization.AnalyzerLanguage(normalizedLanguage);
            if (!_byLanguage.TryGetValue(primaryLanguage, out languageMap))
            {
                return [];
            }
        }

        var normalizedQuery = TextNormalization.ForAnalysis(query);
        var expansions = new HashSet<string>(StringComparer.Ordinal);

        AddExpansions(languageMap, normalizedQuery, expansions);

        var queryTerms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < queryTerms.Length && expansions.Count < _maximumExpansions; index++)
        {
            if (!languageMap.TryGetValue(queryTerms[index], out var replacements))
            {
                continue;
            }

            foreach (var replacement in replacements)
            {
                var variant = queryTerms.ToArray();
                variant[index] = replacement;
                expansions.Add(string.Join(' ', variant));
                if (expansions.Count >= _maximumExpansions)
                {
                    break;
                }
            }
        }

        expansions.Remove(normalizedQuery);
        return expansions.Order(StringComparer.Ordinal).Take(_maximumExpansions).ToImmutableArray();
    }

    private static void AddExpansions(
        IReadOnlyDictionary<string, ImmutableArray<string>> languageMap,
        string query,
        ISet<string> target)
    {
        if (!languageMap.TryGetValue(query, out var values))
        {
            return;
        }

        foreach (var value in values)
        {
            target.Add(value);
        }
    }
}
