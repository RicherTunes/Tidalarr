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

$activeDocs = @(
    'CLAUDE.md',
    'docs/packaging-closure.md',
    'docs/ci-gates-verification.md',
    'docs/operations/multi-plugin-alignment.md'
)

foreach ($relativePath in $activeDocs) {
    $path = Join-Path $repoRoot $relativePath
    Assert-Condition (Test-Path -LiteralPath $path) "Missing active documentation file: $relativePath"
}

if ($failures.Count -eq 0) {
    $workflowDir = Join-Path $repoRoot '.github\workflows'
    $existingWorkflows = @{}
    if (Test-Path -LiteralPath $workflowDir) {
        Get-ChildItem -LiteralPath $workflowDir -Filter '*.yml' -File | ForEach-Object {
            $existingWorkflows[$_.Name] = $true
        }
        Get-ChildItem -LiteralPath $workflowDir -Filter '*.yaml' -File | ForEach-Object {
            $existingWorkflows[$_.Name] = $true
        }
    }

    foreach ($relativePath in $activeDocs) {
        $path = Join-Path $repoRoot $relativePath
        $content = Get-Content -LiteralPath $path -Raw
        $matches = [regex]::Matches($content, '\.github/workflows/([A-Za-z0-9_.-]+\.yml)')
        foreach ($match in $matches) {
            $workflowName = $match.Groups[1].Value
            Assert-Condition ($existingWorkflows.ContainsKey($workflowName)) "$relativePath references missing workflow $workflowName."
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'FAIL: Workflow docs contract'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }
    exit 1
}

Write-Host 'PASS: Workflow docs contract'
