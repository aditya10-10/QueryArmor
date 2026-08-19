@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "EXTENSION_ID=QueryArmor.SSMS.810f8a7d-a567-40e9-913f-d63cb272f93f"
set "LOG=%TEMP%\QueryArmor-vsix-uninstall.log"

echo [QueryArmor] Uninstall started

echo [1/3] Checking SSMS is closed
tasklist /FI "IMAGENAME eq Ssms.exe" 2>nul | find /I "Ssms.exe" >nul
if not errorlevel 1 (
  echo ERROR: SSMS is running.
  echo FIX: Close all SSMS windows, then rerun this script.
  exit /b 14
)
echo SSMS is closed.

echo [2/3] Locating SSMS 22 VSIXInstaller.exe
call :FindSSMS
if not defined SSMS_EXE (
  echo ERROR: SQL Server Management Studio 22 was not found.
  echo FIX: Install or repair SSMS 22.
  echo CMD: winget install Microsoft.SQLServerManagementStudio.22
  exit /b 12
)
call :FindVSIXInstaller
if not defined VSIXINSTALLER (
  echo ERROR: VSIXInstaller.exe was not found.
  echo FIX: Repair or reinstall SSMS 22.
  echo CMD: winget repair Microsoft.SQLServerManagementStudio.22
  exit /b 13
)
echo VSIXInstaller: "%VSIXINSTALLER%"

echo [3/3] Uninstalling QueryArmor
"%VSIXINSTALLER%" /quiet /logFile:"%LOG%" /uninstall:%EXTENSION_ID%
set "UNINSTALL_EXIT=%ERRORLEVEL%"
echo VSIXInstaller exit code: %UNINSTALL_EXIT%
echo Log: "%LOG%"

if not "%UNINSTALL_EXIT%"=="0" (
  echo WARNING: Uninstall returned a non-zero exit code.
  echo This can happen if QueryArmor is not installed for this SSMS profile.
  echo CHECK: notepad "%LOG%"
)

echo DONE: Uninstall command completed.
exit /b 0

:FindSSMS
set "SSMS_EXE="
for %%R in ("%ProgramFiles%" "%ProgramFiles(x86)%") do (
  if not "%%~R"=="" if exist "%%~R" (
    for /f "delims=" %%I in ('dir /b /s "%%~R\Microsoft SQL Server Management Studio 22\ssms.exe" 2^>nul') do if not defined SSMS_EXE set "SSMS_EXE=%%I"
    for /f "delims=" %%I in ('dir /b /s "%%~R\Microsoft SQL Server Management Studio 22*\ssms.exe" 2^>nul') do if not defined SSMS_EXE set "SSMS_EXE=%%I"
  )
)
exit /b 0

:FindVSIXInstaller
if defined VSIXINSTALLER if exist "%VSIXINSTALLER%" exit /b 0
set "VSIXINSTALLER="
for %%I in ("%SSMS_EXE%") do set "SSMS_DIR=%%~dpI"
if exist "!SSMS_DIR!VSIXInstaller.exe" set "VSIXINSTALLER=!SSMS_DIR!VSIXInstaller.exe"
for %%I in ("!SSMS_DIR!..") do set "SSMS_ROOT=%%~fI"
for /f "delims=" %%I in ('dir /b /s "!SSMS_ROOT!\VSIXInstaller.exe" 2^>nul') do if not defined VSIXINSTALLER set "VSIXINSTALLER=%%I"
for %%R in ("%ProgramFiles%" "%ProgramFiles(x86)%") do (
  if not "%%~R"=="" if exist "%%~R" (
    for /f "delims=" %%I in ('dir /b /s "%%~R\Microsoft SQL Server Management Studio 22*\VSIXInstaller.exe" 2^>nul') do if not defined VSIXINSTALLER set "VSIXINSTALLER=%%I"
  )
)
exit /b 0
