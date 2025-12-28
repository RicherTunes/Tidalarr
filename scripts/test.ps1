param(
    [string]$Configuration = 'Release',
    [string]$Solution = '',
    [string]$Filter = '',
    [string]$Settings = '',
    [string]$ResultsDirectory = '',
    [string[]]$Logger = @(),
    [string[]]$AdditionalArgs = @(),
    [switch]$IncludeCliTests,
    [switch]$NoBuild,
    [switch]$ExcludeHostBridge = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path "$PSScriptRoot/.."

if ([string]::IsNullOrWhiteSpace($Solution)) {
    $Solution = Join-Path $repoRoot 'Tidalarr.sln'
}

$effectiveFilter = $Filter
if (-not $IncludeCliTests) {
    if ([string]::IsNullOrWhiteSpace($effectiveFilter)) {
        $effectiveFilter = 'scope!=cli'
    }
    else {
        $effectiveFilter = "($effectiveFilter)&(scope!=cli)"
    }
}

Push-Location $repoRoot
try {
    $arguments = @(
        'test'
        $Solution
        '-c', $Configuration
        '-nr:false'
        '-p:BuildInParallel=false'
        '-p:UseSharedCompilation=false'
    )

    if ($ExcludeHostBridge) {
        $arguments += '-p:ExcludeHostBridge=true'
    }

    if ($NoBuild) {
        $arguments += '--no-build'
    }

    if (-not [string]::IsNullOrWhiteSpace($effectiveFilter)) {
        $arguments += '--filter', $effectiveFilter
    }

    if (-not [string]::IsNullOrWhiteSpace($Settings)) {
        $arguments += '--settings', $Settings
    }

    if (-not [string]::IsNullOrWhiteSpace($ResultsDirectory)) {
        $arguments += '--results-directory', $ResultsDirectory
    }

    foreach ($log in $Logger) {
        if (-not [string]::IsNullOrWhiteSpace($log)) {
            $arguments += '--logger', $log
        }
    }

    foreach ($arg in $AdditionalArgs) {
        if (-not [string]::IsNullOrWhiteSpace($arg)) {
            $arguments += $arg
        }
    }

    dotnet @arguments
}
finally {
    Pop-Location
}
