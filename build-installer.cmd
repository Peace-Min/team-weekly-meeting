@echo off
setlocal
cd /d "%~dp0"
dotnet publish meeting/TeamWeeklyMeeting.csproj -c Release -r win-x64 -o publish/win-x64 --self-contained true
if errorlevel 1 (
  echo.
  echo Build failed. Check the error above.
  pause
  exit /b 1
)
echo.
echo Installer created in: %~dp0dist\installer
pause
exit /b 0
