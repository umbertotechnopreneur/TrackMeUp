#requires -Version 7.0
<#
.SYNOPSIS
Smoke-tests the packaged sensor helper using ordinary privileges and bounded IPC.
.DESCRIPTION
Does not install, activate or request elevation for PawnIO. It prints aggregate
counts only, never sensor names, device identifiers, serial numbers or raw reports.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$HelperPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$resolvedHelperPath = [IO.Path]::GetFullPath($HelperPath)
if (-not (Test-Path -LiteralPath $resolvedHelperPath -PathType Leaf)) { throw 'Hardware helper executable is missing.' }
$pipeName = 'TrackMeUp.Hardware.' + [Guid]::NewGuid().ToString('N')
$pipe = [IO.Pipes.NamedPipeServerStream]::new($pipeName, [IO.Pipes.PipeDirection]::InOut, 1,
    [IO.Pipes.PipeTransmissionMode]::Byte, [IO.Pipes.PipeOptions]::Asynchronous -bor [IO.Pipes.PipeOptions]::CurrentUserOnly)
$child = $null

function Read-HardwareFrame {
    $deadline = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(12))
    try {
        $header = [byte[]]::new(4)
        $pipe.ReadExactlyAsync([Memory[byte]]::new($header), $deadline.Token).AsTask().GetAwaiter().GetResult()
        $length = [BitConverter]::ToInt32($header, 0)
        if ($length -le 0 -or $length -gt 524288) { throw 'Invalid hardware frame length.' }
        $payload = [byte[]]::new($length)
        $pipe.ReadExactlyAsync([Memory[byte]]::new($payload), $deadline.Token).AsTask().GetAwaiter().GetResult()
        return ([Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json -Depth 32)
    }
    finally { $deadline.Dispose() }
}

try {
    $startInfo = [Diagnostics.ProcessStartInfo]::new($resolvedHelperPath)
    $startInfo.WorkingDirectory = [IO.Path]::GetDirectoryName($resolvedHelperPath)
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    foreach ($argument in @('--pipe', $pipeName, '--parent', [string]$PID)) { $startInfo.ArgumentList.Add($argument) }
    $child = [Diagnostics.Process]::Start($startInfo)
    $connectionDeadline = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(12))
    try { $pipe.WaitForConnectionAsync($connectionDeadline.Token).GetAwaiter().GetResult() }
    finally { $connectionDeadline.Dispose() }
    $hello = Read-HardwareFrame
    if ($hello.Version -ne 1) { throw 'Hardware helper protocol version mismatch.' }

    $previousStorageTimes = @{}
    foreach ($sampleNumber in 1..2) {
        $payload = [Text.Encoding]::UTF8.GetBytes('{"Version":1,"Command":"sample"}')
        $header = [BitConverter]::GetBytes([int]$payload.Length)
        $pipe.Write($header)
        $pipe.Write($payload)
        $pipe.Flush()
        $snapshot = Read-HardwareFrame
        if ($snapshot.Status -notin @('ready', 'partial')) { throw "Hardware smoke failed: $($snapshot.Status), $($snapshot.ErrorCode)." }
        if ($snapshot.DriverStatus -notin @('available', 'not-installed')) { throw 'Standard-mode smoke unexpectedly activated low-level access.' }
        $devices = @($snapshot.Devices)
        $sensors = @($devices | ForEach-Object { $_.Sensors })
        $validCount = @($sensors | Where-Object { $null -ne $_.Value }).Count
        if ($devices.Count -gt 64 -or $sensors.Count -gt 4096 -or $validCount -eq 0) { throw 'Hardware smoke returned invalid sensor cardinality.' }
        foreach ($device in $devices | Where-Object Kind -eq 'Storage') {
            if ($sampleNumber -eq 1) { $previousStorageTimes[$device.Id] = $device.SampledAt }
            elseif ($previousStorageTimes.ContainsKey($device.Id) -and $previousStorageTimes[$device.Id] -ne $device.SampledAt) {
                throw 'Storage was polled again inside the 30-second sampling window.'
            }
        }
        [pscustomobject]@{
            Sample = $sampleNumber
            Status = $snapshot.Status
            DriverStatus = $snapshot.DriverStatus
            Devices = $devices.Count
            Sensors = $sensors.Count
            UsableSensors = $validCount
            UsableBatterySensors = @($devices | Where-Object Kind -eq 'Battery' | ForEach-Object { $_.Sensors } | Where-Object { $null -ne $_.Value }).Count
            DeviceKinds = (@($devices.Kind | Sort-Object -Unique) -join ', ')
        }
        if ($sampleNumber -eq 1) { Start-Sleep -Milliseconds 2100 }
    }
}
finally {
    $pipe.Dispose()
    if ($null -ne $child) {
        try {
            # Only this exact, unelevated test child is eligible for cleanup.
            if (-not $child.WaitForExit(1500)) { $child.Kill() }
        }
        finally { $child.Dispose() }
    }
}
