# QueryGuard for SSMS

QueryGuard is a SQL Server Management Studio extension that warns before running risky `UPDATE` or `DELETE` queries.

It blocks statements like:

```sql
DELETE FROM dbo.Users;
UPDATE dbo.Users SET IsActive = 0;
```

It allows statements that include a real filter:

```sql
DELETE FROM dbo.Users WHERE Id = 10;
UPDATE dbo.Users SET IsActive = 0 WHERE Id = 10;
```

The SQL check uses Microsoft ScriptDom, so comments, string literals, batches, subqueries, and CTEs are handled more safely than with regex-only parsing.

## Project Structure

```text
D:\SSMSExtension
|-- SSMSExtension.slnx
|-- SSMSExtension
|   |-- SSMSExtension.csproj
|   |-- source.extension.vsixmanifest
|   |-- QueryGuard.pkgdef
|   |-- src
|       |-- QueryGuardPackage.cs
|       |-- CommandInterceptor.cs
|       |-- Core
|       |   |-- SqlQueryAnalyzer.cs
|       |   |-- QueryAutoFixer.cs
|       |   |-- AnalysisResult.cs
|       |-- Config
|       |   |-- GuardConfiguration.cs
|       |-- Logging
|       |   |-- AuditLogger.cs
|       |-- UI
|           |-- BlockingWarningDialog.cs
|           |-- StatusBarNotifier.cs
|-- SSMSExtension.Tests
    |-- SqlQueryAnalyzerTests.cs
```

## Important Files

`SSMSExtension/src/QueryGuardPackage.cs`

Main extension entry point. SSMS loads this package when the extension starts.

`SSMSExtension/src/CommandInterceptor.cs`

Hooks into the SSMS command system. When the user runs a query, this class gets the active SQL text, analyzes it, and blocks or allows execution.

`SSMSExtension/src/Core/SqlQueryAnalyzer.cs`

Core safety checker. It parses SQL using `Microsoft.SqlServer.TransactSql.ScriptDom` and detects unsafe `UPDATE` or `DELETE` statements without a `WHERE` clause.

`SSMSExtension/src/Core/QueryAutoFixer.cs`

Suggests simple fixes, such as uncommenting a commented `WHERE` clause or adding a placeholder `WHERE`.

`SSMSExtension/src/Core/AnalysisResult.cs`

Shared result model for risks, violations, statement type, table name, and messages.

`SSMSExtension/src/UI/BlockingWarningDialog.cs`

Warning dialog shown when a risky query is detected.

`SSMSExtension/src/Config/GuardConfiguration.cs`

Local configuration. On first run, QueryGuard creates:

```text
%APPDATA%\QueryGuard\config.json
```

Default audit log:

```text
%APPDATA%\QueryGuard\audit.log
```

`SSMSExtension.Tests/SqlQueryAnalyzerTests.cs`

Unit tests for safe and unsafe SQL detection.

## Requirements

For normal use:

- SQL Server Management Studio 22
- .NET Framework 4.8

For development:

- Visual Studio 2022 or newer, with extension development tools
- .NET SDK
- SSMS 22 installed locally

This project targets:

```text
net48
```

## Build Locally

Open PowerShell in the repo root:

```powershell
cd D:\SSMSExtension
dotnet build SSMSExtension.slnx
```

The VSIX file is created here:

```text
D:\SSMSExtension\SSMSExtension\bin\Debug\net48\SSMSExtension.vsix
```

## Run Tests

```powershell
cd D:\SSMSExtension
dotnet test SSMSExtension.Tests\SSMSExtension.Tests.csproj
```

## Install Locally in SSMS 22

Close SSMS first.

Then run:

```powershell
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /quiet "D:\SSMSExtension\SSMSExtension\bin\Debug\net48\SSMSExtension.vsix"
```

The extension is installed under a folder like:

```text
%LOCALAPPDATA%\Microsoft\SSMS\22.0_d5c13cd0\Extensions\<random-folder>\
```

The random folder name is normal. VSIX installer creates it.

## Check Install Log

To create a log during install:

```powershell
$log = Join-Path $env:TEMP "QueryGuard-vsix-install.log"
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /quiet /logFile:$log "D:\SSMSExtension\SSMSExtension\bin\Debug\net48\SSMSExtension.vsix"
notepad $log
```

Look for a line like:

```text
Install to SQL Server Management Studio 22 completed successfully.
```

## Uninstall Locally

Close SSMS first.

Use the same VSIX installer:

```powershell
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /quiet /uninstall:QueryGuard.SSMS.810f8a7d-a567-40e9-913f-d63cb272f93f
```

If you are reinstalling the same version, running the install command again usually upgrades/replaces the old copy automatically.

## Test in SSMS

Open SSMS 22 and connect to any SQL Server instance.

Try a risky query:

```sql
DELETE FROM dbo.SomeTable;
```

QueryGuard should show a warning before the query executes.

Then try a filtered query:

```sql
DELETE FROM dbo.SomeTable WHERE Id = 1;
```

That should be allowed.

## Local Configuration

QueryGuard writes default settings here:

```text
%APPDATA%\QueryGuard\config.json
```

Useful settings:

```json
{
  "enabled": true,
  "blockOnAllEnvironments": true,
  "blockThreshold": 70,
  "allowOverride": true,
  "auditLoggingEnabled": true,
  "excludedTables": [
    "#TempStaging",
    "##GlobalTemp",
    "ETL_STAGING",
    "IMPORT_BUFFER"
  ]
}
```

After changing config, restart SSMS.

## Common Developer Workflow

1. Change code.
2. Run tests:

```powershell
dotnet test SSMSExtension.Tests\SSMSExtension.Tests.csproj
```

3. Build the VSIX:

```powershell
dotnet build SSMSExtension.slnx
```

4. Close SSMS.
5. Install the new VSIX:

```powershell
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /quiet "D:\SSMSExtension\SSMSExtension\bin\Debug\net48\SSMSExtension.vsix"
```

6. Open SSMS and test with a safe and unsafe query.

## Troubleshooting

If the extension does not appear to work:

1. Make sure SSMS was fully closed before installing.
2. Check the install log.
3. Confirm files exist under:

```text
%LOCALAPPDATA%\Microsoft\SSMS\22.0_d5c13cd0\Extensions\
```

4. Look for these files in the installed extension folder:

```text
SSMSExtension.dll
SSMSExtension.pkgdef
Microsoft.SqlServer.TransactSql.ScriptDom.dll
extension.vsixmanifest
```

5. Restart SSMS.

If the extension still does not load, rebuild and reinstall.

