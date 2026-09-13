#requires -Version 7.0
<#
.SYNOPSIS
Exercises automatic formatting in a disposable Git index without creating commits.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$fixtureParent = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/format-hook-tests'))
$fixture = Join-Path $fixtureParent ([Guid]::NewGuid().ToString('N'))
$utf8 = [Text.UTF8Encoding]::new($false)

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Invoke-FixtureGit {
    param([string[]]$Arguments)
    $result = & git -C $fixture @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Fixture Git command failed: $($Arguments[0])" }
    return $result
}

try {
    [void][IO.Directory]::CreateDirectory((Join-Path $fixture 'scripts'))
    [void][IO.Directory]::CreateDirectory((Join-Path $fixture '.githooks'))
    [void][IO.Directory]::CreateDirectory((Join-Path $fixture 'space name'))
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Format-Code.ps1') -Destination (Join-Path $fixture 'scripts/Format-Code.ps1')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot '.githooks/pre-commit') -Destination (Join-Path $fixture '.githooks/pre-commit')
    Invoke-FixtureGit -Arguments @('init', '--quiet')
    Invoke-FixtureGit -Arguments @('config', 'core.autocrlf', 'false')
    Invoke-FixtureGit -Arguments @('config', 'core.hooksPath', '.githooks')

    $rules = @'
root = true
[*.cs]
indent_style = space
indent_size = 4
end_of_line = lf
insert_final_newline = true
csharp_new_line_before_open_brace = all
csharp_preserve_single_line_blocks = false
csharp_preserve_single_line_statements = false
'@
    $fullPath = Join-Path $fixture 'space name/Full à.cs'
    $partialPath = Join-Path $fixture 'Partial.cs'
    $fullSource = "// SPDX-License-Identifier: MIT`npublic class Full{public int Value=1;}`n"
    $partialSource = "// SPDX-License-Identifier: MIT`npublic class Partial{public int Value=1;}`n"
    [IO.File]::WriteAllText((Join-Path $fixture '.editorconfig'), $rules, $utf8)
    [IO.File]::WriteAllText($fullPath, $fullSource, $utf8)
    [IO.File]::WriteAllText($partialPath, $partialSource, $utf8)
    Invoke-FixtureGit -Arguments @('add', '--', '.editorconfig', 'space name/Full à.cs', 'Partial.cs')

    # Both unstaged source edits and unstaged formatting rules must stay out of the index.
    $unstagedSource = $partialSource + "// UNSTAGED_CHANGE`n"
    [IO.File]::WriteAllText($partialPath, $unstagedSource, $utf8)
    [IO.File]::WriteAllText((Join-Path $fixture '.editorconfig'), $rules.Replace('indent_size = 4', 'indent_size = 8'), $utf8)
    $partialBefore = [Convert]::ToBase64String([IO.File]::ReadAllBytes($partialPath))

    Invoke-FixtureGit -Arguments @('hook', 'run', 'pre-commit')
    $fullAfter = [IO.File]::ReadAllText($fullPath)
    $stagedFull = (Invoke-FixtureGit -Arguments @('show', ':space name/Full à.cs')) -join "`n"
    $stagedPartial = (Invoke-FixtureGit -Arguments @('show', ':Partial.cs')) -join "`n"
    Assert-Condition ($fullAfter -match '(?m)^ {4}public int Value = 1;') 'The fully staged file was not automatically formatted with the staged rules.'
    Assert-Condition ($stagedFull -match '(?m)^ {4}public int Value = 1;') 'The index did not receive the fully staged formatting fix.'
    Assert-Condition ($stagedPartial -match '(?m)^ {4}public int Value = 1;') 'The partial index blob was not formatted.'
    Assert-Condition (-not $stagedPartial.Contains('UNSTAGED_CHANGE')) 'Unstaged content entered the index.'
    Assert-Condition ($partialBefore -ceq [Convert]::ToBase64String([IO.File]::ReadAllBytes($partialPath))) 'The partially staged working file changed.'
    Write-Host 'PASS: automatic staging, partial staging, staged rules, spaces and Unicode filenames.'

    $indexBefore = (Invoke-FixtureGit -Arguments @('ls-files', '--stage')) -join "`n"
    Invoke-FixtureGit -Arguments @('hook', 'run', 'pre-commit')
    Assert-Condition ($indexBefore -ceq ((Invoke-FixtureGit -Arguments @('ls-files', '--stage')) -join "`n")) 'A second hook run changed an already formatted index.'
    Write-Host 'PASS: formatting is idempotent.'

    # Read-only verification must fail on malformed source without modifying either copy.
    $failureOutput = & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Format-Code.ps1') -RepositoryRoot $fixture -Verify 2>&1
    Assert-Condition ($LASTEXITCODE -ne 0) 'Read-only verification accepted malformed source.'
    Assert-Condition ($failureOutput.Count -gt 0) 'Formatting failure did not explain the problem.'
    Assert-Condition ($partialBefore -ceq [Convert]::ToBase64String([IO.File]::ReadAllBytes($partialPath))) 'Read-only verification edited a source file.'
    Assert-Condition ($indexBefore -ceq ((Invoke-FixtureGit -Arguments @('ls-files', '--stage')) -join "`n")) 'Read-only verification changed the index.'
    Write-Host 'PASS: verification fails safely without edits.'

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Format-Code.ps1') -RepositoryRoot $fixture
    Assert-Condition ($LASTEXITCODE -eq 0) 'Manual whitespace formatting failed.'
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Format-Code.ps1') -RepositoryRoot $fixture -Verify
    Assert-Condition ($LASTEXITCODE -eq 0) 'Formatted working files do not pass the CI verification command.'
    Assert-Condition ([IO.File]::ReadAllText($partialPath).Contains('UNSTAGED_CHANGE')) 'Manual formatting lost an unstaged edit.'
    Assert-Condition ($indexBefore -ceq ((Invoke-FixtureGit -Arguments @('ls-files', '--stage')) -join "`n")) 'Manual formatting staged changes.'
    Write-Host 'PASS: manual formatting and CI verification agree, without staging edits.'

    Invoke-FixtureGit -Arguments @('read-tree', '--empty')
    Invoke-FixtureGit -Arguments @('hook', 'run', 'pre-commit')
    Write-Host 'PASS: an empty index needs no formatting.'
}
finally {
    if (Test-Path -LiteralPath $fixture) {
        # Keep cleanup confined to the unique fixture, never the repository or its parent.
        $resolvedFixture = (Resolve-Path -LiteralPath $fixture).Path
        if (-not $resolvedFixture.StartsWith($fixtureParent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove a fixture outside artifacts/format-hook-tests.'
        }
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
