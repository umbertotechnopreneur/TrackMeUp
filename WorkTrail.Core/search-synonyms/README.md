# Search synonym catalog

WorkTrail ships 300 concepts in each of ten search locales: 3,000 localized
equivalence groups containing 6,942 terms and phrases. These are focused search
expressions for everyday work, rather than a general dictionary.

## Coverage

| Category | Concepts per locale |
| --- | ---: |
| Files | 25 |
| Communication | 25 |
| Calendar | 25 |
| Productivity | 25 |
| Development | 40 |
| Web | 30 |
| Systems | 30 |
| Office | 20 |
| Data | 20 |
| Administration | 20 |
| Multimedia | 20 |
| Security | 20 |
| **Total** | **300** |

Locales: `en-US`, `it-IT`, `fr-FR`, `de-DE`, `es-ES`, `vi-VN`,
`zh-Hans`, `ko-KR`, `pt-PT`, and `pt-BR`.

## Files and schema

The adjacent `../search-synonyms.json` manifest uses schema version 2 and lists
every canonical locale exactly once. This directory contains one JSON file per
locale, named after that locale. Each file uses this shape:

```json
{
  "schemaVersion": 2,
  "language": "it-IT",
  "sets": [
    {
      "id": "business.data-backup",
      "category": "security",
      "terms": ["backup dei dati", "copia di sicurezza", "backup"]
    }
  ]
}
```

Keep the same concept IDs and categories in every locale. IDs and category names
are English metadata; terms use the corresponding language. Each group contains
two to five equivalent expressions. English technical terms may be included when
they express the same concept and are useful in local search.

The application validates the manifest and every file before constructing the
search service. It rejects missing files, unknown fields or schemas, invalid
metadata, inconsistent concept coverage, and duplicate terms after normalization.
Diagnostics identify the file and offending group where available. Schema 1 is
superseded; deploy the schema-2 manifest and all ten files together.

## Editing rules

1. Choose a concrete concept and keep its ID stable. Prefer narrow meanings:
   a compressed archive is different from any compressed file.
2. Every term must work in both directions. Do not equate brands with categories,
   invoices with payments, or meetings with any occurrence of `call`.
3. Do not pad groups with capitalization, accents, or whitespace variants.
   `SearchSynonymSet.NormalizeTerm` already folds those, including Vietnamese
   `đ`. Two distinct Vietnamese words can fold to the same spelling; keep only
   one normalized entry and review the ambiguity.
4. Include useful plural forms explicitly when needed. Synonym lookup precedes
   the analyzer's stemming; a singular catalog entry alone does not expand a
   plural query.
5. Avoid sharing a normalized term across different concepts within a locale.
   Resolve the meaning instead of creating a chain of indirect equivalences.
6. Preserve regional and script distinctions. Brazilian and European Portuguese
   have separate catalogs. Simplified Chinese data must not silently serve a
   traditional Chinese request.
7. Review both positive examples and negative examples that should stay
   unrelated. Check whole phrases and phrases inside longer searches.
8. Keep entries sorted by category, then concept ID. Update this coverage table
   and the catalog regression expectations when adding concepts.

The terms are project-authored data under the repository MIT license; no
third-party dictionary was imported. Initial linguistic review was performed
during authoring and code review, without independent native-speaker validation.

## Verification

The prepared tests in `WorkTrail.Core.Tests` validate deployed catalogs and
configuration failures. `WorkTrail.Search.Tests` covers retrieval, phrases,
language boundaries, literal tokens, ranking, and the expansion budget.
Execute tests only when explicitly requested by the user.

Synonyms apply only when building queries. Restart the application to load edited
catalogs; existing indexed activity and screenshot text does not need rebuilding.
