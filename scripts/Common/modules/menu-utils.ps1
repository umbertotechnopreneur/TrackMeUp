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
    param(
        [string]$Prompt = 'Press Enter to continue'
    )

    [void](Read-Host $Prompt)
}

function Write-MenuItem {
    param(
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][string]$Label
    )

    Write-Host ("[{0}] {1}" -f $Key, $Label) -ForegroundColor White
}
