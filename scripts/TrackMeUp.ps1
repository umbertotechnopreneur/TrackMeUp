#requires -Version 7.0
<#
.SYNOPSIS
Unified TrackMeUp repository utility entrypoint.

.DESCRIPTION
Runs repository build, test, packaging, validation, asset, and helper tasks from
one agent-friendly CLI. With no arguments, opens an interactive control center
using the repository shared preflight, banner, footer, and menu helpers.

.EXAMPLE
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1

.EXAMPLE
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Test -Platform x64

.EXAMPLE
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action PackageMsix -Platform x64
#>
[CmdletBinding()]
param(
    [ValidateSet(
        'Menu',
        'Preflight',
        'Restore',
        'Build',
        'Test',
        'BuildReports',
        'TestCli',
        'ValidateStoreListing',
        'GenerateAssets',
        'ProbeTaskbar',
        'PublishUnpackaged',
        'PackageMsix',
        'ProtectSecret',
        'ProtectSecretYubiKey',
        'BuildInfo'
    )]
    [string]$Action = 'Menu',

    [ValidateSet('x86', 'x64', 'ARM64')]
    [string]$Platform = 'x64',

    [ValidateSet('Debug', 'Release', 'Debug-Unpackaged', 'Release-Unpackaged')]
    [string]$Configuration = 'Debug',

    [switch]$WarnAsError,
    [switch]$AsJson,
    [switch]$SkipRestore,
    [switch]$Help,
    [string]$ExecutablePath,
    [string]$ListingPath,
    [ValidateSet('Menu', 'Preflight', 'Protect', 'Reveal', 'Copy', 'PrintAndCopy', 'Inspect', 'SetupSlot', 'MigrateCredentialXml')]
    [string]$SecretToolAction = 'Menu',
    [string]$SecretPath,
    [string]$CredentialXmlPath,
    [string]$SecretName,
    [string]$InputValue,
    [ValidateSet('1', '2')]
    [string]$Slot = '2',
    [switch]$Force,
    [switch]$SkipPause,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$PassThruArguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:StartedAt = Get-Date
$script:ScriptName = Split-Path -Leaf $PSCommandPath
$script:RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:ModulesRoot = Join-Path $PSScriptRoot 'Common\modules'
$script:InteractiveMode = $Action -eq 'Menu' -and $MyInvocation.BoundParameters.Count -eq 0 -and @($PassThruArguments).Count -eq 0

function Import-TrackMeUpScriptModules {
    foreach ($moduleName in @(
        'shared-bootstrap.ps1',
        'boot-banner.ps1',
        'footer-banner.ps1',
        'menu-utils.ps1',
        'preflight-checks.ps1'
    )) {
        $modulePath = Join-Path $script:ModulesRoot $moduleName
        if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
            throw "Shared script module not found: $modulePath"
        }

        . $modulePath
    }
}

function Get-TrackMeUpRuntimeIdentifier {
    param([Parameter(Mandatory)][string]$TargetPlatform)

    return "win-$($TargetPlatform.ToLowerInvariant())"
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Invoke-RepositoryScript {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [string[]]$Arguments = @()
    )

    $scriptPath = Join-Path $script:RepositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "Repository script not found: $scriptPath"
    }

    & $scriptPath @Arguments
}

function Invoke-TrackMeUpPreflight {
    $requiredFiles = @(
        (Join-Path $script:RepositoryRoot 'TrackMeUp.slnx'),
        (Join-Path $script:RepositoryRoot 'TrackMeUp\TrackMeUp.csproj'),
        (Join-Path $script:RepositoryRoot 'TrackMeUp.Reports.Web\package.json'),
        (Join-Path $script:RepositoryRoot 'store\listing.json')
    )

    $ok = Invoke-CommonPreflight `
        -Title 'TrackMeUp preflight' `
        -RequiredCommands @('pwsh', 'git', 'dotnet') `
        -RequiredFiles $requiredFiles

    if (-not $ok) {
        throw 'TrackMeUp preflight failed.'
    }
}

function Invoke-TrackMeUpRestore {
    Invoke-NativeCommand -FilePath 'dotnet' -Arguments @('restore', (Join-Path $script:RepositoryRoot 'TrackMeUp.slnx'))
}

function Invoke-TrackMeUpBuild {
    $arguments = @('build', (Join-Path $script:RepositoryRoot 'TrackMeUp.slnx'), "-p:Platform=$Platform")
    if ($WarnAsError) {
        $arguments += '-warnaserror'
    }

    Invoke-NativeCommand -FilePath 'dotnet' -Arguments $arguments
}

function Invoke-TrackMeUpTest {
    $arguments = @('test', (Join-Path $script:RepositoryRoot 'TrackMeUp.slnx'), "-p:Platform=$Platform")
    if ($WarnAsError) {
        $arguments += '-warnaserror'
    }

    Invoke-NativeCommand -FilePath 'dotnet' -Arguments $arguments
}

function Invoke-TrackMeUpBuildReports {
    Invoke-RepositoryScript -RelativePath 'scripts\build-reports-web.ps1'
}

function Invoke-TrackMeUpTestCli {
    $arguments = @()
    if (-not [string]::IsNullOrWhiteSpace($ExecutablePath)) {
        $arguments += @('-ExecutablePath', $ExecutablePath)
    }

    $arguments += $PassThruArguments
    Invoke-RepositoryScript -RelativePath 'scripts\test-cli.ps1' -Arguments $arguments
}

function Invoke-TrackMeUpStoreListingValidation {
    $arguments = @()
    if (-not [string]::IsNullOrWhiteSpace($ListingPath)) {
        $arguments += @('-Path', $ListingPath)
    }

    $arguments += $PassThruArguments
    Invoke-RepositoryScript -RelativePath 'scripts\Test-StoreListing.ps1' -Arguments $arguments
}

function Invoke-TrackMeUpAssetGeneration {
    $python = Get-Command -Name 'python' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $python) {
        $python = Get-Command -Name 'py' -ErrorAction SilentlyContinue | Select-Object -First 1
    }

    if (-not $python) {
        throw 'Python was not found on PATH.'
    }

    $assetScript = Join-Path $script:RepositoryRoot 'scripts\generate_trackmeup_assets.py'
    Invoke-NativeCommand -FilePath $python.Source -Arguments @($assetScript)
}

function Invoke-TrackMeUpTaskbarProbe {
    Invoke-RepositoryScript -RelativePath 'scripts\probe-taskbar.ps1'
}

function Invoke-TrackMeUpUnpackagedPublish {
    $runtimeIdentifier = Get-TrackMeUpRuntimeIdentifier -TargetPlatform $Platform
    $arguments = @(
        'publish',
        (Join-Path $script:RepositoryRoot 'TrackMeUp\TrackMeUp.csproj'),
        '-c',
        'Release-Unpackaged',
        "-p:Platform=$Platform",
        '-r',
        $runtimeIdentifier,
        '--self-contained',
        'true'
    )

    if ($SkipRestore) {
        $arguments += '--no-restore'
    }

    Invoke-NativeCommand -FilePath 'dotnet' -Arguments $arguments
}

function Invoke-TrackMeUpMsixPackage {
    $runtimeIdentifier = Get-TrackMeUpRuntimeIdentifier -TargetPlatform $Platform
    $arguments = @(
        'msbuild',
        (Join-Path $script:RepositoryRoot 'TrackMeUp\TrackMeUp.csproj'),
        '/t:Restore,Publish',
        '/p:Configuration=Release',
        "/p:Platform=$Platform",
        "/p:RuntimeIdentifier=$runtimeIdentifier",
        '/p:GenerateAppxPackageOnBuild=true',
        '/p:UapAppxPackageBuildMode=SideloadOnly',
        '/p:AppxBundle=Never'
    )

    Invoke-NativeCommand -FilePath 'dotnet' -Arguments $arguments
}

function Invoke-TrackMeUpBuildInfo {
    if (@($PassThruArguments).Count -gt 0) {
        Invoke-RepositoryScript -RelativePath 'scripts\New-BuildInfo.ps1' -Arguments $PassThruArguments
        return
    }

    $runtimeIdentifier = Get-TrackMeUpRuntimeIdentifier -TargetPlatform $Platform
    Invoke-RepositoryScript -RelativePath 'scripts\New-BuildInfo.ps1' -Arguments @(
        '-VersionStatePath',
        (Join-Path $script:RepositoryRoot 'TrackMeUp\build-version.json'),
        '-OutputPath',
        (Join-Path $script:RepositoryRoot 'TrackMeUp\BuildInfo.json'),
        '-PackageManifestPath',
        (Join-Path $script:RepositoryRoot 'TrackMeUp\Package.appxmanifest'),
        '-Configuration',
        $Configuration,
        '-Platform',
        $Platform,
        '-RuntimeIdentifier',
        $runtimeIdentifier
    )
}

function Invoke-TrackMeUpSecretTool {
    $arguments = @('-Action', $SecretToolAction)
    if (-not [string]::IsNullOrWhiteSpace($SecretPath)) {
        $arguments += @('-SecretPath', $SecretPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($SecretName)) {
        $arguments += @('-SecretName', $SecretName)
    }

    if (-not [string]::IsNullOrWhiteSpace($InputValue)) {
        $arguments += @('-InputValue', $InputValue)
    }

    if ($Force) {
        $arguments += '-Force'
    }

    if ($SkipPause) {
        $arguments += '-SkipPause'
    }

    $arguments += $PassThruArguments
    Invoke-RepositoryScript -RelativePath 'scripts\Protect-SecretValue.ps1' -Arguments $arguments
}

function Invoke-TrackMeUpYubiKeySecretTool {
    $arguments = @('-Action', $SecretToolAction, '-Slot', $Slot)
    if (-not [string]::IsNullOrWhiteSpace($SecretPath)) {
        $arguments += @('-SecretPath', $SecretPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($CredentialXmlPath)) {
        $arguments += @('-CredentialXmlPath', $CredentialXmlPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($SecretName)) {
        $arguments += @('-SecretName', $SecretName)
    }

    if (-not [string]::IsNullOrWhiteSpace($InputValue)) {
        $arguments += @('-InputValue', $InputValue)
    }

    if ($Force) {
        $arguments += '-Force'
    }

    if ($SkipPause) {
        $arguments += '-SkipPause'
    }

    $arguments += $PassThruArguments
    Invoke-RepositoryScript -RelativePath 'scripts\Protect-SecretWithYubiKey.ps1' -Arguments $arguments
}

function Invoke-TrackMeUpAction {
    param([Parameter(Mandatory)][string]$RequestedAction)

    switch ($RequestedAction) {
        'Preflight' { Invoke-TrackMeUpPreflight }
        'Restore' { Invoke-TrackMeUpRestore }
        'Build' { Invoke-TrackMeUpBuild }
        'Test' { Invoke-TrackMeUpTest }
        'BuildReports' { Invoke-TrackMeUpBuildReports }
        'TestCli' { Invoke-TrackMeUpTestCli }
        'ValidateStoreListing' { Invoke-TrackMeUpStoreListingValidation }
        'GenerateAssets' { Invoke-TrackMeUpAssetGeneration }
        'ProbeTaskbar' { Invoke-TrackMeUpTaskbarProbe }
        'PublishUnpackaged' { Invoke-TrackMeUpUnpackagedPublish }
        'PackageMsix' { Invoke-TrackMeUpMsixPackage }
        'ProtectSecret' { Invoke-TrackMeUpSecretTool }
        'ProtectSecretYubiKey' { Invoke-TrackMeUpYubiKeySecretTool }
        'BuildInfo' { Invoke-TrackMeUpBuildInfo }
        default { throw "Unsupported TrackMeUp action: $RequestedAction" }
    }
}

function Invoke-TrackMeUpActionForAgent {
    param([Parameter(Mandatory)][string]$RequestedAction)

    $started = Get-Date
    if (-not $AsJson) {
        Invoke-TrackMeUpAction -RequestedAction $RequestedAction
        return
    }

    try {
        $output = @(Invoke-TrackMeUpAction -RequestedAction $RequestedAction *>&1 | ForEach-Object { "$_" })
        [pscustomobject]@{
            action = $RequestedAction
            succeeded = $true
            startedAt = $started.ToString('O')
            endedAt = (Get-Date).ToString('O')
            output = $output
        } | ConvertTo-Json -Depth 4
    }
    catch {
        [pscustomobject]@{
            action = $RequestedAction
            succeeded = $false
            startedAt = $started.ToString('O')
            endedAt = (Get-Date).ToString('O')
            error = $_.Exception.Message
        } | ConvertTo-Json -Depth 4
        exit 1
    }
}

function Show-TrackMeUpMenu {
    while ($true) {
        Initialize-SharedBootstrap -ClearScreen
        Show-Banner -Title 'TrackMeUp Control Center' -Subtitle 'Build, validate, package, and repository helpers'
        Invoke-TrackMeUpPreflight
        Write-Section -Title 'Actions'
        Write-MenuItem -Key '1' -Label 'Restore solution'
        Write-MenuItem -Key '2' -Label 'Build solution (x64)'
        Write-MenuItem -Key '3' -Label 'Test solution (x64)'
        Write-MenuItem -Key '4' -Label 'Build reports web assets'
        Write-MenuItem -Key '5' -Label 'Run CLI smoke test'
        Write-MenuItem -Key '6' -Label 'Validate Store listing'
        Write-MenuItem -Key '7' -Label 'Generate Store/package assets'
        Write-MenuItem -Key '8' -Label 'Publish unpackaged x64 build'
        Write-MenuItem -Key '9' -Label 'Package x64 MSIX sideload installer'
        Write-MenuItem -Key '10' -Label 'Probe taskbar widget'
        Write-MenuItem -Key '11' -Label 'DPAPI secret helper'
        Write-MenuItem -Key '12' -Label 'YubiKey secret helper'
        Write-MenuItem -Key '0' -Label 'Exit'

        $choice = Read-MenuChoice -Prompt 'Select' -AllowedChoices @('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12')
        if ($choice -eq '0') {
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
            return
        }

        $selectedAction = switch ($choice) {
            '1' { 'Restore' }
            '2' { 'Build' }
            '3' { 'Test' }
            '4' { 'BuildReports' }
            '5' { 'TestCli' }
            '6' { 'ValidateStoreListing' }
            '7' { 'GenerateAssets' }
            '8' { 'PublishUnpackaged' }
            '9' { 'PackageMsix' }
            '10' { 'ProbeTaskbar' }
            '11' { 'ProtectSecret' }
            '12' { 'ProtectSecretYubiKey' }
        }

        try {
            Write-Section -Title $selectedAction
            Invoke-TrackMeUpAction -RequestedAction $selectedAction
            Write-Host ''
            Write-Host "Action completed: $selectedAction" -ForegroundColor Green
        }
        catch {
            Show-FooterError -ScriptName $script:ScriptName -ErrorMessage $_.Exception.Message -StartTime $script:StartedAt -EndTime (Get-Date)
        }

        Wait-ForEnter
    }
}

if ($Help -or (@($PassThruArguments) -contains '--help') -or (@($PassThruArguments) -contains '-h') -or (@($PassThruArguments) -contains '/?')) {
    Get-Help $PSCommandPath -Detailed
    exit 0
}

. Import-TrackMeUpScriptModules

if ($script:InteractiveMode) {
    Show-TrackMeUpMenu
    exit 0
}

if ($Action -eq 'Menu') {
    Show-TrackMeUpMenu
    exit 0
}

Invoke-TrackMeUpActionForAgent -RequestedAction $Action
