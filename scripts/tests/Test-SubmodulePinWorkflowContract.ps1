param()

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$shaPath = Join-Path $repoRoot 'ext-common-sha.txt'
$githubPinWorkflow = Join-Path $repoRoot '.github\workflows\submodule-pin.yml'
$githubCiWorkflow = Join-Path $repoRoot '.github\workflows\ci.yml'
$giteaCiWorkflow = Join-Path $repoRoot '.gitea\workflows\ci.yml'
$expectedContentsWrapper = Join-Path $repoRoot 'scripts\update-expected-contents.ps1'
$sharedExpectedContentsUpdater = Join-Path $repoRoot 'ext\Lidarr.Plugin.Common\scripts\update-plugin-expected-contents.ps1'
$failures = New-Object System.Collections.Generic.List[string]

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        $script:failures.Add($Message)
    }
}

function Assert-WorkflowHasPinGuard {
    param(
        [string]$Path,
        [string]$Name
    )

    Assert-Condition (Test-Path -LiteralPath $Path) "Missing $Name workflow: $Path"
    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $content = Get-Content -LiteralPath $Path -Raw
    Assert-Condition ($content -match 'Common submodule pin guard') "$Name must include a named Common submodule pin guard step."
    Assert-Condition ($content -match 'repin-common-submodule\.sh') "$Name must run Common's repin guard script."
    Assert-Condition ($content -match '--verify-only') "$Name must run the guard in verify-only mode."
    Assert-Condition ($content -match 'ext/Lidarr\.Plugin\.Common') "$Name must verify the canonical Common submodule path."
}

Assert-Condition (Test-Path -LiteralPath $shaPath) "Missing ext-common-sha.txt: $shaPath"
if (Test-Path -LiteralPath $shaPath) {
    $sha = (Get-Content -LiteralPath $shaPath -Raw).Trim()
    Assert-Condition ($sha -match '^[0-9a-f]{40}$') 'ext-common-sha.txt must contain a lowercase 40-character SHA.'
}

Assert-WorkflowHasPinGuard -Path $githubPinWorkflow -Name 'GitHub submodule-pin'
Assert-WorkflowHasPinGuard -Path $githubCiWorkflow -Name 'GitHub CI'
Assert-WorkflowHasPinGuard -Path $giteaCiWorkflow -Name 'Gitea CI'

if (Test-Path -LiteralPath $githubCiWorkflow) {
    $githubContent = Get-Content -LiteralPath $githubCiWorkflow -Raw
    Assert-Condition ($githubContent -notmatch 'exit\s+\$LASTEXITCODE') `
        'GitHub CI must normalize nullable LASTEXITCODE values before exiting.'
    Assert-Condition ($githubContent -match '\$gateExitCode') `
        'GitHub CI fallback gates must normalize nullable LASTEXITCODE before deciding to exit.'
    Assert-Condition ($githubContent -match '\$runnerExitCode') `
        'GitHub CI shared lint runner path must normalize nullable LASTEXITCODE before exiting.'
}

if (Test-Path -LiteralPath $giteaCiWorkflow) {
    $giteaContent = Get-Content -LiteralPath $giteaCiWorkflow -Raw
    Assert-Condition ($giteaContent -notmatch 'dead \.github/workflows') `
        'Gitea CI comments must not call GitHub workflows dead; GitHub workflows are present and GitHub-gated.'
    Assert-Condition ($giteaContent -notmatch 'neutralized on the Gitea copy') `
        'Gitea CI comments must not claim GitHub workflows need neutralizing on the Gitea copy.'
    Assert-Condition ($giteaContent -notmatch 'exit\s+\$LASTEXITCODE') `
        'Gitea CI must normalize nullable LASTEXITCODE values before exiting.'
    Assert-Condition ($giteaContent -match '\$gateExitCode') `
        'Gitea CI fallback gates must normalize nullable LASTEXITCODE before deciding to exit.'
    Assert-Condition ($giteaContent -match '\$runnerExitCode') `
        'Gitea CI shared lint runner path must normalize nullable LASTEXITCODE before exiting.'
}

Assert-Condition (Test-Path -LiteralPath $sharedExpectedContentsUpdater) `
    "Missing shared expected-contents updater: $sharedExpectedContentsUpdater"
Assert-Condition (Test-Path -LiteralPath $expectedContentsWrapper) `
    "Missing plugin expected-contents wrapper: $expectedContentsWrapper"
if (Test-Path -LiteralPath $expectedContentsWrapper) {
    $wrapperContent = Get-Content -LiteralPath $expectedContentsWrapper -Raw
    Assert-Condition ($wrapperContent -match 'update-plugin-expected-contents\.ps1') `
        'Plugin expected-contents wrapper must delegate to Common shared updater.'
    Assert-Condition ($wrapperContent -match '-RequireCanonicalAbstractions') `
        'Plugin expected-contents wrapper must keep the canonical abstractions packaging gate enabled.'
}

if ($failures.Count -gt 0) {
    Write-Host 'FAIL: Submodule pin workflow contract'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }
    exit 1
}

Write-Host 'PASS: Submodule pin workflow contract'
