# 배포 전 개발 기록

아래 번호는 개발 중 사용한 번호이며 정식 배포 버전이 아닙니다. 정식 배포는 2026-10-23의 v1.0.0부터 시작합니다.

# Release validation: 1.2.1

Validated on Windows on 2026-10-23.

- Release build: zero warnings/errors. Actual WPF/WebView2 fixture suite: **200 checks passed**, including **67 authentication checks**; 203 intercepted requests and 10 synthetic report POSTs. Without private HTML input, 198 checks run.
- Regression fixture now uses native `form.submit()`, masks the password, returns an intermediate RSA HTML page, and redirects to the authenticated home page after 150ms. Manual opt-in retains the original password until verified identity; automatic login follows the redirect. Actual rejected forms still discard the pending credential.
- Login shell appears before cleanup and page loading; closing it during loading cancels safely and the next login succeeds. Logout plus prompt clears site data exactly once. Download cache remains enabled. Already-verified authentication reads the current page instead of reloading the home page.
- Calendar login/cache logic was inspected read-only. Reused final authenticated-page verification, submit detection, browser/cache reuse, and credential readback. Its local identity-only startup cannot supply this app's current website organization menu; no calendar DB, credential file or browser profile dependency was introduced.
- No-submit failures return in under 5 seconds in local QA, with no authentication POST. A submitted-but-stalled flow still enforces the actual 20-second total deadline. No raw credential, encrypted password field or cookie appears in diagnostic logs.
- Sanitized user-log analysis: site navigation about 0.02–1.62s; duplicate app cleanup about 7.3s during logout; false credential-capture warning acknowledged after about 9–26s. Browser initialization could consume up to about 5.44s, but v1.2.0 cannot split that stage further. New environment/core/cleanup/window/page-ready diagnostics separate future reports. Raw user logs and staff information are not committed.
- Scope: synthetic website responses in actual WebView2, not live employee authentication. Website/network latency and cold browser startup still depend on the user's environment; local fixture timings are not a production performance promise.
- One-command publish/installer generation completed. Actual isolated upgrade from 1.2.0 to 1.2.1 passed payload/icon/shortcut/uninstall registration checks; the installed executable passed the same 200 checks (203 intercepted requests, 10 report POSTs). Uninstall removed its app/registration/shortcut and preserved pre-existing settings and credentials. The calendar repository remained unchanged.

## Previous release: 1.2.0

Validated on Windows on 2026-10-23. Work order: design specification → implementation → QA → Wiki.

- Release build: zero warnings/errors. Real WPF/WebView2 fixture suite: **191 checks passed**, including **58 authentication checks**; 173 intercepted requests and 10 synthetic report POSTs. Without the private page HTML, 189 checks run.
- Verified opt-in only, pre-RSA password capture, DPAPI-protected file, session identity matching, expired session authentication, per-run attempt limit, actual 20-second timeout, rejected/locked/disabled/alert/mismatched account, HTTP 503/502, native WebView2 canceled navigation, file corruption/invalid/null data/wrong protection context/IO failures, manual recovery, logout and account isolation, absence of secrets in diagnostic logs.
- Wiki: `docs/wiki/Home.md`, usage/storage policy, troubleshooting, and per-case QA evidence. Included in source clones and installers.
- Scope: synthetic authentication responses, not live employee logins. A different Windows user session was not available; wrong-protection-context rejection was tested. Live RSA/server integration and physical network interruption remain environment-specific acceptance checks.

## Previous release: 1.1.2

Validated on Windows on 2026-10-23.

- Published and installed EXE icons match the current source ICO pixel for pixel at native small and large sizes. The running window still fills its 32px icon with transparent corners.
- Real upgrade from the generated 1.1.1 installer to 1.1.2: installed EXE matches the publish payload; versioned ICO matches the source hash; the existing Start menu shortcut changes to the new ICO path; uninstall registration uses that same icon.
- Installer requests an Explorer association/icon refresh, without deleting the user's icon cache or restarting Explorer.
- Upgraded installed app: **133 checks passed** with the private fixture, 84 intercepted requests and 10 report POSTs. One earlier run failed the existing CDP mouse eraser check; the same installer passed on rerun without product changes. Icon/shortcut checks passed in both runs.
- Uninstall removed the isolated app, shortcut and registration; pre-existing settings remained unchanged.
- Reproduce installation/shortcut QA with `pwsh -File tools/test-installer.ps1`; optionally pass `-PreviousInstaller <older-setup.exe>` to verify an actual upgrade. QA refuses to replace an existing registered installation or Start menu shortcut.

## Previous release: 1.1.1

Validated on Windows on 2026-10-23.

- Release build: zero warnings and errors; **133 checks passed** with the private HTML fixture (131 without it), 84 intercepted requests and 10 report POSTs.
- Version information: exact header button label, current release expanded, previous releases collapsed and expandable; compact dialog visually inspected.
- Login: original 180px/60px/120px table structure reproduced; password label stays on one line, input width remains 140px. Form handlers and authentication are preserved.
- Report title: fixture includes the original fixed-width image structure; the title image fills the report viewport while original report HTML and styles remain preserved.

## Previous release: 1.1.0

Validated on Windows on 2026-10-23.

- Release build: zero warnings and errors.
- Real WebView2 smoke QA with the privately supplied page HTML: **132 checks passed**, 84 intercepted requests and 10 synthetic report POSTs. Without the private fixture, 130 checks run. No live site or database access.
- Version button opens the embedded current and previous release history. The newest entry matches the assembly version.
- Actual mouse/keyboard input verifies pen, highlighter, colors, width, whole-stroke eraser, undo, clear, toolbar collapse, scroll alignment and presenter isolation. Temporary drawings survive presenter navigation and disappear after report refresh.
- Presentation disables native browser zoom gestures, restores them on exit, and keeps the annotation toolbar usable at 200% report zoom. Escape hides drawing tools and exits presentation.
- Native window icon audit: transparent corners and filled 32px canvas, preserving the NETCUS mark with a report badge.

## Previous release: 1.0.0

Validated on Windows on 2026-10-22.

- `build-installer.cmd` was executed end to end: publish succeeded, Inno Setup created the installer, and the batch printed the output directory and exited successfully.
- SDK compatibility: the fixed 9.0.319 SDK requirement was replaced by selection of installed stable .NET 9 SDKs. A separate SDK 9.0.302 build completed with zero warnings/errors; the default environment selected 9.0.319 successfully.

- One-command packaging: `dotnet publish meeting/TeamWeeklyMeeting.csproj -c Release -r win-x64 -o publish/win-x64 --self-contained true` exited successfully after creating both the app and installer. The Inno Setup hook ran exactly once through Windows PowerShell 5.1.
- The generated installer used the selected `publish/win-x64` payload, verified by installed file hashes and the updated packaged documentation. Its installed app passed 110 checks (including the private HTML fixture), the native icon audit and uninstall/settings-preservation checks.

- Independent .NET 9 Release build: no warnings or errors.
- Synthetic WebView2 smoke QA plus the privately supplied page HTML: **110 checks passed**, 80 intercepted requests, 9 synthetic report POSTs. No live site/DB access in this suite. Without the optional private HTML fixture, 108 checks run.
- Runtime taskbar audit: the logo fills **32×30px of the 32×32px native icon**, with transparent corners. This checks the running window's `WM_GETICON` result, not just the ICO file.
- Organization extraction: actual browser DOM parser reads the logged-in weekly menu; hierarchy, title/member association, repeated sections, duplicate users, external links, missing menus and login forms are covered. Changes to page user IDs are reflected without any fixed roster/DB fallback. The privately supplied site HTML is tested locally and not committed.
- Real installer QA in an isolated temporary installation directory: application, self-contained .NET and documentation installed successfully. The installed EXE matched the published payload; no MySQL driver or DB configuration was installed.
- The installed app passed the synthetic page-menu checks and native icon audit.
- Real uninstall QA: application and uninstall registration removed; existing local preferences remained unchanged.
- Public export audit: compiled DB credentials removed; user settings, logs, sessions, local work and generated binaries excluded from Git. The application assembly was checked for the old deployment secret.
- Bundled WebView2 standalone installer: valid Microsoft Authenticode signature verified before packaging.

Reproduce the app/native-icon checks with:

```powershell
pwsh -File tools/test-release.ps1
# Or point at an installed copy:
pwsh -File tools/test-release.ps1 -ExePath 'C:\path\to\TeamWeeklyMeeting.exe'
```

The app and installer are unsigned. Live employee logins and network access remain
environment-specific. Organization data comes from the authenticated page, with no DB dependency.
