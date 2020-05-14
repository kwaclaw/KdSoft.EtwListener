@echo off
echo This cleans up the output directories (bin, obj) of all projects in the solution.

set batdir=%~dp0
echo running from %batdir%

set doClean=n
set /p doClean=Proceed [y/n] (default n)?:

if /i not '%doClean%'=='y' exit

set CustomBeforeMicrosoftCommonTargets=%batdir%CleanUpBinObj.targets

set CustomBeforeMicrosoftCommonCrossTargetingTargets=%batdir%CleanUpBinObj.targets

msbuild.bat EtwListener.sln /t:CleanUpBinObj

pause
