@echo off
setlocal EnableExtensions
pushd "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
 echo.
 echo [ERROR] .NET Framework C# compiler is not installed on this Windows PC.
 echo Enable .NET Framework 4.8 in Windows Features, then run this file again.
 echo.
 pause
 popd
 exit /b 2
)
echo ============================================
echo   KeyMind 3.0 - Windows local build
echo ============================================
echo.
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 /win32icon:"assets\keymind.ico" /out:"KeyMind.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Runtime.Serialization.dll "src\Program.cs" "src\SettingsStore.cs" "src\Lexicon.cs" "src\WordConverter.cs" "src\KeyboardEngine.cs" "src\MainForm.cs" > "Build-Log.txt" 2>&1
if errorlevel 1 (
 echo [ERROR] Build failed. See Build-Log.txt. Error detail:
 type "Build-Log.txt"
 pause
 popd
 exit /b 1
)
if not exist "KeyMind.exe" (
 echo [ERROR] No executable was generated.
 pause
 popd
 exit /b 1
)
echo [OK] KeyMind.exe was created successfully.
popd
exit /b 0
