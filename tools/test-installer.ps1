#requires -Version 7
[CmdletBinding()]
param([string]$PreviousInstaller)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$registration='HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/{8E79D3C7-6E94-4D72-8EBB-42BBD650F547}_is1'
if(Test-Path $registration){throw 'An installed copy exists; isolated QA will not overwrite it'}
$runId=[Guid]::NewGuid().ToString('N')
$qaDir=[IO.Path]::GetFullPath((Join-Path $env:TEMP "TeamWeeklyMeeting-install-qa-$runId"))
if(-not $qaDir.StartsWith([IO.Path]::GetFullPath($env:TEMP),[StringComparison]::OrdinalIgnoreCase)){throw 'QA path escaped TEMP'}
$group='NETCUS 팀 주간보고'
$shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) "$group/NETCUS 팀 주간보고.lnk"
if(Test-Path $shortcut){throw 'An existing app shortcut exists; QA will not overwrite it'}
$settingsBefore=@{}
foreach($name in @('connection.json','meeting.json','autologin.dat')){
    $path=Join-Path $env:APPDATA "TeamWeeklyMeeting/$name"
    if(Test-Path $path){$settingsBefore[$path]=(Get-FileHash -LiteralPath $path).Hash}
}
$project=[xml](Get-Content (Join-Path $root 'meeting/TeamWeeklyMeeting.csproj') -Raw)
$version=[string]$project.Project.PropertyGroup.Version
$installer=Join-Path $root "dist/installer/NETCUS-TeamWeeklyMeeting-Setup-v$version-win-x64.exe"
function Install([string]$Path,[string]$Suffix){
    $log=Join-Path $env:TEMP "TeamWeeklyMeeting-install-$runId-$Suffix.log"
    $process=Start-Process -FilePath $Path -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/TASKS=',"/DIR=`"$qaDir`"","/GROUP=`"$group`"","/LOG=`"$log`"") -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(45000)){throw 'Installer timed out'}
    if($process.ExitCode -ne 0){throw "Installer exit: $($process.ExitCode)"}
}
try {
    $shell=New-Object -ComObject WScript.Shell
    $oldIcon=$null
    if($PreviousInstaller){
        Install (Resolve-Path -LiteralPath $PreviousInstaller).Path 'previous'
        if(-not (Test-Path $shortcut)){throw 'Previous installer did not create a shortcut'}
        $oldIcon=$shell.CreateShortcut($shortcut).IconLocation
    }
    Install $installer 'current'
    $installedExe=Join-Path $qaDir 'TeamWeeklyMeeting.exe'
    $iconPath=Join-Path $qaDir "Assets/TeamWeeklyMeeting-$version.ico"
    foreach($name in @('TeamWeeklyMeeting.exe','coreclr.dll','README.md','DIAGNOSTICS.md','unins000.exe')){
        if(-not(Test-Path (Join-Path $qaDir $name))){throw "Missing installed file: $name"}
    }
    foreach($name in @('connection.json','connection.example.json','meeting.json','autologin.dat','autologin.dat.tmp','TaskCalendarWidget.dll','MySqlConnector.dll')){
        if(Test-Path (Join-Path $qaDir $name)){throw "Unexpected private/legacy file: $name"}
    }
    if((Get-FileHash $installedExe).Hash -ne (Get-FileHash (Join-Path $root 'publish/win-x64/TeamWeeklyMeeting.exe')).Hash){throw 'Installed EXE differs from payload'}
    if((Get-FileHash $iconPath).Hash -ne (Get-FileHash (Join-Path $root 'meeting/Assets/company.ico')).Hash){throw 'Installed shortcut icon differs from source'}
    $link=$shell.CreateShortcut($shortcut)
    if($link.TargetPath -ne $installedExe -or $link.IconLocation -ne "$iconPath,0"){throw "Unexpected shortcut target/icon: $($link.TargetPath); $($link.IconLocation)"}
    if($PreviousInstaller -and $oldIcon -eq $link.IconLocation){throw 'Upgrade reused the old icon cache path'}
    if((Get-ItemProperty $registration).DisplayIcon -notlike "$iconPath*"){throw 'Uninstall icon does not use versioned icon'}
    'INSTALL/SHORTCUT PASS: payload hash, versioned icon bytes, shortcut target and uninstall icon'
    if($PreviousInstaller){'UPGRADE PASS: existing shortcut now references a different icon path'}
    & (Join-Path $PSScriptRoot 'test-release.ps1') -ExePath $installedExe
} finally {
    $uninstaller=Join-Path $qaDir 'unins000.exe'
    if(Test-Path $uninstaller){
        $process=Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit(45000)){throw 'Uninstaller timed out'}
        for($attempt=0;$attempt -lt 100 -and (Test-Path $uninstaller);$attempt++){Start-Sleep -Milliseconds 100}
    }
    if((Test-Path (Join-Path $qaDir 'TeamWeeklyMeeting.exe')) -or (Test-Path $registration) -or (Test-Path $shortcut)){throw 'Uninstall did not remove application/registration/shortcut'}
    foreach($path in $settingsBefore.Keys){if((Get-FileHash -LiteralPath $path).Hash -ne $settingsBefore[$path]){throw "User settings changed: $path"}}
    'UNINSTALL PASS: application, registration and shortcut removed; existing settings preserved'
}
