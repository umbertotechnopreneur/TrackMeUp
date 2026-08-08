using System.Collections.Immutable;

namespace TrackMeUp.Search.Internal;

internal static class SearchValidation
{
    internal static string ValidateOptions(SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.IndexRootPath);

        if (!Path.IsPathFullyQualified(options.IndexRootPath))
        {
            throw new ArgumentException("The search index root path must be absolute.", nameof(options));
        }

        if (options.DefaultResultLimit <= 0 || options.DefaultResultLimit > options.MaxResultLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "DefaultResultLimit must be positive and no greater than MaxResultLimit.");
        }

        if (options.MaxResultLimit <= 0 || options.MaxResultWindow < options.MaxResultLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxResultLimit must be positive and MaxResultWindow must be at least that large.");
        }

        if (options.FuzzyMinimumTermLength < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "FuzzyMinimumTermLength must be at least three.");
        }

        if (options.FuzzyMaxEdits is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "FuzzyMaxEdits must be one or two.");
        }

        if (options.FuzzyPrefixLength < 0 || options.FuzzyMaxExpansions <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "FuzzyPrefixLength cannot be negative and FuzzyMaxExpansions must be positive.");
        }

        if (options.MaxSynonymExpansions <= 0 || options.MaxTextFieldLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Synonym and text-length limits must be positive.");
        }

        if (options.SynonymSets.IsDefault)
        {
            throw new ArgumentException("SynonymSets must be initialized.", nameof(options));
        }

        foreach (var set in options.SynonymSets)
        {
            if (set is null)
            {
                throw new ArgumentException("SynonymSets cannot contain null entries.", nameof(options));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(set.Language);
            if (set.Terms.IsDefault || set.Terms.Length < 2)
            {
                throw new ArgumentException("Each synonym set must contain at least two terms.", nameof(options));
            }

            var normalizedTerms = set.Terms
                .Select(term => string.IsNullOrWhiteSpace(term) ? null : TextNormalization.ForAnalysis(term))
                .ToImmutableHashSet(StringComparer.Ordinal);
            if (normalizedTerms.Contains(null) || normalizedTerms.Count < 2)
            {
                throw new ArgumentException(
                    "Synonym terms must be non-empty and distinct after normalization.",
                    nameof(options));
            }
        }

        return Path.GetFullPath(options.IndexRootPath);
    }

    internal static void ValidateDocument(SearchDocument document, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.Kind);

        if (document.Timestamp == default)
        {
            throw new ArgumentException("Timestamp is required.", nameof(document));
        }

        if (document.AttributesRaw is null)
        {
            throw new ArgumentException("AttributesRaw must be initialized.", nameof(document));
        }

        if (document.SpanLabels.IsDefault)
        {
            throw new ArgumentException("SpanLabels must be initialized.", nameof(document));
        }

        foreach (var key in document.AttributesRaw.Keys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
        }

        ValidateLength(document.Id, nameof(document.Id), options);
        ValidateLength(document.Kind, nameof(document.Kind), options);
        ValidateLength(document.Language, nameof(document.Language), options);
        ValidateLength(document.Application, nameof(document.Application), options);
        ValidateLength(document.ProcessName, nameof(document.ProcessName), options);
        ValidateLength(document.Context, nameof(document.Context), options);
        ValidateLength(document.WindowTitle, nameof(document.WindowTitle), options);
        ValidateLength(document.CaptureKind, nameof(document.CaptureKind), options);
        ValidateLength(document.CaptureOrigin, nameof(document.CaptureOrigin), options);
        ValidateLength(document.CapturePath, nameof(document.CapturePath), options);
        ValidateLength(document.OcrRawText, nameof(document.OcrRawText), options);
        ValidateLength(document.OcrCorrectedText, nameof(document.OcrCorrectedText), options);
        ValidateLength(document.OcrStructuredSummary, nameof(document.OcrStructuredSummary), options);
        ValidateLength(document.AiDescription, nameof(document.AiDescription), options);

        foreach (var pair in document.AttributesRaw)
        {
            ValidateLength(pair.Key, nameof(document.AttributesRaw), options);
            ValidateLength(pair.Value, nameof(document.AttributesRaw), options);
        }

        foreach (var label in document.SpanLabels)
        {
            if (label is null)
            {
                throw new ArgumentException("SpanLabels cannot contain null values.", nameof(document));
            }

            ValidateLength(label, nameof(document.SpanLabels), options);
        }
    }

    internal static int ValidateRequest(SearchRequest request, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Text is null)
        {
            throw new ArgumentException("Text must be initialized.", nameof(request));
        }

        if (request.Kinds is null || request.Languages is null)
        {
            throw new ArgumentException("Filter collections must be initialized.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Text) &&
            request.Kinds.Count == 0 &&
            request.Languages.Count == 0 &&
            request.FromInclusive is null &&
            request.ToExclusive is null)
        {
            throw new ArgumentException("A search request must contain text or at least one filter.", nameof(request));
        }

        if (request.Offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Offset cannot be negative.");
        }

        var limit = request.Limit ?? options.DefaultResultLimit;
        if (limit <= 0 || limit > options.MaxResultLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Limit is outside the configured result range.");
        }

        if ((long)request.Offset + limit > options.MaxResultWindow)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Offset plus limit exceeds MaxResultWindow.");
        }

        if (request.FromInclusive is not null && request.ToExclusive is not null &&
            request.FromInclusive >= request.ToExclusive)
        {
            throw new ArgumentException("FromInclusive must be earlier than ToExclusive.", nameof(request));
        }

        ValidateFilterValues(request.Kinds, "Kinds", request);
        ValidateFilterValues(request.Languages, "Languages", request);
        ValidateLength(request.Text, nameof(request.Text), options);
        ValidateLength(request.QueryLanguage, nameof(request.QueryLanguage), options);

        return limit;
    }

    private static void ValidateFilterValues(IEnumerable<string> values, string name, SearchRequest request)
    {
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException($"{name} cannot contain empty values.", nameof(request));
        }
    }

    private static void ValidateLength(string? value, string name, SearchOptions options)
    {
        if (value is not null && value.Length > options.MaxTextFieldLength)
        {
            throw new ArgumentOutOfRangeException(name, $"Text exceeds {options.MaxTextFieldLength} UTF-16 characters.");
        }
    }
}
