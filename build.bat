@echo off
where dotnet >nul 2>&1
if errorlevel 1 (echo dotnet not found & pause & exit /b 1)

dotnet build LiveWallpaper.csproj -c Release
if errorlevel 1 (pause & exit /b 1)

if exist libmpv-2.dll (
    copy /Y libmpv-2.dll bin\Release\net9.0-windows\win-x64\libmpv-2.dll >nul
)

echo.
echo OK: bin\Release\net9.0-windows\win-x64\LiveWallpaper.exe
pause
