// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Text.Json;
using TrackMeUp.Search;

namespace TrackMeUp.Services;

/// <summary>Loads the explicit, reviewable synonym groups used by the local search service.</summary>
internal static class SearchSynonymConfiguration
{
    private const int SupportedSchemaVersion = 1;

    /// <summary>Loads and validates the deployed multilingual synonym configuration.</summary>
    internal static ImmutableArray<SearchSynonymSet> Load(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("Required local search synonym configuration is missing.", path);
        }

        var configuration = JsonSerializer.Deserialize<SynonymConfiguration>(
            File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Local search synonym configuration is empty.");
        if (configuration.SchemaVersion != SupportedSchemaVersion || configuration.Sets is null)
        {
            throw new InvalidDataException("Local search synonym configuration has an unsupported schema.");
        }

        var sets = configuration.Sets.Select(set => new SearchSynonymSet
        {
            Language = string.IsNullOrWhiteSpace(set.Language)
                ? throw new InvalidDataException("A search synonym language is required.")
                : set.Language.Trim(),
            Terms = set.Terms is { Count: >= 2 } && set.Terms.All(term => !string.IsNullOrWhiteSpace(term))
                ? set.Terms.Select(term => term.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray()
                : throw new InvalidDataException("Every search synonym set requires at least two terms.")
        }).ToImmutableArray();
        if (sets.Any(set => set.Terms.Length < 2))
        {
            throw new InvalidDataException("Search synonym terms must remain distinct after normalization.");
        }

        return sets;
    }

    private sealed record SynonymConfiguration(int SchemaVersion, IReadOnlyList<SynonymSetJson>? Sets);

    private sealed record SynonymSetJson(string? Language, IReadOnlyList<string>? Terms);
}
