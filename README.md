# NETCUS 팀 주간보고

사내 근태관리의 팀원별 최신 주간보고를 한 번에 준비하고, 회의에서 발표자별로 넘겨 보는 Windows 앱입니다.
로그인한 사내 웹페이지의 주간보고 조직 메뉴에서 조직과 사용자 정보를 읽습니다. DB에 접속하지 않으며 캘린더 앱 없이 독립적으로 빌드하고 실행합니다.

## 설치와 실행

1. 아래 명령으로 이 PC에서 앱과 설치 패키지를 함께 빌드합니다. 생성한 설치 EXE를 폐쇄망 PC로 반입해 설치합니다. GitHub Releases 다운로드는 필요하지 않습니다.
2. 앱을 실행해 사내 계정으로 로그인합니다. 개인 PC에서는 로그인창의 **자동로그인**을 선택할 수 있습니다. 로그인한 페이지에서 조직도와 구성원을 자동으로 읽습니다.
3. 팀과 오늘 참석자를 체크한 뒤 **참석자 보고서 준비**를 누릅니다.
4. **보고 시작**으로 전체 화면 발표를 시작합니다. 방향키·PageUp/PageDown으로 발표자를 바꾸고, 드래그·휠로 본문을 스크롤합니다. **Esc**로 종료합니다.

Windows 10 1809 이상 또는 Windows 11의 x64 환경을 대상으로 합니다.
.NET 런타임과 WebView2 오프라인 설치기가 설치 파일에 포함되어 있습니다.
프로그램 자체에는 코드 서명이 적용되지 않았으므로 Windows에서 게시자 확인 경고가 나올 수 있습니다.
사내 사이트에 접속할 수 있는 네트워크와 본인의 사내 웹 계정이 필요합니다.

## 조직도 출처

로그인 직후 사내 페이지의 **주간보고 메뉴(`#submenu1`)**를 읽어 조직명과 사용자 링크의 ID·이름·직급을 추출합니다.
사이트의 본부/하위 팀 구분과 표시 순서를 따르며, 조직 제목 다음의 인력은 해당 조직에 배정합니다.
반복되는 조직·사용자는 합치고, 중복된 근태 메뉴와 외부 링크는 제외합니다.
조직 메뉴를 확인하지 못하면 오류를 표시하며 DB나 고정 명단으로 대체하지 않습니다.
로그인 계정이 바뀌면 새 로그인 페이지에서 다시 읽습니다. DB 설정이나 MySQL 드라이버는 필요하지 않습니다.
공개 소스와 설치 파일에는 직원 명부, 실제 보고서, 쿠키와 사용자 로그를 포함하지 않습니다.

## 기능과 진단

- 팀과 하위 조직의 참석자 체크, 상위 조직 직속 인력 우선 표시.
- 원본 보고서 디자인 유지, 내용 영역만 표시, 25~500% 확대·축소와 증감 폭 설정.
- 발표자별 스크롤 유지, 창 분리, 전체 화면 발표.
- 발표 중 펜·형광펜·획 지우개, 색상·굵기, 되돌리기·전체 지우기와 접을 수 있는 도구막대.
- 앱 상단 **버전 정보**에서 현재 버전·변경 요약 확인. 이전 버전은 펼쳐서 읽습니다.
- 공통 로컬 설정 저장. 계정별 설정은 사용하지 않습니다.
- 선택한 경우에만 인증정보를 Windows 사용자별로 암호화 저장합니다. 자동 시도는 실행당 1회·20초 제한이며 상단에서 로그아웃할 수 있습니다.
- `%APPDATA%\TeamWeeklyMeeting\logs`의 순환 로그와 상단 **문의 및 버그제보**.
- 원본 회사 로고, 투명 배경 아이콘, WPF 작업표시줄 아이콘 축소 문제 수정.

자세한 사용법은 [앱 안내](meeting/README.md), 로그 분석 절차는 [DIAGNOSTICS](meeting/DIAGNOSTICS.md)를 참고하세요.
로그에는 사내 인명·조직 정보가 포함되므로 공개 GitHub 이슈에 올리지 말고 앱의 문의처로 전달하세요.
설치·제거 시 `%APPDATA%\TeamWeeklyMeeting`의 설정·로그·로그인 세션은 보존합니다.
인증정보도 지우려면 제거 전에 **로그아웃**하세요. 자동로그인은 공용 PC에서 사용하지 마세요.

## 개발과 배포 빌드

필요 도구: Windows, .NET 9 SDK, Inno Setup 6. 설치기 생성에는 Windows 기본 PowerShell 5.1을 사용합니다. `global.json`은 설치된 .NET 9 SDK 중 최신 버전을 사용하므로 9.0.301·9.0.315 등으로도 빌드할 수 있습니다.

저장소 루트의 **`build-installer.cmd`를 더블클릭**하면 앱 publish 후 Inno Setup으로 설치 패키지까지 자동 생성합니다. 완료 또는 오류 메시지를 확인할 수 있도록 창을 유지합니다.

저장소 루트에서 **이 명령 한 번으로 앱 publish와 Inno Setup 설치기 생성이 모두 실행됩니다.**

```powershell
dotnet publish meeting/TeamWeeklyMeeting.csproj -c Release -r win-x64 -o publish/win-x64 --self-contained true
```

앱 파일은 `publish/win-x64/`, 설치 패키지는 `dist/installer/NETCUS-TeamWeeklyMeeting-Setup-v<버전>-win-x64.exe`에 생성됩니다.
이 PC에서 빌드한 설치 EXE만 폐쇄망에 반입하면 됩니다. .NET과 WebView2 런타임을 포함하므로 설치 대상 PC에서 별도 다운로드하지 않습니다.
소스 빌드 PC는 NuGet 패키지 복원이 가능해야 하며, WebView2 오프라인 설치기가 없으면 Microsoft 배포 서버에서 한 번 받습니다. 저장소에 분할 파일이나 Git LFS 파일을 올리지 않습니다.

개발·QA용 명령:

```powershell
dotnet build meeting/TeamWeeklyMeeting.csproj -c Release
dotnet run --project meeting/TeamWeeklyMeeting.csproj -- --smoke-test
Get-Content "$env:TEMP/TeamWeeklyMeeting-smoke-result.txt"
pwsh -File tools/build-meeting-icon.ps1
pwsh -File tools/test-release.ps1 -ExePath publish/win-x64/TeamWeeklyMeeting.exe
```

자동 QA는 로컬 합성 조직도·인증·보고서로 실행하며 사내 DB와 사이트에 접속하지 않습니다.
설치기를 따로 다시 만들 때는 `powershell -File installer/build-installer.ps1 -SkipPublish -PayloadDir publish/win-x64`를 사용합니다. 앱만 publish하려면 위 명령에 `-p:BuildInstaller=false`를 추가합니다. QA 도구는 PowerShell 7을 사용합니다.
WebView2 재배포 파일의 Microsoft 서명을 확인합니다.
산출물은 `dist/installer/`의 설치 EXE와 SHA-256 파일입니다. 빌드 폴더나 설치기에 사용자 설정·로그·브라우저 세션을 넣지 마세요.

기존 캘린더에서 가져온 코드와 로고 출처는 [PROVENANCE](PROVENANCE.md), 변경 내역은 [CHANGELOG](CHANGELOG.md)에 기록했습니다.
릴리스의 실제 설치·제거 및 아이콘·기능 검증 결과는 [VALIDATION](VALIDATION.md)에 있습니다.
다음 버전의 번호·변경 내역·태그 관리 절차는 [VERSIONING](VERSIONING.md)에 있습니다.
자동로그인 사용법·오류 대응·인증 QA는 [Wiki](docs/wiki/Home.md)에 있습니다. 소스 clone과 설치본에도 함께 포함됩니다.

설치 바로가기 아이콘은 버전별 ICO 파일을 사용하고 설치 완료 시 Windows에 갱신을 요청합니다. 이전에 작업표시줄에 고정한 항목만 옛 아이콘으로 남으면 고정을 해제하고 새 시작 메뉴 바로가기에서 다시 고정하세요.
