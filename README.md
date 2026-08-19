# QueryArmor for SSMS

QueryArmor is a SQL Server Management Studio extension that warns before running risky `UPDATE` or `DELETE` queries.

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
|-- QueryArmor.slnx
|-- QueryArmor
|   |-- QueryArmor.csproj
|   |-- source.extension.vsixmanifest
|   |-- QueryArmor.pkgdef
|   |-- src
|       |-- QueryArmorPackage.cs
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
|-- QueryArmor.Tests
    |-- SqlQueryAnalyzerTests.cs
```

## Important Files

`QueryArmor/src/QueryArmorPackage.cs`

Main extension entry point. SSMS loads this package when the extension starts.

`QueryArmor/src/CommandInterceptor.cs`

Hooks into the SSMS command system. When the user runs a query, this class gets the active SQL text, analyzes it, and blocks or allows execution.

`QueryArmor/src/Core/SqlQueryAnalyzer.cs`

Core safety checker. It parses SQL using `Microsoft.SqlServer.TransactSql.ScriptDom` and detects unsafe `UPDATE` or `DELETE` statements without a `WHERE` clause.

`QueryArmor/src/Core/QueryAutoFixer.cs`

Suggests simple fixes, such as uncommenting a commented `WHERE` clause or adding a placeholder `WHERE`.

`QueryArmor/src/Core/AnalysisResult.cs`

Shared result model for risks, violations, statement type, table name, and messages.

`QueryArmor/src/UI/BlockingWarningDialog.cs`

Warning dialog shown when a risky query is detected.

`QueryArmor/src/Config/GuardConfiguration.cs`

Local configuration. On first run, QueryArmor creates:

```text
%APPDATA%\QueryArmor\config.json
```

Default audit log:

```text
%APPDATA%\QueryArmor\audit.log
```

`QueryArmor.Tests/SqlQueryAnalyzerTests.cs`

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
dotnet build QueryArmor.slnx
```

The VSIX file is created here:

```text
D:\SSMSExtension\QueryArmor\bin\Debug\net48\QueryArmor.vsix
```

## Run Tests

```powershell
cd D:\SSMSExtension
dotnet test QueryArmor.Tests\QueryArmor.Tests.csproj
```

## Install Locally in SSMS 22

Close SSMS first.

Then run:

```powershell
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /quiet "D:\SSMSExtension\QueryArmor\bin\Debug\net48\QueryArmor.vsix"
```

The extension is installed under a folder like:

```text
%LOCALAPPDATA%\Microsoft\SSMS\22.0_d5c13cd0\Extensions\<random-folder>\
```

The random folder name is normal. VSIX installer creates it.

## Check Install Log

To create a log during install:

```powershell
$log = Join-Path $env:TEMP "QueryArmor-vsix-install.log"
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /quiet /logFile:$log "D:\SSMSExtension\QueryArmor\bin\Debug\net48\QueryArmor.vsix"
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
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /quiet /uninstall:QueryArmor.SSMS.810f8a7d-a567-40e9-913f-d63cb272f93f
```

If you are reinstalling the same version, running the install command again usually upgrades/replaces the old copy automatically.

## Test in SSMS

Open SSMS 22 and connect to any SQL Server instance.

Try a risky query:

```sql
DELETE FROM dbo.SomeTable;
```

QueryArmor should show a warning before the query executes.

Then try a filtered query:

```sql
DELETE FROM dbo.SomeTable WHERE Id = 1;
```

That should be allowed.

## Local Configuration

QueryArmor writes default settings here:

```text
%APPDATA%\QueryArmor\config.json
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
dotnet test QueryArmor.Tests\QueryArmor.Tests.csproj
```

3. Build the VSIX:

```powershell
dotnet build QueryArmor.slnx
```

4. Close SSMS.
5. Install the new VSIX:

```powershell
& "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" /quiet "D:\SSMSExtension\QueryArmor\bin\Debug\net48\QueryArmor.vsix"
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
QueryArmor.dll
QueryArmor.pkgdef
Microsoft.SqlServer.TransactSql.ScriptDom.dll
extension.vsixmanifest
```

5. Restart SSMS.

If the extension still does not load, rebuild and reinstall.
