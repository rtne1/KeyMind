@echo off
setlocal EnableExtensions
pushd "%~dp0"
rem Running a second instance would silently keep the old global-hook process active.
tasklist /FI "IMAGENAME eq KeyMind.exe" 2>nul | find /I "KeyMind.exe" >nul
if not errorlevel 1 (
    echo.
    echo [ACTION REQUIRED] A previous KeyMind is still running.
    echo Right-click its icon beside the Windows clock and choose Exit.
    echo Then run START-KEYMIND.cmd again.
    echo.
    pause
    popd
    exit /b 3
)
rem Always rebuild. This prevents an old KeyMind.exe from silently running
rem after a new ZIP is extracted over an old installation.
call "Build-KeyMind.cmd"
if errorlevel 1 (
    echo [ERROR] Close older KeyMind copies from the clock tray and try again.
    popd
    exit /b 1
)
if not exist "data\en-frequency.txt" (
    echo [ERROR] The dictionary files are missing. Extract the entire ZIP.
    pause
    popd
    exit /b 2
)
start "" "%~dp0KeyMind.exe"
popd
exit /b 0
