@echo off
setlocal EnableExtensions EnableDelayedExpansion

echo [QueryArmor] Install started

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"
set "SOLUTION=%REPO_ROOT%\QueryArmor.slnx"
set "VSIX=%REPO_ROOT%\QueryArmor\bin\Release\net48\QueryArmor.vsix"
set "LOG=%TEMP%\QueryArmor-vsix-install.log"

echo [1/6] Repository: "%REPO_ROOT%"
if not exist "%SOLUTION%" (
  echo ERROR: QueryArmor.slnx was not found.
  echo FIX: Run this script from the extracted/cloned QueryArmor project, or move the scripts folder back under the project root.
  exit /b 10
)

echo [2/6] Checking .NET SDK
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet was not found.
  echo FIX: Install the .NET SDK, then reopen Command Prompt.
  echo CMD: winget install Microsoft.DotNet.SDK.10
  exit /b 11
)
for /f "delims=" %%I in ('dotnet --version') do echo dotnet: %%I

echo [3/6] Checking SSMS 22
call :FindSSMS
if not defined SSMS_EXE (
  echo ERROR: SQL Server Management Studio 22 was not found.
  echo FIX: Install SSMS 22, then rerun this script.
  echo CMD: winget install Microsoft.SQLServerManagementStudio.22
  echo CHECK: dir /b /s "%ProgramFiles%\Microsoft SQL Server Management Studio 22*\ssms.exe"
  exit /b 12
)
echo SSMS: "%SSMS_EXE%"

echo [4/6] Checking VSIXInstaller.exe
call :FindVSIXInstaller
if not defined VSIXINSTALLER (
  echo ERROR: VSIXInstaller.exe was not found inside the SSMS 22 install folder.
  echo FIX: Repair or reinstall SSMS 22.
  echo CMD: winget repair Microsoft.SQLServerManagementStudio.22
  echo CHECK: dir /b /s "%ProgramFiles%\Microsoft SQL Server Management Studio 22*\VSIXInstaller.exe"
  exit /b 13
)
echo VSIXInstaller: "%VSIXINSTALLER%"

echo [5/6] Checking SSMS is closed
tasklist /FI "IMAGENAME eq Ssms.exe" 2>nul | find /I "Ssms.exe" >nul
if not errorlevel 1 (
  echo ERROR: SSMS is running.
  echo FIX: Close all SSMS windows, then rerun this script.
  echo CHECK: tasklist /FI "IMAGENAME eq Ssms.exe"
  exit /b 14
)
echo SSMS is closed.

echo [6/6] Building and installing QueryArmor
pushd "%REPO_ROOT%" >nul
dotnet restore QueryArmor.slnx
if errorlevel 1 (
  popd >nul
  echo ERROR: Restore failed.
  echo FIX: Check internet access and NuGet package restore.
  echo CMD: dotnet restore QueryArmor.slnx -v minimal
  exit /b 15
)

dotnet build QueryArmor.slnx -c Release
if errorlevel 1 (
  popd >nul
  echo ERROR: Build failed.
  echo FIX: Run this command to see full compiler output.
  echo CMD: dotnet build QueryArmor.slnx -c Release -v minimal
  exit /b 16
)
popd >nul

if not exist "%VSIX%" (
  echo ERROR: Build succeeded, but QueryArmor.vsix was not created.
  echo FIX: Check the VSIX project output path.
  echo CHECK: dir /b /s "%REPO_ROOT%\QueryArmor.vsix"
  exit /b 17
)
echo VSIX: "%VSIX%"

"%VSIXINSTALLER%" /quiet /logFile:"%LOG%" "%VSIX%"
set "INSTALL_EXIT=%ERRORLEVEL%"
echo VSIXInstaller exit code: %INSTALL_EXIT%
echo Log: "%LOG%"

if not "%INSTALL_EXIT%"=="0" (
  echo ERROR: VSIX installation failed.
  echo FIX: Open the log and search for ERROR or Exception.
  echo CMD: notepad "%LOG%"
  echo If it says the extension is already installed, run:
  echo CMD: "%REPO_ROOT%\scripts\uninstall-queryarmor.cmd"
  echo Then rerun:
  echo CMD: "%REPO_ROOT%\scripts\install-queryarmor.cmd"
  exit /b %INSTALL_EXIT%
)

call :FindInstalledExtension
if defined INSTALLED_DLL (
  echo Installed file: "%INSTALLED_DLL%"
) else (
  echo WARNING: Installer exited with 0, but QueryArmor.dll was not found under %%LOCALAPPDATA%%\Microsoft\SSMS.
  echo CHECK: dir /b /s "%LOCALAPPDATA%\Microsoft\SSMS\QueryArmor.dll"
)

echo SUCCESS: QueryArmor installation command completed.
echo NEXT: Open SSMS 22 and run: DELETE FROM dbo.SomeTable;
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

:FindInstalledExtension
set "INSTALLED_DLL="
if exist "%LOCALAPPDATA%\Microsoft\SSMS" (
  for /f "delims=" %%I in ('dir /b /s "%LOCALAPPDATA%\Microsoft\SSMS\QueryArmor.dll" 2^>nul') do if not defined INSTALLED_DLL set "INSTALLED_DLL=%%I"
)
exit /b 0
