---
name: release-automation
description: Build complete release automation from scratch for Tidalarr. Use when working with releases, versioning, changelog creation, release workflows, or GitHub releases. Critical for establishing release infrastructure where none exists.
---

# Release Automation Specialist

## Mission
Build a complete automated release system for Tidalarr from the ground up, implementing semantic versioning, changelog management, automated testing, and GitHub release workflows.

## Expertise Areas

### 1. Release Infrastructure Creation
- Design complete release workflow from scratch
- Implement semantic versioning strategy
- Create version management across multiple files
- Establish changelog practices with conventional commits
- Define release process and checklists

### 2. Multi-File Version Synchronization
- Manage versions across plugin.json, csproj, and VERSION file
- Create automated version bumping scripts
- Validate version consistency across files
- Handle TidalModule.Version constant updates

### 3. Changelog and Release Notes
- Create CHANGELOG.md from scratch
- Implement conventional commit parsing
- Generate automated release notes
- Categorize changes by type
- Format notes for GitHub releases

### 4. Release Workflow Implementation
- Create GitHub Actions release workflow
- Implement tag-triggered releases
- Add comprehensive pre-release validation
- Package artifacts properly
- Handle manual and automated releases

### 5. Release Validation and Quality
- Run full test suite before release
- Verify package dependency closure
- Validate manifest alignment
- Check version consistency
- Ensure documentation currency

## Current Project Context

### Tidalarr Release Status
- **Current Status**: CRITICAL - No release automation exists
- **Current Version**: 1.0.1
- **Existing Infrastructure**: NONE - No release workflow
- **CI Infrastructure**: Basic (ci.yml, nightly.yml, packaging-closure.yml)
- **Build Scripts**: Excellent (build.ps1, build.sh with deploy support)
- **Package Format**: ZIP (artifacts/Tidalarr-{version}.zip)
- **Verification**: Good (verify-plugin.ps1 validates versions)

### Critical Missing Components
1. **No Release Workflow** - No .github/workflows/release.yml
2. **No CHANGELOG.md** - No version history tracking
3. **No Automated Release Notes** - Manual GitHub releases
4. **No Version Bump Automation** - Manual updates to 4+ files
5. **No Artifact Signing** - No signing or checksums
6. **No SBOM** - No software bill of materials
7. **No Release Tagging Automation** - Manual git operations

### Version Synchronization Challenge
Tidalarr has **4 locations** requiring version updates:
1. `plugin.json` - version field (1.0.1)
2. `Tidalarr.csproj` - Version, FileVersion, AssemblyVersion (1.0.1.0)
3. `src/Tidalarr/TidalModule.cs` - Version constant ("1.0.1")
4. `plugin.json` - commonVersion field (1.1.5 - shared library version)

Additionally:
- minHostVersion (2.14.2.4786) - Lidarr compatibility
- apiVersion (1.x) - Plugin API version

### Key Files to Create
- `CHANGELOG.md` - **CREATE** - Complete version history
- `.github/workflows/release.yml` - **CREATE** - Full release automation
- `.github/scripts/bump-version.ps1` - **CREATE** - Multi-file version sync
- `.github/scripts/generate-release-notes.sh` - **CREATE** - Note automation
- `scripts/tag-release.sh` - **CREATE** - Git tag helper
- `.github/workflows/release-drafter.yml` - **OPTIONAL** - Draft automation

### Existing Assets to Leverage
- `scripts/verify-plugin.ps1` - Already validates version consistency
- `scripts/ci.ps1` - Complete build and test pipeline
- `scripts/deploy-plugin.ps1` - Docker deployment automation
- `.github/workflows/packaging-closure.yml` - Package validation

## Best Practices

### Version Management Strategy
1. **Single Source of Truth**: Create VERSION file as canonical source
2. **Automated Sync**: Script updates all 4+ locations automatically
3. **Validation**: verify-plugin.ps1 catches mismatches
4. **Conventional Versioning**: Semantic versioning (MAJOR.MINOR.PATCH)

### Multi-File Version Update Process
```powershell
# New script: scripts/bump-version.ps1
param([string]$NewVersion)

# 1. Update VERSION file (create if missing)
# 2. Update plugin.json: version
# 3. Update Tidalarr.csproj: Version, FileVersion, AssemblyVersion
# 4. Update TidalModule.cs: Version constant
# 5. Run verify-plugin.ps1 to validate
# 6. Output confirmation
```

### Release Process Design

#### Phase 1: Preparation (Developer)
1. **Feature Complete**: All intended changes merged
2. **Version Decision**: Determine MAJOR.MINOR.PATCH bump
3. **Update CHANGELOG**: Add [Unreleased] changes to new version section
4. **Commit Changelog**: `git commit -m "docs: update changelog for v1.1.0"`

#### Phase 2: Version Bump (Automated Script)
5. **Run Version Script**: `./scripts/bump-version.ps1 -NewVersion "1.1.0"`
   - Updates VERSION, plugin.json, csproj, TidalModule.cs
   - Validates with verify-plugin.ps1
6. **Commit Version**: `git commit -m "chore: bump version to v1.1.0"`

#### Phase 3: Tagging (Developer or Script)
7. **Create Tag**: `git tag -a v1.1.0 -m "Release v1.1.0"`
8. **Push Tag**: `git push origin v1.1.0`

#### Phase 4: Automated Release (GitHub Actions)
9. **Trigger Workflow**: Tag push triggers .github/workflows/release.yml
10. **Validate**:
    - Extract version from tag
    - Validate tag format (v*.*.*)
    - Check VERSION file matches tag
    - Run verify-plugin.ps1
11. **Build**:
    - Generate host stub assemblies
    - Restore dependencies
    - Build Release configuration
12. **Test**:
    - Run full test suite (exclude CLI tests)
    - Fail release if any tests fail
13. **Package**:
    - Run PluginPack module
    - Create artifacts/Tidalarr-{version}.zip
    - Generate SHA256 checksum
14. **Sign** (Future):
    - Cosign keyless signing
    - Or GPG signing
15. **SBOM** (Future):
    - Generate SPDX JSON
    - Attach to release
16. **Release Notes**:
    - Extract CHANGELOG section for version
    - Parse additional commits since last tag
    - Format with emojis and categories
    - Include full changelog link
17. **GitHub Release**:
    - Create release for tag
    - Attach plugin ZIP
    - Attach checksum file
    - Publish release notes
18. **Post-release**:
    - Update latest tag (optional)
    - Trigger wiki update (optional)

### Conventional Commits Format
Adopt for automated changelog generation:
```
feat: add quality profile selection UI
feat(auth): implement OAuth2 flow for Tidal
fix: resolve album search encoding issue
fix(download): handle retry logic for failed downloads
docs: update deployment guide for Docker
test: add unit tests for search service
perf: optimize caching layer performance
refactor: restructure streaming module
chore: bump dependency versions
BREAKING CHANGE: remove deprecated search API
```

Mapping to CHANGELOG sections:
- `feat:` → **Added**
- `fix:` → **Fixed**
- `perf:` → **Performance**
- `refactor:` → **Changed** (if breaking)
- `docs:` → **Documentation**
- `test:` → (Not in CHANGELOG typically)
- `chore:` → (Not in CHANGELOG typically)
- `BREAKING CHANGE:` → **Breaking Changes** section

### CHANGELOG.md Format
```markdown
# Changelog

All notable changes to Tidalarr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Feature being developed for next release

## [1.1.0] - 2025-11-25

### Added
- ✨ Quality profile selection in settings UI
- 🎵 Support for Tidal HiRes streaming quality
- 🔐 OAuth2 authentication flow

### Fixed
- 🐛 Album search encoding for special characters
- 🔧 Download retry logic for transient API failures
- 🔧 Cache invalidation for expired sessions

### Performance
- ⚡ Optimized search response time by 35%
- ⚡ Improved caching efficiency

### Changed
- 🔄 Restructured streaming module (no breaking changes)

### Documentation
- 📝 Added OAuth setup guide
- 📝 Updated Docker deployment instructions

## [1.0.1] - 2025-11-20

### Fixed
- 🐛 Plugin initialization error on startup
- 🔧 Dependency resolution for host bridge

## [1.0.0] - 2025-11-15

### Added
- 🎉 Initial release
- 🎵 Full Tidal streaming integration
- 🔍 Album and track search
- ⬇️ Download management
- 💾 Metadata caching
- 📊 Telemetry and monitoring

[Unreleased]: https://github.com/RicherTunes/tidalarr/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/RicherTunes/tidalarr/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/RicherTunes/tidalarr/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/RicherTunes/tidalarr/releases/tag/v1.0.0
```

### Release Notes Template
```markdown
## 🎉 What's New in Tidalarr v1.1.0

### ✨ New Features
- **Quality Profile Selection**: Configure preferred streaming quality in plugin settings
- **HiRes Streaming**: Full support for Tidal HiRes audio quality
- **OAuth2 Authentication**: More secure authentication flow with Tidal

### 🐛 Bug Fixes
- Fixed album search encoding issues with special characters (e.g., "Ä", "ñ", "é")
- Improved download retry logic for handling transient API failures
- Resolved cache invalidation when Tidal sessions expire

### ⚡ Performance Improvements
- Reduced search response time by 35% through optimized caching
- Improved overall caching efficiency

### 🔄 Changes
- Restructured streaming module for better maintainability (no breaking changes)

### 📝 Documentation
- Added comprehensive OAuth2 setup guide
- Updated Docker deployment instructions with new examples

---

## 📦 Installation

1. Download `Tidalarr-v1.1.0.zip`
2. Extract to your Lidarr plugins directory:
   - **Docker**: `/config/plugins/tidalarr/`
   - **Windows**: `%ProgramData%\Lidarr\plugins\tidalarr\`
   - **Linux**: `/var/lib/lidarr/plugins/tidalarr/`
3. Restart Lidarr

See the [Installation Guide](docs/INSTALLATION.md) for detailed instructions.

## 🔗 Links
- **Full Changelog**: https://github.com/RicherTunes/tidalarr/compare/v1.0.1...v1.1.0
- **Documentation**: https://github.com/RicherTunes/tidalarr/tree/main/docs
- **Issues**: https://github.com/RicherTunes/tidalarr/issues

## ⚠️ Requirements
- Lidarr 2.14.2.4786 or later (plugins branch)
- Tidal subscription (HiFi or HiFi Plus for HiRes)
- Lidarr.Plugin.Common 1.1.5
```

## Scripts to Create

### 1. Version Bump Script
**File**: `scripts/bump-version.ps1`
```powershell
<#
.SYNOPSIS
    Bump version across all Tidalarr files

.PARAMETER NewVersion
    New semantic version (e.g., "1.1.0")

.PARAMETER CommonVersion
    Optional: Update common library version (default: keep current)
#>
param(
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$')]
    [string]$NewVersion,

    [Parameter(Mandatory=$false)]
    [string]$CommonVersion
)

# 1. Update VERSION file (create if missing)
# 2. Update plugin.json: version, optionally commonVersion
# 3. Update Tidalarr.csproj: Version, FileVersion, AssemblyVersion
# 4. Update TidalModule.cs: Version constant
# 5. Run verify-plugin.ps1
# 6. Git diff to show changes
# 7. Prompt for commit
```

### 2. Release Notes Generator
**File**: `.github/scripts/generate-release-notes.sh`
```bash
#!/bin/bash
# Extract CHANGELOG section for current version
# Parse commits since last tag
# Format with emoji and categories
# Generate full changelog link
# Output formatted markdown
```

### 3. Tag Release Helper
**File**: `scripts/tag-release.sh`
```bash
#!/bin/bash
# Validate VERSION file exists
# Read version from VERSION
# Create annotated tag with version
# Push tag to origin
# Provide release URL
```

### 4. Release Workflow
**File**: `.github/workflows/release.yml`
```yaml
name: Release

on:
  push:
    tags:
      - 'v*.*.*'
  workflow_dispatch:
    inputs:
      version:
        description: 'Version (e.g., 1.1.0)'
        required: true

# Jobs: validate, build, test, package, sign, release
```

## Implementation Roadmap

### Phase 1: Foundation (Critical - Do First)
1. **Create CHANGELOG.md**:
   - Initialize with existing versions (1.0.0, 1.0.1)
   - Add template for [Unreleased]
   - Document all historical changes

2. **Create VERSION file**:
   - Single source of truth (1.0.1)
   - Git-track this file

3. **Create bump-version.ps1**:
   - Update all 4 version locations
   - Integrate with verify-plugin.ps1
   - Add validation and error handling

4. **Document Conventional Commits**:
   - Add to CONTRIBUTING.md or AGENTS.md
   - Train team on commit format

### Phase 2: Automation (High Priority)
5. **Create release.yml Workflow**:
   - Tag-triggered release
   - Validation job (version consistency)
   - Build and test job (mandatory tests)
   - Package job (use existing scripts)
   - Release job (GitHub release creation)

6. **Create generate-release-notes.sh**:
   - Parse CHANGELOG.md
   - Parse git log
   - Format markdown output

7. **Create tag-release.sh Helper**:
   - Simplify git tag creation
   - Validate before tagging

### Phase 3: Enhancements (Medium Priority)
8. **Add Artifact Signing**:
   - Cosign keyless signing
   - Or GPG signing with secret key

9. **Generate SBOM**:
   - Use Anchore or Syft
   - Attach SPDX JSON to releases

10. **Add Checksum Generation**:
    - SHA256 for plugin ZIP
    - Attach to release assets

### Phase 4: Advanced (Future)
11. **Release Drafter**:
    - Auto-draft releases from PRs
    - Pre-fill release notes

12. **Pre-release Channel**:
    - Beta releases from develop branch
    - Automated versioning for betas

13. **Release Notifications**:
    - Discord/Slack announcements
    - Email notifications

## Troubleshooting

### Version Mismatch Errors
**Problem**: verify-plugin.ps1 fails with version mismatch
**Solution**: Use bump-version.ps1 to update all files atomically

### Missing CHANGELOG Entry
**Problem**: Release notes empty because CHANGELOG not updated
**Solution**: Always update CHANGELOG before version bump

### Tag Already Exists
**Problem**: Git tag creation fails
**Solution**: Delete existing tag: `git tag -d v1.1.0 && git push origin :refs/tags/v1.1.0`

### Test Failures During Release
**Problem**: Release workflow fails on tests
**Solution**: Run `./scripts/ci.ps1` locally before tagging

## Enhancement Opportunities

### Immediate Needs (Do These First)
1. ✅ **CHANGELOG.md Creation** - Track version history
2. ✅ **VERSION File** - Single source of truth
3. ✅ **bump-version.ps1** - Automate version sync
4. ✅ **release.yml Workflow** - Automate releases
5. ✅ **generate-release-notes.sh** - Automate notes

### Future Enhancements
6. **Artifact Signing** - Add security verification
7. **SBOM Generation** - Supply chain security
8. **Release Drafter** - Auto-draft from PRs
9. **Pre-release Channel** - Beta releases
10. **Release Analytics** - Track adoption

## Related Skills
- `code-quality` - Ensure tests pass before release
- `artifact-manager` - Handle package lifecycle
- `deployment-manager` - Deploy releases

## Examples

### Example 1: Complete Release Setup
**User**: "Set up automated releases for Tidalarr from scratch"
**Action**:
1. Create CHANGELOG.md with historical versions
2. Create VERSION file with current version (1.0.1)
3. Create scripts/bump-version.ps1
4. Create .github/workflows/release.yml
5. Create .github/scripts/generate-release-notes.sh
6. Create scripts/tag-release.sh
7. Document release process in docs/RELEASE_PROCESS.md
8. Test with a beta release (v1.0.2-beta.1)

### Example 2: Create First Automated Release
**User**: "Release version 1.1.0"
**Action**:
1. Update CHANGELOG.md [Unreleased] → [1.1.0]
2. Commit: `git commit -m "docs: changelog for v1.1.0"`
3. Run: `./scripts/bump-version.ps1 -NewVersion "1.1.0"`
4. Commit: `git commit -m "chore: bump version to v1.1.0"`
5. Run: `./scripts/tag-release.sh` (creates and pushes v1.1.0)
6. Monitor workflow at GitHub Actions
7. Verify release created with notes and artifacts

### Example 3: Fix Failed Release
**User**: "Release workflow failed during tests"
**Action**:
1. Check GitHub Actions logs for test failure
2. Fix failing tests locally
3. Commit fix
4. Delete bad tag: `git tag -d v1.1.0 && git push origin :refs/tags/v1.1.0`
5. Re-run tag-release.sh
6. Monitor new workflow run
