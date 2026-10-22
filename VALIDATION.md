# Release validation: 1.0.0

Validated on Windows on 2026-10-22.

- Independent .NET 9 Release build: no warnings or errors.
- Synthetic WebView2 smoke QA: **102 checks passed**, 80 intercepted requests, 9 synthetic report POSTs. No live site/DB access in this suite.
- Runtime taskbar audit: the logo fills **32×30px of the 32×32px native icon**, with transparent corners. This checks the running window's `WM_GETICON` result, not just the ICO file.
- Read-only integration comparison: the separated roster query and the original calendar module returned identical payloads against the existing DB, including 12 organization units. No DB writes were performed; no real roster payload is committed.
- Real installer QA in an isolated temporary installation directory: application, self-contained .NET, configuration example and documentation installed successfully. The installed EXE matched the published payload.
- The installed app passed the same 102 checks and native icon audit.
- Real uninstall QA: application and uninstall registration removed; existing local connection/preferences files remained unchanged.
- Public export audit: compiled DB credentials removed; user settings, logs, sessions, local work and generated binaries excluded from Git. The application assembly was checked for the old deployment secret.
- Bundled WebView2 standalone installer: valid Microsoft Authenticode signature verified before packaging.

Reproduce the app/native-icon checks with:

```powershell
pwsh -File tools/test-release.ps1
# Or point at an installed copy:
pwsh -File tools/test-release.ps1 -ExePath 'C:\path\to\TeamWeeklyMeeting.exe'
```

The app and installer are unsigned. Live employee logins and network access remain
environment-specific; the public release supplies no company credentials.
