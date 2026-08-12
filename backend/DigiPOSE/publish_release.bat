@echo off
echo ========================================================
echo [BUILD]: Publishing DigiPOSE Application (.NET 10.0 Release)
echo ========================================================

dotnet publish DigiPOSE.csproj -c Release -o ./bin/Release/net10.0/publish /p:IntermediateOutputPath=obj\RelBuild\

if %ERRORLEVEL% EQU 0 (
    echo.
    echo === [SUCCESS]: Application published successfully to ./bin/Release/net10.0/publish
) else (
    echo.
    echo === [ERROR]: Publish failed with error code %ERRORLEVEL%
)
pause
