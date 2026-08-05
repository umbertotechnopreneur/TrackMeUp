[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$VersionStatePath,
    [Parameter(Mandatory)] [string]$OutputPath,
    [Parameter(Mandatory)] [string]$PackageManifestPath,
    [Parameter(Mandatory)] [string]$Configuration,
    [Parameter(Mandatory)] [string]$Platform,
    [string]$RuntimeIdentifier = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Utf8Json {
    param([Parameter(Mandatory)] [string]$Path, [Parameter(Mandatory)] [object]$Value)

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    $json = $Value | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$resolvedStatePath = [System.IO.Path]::GetFullPath($VersionStatePath)
$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$resolvedManifestPath = [System.IO.Path]::GetFullPath($PackageManifestPath)

if (-not (Test-Path -LiteralPath $resolvedStatePath -PathType Leaf)) {
    throw "Build version state not found: $resolvedStatePath"
}

if (-not (Test-Path -LiteralPath $resolvedManifestPath -PathType Leaf)) {
    throw "Package manifest not found: $resolvedManifestPath"
}

$state = Get-Content -LiteralPath $resolvedStatePath -Raw | ConvertFrom-Json
if ($state.semVer -notmatch '^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)$') {
    throw "Invalid SemVer in build version state: '$($state.semVer)'"
}

$major = [int]$Matches.major
$minor = [int]$Matches.minor
$patch = [int]$Matches.patch + 1
$semVer = "$major.$minor.$patch"
$packageVersion = "$semVer.0"
$builtAtUtc = [DateTimeOffset]::UtcNow
$builtAtLocal = [DateTimeOffset]::Now

$gitCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or $gitCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'Unable to resolve the Git commit for this build.'
}

& git -C $repositoryRoot diff --quiet --ignore-submodules -- 2>$null
$trackedDirty = $LASTEXITCODE -ne 0
$untracked = & git -C $repositoryRoot ls-files --others --exclude-standard 2>$null
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the Git worktree for this build.'
}

$buildInfo = [ordered]@{
    schemaVersion = 1
    semVer = $semVer
    packageVersion = $packageVersion
    builtAtUtc = $builtAtUtc.ToString('O')
    builtAtLocal = $builtAtLocal.ToString('O')
    machineName = [Environment]::MachineName
    gitCommit = $gitCommit
    gitCommitShort = $gitCommit.Substring(0, 12)
    gitDirty = $trackedDirty -or @($untracked).Count -gt 0
    configuration = $Configuration
    platform = $Platform
    runtimeIdentifier = $RuntimeIdentifier
}

Write-Utf8Json -Path $resolvedStatePath -Value ([ordered]@{ semVer = $semVer })
Write-Utf8Json -Path $resolvedOutputPath -Value $buildInfo

[xml]$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw
$identity = $manifest.Package.Identity
if ($null -eq $identity) {
    throw 'Package manifest does not contain an Identity element.'
}

$identity.Version = $packageVersion
$writerSettings = [System.Xml.XmlWriterSettings]::new()
$writerSettings.Indent = $true
$writerSettings.Encoding = [System.Text.UTF8Encoding]::new($false)
$writerSettings.NewLineChars = [Environment]::NewLine
$writerSettings.OmitXmlDeclaration = $false
$writer = [System.Xml.XmlWriter]::Create($resolvedManifestPath, $writerSettings)
try {
    $manifest.Save($writer)
}
finally {
    $writer.Dispose()
}

Write-Host "TrackMeUp build $semVer ($($buildInfo.gitCommitShort)) generated at $resolvedOutputPath"
