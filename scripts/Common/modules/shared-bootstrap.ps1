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
    param(
        [switch]$ClearScreen
    )

    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8

    if ($ClearScreen) {
        Clear-TerminalFull
    }
}
