#Requires -Modules Pester

<#
.SYNOPSIS
    Phase 0.6 — scripts/ci.ps1 must treat PluginPack packaging failure as fatal.

.DESCRIPTION
    Prior behavior swallowed PluginPack failures inside a try/catch that only
    re-threw when -IncludeCliTests was supplied. That let PR/CI runs declare
    success even when the plugin couldn't be packaged. This test pins the
    expected behavior: the packaging block must not contain a conditional
    swallow keyed off $IncludeCliTests.
#>

BeforeAll {
    $script:CiScript = Join-Path $PSScriptRoot '..' 'ci.ps1'
}

Describe 'tidalarr/scripts/ci.ps1 — packaging fatality' {

    It 'exists' {
        Test-Path $script:CiScript | Should -BeTrue
    }

    It 'does not gate packaging-failure rethrow on -IncludeCliTests' {
        $content = Get-Content $script:CiScript -Raw
        # Forbidden pattern: catch block that only throws when CLI tests enabled.
        $content | Should -Not -Match 'if\s*\(\s*\$IncludeCliTests\s*\)\s*\{\s*throw\s*\}'
    }

    It 'PluginPack invocation is not wrapped in a swallowing try/catch' {
        $content = Get-Content $script:CiScript -Raw
        # The previous shape: try { ... New-PluginPackage ... } catch { Write-Warning ... }
        $content | Should -Not -Match '(?s)New-PluginPackage[^}]*}\s*catch\s*\{\s*Write-Warning'
    }
}
