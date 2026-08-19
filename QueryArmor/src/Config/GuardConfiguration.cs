using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QueryArmor.Config
{
    /// <summary>
    /// Persistent configuration for QueryArmor.
    /// Stored at %APPDATA%\QueryArmor\config.json
    /// Supports per-team policy distribution via a shared UNC path.
    /// </summary>
    public class GuardConfiguration
    {
        private static readonly string DefaultConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QueryArmor", "config.json");

        // -- Core settings --------------------------------------------------
        [JsonPropertyName("enabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Block on all environments, not just production.
        /// Recommended to leave true for individual developer safety.
        /// </summary>
        [JsonPropertyName("blockOnAllEnvironments")]
        public bool BlockOnAllEnvironments { get; set; } = true;

        // -- Environment detection -------------------------------------------
        /// <summary>
        /// Server name substrings that identify production instances.
        /// Matches are case-insensitive. Example: ["PROD", "PRD", "LIVE"]
        /// </summary>
        [JsonPropertyName("productionServerPatterns")]
        public List<string> ProductionServerPatterns { get; set; } = new()
        {
            "PROD", "PRD", "LIVE", "RELEASE", "MASTER-DB"
        };

        // -- Risk thresholds -------------------------------------------------
        /// <summary>
        /// Risk score at or above which execution is blocked (0-100).
        /// Default 70 = block HIGH and CRITICAL; allow SAFE/LOW/MEDIUM through.
        /// </summary>
        [JsonPropertyName("blockThreshold")]
        public int BlockThreshold { get; set; } = 70;

        /// <summary>
        /// Require a second confirmation dialog for Critical-level overrides.
        /// </summary>
        [JsonPropertyName("requireDoubleConfirmForCritical")]
        public bool RequireDoubleConfirmForCritical { get; set; } = true;

        [JsonPropertyName("allowOverride")]
        public bool AllowOverride { get; set; } = true;

        // -- Audit logging ---------------------------------------------------
        [JsonPropertyName("auditLoggingEnabled")]
        public bool AuditLoggingEnabled { get; set; } = true;

        /// <summary>
        /// Path for audit log file. Supports UNC paths for centralized logging.
        /// Default: %APPDATA%\QueryArmor\audit.log
        /// </summary>
        [JsonPropertyName("auditLogPath")]
        public string AuditLogPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QueryArmor", "audit.log");

        /// <summary>
        /// Optional: shared network path to push audit events to a central store.
        /// Set to null to disable central logging.
        /// </summary>
        [JsonPropertyName("centralAuditPath")]
        public string? CentralAuditPath { get; set; } = null;

        // -- Table exclusions ------------------------------------------------
        /// <summary>
        /// Tables where blanket UPDATE/DELETE is allowed (e.g. temp/staging tables).
        /// Matches are case-insensitive exact table name comparisons.
        /// </summary>
        [JsonPropertyName("excludedTables")]
        public List<string> ExcludedTables { get; set; } = new()
        {
            "#TempStaging", "##GlobalTemp", "ETL_STAGING", "IMPORT_BUFFER"
        };

        // -- Team policy overlay ---------------------------------------------
        /// <summary>
        /// Optional: path to a shared team policy JSON file that overrides
        /// individual settings. Useful for enforcing org-wide standards.
        /// Set via GPO, SCCM, or deploy alongside the extension DLL.
        /// </summary>
        [JsonPropertyName("teamPolicyPath")]
        public string? TeamPolicyPath { get; set; } = null;

        // -- Serialization ---------------------------------------------------
        public static GuardConfiguration Load(string? path = null)
        {
            path ??= DefaultConfigPath;

            GuardConfiguration config;

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    config = JsonSerializer.Deserialize<GuardConfiguration>(json)
                             ?? new GuardConfiguration();
                }
                catch
                {
                    config = new GuardConfiguration();
                }
            }
            else
            {
                config = new GuardConfiguration();
                config.Save(path);  // Write defaults on first run
            }

            // Apply team policy overlay if configured
            if (!string.IsNullOrEmpty(config.TeamPolicyPath) && File.Exists(config.TeamPolicyPath))
            {
                try
                {
                    var teamPolicy = JsonSerializer.Deserialize<TeamPolicy>(
                        File.ReadAllText(config.TeamPolicyPath));
                    teamPolicy?.ApplyTo(config);
                }
                catch { /* Team policy override failed — fall back to local config */ }
            }

            return config;
        }

        public void Save(string? path = null)
        {
            path ??= DefaultConfigPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    /// <summary>
    /// Team-level policy overlay distributed via network share.
    /// Allows DBA/security teams to enforce minimum standards
    /// without overriding all developer preferences.
    /// </summary>
    public class TeamPolicy
    {
        [JsonPropertyName("minimumBlockThreshold")]
        public int? MinimumBlockThreshold { get; set; }

        [JsonPropertyName("enforceProductionPatterns")]
        public List<string>? EnforceProductionPatterns { get; set; }

        [JsonPropertyName("centralAuditPath")]
        public string? CentralAuditPath { get; set; }

        [JsonPropertyName("disableOverride")]
        public bool? DisableOverride { get; set; }

        public void ApplyTo(GuardConfiguration config)
        {
            // Team policy can only tighten restrictions, never loosen them
            if (MinimumBlockThreshold.HasValue && config.BlockThreshold > MinimumBlockThreshold.Value)
                config.BlockThreshold = MinimumBlockThreshold.Value;

            if (EnforceProductionPatterns?.Count > 0)
            {
                foreach (var p in EnforceProductionPatterns)
                    if (!config.ProductionServerPatterns.Contains(p, StringComparer.OrdinalIgnoreCase))
                        config.ProductionServerPatterns.Add(p);
            }

            if (!string.IsNullOrEmpty(CentralAuditPath))
                config.CentralAuditPath = CentralAuditPath;

            if (DisableOverride == true)
                config.AllowOverride = false;
        }
    }
}
