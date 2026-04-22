@echo off
setlocal
title LiveWallpaper -- Build Installer
color 0A

echo.
echo  ================================================
echo   LiveWallpaper  --  Build Installer
echo  ================================================
echo.

:: ── Step 1: Publish (self-contained single file) ─────────────────────────────
echo  [1/2]  Publishing self-contained exe...
echo.

dotnet publish -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo  ERROR: dotnet publish failed. Make sure .NET 9 SDK is installed.
    echo  Download: https://dot.net/
    pause
    exit /b 1
)

echo.
echo  [1/2]  Publish OK

:: ── Step 2: Compile Inno Setup installer ─────────────────────────────────────
echo.
echo  [2/2]  Compiling installer with Inno Setup...
echo.

:: Try common Inno Setup install locations
set ISCC=
if exist "%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe" (
    set ISCC="%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe"
)
if exist "C:\Program Files\Inno Setup 7\ISCC.exe" (
    set ISCC="C:\Program Files\Inno Setup 7\ISCC.exe"
)

if "%ISCC%"=="" (
    echo  ERROR: Inno Setup 7 not found.
    echo  Download: https://jrsoftware.org/isinfo.php
    echo.
    echo  After installing, re-run this script.
    pause
    exit /b 1
)

mkdir installer 2>nul
%ISCC% setup.iss

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo  ERROR: Inno Setup compilation failed.
    pause
    exit /b 1
)

echo.
echo  ================================================
echo   Done!
echo   Installer  -->  installer\LiveWallpaper_Setup_v3.0.exe
echo  ================================================
echo.
pause
