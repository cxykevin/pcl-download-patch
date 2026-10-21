@echo off
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [!] csc.exe not found: %CSC%
  pause
  exit /b 1
)
"%CSC%" /nologo /out:bin\ManualPatcher.exe /r:bin\Mono.Cecil.dll src\ManualPatcher.cs
"%CSC%" /nologo /out:bin\VerifyIL.exe /r:bin\Mono.Cecil.dll src\VerifyIL.cs
echo.
echo [OK] rebuilt bin\ManualPatcher.exe and bin\VerifyIL.exe
pause
