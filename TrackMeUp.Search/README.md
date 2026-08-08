# TrackMeUp.Search

`TrackMeUp.Search` is an independent, mandatory local-search service. It does not
depend on the WinUI application, `TrackMeUp.Core`, an AI provider, or an OCR
engine.

## Public API

- `ILocalSearchService` / `LocalSearchService`
- `UpsertAsync`, `DeleteAsync`, `RebuildAsync`, and `SearchAsync`
- Immutable `SearchDocument`, `SearchRequest`, `SearchHit`, `SearchResponse`,
  `SearchOptions`, and `SearchSynonymSet` records

The caller supplies an absolute `SearchOptions.IndexRootPath`. The service stores
derived data below `lucene-v1`, validates the schema marker on reopen, serializes
all operations, and explicitly commits every mutation. `RebuildAsync` accepts the
authoritative source documents and replaces the full index.

Synonyms are caller-provided equivalence groups and are expanded only while
building a query. No general-purpose dictionary is embedded. OCR raw text,
AI-corrected OCR, an OCR structured summary, and an AI screenshot description are
separate optional fields; none is required for indexing or retrieval.

## Verification Checklist

- [x] Search across raw activity and screenshot fields without an AI description
- [x] Italian, English, French, German, and Spanish analyzers
- [x] Unicode fallback plus case and diacritic normalization
- [x] Query-time synonyms and controlled typo matching
- [x] Exact and phrase ranking above synonym and fuzzy matches
- [x] Kind and timestamp filters
- [x] Upsert, delete, full rebuild, persistence, and concurrent operations
- [x] Fail-fast validation for documents, requests, and options
