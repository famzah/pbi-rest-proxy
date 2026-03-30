@echo off
setlocal EnableExtensions

set "ROOT_DIR=%~dp0"
set "PROJECT_FILE=%ROOT_DIR%src\pbi-dax-proxy.csproj"
set "ARTIFACTS_DIR=%ROOT_DIR%artifacts"
set "PUBLISH_ROOT=%ARTIFACTS_DIR%\publish"
set "CONFIGURATION=Release"
set "RUNTIME=win-x64"

for /f %%i in ('powershell -NoProfile -Command "(Get-Date).ToString('yyyyMMdd-HHmmss')"') do set "BUILD_SUFFIX=%%i"
for /f %%i in ('powershell -NoProfile -Command "(Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')"') do set "BUILD_TIMESTAMP_UTC=%%i"
for /f %%i in ('powershell -NoProfile -Command "(Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz')"') do set "BUILD_TIMESTAMP_LOCAL=%%i"
for /f %%i in ('powershell -NoProfile -Command "([xml](Get-Content '%PROJECT_FILE%')).Project.PropertyGroup.Version"') do set "APP_VERSION=%%i"

if not defined APP_VERSION set "APP_VERSION=1.0.0"

set "BUILD_VERSION=%APP_VERSION%+build.%BUILD_SUFFIX%"
set "PUBLISH_DIR=%PUBLISH_ROOT%\%BUILD_SUFFIX%"
set "PUBLISHED_EXE=%PUBLISH_DIR%\pbi-dax-proxy.exe"
set "ARTIFACT_ZIP=%ARTIFACTS_DIR%\pbi-dax-proxy-%BUILD_SUFFIX%.zip"

if not exist "%ARTIFACTS_DIR%" mkdir "%ARTIFACTS_DIR%"
if not exist "%PUBLISH_ROOT%" mkdir "%PUBLISH_ROOT%"
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"

echo Publishing pbi-dax-proxy %APP_VERSION%...

dotnet publish "%PROJECT_FILE%" ^
  -c %CONFIGURATION% ^
  -r %RUNTIME% ^
  --self-contained false ^
  -o "%PUBLISH_DIR%" ^
  /p:InformationalVersion=%BUILD_VERSION% ^
  /p:BuildVersion=%BUILD_VERSION% ^
  /p:BuildTimestampUtc="%BUILD_TIMESTAMP_UTC%" ^
  /p:BuildTimestampLocal="%BUILD_TIMESTAMP_LOCAL%" ^
  /p:BuildArtifactSuffix=%BUILD_SUFFIX%

if errorlevel 1 goto :error

if exist "%ARTIFACT_ZIP%" del "%ARTIFACT_ZIP%"
powershell -NoProfile -Command "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%ARTIFACT_ZIP%' -CompressionLevel Optimal"
if errorlevel 1 goto :error

echo.
echo Build version: %BUILD_VERSION%
echo Build timestamp local: %BUILD_TIMESTAMP_LOCAL%
echo Build timestamp UTC:   %BUILD_TIMESTAMP_UTC%
echo Published files:       %PUBLISH_DIR%
echo Published exe:         %PUBLISHED_EXE%
echo Artifact zip:          %ARTIFACT_ZIP%
exit /b 0

:error
echo.
echo Build failed.
exit /b 1
