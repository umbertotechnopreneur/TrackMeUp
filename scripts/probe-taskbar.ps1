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

$script:sb = [System.Text.StringBuilder]::new(256)
$script:tb = [System.Text.StringBuilder]::new(256)
$script:results = [System.Collections.Generic.List[string]]::new()
$taskbar = [TaskbarProbe]::FindWindow("Shell_TrayWnd", $null)
Write-Host "Shell_TrayWnd handle: $taskbar"
if ($taskbar -eq 0) { return }

$script:count = 0
[TaskbarProbe]::EnumChildWindows($taskbar, [TaskbarProbe+EnumWindowsProc]{
    param($hWnd, $lParam)
    $script:sb.Clear() | Out-Null
    $script:tb.Clear() | Out-Null
    [void][TaskbarProbe]::GetClassName($hWnd, $script:sb, 256)
    [void][TaskbarProbe]::GetWindowText($hWnd, $script:tb, 256)
    $cls = $script:sb.ToString()
    $txt = $script:tb.ToString()
    $vis = [TaskbarProbe]::IsWindowVisible($hWnd)
    $rc = New-Object TaskbarProbe+RECT
    [void][TaskbarProbe]::GetWindowRect($hWnd, [ref]$rc)
    $w = $rc.Right - $rc.Left
    $h = $rc.Bottom - $rc.Top
    $script:count++
    if ($cls -like "*HwndWrapper*" -or $txt -like "*TrackMeUp*") {
        $script:results.Add("handle=$hWnd cls=$cls txt='$txt' vis=$vis left=$($rc.Left) top=$($rc.Top) right=$($rc.Right) bottom=$($rc.Bottom) width=$w height=$h")
    }
    return $true
}, [IntPtr]::Zero)

$script:results | ForEach-Object { Write-Host $_ }
Write-Host "Enumerated $($script:count) child windows"
