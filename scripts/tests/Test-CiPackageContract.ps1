param()

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$ciPath = Join-Path $repoRoot 'scripts\ci.ps1'
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

Assert-Condition (Test-Path $ciPath) "Missing CI script: $ciPath"

if ($failures.Count -eq 0) {
    $ci = Get-Content -Raw $ciPath

    Assert-Condition ($ci -match '\$pluginPackagePath\s*=\s*New-PluginPackage') `
        'CI must retain the canonical package path returned by Common New-PluginPackage.'
    Assert-Condition ($ci -notmatch 'Compress-Archive') `
        'CI must not create a second hand-rolled package with Compress-Archive.'
    Assert-Condition ($ci -match 'Lidarr\.Plugin\.Tidalarr-v\$\(\$manifest\.version\)\.net8\.0\.zip') `
        'CI artifact name must include the canonical plugin id, version, and net8.0 suffix.'
    Assert-Condition ($ci -match 'Copy-Item\s+-Path\s+\$pluginPackagePath\s+-Destination\s+\$packagePath') `
        'CI artifact must be copied from the package produced by New-PluginPackage.'
    Assert-Condition ($ci -notmatch 'if\s*\(\$IncludeCliTests\)\s*\{\s*throw\s*\}') `
        'CI package failures must be fatal in the default package path, not only when CLI tests are enabled.'
    Assert-Condition ($ci -match 'catch\s*\{\s*throw\s+"Packaging step failed:') `
        'CI package failure handler must throw with a clear error message.'
}

if ($failures.Count -gt 0) {
    Write-Host 'FAIL: CI package contract'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }
    exit 1
}

Write-Host 'PASS: CI package contract'
