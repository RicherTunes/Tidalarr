# Tidalarr Ecosystem E2E Plan (with Qobuzarr)

## Goal
Prove that **Tidalarr + Qobuzarr** can co-exist in the same Lidarr Docker instance and:
1. **Load** (schema shows both indexers + download clients)
2. **Search** (indexer test/search returns non-empty results when credentials are provided)
3. **Download** (optional, credential-gated; verifies files appear on disk)

## Baseline
- Lidarr Docker tag (plugins branch, net8 host): `ghcr.io/hotio/lidarr:pr-plugins-3.1.2.4913`
- Default gate for CI: **Basic gate only** (no secrets required)
- Credential-gated: **Medium/Full gates** (require secrets; run via `workflow_dispatch`)

## What Tidalarr Already Has (Phase 0)
- Host-version coupling guards (pin/verify type-identity assemblies against host)
- Packaging policy tests (required/forbidden DLLs; skip local, fail CI)

## Phase 1 — Multi-Plugin Docker Harness (Basic Gate)
- [ ] Adopt the shared harness from `lidarr.plugin.common`:
  - Script: `lidarr.plugin.common/scripts/multi-plugin-docker-smoke-test.ps1`
  - Gate: validate `/api/v1/indexer/schema` and `/api/v1/downloadclient/schema`
- [ ] Add a wrapper script in Tidalarr (optional) to:
  - Build/package Tidalarr + Qobuzarr
  - Call the common harness with both zips mounted

**Definition of done**
- Both implementations present:
  - Indexer: `TidalLidarrIndexer`
  - Download client: `TidalLidarrDownloadClient`
  - Indexer: `QobuzIndexer`
  - Download client: `QobuzDownloadClient`

## Phase 2 — Medium Gate (Credential-Gated)
**Why:** schema-only proves load, not behavior. Medium gate proves the plugin can be configured and exercised without runtime exceptions.

- [ ] Add `workflow_dispatch` inputs and secrets for:
  - Tidalarr OAuth: `TIDALARR_REDIRECT_URL` (and optionally market)
  - Qobuzarr auth (token or email/password) + `QOBUZARR_APP_ID/QOBUZARR_APP_SECRET` (recommended)
- [ ] Run the harness with `-RunMediumGate`:
  - It should `POST /api/v1/indexer` and `POST /api/v1/indexer/test` for each configured plugin
- [ ] Promote Medium gate to the default “manual verification” path until Actions billing is restored.

**Definition of done**
- Indexer tests succeed (HTTP 2xx) with real credentials, and Lidarr stays healthy.

## Phase 3 — Full Gate (Credential-Gated Download)
**Why:** this is the true end-to-end proof, but it requires credentials and careful cleanup.

- [ ] Use the common harness grab gate:
  - `-RunGrabGate` queues a release download via `POST /api/v1/release` and verifies it appears in `/api/v1/queue`
  - Optional: `-RequireDownloadedFiles` to assert at least one file appears under `/downloads/<plugin>` (slower; best for local)

**Definition of done**
- A download completes and a file (or folder) is present under the mounted downloads directory.

## Known Blocker: Multi-Plugin Host Bug (Upstream Lidarr)
If schema loading fails only when *both* plugins are present (but each plugin works alone), this is likely the upstream Lidarr plugin-loader bug (AssemblyLoadContext collected/unloaded too early).

- Track the upstream host fix and bump the Docker tag once a `pr-plugins-*` image includes it.
- For local validation ahead of a published image, run the common harness with a host override mount (see `lidarr.plugin.common/docs/MULTI_PLUGIN_SMOKE_TEST.md`).

## Test/CLI Reliability Notes (Tech Debt Guardrails)
These are the typical failure modes seen during ecosystem work:
- **Type-identity mismatches** (FluentValidation/NLog/Abstractions): keep “SHIP” assemblies external and verify versions against host.
- **Host assembly drift**: always verify against the selected Lidarr Docker tag.
- **CLI divergence**: ensure the CLI projects target the same TFM and reference the same code paths as the plugin (no duplicated business logic).

### CLI Failure Triage (when tests fail)
When `TidalCLI/` or CLI-driven tests fail, treat it as a compatibility surface:
1. Confirm the CLI and plugin target the same .NET runtime (net8.0 for the plugins host).
2. Ensure CLI tests do not implicitly require Lidarr host assemblies unless explicitly marked as integration.
3. Prefer end-to-end harness validation (Docker + API schema + gated flows) over unit tests that reflect private implementation details.

## Suggested Follow-ups
- [ ] Standardize shared test infrastructure in `lidarr.plugin.common` (packaging strict-mode attribute, host-version checks, shared docker harness helpers).
- [ ] Add a short `docs/TESTING.md` section in Tidalarr pointing at the harness and how to run credential-gated gates safely.
