# Tidalarr

Tidalarr is a Lidarr plugin that indexes and downloads lossless audio directly from the Tidal service while sharing key infrastructure with the Lidarr.Plugin.Common library.

## Getting Started
- `git submodule update --init --recursive` to sync the common library and CLI dependencies.
- `dotnet restore Tidalarr.sln` then `dotnet build Tidalarr.sln` to ensure the solution compiles cleanly.
- See `TidalCLI/` for manual verification helpers and CLI tooling support.
- Host integrators: see `docs/hostbridge-integration.md` for wiring host-only settings (with NzbDrone annotations) and mapping them to core via DI.
- For framework choices, see `docs/TFM_RATIONALE.md` (core net6.0, CLI net9.0).

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
