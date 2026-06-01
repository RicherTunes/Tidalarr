# Quality & Formats

This page documents how Tidalarr maps quality tiers to codecs, how it assembles
the downloaded audio, and what post-processing (FLAC extraction, ISRC tags,
lyrics) it applies. For the full settings list and defaults, see the
[README configuration table](../README.md#configuration).

## Quality tiers

The **Preferred Quality** download setting (default `Lossless`) selects one of
four tiers. Each maps to a Tidal API quality string
(`src/Tidalarr/Core/Models/TidalQuality.cs`,
`src/Tidalarr/Core/Constants/TidalConstants.cs`):

| Tier | API parameter | Codec | Resolution |
|---|---|---|---|
| Low | `LOW` | AAC | ~96 kbps |
| High | `HIGH` | AAC | ~320 kbps |
| Lossless | `LOSSLESS` | FLAC | 16-bit / 44.1 kHz (CD) |
| HiRes | `HI_RES_LOSSLESS` | FLAC | up to 24-bit / 192 kHz |

The tier you request is a *ceiling*: Tidal serves the best stream available for
the track within your subscription. A track that cannot be served at the
requested tier raises `TidalStreamUnavailableException` (see
[Troubleshooting](Troubleshooting.md)).

## Download & assembly

Tidal delivers tracks as **chunked** streams described by a manifest, not as a
single file (`src/Tidalarr/Domain/Streaming/`):

- **DASH manifest** (`application/dash+xml`) — parsed for segment URLs; assembled
  into an `.m4a` container.
- **BTS manifest** (`application/vnd.tidal.bts`) — JSON listing chunk URLs and
  codec; assembled into `.flac` or `.m4a` depending on the codec.

Chunks are downloaded and concatenated into the output file, decrypted when the
manifest is flagged encrypted (a security token must be present). Because a track
is many small HTTP requests, throughput is tuned via **Chunk Delay**,
**Max Concurrent Track Downloads**, and **Max Concurrent Chunk Downloads** — see
[Home → Performance tuning](Home.md#performance-tuning).

## FLAC extraction

When **Extract FLAC** is enabled (default `true`) and a downloaded `.m4a`
actually contains a FLAC stream, Tidalarr extracts it to a real `.flac` file
using FFmpeg with a **stream copy** (no re-encode):

```text
ffmpeg -y -hide_banner -loglevel error -i <input.m4a> -map 0:a:0 -c:a copy <output.flac>
```

If FFmpeg is not available, extraction is skipped and the original `.m4a` is kept
— Tidalarr never produces a mislabeled `.flac`
(`src/Tidalarr/Domain/Streaming/TidalAudioFormatHandler.cs`).

Related download settings: **Include MQA** (default `true`) and
**Re-encode AAC** (default `false`).

## Metadata: ISRC tags

After download, ISRC codes captured from the Tidal API are written into the audio
file's tags by the shared metadata applier in Common
(`TagLibAudioMetadataApplier`): the `TSRC` frame for ID3v2, the `ISRC` field for
FLAC/Vorbis (Xiph) comments, and the MP4/iTunes tag where supported. Accurate
ISRC tags help Lidarr match imports to the correct release.

## Synced lyrics

When **Save Synced Lyrics** is enabled (default `true`), Tidalarr writes a `.lrc`
file beside the audio. Because Tidal does not currently expose synced lyrics, the
**Use LRCLIB** setting (default `false`) enables an [LRCLIB](https://lrclib.net)
fallback that fetches timed lyrics by artist/title/album/duration. Lyrics
enrichment is best-effort: a miss is logged at debug level and never fails the
download (`src/Tidalarr/Integration/TidalAudioPostProcessor.cs`).
