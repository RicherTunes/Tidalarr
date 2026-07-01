param()

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$workflowPath = Join-Path $repoRoot '.gitea\workflows\ci.yml'
$verifyLocalPath = Join-Path $repoRoot 'scripts\verify-local.ps1'
$failures = New-Object System.Collections.Generic.List[string]
$runnerOwnedScriptPattern = '(ecosystem-parity-lint|lint-date-parsing|lint-sync-over-async|lint-test-traits|lint-doc-script-refs|lint-gitea-secret-scan)\.ps1'
$runnerSkipSwitchPattern = '-(SkipDateParsing|SkipSyncOverAsync|SkipTestTraits|SkipEcosystemParity|SkipVersionContract|SkipPluginContractTests|SkipDocRefs|SkipGiteaSecretScan)\b'

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        $script:failures.Add($Message)
    }
}

Assert-Condition (Test-Path -LiteralPath $workflowPath) "Missing Gitea CI workflow: $workflowPath"
Assert-Condition (Test-Path -LiteralPath $verifyLocalPath) "Missing verify-local wrapper: $verifyLocalPath"

if ($failures.Count -eq 0) {
    $rawContent = Get-Content -LiteralPath $workflowPath -Raw
    $content = ((Get-Content -LiteralPath $workflowPath | Where-Object {
        -not $_.TrimStart().StartsWith('#')
    }) -join "`n")
    $verifyLocal = Get-Content -LiteralPath $verifyLocalPath -Raw

    Assert-Condition ($content -match '(?m)^\s*verify:\s*$') `
        'Gitea CI must include a verify job, not only lint.'
    Assert-Condition ($content -match 'scripts/verify-local\.ps1') `
        'Gitea verify job must run the shared Common local-ci pipeline through scripts/verify-local.ps1.'
    Assert-Condition ($content -match '8\.0\.x') `
        'Gitea verify job must install/use the .NET 8 SDK.'
    Assert-Condition ($content -match 'pwsh .*scripts/verify-local\.ps1|pwsh\s+\.\/scripts\/verify-local\.ps1') `
        'Gitea verify job must invoke verify-local.ps1 with PowerShell.'
    Assert-Condition ($content -match 'run-plugin-lint-gates\.ps1') `
        'Gitea lint job must invoke Common run-plugin-lint-gates.ps1.'
    Assert-Condition ($content -notmatch $runnerOwnedScriptPattern) `
        'Gitea lint job must not call direct Common lint scripts; direct fallback subsets can silently bypass new Common gates.'
    Assert-Condition ($content -notmatch $runnerSkipSwitchPattern) `
        'Gitea lint job must not pass skip switches to the shared Common lint runner.'
    Assert-Condition ($content -notmatch '-SkipTests') `
        'Gitea verify job must not skip tests.'
    Assert-Condition ($content -notmatch '-Skip[A-Za-z]*|-NoRestore') `
        'Gitea verify job must not pass skip/fast-iteration switches to verify-local.ps1.'
    Assert-Condition ($content -notmatch '(?m)^\s*continue-on-error\s*:\s*true\s*$') `
        'Gitea verify job must not be allowed to continue on failure.'
    Assert-Condition ($content -notmatch '(?m)^\s*if\s*:\s*(false|\$\{\{\s*false\s*\}\})\s*$') `
        'Gitea verify job must not be disabled with if: false.'
    Assert-Condition ($content -notmatch '\|\|\s*true') `
        'Gitea verify job must not swallow failures with || true.'
    Assert-Condition ($content -notmatch '(?m)^\s*exit\s+0\s*$') `
        'Gitea verify job must not force a successful exit.'
    Assert-Condition ($rawContent -notmatch 'LINT-ONLY|LOCAL-VALIDATION-ONLY|build \+ tests remain LOCAL') `
        'Gitea CI comments must not describe build/test as local-only after enabling verify.'
    Assert-Condition ($verifyLocal -match 'RequireHermeticTests\s*=\s*\$true') `
        'verify-local.ps1 must fail if the Gitea hermetic test filter matches zero tests.'
}

if ($failures.Count -gt 0) {
    Write-Host 'FAIL: Gitea full CI contract'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }
    exit 1
}

Write-Host 'PASS: Gitea full CI contract'
