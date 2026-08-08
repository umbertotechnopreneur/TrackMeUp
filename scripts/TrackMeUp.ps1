#requires -Version 7.0
<#
.SYNOPSIS
Unified TrackMeUp repository utility entrypoint.

.DESCRIPTION
Runs repository build, test, packaging, installer, validation, asset, and helper
tasks from one agent-friendly CLI. With no arguments, opens an interactive
control center using the repository preflight, banner, footer, and menu flow.

.EXAMPLE
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1

.EXAMPLE
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Test -Platform x64

.EXAMPLE
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action CreateInstaller -Platform x64
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
        'CreateInstaller',
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
    [switch]$SkipPackageBuild,
    [switch]$Help,
    [string]$ExecutablePath = 'trackmeup.exe',
    [string]$ListingPath,
    [ValidateSet('Msix', 'Zip')]
    [string]$InstallerFormat = 'Msix',
    [string]$InstallerOutputPath,
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
    [string]$VersionStatePath,
    [string]$OutputPath,
    [string]$PackageManifestPath,
    [string]$RuntimeIdentifier,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$PassThruArguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:StartedAt = Get-Date
$script:ScriptName = Split-Path -Leaf $PSCommandPath
$script:RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:InteractiveMode = $Action -eq 'Menu' -and $MyInvocation.BoundParameters.Count -eq 0 -and @($PassThruArguments).Count -eq 0

function Test-RunningInIdeTerminal {
    if ($env:WT_SESSION -or $env:WT_PROFILE_ID) {
        return $false
    }

    if ($env:TERM_PROGRAM -eq 'vscode' -or $env:VSCODE_PID -or $env:VSCODE_INJECTION) {
        return $true
    }

    return $false
}

function Clear-TerminalFull {
    [CmdletBinding()]
    param()

    try {
        if (-not [Console]::IsOutputRedirected) {
            Write-Host "`e[3J`e[H`e[2J" -NoNewline
        }
        else {
            try {
                Clear-Host
            }
            catch {
            }
        }
    }
    catch {
        try {
            Clear-Host
        }
        catch {
        }
    }
}

function Initialize-SharedBootstrap {
    [CmdletBinding()]
    param([switch]$ClearScreen)

    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8

    if ($ClearScreen) {
        Clear-TerminalFull
    }
}

function Get-BannerLine {
    param(
        [string]$Text,
        [int]$Width = 76
    )

    $trimmed = $Text.Trim()
    if ($trimmed.Length -gt ($Width - 4)) {
        $trimmed = $trimmed.Substring(0, $Width - 4)
    }

    return ('| ' + $trimmed.PadRight($Width - 4) + ' |')
}

function Show-Banner {
    [CmdletBinding()]
    param(
        [string]$Title = 'Shared PowerShell Utility',
        [string]$Subtitle = ''
    )

    $width = 76
    $border = '+' + ('-' * ($width - 2)) + '+'

    Write-Host $border -ForegroundColor DarkCyan
    Write-Host (Get-BannerLine -Text $Title -Width $width) -ForegroundColor Cyan
    if (-not [string]::IsNullOrWhiteSpace($Subtitle)) {
        Write-Host (Get-BannerLine -Text $Subtitle -Width $width) -ForegroundColor Gray
    }

    Write-Host $border -ForegroundColor DarkCyan
    Write-Host ''
}

function Write-Section {
    param([Parameter(Mandatory)][string]$Title)

    Write-Host $Title -ForegroundColor Yellow
    Write-Host (('-' * [Math]::Max($Title.Length, 12))) -ForegroundColor DarkGray
}

function Show-Footer {
    [CmdletBinding()]
    param(
        [string]$ScriptName = 'Script',
        [string]$Status = 'COMPLETED',
        [datetime]$StartTime = (Get-Date),
        [datetime]$EndTime = (Get-Date)
    )

    $elapsed = $EndTime - $StartTime
    $line = ('=' * 76)
    Write-Host ''
    Write-Host $line -ForegroundColor DarkGray
    Write-Host ("[{0}] {1}" -f $Status, $ScriptName) -ForegroundColor Green
    Write-Host ("Started: {0}" -f $StartTime.ToString('yyyy-MM-dd HH:mm:ss')) -ForegroundColor Gray
    Write-Host ("Ended:   {0}" -f $EndTime.ToString('yyyy-MM-dd HH:mm:ss')) -ForegroundColor Gray
    Write-Host ("Elapsed: {0:mm\:ss}" -f $elapsed) -ForegroundColor Gray
    Write-Host $line -ForegroundColor DarkGray
}

function Show-FooterError {
    [CmdletBinding()]
    param(
        [string]$ScriptName = 'Script',
        [string]$ErrorMessage = 'Unknown error',
        [datetime]$StartTime = (Get-Date),
        [datetime]$EndTime = (Get-Date)
    )

    $elapsed = $EndTime - $StartTime
    $line = ('=' * 76)
    Write-Host ''
    Write-Host $line -ForegroundColor DarkRed
    Write-Host ("[FAILED] {0}" -f $ScriptName) -ForegroundColor Red
    Write-Host $ErrorMessage -ForegroundColor Yellow
    Write-Host ("Started: {0}" -f $StartTime.ToString('yyyy-MM-dd HH:mm:ss')) -ForegroundColor Gray
    Write-Host ("Ended:   {0}" -f $EndTime.ToString('yyyy-MM-dd HH:mm:ss')) -ForegroundColor Gray
    Write-Host ("Elapsed: {0:mm\:ss}" -f $elapsed) -ForegroundColor Gray
    Write-Host $line -ForegroundColor DarkRed
}

function Read-MenuChoice {
    param(
        [string]$Prompt = 'Select',
        [string[]]$AllowedChoices = @()
    )

    while ($true) {
        $choice = Read-Host $Prompt
        if ($AllowedChoices.Count -eq 0 -or $choice -in $AllowedChoices) {
            return $choice
        }

        Write-Host ("Valid choices: {0}" -f ($AllowedChoices -join ', ')) -ForegroundColor Yellow
    }
}

function Wait-ForEnter {
    param([string]$Prompt = 'Press Enter to continue')

    [void](Read-Host $Prompt)
}

function Write-MenuItem {
    param(
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][string]$Label
    )

    Write-Host ("[{0}] {1}" -f $Key, $Label) -ForegroundColor White
}

function Invoke-CommonPreflight {
    [CmdletBinding()]
    param(
        [string]$Title = 'Preflight',
        [string[]]$RequiredCommands = @(),
        [string[]]$RequiredFiles = @(),
        [switch]$RequireClipboard,
        [scriptblock]$ValidationScript
    )

    $checks = [System.Collections.Generic.List[object]]::new()
    $checks.Add([pscustomobject]@{
        Check = 'PowerShell'
        Status = if ($PSVersionTable.PSVersion.Major -ge 7) { 'OK' } else { 'FAIL' }
        Details = ("Detected {0}" -f $PSVersionTable.PSVersion)
    })

    foreach ($commandName in $RequiredCommands) {
        $command = Get-Command -Name $commandName -ErrorAction SilentlyContinue | Select-Object -First 1
        $checks.Add([pscustomobject]@{
            Check = "Command: $commandName"
            Status = if ($command) { 'OK' } else { 'FAIL' }
            Details = if ($command) { $command.Source } else { 'Not found' }
        })
    }

    foreach ($filePath in $RequiredFiles) {
        $exists = Test-Path -LiteralPath $filePath -PathType Leaf
        $checks.Add([pscustomobject]@{
            Check = "File: $(Split-Path -Leaf $filePath)"
            Status = if ($exists) { 'OK' } else { 'FAIL' }
            Details = $filePath
        })
    }

    if ($RequireClipboard) {
        $clipboardCommand = Get-Command -Name Set-Clipboard -ErrorAction SilentlyContinue | Select-Object -First 1
        $checks.Add([pscustomobject]@{
            Check = 'Clipboard'
            Status = if ($clipboardCommand) { 'OK' } else { 'FAIL' }
            Details = if ($clipboardCommand) { $clipboardCommand.Source } else { 'Set-Clipboard not available' }
        })
    }

    if ($ValidationScript) {
        try {
            & $ValidationScript
            $checks.Add([pscustomobject]@{
                Check = 'Credential probe'
                Status = 'OK'
                Details = 'Credential decrypted successfully for the current Windows user.'
            })
        }
        catch {
            $checks.Add([pscustomobject]@{
                Check = 'Credential probe'
                Status = 'FAIL'
                Details = $_.Exception.Message
            })
        }
    }

    Write-Section -Title $Title
    $checks | Format-Table -AutoSize | Out-String | Write-Host

    return -not ($checks.Status -contains 'FAIL')
}

function Resolve-TrackMeUpPath {
    param(
        [Parameter(Mandatory)][string]$Path,
        [string]$BasePath = $script:RepositoryRoot
    )

    $expanded = [Environment]::ExpandEnvironmentVariables($Path.Trim())
    if ([System.IO.Path]::IsPathRooted($expanded)) {
        return [System.IO.Path]::GetFullPath($expanded)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $expanded))
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

function Write-Utf8Json {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][object]$Value
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    $json = $Value | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
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
    $webRoot = Join-Path $script:RepositoryRoot 'TrackMeUp.Reports.Web'
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
        Invoke-NativeCommand -FilePath 'npm' -Arguments @('ci')
        Invoke-NativeCommand -FilePath 'npm' -Arguments @('run', 'build')
    }
    finally {
        Pop-Location
    }

    if (-not (Test-Path -LiteralPath $outputIndex -PathType Leaf)) {
        throw "Reports web build completed without producing: $outputIndex"
    }

    Write-Host "Reports web assets ready at: $(Split-Path -Parent $outputIndex)"
}

function Invoke-TrackMeUpTestCli {
    if ($PSVersionTable.PSVersion.Major -lt 7) {
        throw 'TrackMeUp CLI is supported only in PowerShell 7 or later.'
    }

    [Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
    [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $roundTrip = 'Italiano è · Tiếng Việt · Français · Deutsch · Español · ╭─╮ · 🚀'
    if ([System.Text.Encoding]::UTF8.GetString([System.Text.Encoding]::UTF8.GetBytes($roundTrip)) -ne $roundTrip) {
        throw 'UTF-8 round-trip verification failed.'
    }

    function Invoke-TrackMeUpCli {
        param([string[]]$Arguments)

        $output = & $ExecutablePath -cli @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "trackmeup -cli $($Arguments -join ' ') failed with exit code $LASTEXITCODE.`n$output"
        }

        return $output
    }

    $generalHelp = Invoke-TrackMeUpCli -Arguments @('/help', '--format', 'plain')
    if (($generalHelp -join "`n") -notmatch '/config' -or ($generalHelp -join "`n") -notmatch '/doctor') {
        throw 'General slash-command help did not include the expected command families.'
    }

    $configHelp = Invoke-TrackMeUpCli -Arguments @('/help', '/config', '--format', 'plain')
    if (($configHelp -join "`n") -notmatch 'screenshots.enabled' -or ($configHelp -join "`n") -match 'InstallationId|PrivacyProcessNames|ApiKey') {
        throw 'Config help is missing public keys or exposes an internal/secret field.'
    }

    Invoke-TrackMeUpCli -Arguments @('/status', '--help', '--format', 'plain') | Out-Null
    Invoke-TrackMeUpCli -Arguments @('--version', '--format', 'plain') | Out-Null

    foreach ($command in @(@('/status', '--json'), @('/runtime', 'health', '--json'), @('/doctor', '--json'), @('/config', 'list', '--json'), @('/config', 'get', 'theme', '--json'))) {
        $output = Invoke-TrackMeUpCli -Arguments $command
        $document = $output | ConvertFrom-Json
        if ($null -eq $document.code) {
            throw "CLI JSON output did not include a stable result code for '$($command -join ' ')'."
        }

        if (($output -join "`n") -match "`e\[") {
            throw "CLI JSON output contains an ANSI sequence for '$($command -join ' ')'."
        }

        if (($command -contains '/config') -and ($output -join "`n") -match 'installationId|privacyProcessNames|apiKey') {
            throw "CLI settings output exposed an internal or secret field for '$($command -join ' ')'."
        }
    }

    $visibleWindows = Get-Process -Name 'TrackMeUp' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
    if ($visibleWindows) {
        throw 'CLI smoke test observed a visible TrackMeUp window.'
    }

    Write-Host 'TrackMeUp CLI smoke test completed.'
}

function Assert-Condition {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Text {
    param(
        [Parameter(Mandatory)][string]$Value,
        [Parameter(Mandatory)][string]$Name,
        [int]$MaximumLength = 2000
    )

    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($Value)) -Message "$Name must not be empty."
    Assert-Condition -Condition ($Value.Length -le $MaximumLength) -Message "$Name is longer than $MaximumLength characters."
}

function Assert-Url {
    param(
        [Parameter(Mandatory)][string]$Value,
        [Parameter(Mandatory)][string]$Name
    )

    $uri = $null
    $isValid = [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri)
    Assert-Condition -Condition ($isValid -and $uri.Scheme -in @('http', 'https')) -Message "$Name must be an absolute HTTP or HTTPS URL."
}

function Invoke-TrackMeUpStoreListingValidation {
    $pathToValidate = if ([string]::IsNullOrWhiteSpace($ListingPath)) { Join-Path $script:RepositoryRoot 'store\listing.json' } else { Resolve-TrackMeUpPath -Path $ListingPath }
    $resolvedPath = (Resolve-Path -LiteralPath $pathToValidate).Path
    $repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $resolvedPath) '..'))
    $raw = Get-Content -LiteralPath $resolvedPath -Raw
    $listing = $raw | ConvertFrom-Json

    Assert-Condition -Condition ($listing.schemaVersion -eq 1) -Message 'Unsupported Store listing schemaVersion.'
    Assert-Text -Value $listing.product.name -Name 'product.name' -MaximumLength 80
    Assert-Text -Value $listing.product.publisher -Name 'product.publisher' -MaximumLength 120
    Assert-Text -Value $listing.product.category -Name 'product.category' -MaximumLength 120

    foreach ($property in @('sourceCodeUrl', 'publisherUrl', 'privacyPolicyUrl', 'supportUrl')) {
        Assert-Url -Value $listing.product.$property -Name "product.$property"
    }

    $locales = @($listing.locales.PSObject.Properties)
    Assert-Condition -Condition ($locales.Count -gt 0) -Message 'At least one Store locale is required.'

    foreach ($locale in $locales) {
        $copy = $locale.Value
        Assert-Text -Value $copy.displayName -Name "locales.$($locale.Name).displayName" -MaximumLength 80
        Assert-Text -Value $copy.subtitle -Name "locales.$($locale.Name).subtitle" -MaximumLength 200
        Assert-Text -Value $copy.shortDescription -Name "locales.$($locale.Name).shortDescription" -MaximumLength 1000
        Assert-Text -Value $copy.description -Name "locales.$($locale.Name).description" -MaximumLength 10000

        $features = @($copy.features)
        Assert-Condition -Condition ($features.Count -gt 0) -Message "locales.$($locale.Name).features must contain at least one feature."
        foreach ($feature in $features) {
            Assert-Text -Value $feature -Name "locales.$($locale.Name).feature" -MaximumLength 300
        }
    }

    $screenshotDirectory = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $listing.screenshots.directory.Replace('/', '\')))
    $screenshotRoot = $screenshotDirectory.TrimEnd('\') + '\'
    Assert-Condition -Condition (Test-Path -LiteralPath $screenshotDirectory -PathType Container) -Message "Screenshot directory not found: $screenshotDirectory"

    foreach ($item in @($listing.screenshots.items)) {
        Assert-Text -Value $item.path -Name 'screenshots.items.path' -MaximumLength 260
        Assert-Text -Value $item.locale -Name 'screenshots.items.locale' -MaximumLength 20
        Assert-Text -Value $item.caption -Name 'screenshots.items.caption' -MaximumLength 300
        Assert-Text -Value $item.purpose -Name 'screenshots.items.purpose' -MaximumLength 300

        $relativePath = $item.path.Replace('/', '\')
        $candidate = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $relativePath))
        Assert-Condition -Condition ($candidate.StartsWith($screenshotRoot, [StringComparison]::OrdinalIgnoreCase)) -Message "Screenshot path escapes the screenshot directory: $($item.path)"
        Assert-Condition -Condition (Test-Path -LiteralPath $candidate -PathType Leaf) -Message "Screenshot file not found: $candidate"
    }

    if (-not [string]::IsNullOrWhiteSpace($listing.publishing.partnerCenterMetadataPath)) {
        $metadataPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $listing.publishing.partnerCenterMetadataPath.Replace('/', '\')))
        $repositoryRootPrefix = $repositoryRoot.TrimEnd('\') + '\'
        Assert-Condition -Condition ($metadataPath.StartsWith($repositoryRootPrefix, [StringComparison]::OrdinalIgnoreCase)) -Message 'Partner Center metadata path must stay inside the repository.'
    }

    $secretPattern = '(?i)(sk-[A-Za-z0-9]|clientSecret|accessToken|api[_-]?key\s*[:=]\s*["''][^"'']+)'
    Assert-Condition -Condition ($raw -notmatch $secretPattern) -Message 'Store listing appears to contain a credential or access token.'

    Write-Host "Store listing validation passed: $resolvedPath"
    Write-Host "Locales: $($locales.Name -join ', ')"
    Write-Host "Screenshots declared: $(@($listing.screenshots.items).Count)"
}

function Invoke-TrackMeUpAssetGeneration {
    $python = Get-Command -Name 'python' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $python) {
        $python = Get-Command -Name 'py' -ErrorAction SilentlyContinue | Select-Object -First 1
    }

    if (-not $python) {
        throw 'Python was not found on PATH.'
    }

    $assetGenerator = @'
"""Generate Microsoft Store-ready TrackMeUp visual assets from the approved artwork."""

import os
from pathlib import Path

from PIL import Image


ROOT = Path(os.environ["TRACKMEUP_REPOSITORY_ROOT"])
ASSETS = ROOT / "TrackMeUp" / "Assets"
REFERENCE = ROOT / "design" / "branding" / "trackmeup-icon-reference.png"
SCALES = (100, 125, 150, 200, 400)
TARGET_SIZES = (16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256)
REFERENCE_SIZE = (1536, 1024)
MASTER_BOX = (165, 225, 735, 795)
COMPACT_BOX = (1075, 395, 1305, 625)


def _scaled_box(source, box):
    width_ratio = source.width / REFERENCE_SIZE[0]
    height_ratio = source.height / REFERENCE_SIZE[1]
    return tuple(round(value * ratio) for value, ratio in zip(box, (width_ratio, height_ratio, width_ratio, height_ratio)))


def _extract_icon(source, box):
    icon = source.crop(_scaled_box(source, box)).convert("RGBA")
    pixels = icon.load()
    for y in range(icon.height):
        for x in range(icon.width):
            red, green, blue, alpha = pixels[x, y]
            if min(red, green, blue) >= 236:
                pixels[x, y] = (red, green, blue, 0)

    bounds = icon.getbbox()
    return icon.crop(bounds) if bounds else icon


def _pixel_size(base_size, scale):
    return (base_size * scale + 50) // 100


def _fit(icon, size, padding=0.06):
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    available_width = max(1, round(size[0] * (1 - padding * 2)))
    available_height = max(1, round(size[1] * (1 - padding * 2)))
    fitted = icon.copy()
    fitted.thumbnail((available_width, available_height), Image.Resampling.LANCZOS)
    offset = ((size[0] - fitted.width) // 2, (size[1] - fitted.height) // 2)
    canvas.alpha_composite(fitted, offset)
    return canvas


def _themed_canvas(size, icon, theme):
    backgrounds = {"default": "#112235", "dark": "#314157", "light": "#F8F4ED"}
    canvas = Image.new("RGBA", size, backgrounds[theme])
    fitted = _fit(icon, size, 0.14)
    canvas.alpha_composite(fitted)
    return canvas


def _save(image, path):
    image.save(path, format="PNG")


def _clear_previous_assets():
    for path in ASSETS.glob("TrackMeUp*.png"):
        path.unlink()
    icon_file = ASSETS / "TrackMeUpIcon.ico"
    if icon_file.exists():
        icon_file.unlink()
    for name in (
        "LockScreenLogo.scale-200.png",
        "SplashScreen.scale-200.png",
        "Square150x150Logo.scale-200.png",
        "Square44x44Logo.scale-200.png",
        "Square44x44Logo.targetsize-24_altform-unplated.png",
        "StoreLogo.png",
        "Wide310x150Logo.scale-200.png",
    ):
        path = ASSETS / name
        if path.exists():
            path.unlink()


def _write_square_assets(master, compact):
    _save(_fit(compact, (44, 44)), ASSETS / "TrackMeUpSquare44Logo.png")
    _save(_fit(master, (150, 150)), ASSETS / "TrackMeUpSquare150Logo.png")
    _save(_fit(master, (50, 50)), ASSETS / "TrackMeUpStoreLogo.png")

    for scale in SCALES:
        _save(_fit(compact, (_pixel_size(44, scale),) * 2), ASSETS / f"TrackMeUpSquare44Logo.scale-{scale}.png")
        _save(_fit(compact, (_pixel_size(44, scale),) * 2), ASSETS / f"TrackMeUpSquare44Logo.scale-{scale}_altform-colorful_theme-light.png")
        _save(_fit(compact, (_pixel_size(44, scale),) * 2), ASSETS / f"TrackMeUpSquare44Logo.scale-{scale}_altform-colorful_theme-dark.png")
        _save(_fit(master, (_pixel_size(150, scale),) * 2), ASSETS / f"TrackMeUpSquare150Logo.scale-{scale}.png")
        _save(_fit(master, (_pixel_size(150, scale),) * 2), ASSETS / f"TrackMeUpSquare150Logo.scale-{scale}_altform-colorful_theme-light.png")
        _save(_fit(master, (_pixel_size(150, scale),) * 2), ASSETS / f"TrackMeUpSquare150Logo.scale-{scale}_altform-colorful_theme-dark.png")
        _save(_fit(master, (_pixel_size(50, scale),) * 2), ASSETS / f"TrackMeUpStoreLogo.scale-{scale}.png")
        _save(_fit(master, (_pixel_size(50, scale),) * 2), ASSETS / f"TrackMeUpStoreLogo.scale-{scale}_altform-colorful_theme-light.png")
        _save(_fit(master, (_pixel_size(50, scale),) * 2), ASSETS / f"TrackMeUpStoreLogo.scale-{scale}_altform-colorful_theme-dark.png")

    for size in TARGET_SIZES:
        source = master if size == 256 else compact
        icon = _fit(source, (size, size), 0.04)
        _save(icon, ASSETS / f"TrackMeUpSquare44Logo.targetsize-{size}.png")
        _save(icon, ASSETS / f"TrackMeUpSquare44Logo.targetsize-{size}_altform-unplated.png")
        _save(icon, ASSETS / f"TrackMeUpSquare44Logo.targetsize-{size}_altform-lightunplated.png")


def _write_wide_and_splash_assets(master):
    _save(_themed_canvas((310, 150), master, "default"), ASSETS / "TrackMeUpWide310x150Logo.png")
    _save(_themed_canvas((620, 300), master, "default"), ASSETS / "TrackMeUpSplashScreen.png")

    for scale in SCALES:
        wide_size = (_pixel_size(310, scale), _pixel_size(150, scale))
        splash_size = (_pixel_size(620, scale), _pixel_size(300, scale))
        _save(_themed_canvas(wide_size, master, "default"), ASSETS / f"TrackMeUpWide310x150Logo.scale-{scale}.png")
        _save(_themed_canvas(wide_size, master, "light"), ASSETS / f"TrackMeUpWide310x150Logo.scale-{scale}_altform-colorful_theme-light.png")
        _save(_themed_canvas(wide_size, master, "dark"), ASSETS / f"TrackMeUpWide310x150Logo.scale-{scale}_altform-colorful_theme-dark.png")
        _save(_themed_canvas(splash_size, master, "default"), ASSETS / f"TrackMeUpSplashScreen.scale-{scale}.png")
        _save(_themed_canvas(splash_size, master, "light"), ASSETS / f"TrackMeUpSplashScreen.scale-{scale}_altform-colorful_theme-light.png")
        _save(_themed_canvas(splash_size, master, "dark"), ASSETS / f"TrackMeUpSplashScreen.scale-{scale}_altform-colorful_theme-dark.png")


def _write_template_compatibility_assets(master, compact):
    _save(_fit(compact, (88, 88)), ASSETS / "Square44x44Logo.scale-200.png")
    _save(_fit(compact, (24, 24)), ASSETS / "Square44x44Logo.targetsize-24_altform-unplated.png")
    _save(_fit(master, (300, 300)), ASSETS / "Square150x150Logo.scale-200.png")
    _save(_fit(master, (50, 50)), ASSETS / "StoreLogo.png")
    _save(_themed_canvas((620, 300), master, "default"), ASSETS / "Wide310x150Logo.scale-200.png")
    _save(_themed_canvas((1240, 600), master, "default"), ASSETS / "SplashScreen.scale-200.png")
    _save(_fit(compact, (48, 48)), ASSETS / "LockScreenLogo.scale-200.png")


def _write_ico(master, compact):
    frames = [_fit(master if size == 256 else compact, (size, size), 0.04) for size in TARGET_SIZES]
    frames[-1].save(ASSETS / "TrackMeUpIcon.ico", format="ICO", sizes=[(size, size) for size in TARGET_SIZES], append_images=frames[:-1])


def main():
    if not REFERENCE.is_file():
        raise FileNotFoundError(f"Approved TrackMeUp artwork is missing: {REFERENCE}")

    ASSETS.mkdir(parents=True, exist_ok=True)
    source = Image.open(REFERENCE)
    master = _extract_icon(source, MASTER_BOX)
    compact = _extract_icon(source, COMPACT_BOX)
    _clear_previous_assets()
    _write_square_assets(master, compact)
    _write_wide_and_splash_assets(master)
    _write_template_compatibility_assets(master, compact)
    _write_ico(master, compact)


if __name__ == "__main__":
    main()
'@

    $previousRoot = $env:TRACKMEUP_REPOSITORY_ROOT
    $env:TRACKMEUP_REPOSITORY_ROOT = $script:RepositoryRoot
    try {
        Invoke-NativeCommand -FilePath $python.Source -Arguments @('-c', $assetGenerator)
    }
    finally {
        if ($null -eq $previousRoot) {
            Remove-Item Env:\TRACKMEUP_REPOSITORY_ROOT -ErrorAction SilentlyContinue
        }
        else {
            $env:TRACKMEUP_REPOSITORY_ROOT = $previousRoot
        }
    }
}

function Invoke-TrackMeUpTaskbarProbe {
    if (-not ('TaskbarProbe' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public class TaskbarProbe {
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
'@
    }

    $script:TaskbarClassBuffer = [System.Text.StringBuilder]::new(256)
    $script:TaskbarTitleBuffer = [System.Text.StringBuilder]::new(256)
    $script:TaskbarProbeResults = [System.Collections.Generic.List[string]]::new()
    $taskbar = [TaskbarProbe]::FindWindow('Shell_TrayWnd', $null)
    Write-Host "Shell_TrayWnd handle: $taskbar"
    if ($taskbar -eq [IntPtr]::Zero) {
        return
    }

    $script:TaskbarProbeCount = 0
    [TaskbarProbe]::EnumChildWindows($taskbar, [TaskbarProbe+EnumWindowsProc]{
        param($hWnd, $lParam)
        $script:TaskbarClassBuffer.Clear() | Out-Null
        $script:TaskbarTitleBuffer.Clear() | Out-Null
        [void][TaskbarProbe]::GetClassName($hWnd, $script:TaskbarClassBuffer, 256)
        [void][TaskbarProbe]::GetWindowText($hWnd, $script:TaskbarTitleBuffer, 256)
        $className = $script:TaskbarClassBuffer.ToString()
        $title = $script:TaskbarTitleBuffer.ToString()
        $visible = [TaskbarProbe]::IsWindowVisible($hWnd)
        $rect = New-Object TaskbarProbe+RECT
        [void][TaskbarProbe]::GetWindowRect($hWnd, [ref]$rect)
        $width = $rect.Right - $rect.Left
        $height = $rect.Bottom - $rect.Top
        $script:TaskbarProbeCount++
        if ($className -like '*HwndWrapper*' -or $title -like '*TrackMeUp*') {
            $script:TaskbarProbeResults.Add("handle=$hWnd cls=$className txt='$title' vis=$visible left=$($rect.Left) top=$($rect.Top) right=$($rect.Right) bottom=$($rect.Bottom) width=$width height=$height")
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null

    $script:TaskbarProbeResults | ForEach-Object { Write-Host $_ }
    Write-Host "Enumerated $($script:TaskbarProbeCount) child windows"
}

function Invoke-TrackMeUpUnpackagedPublish {
    $runtime = Get-TrackMeUpRuntimeIdentifier -TargetPlatform $Platform
    $arguments = @(
        'publish',
        (Join-Path $script:RepositoryRoot 'TrackMeUp\TrackMeUp.csproj'),
        '-c',
        'Release-Unpackaged',
        "-p:Platform=$Platform",
        '-r',
        $runtime,
        '--self-contained',
        'true'
    )

    if ($SkipRestore) {
        $arguments += '--no-restore'
    }

    Invoke-NativeCommand -FilePath 'dotnet' -Arguments $arguments
}

function Invoke-TrackMeUpMsixPackage {
    $runtime = Get-TrackMeUpRuntimeIdentifier -TargetPlatform $Platform
    $arguments = @(
        'msbuild',
        (Join-Path $script:RepositoryRoot 'TrackMeUp\TrackMeUp.csproj'),
        '/t:Restore,Publish',
        '/p:Configuration=Release',
        "/p:Platform=$Platform",
        "/p:RuntimeIdentifier=$runtime",
        '/p:GenerateAppxPackageOnBuild=true',
        '/p:UapAppxPackageBuildMode=SideloadOnly',
        '/p:AppxBundle=Never'
    )

    Invoke-NativeCommand -FilePath 'dotnet' -Arguments $arguments
}

function Get-TrackMeUpLatestPackageFile {
    $roots = @(
        (Join-Path $script:RepositoryRoot 'TrackMeUp\AppPackages'),
        (Join-Path $script:RepositoryRoot 'TrackMeUp\bin')
    )

    $extensions = @('.msix', '.msixbundle', '.appx', '.appxbundle')
    $files = foreach ($root in $roots) {
        if (Test-Path -LiteralPath $root -PathType Container) {
            Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction Stop | Where-Object { $_.Extension -in $extensions }
        }
    }

    $latest = @($files | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1)
    if ($latest.Count -eq 0) {
        throw 'No MSIX/AppX package file was found. Run PackageMsix first or use CreateInstaller without -SkipPackageBuild.'
    }

    return $latest[0]
}

function Resolve-TrackMeUpInstallerOutputPath {
    param([Parameter(Mandatory)][string]$DefaultExtension)

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    if ([string]::IsNullOrWhiteSpace($InstallerOutputPath)) {
        $directory = Join-Path $script:RepositoryRoot 'artifacts\installers'
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
        return Join-Path $directory "TrackMeUp-$Platform-$timestamp$DefaultExtension"
    }

    $candidate = Resolve-TrackMeUpPath -Path $InstallerOutputPath
    $extension = [System.IO.Path]::GetExtension($candidate)
    if ([string]::IsNullOrWhiteSpace($extension)) {
        [System.IO.Directory]::CreateDirectory($candidate) | Out-Null
        return Join-Path $candidate "TrackMeUp-$Platform-$timestamp$DefaultExtension"
    }

    $directory = Split-Path -Parent $candidate
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    return $candidate
}

function Assert-OutputFileCanBeWritten {
    param([Parameter(Mandatory)][string]$Path)

    if ((Test-Path -LiteralPath $Path -PathType Leaf) -and -not $Force) {
        throw "Output file already exists. Re-run with -Force to overwrite: $Path"
    }
}

function Invoke-TrackMeUpInstallerCreation {
    if (-not $SkipPackageBuild) {
        Invoke-TrackMeUpMsixPackage
    }

    $packageFile = Get-TrackMeUpLatestPackageFile
    if ($InstallerFormat -eq 'Zip') {
        $outputPath = Resolve-TrackMeUpInstallerOutputPath -DefaultExtension '.zip'
        Assert-OutputFileCanBeWritten -Path $outputPath
        if ((Test-Path -LiteralPath $outputPath -PathType Leaf) -and $Force) {
            Remove-Item -LiteralPath $outputPath -Force
        }

        $sourceDirectory = Split-Path -Parent $packageFile.FullName
        Compress-Archive -Path (Join-Path $sourceDirectory '*') -DestinationPath $outputPath -Force:$Force
        Write-Host "Installer archive created: $outputPath"
        return
    }

    $outputPath = Resolve-TrackMeUpInstallerOutputPath -DefaultExtension $packageFile.Extension
    Assert-OutputFileCanBeWritten -Path $outputPath
    Copy-Item -LiteralPath $packageFile.FullName -Destination $outputPath -Force:$Force
    Write-Host "Installer file created: $outputPath"
}

function Invoke-TrackMeUpBuildInfo {
    $statePath = if ([string]::IsNullOrWhiteSpace($VersionStatePath)) { Join-Path $script:RepositoryRoot 'TrackMeUp\build-version.json' } else { Resolve-TrackMeUpPath -Path $VersionStatePath }
    $buildInfoPath = if ([string]::IsNullOrWhiteSpace($OutputPath)) { Join-Path $script:RepositoryRoot 'TrackMeUp\BuildInfo.json' } else { Resolve-TrackMeUpPath -Path $OutputPath }
    $manifestPath = if ([string]::IsNullOrWhiteSpace($PackageManifestPath)) { Join-Path $script:RepositoryRoot 'TrackMeUp\Package.appxmanifest' } else { Resolve-TrackMeUpPath -Path $PackageManifestPath }
    $runtime = if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) { Get-TrackMeUpRuntimeIdentifier -TargetPlatform $Platform } else { $RuntimeIdentifier }

    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        throw "Build version state not found: $statePath"
    }

    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Package manifest not found: $manifestPath"
    }

    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
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

    $gitCommit = (& git -C $script:RepositoryRoot rev-parse HEAD 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or $gitCommit -notmatch '^[0-9a-f]{40}$') {
        throw 'Unable to resolve the Git commit for this build.'
    }

    & git -C $script:RepositoryRoot diff --quiet --ignore-submodules -- 2>$null
    $trackedDirty = $LASTEXITCODE -ne 0
    $untracked = & git -C $script:RepositoryRoot ls-files --others --exclude-standard 2>$null
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
        runtimeIdentifier = $runtime
    }

    Write-Utf8Json -Path $statePath -Value ([ordered]@{ semVer = $semVer })
    Write-Utf8Json -Path $buildInfoPath -Value $buildInfo

    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
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
    $writer = [System.Xml.XmlWriter]::Create($manifestPath, $writerSettings)
    try {
        $manifest.Save($writer)
    }
    finally {
        $writer.Dispose()
    }

    Write-Host "TrackMeUp build $semVer ($($buildInfo.gitCommitShort)) generated at $buildInfoPath"
}

function ConvertTo-DpapiSecretPath {
    param([string]$PathText)

    $trimmed = ($PathText ?? '').Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        throw 'A secret file path is required.'
    }

    return [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($trimmed))
}

function Resolve-DpapiSecretPath {
    if (-not [string]::IsNullOrWhiteSpace($SecretPath)) {
        return ConvertTo-DpapiSecretPath -PathText $SecretPath
    }

    while ($true) {
        $entered = Read-Host 'Secret file path'
        try {
            return ConvertTo-DpapiSecretPath -PathText $entered
        }
        catch {
            Write-Host $_.Exception.Message -ForegroundColor Yellow
        }
    }
}

function Resolve-DpapiSecretName {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    if (-not [string]::IsNullOrWhiteSpace($SecretName)) {
        return $SecretName.Trim()
    }

    return [System.IO.Path]::GetFileNameWithoutExtension($ResolvedSecretPath)
}

function Read-SecretSecureString {
    if (-not [string]::IsNullOrWhiteSpace($InputValue)) {
        return (ConvertTo-SecureString -String $InputValue -AsPlainText -Force)
    }

    while ($true) {
        $first = Read-Host 'Enter the secret value' -AsSecureString
        $second = Read-Host 'Confirm the secret value' -AsSecureString
        $firstPlain = ([pscredential]::new('secret', $first)).GetNetworkCredential().Password
        $secondPlain = ([pscredential]::new('secret', $second)).GetNetworkCredential().Password

        try {
            if ([string]::IsNullOrWhiteSpace($firstPlain)) {
                Write-Host 'The secret cannot be empty.' -ForegroundColor Yellow
                continue
            }

            if ($firstPlain -ne $secondPlain) {
                Write-Host 'The two values do not match. Try again.' -ForegroundColor Yellow
                continue
            }

            return $first
        }
        finally {
            $firstPlain = $null
            $secondPlain = $null
        }
    }
}

function Import-DpapiSecretCredential {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    if (-not (Test-Path -LiteralPath $ResolvedSecretPath -PathType Leaf)) {
        throw "Protected file not found: $ResolvedSecretPath"
    }

    $credential = Import-Clixml -LiteralPath $ResolvedSecretPath
    if ($null -eq $credential -or -not ($credential -is [pscredential])) {
        throw 'The protected file is not a supported PSCredential XML payload.'
    }

    return $credential
}

function Get-DpapiSecretPlainValue {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    $credential = Import-DpapiSecretCredential -ResolvedSecretPath $ResolvedSecretPath
    try {
        return $credential.GetNetworkCredential().Password
    }
    finally {
        $credential = $null
    }
}

function Invoke-DpapiSecretPreflight {
    param(
        [string]$ResolvedSecretPath,
        [switch]$RequireExistingSecret,
        [switch]$RequireClipboard
    )

    $requiredFiles = @()
    if ($RequireExistingSecret -and -not [string]::IsNullOrWhiteSpace($ResolvedSecretPath)) {
        $requiredFiles = @($ResolvedSecretPath)
    }

    $ok = Invoke-CommonPreflight `
        -Title 'DPAPI secret preflight' `
        -RequiredCommands @('pwsh') `
        -RequiredFiles $requiredFiles `
        -RequireClipboard:$RequireClipboard

    if (-not $ok) {
        throw 'DPAPI secret preflight failed.'
    }
}

function Protect-DpapiSecretValue {
    param(
        [Parameter(Mandatory)][string]$ResolvedSecretPath,
        [Parameter(Mandatory)][string]$ResolvedSecretName
    )

    if (Test-Path -LiteralPath $ResolvedSecretPath -PathType Leaf) {
        if (-not $Force) {
            $overwriteChoice = Read-Host 'The target file already exists. Overwrite it? [y/N]'
            if ($overwriteChoice -notmatch '^(y|yes|s|si)$') {
                Write-Host 'Nothing changed.' -ForegroundColor DarkYellow
                return
            }
        }
    }

    $parentPath = Split-Path -Path $ResolvedSecretPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($parentPath) -and -not (Test-Path -LiteralPath $parentPath -PathType Container)) {
        New-Item -ItemType Directory -Path $parentPath -Force | Out-Null
    }

    $secureValue = Read-SecretSecureString
    try {
        $credential = [pscredential]::new($ResolvedSecretName, $secureValue)
        $credential | Export-Clixml -LiteralPath $ResolvedSecretPath
        Write-Host "Protected secret written to: $ResolvedSecretPath" -ForegroundColor Green
    }
    finally {
        $secureValue = $null
        $credential = $null
    }
}

function Show-DpapiSecretValue {
    param(
        [Parameter(Mandatory)][string]$ResolvedSecretPath,
        [switch]$CopyAfter
    )

    $plainValue = Get-DpapiSecretPlainValue -ResolvedSecretPath $ResolvedSecretPath
    try {
        Write-Section -Title 'Secret value'
        Write-Host ''
        Write-Host $plainValue -ForegroundColor Yellow
        Write-Host ''
        Write-Host 'Do not paste this into source files, logs, screenshots, or tickets.' -ForegroundColor DarkYellow

        if ($CopyAfter) {
            Set-Clipboard -Value $plainValue
            Write-Host 'The secret has also been copied to the clipboard.' -ForegroundColor Green
        }
    }
    finally {
        $plainValue = $null
    }
}

function Copy-DpapiSecretValue {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    $plainValue = Get-DpapiSecretPlainValue -ResolvedSecretPath $ResolvedSecretPath
    try {
        Set-Clipboard -Value $plainValue
        Write-Host 'The secret has been copied to the clipboard.' -ForegroundColor Green
    }
    finally {
        $plainValue = $null
    }
}

function Show-DpapiSecretMetadata {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    $credential = Import-DpapiSecretCredential -ResolvedSecretPath $ResolvedSecretPath
    $item = Get-Item -LiteralPath $ResolvedSecretPath
    [pscustomobject]@{
        SecretPath = $ResolvedSecretPath
        Label = $credential.UserName
        LengthBytes = $item.Length
        LastWriteTime = $item.LastWriteTime
    } | Format-List | Out-String | Write-Host
}

function Show-DpapiSecretMenuScreen {
    Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
    Write-MenuItem -Key '1' -Label 'Protect a new or existing secret file'
    Write-MenuItem -Key '2' -Label 'Inspect a protected file'
    Write-MenuItem -Key '3' -Label 'Reveal the decrypted secret'
    Write-MenuItem -Key '4' -Label 'Copy the decrypted secret to the clipboard'
    Write-MenuItem -Key '5' -Label 'Reveal and copy the decrypted secret'
    Write-MenuItem -Key '6' -Label 'Run preflight for an existing file'
    Write-MenuItem -Key '0' -Label 'Exit'
    Write-Host ''
}

function Invoke-DpapiSecretMenuLoop {
    while ($true) {
        Initialize-SharedBootstrap -ClearScreen
        Show-DpapiSecretMenuScreen
        $choice = Read-MenuChoice -Prompt 'Select' -AllowedChoices @('0', '1', '2', '3', '4', '5', '6')
        if ($choice -eq '0') {
            return
        }

        Initialize-SharedBootstrap -ClearScreen
        Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
        $resolvedSecretPath = Resolve-DpapiSecretPath

        switch ($choice) {
            '1' {
                $resolvedSecretName = Resolve-DpapiSecretName -ResolvedSecretPath $resolvedSecretPath
                Protect-DpapiSecretValue -ResolvedSecretPath $resolvedSecretPath -ResolvedSecretName $resolvedSecretName
            }
            '2' {
                Invoke-DpapiSecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
                Show-DpapiSecretMetadata -ResolvedSecretPath $resolvedSecretPath
            }
            '3' {
                Invoke-DpapiSecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
                Show-DpapiSecretValue -ResolvedSecretPath $resolvedSecretPath
            }
            '4' {
                Invoke-DpapiSecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard
                Copy-DpapiSecretValue -ResolvedSecretPath $resolvedSecretPath
            }
            '5' {
                Invoke-DpapiSecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard
                Show-DpapiSecretValue -ResolvedSecretPath $resolvedSecretPath -CopyAfter
            }
            '6' {
                Invoke-DpapiSecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
            }
        }

        Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        if (-not $SkipPause) {
            Wait-ForEnter
        }
    }
}

function Invoke-TrackMeUpSecretTool {
    switch ($SecretToolAction) {
        'Menu' { Invoke-DpapiSecretMenuLoop }
        'Preflight' {
            $resolvedSecretPath = Resolve-DpapiSecretPath
            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Invoke-DpapiSecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Protect' {
            $resolvedSecretPath = Resolve-DpapiSecretPath
            $resolvedSecretName = Resolve-DpapiSecretName -ResolvedSecretPath $resolvedSecretPath
            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Protect-DpapiSecretValue -ResolvedSecretPath $resolvedSecretPath -ResolvedSecretName $resolvedSecretName
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Reveal' {
            $resolvedSecretPath = Resolve-DpapiSecretPath
            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Invoke-DpapiSecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
            Show-DpapiSecretValue -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Copy' {
            $resolvedSecretPath = Resolve-DpapiSecretPath
            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Invoke-DpapiSecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard
            Copy-DpapiSecretValue -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'PrintAndCopy' {
            $resolvedSecretPath = Resolve-DpapiSecretPath
            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Invoke-DpapiSecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard
            Show-DpapiSecretValue -ResolvedSecretPath $resolvedSecretPath -CopyAfter
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Inspect' {
            $resolvedSecretPath = Resolve-DpapiSecretPath
            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Invoke-DpapiSecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
            Show-DpapiSecretMetadata -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        default { throw "Unsupported DPAPI secret action: $SecretToolAction" }
    }
}

function Get-YkmanExecutable {
    $directPath = 'C:\Program Files\Yubico\YubiKey Manager\ykman.exe'
    if (Test-Path -LiteralPath $directPath -PathType Leaf) {
        return $directPath
    }

    $command = Get-Command -Name 'ykman.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) {
        return $command.Source
    }

    throw 'ykman.exe was not found. Install YubiKey Manager first.'
}

function Invoke-Ykman {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$AllowFailure
    )

    $ykmanPath = Get-YkmanExecutable
    $output = & $ykmanPath @Arguments 2>&1
    $exitCode = $LASTEXITCODE

    if (-not $AllowFailure -and $exitCode -ne 0) {
        $message = ($output | ForEach-Object { "$_" }) -join [Environment]::NewLine
        if ([string]::IsNullOrWhiteSpace($message)) {
            $message = "ykman exited with code $exitCode."
        }

        throw $message.Trim()
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = @($output)
    }
}

function ConvertTo-YubiSecretPath {
    param([string]$PathText)

    $trimmed = ($PathText ?? '').Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        throw 'A file path is required.'
    }

    return [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($trimmed))
}

function Resolve-YubiSecretPath {
    if (-not [string]::IsNullOrWhiteSpace($SecretPath)) {
        return ConvertTo-YubiSecretPath -PathText $SecretPath
    }

    while ($true) {
        $entered = Read-Host 'Protected XML file path'
        try {
            return ConvertTo-YubiSecretPath -PathText $entered
        }
        catch {
            Write-Host $_.Exception.Message -ForegroundColor Yellow
        }
    }
}

function Resolve-YubiCredentialXmlPath {
    if (-not [string]::IsNullOrWhiteSpace($CredentialXmlPath)) {
        return ConvertTo-YubiSecretPath -PathText $CredentialXmlPath
    }

    while ($true) {
        $entered = Read-Host 'Source credential XML path'
        try {
            return ConvertTo-YubiSecretPath -PathText $entered
        }
        catch {
            Write-Host $_.Exception.Message -ForegroundColor Yellow
        }
    }
}

function Resolve-YubiSecretLabel {
    param(
        [string]$ResolvedSecretPath,
        [string]$FallbackLabel
    )

    if (-not [string]::IsNullOrWhiteSpace($SecretName)) {
        return $SecretName.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($FallbackLabel)) {
        return $FallbackLabel
    }

    return [System.IO.Path]::GetFileNameWithoutExtension($ResolvedSecretPath)
}

function Get-YubiOtpInfo {
    $result = Invoke-Ykman -Arguments @('otp', 'info')
    $slotLines = @{}

    foreach ($line in $result.Output) {
        if ($line -match '^Slot\s+([12]):\s+(.+)$') {
            $slotLines[$Matches[1]] = $Matches[2].Trim()
        }
    }

    return [pscustomobject]@{
        Slot1 = $slotLines['1']
        Slot2 = $slotLines['2']
    }
}

function Get-YubiSerialNumber {
    $result = Invoke-Ykman -Arguments @('list', '--serials')
    $serial = ($result.Output | Select-Object -First 1).ToString().Trim()
    if ([string]::IsNullOrWhiteSpace($serial)) {
        return ''
    }

    return $serial
}

function Test-YubiSlotProgrammed {
    param([Parameter(Mandatory)][string]$SlotNumber)

    $info = Get-YubiOtpInfo
    $status = if ($SlotNumber -eq '1') { $info.Slot1 } else { $info.Slot2 }
    return ($status -match 'programmed')
}

function Invoke-YubiPreflight {
    param(
        [string]$ResolvedSecretPath,
        [switch]$RequireExistingSecret,
        [switch]$RequireClipboard,
        [switch]$RequireProgrammedSlot
    )

    $checks = [System.Collections.Generic.List[object]]::new()
    $ykmanPath = ''

    try {
        $ykmanPath = Get-YkmanExecutable
        $checks.Add([pscustomobject]@{
            Check = 'ykman.exe'
            Status = 'OK'
            Details = $ykmanPath
        })
    }
    catch {
        $checks.Add([pscustomobject]@{
            Check = 'ykman.exe'
            Status = 'FAIL'
            Details = $_.Exception.Message
        })
    }

    $checks.Add([pscustomobject]@{
        Check = 'PowerShell'
        Status = if ($PSVersionTable.PSVersion.Major -ge 7) { 'OK' } else { 'FAIL' }
        Details = ("Detected {0}" -f $PSVersionTable.PSVersion)
    })

    if ($RequireClipboard) {
        $clipboardCommand = Get-Command -Name Set-Clipboard -ErrorAction SilentlyContinue | Select-Object -First 1
        $checks.Add([pscustomobject]@{
            Check = 'Clipboard'
            Status = if ($clipboardCommand) { 'OK' } else { 'FAIL' }
            Details = if ($clipboardCommand) { $clipboardCommand.Source } else { 'Set-Clipboard not available' }
        })
    }

    if ($RequireExistingSecret -and -not [string]::IsNullOrWhiteSpace($ResolvedSecretPath)) {
        $exists = Test-Path -LiteralPath $ResolvedSecretPath -PathType Leaf
        $checks.Add([pscustomobject]@{
            Check = "File: $(Split-Path -Leaf $ResolvedSecretPath)"
            Status = if ($exists) { 'OK' } else { 'FAIL' }
            Details = $ResolvedSecretPath
        })
    }

    if ($RequireProgrammedSlot -and $ykmanPath) {
        try {
            $slotReady = Test-YubiSlotProgrammed -SlotNumber $Slot
            $checks.Add([pscustomobject]@{
                Check = "OTP slot $Slot"
                Status = if ($slotReady) { 'OK' } else { 'FAIL' }
                Details = if ($slotReady) { 'Challenge-response slot appears programmed.' } else { 'Slot is empty. Run SetupSlot first.' }
            })
        }
        catch {
            $checks.Add([pscustomobject]@{
                Check = "OTP slot $Slot"
                Status = 'FAIL'
                Details = $_.Exception.Message
            })
        }
    }

    Write-Section -Title 'YubiKey preflight'
    $checks | Format-Table -AutoSize | Out-String | Write-Host

    if ($checks.Status -contains 'FAIL') {
        throw 'Preflight did not pass. Resolve the failing checks and try again.'
    }
}

function New-RandomBytes {
    param([Parameter(Mandatory)][int]$Length)

    $buffer = [byte[]]::new($Length)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
    return $buffer
}

function ConvertTo-HexString {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    return ([System.BitConverter]::ToString($Bytes)).Replace('-', '').ToLowerInvariant()
}

function ConvertFrom-HexString {
    param([Parameter(Mandatory)][string]$Hex)

    $cleanHex = ($Hex ?? '').Trim()
    if (($cleanHex.Length % 2) -ne 0) {
        throw 'Hex input length must be even.'
    }

    $buffer = [byte[]]::new($cleanHex.Length / 2)
    for ($i = 0; $i -lt $buffer.Length; $i++) {
        $buffer[$i] = [Convert]::ToByte($cleanHex.Substring($i * 2, 2), 16)
    }

    return $buffer
}

function Get-YubiChallengeResponse {
    param(
        [Parameter(Mandatory)][string]$SlotNumber,
        [Parameter(Mandatory)][byte[]]$ChallengeBytes
    )

    Write-Host 'Insert the YubiKey now. Touch it if prompted.' -ForegroundColor Yellow
    $challengeHex = ConvertTo-HexString -Bytes $ChallengeBytes
    $result = Invoke-Ykman -Arguments @('otp', 'calculate', $SlotNumber, $challengeHex)
    $responseLine = $result.Output |
        ForEach-Object { "$_".Trim() } |
        Where-Object { $_ -match '^[0-9a-fA-F]+$' } |
        Select-Object -Last 1

    if ([string]::IsNullOrWhiteSpace($responseLine)) {
        $message = ($result.Output | ForEach-Object { "$_" }) -join [Environment]::NewLine
        throw "Could not read a challenge-response output from YubiKey. $message"
    }

    return (ConvertFrom-HexString -Hex $responseLine)
}

function Get-AesKeyFromYubiResponse {
    param(
        [Parameter(Mandatory)][byte[]]$ResponseBytes,
        [Parameter(Mandatory)][byte[]]$SaltBytes
    )

    $kdf = [System.Security.Cryptography.Rfc2898DeriveBytes]::new($ResponseBytes, $SaltBytes, 200000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        return $kdf.GetBytes(32)
    }
    finally {
        $kdf.Dispose()
    }
}

function Protect-PlaintextWithYubi {
    param(
        [Parameter(Mandatory)][string]$ResolvedSecretPath,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$PlaintextSecret
    )

    if (Test-Path -LiteralPath $ResolvedSecretPath -PathType Leaf) {
        if (-not $Force) {
            $overwriteChoice = Read-Host 'The target file already exists. Overwrite it? [y/N]'
            if ($overwriteChoice -notmatch '^(y|yes|s|si)$') {
                Write-Host 'Nothing changed.' -ForegroundColor DarkYellow
                return
            }
        }
    }

    $parentPath = Split-Path -Path $ResolvedSecretPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($parentPath) -and -not (Test-Path -LiteralPath $parentPath -PathType Container)) {
        New-Item -ItemType Directory -Path $parentPath -Force | Out-Null
    }

    $challengeBytes = New-RandomBytes -Length 32
    $saltBytes = New-RandomBytes -Length 16
    $nonceBytes = New-RandomBytes -Length 12
    $responseBytes = Get-YubiChallengeResponse -SlotNumber $Slot -ChallengeBytes $challengeBytes
    $keyBytes = Get-AesKeyFromYubiResponse -ResponseBytes $responseBytes -SaltBytes $saltBytes
    $plainBytes = [System.Text.Encoding]::UTF8.GetBytes($PlaintextSecret)
    $cipherBytes = [byte[]]::new($plainBytes.Length)
    $tagBytes = [byte[]]::new(16)
    $serial = Get-YubiSerialNumber
    $aes = $null

    try {
        $aes = [System.Security.Cryptography.AesGcm]::new($keyBytes, 16)
        $aes.Encrypt($nonceBytes, $plainBytes, $cipherBytes, $tagBytes)
    }
    finally {
        if ($aes) { $aes.Dispose() }
        [Array]::Clear($plainBytes, 0, $plainBytes.Length)
        [Array]::Clear($keyBytes, 0, $keyBytes.Length)
        [Array]::Clear($responseBytes, 0, $responseBytes.Length)
    }

    $payload = [pscustomobject]@{
        Format = 'YubiKeySecretV1'
        Label = $Label
        Slot = $Slot
        YubiKeySerial = $serial
        ChallengeHex = (ConvertTo-HexString -Bytes $challengeBytes)
        SaltHex = (ConvertTo-HexString -Bytes $saltBytes)
        NonceHex = (ConvertTo-HexString -Bytes $nonceBytes)
        CiphertextBase64 = [Convert]::ToBase64String($cipherBytes)
        TagBase64 = [Convert]::ToBase64String($tagBytes)
        CreatedAtUtc = [DateTime]::UtcNow.ToString('o')
        Notes = 'Protected with YubiKey OTP challenge-response.'
    }

    $payload | Export-Clixml -LiteralPath $ResolvedSecretPath
    Write-Host "Protected YubiKey XML written to: $ResolvedSecretPath" -ForegroundColor Green
}

function Import-YubiProtectedFile {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    if (-not (Test-Path -LiteralPath $ResolvedSecretPath -PathType Leaf)) {
        throw "Protected file not found: $ResolvedSecretPath"
    }

    $payload = Import-Clixml -LiteralPath $ResolvedSecretPath
    if ($null -eq $payload -or $payload.Format -ne 'YubiKeySecretV1') {
        throw 'This file is not a supported YubiKey protected XML payload.'
    }

    return $payload
}

function Get-PlaintextFromYubiProtectedFile {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    $payload = Import-YubiProtectedFile -ResolvedSecretPath $ResolvedSecretPath
    $challengeBytes = ConvertFrom-HexString -Hex $payload.ChallengeHex
    $saltBytes = ConvertFrom-HexString -Hex $payload.SaltHex
    $nonceBytes = ConvertFrom-HexString -Hex $payload.NonceHex
    $cipherBytes = [Convert]::FromBase64String($payload.CiphertextBase64)
    $tagBytes = [Convert]::FromBase64String($payload.TagBase64)
    $responseBytes = Get-YubiChallengeResponse -SlotNumber ([string]$payload.Slot) -ChallengeBytes $challengeBytes
    $keyBytes = Get-AesKeyFromYubiResponse -ResponseBytes $responseBytes -SaltBytes $saltBytes
    $plainBytes = [byte[]]::new($cipherBytes.Length)
    $aes = $null

    try {
        $aes = [System.Security.Cryptography.AesGcm]::new($keyBytes, 16)
        $aes.Decrypt($nonceBytes, $cipherBytes, $tagBytes, $plainBytes)
        return [System.Text.Encoding]::UTF8.GetString($plainBytes)
    }
    finally {
        if ($aes) { $aes.Dispose() }
        [Array]::Clear($plainBytes, 0, $plainBytes.Length)
        [Array]::Clear($keyBytes, 0, $keyBytes.Length)
        [Array]::Clear($responseBytes, 0, $responseBytes.Length)
    }
}

function Protect-FromYubiPrompt {
    $resolvedSecretPath = Resolve-YubiSecretPath
    $label = Resolve-YubiSecretLabel -ResolvedSecretPath $resolvedSecretPath -FallbackLabel ''
    $secureValue = Read-SecretSecureString
    $plainValue = ([pscredential]::new($label, $secureValue)).GetNetworkCredential().Password

    try {
        Protect-PlaintextWithYubi -ResolvedSecretPath $resolvedSecretPath -Label $label -PlaintextSecret $plainValue
    }
    finally {
        $plainValue = $null
        $secureValue = $null
    }
}

function Migrate-CredentialXmlToYubi {
    $resolvedCredentialPath = Resolve-YubiCredentialXmlPath
    $resolvedSecretPath = Resolve-YubiSecretPath

    if (-not (Test-Path -LiteralPath $resolvedCredentialPath -PathType Leaf)) {
        throw "Credential XML not found: $resolvedCredentialPath"
    }

    $credential = Import-Clixml -LiteralPath $resolvedCredentialPath
    if ($null -eq $credential) {
        throw 'The source credential XML could not be read.'
    }

    try {
        $label = Resolve-YubiSecretLabel -ResolvedSecretPath $resolvedSecretPath -FallbackLabel $credential.UserName
        $plainValue = $credential.GetNetworkCredential().Password
        if ([string]::IsNullOrWhiteSpace($plainValue)) {
            throw 'The source credential XML does not contain a password value.'
        }

        Protect-PlaintextWithYubi -ResolvedSecretPath $resolvedSecretPath -Label $label -PlaintextSecret $plainValue
    }
    finally {
        $credential = $null
        $plainValue = $null
    }
}

function Show-YubiProtectedSecret {
    param(
        [Parameter(Mandatory)][string]$ResolvedSecretPath,
        [switch]$CopyAfter
    )

    $plainValue = Get-PlaintextFromYubiProtectedFile -ResolvedSecretPath $ResolvedSecretPath
    try {
        Write-Section -Title 'Secret value'
        Write-Host ''
        Write-Host $plainValue -ForegroundColor Yellow
        Write-Host ''
        Write-Host 'Do not paste this into source files, logs, screenshots, or tickets.' -ForegroundColor DarkYellow

        if ($CopyAfter) {
            Set-Clipboard -Value $plainValue
            Write-Host 'The secret has also been copied to the clipboard.' -ForegroundColor Green
        }
    }
    finally {
        $plainValue = $null
    }
}

function Copy-YubiProtectedSecret {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    $plainValue = Get-PlaintextFromYubiProtectedFile -ResolvedSecretPath $ResolvedSecretPath
    try {
        Set-Clipboard -Value $plainValue
        Write-Host 'The secret has been copied to the clipboard.' -ForegroundColor Green
    }
    finally {
        $plainValue = $null
    }
}

function Show-YubiProtectedMetadata {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    $payload = Import-YubiProtectedFile -ResolvedSecretPath $ResolvedSecretPath
    [pscustomobject]@{
        SecretPath = $ResolvedSecretPath
        Label = $payload.Label
        Slot = $payload.Slot
        YubiKeySerial = $payload.YubiKeySerial
        CreatedAtUtc = $payload.CreatedAtUtc
        Format = $payload.Format
    } | Format-List | Out-String | Write-Host
}

function Initialize-YubiSlot {
    if (-not $Force) {
        Write-Host 'This will program the selected OTP slot for challenge-response and replace its current contents.' -ForegroundColor Yellow
        $confirm = Read-Host 'Continue? [y/N]'
        if ($confirm -notmatch '^(y|yes|s|si)$') {
            Write-Host 'Nothing changed.' -ForegroundColor DarkYellow
            return
        }
    }

    Write-Host 'Insert the YubiKey now. Touch will be required for future decryptions.' -ForegroundColor Yellow
    Invoke-Ykman -Arguments @('otp', 'chalresp', '--generate', '--touch', '--force', $Slot) | Out-Null
    Write-Host "OTP slot $Slot has been configured for challenge-response." -ForegroundColor Green
}

function Show-YubiSecretMenuScreen {
    Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
    Write-MenuItem -Key '1' -Label 'Run preflight'
    Write-MenuItem -Key '2' -Label 'Setup OTP slot for challenge-response'
    Write-MenuItem -Key '3' -Label 'Protect a new secret into YubiKey XML'
    Write-MenuItem -Key '4' -Label 'Migrate an existing DPAPI credential XML'
    Write-MenuItem -Key '5' -Label 'Inspect a protected YubiKey XML file'
    Write-MenuItem -Key '6' -Label 'Reveal a protected secret'
    Write-MenuItem -Key '7' -Label 'Copy a protected secret to the clipboard'
    Write-MenuItem -Key '8' -Label 'Reveal and copy a protected secret'
    Write-MenuItem -Key '0' -Label 'Exit'
    Write-Host ''
}

function Invoke-YubiSecretMenuLoop {
    while ($true) {
        Initialize-SharedBootstrap -ClearScreen
        Show-YubiSecretMenuScreen
        $choice = Read-MenuChoice -Prompt 'Select' -AllowedChoices @('0', '1', '2', '3', '4', '5', '6', '7', '8')
        if ($choice -eq '0') {
            return
        }

        Initialize-SharedBootstrap -ClearScreen
        Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'

        switch ($choice) {
            '1' {
                Invoke-YubiPreflight -RequireProgrammedSlot
            }
            '2' {
                Invoke-YubiPreflight
                Initialize-YubiSlot
            }
            '3' {
                Invoke-YubiPreflight -RequireProgrammedSlot
                Protect-FromYubiPrompt
            }
            '4' {
                Invoke-YubiPreflight -RequireProgrammedSlot
                Migrate-CredentialXmlToYubi
            }
            '5' {
                $resolvedSecretPath = Resolve-YubiSecretPath
                Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
                Show-YubiProtectedMetadata -ResolvedSecretPath $resolvedSecretPath
            }
            '6' {
                $resolvedSecretPath = Resolve-YubiSecretPath
                Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireProgrammedSlot
                Show-YubiProtectedSecret -ResolvedSecretPath $resolvedSecretPath
            }
            '7' {
                $resolvedSecretPath = Resolve-YubiSecretPath
                Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard -RequireProgrammedSlot
                Copy-YubiProtectedSecret -ResolvedSecretPath $resolvedSecretPath
            }
            '8' {
                $resolvedSecretPath = Resolve-YubiSecretPath
                Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard -RequireProgrammedSlot
                Show-YubiProtectedSecret -ResolvedSecretPath $resolvedSecretPath -CopyAfter
            }
        }

        Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        if (-not $SkipPause) {
            Wait-ForEnter
        }
    }
}

function Invoke-TrackMeUpYubiKeySecretTool {
    switch ($SecretToolAction) {
        'Menu' { Invoke-YubiSecretMenuLoop }
        'Preflight' {
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -RequireProgrammedSlot
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'SetupSlot' {
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight
            Initialize-YubiSlot
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Protect' {
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -RequireProgrammedSlot
            Protect-FromYubiPrompt
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'MigrateCredentialXml' {
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -RequireProgrammedSlot
            Migrate-CredentialXmlToYubi
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Reveal' {
            $resolvedSecretPath = Resolve-YubiSecretPath
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireProgrammedSlot
            Show-YubiProtectedSecret -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Copy' {
            $resolvedSecretPath = Resolve-YubiSecretPath
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard -RequireProgrammedSlot
            Copy-YubiProtectedSecret -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'PrintAndCopy' {
            $resolvedSecretPath = Resolve-YubiSecretPath
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard -RequireProgrammedSlot
            Show-YubiProtectedSecret -ResolvedSecretPath $resolvedSecretPath -CopyAfter
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Inspect' {
            $resolvedSecretPath = Resolve-YubiSecretPath
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
            Show-YubiProtectedMetadata -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $script:ScriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        default { throw "Unsupported YubiKey secret action: $SecretToolAction" }
    }
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
        'CreateInstaller' { Invoke-TrackMeUpInstallerCreation }
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
        Write-MenuItem -Key '10' -Label 'Create installer file'
        Write-MenuItem -Key '11' -Label 'Probe taskbar widget'
        Write-MenuItem -Key '12' -Label 'DPAPI secret helper'
        Write-MenuItem -Key '13' -Label 'YubiKey secret helper'
        Write-MenuItem -Key '0' -Label 'Exit'

        $choice = Read-MenuChoice -Prompt 'Select' -AllowedChoices @('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12', '13')
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
            '10' { 'CreateInstaller' }
            '11' { 'ProbeTaskbar' }
            '12' { 'ProtectSecret' }
            '13' { 'ProtectSecretYubiKey' }
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

Initialize-SharedBootstrap

if ($script:InteractiveMode) {
    Show-TrackMeUpMenu
    exit 0
}

if ($Action -eq 'Menu') {
    Show-TrackMeUpMenu
    exit 0
}

Invoke-TrackMeUpActionForAgent -RequestedAction $Action
