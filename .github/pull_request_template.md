## Summary

Briefly describe the change. Focus on why and how, not line-by-line diffs.

- What does this PR do?
- Why is it needed?
- Risk/impact area(s)?

---

## Host vs Core Notes (for reviewers)

- Core plugin is hostless. Host-facing UI annotations live in HostBridge.
- HostBridge guide: docs/hostbridge-integration.md
- DI: `services.AddTidalarrHostBridgeServices()` registers `IHostSettingsMapper`.

---

## Testing

- [ ] `dotnet build Tidalarr.sln -c Release` succeeded locally
- [ ] `dotnet test Tidalarr.sln -c Release` succeeded (unit/integration)
- [ ] Optional CLI tests (Trait `scope=cli`) run locally when enabled
  - Settings JSON shape (CFG000)
  - Indexer auth failure (IX200)
  - Download diagnostics (DL100/DL001)

If CLI tests are skipped in CI, paste local output samples here:

```
{
  "success": true,
  "value": { "id": "CFG000", "service": "Tidal" },
  "error": null
}
```

---

## Packaging

- [ ] Artifact contains only plugin-owned files + Common runtime
- [ ] Excludes `Lidarr.Plugin.Abstractions.*`
- [ ] Zip metadata/commit hash present (if build.ps1 packaging used)

---

## Submodule State

- [ ] `ext/Lidarr.Plugin.Common` is pinned to the required commit for this change

---

## Linked Issues

Fixes #

---

## Screenshots / Logs (if user-visible)

Add any relevant output or screenshots.

---

## Checklist

- [ ] Conventional Commit message
- [ ] No secrets/credentials in code or tests
- [ ] Public surface area documented if changed
- [ ] Docs updated (e.g., HostBridge guide) if needed

---

## Pre-Merge Verification (CI billing blocked — manual verification required)

### Required (attach evidence or explain skip)
- [ ] `dotnet build` succeeds (0 errors)
- [ ] `dotnet test --blame-hang-timeout 30s` — test count and failures noted below
- [ ] Runtime sandbox tests pass (`--filter "Category=Runtime"`)
- [ ] No new `net6.0` references introduced

### If Common submodule changed
- [ ] Common SHA matches a tagged release (e.g., v1.7.1)
- [ ] Promotion checklist items verified per `ext/Lidarr.Plugin.Common/docs/ECOSYSTEM_PROMOTION_CHECKLIST.md`

### Test Results
- Total: ___ passed, ___ failed, ___ skipped
- Runtime: ___ passed

### Bridge Parity (streaming plugins only)
- [ ] `AddBridgeDefaults()` called in ConfigureServices
- [ ] No silent exception swallowing in indexer/download client paths

