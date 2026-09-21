#requires -Version 7.0
<#
.SYNOPSIS
Enables WorkTrail's automatic C# whitespace formatting before local commits.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repositoryRoot
try {
    $current = & git config --get core.hooksPath
    if ($LASTEXITCODE -notin @(0, 1)) { throw 'Could not inspect the existing Git hooks configuration.' }
    if ($current -and $current -ne '.githooks') {
        # Existing custom hooks are never replaced silently.
        throw "An existing hooks path is configured: $current. Integrate the formatter there before changing it."
    }
    if (-not $current) {
        $commonDirectory = & git rev-parse --git-common-dir
        if ($LASTEXITCODE -ne 0) { throw 'Could not locate the default Git hooks directory.' }
        $defaultHooks = Join-Path $commonDirectory 'hooks'
        if (Test-Path -LiteralPath $defaultHooks) {
            $existingHooks = @(Get-ChildItem -LiteralPath $defaultHooks -File | Where-Object { $_.Name -notlike '*.sample' })
            if ($existingHooks.Count -gt 0) {
                throw 'The default Git hooks directory contains custom hooks. Integrate them before changing core.hooksPath.'
            }
        }
    }
    & git config --local core.hooksPath .githooks
    if ($LASTEXITCODE -ne 0) { throw 'Could not enable the repository Git hooks.' }
    Write-Host 'Enabled pre-commit C# whitespace formatting for this repository.'
}
finally {
    Pop-Location
}
