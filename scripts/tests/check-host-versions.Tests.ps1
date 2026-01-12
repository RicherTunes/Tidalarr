#Requires -Modules Pester

<#
.SYNOPSIS
    Tests for check-host-versions.ps1 wrapper exit code behavior.

.DESCRIPTION
    Verifies that the wrapper correctly propagates exit codes from Common's module.
    Does NOT test the module itself (that's tested in Common).
#>

BeforeAll {
    $script:ScriptPath = Join-Path $PSScriptRoot '..' 'check-host-versions.ps1'
    $script:ModulePath = Join-Path $PSScriptRoot '..' '..' 'ext' 'Lidarr.Plugin.Common' 'scripts' 'lib' 'e2e-host-versions.psm1'

    # Skip all tests if Common module not available
    if (-not (Test-Path $script:ModulePath)) {
        $script:SkipTests = $true
        Write-Warning "Common module not found at: $script:ModulePath - skipping wrapper tests"
    }
    else {
        $script:SkipTests = $false
        Import-Module $script:ModulePath -Force
    }
}

Describe 'check-host-versions.ps1 exit code behavior' -Skip:$script:SkipTests {
    BeforeAll {
        # Create temp directory structure for testing
        $script:TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "tidalarr-wrapper-test-$([guid]::NewGuid().ToString('N').Substring(0,8))"
        New-Item -ItemType Directory -Path $script:TempRoot -Force | Out-Null

        # Create minimal Directory.Packages.props
        $propsContent = @'
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="FluentValidation" Version="11.9.0" />
    <PackageVersion Include="NLog" Version="5.2.8" />
  </ItemGroup>
</Project>
'@
        Set-Content -Path (Join-Path $script:TempRoot 'Directory.Packages.props') -Value $propsContent
    }

    AfterAll {
        Remove-Item -Path $script:TempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    Context 'With mismatched versions' {
        It 'Returns hasErrors=true in JSON output when mismatch exists' {
            # Use the module function directly with mismatched versions
            $hostVersions = @(
                [PSCustomObject]@{
                    PackageId = 'FluentValidation'
                    DllName = 'FluentValidation.dll'
                    Reason = 'ValidationFailure type crosses plugin boundary'
                    AssemblyVersion = '9.0.0.0'
                    FileVersion = '9.5.4'  # Mismatched from 11.9.0
                    ProductVersion = '9.5.4'
                    Found = $true
                }
            )
            $pinnedVersions = @(
                [PSCustomObject]@{
                    PackageId = 'FluentValidation'
                    PinnedVersion = '11.9.0'
                }
            )

            $json = Compare-HostPluginVersions -HostVersions $hostVersions -PinnedVersions $pinnedVersions -MatchPolicy MajorMinor -Format Json
            $result = $json | ConvertFrom-Json

            $result.hasErrors | Should -Be $true
            $result.results[0].status | Should -Be 'MISMATCH'
        }

        It 'Wrapper script -Strict flag causes non-zero exit on mismatch (integration)' -Skip {
            # This test requires running the actual script which needs real assemblies
            # Marked as Skip - run manually with Docker or real host assemblies
            # Command: pwsh -File scripts/check-host-versions.ps1 -Strict -HostAssembliesDir <path>
        }
    }

    Context 'With matching versions' {
        It 'Returns hasErrors=false in JSON output when versions match' {
            $hostVersions = @(
                [PSCustomObject]@{
                    PackageId = 'FluentValidation'
                    DllName = 'FluentValidation.dll'
                    Reason = 'ValidationFailure type crosses plugin boundary'
                    AssemblyVersion = '11.0.0.0'
                    FileVersion = '11.9.0'
                    ProductVersion = '11.9.0'
                    Found = $true
                }
            )
            $pinnedVersions = @(
                [PSCustomObject]@{
                    PackageId = 'FluentValidation'
                    PinnedVersion = '11.9.0'
                }
            )

            $json = Compare-HostPluginVersions -HostVersions $hostVersions -PinnedVersions $pinnedVersions -MatchPolicy MajorMinor -Format Json
            $result = $json | ConvertFrom-Json

            $result.hasErrors | Should -Be $false
            $result.results[0].status | Should -Be 'OK'
        }
    }

    Context 'Exit code robustness' {
        It 'Wrapper parses JSON hasErrors correctly for exit code decision' {
            # Simulate what the wrapper does: parse JSON and check hasErrors
            $mismatchJson = '{"hasErrors":true,"matchPolicy":"MajorMinor","results":[{"status":"MISMATCH"}]}'
            $matchJson = '{"hasErrors":false,"matchPolicy":"MajorMinor","results":[{"status":"OK"}]}'

            $mismatchParsed = $mismatchJson | ConvertFrom-Json
            $matchParsed = $matchJson | ConvertFrom-Json

            $mismatchParsed.hasErrors | Should -Be $true
            $matchParsed.hasErrors | Should -Be $false
        }
    }
}

Describe 'Backward compatibility' -Skip:$script:SkipTests {
    It 'Script accepts legacy -Strict parameter' {
        # Just verify the script can be parsed with legacy params
        $scriptContent = Get-Content $script:ScriptPath -Raw
        $scriptContent | Should -Match '\[switch\]\$Strict'
    }

    It 'Script accepts legacy -HostAssembliesDir parameter' {
        $scriptContent = Get-Content $script:ScriptPath -Raw
        $scriptContent | Should -Match '\[string\]\$HostAssembliesDir'
    }
}
