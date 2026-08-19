using System;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using QueryArmor.Application.Auditing;
using QueryArmor.Application.Configuration;
using QueryArmor.Application.Fixes;
using QueryArmor.Application.Inspection;
using QueryArmor.Domain.Analysis;
using QueryArmor.Presentation.Dialogs;
using QueryArmor.Presentation.Notifications;

namespace QueryArmor.Ssms
{
    /// <summary>
    /// Hooks into the SSMS command pipeline and delegates query policy decisions
    /// to the application layer.
    /// </summary>
    internal sealed class CommandInterceptor
    {
        private readonly QueryInspectionService _inspectionService;
        private readonly IQueryAutoFixer _fixer;
        private readonly GuardConfiguration _config;
        private readonly IAuditLogger _logger;

        private DTE2? _dte;
        private CommandEvents? _allCommandEvents;

        public CommandInterceptor(
            QueryInspectionService inspectionService,
            IQueryAutoFixer fixer,
            GuardConfiguration config,
            IAuditLogger logger)
        {
            _inspectionService = inspectionService;
            _fixer = fixer;
            _config = config;
            _logger = logger;
        }

        public void RegisterCommandHook()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (_dte == null)
                return;

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
            string commandGuid,
            int commandId,
            object customIn,
            object customOut,
            ref bool cancelDefault)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!_config.IsEnabled || !ShouldInspectCommand(commandGuid, commandId))
                return;

            string sql = GetActiveQueryText();
            if (string.IsNullOrWhiteSpace(sql))
                return;

            var inspection = _inspectionService.Inspect(sql, GetActiveConnectionString());
            var analysis = inspection.Analysis;

            if (analysis.IsSafe)
                return;

            _ = _logger.LogAsync(AuditEvent.QueryIntercepted, sql, analysis);

            if (inspection.ShouldBlock)
            {
                ShowBlockingDialog(sql, analysis, ref cancelDefault);
                return;
            }

            if (analysis.HasWarnings)
                StatusBarNotifier.ShowWarning(_dte!, $"QueryArmor: {analysis.Violations[0].Message}");
        }

        private void ShowBlockingDialog(string sql, AnalysisResult analysis, ref bool cancelDefault)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dialog = new BlockingWarningDialog(analysis, sql, _fixer, _config);
            var userChoice = dialog.ShowModal();

            switch (userChoice)
            {
                case UserChoice.Cancel:
                    cancelDefault = true;
                    _ = _logger.LogAsync(AuditEvent.QueryBlocked, sql, analysis);
                    break;

                case UserChoice.ApplyFix:
                    cancelDefault = true;
                    if (!string.IsNullOrWhiteSpace(dialog.FixedSql))
                        ApplyFixToEditor(dialog.FixedSql!);
                    _ = _logger.LogAsync(AuditEvent.FixApplied, sql, analysis);
                    break;

                case UserChoice.ForceExecute:
                    if (!_config.AllowOverride)
                    {
                        cancelDefault = true;
                        _ = _logger.LogAsync(AuditEvent.QueryBlocked, sql, analysis);
                        StatusBarNotifier.ShowWarning(_dte!, "QueryArmor: team policy blocked an override attempt.");
                        break;
                    }

                    _ = _logger.LogAsync(AuditEvent.OverrideExecuted, sql, analysis);
                    break;
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

        private string GetActiveQueryText()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var doc = _dte?.ActiveDocument;
                if (doc == null)
                    return string.Empty;

                var textDoc = (TextDocument)doc.Object("TextDocument");
                var selection = textDoc.Selection;

                return selection.IsEmpty
                    ? textDoc.StartPoint.CreateEditPoint().GetText(textDoc.EndPoint)
                    : selection.Text;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string? GetActiveConnectionString()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var serviceType = Type.GetTypeFromProgID("SqlConnectionInfoWithConnection");
                var connectionInfoService = serviceType == null
                    ? null
                    : Package.GetGlobalService(serviceType);

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
                StatusBarNotifier.ShowError(_dte!, $"QueryArmor: Could not apply fix - {ex.Message}");
            }
        }
    }
}
