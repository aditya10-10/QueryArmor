using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using QueryArmor.Application.Auditing;
using QueryArmor.Application.Fixes;
using QueryArmor.Application.Inspection;
using QueryArmor.Domain.Analysis;
using QueryArmor.Infrastructure.Configuration;
using QueryArmor.Infrastructure.Logging;

namespace QueryArmor.Ssms
{
    /// <summary>
    /// Async package entry point for the QueryArmor SSMS extension.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.1.0")]
    [ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    public sealed class QueryArmorPackage : AsyncPackage
    {
        public const string PackageGuidString = "367c6c07-d4f7-4a85-8485-093a428c3a39";

        private CommandInterceptor? _interceptor;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var configProvider = new GuardConfigurationProvider();
            var config = configProvider.Load();
            var logger = new AuditLogger(config.AuditLogPath, config.CentralAuditPath);
            var analyzer = new SqlQueryAnalyzer();
            var fixer = new QueryAutoFixer();
            var inspectionService = new QueryInspectionService(analyzer, config);

            _interceptor = new CommandInterceptor(
                inspectionService,
                fixer,
                config,
                logger);

            _interceptor.RegisterCommandHook();
            _ = logger.LogAsync(AuditEvent.ExtensionLoaded, "QueryArmor loaded.");
        }

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                _interceptor?.UnregisterCommandHook();
            });

            base.Dispose(disposing);
        }
    }
}
