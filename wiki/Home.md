# Tidalarr

> **Canonical source:** the root [`README.md`](../README.md) is kept up to date. This wiki page may lag behind; prefer the README for the latest installation steps, configuration table, and feature list.

Tidalarr is a Lidarr plugin that indexes and downloads lossless and hi-res audio from the [Tidal](https://tidal.com) streaming service. It ships as a single merged DLL (`Lidarr.Plugin.Tidalarr.dll`) targeting `net8.0`.

- **Version**: 1.2.9
- **Repository**: <https://github.com/RicherTunes/Tidalarr>
- **License**: MIT

## Built on Lidarr.Plugin.Common

Tidalarr builds on the shared [Lidarr.Plugin.Common](https://github.com/RicherTunes/Lidarr.Plugin.Common) library (vendored at `ext/Lidarr.Plugin.Common`). Foundation topics — architecture, extension points, shared helpers, and submodule versioning — are documented in **Common's wiki**, not duplicated here:

| Common wiki page | Why follow it |
|---|---|
| [Home](https://github.com/RicherTunes/Lidarr.Plugin.Common/blob/main/wiki/Home.md) | Overview of the shared library and the four-plugin ecosystem |
| [Architecture Overview](https://github.com/RicherTunes/Lidarr.Plugin.Common/blob/main/wiki/Architecture-Overview.md) | Base classes, DI container, and the plugin lifecycle that Tidalarr inherits |
| [SDK and Extension Points](https://github.com/RicherTunes/Lidarr.Plugin.Common/blob/main/wiki/SDK-and-Extension-Points.md) | How to extend `BaseStreamingIndexer`, `BaseStreamingDownloadClient`, and other service interfaces |
| [Shared Helpers Catalog](https://github.com/RicherTunes/Lidarr.Plugin.Common/blob/main/wiki/Shared-Helpers-Catalog.md) | Ready-made utilities (caching, auth gates, health probes, lyrics enrichment) that Tidalarr consumes |
| [Versioning and Submodule Pinning](https://github.com/RicherTunes/Lidarr.Plugin.Common/blob/main/wiki/Versioning-and-Submodule-Pinning.md) | How `ext-common-sha.txt` and the gitlink stay in sync, and the nightly bump workflow |

## Installation

### Prerequisites

- Lidarr **v3.0.0.4855** or higher on the **plugins branch** (`.NET 8` image, e.g. `pr-plugins-3.1.2.4913`).
- A Tidal subscription (HiFi or HiFi Plus for lossless/hi-res quality).

### Install via the Lidarr UI

1. **Settings → Plugins** → paste `https://github.com/RicherTunes/Tidalarr` → **Install** → restart Lidarr.
2. Add **Tidalarr** under **Settings → Indexers** and **Settings → Download Clients**.
3. Complete the Tidal OAuth 2.0 PKCE sign-in from the plugin settings.

To build from source, see the [Getting Started](#getting-started) section below.

## Configuration

### Performance tuning

Tidal downloads are chunked (many HTTP requests per track), so they will not match single-file providers 1:1. The defaults aim for a safe baseline; raise cautiously if you hit slow downloads.

| Setting | Default | Range | Description |
|---|---|---|---|
| Chunk Delay (ms) | 0 | 0–60 000 | Delay between chunk requests. Use `0` for maximum speed; increase if you get rate limited. |
| Max Concurrent Track Downloads | 2 | 1–3 | Parallel tracks per album. |
| Max Concurrent Chunk Downloads | 2 | 1–8 | Parallel chunk requests per track. When `Chunk Delay > 0`, chunk parallelism is disabled to preserve "delay between requests" semantics. |

Settings are exposed via the Lidarr UI (see the README config table for the full list) and the core classes at `src/Tidalarr/Integration/`.

## Getting Started

```bash
# Clone with submodules
git clone --recursive https://github.com/RicherTunes/Tidalarr.git

# Restore and build
dotnet restore Tidalarr.sln
dotnet build Tidalarr.sln

# Run tests
pwsh scripts/test.ps1
```

### Host vs. Core

- **Core plugin** (`src/Tidalarr`): hostless runtime used by CLI and tests; no NzbDrone/Lidarr references. Ships in the plugin zip.
- **Host bridge** (`src/Tidalarr.HostBridge`): host-only wrappers with NzbDrone annotations and pretty enum labels; translates host UI models to core settings via `IHostSettingsMapper`. Not shipped in the plugin zip.
- See [`docs/hostbridge-integration.md`](../docs/hostbridge-integration.md) for wiring details.
- Framework rationale: [`docs/TFM_RATIONALE.md`](../docs/TFM_RATIONALE.md).

### CLI tool

`TidalCLI/` provides manual verification helpers and named-argument commands:

```bash
dotnet run --project TidalCLI -- search "Miles Davis Kind of Blue"
dotnet run --project TidalCLI -- download-album AlbumId=<id> OutputDir=<dir> Quality=HiRes
```

## Project Structure

```text
src/Tidalarr/
├── Application/        # Application-level services
├── Core/               # Constants, exceptions, DTOs
├── Diagnostics/        # Diagnostic helpers
├── Domain/             # API clients, streaming, manifest parsing
├── Infrastructure/     # Caching, resilience, storage
├── Integration/        # Lidarr integration (indexer, download client, DI module)
└── Properties/         # Assembly metadata
```

## Documentation

| Document | Description |
|---|---|
| [`CHANGELOG.md`](../CHANGELOG.md) | Release history (Keep a Changelog format) |
| [`docs/hostbridge-integration.md`](../docs/hostbridge-integration.md) | Host bridge wiring guide |
| [`docs/TFM_RATIONALE.md`](../docs/TFM_RATIONALE.md) | Why `net8.0` core / `net9.0` CLI |
| [`docs/packaging-closure.md`](../docs/packaging-closure.md) | Plugin packaging validation |
| [`docs/ci-gates-verification.md`](../docs/ci-gates-verification.md) | CI gate details |
| [`docs/SETTINGS-MIGRATION.md`](../docs/SETTINGS-MIGRATION.md) | Settings migration notes |
| [`CLAUDE.md`](../CLAUDE.md) | Full development guide for contributors and automation |

## Support

Open a [GitHub issue](https://github.com/RicherTunes/Tidalarr/issues) with detailed logs and reproduction steps.
