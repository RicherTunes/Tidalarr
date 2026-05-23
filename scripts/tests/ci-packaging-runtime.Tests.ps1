#Requires -Modules Pester

<#
.SYNOPSIS
    Phase 1 — runtime regression test: packaging failure must be fatal.

.DESCRIPTION
    Phase 0 made the packaging step non-swallowing (see ci-packaging-fatal.Tests.ps1 for the
    static-analysis proof).  This file adds a *runtime* counterpart: it actually invokes
    ci-package.ps1 with a shadow PluginPack module that throws, and asserts the process exits
    non-zero.

    Strategy:
    - Write a temp .psm1 that exports New-PluginPackage as a function that throws.
    - Launch pwsh -File scripts/ci-package.ps1 -ModulePath <shadow> in a child process.
    - Assert $LASTEXITCODE -ne 0.
    - Tear down the temp file in AfterAll.
#>

BeforeAll {
    $script:RepoRoot      = Resolve-Path (Join-Path $PSScriptRoot '..\..')
    $script:CiPackage     = Join-Path $PSScriptRoot '..\ci-package.ps1'

    # Shadow module: New-PluginPackage always throws.
    $script:ShadowDir     = Join-Path ([System.IO.Path]::GetTempPath()) "pester-shadow-$([System.Guid]::NewGuid().ToString('N'))"
    $null                 = New-Item -ItemType Directory -Path $script:ShadowDir -Force
    $script:ShadowPsm1    = Join-Path $script:ShadowDir 'PluginPack.psm1'

    Set-Content -Path $script:ShadowPsm1 -Encoding UTF8 -Value @'
function New-PluginPackage {
    param([string]$Csproj, [string]$Manifest, [string]$Framework, [string]$Configuration)
    throw "Simulated PluginPack failure — packaging is NOT optional."
}

Export-ModuleMember -Function New-PluginPackage
'@
}

AfterAll {
    if ($script:ShadowDir -and (Test-Path $script:ShadowDir)) {
        Remove-Item -Recurse -Force $script:ShadowDir
    }
}

Describe 'tidalarr/scripts/ci-package.ps1 — runtime packaging fatality' {

    It 'ci-package.ps1 exists' {
        Test-Path $script:CiPackage | Should -BeTrue
    }

    It 'exports LASTEXITCODE != 0 when New-PluginPackage throws' {
        # Run in a child process so the throw propagates to an OS exit code.
        $pwsh = (Get-Command pwsh -ErrorAction SilentlyContinue)?.Source
        if (-not $pwsh) { $pwsh = 'pwsh' }

        $proc = Start-Process -FilePath $pwsh `
            -ArgumentList @(
                '-NoProfile',
                '-NonInteractive',
                '-File', $script:CiPackage,
                '-RepoRoot', $script:RepoRoot,
                '-ModulePath', $script:ShadowPsm1
            ) `
            -PassThru `
            -Wait `
            -WindowStyle Hidden

        $proc.ExitCode | Should -Not -Be 0 -Because 'a thrown exception in New-PluginPackage must propagate as a non-zero exit code'
    }

    It 'exits 0 when New-PluginPackage succeeds' {
        # Write a shadow that succeeds silently.
        $successPsm1 = Join-Path $script:ShadowDir 'PluginPackSuccess.psm1'
        Set-Content -Path $successPsm1 -Encoding UTF8 -Value @'
function New-PluginPackage {
    param([string]$Csproj, [string]$Manifest, [string]$Framework, [string]$Configuration)
    # No-op success
}

Export-ModuleMember -Function New-PluginPackage
'@

        $pwsh = (Get-Command pwsh -ErrorAction SilentlyContinue)?.Source
        if (-not $pwsh) { $pwsh = 'pwsh' }

        $proc = Start-Process -FilePath $pwsh `
            -ArgumentList @(
                '-NoProfile',
                '-NonInteractive',
                '-File', $script:CiPackage,
                '-RepoRoot', $script:RepoRoot,
                '-ModulePath', $successPsm1
            ) `
            -PassThru `
            -Wait `
            -WindowStyle Hidden

        $proc.ExitCode | Should -Be 0 -Because 'a successful New-PluginPackage must exit 0'
    }
}
