# Release validation: 1.0.0

First public distribution baseline: 2026-10-23. Earlier development version numbers are archived in [development validation](docs/development/VALIDATION.md); they are not formal release numbers.

The application, embedded release notes, installer metadata, versioned shortcut icon, logs and Wiki use 1.0.0. No feature or authentication-storage format change is included in this version reset. The stable installer AppId and existing user-data directory are retained.

Validated on Windows on 2026-10-23; release finalization resumed on 2026-10-26.

- Release build: zero warnings/errors. Development and installed executables each passed 200 checks, including 67 authentication checks, 202 intercepted requests and 10 synthetic report POSTs.
- Version dialog visually verified: one expanded 1.0.0 entry with the first formal release notes. Application and installer metadata, shortcut icon filename and diagnostic version all use 1.0.0.
- One-command publish and installer generation completed. Replacement of the 1.2.1 development installation with formal 1.0.0 passed installed payload/icon hashes, shortcut target and uninstall registration checks. Uninstall removed the test app, registration and shortcut while preserving pre-existing settings and credentials.
- Earlier development source/tag/release history is preserved separately. The calendar repository is unchanged.

Live employee authentication remains an environment-specific acceptance check; automated authentication checks use synthetic responses in real WPF/WebView2.
