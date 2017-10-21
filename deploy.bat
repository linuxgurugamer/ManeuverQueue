@cd /D %~dp0
@call vars.bat

if not exist "GameData" mkdir GameData
if not exist "GameData\%GAMEDIR%" mkdir "GameData\%GAMEDIR%"
if not exist "GameData\%GAMEDIR%\Plugins" mkdir "GameData\%GAMEDIR%\Plugins"

if "%1" NEQ "" copy /Y "%1%2" "GameData\%GAMEDIR%\Plugins"
if "%1" EQU "" copy /Y "%GAMEDIR%\bin\Release\%GAMEDIR%.dll" "GameData\%GAMEDIR%\Plugins"
if not exist "%KerbalHive%\GameData\%GAMEDIR%" mkdir "%KerbalHive%\GameData\%GAMEDIR%"
robocopy /E GameData\%GAMEDIR% "%KerbalHive%\GameData\%GAMEDIR%"

exit /b 0
