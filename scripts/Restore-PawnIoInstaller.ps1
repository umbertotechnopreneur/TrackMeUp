#requires -Version 7.0
# SPDX-License-Identifier: MIT
<#
.SYNOPSIS
Restores the pinned, signed PawnIO installer into the hardware project's build cache.
.DESCRIPTION
Verifies the exact SHA-256 and Authenticode signature. Never executes the installer.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Destination)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'TrackMeUp.Hardware/obj'))
$resolvedDestination = [IO.Path]::GetFullPath($Destination)
if (-not $resolvedDestination.StartsWith($allowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'PawnIO installer cache must remain inside the hardware project obj directory.'
}
for ($ancestor = $resolvedDestination; -not [string]::IsNullOrEmpty($ancestor); $ancestor = Split-Path -Parent $ancestor) {
    if ((Test-Path -LiteralPath $ancestor) -and
        ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'PawnIO installer cache cannot pass through a symbolic link or junction.'
    }
}
$distribution = Get-Content -LiteralPath (Join-Path $repositoryRoot 'TrackMeUp.Hardware/PawnIO/distribution.json') -Raw | ConvertFrom-Json
if ($distribution.sha256 -cnotmatch '^[0-9A-F]{64}$' -or
    $distribution.url -cne "https://github.com/namazso/PawnIO.Setup/releases/download/$($distribution.version)/PawnIO_setup.exe") {
    throw 'Invalid pinned PawnIO distribution.'
}
[void][IO.Directory]::CreateDirectory($resolvedDestination)
$installerPath = Join-Path $resolvedDestination 'PawnIO_setup.exe'
if (-not (Test-Path -LiteralPath $installerPath)) {
    # Validate downloaded bytes before committing them to the cache; no runtime download is needed.
    $response = Invoke-WebRequest -Uri $distribution.url -TimeoutSec 120
    $bytes = [byte[]]$response.Content
    if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)) -cne $distribution.sha256) {
        throw 'Downloaded PawnIO installer checksum mismatch.'
    }
    [IO.File]::WriteAllBytes($installerPath, $bytes)
}
if (((Get-Item -LiteralPath $installerPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -or
    (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash -cne $distribution.sha256) {
    throw 'Cached PawnIO installer is invalid or has a checksum mismatch.'
}
if ((Get-AuthenticodeSignature -LiteralPath $installerPath).Status -ne 'Valid') {
    throw 'The pinned PawnIO installer signature is not trusted and valid.'
}
