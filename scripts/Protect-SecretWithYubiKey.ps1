<#
.SYNOPSIS
Protects or reveals a secret XML file using a YubiKey OTP challenge-response slot.

.DESCRIPTION
This helper creates an XML file whose secret payload is encrypted with a key
derived from a YubiKey OTP challenge-response operation. The intended flow is:

1. Program OTP slot 2 once with challenge-response and touch enabled.
2. Protect a secret value or migrate an existing DPAPI `.credential.xml` file.
3. Reveal or copy the protected secret later by inserting the same YubiKey.

The resulting file is XML, but it is not the same format as a DPAPI
`PSCredential` export. It is a portable metadata envelope whose encrypted
payload can only be decrypted by the configured YubiKey.

.PARAMETER Action
Menu                Opens the interactive menu.
Preflight           Checks ykman availability and slot status.
SetupSlot           Programs slot 2 for challenge-response with touch enabled.
Protect             Prompts for a secret and writes the protected XML file.
MigrateCredentialXml
                    Reads a DPAPI `.credential.xml` file and rewrites it into
                    a YubiKey-protected XML file.
Reveal              Decrypts the XML file and prints the secret.
Copy                Decrypts the XML file and copies the secret to the clipboard.
PrintAndCopy        Decrypts the XML file, prints it, and copies it.
Inspect             Shows metadata without revealing the secret.

.PARAMETER SecretPath
Path to the YubiKey-protected XML file.

.PARAMETER CredentialXmlPath
Path to an existing DPAPI `PSCredential` XML file to migrate.

.PARAMETER SecretName
Logical label stored in the protected metadata.

.PARAMETER InputValue
Optional plaintext secret value used by `Protect`. Avoid command-line secrets
when possible.

.PARAMETER Slot
OTP slot number to use. Defaults to slot 2.

.PARAMETER Force
Overwrites files or slot configuration without asking again.

.PARAMETER Help
Shows help and exits.

.PARAMETER SkipPause
Skips the pause prompt after interactive actions.

.EXAMPLE
pwsh -NoProfile -File '.\Protect-SecretWithYubiKey.ps1' -Action Preflight

.EXAMPLE
pwsh -NoProfile -File '.\Protect-SecretWithYubiKey.ps1' -Action SetupSlot

.EXAMPLE
pwsh -NoProfile -File '.\Protect-SecretWithYubiKey.ps1' -Action MigrateCredentialXml `
  -CredentialXmlPath 'C:\Users\umber\OneDrive\Obsidian\Vault\Views BYOK Test Key.credential.xml' `
  -SecretPath 'C:\Users\umber\OneDrive\Obsidian\Vault\Views BYOK Test Key.yubi.xml'
#>
[CmdletBinding()]
param(
    [ValidateSet('Menu', 'Preflight', 'SetupSlot', 'Protect', 'MigrateCredentialXml', 'Reveal', 'Copy', 'PrintAndCopy', 'Inspect')]
    [string]$Action = 'Menu',
    [string]$SecretPath,
    [string]$CredentialXmlPath,
    [string]$SecretName,
    [string]$InputValue,
    [ValidateSet('1', '2')]
    [string]$Slot = '2',
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

function ConvertTo-NormalizedPath {
    param([string]$PathText)

    $trimmed = ($PathText ?? '').Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        throw 'A file path is required.'
    }

    return [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($trimmed))
}

function Resolve-SecretPath {
    if (-not [string]::IsNullOrWhiteSpace($SecretPath)) {
        return ConvertTo-NormalizedPath -PathText $SecretPath
    }

    while ($true) {
        $entered = Read-Host 'Protected XML file path'
        try {
            return ConvertTo-NormalizedPath -PathText $entered
        }
        catch {
            Write-Host $_.Exception.Message -ForegroundColor Yellow
        }
    }
}

function Resolve-CredentialXmlPath {
    if (-not [string]::IsNullOrWhiteSpace($CredentialXmlPath)) {
        return ConvertTo-NormalizedPath -PathText $CredentialXmlPath
    }

    while ($true) {
        $entered = Read-Host 'Source credential XML path'
        try {
            return ConvertTo-NormalizedPath -PathText $entered
        }
        catch {
            Write-Host $_.Exception.Message -ForegroundColor Yellow
        }
    }
}

function Resolve-SecretLabel {
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

function Test-SlotProgrammed {
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
            $slotReady = Test-SlotProgrammed -SlotNumber $Slot
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

function Protect-FromPrompt {
    $resolvedSecretPath = Resolve-SecretPath
    $label = Resolve-SecretLabel -ResolvedSecretPath $resolvedSecretPath -FallbackLabel ''
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
    $resolvedCredentialPath = Resolve-CredentialXmlPath
    $resolvedSecretPath = Resolve-SecretPath

    if (-not (Test-Path -LiteralPath $resolvedCredentialPath -PathType Leaf)) {
        throw "Credential XML not found: $resolvedCredentialPath"
    }

    $credential = Import-Clixml -LiteralPath $resolvedCredentialPath
    if ($null -eq $credential) {
        throw 'The source credential XML could not be read.'
    }

    try {
        $label = Resolve-SecretLabel -ResolvedSecretPath $resolvedSecretPath -FallbackLabel $credential.UserName
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

function Show-ProtectedSecret {
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

function Copy-ProtectedSecret {
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

function Show-ProtectedMetadata {
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
        Write-Host 'This will program OTP slot 2 for challenge-response and replace its current contents.' -ForegroundColor Yellow
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

function Show-MenuScreen {
    Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
    Write-MenuItem -Key '1' -Label 'Run preflight'
    Write-MenuItem -Key '2' -Label 'Setup OTP slot 2 for challenge-response'
    Write-MenuItem -Key '3' -Label 'Protect a new secret into YubiKey XML'
    Write-MenuItem -Key '4' -Label 'Migrate an existing DPAPI credential XML'
    Write-MenuItem -Key '5' -Label 'Inspect a protected YubiKey XML file'
    Write-MenuItem -Key '6' -Label 'Reveal a protected secret'
    Write-MenuItem -Key '7' -Label 'Copy a protected secret to the clipboard'
    Write-MenuItem -Key '8' -Label 'Reveal and copy a protected secret'
    Write-MenuItem -Key '0' -Label 'Exit'
    Write-Host ''
}

function Invoke-MenuLoop {
    while ($true) {
        Initialize-SharedBootstrap -ClearScreen
        Show-MenuScreen
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
                Protect-FromPrompt
            }
            '4' {
                Invoke-YubiPreflight -RequireProgrammedSlot
                Migrate-CredentialXmlToYubi
            }
            '5' {
                $resolvedSecretPath = Resolve-SecretPath
                Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
                Show-ProtectedMetadata -ResolvedSecretPath $resolvedSecretPath
            }
            '6' {
                $resolvedSecretPath = Resolve-SecretPath
                Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireProgrammedSlot
                Show-ProtectedSecret -ResolvedSecretPath $resolvedSecretPath
            }
            '7' {
                $resolvedSecretPath = Resolve-SecretPath
                Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard -RequireProgrammedSlot
                Copy-ProtectedSecret -ResolvedSecretPath $resolvedSecretPath
            }
            '8' {
                $resolvedSecretPath = Resolve-SecretPath
                Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard -RequireProgrammedSlot
                Show-ProtectedSecret -ResolvedSecretPath $resolvedSecretPath -CopyAfter
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
        'Preflight' {
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -RequireProgrammedSlot
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'SetupSlot' {
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight
            Initialize-YubiSlot
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Protect' {
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -RequireProgrammedSlot
            Protect-FromPrompt
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'MigrateCredentialXml' {
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -RequireProgrammedSlot
            Migrate-CredentialXmlToYubi
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Reveal' {
            $resolvedSecretPath = Resolve-SecretPath
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireProgrammedSlot
            Show-ProtectedSecret -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Copy' {
            $resolvedSecretPath = Resolve-SecretPath
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard -RequireProgrammedSlot
            Copy-ProtectedSecret -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'PrintAndCopy' {
            $resolvedSecretPath = Resolve-SecretPath
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret -RequireClipboard -RequireProgrammedSlot
            Show-ProtectedSecret -ResolvedSecretPath $resolvedSecretPath -CopyAfter
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
        'Inspect' {
            $resolvedSecretPath = Resolve-SecretPath
            Show-Banner -Title 'Protect Secret With YubiKey' -Subtitle 'YubiKey-backed XML secret helper'
            Invoke-YubiPreflight -ResolvedSecretPath $resolvedSecretPath -RequireExistingSecret
            Show-ProtectedMetadata -ResolvedSecretPath $resolvedSecretPath
            Show-Footer -ScriptName $scriptName -Status 'COMPLETED' -StartTime $script:StartedAt -EndTime (Get-Date)
        }
    }
}
catch {
    Show-FooterError -ScriptName $scriptName -ErrorMessage $_.Exception.Message -StartTime $script:StartedAt -EndTime (Get-Date)
    throw
}
