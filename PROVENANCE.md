# Source and assets

The weekly meeting app was extracted from the local `meeting/` companion of
[Peace-Min/task-calendar](https://github.com/Peace-Min/task-calendar).
The calendar repository's base commit was `4c1aa4fc9bac1abf870dd3df08a537e9ab2d39c4`.
Companion changes made before extraction are included in the initial commit here.

- `integration/NetcusText.cs`, `DbGate.cs`, `DbFault.cs`: existing calendar modules.
- `integration/DbErrors.cs`: existing error handling with the unrelated calendar-only exception branch removed.
- `integration/ProjectDb.cs`: only the existing read-only organization query, its helpers and schema gate connection are retained. Calendar write/session modules are not included.
- `integration/DeployConfig.cs`: local JSON/environment configuration replaces compiled database credentials.
- `meeting/Assets/company-logo-source.pdf`: company logo supplied by the project owner.
- `meeting/Assets/company-logo.jpg`: the original embedded image extracted without modification from that PDF.
- `tools/build-meeting-icon.ps1`: crops the NET symbol, removes the white matte for Windows icons, and creates nine ICO resolutions plus the full-size WPF icon source. The white header logo is preserved separately.

Code retains the original MIT license. The company logo and name identify NETCUS;
the software license does not grant trademark rights.

Installers are built using [Inno Setup](https://jrsoftware.org/), copyright Jordan Russell and Martijn Laan; its notices remain embedded in the generated installer. Microsoft .NET and WebView2 redistributables retain their own licenses and notices.
