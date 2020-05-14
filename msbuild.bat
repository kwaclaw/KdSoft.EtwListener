@echo off
setlocal enabledelayedexpansion

set path=%path%;%ProgramFiles(x86)%\Microsoft Visual Studio\Installer

for /f "usebackq tokens=*" %%i in (`vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
  "%%i" %*
  exit /b !errorlevel!
)