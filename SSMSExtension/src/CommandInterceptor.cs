using System;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using EnvDTE80;
using QueryGuard.Core;
using QueryGuard.Config;
using QueryGuard.Logging;
using QueryGuard.UI;
using System.Linq;

namespace QueryGuard
{
    /// <summary>
    /// Hooks into the SSMS "Execute Query" command pipeline.
    ///
    /// SSMS uses the DTE (Development Tools Environment) automation model.
    /// We register a BeforeExecute handler on the "Query.Execute" command
    /// (command ID 0x0002 in the SqlQueryGroup command group) to intercept
    /// execution before the query reaches the SQL Server connection.
    /// </summary>
    internal class CommandInterceptor
    {
        private readonly AsyncPackage _package;
        private readonly SqlQueryAnalyzer _analyzer;
        private readonly QueryAutoFixer _fixer;
        private readonly GuardConfiguration _config;
        private readonly AuditLogger _logger;

        private DTE2? _dte;
        private CommandEvents? _allCommandEvents;

        public CommandInterceptor(
            AsyncPackage package,
            SqlQueryAnalyzer analyzer,
            QueryAutoFixer fixer,
            GuardConfiguration config,
            AuditLogger logger)
        {
            _package = package;
            _analyzer = analyzer;
            _fixer = fixer;
            _config = config;
            _logger = logger;
        }

        public void RegisterCommandHook()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (_dte == null) return;

            _allCommandEvents = _dte.Events.CommandEvents;
            _allCommandEvents.BeforeExecute += OnBeforeExecuteQuery;
        }

        public void UnregisterCommandHook()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_allCommandEvents != null)
                _allCommandEvents.BeforeExecute -= OnBeforeExecuteQuery;
        }

        private void OnBeforeExecuteQuery(
            string commandGuid, int commandId,
            object CustomIn, object CustomOut,
            ref bool CancelDefault)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!_config.IsEnabled) return;
            if (!ShouldInspectCommand(commandGuid, commandId)) return;

            // Get the active query text from the SSMS editor window
            string sql = GetActiveQueryText();
            if (string.IsNullOrWhiteSpace(sql)) return;

            // Analyze the query
            var result = _analyzer.Analyze(sql);
            ApplyConfiguration(result);

            if (result.IsSafe) return; // Nothing to do — let SSMS proceed

            // Log the event
            _ = _logger.LogAsync(AuditEvent.QueryIntercepted, sql, result);

            bool shouldBlock = ShouldBlock(result);

            if (shouldBlock)
            {
                // Show blocking dialog
                var dialog = new BlockingWarningDialog(result, sql, _fixer, _config);
                var userChoice = dialog.ShowModal();

                switch (userChoice)
                {
                    case UserChoice.Cancel:
                        CancelDefault = true;     // Block execution
                        _ = _logger.LogAsync(AuditEvent.QueryBlocked, sql, result);
                        break;

                    case UserChoice.ApplyFix:
                        CancelDefault = true;     // Block original; fix will be applied to editor
                        if (!string.IsNullOrWhiteSpace(dialog.FixedSql))
                            ApplyFixToEditor(dialog.FixedSql!);
                        _ = _logger.LogAsync(AuditEvent.FixApplied, sql, result);
                        break;

                    case UserChoice.ForceExecute:
                        if (!_config.AllowOverride)
                        {
                            CancelDefault = true;
                            _ = _logger.LogAsync(AuditEvent.QueryBlocked, sql, result);
                            StatusBarNotifier.ShowWarning(_dte!, "QueryGuard: team policy blocked an override attempt.");
                            break;
                        }

                        // User accepted the risk — log with elevated severity
                        _ = _logger.LogAsync(AuditEvent.OverrideExecuted, sql, result);
                        break;
                }
            }
            else if (result.HasWarnings)
            {
                // Non-blocking toast notification for warnings
                StatusBarNotifier.ShowWarning(_dte!,
                    $"QueryGuard: {result.Violations[0].Message}");
            }
        }

        private bool ShouldInspectCommand(string commandGuid, int commandId)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string commandName = GetCommandName(commandGuid, commandId);
            if (string.IsNullOrWhiteSpace(commandName))
                return false;

            return commandName.IndexOf("execute", StringComparison.OrdinalIgnoreCase) >= 0
                && (commandName.IndexOf("query", StringComparison.OrdinalIgnoreCase) >= 0
                    || commandName.IndexOf("sql", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private string GetCommandName(string commandGuid, int commandId)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                Command command = _dte!.Commands.Item(commandGuid, commandId);
                return !string.IsNullOrWhiteSpace(command.Name)
                    ? command.Name
                    : command.LocalizedName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void ApplyConfiguration(AnalysisResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.AffectedTable) &&
                _config.ExcludedTables.Any(table => table.Equals(result.AffectedTable, StringComparison.OrdinalIgnoreCase)))
            {
                result.Violations.Clear();
                result.RiskLevel = RiskLevel.Safe;
                return;
            }

            if (result.RiskScore >= _config.BlockThreshold)
                return;

            if (result.RiskLevel >= RiskLevel.High)
                result.RiskLevel = RiskLevel.Medium;
        }

        private bool ShouldBlock(AnalysisResult result)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (result.RiskScore < _config.BlockThreshold)
                return false;

            if (_config.BlockOnAllEnvironments)
                return true;

            return IsProductionEnvironment();
        }

        private string GetActiveQueryText()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var doc = _dte?.ActiveDocument;
                if (doc == null) return string.Empty;

                var textDoc = (TextDocument)doc.Object("TextDocument");
                var selection = textDoc.Selection;

                // If user selected text, validate only that; otherwise validate entire document
                return selection.IsEmpty
                    ? textDoc.StartPoint.CreateEditPoint().GetText(textDoc.EndPoint)
                    : selection.Text;
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool IsProductionEnvironment()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                // Check if the active connection matches a production server pattern
                string? connectionString = GetActiveConnectionString();
                if (connectionString == null) return false;

                return _config.ProductionServerPatterns.Any(pattern =>
                    connectionString.Contains(pattern, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                // If we can't determine the environment, err on the side of caution
                return _config.BlockOnAllEnvironments;
            }
        }

        private string? GetActiveConnectionString()
        {
            // Retrieve the current connection info from SSMS's SQL connection object
            // This uses SSMS's SqlConnectionInfo which is available through
            // the SqlConnectionInfoWithConnection service
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var serviceType = Type.GetTypeFromProgID("SqlConnectionInfoWithConnection");
                var connectionInfoService = serviceType == null
                    ? null
                    : Package.GetGlobalService(serviceType);
                // Extract server name via reflection if direct cast isn't available
                return connectionInfoService?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private void ApplyFixToEditor(string fixedSql)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var textDoc = (TextDocument)_dte!.ActiveDocument.Object("TextDocument");
                var editPoint = textDoc.StartPoint.CreateEditPoint();
                editPoint.Delete(textDoc.EndPoint);
                editPoint.Insert(fixedSql);
            }
            catch (Exception ex)
            {
                StatusBarNotifier.ShowError(_dte!, $"QueryGuard: Could not apply fix - {ex.Message}");
            }
        }
    }
}
