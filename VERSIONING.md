# Versioning — Tidalarr

## Source of truth: `VERSION` file

The single source of truth for the plugin version is the top-level `VERSION` file
(e.g. `1.2.2`).  All other version references are derived from it automatically.

| Artifact | How it gets the version | Do not edit manually |
|---|---|---|
| `VERSION` | **Source of truth** — edit this one | — |
| Assembly `InformationalVersion` | `Directory.Build.props` reads `VERSION` via `$([System.IO.File]::ReadAllText(...))` | yes |
| `plugin.json` `.version` | **Hardcoded static file** — must be kept in sync with `VERSION` manually (see below) | keep in sync with VERSION |

## Wiring

`Directory.Build.props` (repo root) contains:

```xml
<VersionFromFile Condition="Exists('$(MSBuildThisFileDirectory)VERSION')">
  $([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory)VERSION').Trim())
</VersionFromFile>
<Version Condition="'$(Version)' == '' And '$(VersionFromFile)' != ''">$(VersionFromFile)</Version>
```

`plugin.json` is a **static source-controlled file** (not generated from a template).
Its `version` field must be kept in sync with `VERSION` by hand when cutting a release.

## Drift risk (known)

Unlike Qobuzarr (which generates `plugin.json` from `plugin.json.template` at build
time), Tidalarr's `plugin.json` is a static file.  Past drift has been observed
(e.g. `plugin.json` at 1.1.1 vs tag v1.2.1).

**Recommendation**: Migrate `plugin.json` to a template (`plugin.json.template` with
`{VERSION}` placeholder) and add a GeneratePluginJson MSBuild target, matching the
Qobuzarr pattern.  This migration was deferred to avoid disrupting the active release
workflow — track as tech debt.

## Bumping a version

1. Edit `VERSION` with the new semver string.
2. Edit `plugin.json` `.version` to match.
3. Push a git tag `v<VERSION>`.
