# QueryArmor SSMS 22 Installation Guide

Use this guide on any Windows laptop. The project can be in any folder because the helper scripts calculate paths relative to the `scripts` folder.

Microsoft documents SSMS command-line installation through Windows Package Manager (`winget install Microsoft.SQLServerManagementStudio.22`): https://learn.microsoft.com/en-us/ssms/install/command-line-parameters

## 1. Open Command Prompt in the Project Folder

Open `cmd.exe`, then go to the folder that contains `QueryArmor.slnx`.

```cmd
cd /d "<folder-that-contains-QueryArmor.slnx>"
dir /b QueryArmor.slnx QueryArmor\QueryArmor.csproj scripts\install-queryarmor.cmd
```

Expected output:

```text
QueryArmor.slnx
QueryArmor.csproj
install-queryarmor.cmd
```

If you do not see that output, find the correct folder:

```cmd
cd /d C:\
dir /s /b QueryArmor.slnx
```

Then run `cd /d` with the folder printed by the command.

## 2. Run Diagnostics First

```cmd
scripts\diagnose-queryarmor.cmd
```

Expected output should include:

```text
[QueryArmor] Diagnostics
FOUND: "<project-folder>\QueryArmor.slnx"
FOUND: "<project-folder>\QueryArmor\QueryArmor.csproj"
.NET SDK
FOUND: "<path-to-ssms-22>\ssms.exe"
FOUND: "<path-to-ssms-22>\VSIXInstaller.exe"
CLOSED: SSMS is not running.
```

If `.NET SDK` says `MISSING`, install the .NET SDK:

```cmd
winget install Microsoft.DotNet.SDK.10
dotnet --info
```

If `SSMS 22` says `MISSING`, install SSMS 22:

```cmd
winget install Microsoft.SQLServerManagementStudio.22
```

Then verify SSMS 22 exists:

```cmd
dir /b /s "%ProgramFiles%\Microsoft SQL Server Management Studio 22*\ssms.exe"
dir /b /s "%ProgramFiles(x86)%\Microsoft SQL Server Management Studio 22*\ssms.exe"
```

If `VSIXInstaller.exe` says `MISSING`, repair SSMS 22:

```cmd
winget repair Microsoft.SQLServerManagementStudio.22
```

Then verify the installer exists:

```cmd
dir /b /s "%ProgramFiles%\Microsoft SQL Server Management Studio 22*\VSIXInstaller.exe"
dir /b /s "%ProgramFiles(x86)%\Microsoft SQL Server Management Studio 22*\VSIXInstaller.exe"
```

If diagnostics says `RUNNING: Close SSMS before install/uninstall`, close SSMS. If it is stuck, save work first, then run:

```cmd
taskkill /IM Ssms.exe
```

## 3. Build the VSIX

```cmd
dotnet restore QueryArmor.slnx
dotnet build QueryArmor.slnx -c Release
```

Expected output:

```text
Determining projects to restore...
All projects are up-to-date for restore.
QueryArmor -> <project-folder>\QueryArmor\bin\Release\net48\QueryArmor.dll
QueryArmor -> <project-folder>\QueryArmor\bin\Release\net48\QueryArmor.vsix
Build succeeded.
```

If build fails, run the verbose check:

```cmd
dotnet build QueryArmor.slnx -c Release -v minimal
```

If `QueryArmor.vsix` is not created, check the output folder:

```cmd
dir /b QueryArmor\bin\Release\net48\QueryArmor.vsix
dir /s /b QueryArmor.vsix
```

## 4. Install QueryArmor

Close SSMS first, then run:

```cmd
scripts\install-queryarmor.cmd
```

Expected output:

```text
[QueryArmor] Install started
[1/6] Repository: "<project-folder>"
[2/6] Checking .NET SDK
dotnet: <version>
[3/6] Checking SSMS 22
SSMS: "<path-to-ssms-22>\ssms.exe"
[4/6] Checking VSIXInstaller.exe
VSIXInstaller: "<path-to-ssms-22>\VSIXInstaller.exe"
[5/6] Checking SSMS is closed
SSMS is closed.
[6/6] Building and installing QueryArmor
Build succeeded.
VSIXInstaller exit code: 0
SUCCESS: QueryArmor installation command completed.
```

The installer log is written here:

```text
%TEMP%\QueryArmor-vsix-install.log
```

Open the log:

```cmd
notepad "%TEMP%\QueryArmor-vsix-install.log"
```

If `VSIXInstaller exit code` is not `0`, search the log:

```cmd
findstr /i "error exception failed already" "%TEMP%\QueryArmor-vsix-install.log"
```

If the log says the extension is already installed, uninstall and install again:

```cmd
scripts\uninstall-queryarmor.cmd
scripts\install-queryarmor.cmd
```

If the script cannot find SSMS 22, run:

```cmd
scripts\diagnose-queryarmor.cmd
winget install Microsoft.SQLServerManagementStudio.22
```

If the script cannot find `VSIXInstaller.exe`, run:

```cmd
winget repair Microsoft.SQLServerManagementStudio.22
scripts\diagnose-queryarmor.cmd
```

## 5. Verify the Installed Files

```cmd
dir /b /s "%LOCALAPPDATA%\Microsoft\SSMS\QueryArmor.dll"
```

Expected output:

```text
<some-ssms-profile-folder>\Extensions\<extension-folder>\QueryArmor.dll
```

Also check the manifest:

```cmd
for /f "delims=" %i in ('dir /b /s "%LOCALAPPDATA%\Microsoft\SSMS\extension.vsixmanifest" 2^>nul') do @findstr /i "QueryArmor" "%i" && echo Manifest: %i
```

If no installed file is found, rerun install with log:

```cmd
scripts\install-queryarmor.cmd
notepad "%TEMP%\QueryArmor-vsix-install.log"
```

## 6. Test Inside SSMS

Open SQL Server Management Studio 22 and connect to any SQL Server.

Run this unsafe query in a test database only:

```sql
DELETE FROM dbo.SomeTable;
```

Expected result:

```text
QueryArmor shows a warning before the query runs.
```

Then run a safe query:

```sql
DELETE FROM dbo.SomeTable WHERE Id = 1;
```

Expected result:

```text
QueryArmor allows the query.
```

## 7. Uninstall QueryArmor

Close SSMS first, then run:

```cmd
scripts\uninstall-queryarmor.cmd
```

Expected output:

```text
[QueryArmor] Uninstall started
SSMS is closed.
VSIXInstaller: "<path-to-ssms-22>\VSIXInstaller.exe"
VSIXInstaller exit code: 0
DONE: Uninstall command completed.
```

If uninstall returns a non-zero exit code, check the log:

```cmd
notepad "%TEMP%\QueryArmor-vsix-uninstall.log"
findstr /i "error exception failed not installed" "%TEMP%\QueryArmor-vsix-uninstall.log"
```

If the extension was not installed on that laptop/profile, the non-zero uninstall result can be ignored.

## Quick Reinstall Command

Use this when updating an existing laptop:

```cmd
scripts\uninstall-queryarmor.cmd
scripts\install-queryarmor.cmd
```

## Manual VSIXInstaller Command

Use this only if the helper script is not allowed by company policy.

```cmd
set "VSIX=%CD%\QueryArmor\bin\Release\net48\QueryArmor.vsix"
for /f "delims=" %i in ('dir /b /s "%ProgramFiles%\Microsoft SQL Server Management Studio 22*\VSIXInstaller.exe" 2^>nul') do set "VSIXINSTALLER=%i"
for /f "delims=" %i in ('dir /b /s "%ProgramFiles(x86)%\Microsoft SQL Server Management Studio 22*\VSIXInstaller.exe" 2^>nul') do if not defined VSIXINSTALLER set "VSIXINSTALLER=%i"
"%VSIXINSTALLER%" /quiet /logFile:"%TEMP%\QueryArmor-vsix-install.log" "%VSIX%"
```

Expected output is usually empty for `/quiet`. Check the exit code:

```cmd
echo %ERRORLEVEL%
```

Expected output:

```text
0
```

If it is not `0`, open the log:

```cmd
notepad "%TEMP%\QueryArmor-vsix-install.log"
```

For a `.bat` file, replace `%i` with `%%i` in both `for /f` lines.
