#requires -Version 5.1
[CmdletBinding()]
param([switch]$SkipPublish, [string]$Iscc, [string]$PayloadDir)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    # Prefer built-in 5.1 modules when the parent shell is PowerShell 7.
    $env:PSModulePath = "$PSHOME\Modules;" + [Environment]::GetEnvironmentVariable('PSModulePath','Machine')
}
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'meeting/TeamWeeklyMeeting.csproj'
$payload = if ($PayloadDir) { [IO.Path]::GetFullPath($PayloadDir) } else { Join-Path $root 'dist/app-web' }
$payload = $payload.TrimEnd([char[]]"\/")
[xml]$projectXml = Get-Content -LiteralPath $project
$version = [string]$projectXml.Project.PropertyGroup.Version

if (-not $SkipPublish) {
    & dotnet publish $project -c Release -r win-x64 --self-contained true `
        -p:BuildInstaller=false `
        -p:PublishSingleFile=false -o $payload
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
}
if (-not (Test-Path (Join-Path $payload 'TeamWeeklyMeeting.exe'))) { throw 'Application payload is missing' }
$privateFiles = Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object {
    $_.Name -in @('connection.json', 'connection.example.json', 'meeting.json', 'Cookies', 'MySqlConnector.dll', 'TaskCalendarWidget.dll') -or $_.Extension -eq '.log' -or $_.Name -match '\.log\.\d+$' -or $_.FullName -match '[\\/]WebView2[\\/]'
}
if ($privateFiles) { throw 'Private configuration/session/log files found in the payload. Use a clean publish directory.' }
Copy-Item -LiteralPath (Join-Path $root 'README.md'), (Join-Path $root 'LICENSE') -Destination $payload -Force
Copy-Item -LiteralPath (Join-Path $root 'meeting/DIAGNOSTICS.md') -Destination $payload -Force
foreach ($name in @('VALIDATION.md','CHANGELOG.md','PROVENANCE.md','VERSIONING.md')) {
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination $payload -Force
}
$userDocs = Join-Path $payload 'meeting'
New-Item -ItemType Directory -Path $userDocs -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'meeting/README.md'), (Join-Path $root 'meeting/DIAGNOSTICS.md') -Destination $userDocs -Force

$runtime = Join-Path $root 'redist/MicrosoftEdgeWebView2RuntimeInstallerX64.exe'
if (-not (Test-Path $runtime)) {
    New-Item -ItemType Directory -Path (Split-Path $runtime) -Force | Out-Null
    # Microsoft's official Evergreen standalone x64 distribution endpoint.
    Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/?linkid=2124701' -OutFile $runtime
}
$signature = Get-AuthenticodeSignature -LiteralPath $runtime
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*Microsoft Corporation*') {
    throw 'Microsoft WebView2 installer signature validation failed'
}
if (-not $Iscc) {
    $Iscc = @("$env:LOCALAPPDATA/Programs/Inno Setup 6/ISCC.exe", "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe", "$env:ProgramFiles/Inno Setup 6/ISCC.exe") |
        Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $Iscc) { $Iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
}
if (-not $Iscc) { throw 'Install Inno Setup 6 or pass -Iscc <path>' }
& $Iscc /Qp "/DMyAppVersion=$version" "/DAppPublishDir=$payload" (Join-Path $PSScriptRoot 'team-weekly-meeting.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
$output = Join-Path $root "dist/installer/NETCUS-TeamWeeklyMeeting-Setup-v$version-win-x64.exe"
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText("$output.sha256", "$hash  $([System.IO.Path]::GetFileName($output))`n", [System.Text.UTF8Encoding]::new($false))
Write-Output "Installer: $output"
