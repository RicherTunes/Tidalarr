param()

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
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

$files = @{
    Readme = Join-Path $repoRoot 'README.md'
    CliProject = Join-Path $repoRoot 'TidalCLI\TidalCLI.csproj'
    CliDiagnosticsTests = Join-Path $repoRoot 'tests\Tidalarr.Tests\CLI\CLIDiagnosticsTests.cs'
    CliArgParsingTests = Join-Path $repoRoot 'tests\Tidalarr.Tests\CLI\CLIArgParsingTests.cs'
}

foreach ($path in $files.Values) {
    Assert-Condition (Test-Path $path) "Missing file: $path"
}

if ($failures.Count -eq 0) {
    $readme = Get-Content -Raw $files.Readme
    $cliProject = Get-Content -Raw $files.CliProject
    $cliDiagnosticsTests = Get-Content -Raw $files.CliDiagnosticsTests
    $cliArgParsingTests = Get-Content -Raw $files.CliArgParsingTests

    Assert-Condition ($cliProject -match '<TargetFramework>net8\.0</TargetFramework>') `
        'TidalCLI must target net8.0.'
    Assert-Condition ($readme -notmatch 'net9\.0') `
        'README must not describe TidalCLI as net9.0.'
    Assert-Condition ($cliDiagnosticsTests -notmatch 'net9\.0') `
        'CLIDiagnosticsTests must resolve the CLI from the net8.0 output path.'
    Assert-Condition ($cliArgParsingTests -notmatch 'net9\.0') `
        'CLIArgParsingTests must resolve the CLI from the net8.0 output path.'
}

if ($failures.Count -gt 0) {
    Write-Host 'FAIL: TFM contract'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }
    exit 1
}

Write-Host 'PASS: TFM contract'
