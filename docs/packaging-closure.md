# Packaging Closure Checks

These checks build and package the Tidalarr plugin and verify that the produced zip contains no unintended host assemblies.

## Why it exists

- Prevents shipping assemblies from the host application (e.g., other `Lidarr.*` DLLs)
- Provides a minimal, repeatable packaging gate on PRs and `main` pushes

## Current checks

Gitea is the authoritative CI surface. `.gitea/workflows/ci.yml` runs three required policy/build jobs on PRs and `main` pushes. The repo also carries `.github/workflows/ci.yml` as a guarded GitHub mirror; Common's ecosystem CI contract enforces that there is exactly one guarded mirror workflow.

- `secret-scan` downloads the pinned Gitleaks release, verifies the archive checksum, and runs `gitleaks detect --redact --exit-code 1`.
- `lint` initializes the Common submodule, verifies `ext-common-sha.txt` matches the submodule gitlink, installs .NET 8, and runs Common's shared plugin lint runner (`run-plugin-lint-gates.ps1`) without fallback lint subsets or skip switches.
- `verify` depends on `lint` and `secret-scan` and runs `./scripts/verify-local.ps1`, which extracts host assemblies, builds, packages through Common's `New-PluginPackage`, validates package closure, and runs the hermetic test subset.

Developers can run the same local path through `scripts/ci.ps1` or `scripts/verify-local.ps1`.

## Key practices

- Gitea installs PowerShell, .NET, and the Docker CLI directly on the runner.
- The Gitea lint and verify jobs use recursive checkout and run the same Common submodule pin guard before policy/build work.
- GitHub-specific workflow setup belongs in the guarded `.github/workflows/ci.yml` mirror and must stay equivalent to the Gitea merge gates.

## NuGet caching

If a dedicated build/package workflow is added later, enable `setup-dotnet` caching with `cache: true` and `cache-dependency-path: **/*.csproj` to speed up restores in CI.

## Formatting check

When formatting is enabled, run a non-blocking verification limited to this repo:

```
dotnet format whitespace Tidalarr.sln --verify-no-changes -v minimal --exclude ext --exclude temp --exclude 'src/Tidalarr/Integration/LidarrNative'
```

The check excludes the `ext/` submodule, `temp/` content, and specific test files to avoid false positives from external sources and generated code.

## Submodule Pin

- The file `ext-common-sha.txt` contains a full 40-character commit SHA (the submodule HEAD). Keep it synchronized with the recorded submodule gitlink and update it via `pwsh ext/Lidarr.Plugin.Common/scripts/repin-common-submodule.sh --sha-from-submodule --stage`.
