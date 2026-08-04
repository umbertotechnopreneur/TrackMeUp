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
