# Tidalarr

Tidalarr is a Lidarr plugin that indexes and downloads lossless audio directly from the Tidal service while sharing key infrastructure with the Lidarr.Plugin.Common library.

## Installation

### Prerequisites

- Lidarr v3.0.0.4855 or higher on the **plugins branch** (`pr-plugins-3.x`, .NET 8)
- A Tidal subscription (HiFi / HiFi Plus for lossless and hi-res)

### Install via the Lidarr UI (recommended)

Settings → Plugins → paste `https://github.com/RicherTunes/Tidalarr` → Install, then restart Lidarr. Add Tidalarr under Settings → Indexers and Settings → Download Clients, then complete the Tidal sign-in/OAuth flow from the plugin settings.

To build from source instead, see **Getting Started** below.

## Getting Started

- `git submodule update --init --recursive` to sync the common library and CLI dependencies.
- `dotnet restore Tidalarr.sln` then `dotnet build Tidalarr.sln` to ensure the solution compiles cleanly.
- See `TidalCLI/` for manual verification helpers and CLI tooling support.
- Host integrators: see `docs/hostbridge-integration.md` for wiring host-only settings (with NzbDrone annotations) and mapping them to core via DI.
- For framework choices, see `docs/TFM_RATIONALE.md` (core net8.0, CLI net9.0).

## Contributor Resources

- Read the [Repository Guidelines](AGENTS.md) for coding, testing, and review expectations.
- Review `docs/` for architecture, testing, and project status background.
- Check `CLAUDE.md` if you are coordinating with Claude Code or automation agents.

## Support & Questions

Open a GitHub issue with detailed logs and reproduction steps.

## Performance tuning

Tidal downloads are chunked (many HTTP requests per track), so they will not match single-file providers 1:1. The defaults aim for a safe baseline; raise cautiously if you hit slow downloads.

- `Chunk Delay (ms)`: delay between chunk requests. Use `0` for maximum speed; increase if you get rate limited.
- `Max Concurrent Track Downloads`: parallel tracks per album (default `2`, range `1-3`).
- `Max Concurrent Chunk Downloads`: parallel chunk requests per track (default `2`, range `1-8`). Note: when `Chunk Delay (ms) > 0`, chunk parallelism is disabled to preserve "delay between requests" semantics.

## Host vs. Core

- Core plugin (`src/Tidalarr`): hostless runtime used by CLI/tests; no NzbDrone/Lidarr references. Ships in the plugin zip.
- Host bridge (`src/Tidalarr.HostBridge`): host-only wrappers with NzbDrone annotations and pretty enum labels; translates host UI models to core settings via `IHostSettingsMapper`. Not shipped in the plugin zip.
- Start here for host wiring: `docs/hostbridge-integration.md`.
- Framework rationale: `docs/TFM_RATIONALE.md`.

## CLI: Named argument support for search/download

The CLI now supports named arguments alongside positional ones:

- search: search <query> or search Query=<query>
- download-track: download-track <trackId> <outputDir> or download-track TrackId=<id> OutputDir=<dir> [Quality=Low|High|Lossless|HiRes]
- download-album: download-album <albumId> <outputDir> or download-album AlbumId=<id> OutputDir=<dir> [Quality=Low|High|Lossless|HiRes]

Unknown keys and invalid values are surfaced with friendly messages, and tests are gated via RUN_REAL_CLI_TESTS=1.

## ⚠️ Disclaimer

Tidalarr is an independent, open-source project developed by RicherTunes for **educational and research purposes** — to study plugin architecture, streaming protocols, and the Lidarr ecosystem.

- **Not affiliated with, authorized, or endorsed by TIDAL.** "TIDAL" and related marks are trademarks of their respective owners; used here descriptively only.
- Intended for **personal use with your own valid TIDAL subscription**. You are solely responsible for complying with TIDAL's Terms of Service and all laws applicable in your jurisdiction.
- Provided **"as is", without warranty of any kind; use at your own risk** (see [LICENSE](LICENSE)). The authors accept no liability for misuse or for any consequences of use.
- Do not use this software to infringe copyright or to access or redistribute content you are not licensed to use.
- Any streaming-protocol constants present are **publicly documented and common to open-source TIDAL clients**; they are included solely to enable **interoperability** and personal-use playback of content you are already licensed to access — **not** to circumvent technological protection measures for any infringing purpose. If you are a rights holder and believe any content here is inappropriate, contact the maintainer (see [SECURITY.md](SECURITY.md)) and it will be addressed promptly.
