#requires -Version 7
[CmdletBinding()]
param([string]$ExePath = "$PSScriptRoot/../publish/win-x64/TeamWeeklyMeeting.exe")
$ErrorActionPreference = 'Stop'
$dependencyFile = [System.IO.Path]::ChangeExtension((Resolve-Path -LiteralPath $ExePath).Path, '.deps.json')
if ((Get-Content -LiteralPath $dependencyFile -Raw) -match 'MySqlConnector|TaskCalendarWidget') { throw 'Unexpected database/calendar assembly dependency' }
Write-Output 'DEPENDENCY PASS: no MySQL driver or calendar assembly'
& "$PSScriptRoot/test-icon.ps1" -ExePath $ExePath
Add-Type -AssemblyName System.Drawing
if (-not ('MeetingIconAudit' -as [type])) {
    Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class MeetingIconAudit { [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l); }'
}
$qaProcess = Start-Process -FilePath (Resolve-Path -LiteralPath $ExePath).Path -ArgumentList '--smoke-test' -WindowStyle Hidden -PassThru
$captured = $false
for ($attempt=0; $attempt -lt 200; $attempt++) {
    $qaProcess.Refresh()
    if ($qaProcess.HasExited) { break }
    if ($qaProcess.MainWindowHandle -ne [IntPtr]::Zero) {
        $handle = [MeetingIconAudit]::SendMessage($qaProcess.MainWindowHandle, 0x7F, [IntPtr]1, [IntPtr]::Zero)
        if ($handle -ne [IntPtr]::Zero) {
            $bitmap = [System.Drawing.Icon]::FromHandle($handle).ToBitmap()
            try {
                $left=$bitmap.Width; $right=-1; $top=$bitmap.Height; $bottom=-1
                for ($y=0; $y -lt $bitmap.Height; $y++) {
                    for ($x=0; $x -lt $bitmap.Width; $x++) {
                        if ($bitmap.GetPixel($x,$y).A -gt 16) {
                            $left=[Math]::Min($left,$x); $right=[Math]::Max($right,$x)
                            $top=[Math]::Min($top,$y); $bottom=[Math]::Max($bottom,$y)
                        }
                    }
                }
                if (($right-$left+1) -lt $bitmap.Width*.90 -or ($bottom-$top+1) -lt $bitmap.Height*.85) { throw 'Taskbar logo is undersized' }
                if ($bitmap.GetPixel(0,0).A -ne 0) { throw 'Taskbar background is not transparent' }
                Write-Output "LIVE ICON PASS: $($right-$left+1)x$($bottom-$top+1) inside $($bitmap.Width)x$($bitmap.Height), transparent background"
                $captured=$true
            } finally { $bitmap.Dispose() }
            break
        }
    }
    Start-Sleep -Milliseconds 50
}
if (-not $qaProcess.WaitForExit(120000)) { $qaProcess.Kill(); throw 'Smoke test timed out' }
if (-not $captured) { throw 'Could not inspect the running window icon' }
$result = Get-Content -LiteralPath "$env:TEMP/TeamWeeklyMeeting-smoke-result.txt" -Raw
if ($qaProcess.ExitCode -ne 0 -or $result -notmatch '^PASS:') { throw $result }
Write-Output $result.Trim()
