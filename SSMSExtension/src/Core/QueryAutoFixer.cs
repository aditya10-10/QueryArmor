using System;
using System.Text.RegularExpressions;

namespace QueryGuard.Core
{
    /// <summary>
    /// Attempts to produce a safe, corrected version of a flagged query.
    /// All fixes are suggestions — the user always reviews before execution.
    /// </summary>
    public class QueryAutoFixer
    {
        private static readonly Regex CommentedWhere = new Regex(
            @"--\s*(WHERE\s+.+?)(?:\r?\n|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BlockCommentedWhere = new Regex(
            @"/\*\s*(WHERE\s+.*?)\s*\*/",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// Returns a fixed version of the SQL and a description of what was changed.
        /// Returns null if no automatic fix is possible.
        /// </summary>
        public FixResult? TryFix(string rawSql, AnalysisResult analysis)
        {
            // Strategy 1: Uncomment a commented-out WHERE clause
            var uncommented = TryUncommentWhere(rawSql);
            if (uncommented != null)
                return new FixResult(uncommented, "Uncommented existing WHERE clause.");

            // Strategy 2: Inject a placeholder WHERE clause
            var injected = TryInjectWherePlaceholder(rawSql, analysis);
            if (injected != null)
                return new FixResult(injected,
                    "Injected a WHERE clause placeholder. " +
                    "Replace /* YOUR_FILTER */ with the intended condition.");

            return null; // Cannot auto-fix — manual review required
        }

        private string? TryUncommentWhere(string sql)
        {
            if (CommentedWhere.IsMatch(sql))
                return CommentedWhere.Replace(sql, "$1\n");

            if (BlockCommentedWhere.IsMatch(sql))
                return BlockCommentedWhere.Replace(sql, "$1");

            return null;
        }

        private string? TryInjectWherePlaceholder(string sql, AnalysisResult analysis)
        {
            if (analysis.StatementType == StatementType.Update)
            {
                // Insert before the end of the statement (after last SET clause)
                return AppendWhereClause(sql, analysis.AffectedTable);
            }

            if (analysis.StatementType == StatementType.Delete)
            {
                return AppendWhereClause(sql, analysis.AffectedTable);
            }

            return null;
        }

        private string AppendWhereClause(string sql, string table)
        {
            string trimmed = sql.TrimEnd().TrimEnd(';');
            string placeholder = $"\nWHERE /* TODO: specify your filter for {table} */";
            return trimmed + placeholder + ";";
        }
    }

    public sealed class FixResult
    {
        public FixResult(string fixedSql, string description)
        {
            FixedSql = fixedSql;
            Description = description;
        }

        public string FixedSql { get; }
        public string Description { get; }
    }
}
