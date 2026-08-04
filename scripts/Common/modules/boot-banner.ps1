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
