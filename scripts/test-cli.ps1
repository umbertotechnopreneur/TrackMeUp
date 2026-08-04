[CmdletBinding()]
param(
    [string]$ExecutablePath = 'trackmeup.exe'
)

$ErrorActionPreference = 'Stop'

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

# CLI processes do not activate a WinUI window. The runtime host may remain in the background by design.
$visibleWindows = Get-Process -Name 'TrackMeUp' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
if ($visibleWindows) {
    throw 'CLI smoke test observed a visible TrackMeUp window.'
}

Write-Host 'TrackMeUp CLI smoke test completed.'
