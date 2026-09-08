param()

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$hostVersion = '3.1.2.4913'
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

$psBuildPath = Join-Path $repoRoot 'build.ps1'
$shBuildPath = Join-Path $repoRoot 'build.sh'
$manifestPath = Join-Path $repoRoot 'plugin.json'
$verifyLocalPath = Join-Path $repoRoot 'scripts\verify-local.ps1'
$verifyPluginPath = Join-Path $repoRoot 'scripts\verify-plugin.ps1'
$runtimeFixturePath = Join-Path $repoRoot 'tests\Tidalarr.Tests\Runtime\LidarrContainerFixture.cs'

foreach ($path in @($psBuildPath, $shBuildPath, $manifestPath, $verifyLocalPath, $verifyPluginPath, $runtimeFixturePath)) {
    Assert-Condition (Test-Path $path) "Missing build script: $path"
}

if ($failures.Count -eq 0) {
    $ps = Get-Content -Raw $psBuildPath
    $sh = Get-Content -Raw $shBuildPath
    $manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json
    $verifyLocal = Get-Content -Raw $verifyLocalPath
    $verifyPlugin = Get-Content -Raw $verifyPluginPath
    $runtimeFixture = Get-Content -Raw $runtimeFixturePath

    foreach ($entry in @(
        @{ Name = 'PowerShell build'; Content = $ps },
        @{ Name = 'Bash build'; Content = $sh }
    )) {
        Assert-Condition ($entry.Content -notmatch '2\.13\.2\.468[56]') "$($entry.Name) must not default to pre-net8 Lidarr host versions."
        Assert-Condition ($entry.Content -match [regex]::Escape($hostVersion)) "$($entry.Name) must advertise the current CI host assembly version $hostVersion."
    }

    Assert-Condition ($ps -match "\[string\]\`$LidarrVersion\s*=\s*`"$([regex]::Escape($hostVersion))`"") `
        'PowerShell build default LidarrVersion must match the current CI host assembly version.'
    Assert-Condition ($sh -match "LIDARR_VERSION=`"$([regex]::Escape($hostVersion))`"") `
        'Bash build default LIDARR_VERSION must match the current CI host assembly version.'
    Assert-Condition ($manifest.minHostVersion -eq $hostVersion) `
        'Plugin manifest minimum host version must match the supported host floor.'
    Assert-Condition ($verifyLocal -match "LidarrDockerVersion\s*=\s*'pr-plugins-$([regex]::Escape($hostVersion))'") `
        'Local verification must extract assemblies from the supported host floor.'
    Assert-Condition ($verifyPlugin -match "hostVersionTarget\s*=\s*'$([regex]::Escape($hostVersion))'") `
        'Manifest verification must enforce the supported host floor.'
    Assert-Condition ($runtimeFixture -match "DockerImage:\s*`"ghcr\.io/hotio/lidarr:pr-plugins-$([regex]::Escape($hostVersion))`"") `
        'Runtime fixture must exercise the supported host floor by default.'
}

if ($failures.Count -gt 0) {
    Write-Host 'FAIL: Build host version contract'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }
    exit 1
}

Write-Host 'PASS: Build host version contract'
