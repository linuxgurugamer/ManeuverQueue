
rem @echo off

set H=R:\KSP_1.3.1_dev
set GAMEDIR=ManeuverQueue

echo %H%

copy /Y "%1%2" "GameData\%GAMEDIR%\Plugins"


rem mkdir "%H%\GameData\%GAMEDIR%"
xcopy  /E /y /i GameData\%GAMEDIR% "%H%\GameData\%GAMEDIR%"
