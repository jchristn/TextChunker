@echo off
setlocal enabledelayedexpansion

rem ---------------------------------------------------------------------------
rem  publish-nuget.bat ^<NuGet-API-key^>
rem
rem  Packs every packable project in the solution in Release and publishes each
rem  package to nuget.org. The matching .snupkg symbol package is pushed
rem  automatically because it sits next to its .nupkg. Only projects with
rem  IsPackable=true produce packages (TextChunker and any other packable
rem  project); the test, console, benchmark, and evaluation projects are skipped.
rem
rem  The package version comes from project metadata (src\Directory.Build.props),
rem  so this always publishes the current version. Already-published versions are
rem  skipped rather than treated as errors, so the script is safe to re-run.
rem ---------------------------------------------------------------------------

if "%~1"=="" (
    echo Usage: publish-nuget.bat ^<NuGet-API-key^>
    exit /b 1
)

set "APIKEY=%~1"
set "SOLUTION=src\TextChunker.sln"
set "OUTPUT=artifacts"
set "SOURCE=https://api.nuget.org/v3/index.json"
set "CONFIG=Release"

pushd "%~dp0"

echo.
echo === Cleaning old packages in %OUTPUT% ===
if not exist "%OUTPUT%" mkdir "%OUTPUT%"
del /q "%OUTPUT%\*.nupkg" 2>nul
del /q "%OUTPUT%\*.snupkg" 2>nul

echo.
echo === Packing %SOLUTION% (%CONFIG%) ===
dotnet pack "%SOLUTION%" -c %CONFIG% -o "%OUTPUT%"
if errorlevel 1 goto :error

echo.
echo === Verifying packages were produced ===
if not exist "%OUTPUT%\*.nupkg" (
    echo No .nupkg files were produced in %OUTPUT%.
    goto :error
)

echo.
echo === Pushing packages and symbols to %SOURCE% ===
dotnet nuget push "%OUTPUT%\*.nupkg" --api-key %APIKEY% --source %SOURCE% --skip-duplicate
if errorlevel 1 goto :error

echo.
echo === Done. Packages and symbols published. ===
popd
endlocal
exit /b 0

:error
echo.
echo === Publish FAILED (exit code %errorlevel%). ===
popd
endlocal
exit /b 1
