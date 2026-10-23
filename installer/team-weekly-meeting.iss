#ifndef MyAppVersion
  #define MyAppVersion "1.1.1"
#endif
#ifndef AppPublishDir
  #define AppPublishDir "..\dist\app-web"
#endif
#define MyAppName "NETCUS 팀 주간보고"
#define MyAppExeName "TeamWeeklyMeeting.exe"
#define RuntimeFile "MicrosoftEdgeWebView2RuntimeInstallerX64.exe"

[Setup]
SourceDir={#SourcePath}
AppId={{8E79D3C7-6E94-4D72-8EBB-42BBD650F547}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=NETCUS
AppPublisherURL=https://github.com/Peace-Min/team-weekly-meeting
AppSupportURL=https://github.com/Peace-Min/team-weekly-meeting/issues
VersionInfoVersion={#MyAppVersion}.0
DefaultDirName={localappdata}\Programs\NETCUS\TeamWeeklyMeeting
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
SetupIconFile=..\meeting\Assets\company.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\dist\installer
OutputBaseFilename=NETCUS-TeamWeeklyMeeting-Setup-v{#MyAppVersion}-win-x64
; Keep the offline-runtime build usable on machines with limited free memory.
Compression=lzma2/fast
LZMANumBlockThreads=1
LZMADictionarySize=8192
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면 바로가기 만들기"; GroupDescription: "바로가기:"

[Files]
Source: "{#AppPublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\redist\{#RuntimeFile}"; Flags: dontcopy

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} 실행"; Flags: nowait postinstall skipifsilent

; Preferences, logs and sessions in APPDATA are preserved.
[Code]
function HasRuntime(Root: Integer): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(Root, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version)
    and (Version <> '') and (Version <> '0.0.0.0');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  if HasRuntime(HKLM64) or HasRuntime(HKLM32) or HasRuntime(HKCU) then Exit;
  ExtractTemporaryFile('{#RuntimeFile}');
  if not Exec(ExpandConstant('{tmp}\{#RuntimeFile}'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    Result := 'WebView2 설치기를 실행할 수 없습니다. 사내 IT 담당자에게 문의하세요.';
    Exit;
  end;
  Log(Format('WebView2 installer exit code: %d', [ExitCode]));
  if ExitCode = 3010 then NeedsRestart := True;
  if not (HasRuntime(HKLM64) or HasRuntime(HKLM32) or HasRuntime(HKCU)) then
    Result := 'WebView2 Runtime 설치를 확인하지 못했습니다. PC를 다시 시작하거나 사내 IT 담당자에게 문의하세요.';
end;
