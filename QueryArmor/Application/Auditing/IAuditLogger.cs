using System.Threading.Tasks;
using QueryArmor.Domain.Analysis;

namespace QueryArmor.Application.Auditing
{
    public interface IAuditLogger
    {
        Task LogAsync(AuditEvent eventType, string sql, AnalysisResult? analysis = null);
    }

    public enum AuditEvent
    {
        ExtensionLoaded,
        QueryIntercepted,
        QueryBlocked,
        FixApplied,
        OverrideExecuted,
        ConfigurationChanged
    }
}
