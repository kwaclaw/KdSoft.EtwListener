@echo off
Setlocal enabledelayedexpansion

rem switch to location of this script
CD /D "%~dp0"

rem default Parameters
set -installdir=
set -url=

rem %~1 removes surrounding quotes from first parameter
set -installdir=
set -url=
for %%a in (%*) do (
    call set "%%~1=%%~2"
    shift
)

set defaultDir="%ProgramFiles%\Kd-Soft\EtwEvents.AgentManager"

rem prompt for INSTALL DIRECTORY if not provided as input argument

rem need internal default value, to avoid an error when the user does not provide input
set targetDir=##none##
if [%-installdir%]==[] (
    set /p targetDir=Install directory ^(Enter for '%defaultDir:"=%'^):
)
rem remove quotes
set targetDir=%targetDir:"=%
rem if not provided, use default install directory
if [%targetDir%]==[##none##] (set targetDir=%defaultDir:"=%)

REM use single quotes for PowerShell arguments containing blanks
PowerShell -ExecutionPolicy Bypass .\InstallService.ps1 -sourceDir . -targetDir '%targetDir%' ^
    -file 'KdSoft.EtwEvents.AgentManager.exe' -port 50300

pause