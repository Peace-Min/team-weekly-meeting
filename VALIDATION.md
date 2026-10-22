# Release validation: 1.0.0

Validated on Windows on 2026-10-22.

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
