
REM switch to batch file directory (needed when running as administrator)
pushd "%~dp0"

PowerShell .\CreateService.ps1 -file ".\publish\EtwEvents.Server.exe" -user .\LocalSystem

popd
pause