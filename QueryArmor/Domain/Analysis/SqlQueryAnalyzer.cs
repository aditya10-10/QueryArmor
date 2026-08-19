using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace QueryArmor.Domain.Analysis
{
    /// <summary>
    /// Analyzes SQL queries for unsafe patterns before execution.
    /// Detects UPDATE/DELETE statements lacking proper WHERE clause filtering.
    /// </summary>
    public class SqlQueryAnalyzer : ISqlQueryAnalyzer
    {
        private static readonly Regex TrivialWherePredicate = new Regex(
            @"\bWHERE\s+(?:1\s*=\s*1|'[^']*'\s*=\s*'[^']*'|NULL\s+IS\s+NULL)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CommentedWhereClause = new Regex(
            @"--\s*WHERE\s+|/\*\s*WHERE\s+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public AnalysisResult Analyze(string rawSql)
        {
            if (string.IsNullOrWhiteSpace(rawSql))
                return AnalysisResult.Clean("Empty query.");

            var result = new AnalysisResult();

            if (CommentedWhereClause.IsMatch(rawSql))
            {
                result.AddWarning(ViolationCode.CommentedWhereClause,
                    "A WHERE clause appears commented out. Did you mean to include it?");
                result.RiskLevel = RiskLevel.Medium;
            }

            foreach (var batch in SplitBatches(rawSql))
            {
                var parser = new TSql160Parser(initialQuotedIdentifiers: true);
                using var reader = new System.IO.StringReader(batch);
                var fragment = parser.Parse(reader, out IList<ParseError> errors);

                if (errors.Count > 0)
                    continue;

                var visitor = new UnsafeDmlVisitor();
                fragment.Accept(visitor);
                result.Merge(visitor.Result);
            }

            return result;
        }

        private IEnumerable<string> SplitBatches(string sql)
        {
            return Regex.Split(sql, @"^\s*GO\s*(?:--[^\r\n]*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Where(batch => !string.IsNullOrWhiteSpace(batch));
        }

        private sealed class UnsafeDmlVisitor : TSqlFragmentVisitor
        {
            public AnalysisResult Result { get; } = new AnalysisResult();

            public override void ExplicitVisit(UpdateStatement node)
            {
                AnalyzeUpdate(node.UpdateSpecification);
            }

            public override void ExplicitVisit(DeleteStatement node)
            {
                AnalyzeDelete(node.DeleteSpecification);
            }

            private void AnalyzeUpdate(UpdateSpecification spec)
            {
                string table = GetTargetName(spec.Target);
                Result.StatementType = StatementType.Update;
                Result.AffectedTable = table;

                if (spec.WhereClause == null)
                {
                    Result.AddViolation(ViolationCode.UpdateWithoutWhere,
                        $"UPDATE on '{table}' has no WHERE clause. This will modify ALL rows in the table.");
                    Result.RiskLevel = RiskLevel.Critical;
                }
                else if (HasTrivialWhere(spec.WhereClause))
                {
                    Result.AddViolation(ViolationCode.TrivialWhereClause,
                        $"UPDATE on '{table}' uses a trivially-true WHERE predicate (e.g. WHERE 1=1). This effectively filters nothing.");
                    if (Result.RiskLevel < RiskLevel.High) Result.RiskLevel = RiskLevel.High;
                }
            }

            private void AnalyzeDelete(DeleteSpecification spec)
            {
                string table = GetTargetName(spec.Target);
                Result.StatementType = StatementType.Delete;
                Result.AffectedTable = table;

                if (spec.WhereClause == null)
                {
                    Result.AddViolation(ViolationCode.DeleteWithoutWhere,
                        $"DELETE FROM '{table}' has no WHERE clause. ALL rows will be permanently deleted.");
                    Result.RiskLevel = RiskLevel.Critical;
                }
                else if (HasTrivialWhere(spec.WhereClause))
                {
                    Result.AddViolation(ViolationCode.TrivialWhereClause,
                        $"DELETE on '{table}' uses a trivially-true WHERE predicate. Every row will still be deleted.");
                    if (Result.RiskLevel < RiskLevel.High) Result.RiskLevel = RiskLevel.High;
                }
            }

            private bool HasTrivialWhere(WhereClause whereClause)
            {
                return TrivialWherePredicate.IsMatch(GetFragmentText(whereClause));
            }

            private string GetTargetName(TableReference target)
            {
                if (target is NamedTableReference named && named.SchemaObject.BaseIdentifier != null)
                    return named.SchemaObject.BaseIdentifier.Value;

                if (target is VariableTableReference variable)
                    return variable.Variable.Name;

                return GetFragmentText(target).Trim();
            }

            private string GetFragmentText(TSqlFragment fragment)
            {
                if (fragment.ScriptTokenStream == null ||
                    fragment.FirstTokenIndex < 0 ||
                    fragment.LastTokenIndex < fragment.FirstTokenIndex)
                {
                    return string.Empty;
                }

                return string.Concat(fragment.ScriptTokenStream
                    .Skip(fragment.FirstTokenIndex)
                    .Take(fragment.LastTokenIndex - fragment.FirstTokenIndex + 1)
                    .Select(token => token.Text));
            }
        }
    }
}
