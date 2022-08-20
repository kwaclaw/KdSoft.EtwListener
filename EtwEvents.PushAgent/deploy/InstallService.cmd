@echo off

rem switch to location of this script
CD /D "%~dp0"

.\tools\Elevate.exe .\_performInstall.cmd
