@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"

echo [QueryArmor] Diagnostics
echo Repository: "%REPO_ROOT%"
echo.

echo [Project files]
if exist "%REPO_ROOT%\QueryArmor.slnx" (
  echo FOUND: "%REPO_ROOT%\QueryArmor.slnx"
) else (
  echo MISSING: "%REPO_ROOT%\QueryArmor.slnx"
)
if exist "%REPO_ROOT%\QueryArmor\QueryArmor.csproj" (
  echo FOUND: "%REPO_ROOT%\QueryArmor\QueryArmor.csproj"
) else (
  echo MISSING: "%REPO_ROOT%\QueryArmor\QueryArmor.csproj"
)
if exist "%REPO_ROOT%\QueryArmor\bin\Release\net48\QueryArmor.vsix" (
  echo FOUND: "%REPO_ROOT%\QueryArmor\bin\Release\net48\QueryArmor.vsix"
) else (
  echo MISSING: "%REPO_ROOT%\QueryArmor\bin\Release\net48\QueryArmor.vsix"
  echo FIX: dotnet build "%REPO_ROOT%\QueryArmor.slnx" -c Release
)
echo.

echo [.NET SDK]
where dotnet >nul 2>nul
if errorlevel 1 (
  echo MISSING: dotnet
  echo FIX: winget install Microsoft.DotNet.SDK.10
) else (
  where dotnet
  dotnet --version
)
echo.

echo [SSMS 22]
call :FindSSMS
if defined SSMS_EXE (
  echo FOUND: "%SSMS_EXE%"
) else (
  echo MISSING: SSMS 22
  echo FIX: winget install Microsoft.SQLServerManagementStudio.22
)
echo.

echo [VSIXInstaller.exe]
if defined SSMS_EXE call :FindVSIXInstaller
if defined VSIXINSTALLER (
  echo FOUND: "%VSIXINSTALLER%"
) else (
  echo MISSING: VSIXInstaller.exe
  echo CHECK: dir /b /s "%ProgramFiles%\Microsoft SQL Server Management Studio 22*\VSIXInstaller.exe"
)
echo.

echo [SSMS process]
tasklist /FI "IMAGENAME eq Ssms.exe" 2>nul | find /I "Ssms.exe" >nul
if not errorlevel 1 (
  echo RUNNING: Close SSMS before install/uninstall.
) else (
  echo CLOSED: SSMS is not running.
)
echo.

echo [Installed QueryArmor files]
set "FOUND_ANY="
if exist "%LOCALAPPDATA%\Microsoft\SSMS" (
  for /f "delims=" %%I in ('dir /b /s "%LOCALAPPDATA%\Microsoft\SSMS\QueryArmor.dll" 2^>nul') do (
    echo FOUND: "%%I"
    set "FOUND_ANY=1"
  )
)
if not defined FOUND_ANY (
  echo NOT FOUND under "%LOCALAPPDATA%\Microsoft\SSMS"
)

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
