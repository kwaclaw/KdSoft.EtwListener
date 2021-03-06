@echo off
Setlocal enabledelayedexpansion

set -installdir=
set -url=
for %%a in (%*) do (
    call set "%%~1=%%~2"
    shift
)

set defaultDir="C:\Program Files\Kd-Soft\EtwEvents.PushAgent"

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

rem prompt for MANAGER URL if not provided as input argument

rem need internal default value, to avoid an error when the user does not provide input
set managerUrl=##none##
if [%-url%]==[] (
    set /p managerUrl=Manager URL:
)
rem remove quotes
set managerUrl=%managerUrl:"=%
rem if not provided, use default install directory
if [%managerUrl%]==[##none##] (
    @echo One can enter the Manager URL later in 'appSettings.Local.json'.
)

@echo :'%targetDir%'
@echo :'%managerUrl%'

pause