# NETCUS 팀 주간보고

사내 근태관리의 팀원별 최신 주간보고를 한 번에 준비하고, 회의에서 발표자별로 넘겨 보는 Windows 앱입니다.
로그인한 사내 웹페이지의 주간보고 조직 메뉴에서 조직과 사용자 정보를 읽습니다. DB에 접속하지 않으며 캘린더 앱 없이 독립적으로 빌드하고 실행합니다.

## 설치와 실행

1. [Releases](https://github.com/Peace-Min/team-weekly-meeting/releases)에서 `NETCUS-TeamWeeklyMeeting-Setup-v1.0.0-win-x64.exe`를 내려받아 설치합니다.
2. 앱을 실행해 사내 계정으로 로그인합니다. 로그인한 페이지에서 조직도와 구성원을 자동으로 읽습니다.
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
- 공통 로컬 설정 저장. 계정별 설정은 사용하지 않습니다.
- `%APPDATA%\TeamWeeklyMeeting\logs`의 순환 로그와 상단 **문의 및 버그제보**.
- 원본 회사 로고, 투명 배경 아이콘, WPF 작업표시줄 아이콘 축소 문제 수정.

자세한 사용법은 [앱 안내](meeting/README.md), 로그 분석 절차는 [DIAGNOSTICS](meeting/DIAGNOSTICS.md)를 참고하세요.
로그에는 사내 인명·조직 정보가 포함되므로 공개 GitHub 이슈에 올리지 말고 앱의 문의처로 전달하세요.
설치·제거 시 `%APPDATA%\TeamWeeklyMeeting`의 설정·로그·로그인 세션은 보존합니다.

## 개발과 배포 빌드

필요 도구: Windows, .NET SDK 9.0.319, PowerShell 7, Inno Setup 6.

```powershell
dotnet build meeting/TeamWeeklyMeeting.csproj -c Release
dotnet run --project meeting/TeamWeeklyMeeting.csproj -- --smoke-test
Get-Content "$env:TEMP/TeamWeeklyMeeting-smoke-result.txt"
pwsh -File tools/build-meeting-icon.ps1
pwsh -File installer/build-installer.ps1
pwsh -File tools/test-release.ps1
```

자동 QA는 로컬 합성 조직도·인증·보고서로 실행하며 사내 DB와 사이트에 접속하지 않습니다.
빌드 시 NuGet과 Microsoft WebView2 배포 서버에 접속합니다. WebView2 재배포 파일의 Microsoft 서명을 확인합니다.
산출물은 `dist/installer/`의 설치 EXE와 SHA-256 파일입니다. 빌드 폴더나 설치기에 사용자 설정·로그·브라우저 세션을 넣지 마세요.

기존 캘린더에서 가져온 코드와 로고 출처는 [PROVENANCE](PROVENANCE.md), 변경 내역은 [CHANGELOG](CHANGELOG.md)에 기록했습니다.
릴리스의 실제 설치·제거 및 아이콘·기능 검증 결과는 [VALIDATION](VALIDATION.md)에 있습니다.
