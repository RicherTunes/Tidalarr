#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run Tidalarr tests with correct build flags.

.DESCRIPTION
    Wrapper script that disables ILRepack during test builds to avoid type identity issues
    with common library types. Packaging/compliance tests that need the merged assembly
    are excluded by default.

.EXAMPLE
    ./scripts/test.ps1
    # Runs all unit tests (excludes Packaging and Compliance categories)

.EXAMPLE
    ./scripts/test.ps1 -c Release --filter "TidalApiClient"
    # Runs matching tests in Release config

.EXAMPLE
    ./scripts/test.ps1 --filter "Category=Packaging|Category=Compliance" -p:PluginPackagingDisable=false
    # Runs packaging/compliance tests against merged assembly
#>

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$PassThrough
)

# Exclude tests that require the merged (ILRepack'd) assembly
$defaultFilter = "Category!=Packaging&Category!=Compliance"
$hasFilter = $PassThrough -match '--filter'

$args = @(
    "test"
    "-p:PluginPackagingDisable=true"
    "-p:GeneratePackageOnBuild=false"
    "--nologo"
)

if (-not $hasFilter) {
    $args += "--filter"
    $args += $defaultFilter
}

$args += $PassThrough

Write-Host "dotnet $($args -join ' ')" -ForegroundColor Cyan
& dotnet @args
exit $LASTEXITCODE
