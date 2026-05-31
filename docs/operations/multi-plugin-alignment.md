# Multi-Plugin Alignment Checklist

Use this to bring every Lidarr streaming plugin repo in sync with the new isolation model.

## 1. Update submodules & shared tooling

1. `git submodule update --remote --merge ext/Lidarr.Plugin.Common`
2. Copy `scripts/ci.ps1`, `scripts/verify-local.ps1`, and `.github/workflows/packaging-gates.yml` into the target repo.
3. Ensure the repo has an `artifacts/` folder (ignored in git) for packaging outputs.

## 2. Switch to the host-owned abstractions

- Replace `using Lidarr.Plugin.Common.Models;` with `using Lidarr.Plugin.Abstractions.Models;`
- Remove any direct references to `Lidarr.Core`, `Lidarr.Common`, etc. where the shared abstractions provide equivalents.
- Keep only the `Lidarr.Plugin.Common` project reference; do not ship Lidarr assemblies inside the plugin package.

## 3. Manifest contract

Add or update the following fields in `plugin.json`:

```json
{
  "id": "<plugin-id>",
  "version": "<semver>",
  "apiVersion": "1.x",
  "commonVersion": "<common-submodule-sha-or-tag>",
  "minHostVersion": "3.0.0.4855"
}
```

Run `./scripts/ci.ps1` to confirm the manifest matches and the shared library version is correct.

## 4. CI enforcement

Every plugin should run `.github/workflows/packaging-gates.yml` in CI (which delegates to Common's reusable packaging gates workflow). This ensures:

- Host assemblies align with the pinned version (via Common's shared CI scripts).
- Manifest metadata is correct.
- Release build + ILRepack succeed (via Common's `New-PluginPackage`).
- Test suite still executes (or explicitly reports empty).
- A packaged zip lands in `src/<Plugin>/artifacts/packages/` for publishing.

## 5. Packaging & release

1. Collect the `src/<Plugin>/artifacts/packages/<plugin-id>-<version>-net8.0.zip` file.
2. Attach to the release alongside changelog entries and validation logs.
3. Update the shared `plugins-platform` (if you maintain one) with the new commit hash so downstream automation can consume it.

## 6. Post-release monitoring

- Tail Lidarr logs for `ReflectionTypeLoadException` or `FileNotFoundException` messages.
- Keep `docs/operations/deployment-smoke-test.md` handy for manual smoke runs in complex environments.
- Run the Docker E2E smoke tests (`scripts/e2e.ps1`) to validate the plugin loads correctly in a real Lidarr container.

By following this checklist every plugin release stays compatible even when users mix versions, and the loader will reject incompatible manifests instead of crashing Lidarr.
