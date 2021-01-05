
REM switch to batch file directory (needed when running as administrator)
pushd "%~dp0"

REM %~1 removes surrounding quotes from first parameter;
REM use single quotes for PowerShell arguments conatining blanks
if [%1]==[] (set targetDir='C:\EtwEvents.Server') else (set targetDir='%~1')

REM use single quotes for PowerShell arguments conatining blanks
PowerShell .\CreateService.ps1 -sourceDir '.' -targetDir %targetDir% -file 'KdSoft.EtwEvents.Server.exe' -user .\LocalSystem

popd
pause