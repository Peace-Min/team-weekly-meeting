# 배포 검증: 1.1.0

2026-11-09 Windows x64에서 검증했습니다.

발표자 표시명을 16px와 진한 글씨로 바꾼 v1.1.0 설치본을 다시 검증했습니다. 이전 v1.1.0 위에 덮어쓴 뒤 설치된 앱 DLL의 해시가 새 빌드와 일치했고, 206개 검사(로컬 응답 201건, 모의 보고서 POST 10회)가 통과했습니다. 제거 뒤 기존 설정도 보존됐습니다.

- `build-installer.cmd`로 앱과 설치파일을 생성했습니다. 앱과 설치파일의 버전은 1.1.0이며, 설치파일 SHA-256이 함께 생성한 체크섬과 일치했습니다.
- 정식 v1.0.0 위에 v1.1.0을 설치해 실행 파일과 아이콘 해시, 바로가기, 제거 등록을 확인했습니다. 시험용 앱을 제거한 뒤 기존 설정이 보존됐습니다.
- 설치된 앱에서 실제 WPF와 WebView2로 206개 검사가 통과했습니다. 로컬 응답 202건, 모의 보고서 POST 10회입니다. 발표자 이동의 세부 내용은 [Wiki](docs/wiki/Presenter-Navigation.md)에 있습니다.
- 실제 사내 계정과 네트워크는 모의 환경 검증에 포함되지 않았습니다.

## 이전 배포 검증: 1.0.0

First public distribution baseline: 2026-10-23. Earlier development version numbers are archived in [development validation](docs/development/VALIDATION.md); they are not formal release numbers.

The application, embedded release notes, installer metadata, versioned shortcut icon, logs and Wiki use 1.0.0. No feature or authentication-storage format change is included in this version reset. The stable installer AppId and existing user-data directory are retained.

Validated on Windows on 2026-10-23; release finalization resumed on 2026-10-26.

- Release build: zero warnings/errors. Development and installed executables each passed 200 checks, including 67 authentication checks, 202 intercepted requests and 10 synthetic report POSTs.
- Version dialog visually verified: one expanded 1.0.0 entry with the first formal release notes. Application and installer metadata, shortcut icon filename and diagnostic version all use 1.0.0.
- One-command publish and installer generation completed. Replacement of the 1.2.1 development installation with formal 1.0.0 passed installed payload/icon hashes, shortcut target and uninstall registration checks. Uninstall removed the test app, registration and shortcut while preserving pre-existing settings and credentials.
- Earlier development source/tag/release history is preserved separately. The calendar repository is unchanged.

Live employee authentication remains an environment-specific acceptance check; automated authentication checks use synthetic responses in real WPF/WebView2.
