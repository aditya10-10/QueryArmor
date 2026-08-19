using QueryArmor.Domain.Analysis;
using System.Linq;
using Xunit;

namespace QueryArmor.Tests
{
    public class SqlQueryAnalyzerTests
    {
        private readonly SqlQueryAnalyzer _analyzer = new SqlQueryAnalyzer();

        [Fact]
        public void EmptyQuery_IsSafe()
        {
            var result = _analyzer.Analyze("");
            Assert.True(result.IsSafe);
        }

        [Fact]
        public void SelectQuery_IsSafe()
        {
            var result = _analyzer.Analyze("SELECT * FROM dbo.Users WHERE Id = 1;");
            Assert.True(result.IsSafe);
        }

        [Fact]
        public void UpdateWithoutWhere_IsCritical()
        {
            var result = _analyzer.Analyze("UPDATE dbo.Users SET IsActive = 0;");
            Assert.Equal(RiskLevel.Critical, result.RiskLevel);
            Assert.Contains(result.Violations, v => v.Code == ViolationCode.UpdateWithoutWhere);
        }

        [Fact]
        public void DeleteWithoutWhere_IsCritical()
        {
            var result = _analyzer.Analyze("DELETE FROM dbo.Users;");
            Assert.Equal(RiskLevel.Critical, result.RiskLevel);
            Assert.Contains(result.Violations, v => v.Code == ViolationCode.DeleteWithoutWhere);
        }

        [Fact]
        public void UpdateWithWhere_IsSafe()
        {
            var result = _analyzer.Analyze("UPDATE dbo.Users SET IsActive = 0 WHERE Id = 5;");
            Assert.True(result.IsSafe);
        }

        [Fact]
        public void DeleteWithWhere_IsSafe()
        {
            var result = _analyzer.Analyze("DELETE FROM dbo.Users WHERE Id = 5;");
            Assert.True(result.IsSafe);
        }

        [Fact]
        public void TrivialUpdateWhere_IsHighRisk()
        {
            var result = _analyzer.Analyze("UPDATE dbo.Users SET IsActive = 0 WHERE 1=1;");
            Assert.Equal(RiskLevel.High, result.RiskLevel);
            Assert.Contains(result.Violations, v => v.Code == ViolationCode.TrivialWhereClause);
        }

        [Fact]
        public void TrivialDeleteWhere_IsHighRisk()
        {
            var result = _analyzer.Analyze("DELETE FROM dbo.Users WHERE NULL IS NULL;");
            Assert.Equal(RiskLevel.High, result.RiskLevel);
            Assert.Contains(result.Violations, v => v.Code == ViolationCode.TrivialWhereClause);
        }

        [Fact]
        public void CommentedWhere_AddsWarning()
        {
            var result = _analyzer.Analyze("UPDATE dbo.Users SET Name = 'A' -- WHERE Id = 1");
            Assert.True(result.HasWarnings);
            Assert.Contains(result.Violations, v => v.Code == ViolationCode.CommentedWhereClause);
        }

        [Fact]
        public void WhereInsideSubqueryOnly_DoesNotClearDelete()
        {
            var result = _analyzer.Analyze("DELETE FROM dbo.Users WHERE Id IN (SELECT Id FROM dbo.UsersArchive WHERE Flag = 1);");
            Assert.True(result.IsSafe);
        }

        [Fact]
        public void DeleteWithSubqueryButNoOuterWhere_IsStillCritical()
        {
            var result = _analyzer.Analyze(
                "DELETE u FROM dbo.Users u JOIN (SELECT Id FROM dbo.UsersArchive WHERE Flag = 1) a ON a.Id = u.Id;");
            Assert.Equal(RiskLevel.Critical, result.RiskLevel);
        }

        [Fact]
        public void UpdateWithSubqueryButNoOuterWhere_IsStillCritical()
        {
            var result = _analyzer.Analyze("UPDATE dbo.Users SET ManagerId = (SELECT TOP 1 Id FROM dbo.Managers WHERE IsActive = 1);");
            Assert.Equal(RiskLevel.Critical, result.RiskLevel);
        }

        [Fact]
        public void GoBatch_AnalyzesEachStatement()
        {
            var sql = "UPDATE dbo.Users SET IsActive = 1 WHERE Id = 1\nGO\nDELETE FROM dbo.Users;";
            var result = _analyzer.Analyze(sql);
            Assert.Equal(RiskLevel.Critical, result.RiskLevel);
            Assert.Equal(1, result.Violations.Count(v => v.Code == ViolationCode.DeleteWithoutWhere));
        }

        [Fact]
        public void SemicolonBatch_AnalyzesEachStatement()
        {
            var sql = "UPDATE dbo.Users SET IsActive = 1 WHERE Id = 1; DELETE FROM dbo.Users;";
            var result = _analyzer.Analyze(sql);
            Assert.Equal(RiskLevel.Critical, result.RiskLevel);
        }

        [Fact]
        public void EscapedQuotes_DoNotBreakStatementSplit()
        {
            var sql = "UPDATE dbo.Users SET Name = 'O''Reilly' WHERE Id = 1; DELETE FROM dbo.Users WHERE Id = 2;";
            var result = _analyzer.Analyze(sql);
            Assert.True(result.IsSafe);
        }

        [Fact]
        public void LineComments_AreIgnoredForSafeQuery()
        {
            var result = _analyzer.Analyze("-- harmless\nDELETE FROM dbo.Users WHERE Id = 5;");
            Assert.True(result.IsSafe);
        }

        [Fact]
        public void CommentedOutUnsafeQuery_IsIgnored()
        {
            var result = _analyzer.Analyze("-- DELETE FROM dbo.Users;\nSELECT 1;");
            Assert.True(result.IsSafe);
        }

        [Fact]
        public void DeleteInsideStringLiteral_IsIgnored()
        {
            var result = _analyzer.Analyze("SELECT 'DELETE FROM dbo.Users';");
            Assert.True(result.IsSafe);
        }

        [Fact]
        public void CteDeleteWithoutWhere_IsCritical()
        {
            var result = _analyzer.Analyze(
                "WITH TargetUsers AS (SELECT Id FROM dbo.Users WHERE IsActive = 0) DELETE FROM TargetUsers;");
            Assert.Equal(RiskLevel.Critical, result.RiskLevel);
            Assert.Contains(result.Violations, v => v.Code == ViolationCode.DeleteWithoutWhere);
        }

        [Fact]
        public void CteDeleteWithWhere_IsSafe()
        {
            var result = _analyzer.Analyze(
                "WITH TargetUsers AS (SELECT Id FROM dbo.Users WHERE IsActive = 0) DELETE FROM TargetUsers WHERE Id < 100;");
            Assert.True(result.IsSafe);
        }

        [Fact]
        public void BlockComments_AreIgnoredForSafeQuery()
        {
            var result = _analyzer.Analyze("/* note */ UPDATE dbo.Users SET IsActive = 1 WHERE Id = 5;");
            Assert.True(result.IsSafe);
        }

        [Fact]
        public void BracketedTableName_IsCaptured()
        {
            var result = _analyzer.Analyze("DELETE FROM [dbo].[Users];");
            Assert.Equal("Users", result.AffectedTable);
        }

        [Fact]
        public void MultipleWarningsAndErrors_AreMerged()
        {
            var result = _analyzer.Analyze("UPDATE dbo.Users SET Name = 'A' -- WHERE Id = 1");
            Assert.True(result.Violations.Count >= 2);
            Assert.Equal(RiskLevel.Critical, result.RiskLevel);
        }
    }
}
