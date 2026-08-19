@echo off
setlocal EnableExtensions EnableDelayedExpansion

echo [QueryArmor] Update started

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"
set "INSTALL_SCRIPT=%SCRIPT_DIR%install-queryarmor.cmd"
set "UNINSTALL_SCRIPT=%SCRIPT_DIR%uninstall-queryarmor.cmd"

if not exist "%REPO_ROOT%\QueryArmor.slnx" (
  echo ERROR: QueryArmor.slnx was not found.
  echo FIX: Run this script from the extracted/cloned QueryArmor project, or keep it inside the scripts folder.
  exit /b 10
)

if not exist "%INSTALL_SCRIPT%" (
  echo ERROR: Install script was not found: "%INSTALL_SCRIPT%"
  exit /b 11
)

if not exist "%UNINSTALL_SCRIPT%" (
  echo ERROR: Uninstall script was not found: "%UNINSTALL_SCRIPT%"
  exit /b 12
)

echo [1/3] Closing check and uninstalling any existing QueryArmor/QueryGuard installation
call "%UNINSTALL_SCRIPT%"
set "UNINSTALL_EXIT=%ERRORLEVEL%"
if "%UNINSTALL_EXIT%"=="14" (
  echo ERROR: Update stopped because SSMS is running.
  echo FIX: Close all SSMS windows, then rerun this script.
  exit /b 14
)
if not "%UNINSTALL_EXIT%"=="0" (
  echo WARNING: Uninstall step returned exit code %UNINSTALL_EXIT%.
  echo Continuing with install because the extension may not have been installed on this laptop.
)

echo [2/3] Installing latest QueryArmor build
call "%INSTALL_SCRIPT%"
set "INSTALL_EXIT=%ERRORLEVEL%"
if not "%INSTALL_EXIT%"=="0" (
  echo ERROR: Update failed during install. Exit code: %INSTALL_EXIT%
  echo CHECK: notepad "%TEMP%\QueryArmor-vsix-install.log"
  exit /b %INSTALL_EXIT%
)

echo [3/3] Update complete
echo SUCCESS: QueryArmor is updated to the latest local build.
echo NEXT: Open SSMS 22 and test with a safe and unsafe query.
exit /b 0
