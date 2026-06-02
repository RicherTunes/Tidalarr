# Tidalarr

> **Canonical source:** the root [`README.md`](../README.md) is kept up to date. This wiki page may lag behind; prefer the README for the latest installation steps, configuration table, and feature list.

Tidalarr is a Lidarr plugin that indexes and downloads lossless and hi-res audio from [Tidal](https://tidal.com). It ships as a single merged DLL targeting `net8.0`, built on the shared [Lidarr.Plugin.Common](https://github.com/RicherTunes/Lidarr.Plugin.Common) library.

For full details — features, installation, configuration, CLI usage, and project structure — see the **[README](../README.md)**.

## Wiki pages

These pages cover Tidal-specific operational detail that goes beyond the README:

| Page | What it covers |
|---|---|
| [Authentication](Authentication.md) | OAuth 2.0 + PKCE sign-in (UI & CLI), token refresh, region/market, the "fresh redirect URL" error |
| [Quality & Formats](Quality-and-Formats.md) | Quality tiers → codecs, chunked DASH/BTS assembly, FLAC extraction, ISRC tags, synced lyrics |
| [Troubleshooting](Troubleshooting.md) | Typed errors mapped to cause + fix (auth, manifests, rate limits, stream availability, FFmpeg) |

Foundation topics (architecture, extension points, shared helpers, submodule
pinning) live in **[Common's wiki](https://github.com/RicherTunes/Lidarr.Plugin.Common/blob/main/wiki/Home.md)**
and are not duplicated here.

## Performance tuning

Tidal downloads are chunked (many HTTP requests per track), so they will not match single-file providers 1:1. The defaults aim for a safe baseline; raise cautiously if you hit slow downloads.

| Setting | Default | Range | Description |
|---|---|---|---|
| Chunk Delay (ms) | 0 | 0–60 000 | Delay between chunk requests. Use `0` for maximum speed; increase if you get rate limited. |
| Max Concurrent Track Downloads | 2 | 1–3 | Parallel tracks per album. |
| Max Concurrent Chunk Downloads | 2 | 1–8 | Parallel chunk requests per track. When `Chunk Delay > 0`, chunk parallelism is disabled to preserve "delay between requests" semantics. |

Settings are exposed via the Lidarr UI (see the [README config table](../README.md#configuration) for the full list) and the core classes at `src/Tidalarr/Integration/`.
