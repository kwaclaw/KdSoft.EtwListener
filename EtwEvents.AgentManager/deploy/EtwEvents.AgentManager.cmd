@cho off

REM switch to batch file directory (needed when running as administrator)
pushd "%~dp0\publish"

start /min KdSoft.EtwEvents.AgentManager.exe
start https://localhost:5299

popd