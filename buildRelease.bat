@cd /D %~dp0
@call deploy.bat %1 %2

copy /Y "%GAMEDIR%.version" "GameData\%GAMEDIR%"
if exist "..\MiniAVC.dll" copy /Y "..\MiniAVC.dll" "GameData\%GAMEDIR%"

if "%LICENSE%" NEQ "" copy /y "%LICENSE%" "GameData\%GAMEDIR%"
if "%README%" NEQ "" copy /Y "%README%" "GameData\%GAMEDIR%"

rem Get Version info

rem The following requires the JQ program, available here: https://stedolan.github.io/jq/download/
%JQ%  ".VERSION.MAJOR" %VERSIONFILE% >tmpfile
set /P major=<tmpfile

%JQ%  ".VERSION.MINOR"  %VERSIONFILE% >tmpfile
set /P minor=<tmpfile

%JQ%  ".VERSION.PATCH"  %VERSIONFILE% >tmpfile
set /P patch=<tmpfile

%JQ%  ".VERSION.BUILD"  %VERSIONFILE% >tmpfile
set /P build=<tmpfile
del tmpfile
set VERSION=%major%.%minor%.%patch%
if "%build%" NEQ "0"  set VERSION=%VERSION%.%build%

echo Version:  %VERSION%


rem Build the zip FILE

set FILE="%RELEASEDIR%\%GAMEDIR%-%VERSION%.zip"
IF EXIST %FILE% del /F %FILE%
"%ZIP%" a -tzip "%FILE%" GameData

pause
