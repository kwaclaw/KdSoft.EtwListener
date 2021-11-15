@echo off

REM switch to this batch file's directory (needed when running as administrator)
pushd "%~dp0"

@echo Installing KdSoft ETW Push Agent client certificate...

elevate -wait4exit certutil.exe -importpfx "%1"

if %errorlevel% equ 0 (@echo Finished installing certificate) ^
else (@echo Failed to install certificate, error: %errorlevel%)

pause