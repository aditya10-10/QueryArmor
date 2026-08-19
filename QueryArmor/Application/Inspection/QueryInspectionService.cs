using System;
using System.Linq;
using QueryArmor.Application.Configuration;
using QueryArmor.Domain.Analysis;

namespace QueryArmor.Application.Inspection
{
    public sealed class QueryInspectionService
    {
        private readonly ISqlQueryAnalyzer _analyzer;
        private readonly GuardConfiguration _config;

        public QueryInspectionService(ISqlQueryAnalyzer analyzer, GuardConfiguration config)
        {
            _analyzer = analyzer;
            _config = config;
        }

        public QueryInspectionResult Inspect(string sql, string? connectionString)
        {
            var analysis = _analyzer.Analyze(sql);
            ApplyConfiguration(analysis);

            return new QueryInspectionResult(
                analysis,
                shouldBlock: ShouldBlock(analysis, connectionString));
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

        private bool ShouldBlock(AnalysisResult result, string? connectionString)
        {
            if (result.RiskScore < _config.BlockThreshold)
                return false;

            if (_config.BlockOnAllEnvironments)
                return true;

            return IsProductionEnvironment(connectionString);
        }

        private bool IsProductionEnvironment(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            try
            {
                return _config.ProductionServerPatterns.Any(pattern =>
                    connectionString.Contains(pattern, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return _config.BlockOnAllEnvironments;
            }
        }
    }
}
