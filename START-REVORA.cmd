@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0scripts\Start-Revora.ps1" %*
set "REVORA_EXIT=%ERRORLEVEL%"
if not "%REVORA_EXIT%"=="0" echo Setup needs attention. Read docs\QUICKSTART.md and the message above.
pause
exit /b %REVORA_EXIT%
