# Troubleshooting

Tidal-specific failure modes and how to resolve them. For installation and the
full settings list, see the [README](../README.md); for slow downloads, see
[Home → Performance tuning](Home.md#performance-tuning).

Tidalarr raises a small set of typed exceptions
(`src/Tidalarr/Core/Exceptions/TidalExceptions.cs`); the table below maps the
symptom you'll see in logs to the cause and fix.

## Authentication

**"Authorization code is invalid or expired — paste a fresh redirect URL…"**
(`TidalInvalidGrantException`)

Tidal authorization codes are single-use. This appears when a redirect URL is
pasted twice or after it has expired. **Fix:** start a new browser login and
paste the *fresh* redirect URL. See [Authentication](Authentication.md).

**Auth failures after working previously** (`TidalAuthenticationException`)

The refresh token may have been revoked (password change, session logout, or
Tidal-side expiry). Re-run the sign-in flow to issue new tokens.

## Downloads

**"Manifest contains no chunk URLs — cannot assemble an empty stream."**
(`InvalidOperationException`)

Tidal returned a manifest with no segments — usually a transient catalog/region
issue or a track that is not actually streamable in your market. Retry; if it
persists, the track may be unavailable in your [region](Authentication.md#region--market).

**"Encrypted manifest missing security token for decryption."**
(`InvalidOperationException`)

The stream is encrypted but Tidal did not return a usable security token. This is
typically transient; retry the download.

**"Unsupported manifest type: …"** (`NotSupportedException`) or
`TidalManifestException` (with `ManifestType` `DASH`/`BTS`)

Tidalarr understands DASH (`application/dash+xml`) and Tidal BTS
(`application/vnd.tidal.bts`) manifests. A different/garbled manifest indicates a
Tidal API change or a partial response — retry, and report it if reproducible.

**Track won't download at the chosen quality** (`TidalStreamUnavailableException`,
carries `TrackId` and `RequestedQuality`)

The track isn't available at the requested tier for your subscription. Lower
**Preferred Quality** (e.g. HiRes → Lossless) or confirm your plan supports it.
See [Quality & Formats](Quality-and-Formats.md).

**Downloaded files stay `.m4a` instead of `.flac`**

FFmpeg isn't on `PATH`, so FLAC extraction was skipped (by design, to avoid
mislabeled files). Install FFmpeg and re-download, or keep `.m4a` if you prefer.
See [Quality & Formats → FLAC extraction](Quality-and-Formats.md#flac-extraction).

## Rate limiting

**Throttling / slow or failing downloads** (`TidalRateLimitException`, carries
`RetryAfterSeconds`)

Tidal returned HTTP 429. Tidalarr routes traffic through Common's adaptive rate
limiter, but aggressive concurrency can still trip limits. **Fix:** raise
**Chunk Delay (ms)** above `0` and/or lower the concurrency settings — see
[Home → Performance tuning](Home.md#performance-tuning).

## Backend health

Tidalarr tracks the health of three Tidal endpoints separately
(`src/Tidalarr/Infrastructure/Resilience/TidalBackendHealthHandler.cs`):

| Bucket | Hosts |
|---|---|
| `tidal:auth` | `auth.tidal.com` |
| `tidal:api` | `api.tidal.com`, `api.tidalhifi.com` |
| `tidal:cdn` | stream/segment hosts |

Repeated failures against one bucket are cached briefly to avoid hammering a
degraded endpoint, so a wave of errors may pause before recovering on its own.

## Diagnostics

`src/Tidalarr/Diagnostics/TidalHealthDiagnostics.cs` exposes a health snapshot,
and the CLI's `*-validate` commands (`settings-validate`, `indexer-validate`,
`download-validate`) emit JSON diagnostics you can use to check configuration
without a live download.
