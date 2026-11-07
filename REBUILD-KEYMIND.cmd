@echo off
cd /d "%~dp0"
if exist "KeyMind.exe" del /f /q "KeyMind.exe"
call "Build-KeyMind.cmd"
if errorlevel 1 exit /b 1
start "" "%~dp0KeyMind.exe"
