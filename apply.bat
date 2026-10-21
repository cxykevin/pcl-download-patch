@echo off
setlocal
set "HERE=%~dp0"
set "PCLDIR=%HERE%"
if exist "%HERE%..\pcl.exe" set "PCLDIR=%HERE%..\"

if not exist "%PCLDIR%pcl.exe" (
  echo [!] pcl.exe not found. Put the pcl-download-patch folder next to pcl.exe.
  pause
  exit /b 1
)
if not exist "%HERE%bin\ManualPatcher.exe" (
  echo [!] bin\ManualPatcher.exe not found.
  pause
  exit /b 1
)

if not exist "%PCLDIR%pcl.exe.bak" copy /y "%PCLDIR%pcl.exe" "%PCLDIR%pcl.exe.bak" >nul

"%HERE%bin\ManualPatcher.exe" "%PCLDIR%pcl.exe" "%PCLDIR%pcl.exe.patched"
if errorlevel 1 (
  echo [!] patch failed, pcl.exe was NOT modified.
  pause
  exit /b 1
)

copy /y "%PCLDIR%pcl.exe.patched" "%PCLDIR%pcl.exe" >nul
del /q "%PCLDIR%pcl.exe.patched" >nul 2>nul

echo [OK] patched. original backup: pcl.exe.bak
pause
