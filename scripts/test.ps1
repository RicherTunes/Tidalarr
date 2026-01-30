<#
.SYNOPSIS
    Runs Tidalarr unit tests with proper packaging flags.

.DESCRIPTION
    This script ensures tests run in "unmerged" mode (PluginPackagingDisable=true)
    so that ILRepack internalization doesn't break type identity in tests.

    Uses shared test-runner.psm1 module from Lidarr.Plugin.Common for standardized
    TRX parsing and result display.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Debug

.PARAMETER Filter
    Optional test filter expression (e.g., "FullyQualifiedName~TidalApiClient")

.PARAMETER ExcludeHostBridge
    Exclude HostBridge tests (useful for CI where host assemblies may not be available)

.PARAMETER Coverage
    Enable code coverage collection

.PARAMETER Verbose
    Enable verbose output

.EXAMPLE
    ./scripts/test.ps1
    ./scripts/test.ps1 -Filter "FullyQualifiedName~TidalApiClient"
    ./scripts/test.ps1 -ExcludeHostBridge -Coverage
#>

param(
    [string]$Configuration = "Debug",
    [string]$Filter = "",
    [switch]$ExcludeHostBridge = $false,
    [switch]$Coverage = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

# Import shared test runner module from Common submodule
$CommonScripts = Join-Path $PSScriptRoot "../ext/Lidarr.Plugin.Common/scripts/lib"
Import-Module (Join-Path $CommonScripts "test-runner.psm1") -Force

Write-Host "[TEST] Tidalarr Unit Test Runner" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$TestProject = Join-Path $ProjectRoot "tests/Tidalarr.Tests/Tidalarr.Tests.csproj"
$OutputDir = Join-Path $ProjectRoot "TestResults"

# Ensure output directory exists
if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "[INFO] Test Project: $TestProject" -ForegroundColor Gray
Write-Host "[INFO] Output Directory: $OutputDir" -ForegroundColor Gray
Write-Host ""

# Build with PluginPackagingDisable=true to avoid ILRepack internalization issues
Write-Host "[BUILD] Building test project (unmerged mode)..." -ForegroundColor Yellow

$buildArgs = @(
    "build", $TestProject,
    "--configuration", $Configuration,
    "-p:PluginPackagingDisable=true",
    "-p:RunAnalyzersDuringBuild=false",
    "-p:EnableNETAnalyzers=false",
    "-p:TreatWarningsAsErrors=false"
)

if ($Verbose) {
    $buildArgs += @("--verbosity", "detailed")
} else {
    $buildArgs += @("--verbosity", "minimal")
}

& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Build successful!" -ForegroundColor Green
Write-Host ""

# Run tests
Write-Host "[TEST] Running tests..." -ForegroundColor Yellow

$testArgs = @(
    "test", $TestProject,
    "--configuration", $Configuration,
    "--no-build",
    "--logger", "trx;LogFileName=Tidalarr.Tests.trx",
    "--results-directory", $OutputDir
)

# Apply filters
$effectiveFilter = $Filter
if ($ExcludeHostBridge) {
    $hostBridgeFilter = "FullyQualifiedName!~HostBridge"
    if ($effectiveFilter) {
        $effectiveFilter = "($effectiveFilter) & ($hostBridgeFilter)"
    } else {
        $effectiveFilter = $hostBridgeFilter
    }
}

if ($effectiveFilter) {
    Write-Host "[INFO] Test filter: $effectiveFilter" -ForegroundColor Gray
    $testArgs += @("--filter", $effectiveFilter)
}

if ($Coverage) {
    $testArgs += @("--collect", "XPlat Code Coverage")
}

if ($Verbose) {
    $testArgs += @("--verbosity", "detailed")
} else {
    $testArgs += @("--verbosity", "normal")
}

& dotnet @testArgs
$testExitCode = $LASTEXITCODE

Write-Host ""

# Parse and display results using shared module
$trxFile = Join-Path $OutputDir "Tidalarr.Tests.trx"
$summary = Get-TrxTestSummary -TrxPath $trxFile
if ($summary) {
    Write-TestSummary -Summary $summary
}

Write-Host ""
Write-Host "Results saved to: $OutputDir" -ForegroundColor Gray

if ($testExitCode -eq 0) {
    Write-Host "[OK] All tests passed!" -ForegroundColor Green
} else {
    Write-Host "[FAIL] Some tests failed!" -ForegroundColor Red
}

exit $testExitCode
