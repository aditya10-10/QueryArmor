using QueryArmor.Application.Configuration;
using QueryArmor.Application.Inspection;
using QueryArmor.Domain.Analysis;
using Xunit;

namespace QueryArmor.Tests
{
    public class QueryInspectionServiceTests
    {
        [Fact]
        public void Inspect_WhenTableIsExcluded_AllowsUnsafeStatement()
        {
            var config = new GuardConfiguration
            {
                ExcludedTables = { "Users" }
            };
            var service = new QueryInspectionService(new SqlQueryAnalyzer(), config);

            var result = service.Inspect("DELETE FROM dbo.Users;", connectionString: null);

            Assert.False(result.ShouldBlock);
            Assert.True(result.Analysis.IsSafe);
        }

        [Fact]
        public void Inspect_WhenOnlyProductionBlockingEnabled_BlocksProductionConnection()
        {
            var config = new GuardConfiguration
            {
                BlockOnAllEnvironments = false,
                ProductionServerPatterns = { "PROD" }
            };
            var service = new QueryInspectionService(new SqlQueryAnalyzer(), config);

            var result = service.Inspect("UPDATE dbo.Users SET IsActive = 0;", "Server=SQL-PROD-01");

            Assert.True(result.ShouldBlock);
            Assert.Equal(RiskLevel.Critical, result.Analysis.RiskLevel);
        }

        [Fact]
        public void Inspect_WhenOnlyProductionBlockingEnabled_AllowsNonProductionConnection()
        {
            var config = new GuardConfiguration
            {
                BlockOnAllEnvironments = false,
                ProductionServerPatterns = { "PROD" }
            };
            var service = new QueryInspectionService(new SqlQueryAnalyzer(), config);

            var result = service.Inspect("UPDATE dbo.Users SET IsActive = 0;", "Server=SQL-DEV-01");

            Assert.False(result.ShouldBlock);
            Assert.Equal(RiskLevel.Critical, result.Analysis.RiskLevel);
        }
    }
}
