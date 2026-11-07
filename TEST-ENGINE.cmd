@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (echo Missing .NET Framework 4 compiler. & pause & exit /b 2)
"%CSC%" /nologo /target:exe /out:"KeyMind.Tests.exe" /codepage:65001 /reference:System.Core.dll /reference:System.Runtime.Serialization.dll /reference:System.Windows.Forms.dll "src\SettingsStore.cs" "src\Lexicon.cs" "src\WordConverter.cs" "src\KeyboardEngine.cs" "src\Program.TestStub.cs" "tests\SmokeTests.cs"
if errorlevel 1 (pause & exit /b 1)
"KeyMind.Tests.exe"
pause
