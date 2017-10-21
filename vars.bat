@echo off
cd /D %~dp0

set GAMEDIR=ManeuverQueue
set LICENSE=LICENSE.txt
set README=ReadMe.md
set VERSIONFILE=%GAMEDIR%.version

if not defined RELEASEDIR	set RELEASEDIR=d:\Users\jbb\release
if not defined ZIP			set ZIP=%ProgramFiles%\7-Zip\7z.exe
if not defined JQ			set JQ=c:\local\jq-win64


if defined KerbalHive goto definedKerbalHive
if exist "R:\KSP_1.3.1_dev" set "KerbalHive=R:\KSP_1.3.1_dev"
if not defined KerbalHive set "KerbalHive=%ProgramFiles(x86)%\Steam\steamapps\common\Kerbal Space Program"
if exist "%KerbalHive%\KSP.exe" (
	echo Exporting KerbalHive="%KerbalHive%"
	setx KerbalHive "%KerbalHive%"
	echo.
	goto haveKerbalHive
)
:definedKerbalHive
if exist "%KerbalHive%\KSP.exe" goto haveKerbalHive
echo KerbalHive="%KerbalHive%\KSP.exe" does not exist
exit /b 1
:haveKerbalHive
echo KerbalHive: %KerbalHive%

if exist "%RELEASEDIR%" goto haveReleaseDir
set "RELEASEDIR=%repos%"
if exist "%RELEASEDIR%" goto haveReleaseDir
set "RELEASEDIR=c:\devel"
if exist "%RELEASEDIR%" goto haveReleaseDir
echo RELEASEDIR="%RELEASEDIR%" does not exist
exit /b 1
:haveReleaseDir
echo Release Directory: %RELEASEDIR%

echo ZIP="%ZIP%"
if exist "%ZIP%" goto haveZip
if exist "%ProgramFiles(x86)%\7-zip\7z.exe" set ZIP=%ProgramFiles(x86)%\7-zip\7z.exe
if exist "%ZIP%" goto haveZip
if exist "C:\Program Files\7-zip\7z.exe" set ZIP=C:\Program Files\7-zip\7z.exe
if exist "%ZIP%" goto haveZip
echo ZIP="%ZIP%" does not exist
exit /b 1
:haveZip
echo 7zip: %ZIP%

if exist "%JQ%" goto haveJQ
if exist "c:\devel\utils\jq-win64.exe" set JQ=c:\devel\utils\jq-win64.exe
if exist "%JQ%" goto haveJQ
echo JQ="%JQ%" does not exist
exit /b 1
:haveJQ
echo JQ: %JQ%

