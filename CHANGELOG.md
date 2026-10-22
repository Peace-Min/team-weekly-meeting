# Changelog

## Build workflow

- One `dotnet publish ... --self-contained true` command now builds the app and invokes Inno Setup automatically.
- The selected publish directory is passed to the installer; direct script builds suppress the hook to avoid recursion.
- Distribution uses the locally generated setup EXE for closed-network import. No split runtime files or Git LFS are used.

## 1.0.0

- Independent Windows app with automatic login and organization refresh.
- Team hierarchy, attendance checkboxes and ordered weekly report preparation.
- Original report styling, responsive content, zoom and detached windows.
- Full-screen presentation with keyboard navigation, mouse drag/wheel and Esc.
- Shared local preferences, rotating diagnostic logs and contact dialog.
- Company logo and transparent taskbar icon that fills the native icon area.
- Organization and users extracted from the authenticated weekly-report page; no DB connection, driver or configuration.
- Public source without user data or browser sessions.
- Per-user Windows x64 installer with self-contained .NET and offline WebView2 runtime.
