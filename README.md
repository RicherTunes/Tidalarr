# Tidalarr

Tidalarr is a Lidarr plugin that indexes and downloads lossless and hi-res audio from the [Tidal](https://tidal.com) streaming service. It ships as a single merged DLL (`Lidarr.Plugin.Tidalarr.dll`) targeting `net8.0`.

- **Version**: 1.2.9
- **Repository**: <https://github.com/RicherTunes/Tidalarr>
- **License**: [MIT](LICENSE)

**Who is this for?** Lidarr users with a Tidal HiFi or HiFi Plus subscription who want automatic, high-quality audio imports. The plugin handles search, download, format conversion, and metadata — all from within the Lidarr UI.

## Key features

- **Search & index** — Tidal albums and tracks appear as Lidarr search results via the indexer.
- **Chunked DASH downloads** — Tidal's streaming manifest is parsed into chunks, downloaded in parallel, and assembled into M4A or FLAC.
- **Quality tiers** — Low (AAC 96 kbps), High (AAC 320 kbps), Lossless (FLAC 16-bit/44.1 kHz), and Hi-Res (FLAC up to 24-bit/192 kHz).
- **FLAC extraction** — optionally extracts FLAC from M4A containers (`ExtractFlac`, on by default).
- **Synced lyrics** — best-effort `.lrc` fetch via LRCLIB (`SaveSyncedLyrics` / `UseLRCLIB`).
- **ISRC tag writing** — writes ISRC tags on downloaded tracks to anchor Lidarr import matching.
- **OAuth 2.0 + PKCE** — secure, token-based authentication; no stored passwords.

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

Settings are available in the Lidarr UI under the plugin's Settings panel. Advanced items are hidden by default — click **Show Advanced** to reveal them.

### Indexer settings

| Setting | Default | Range | Description |
|---|---|---|---|
| Config Path | auto | — | Directory for Tidal OAuth tokens. Defaults to `/config/Tidalarr` (Docker) or `~/.config/Tidalarr` (Linux). |
| OAuth Redirect URL | — | — | Paste the redirect URL from your browser after OAuth sign-in. Overwrite if stale. |
| Market *(adv)* | `US` | `US` `UK` `DE` `FR` `CA` `AU` `JP` | Tidal API market code. |
| Early Release Limit *(adv)* | 14 | 0–365 days | Skip pre-release downloads beyond this many days before release. 0 = include all. |
| Enable Cache *(adv)* | on | — | Cache search results to reduce API calls. |
| Cache Duration *(adv)* | 15 min | 0–1440 min | Cached search result lifetime. |

> **OAuth Authorization URL** is a read-only convenience field derived from Config Path — use it to copy the auth link without digging through logs.

### Download client settings

| Setting | Default | Range | Description |
|---|---|---|---|
| Config Path | auto | — | Must match the indexer's Config Path to reuse the same OAuth tokens. |
| Download Path | — | — | Destination folder for downloaded albums. |
| Preferred Quality | Lossless | Low / High / Lossless / HiRes | Audio quality tier. Falls back to the highest your subscription allows; HiFi Plus required for Lossless+. |
| Include MQA *(adv)* | on | — | Include Master (MQA) releases. |
| Extract FLAC *(adv)* | on | — | Extract FLAC from M4A containers. |
| Chunk Delay *(adv)* | 0 ms | 0–60 000 ms | Delay between chunk requests. Increase if rate-limited. |
| Max Concurrent Track Downloads *(adv)* | 2 | 1–3 | Parallel tracks per album. |
| Max Concurrent Chunk Downloads *(adv)* | 2 | 1–8 | Parallel chunk requests per track. Effective parallelism is capped so that tracks × chunks ≤ 6. Disabled when Chunk Delay > 0. |
| Save Synced Lyrics *(adv)* | on | — | Save `.lrc` lyrics alongside downloaded tracks. |
| Use LRCLIB *(adv)* | off | — | Fall back to lrclib.net for lyrics when Tidal has none. Sends artist/track/album names. |

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
- See [`docs/hostbridge-integration.md`](docs/hostbridge-integration.md) for wiring details.
- Framework rationale: [`docs/TFM_RATIONALE.md`](docs/TFM_RATIONALE.md).

### CLI tool

`TidalCLI/` provides manual verification helpers and named-argument commands:

```bash
dotnet run --project TidalCLI -- search "Miles Davis Kind of Blue"
dotnet run --project TidalCLI -- download-album AlbumId=<id> OutputDir=<dir> Quality=HiRes
```

## Project structure

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

### Wiki

| Page | Description |
|---|---|
| [Tidalarr Wiki — Home](wiki/Home.md) | Plugin overview, version, and links to the Common ecosystem |

### Local docs

| Document | Description |
|---|---|
| [`CHANGELOG.md`](CHANGELOG.md) | Release history (Keep a Changelog format) |
| [`CLAUDE.md`](CLAUDE.md) | Full development guide for contributors and automation |
| [`docs/hostbridge-integration.md`](docs/hostbridge-integration.md) | Host bridge wiring guide |
| [`docs/TFM_RATIONALE.md`](docs/TFM_RATIONALE.md) | Why `net8.0` core / `net9.0` CLI |
| [`docs/packaging-closure.md`](docs/packaging-closure.md) | Plugin packaging validation |
| [`docs/ci-gates-verification.md`](docs/ci-gates-verification.md) | CI gate details |
| [`docs/SETTINGS-MIGRATION.md`](docs/SETTINGS-MIGRATION.md) | Settings migration notes |
| [`docs/operations/deployment-smoke-test.md`](docs/operations/deployment-smoke-test.md) | Deployment and smoke-test playbook |
| [`docs/operations/multi-plugin-alignment.md`](docs/operations/multi-plugin-alignment.md) | Multi-plugin coexistence checklist |
| [`AGENTS.md`](AGENTS.md) | Repository guidelines for coding, testing, and reviews |

For the full docs index, see [`docs/README.md`](docs/README.md).

## Disclaimer

Tidalarr is an independent, open-source project by RicherTunes for **educational and research purposes** — to study plugin architecture, streaming protocols, and the Lidarr ecosystem.

- **Not affiliated with, authorized, or endorsed by TIDAL.** "TIDAL" and related marks are trademarks of their respective owners; used here descriptively only.
- Intended for **personal use with your own valid TIDAL subscription**. You are solely responsible for complying with TIDAL's Terms of Service and all applicable laws.
- Provided **"as is", without warranty of any kind** (see [LICENSE](LICENSE)). Report security issues via [SECURITY.md](SECURITY.md).

## Support

Open a [GitHub issue](https://github.com/RicherTunes/Tidalarr/issues) with detailed logs and reproduction steps.
