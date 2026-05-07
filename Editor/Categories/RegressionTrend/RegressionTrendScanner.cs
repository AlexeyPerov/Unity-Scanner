using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Results;

namespace UnityScanner.Categories.RegressionTrend
{
    public static class RegressionTrendScanner
    {
        public static BaselineSnapshot CreateBaseline(List<UnityScannerResult> results, string platformProfile)
        {
            var snapshot = new BaselineSnapshot
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                UnityVersion = UnityEngine.Application.unityVersion,
                PlatformProfile = platformProfile ?? ""
            };

            foreach (var result in results)
            {
                snapshot.Categories.Add(new CategoryBaseline
                {
                    CategoryId = result.CategoryId,
                    DisplayName = result.DisplayName,
                    TotalIssues = result.Issues.Count,
                    Errors = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error),
                    Warnings = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning),
                    Infos = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Info),
                    Verboses = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Verbose),
                    ScanDurationMs = result.ScanDurationMs
                });
            }

            return snapshot;
        }

        public static string SerializeBaseline(BaselineSnapshot snapshot)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"timestamp\": \"{Escape(snapshot.Timestamp)}\",");
            sb.AppendLine($"  \"unityVersion\": \"{Escape(snapshot.UnityVersion)}\",");
            sb.AppendLine($"  \"platformProfile\": \"{Escape(snapshot.PlatformProfile)}\",");
            sb.AppendLine("  \"categories\": [");
            for (var i = 0; i < snapshot.Categories.Count; i++)
            {
                var c = snapshot.Categories[i];
                var comma = i < snapshot.Categories.Count - 1 ? "," : "";
                sb.AppendLine("    {");
                sb.AppendLine($"      \"categoryId\": \"{Escape(c.CategoryId)}\",");
                sb.AppendLine($"      \"displayName\": \"{Escape(c.DisplayName)}\",");
                sb.AppendLine($"      \"totalIssues\": {c.TotalIssues},");
                sb.AppendLine($"      \"errors\": {c.Errors},");
                sb.AppendLine($"      \"warnings\": {c.Warnings},");
                sb.AppendLine($"      \"infos\": {c.Infos},");
                sb.AppendLine($"      \"verboses\": {c.Verboses},");
                sb.AppendLine($"      \"scanDurationMs\": {c.ScanDurationMs:F1}");
                sb.AppendLine($"    }}{comma}");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static BaselineSnapshot DeserializeBaseline(string json)
        {
            var snapshot = new BaselineSnapshot();
            var lines = json.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("\"timestamp\""))
                    snapshot.Timestamp = ExtractValue(trimmed);
                else if (trimmed.StartsWith("\"unityVersion\""))
                    snapshot.UnityVersion = ExtractValue(trimmed);
                else if (trimmed.StartsWith("\"platformProfile\""))
                    snapshot.PlatformProfile = ExtractValue(trimmed);
                else if (trimmed.StartsWith("\"categoryId\""))
                {
                    var cat = new CategoryBaseline
                    {
                        CategoryId = ExtractValue(trimmed),
                        DisplayName = ExtractNamedValue(lines, trimmed, "displayName"),
                        TotalIssues = ExtractInt(lines, trimmed, "totalIssues"),
                        Errors = ExtractInt(lines, trimmed, "errors"),
                        Warnings = ExtractInt(lines, trimmed, "warnings"),
                        Infos = ExtractInt(lines, trimmed, "infos"),
                        Verboses = ExtractInt(lines, trimmed, "verboses")
                    };
                    snapshot.Categories.Add(cat);
                }
            }
            return snapshot;
        }

        public static List<CategoryComparison> Compare(BaselineSnapshot baseline, List<UnityScannerResult> currentResults)
        {
            var comparisons = new List<CategoryComparison>();
            var baselineMap = baseline.Categories.ToDictionary(c => c.CategoryId, c => c);

            foreach (var result in currentResults)
            {
                var baselineCategory = baselineMap.GetValueOrDefault(result.CategoryId);
                var currentErrors = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
                var currentWarnings = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);
                var currentInfos = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Info);
                var currentVerboses = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Verbose);

                comparisons.Add(new CategoryComparison
                {
                    CategoryId = result.CategoryId,
                    DisplayName = result.DisplayName,
                    BaselineErrors = baselineCategory?.Errors ?? 0,
                    BaselineWarnings = baselineCategory?.Warnings ?? 0,
                    BaselineInfos = baselineCategory?.Infos ?? 0,
                    BaselineVerboses = baselineCategory?.Verboses ?? 0,
                    CurrentErrors = currentErrors,
                    CurrentWarnings = currentWarnings,
                    CurrentInfos = currentInfos,
                    CurrentVerboses = currentVerboses
                });
            }

            foreach (var bCat in baseline.Categories)
            {
                if (comparisons.Any(c => c.CategoryId == bCat.CategoryId))
                {
                    continue;
                }
                comparisons.Add(new CategoryComparison
                {
                    CategoryId = bCat.CategoryId,
                    DisplayName = bCat.DisplayName,
                    BaselineErrors = bCat.Errors,
                    BaselineWarnings = bCat.Warnings,
                    BaselineInfos = bCat.Infos,
                    BaselineVerboses = bCat.Verboses,
                    CurrentErrors = 0,
                    CurrentWarnings = 0,
                    CurrentInfos = 0,
                    CurrentVerboses = 0
                });
            }

            return comparisons;
        }

        public static void SaveBaseline(BaselineSnapshot snapshot, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, SerializeBaseline(snapshot));
        }

        public static BaselineSnapshot LoadBaseline(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return DeserializeBaseline(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ExtractValue(string line)
        {
            var idx = line.IndexOf(':');
            if (idx < 0) return "";
            var val = line.Substring(idx + 1).Trim().TrimEnd(',').Trim().Trim('"');
            return val.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        private static string ExtractNamedValue(string[] lines, string currentLine, string name)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i] == currentLine && i + 1 < lines.Length)
                {
                    var next = lines[i + 1].Trim();
                    if (next.StartsWith("\"" + name + "\""))
                        return ExtractValue(next);
                }
            }
            return "";
        }

        private static int ExtractInt(string[] lines, string currentLine, string name)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (lines[i].Contains("\"" + name + "\"") && IsNearLine(lines, i, currentLine))
                {
                    var idx = trimmed.IndexOf(':');
                    if (idx >= 0)
                    {
                        var val = trimmed.Substring(idx + 1).Trim().TrimEnd(',').Trim();
                        if (int.TryParse(val, out var result)) return result;
                    }
                }
            }
            return 0;
        }

        private static bool IsNearLine(string[] lines, int checkIdx, string targetLine)
        {
            for (var i = Math.Max(0, checkIdx - 8); i <= Math.Min(lines.Length - 1, checkIdx + 8); i++)
            {
                if (lines[i].Trim() == targetLine.Trim()) return true;
            }
            return false;
        }
    }
}
