# TrackMeUp.Search

`TrackMeUp.Search` is an independent, mandatory local-search service. It does not
depend on the WinUI application, `TrackMeUp.Core`, an AI provider, or an OCR
engine.

## Public API

- `ILocalSearchService` / `LocalSearchService`
- revisioned `ApplyBatchAsync` and `RebuildAsync`, plus `SearchAsync` and `SuggestAsync`
- Immutable `SearchDocument`, `SearchRequest`, `SearchHit`, `SearchResponse`,
  `SearchOptions`, and `SearchSynonymSet` records

The caller supplies an absolute `SearchOptions.IndexRootPath`. The service stores
derived data below `lucene-v2`, validates the schema marker on reopen, serializes
all operations, and explicitly commits every mutation. Mutations always carry the
authoritative source revision. Suggestions keep their own revision marker and are
rebuilt lazily before lookup when missing or stale, coalescing multiple mutations
without serving suggestions from an older source snapshot.

Synonyms are caller-provided equivalence groups and are expanded only while
building a query. No general-purpose dictionary is embedded. OCR raw text,
AI-corrected OCR, an OCR structured summary, and an AI screenshot description are
separate optional fields; none is required for indexing or retrieval.

## Verification Checklist

- [x] Search across raw activity and screenshot fields without an AI description
- [x] Italian, English, French, German, Spanish, Vietnamese, Simplified Chinese,
      Korean, European Portuguese, and Brazilian Portuguese analysis
- [x] Unicode fallback plus case and diacritic normalization, including Vietnamese `đ`
- [x] Query-time synonyms and controlled typo matching
- [x] Exact and phrase ranking above synonym and fuzzy matches
- [x] Kind and timestamp filters
- [x] Batched upsert/delete, full rebuild, and explicit-commit persistence
- [x] Lazy suggestion repair after a stale marker, failed marker persistence, and reopen
- [x] Fail-fast UTF-16 field budgets and Lucene UTF-8 exact-term limits
- [x] Fail-fast validation for documents, requests, options, and committed source revisions
