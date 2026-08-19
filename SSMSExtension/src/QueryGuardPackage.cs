using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace QueryGuard
{
    /// <summary>
    /// Async package entry point for the QueryGuard SSMS extension.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0")]
    [ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    public sealed class QueryGuardPackage : AsyncPackage
    {
        public const string PackageGuidString = "367c6c07-d4f7-4a85-8485-093a428c3a39";

        private CommandInterceptor? _interceptor;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var config = Config.GuardConfiguration.Load();
            var logger = new Logging.AuditLogger(config.AuditLogPath, config.CentralAuditPath);

            _interceptor = new CommandInterceptor(
                this,
                new Core.SqlQueryAnalyzer(),
                new Core.QueryAutoFixer(),
                config,
                logger);

            _interceptor.RegisterCommandHook();
            _ = logger.LogAsync(Logging.AuditEvent.ExtensionLoaded, "QueryGuard loaded.");
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
