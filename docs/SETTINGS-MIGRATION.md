> ⚠️ Historical (flagged 2026-05-31): describes a past state; some details below no longer match the current code.

# Tidalarr Settings Split – Migration Guide

This release restored separate settings types for the indexer and the download client. The current codebase uses `TidalIndexerSettings` and `TidalDownloadClientSettings` as the primary settings types, with `TidalarrSettings` maintained for back-compatibility.

## What changed

- Introduced distinct runtime settings:
  - `TidalIndexerSettings` – OAuth + indexer knobs (market, cache, etc.)
  - `TidalDownloadClientSettings` – quality, paths, extraction/transcoding, throttling
- `TidalIndexer` now depends on `TidalIndexerSettings`.
- `TidalDownloadClient` now depends on `TidalDownloadClientSettings`.
- Back-compat: if only a single aggregated `TidalarrSettings` is registered, the module maps it into the two runtime settings automatically.
- Optional: two dedicated plugin classes exist (`TidalarrIndexerPlugin`, `TidalarrDownloadPlugin`) so hosts can expose separate settings pages. The legacy `TidalarrPlugin` remains for compatibility and maps its single settings into both runtime settings.

## How to register in custom hosts/tests

Old pattern (problematic):

```csharp
services.AddSingleton(new TidalarrSettings { /* indexer fields */ });
services.AddSingleton(new TidalarrSettings { /* download fields */ });
TidalModule.RegisterServices(services);
```

New pattern (recommended):

```csharp
services.AddSingleton(new TidalIndexerSettings {
    RedirectUrl = "https://tidal.com/android/login/auth?code=…&state=…",
    ConfigPath = Path.GetTempPath(),
    TidalMarket = "US",
    EnableCache = true,
    CacheDuration = 15
});

services.AddSingleton(new TidalDownloadClientSettings {
    PreferredQuality = TidalQuality.Lossless,
    DownloadPath = Path.GetTempPath(),
    ExtractFlac = true
});

TidalModule.RegisterServices(services);
```

Back-compat (still works):

```csharp
// If you only have one object, the module will map it to both runtime types
services.AddSingleton(new TidalarrSettings { /* all fields */ });
TidalModule.RegisterServices(services);
```

## Separate settings pages in Lidarr

For hosts that support multiple plugin surfaces per assembly, use:

- `TidalarrIndexerPlugin` for the indexer (exposes only `IIndexer`).
- `TidalarrDownloadPlugin` for the download client (exposes only `IDownloadClient`).

The legacy `TidalarrPlugin` remains available and exposes both via one settings surface.

## Validation

Both settings types have focused FluentValidation rules and preserve the previous error codes (e.g., `RedirectRequired`, `ConfigPathRequired`, `DownloadPathRequired`, etc.).
