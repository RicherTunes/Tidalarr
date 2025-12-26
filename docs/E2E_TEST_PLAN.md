# Tidalarr E2E Test Plan (Local + Docker)

This document tracks a practical, repeatable path to validate **Tidalarr** end-to-end in a Lidarr Docker host, and then validate **Tidalarr + Qobuzarr together** once the host supports stable multi-plugin loading.

## Goals (Definition of Done)

**Gate 1 (Schema)**: Lidarr loads the plugin and both appear in:
- `/api/v1/indexer/schema`
- `/api/v1/downloadclient/schema`

**Gate 2 (Search)**: Trigger an album search and see releases returned in Lidarr.

**Gate 3 (Grab + Download)**: Grab a release and confirm the expected audio files exist on disk.

## Baseline & Version Alignment

- Use the net8 plugins host image for local runs:
  - `ghcr.io/hotio/lidarr:pr-plugins-3.1.1.4884`
- Keep `ext/Lidarr.Plugin.Common` updated when common tooling/validators change.

## Local Persistent Runner (Recommended)

Use the persistent runner in `lidarr.plugin.common` to avoid manual DLL copying and to keep Lidarr config between restarts.

### First Run (OAuth setup)

From the workspace root (sibling repos checked out):

```powershell
pwsh lidarr.plugin.common/scripts/test-tidalarr-persistent.ps1 -Rebuild -Clean
```

Then in Lidarr UI:
1. Add the Tidalarr indexer/download client.
2. Set `ConfigPath` to `/config/tidalarr`.
3. Complete OAuth (PKCE) and paste the `RedirectUrl`.
4. Set `TidalMarket` (e.g. `US`) and click **Test**.

### Subsequent Runs

```powershell
pwsh lidarr.plugin.common/scripts/test-tidalarr-persistent.ps1 -Rebuild
```

## E2E Checklist (Manual)

### Gate 1: Plugin Loads
- Verify Tidalarr shows up in Lidarr Settings → Indexers / Download Clients.
- If missing, check container logs:
  - `docker logs -f tidalarr-test`

### Gate 2: Search
- In Lidarr UI, run an album search for a well-known album available in your market.
- Confirm releases appear.

### Gate 3: Grab + Download
- Grab one release and confirm the download completes.
- Confirm files exist under Lidarr’s download/import paths and are non-empty.

## Multi-Plugin (Tidalarr + Qobuzarr)

When the Lidarr host supports reliable multi-plugin loading, run the persistent multi-plugin runner:

```powershell
pwsh lidarr.plugin.common/scripts/test-multi-plugin-persistent.ps1 -Rebuild -Clean -KeepRunning
```

Then run gates using the existing persisted config:

```powershell
pwsh lidarr.plugin.common/scripts/test-multi-plugin-persistent.ps1 -RunSearchGate -RunGrabGate
```

## Test Suite Hygiene (Keeping CI Signal Clean)

If test runs fail due to packaging/ILRepack behavior, validate unit tests with packaging disabled:

```powershell
dotnet test -c Release -p:PluginPackagingDisable=true
```

Guideline:
- Fix broken **unit** tests (constructor/DI drift) rather than categorizing them away.
- Trait true environment-dependent tests as `Category=Integration` and exclude by default in fast runs.

## Backlog (Next Work Items)

1. Add a Tidalarr-focused smoke script that can:
   - validate schema via API (API key extraction), then
   - trigger a search command, then
   - verify at least one release exists.
2. Add a “grab gate” script (credential-gated) that verifies files appear on disk.
3. Stabilize multi-plugin runs once the upstream Lidarr load-context issue is fixed in a published Docker tag.

