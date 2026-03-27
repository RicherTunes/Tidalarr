param(
  [Parameter(Mandatory=$true)][string]$Repo,
  [switch]$DryRun
)

<#
.SYNOPSIS
Creates GitHub issues for prioritized tech-debt items using the GitHub CLI.

.USAGE
  ./scripts/create-tech-debt-issues.ps1 -Repo RicherTunes/Tidalarr
  ./scripts/create-tech-debt-issues.ps1 -Repo RicherTunes/Tidalarr -DryRun

.REQUIRES
  - gh CLI authenticated with 'repo' scope
#>

function New-Issue {
  param(
    [string]$Title,
    [string]$Body,
    [string[]]$Labels
  )
  if ($DryRun) {
    Write-Host "[DRYRUN] gh issue create --repo $Repo --title $Title --label $($Labels -join ',')" -ForegroundColor Yellow
    return
  }
  gh issue create --repo $Repo --title $Title --body $Body --label ($Labels -join ',') | Out-Null
}

# Ensure label exists
if (-not $DryRun) {
  try { gh label create tech-debt --repo $Repo --color FF8800 --description "Technical debt" 2>$null } catch {}
}

$issues = @(
  @{
    Title = 'test(hostbridge): add edge-case mapping tests (nulls/defaults)';
    Body = @'
Context: Basic HostBridge→core mapping tests exist. Add negative/edge cases.

Acceptance:
- [ ] Null/default values map safely.
- [ ] Invalid quality enum mapping guarded with sensible default.
References: tests/Tidalarr.Tests/Unit/HostBridgeMappingTests.cs
'@;
    Labels = @('tech-debt','tests')
  }
  ,@{
    Title = 'test(validation): strengthen path validation parity';
    Body = @'
Context: Core uses `PathValidationExtensions.IsReasonablePath`.

Acceptance:
- [ ] Tests for UNC paths, long paths, invalid chars, relative paths.
- [ ] Decide whether to move validation into Common.
References: src/Tidalarr/Integration/PathValidationExtensions.cs
'@;
    Labels = @('tech-debt','tests')
  }
  ,@{
    Title = 'ci(packaging): add dependency-closure gate for zip';
    Body = @'
Context: Prevent host assemblies from leaking into plugin zip.

Acceptance:
- [ ] CI job runs `build.ps1 -Package` for net8.0.
- [ ] Fails if zip contains disallowed assemblies (allowlist: `Lidarr.Plugin.Tidalarr.dll`, `Lidarr.Plugin.Common.dll`).
References: CLI packaging test (Trait scope=cli)
'@;
    Labels = @('tech-debt','ci')
  }
  ,@{
    Title = 'refactor(settings): reduce duplication between Core and HostBridge';
    Body = @'
Context: Display labels, ordering, defaults duplicated across layers.

Acceptance (choose one):
- [ ] Extract shared display metadata as constants used by both layers; OR
- [ ] Add a small source generator to emit HostBridge wrappers from core definitions.
References: src/Tidalarr/Integration/*Settings.cs, src/Tidalarr.HostBridge/Settings/*
'@;
    Labels = @('tech-debt')
  }
  ,@{
    Title = 'docs(tfms): document net8.0(core) vs net9.0(cli) rationale';
    Body = @'
Context: Different TFMs for core and CLI.

Acceptance:
- [ ] Add a short doc/rationale and guidance for future alignment.
References: README.md, docs/
'@;
    Labels = @('tech-debt','docs')
  }
  ,@{
    Title = 'test(diagnostics): add JSON snapshot tests (CFG000/IX200/DL100)';
    Body = @'
Context: Stabilize diagnostics JSON contract for tooling.

Acceptance:
- [ ] Snapshot tests for success+error shapes using PluginOperationResultJson.
- [ ] Document fields/ids in docs.
References: PluginOperationResultJson usage in CLI
'@;
    Labels = @('tech-debt','tests')
  }
  ,@{
    Title = 'feat(cli): harden argument validation and error messages';
    Body = @'
Context: CLI arg parsing should consistently report invalid enums/args.

Acceptance:
- [ ] Add validation + helpful errors for invalid quality/args.
- [ ] Unit tests cover parsing errors.
References: TidalCLI/Program.cs
'@;
    Labels = @('tech-debt','cli')
  }
  ,@{
    Title = 'feat(obs): propose shared observability events in Common';
    Body = @'
Context: Align telemetry/event IDs across plugins via Common.

Acceptance:
- [ ] Draft proposal PR to Common (observability events/minimal set).
- [ ] Track in this repo until upstream accepted.
References: Common repo; docs/alignment/
'@;
    Labels = @('tech-debt','common')
  }
  ,@{
    Title = 'ci: submodule pinning guard for ext/Lidarr.Plugin.Common';
    Body = @'
Context: Ensure submodule stays at required commit.

Acceptance:
- [ ] CI step validates submodule SHA vs. manifest/commonVersion and fails when diverged.
References: ext/Lidarr.Plugin.Common
'@;
    Labels = @('tech-debt','ci')
  }
)

foreach ($i in $issues) {
  New-Issue -Title $i.Title -Body $i.Body -Labels $i.Labels
}

Write-Host "Created $($issues.Count) tech-debt issues for $Repo (DryRun=$DryRun)." -ForegroundColor Green

