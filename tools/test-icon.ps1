#requires -Version 7
param([Parameter(Mandatory)][string]$ExePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('MeetingExeIcon' -as [type])) {
    Add-Type 'using System;using System.Runtime.InteropServices;public static class MeetingExeIcon{[DllImport("shell32.dll",CharSet=CharSet.Unicode)]public static extern uint ExtractIconEx(string path,int index,IntPtr[] large,IntPtr[] small,uint count);[DllImport("user32.dll")]public static extern bool DestroyIcon(IntPtr icon);}'
}
$paths=@((Resolve-Path -LiteralPath $ExePath).Path,(Resolve-Path "$PSScriptRoot/../meeting/Assets/company.ico").Path)
$bitmaps=@();$handles=@()
try {
    foreach($path in $paths){
        $large=[IntPtr[]]::new(1);$small=[IntPtr[]]::new(1)
        if([MeetingExeIcon]::ExtractIconEx($path,0,$large,$small,1) -eq 0 -or $large[0] -eq [IntPtr]::Zero -or $small[0] -eq [IntPtr]::Zero){throw "No embedded icon: $path"}
        foreach($handle in @($large[0],$small[0])){
            $handles+=$handle;$bitmaps+=[System.Drawing.Icon]::FromHandle($handle).ToBitmap()
        }
    }
    foreach($size in @(0,1)){
        $actual=$bitmaps[$size];$expected=$bitmaps[$size+2]
        if($actual.Size -ne $expected.Size){throw 'Embedded icon dimensions differ from source'}
        for($y=0;$y -lt $actual.Height;$y++){for($x=0;$x -lt $actual.Width;$x++){
            if($actual.GetPixel($x,$y).ToArgb() -ne $expected.GetPixel($x,$y).ToArgb()){throw 'Embedded EXE icon differs from current source'}
        }}
    }
    'EXE ICON PASS: small and large embedded icons match current source pixel for pixel'
} finally {
    foreach($bitmap in $bitmaps){$bitmap.Dispose()}
    foreach($handle in $handles){[void][MeetingExeIcon]::DestroyIcon($handle)}
}
