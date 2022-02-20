@cho off

REM switch to batch file directory (needed when running as administrator)
pushd "%~dp0\publish"

start /min KdSoft.EtwEvents.AgentManager.exe
start https://agent-manager.kd-soft.net:50300

popd

pause