using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityScanner.Batch;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Results;
using UnityScanner.Core.Settings;
using UnityEditor;

namespace UnityScanner.MCP
{
    public static class UnityScannerMCPResultFormatter
    {
        public static string FormatScanResult(BatchResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"success\": " + (result.ExitCode == 0 || result.ExitCode == 1).ToString().ToLowerInvariant() + ",");
            sb.AppendLine("  \"exitCode\": " + result.ExitCode + ",");
            sb.AppendLine("  \"message\": " + EscapeJsonVal(result.Message) + ",");
            sb.AppendLine("  \"totalDurationMs\": " + result.TotalDurationMs.ToString("F1") + ",");
            sb.AppendLine("  \"summary\": {");
            sb.AppendLine("    \"totalIssues\": " + result.Issues.Count + ",");
            sb.AppendLine("    \"errors\": " + result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error) + ",");
            sb.AppendLine("    \"warnings\": " + result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning) + ",");
            sb.AppendLine("    \"info\": " + result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Info) + ",");
            sb.AppendLine("    \"categoriesScanned\": " + (result.Results?.Count ?? 0));
            sb.AppendLine("  },");
            sb.AppendLine("  \"categories\": [");

            if (result.Results != null)
            {
                for (var i = 0; i < result.Results.Count; i++)
                {
                    var r = result.Results[i];
                    var comma = i < result.Results.Count - 1 ? "," : "";
                    sb.AppendLine("    {");
                    sb.AppendLine("      \"id\": " + EscapeJsonVal(r.CategoryId) + ",");
                    sb.AppendLine("      \"name\": " + EscapeJsonVal(r.DisplayName) + ",");
                    sb.AppendLine("      \"issueCount\": " + r.Issues.Count + ",");
                    sb.AppendLine("      \"durationMs\": " + r.ScanDurationMs.ToString("F1") + ",");
                    sb.AppendLine("      \"succeeded\": " + r.Succeeded.ToString().ToLowerInvariant() + ",");
                    if (!string.IsNullOrEmpty(r.ErrorMessage))
                        sb.AppendLine("      \"error\": " + EscapeJsonVal(r.ErrorMessage) + ",");
                    sb.AppendLine("      \"issues\": [");
                    for (var j = 0; j < r.Issues.Count; j++)
                    {
                        var issue = r.Issues[j];
                        var icomma = j < r.Issues.Count - 1 ? "," : "";
                        sb.AppendLine("        {");
                        sb.AppendLine("          \"severity\": \"" + issue.Severity + "\",");
                        sb.AppendLine("          \"code\": " + EscapeJsonVal(issue.IssueCode) + ",");
                        sb.AppendLine("          \"assetPath\": " + EscapeJsonVal(issue.AssetPath) + ",");
                        sb.AppendLine("          \"description\": " + EscapeJsonVal(issue.Description));
                        sb.AppendLine("        }" + icomma);
                    }
                    sb.AppendLine("      ]");
                    sb.AppendLine("    }" + comma);
                }
            }

            sb.AppendLine("  ],");
            sb.AppendLine("  \"textSummary\": " + EscapeJsonVal(BuildTextSummary(result)));
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static string FormatCategoryList(List<IUnityScannerCategory> categories)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"categories\": [");
            for (var i = 0; i < categories.Count; i++)
            {
                var cat = categories[i];
                var comma = i < categories.Count - 1 ? "," : "";
                sb.AppendLine("    {");
                sb.AppendLine("      \"id\": " + EscapeJsonVal(cat.Id) + ",");
                sb.AppendLine("      \"name\": " + EscapeJsonVal(cat.DisplayName) + ",");
                sb.AppendLine("      \"enabled\": " + cat.Settings.Enabled.ToString().ToLowerInvariant());
                sb.AppendLine("    }" + comma);
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static string FormatSettings(UnityScannerSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"activePlatformProfile\": " + EscapeJsonVal(settings.ActivePlatformProfileId) + ",");
            sb.AppendLine("  \"profiles\": [\"mobile\", \"console\", \"desktop\"],");
            sb.AppendLine("  \"yieldAssetThreshold\": " + settings.YieldAssetThreshold + ",");
            sb.AppendLine("  \"yieldIntervalDivisor\": " + settings.YieldIntervalDivisor);
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static string FormatProfileSet(string profileId)
        {
            return "{ \"success\": true, \"profile\": " + EscapeJsonVal(profileId) + " }";
        }

        public static string FormatError(string message)
        {
            return "{ \"success\": false, \"error\": " + EscapeJsonVal(message) + " }";
        }

        public static string FormatBaseline(BatchResult result, string baselinePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"success\": true,");
            sb.AppendLine("  \"baselinePath\": " + EscapeJsonVal(baselinePath) + ",");
            sb.AppendLine("  \"totalIssues\": " + result.Issues.Count + ",");
            sb.AppendLine("  \"categories\": " + (result.Results?.Count ?? 0));
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static string FormatRegression(BatchResult result, string textSummary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"success\": true,");
            sb.AppendLine("  \"exitCode\": " + result.ExitCode + ",");
            sb.AppendLine("  \"totalIssues\": " + result.Issues.Count + ",");
            sb.AppendLine("  \"textSummary\": " + EscapeJsonVal(textSummary));
            sb.AppendLine("}");
            return sb.ToString();
        }

        internal static string BuildTextSummary(BatchResult result)
        {
            var sb = new StringBuilder();
            var errors = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
            var warns = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);
            var infos = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Info);

            sb.AppendLine("UnityScanner Results:");
            sb.AppendLine("  Total: " + result.Issues.Count + " issues (" + errors + " errors, " + warns + " warnings, " + infos + " info)");
            sb.AppendLine("  Duration: " + result.TotalDurationMs.ToString("F0") + "ms");
            sb.AppendLine("  Categories scanned: " + (result.Results?.Count ?? 0));

            if (result.Results != null)
            {
                foreach (var r in result.Results)
                {
                    if (!r.Succeeded)
                    {
                        sb.AppendLine("  [" + r.DisplayName + "] FAILED: " + r.ErrorMessage);
                        continue;
                    }
                    if (r.Issues.Count == 0) continue;
                    sb.AppendLine("  [" + r.DisplayName + "] " + r.Issues.Count + " issues:");
                    foreach (var issue in r.Issues.Take(5))
                        sb.AppendLine("    - " + issue.Severity + ": " + issue.Description);
                    if (r.Issues.Count > 5)
                        sb.AppendLine("    ... and " + (r.Issues.Count - 5) + " more");
                }
            }

            return sb.ToString();
        }

        private static string EscapeJsonVal(string value)
        {
            if (value == null) return "null";
            return "\"" + EscapeJson(value) + "\"";
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.AppendFormat("\\u{0:X4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}