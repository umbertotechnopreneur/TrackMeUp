[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$webRoot = Join-Path $repositoryRoot 'TrackMeUp.Reports.Web'
$manifestPath = Join-Path $webRoot 'package.json'
$outputIndex = Join-Path $webRoot 'dist\index.html'

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Reports web manifest not found: $manifestPath"
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw 'npm was not found on PATH. Node.js 20.19.0 or newer is required.'
}

Push-Location -LiteralPath $webRoot
try {
    & npm ci
    if ($LASTEXITCODE -ne 0) {
        throw "npm ci failed with exit code $LASTEXITCODE."
    }

    & npm run build
    if ($LASTEXITCODE -ne 0) {
        throw "Reports web build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $outputIndex -PathType Leaf)) {
    throw "Reports web build completed without producing: $outputIndex"
}

Write-Host "Reports web assets ready at: $(Split-Path -Parent $outputIndex)"
