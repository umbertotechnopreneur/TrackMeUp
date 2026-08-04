<#
.SYNOPSIS
Protects or reveals a single secret value using Windows DPAPI.

.DESCRIPTION
Creates or reads a `.credential.xml`-style secret file backed by
`Export-Clixml` and the current Windows user profile. It uses the shared
branding, preflight, menu, and footer helpers stored under `70_Assets` so the
same look and feel can be reused on other machines.

The stored file can only be decrypted by the same Windows user account on a
compatible machine context, which makes it useful for local one-user secrets
such as test API keys, tokens, or short passwords.

.PARAMETER Action
Menu         Opens the interactive menu.
Protect      Prompts for a secret and writes the protected file.
Reveal       Prints the decrypted secret to the terminal.
Copy         Copies the decrypted secret to the clipboard.
PrintAndCopy Prints the secret and copies it to the clipboard.
Inspect      Shows file metadata and stored label without revealing the secret.
Preflight    Runs dependency checks for the requested path and action.

.PARAMETER SecretPath
Path to the protected file. A `.credential.xml` extension is recommended.

.PARAMETER SecretName
Logical label stored as the PSCredential username field. If omitted, the file
name without extension is used.

.PARAMETER InputValue
Optional plaintext value to protect. If omitted, the script prompts for it.
Avoid passing real secrets on the command line when possible.

.PARAMETER Force
Overwrites an existing secret file without asking.

.PARAMETER Help
Shows help and exits.

.PARAMETER SkipPause
Skips the menu pause prompt after each action.

.EXAMPLE
pwsh -NoProfile -File '.\Protect-SecretValue.ps1'

.EXAMPLE
pwsh -NoProfile -File '.\Protect-SecretValue.ps1' -Action Protect -SecretPath 'C:\Secrets\OpenAI Test Key.credential.xml'

.EXAMPLE
pwsh -NoProfile -File '.\Protect-SecretValue.ps1' -Action Reveal -SecretPath 'C:\Secrets\OpenAI Test Key.credential.xml'
#>
[CmdletBinding()]
param(
    [ValidateSet('Menu', 'Protect', 'Reveal', 'Copy', 'PrintAndCopy', 'Inspect', 'Preflight')]
    [string]$Action = 'Menu',
    [string]$SecretPath,
    [string]$SecretName,
    [string]$InputValue,
    [switch]$Force,
    [switch]$Help,
    [switch]$SkipPause
)

if ($Help) {
    Get-Help $PSCommandPath -Detailed
    exit 0
}

$ErrorActionPreference = 'Stop'
$script:StartedAt = Get-Date
$scriptName = Split-Path -Leaf $PSCommandPath
$modulesPath = Join-Path $PSScriptRoot 'Common\modules'

foreach ($moduleName in @(
    'shared-bootstrap.ps1',
    'boot-banner.ps1',
    'footer-banner.ps1',
    'menu-utils.ps1',
    'preflight-checks.ps1'
)) {
    $modulePath = Join-Path $modulesPath $moduleName
    if (-not (Test-Path -LiteralPath $modulePath -PathType Leaf)) {
        throw "Shared module not found: $modulePath"
    }

    . $modulePath
}

function ConvertTo-NormalizedSecretPath {
    param([string]$PathText)

    $trimmed = ($PathText ?? '').Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        throw 'A secret file path is required.'
    }

    return [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($trimmed))
}

function Get-SecretPathInteractive {
    param(
        [string]$PromptText = 'Secret file path'
    )

    while ($true) {
        $enteredPath = Read-Host $PromptText
        try {
            return ConvertTo-NormalizedSecretPath -PathText $enteredPath
        }
        catch {
            Write-Host $_.Exception.Message -ForegroundColor Yellow
        }
    }
}

function Resolve-SecretPath {
    if (-not [string]::IsNullOrWhiteSpace($SecretPath)) {
        return ConvertTo-NormalizedSecretPath -PathText $SecretPath
    }

    return Get-SecretPathInteractive
}

function Resolve-SecretName {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    if (-not [string]::IsNullOrWhiteSpace($SecretName)) {
        return $SecretName.Trim()
    }

    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($ResolvedSecretPath)
    if ([string]::IsNullOrWhiteSpace($fileName)) {
        return 'secret-value'
    }

    return $fileName
}

function Read-SecretSecureString {
    if (-not [string]::IsNullOrWhiteSpace($InputValue)) {
        return (ConvertTo-SecureString -String $InputValue -AsPlainText -Force)
    }

    while ($true) {
        $first = Read-Host 'Enter the secret value' -AsSecureString
        $second = Read-Host 'Confirm the secret value' -AsSecureString

        $firstCredential = [pscredential]::new('secret', $first)
        $secondCredential = [pscredential]::new('secret', $second)
        $firstPlain = $firstCredential.GetNetworkCredential().Password
        $secondPlain = $secondCredential.GetNetworkCredential().Password

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
            $firstCredential = $null
            $secondCredential = $null
            $firstPlain = $null
            $secondPlain = $null
        }
    }
}

function Import-SecretCredential {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    if (-not (Test-Path -LiteralPath $ResolvedSecretPath -PathType Leaf)) {
        throw "Secret file not found: $ResolvedSecretPath"
    }

    $credential = Import-Clixml -LiteralPath $ResolvedSecretPath
    if ($null -eq $credential) {
        throw 'The secret file could not be read.'
    }

    return $credential
}

function Get-SecretPlainValue {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    $credential = Import-SecretCredential -ResolvedSecretPath $ResolvedSecretPath
    try {
        $plainValue = $credential.GetNetworkCredential().Password
        if ([string]::IsNullOrWhiteSpace($plainValue)) {
            throw 'The decrypted file does not contain a password value.'
        }

        return $plainValue
    }
    finally {
        $credential = $null
    }
}

function Invoke-SecretPreflight {
    param(
        [string]$ResolvedSecretPath,
        [switch]$RequireExistingSecret,
        [switch]$RequireClipboard
    )

    $requiredFiles = @()
    if ($RequireExistingSecret -and -not [string]::IsNullOrWhiteSpace($ResolvedSecretPath)) {
        $requiredFiles += $ResolvedSecretPath
    }

    $probe = $null
    if ($RequireExistingSecret) {
        $probe = {
            $null = Get-SecretPlainValue -ResolvedSecretPath $ResolvedSecretPath
        }.GetNewClosure()
    }

    $isReady = Invoke-CommonPreflight `
        -Title 'Secret value preflight' `
        -RequiredCommands @('pwsh') `
        -RequiredFiles $requiredFiles `
        -RequireClipboard:$RequireClipboard `
        -ValidationScript $probe

    if (-not $isReady) {
        throw 'Preflight did not pass. Resolve the failing checks and try again.'
    }
}

function Protect-SecretValue {
    param(
        [Parameter(Mandatory)][string]$ResolvedSecretPath,
        [Parameter(Mandatory)][string]$ResolvedSecretName
    )

    if (Test-Path -LiteralPath $ResolvedSecretPath -PathType Leaf) {
        if (-not $Force) {
            $overwriteChoice = Read-Host 'The file already exists. Overwrite it? [y/N]'
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
    }
    finally {
        $credential = $null
        $secureValue = $null
    }

    Write-Host "Protected file written to: $ResolvedSecretPath" -ForegroundColor Green
}

function Show-SecretValue {
    param(
        [Parameter(Mandatory)][string]$ResolvedSecretPath,
        [switch]$CopyAfter
    )

    $plainValue = Get-SecretPlainValue -ResolvedSecretPath $ResolvedSecretPath
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

function Copy-SecretValue {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    $plainValue = Get-SecretPlainValue -ResolvedSecretPath $ResolvedSecretPath
    try {
        Set-Clipboard -Value $plainValue
        Write-Host 'The secret has been copied to the clipboard.' -ForegroundColor Green
    }
    finally {
        $plainValue = $null
    }
}

function Show-SecretMetadata {
    param([Parameter(Mandatory)][string]$ResolvedSecretPath)

    $credential = Import-SecretCredential -ResolvedSecretPath $ResolvedSecretPath
    $file = Get-Item -LiteralPath $ResolvedSecretPath

    try {
        [pscustomobject]@{
            SecretPath = $file.FullName
            SecretName = $credential.UserName
            Length = $file.Length
            LastWriteTime = $file.LastWriteTime
            CurrentUserCanDecrypt = $true
        } | Format-List | Out-String | Write-Host
    }
    finally {
        $credential = $null
        $file = $null
    }
}

function Show-MenuScreen {
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

function Invoke-MenuLoop {
    while ($true) {
        Initialize-SharedBootstrap -ClearScreen
        Show-MenuScreen

        $choice = Read-MenuChoice -Prompt 'Select' -AllowedChoices @('0', '1', '2', '3', '4', '5', '6')
        if ($choice -eq '0') {
            return
        }

        $resolvedSecretPath = Resolve-SecretPath
        $resolvedSecretName = Resolve-SecretName -ResolvedSecretPath $resolvedSecretPath

        Initialize-SharedBootstrap -ClearScreen
        Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'

        switch ($choice) {
            '1' {
                Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath
                Protect-SecretValue -ResolvedSecretPath $resolvedSecretPath -ResolvedSecretName $resolvedSecretName
            }
            '2' {
                Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
                Show-SecretMetadata -ResolvedSecretPath $resolvedSecretPath
            }
            '3' {
                Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
                Show-SecretValue -ResolvedSecretPath $resolvedSecretPath
            }
            '4' {
                Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard
                Copy-SecretValue -ResolvedSecretPath $resolvedSecretPath
            }
            '5' {
                Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard
                Show-SecretValue -ResolvedSecretPath $resolvedSecretPath -CopyAfter
            }
            '6' {
                Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
            }
        }

        Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        if (-not $SkipPause) {
            Wait-ForEnter
        }
    }
}

try {
    Initialize-SharedBootstrap -ClearScreen

    switch ($Action) {
        'Menu' {
            Invoke-MenuLoop
        }
        'Protect' {
            $resolvedSecretPath = Resolve-SecretPath
            $resolvedSecretName = Resolve-SecretName -ResolvedSecretPath $resolvedSecretPath
            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath
            Protect-SecretValue -ResolvedSecretPath $resolvedSecretPath -ResolvedSecretName $resolvedSecretName
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Reveal' {
            $resolvedSecretPath = Resolve-SecretPath
            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
            Show-SecretValue -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Copy' {
            $resolvedSecretPath = Resolve-SecretPath
            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard
            Copy-SecretValue -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'PrintAndCopy' {
            $resolvedSecretPath = Resolve-SecretPath
            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard
            Show-SecretValue -ResolvedSecretPath $resolvedSecretPath -CopyAfter
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Inspect' {
            $resolvedSecretPath = Resolve-SecretPath
            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
            Show-SecretMetadata -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Preflight' {
            $resolvedSecretPath = $null
            if (-not [string]::IsNullOrWhiteSpace($SecretPath)) {
                $resolvedSecretPath = Resolve-SecretPath
            }

            Show-Banner -Title 'Protect Secret Value' -Subtitle 'Reusable DPAPI helper for one-user local secrets'
            Invoke-SecretPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret:([bool]$resolvedSecretPath)
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
    }
}
catch {
    Show-FooterError -ScriptName $scriptName -ErrorMessage $_.Exception.Message -StartTime $script:StartedAt -EndTime (Get-Date)
    throw
}
