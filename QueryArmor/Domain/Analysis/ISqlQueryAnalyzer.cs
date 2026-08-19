namespace QueryArmor.Domain.Analysis
{
    public interface ISqlQueryAnalyzer
    {
        AnalysisResult Analyze(string rawSql);
    }
}
