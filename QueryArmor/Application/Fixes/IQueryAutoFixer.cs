using QueryArmor.Domain.Analysis;

namespace QueryArmor.Application.Fixes
{
    public interface IQueryAutoFixer
    {
        FixResult? TryFix(string rawSql, AnalysisResult analysis);
    }
}
