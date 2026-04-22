@echo off
title LiveWallpaper — Console Log
cd /d "%~dp0"

echo ============================================================
echo  LiveWallpaper — running with console logging
echo  Log file: %~dp0livewallpaper.log
echo ============================================================
echo.

dotnet run --project LiveWallpaper.csproj -c Release

echo.
echo ============================================================
echo  Process exited.  Log saved to: %~dp0livewallpaper.log
echo ============================================================
pause
