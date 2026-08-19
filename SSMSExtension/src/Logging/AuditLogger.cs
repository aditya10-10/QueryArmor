using QueryGuard.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QueryGuard.Logging
{
    public enum AuditEvent
    {
        ExtensionLoaded,
        QueryIntercepted,
        QueryBlocked,
        FixApplied,
        OverrideExecuted,
        ConfigurationChanged
    }

    /// <summary>
    /// Writes structured audit events to a local log file and optionally
    /// to a central network share for DBA/compliance review.
    ///
    /// Log format: newline-delimited JSON (NDJSON) for easy ingestion
    /// into SIEM systems (Splunk, ELK, Azure Sentinel).
    /// </summary>
    public class AuditLogger
    {
        private readonly string _localPath;
        private readonly string? _centralPath;
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public AuditLogger(string localPath, string? centralPath = null)
        {
            _localPath = localPath;
            _centralPath = centralPath;
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        }

        public async Task LogAsync(AuditEvent eventType, string sql,
            AnalysisResult? analysis = null)
        {
            var entry = new AuditEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Event = eventType.ToString(),
                UserName = Environment.UserName,
                MachineName = Environment.MachineName,
                SqlSnippet = TruncateSql(sql),
                TableName = analysis?.AffectedTable,
                Operation = analysis?.StatementType.ToString(),
                RiskLevel = analysis?.RiskLevelDisplay,
                ViolationCount = analysis?.Violations.Count ?? 0
            };

            string line = JsonSerializer.Serialize(entry) + Environment.NewLine;

            await _lock.WaitAsync();
            try
            {
                await AppendLineAsync(_localPath, line);

                if (_centralPath is string centralPath && centralPath.Length > 0)
                {
                    try { await AppendLineAsync(centralPath, line); }
                    catch { /* Central log failure is non-fatal */ }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private static string TruncateSql(string sql)
            => sql.Length > 500 ? sql.Substring(0, 500) + "..." : sql;

        private static Task AppendLineAsync(string path, string line)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            return Task.Run(() => File.AppendAllText(path, line));
        }
    }

    internal class AuditEntry
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Event { get; set; } = "";
        public string UserName { get; set; } = "";
        public string MachineName { get; set; } = "";
        public string SqlSnippet { get; set; } = "";
        public string? TableName { get; set; }
        public string? Operation { get; set; }
        public string? RiskLevel { get; set; }
        public int ViolationCount { get; set; }
    }
}
