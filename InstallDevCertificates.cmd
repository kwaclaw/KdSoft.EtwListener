@echo off
Setlocal enabledelayedexpansion

rem switch to location of this script
CD /D "%~dp0"

.\EtwEvents.PushAgent\deploy\tools\elevate.exe -c PowerShell -ExecutionPolicy Bypass .\InstallDevCertificates.ps1
