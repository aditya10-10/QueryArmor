using System;
using System.Collections.Generic;
using System.Linq;

namespace QueryGuard.Core
{
    public enum RiskLevel
    {
        Safe = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public enum StatementType
    {
        Unknown,
        Select,
        Update,
        Delete,
        Insert,
        Other
    }

    public enum ViolationCode
    {
        UpdateWithoutWhere = 1001,
        DeleteWithoutWhere = 1002,
        TrivialWhereClause = 1003,
        CommentedWhereClause = 1004,
        MissingRowFilter = 1005
    }

    public class Violation
    {
        public ViolationCode Code { get; }
        public string Message { get; }
        public bool IsWarning { get; }
        public string Severity => IsWarning ? "WARNING" : "ERROR";

        public Violation(ViolationCode code, string message, bool isWarning = false)
        {
            Code = code;
            Message = message;
            IsWarning = isWarning;
        }
    }

    public class AnalysisResult
    {
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Safe;
        public StatementType StatementType { get; set; } = StatementType.Unknown;
        public string AffectedTable { get; set; } = string.Empty;
        public List<Violation> Violations { get; } = new();
        public string? AutoFixSuggestion { get; set; }
        public int RiskScore => RiskLevel switch
        {
            RiskLevel.Critical => 100,
            RiskLevel.High => 80,
            RiskLevel.Medium => 50,
            RiskLevel.Low => 20,
            _ => 0
        };

        public bool IsBlocked => RiskLevel >= RiskLevel.High && Violations.Any(v => !v.IsWarning);
        public bool HasWarnings => Violations.Any(v => v.IsWarning);
        public bool IsSafe => RiskLevel == RiskLevel.Safe && !Violations.Any(v => !v.IsWarning);

        public void AddViolation(ViolationCode code, string message)
            => Violations.Add(new Violation(code, message, isWarning: false));

        public void AddWarning(ViolationCode code, string message)
            => Violations.Add(new Violation(code, message, isWarning: true));

        public void Merge(AnalysisResult other)
        {
            if (other.RiskLevel > RiskLevel) RiskLevel = other.RiskLevel;
            if (other.StatementType != StatementType.Unknown) StatementType = other.StatementType;
            if (!string.IsNullOrEmpty(other.AffectedTable)) AffectedTable = other.AffectedTable;
            Violations.AddRange(other.Violations);
        }

        public static AnalysisResult Clean(string reason = "")
            => new() { RiskLevel = RiskLevel.Safe };

        public string RiskLevelDisplay => RiskLevel switch
        {
            RiskLevel.Critical => "CRITICAL",
            RiskLevel.High => "HIGH",
            RiskLevel.Medium => "MEDIUM",
            RiskLevel.Low => "LOW",
            _ => "SAFE"
        };

        public string SummaryMessage
        {
            get
            {
                if (IsSafe) return "Query passed all safety checks.";
                var errors = Violations.Where(v => !v.IsWarning).ToList();
                if (errors.Count == 0) return $"{Violations.Count} warning(s) found. Review before executing.";
                return $"{errors.Count} violation(s) detected. Execution blocked.";
            }
        }
    }
}
