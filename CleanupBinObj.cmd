@echo off
echo This cleans up the output directories (bin, obj) of all projects in the solution.

set batdir=%~dp0
echo running from %batdir%

set doClean=n
set /p doClean=Proceed [y/n] (default n)?:

if /i not '%doClean%'=='y' exit


set msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"

set CustomBeforeMicrosoftCommonTargets=%batdir%CleanUpBinObj.targets

set CustomBeforeMicrosoftCommonCrossTargetingTargets=%batdir%CleanUpBinObj.targets

%msbuild% EtwListener.sln /t:CleanUpBinObj

pause
