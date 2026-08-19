using QueryArmor.Domain.Analysis;

namespace QueryArmor.Application.Inspection
{
    public sealed class QueryInspectionResult
    {
        public QueryInspectionResult(AnalysisResult analysis, bool shouldBlock)
        {
            Analysis = analysis;
            ShouldBlock = shouldBlock;
        }

        public AnalysisResult Analysis { get; }
        public bool ShouldBlock { get; }
    }
}
