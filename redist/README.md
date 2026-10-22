# WebView2 redistributable

The build script downloads Microsoft's signed Evergreen standalone x64 installer
and verifies its Authenticode signature before packaging it. The binary is ignored
by Git and included only in release installers. An existing installed runtime is
detected, so it is not reinstalled unnecessarily.

Official distribution documentation:
https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution

The final installer includes .NET and the offline WebView2 installer; a separate
runtime download during installation is not required. This does not replace the
company network/database access needed to use the app.
