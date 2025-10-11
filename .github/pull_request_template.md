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

