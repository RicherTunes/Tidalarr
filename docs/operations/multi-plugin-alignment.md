# Multi-Plugin Alignment Checklist

Use this to bring every Lidarr streaming plugin repo in sync with the new isolation model.

## 1. Update submodules & shared tooling

1. `git submodule update --remote --merge ext/Lidarr.Plugin.Common`
2. Copy `scripts/verify-plugin.ps1`, `scripts/ci.ps1`, and `.github/workflows/ci.yml` into the target repo.
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
  "commonVersion": "1.1.4",
  "minHostVersion": "2.14.2.4786",
  "minimumVersion": "2.14.2.4786"
}
```

Run `./scripts/verify-plugin.ps1` to confirm the manifest matches `TidalModule.Version` (or equivalent) and the shared library version.

## 4. CI enforcement

Every plugin should run `./scripts/ci.ps1` in CI. This ensures:

- Host assemblies align with the pinned version (via `verify-assemblies.ps1`).
- Manifest metadata is correct.
- Release build + ILRepack succeed.
- Test suite still executes (or explicitly reports empty).
- A packaged zip lands in `artifacts/` for publishing.

## 5. Packaging & release

1. Collect the `artifacts/<Plugin>-<version>.zip` file.
2. Attach to the release alongside changelog entries and validation logs.
3. Update the shared `plugins-platform` (if you maintain one) with the new commit hash so downstream automation can consume it.

## 6. Post-release monitoring

- Tail Lidarr logs for `ReflectionTypeLoadException` or `FileNotFoundException` messages.
- Keep `docs/operations/deployment-smoke-test.md` handy for manual smoke runs in complex environments.
- Schedule the automated isolation host sample (`ext/Lidarr.Plugin.Common/examples/IsolationHostSample`) against the newly packaged plugin to validate cross-ALC loading.

By following this checklist every plugin release stays compatible even when users mix versions, and the loader will reject incompatible manifests instead of crashing Lidarr.



