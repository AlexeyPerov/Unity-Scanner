using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.Addressables
{
    public static class AddressablesIssueMapper
    {
        public const string CodeBuiltInDependsOnRemote = "built_in_depends_on_remote";
        public const string CodeCircularDependency = "circular_dependency";
        public const string CodeTransitiveRemoteDependency = "transitive_remote_dependency";
        public const string CodeStartupRemoteOverload = "startup_remote_overload";
        public const string CodeDuplicateAsset = "duplicate_asset";
        public const string CodeBuiltinAssetConflict = "builtin_asset_conflict";
        public const string CodeEmptyBundle = "empty_bundle";
        public const string CodeGateFailed = "gate_failed";
        public const string CodeParseFailure = "parse_failure";
        public const string CodeComparisonRegression = "comparison_regression";

        public static List<UnityScannerIssue> MapIssues(AddressablesScanResult result)
        {
            var issues = new List<UnityScannerIssue>();
            if (result?.Layout == null)
                return issues;

            var layout = result.Layout;

            foreach (var group in layout.Groups)
            {
                foreach (var bundle in group.Archives)
                {
                    foreach (var rec in bundle.Recommendations)
                    {
                        var code = ClassifyRecommendation(rec.Message);
                        var severity = MapWarningLevel(rec.WarningLevel);

                        issues.Add(MakeIssue(code,
                            rec.Message,
                            severity,
                            bundle.Name,
                            group.Name));
                    }
                }
            }

            foreach (var dup in result.Duplicates)
            {
                issues.Add(MakeIssue(CodeDuplicateAsset,
                    $"Duplicate asset '{dup.AssetPath}' in {dup.Bundles.Count} bundles, wasted: {dup.WastedSize} bytes. {dup.SuggestedFix}",
                    UnityScannerIssueSeverity.Warning,
                    dup.AssetPath));
            }

            foreach (var gate in result.GateResults)
            {
                if (!gate.Pass)
                {
                    issues.Add(MakeIssue(CodeGateFailed,
                        $"Gate '{gate.Name}' FAILED: {gate.FormattedActual} exceeds threshold",
                        UnityScannerIssueSeverity.Warning,
                        ""));
                }
            }

            return issues;
        }

        private static string ClassifyRecommendation(string message)
        {
            if (message.Contains("CIRCULAR"))
                return CodeCircularDependency;
            if (message.Contains("directly (!) references remote"))
                return CodeBuiltInDependsOnRemote;
            if (message.Contains("references remote"))
                return CodeTransitiveRemoteDependency;
            if (message.Contains("Startup remote"))
                return CodeStartupRemoteOverload;
            if (message.Contains("builtin asset"))
                return CodeBuiltinAssetConflict;
            if (message.Contains("contains no assets"))
                return CodeEmptyBundle;
            return CodeBuiltinAssetConflict;
        }

        private static UnityScannerIssueSeverity MapWarningLevel(int level)
        {
            if (level >= 4) return UnityScannerIssueSeverity.Error;
            if (level >= 2) return UnityScannerIssueSeverity.Warning;
            if (level >= 1) return UnityScannerIssueSeverity.Info;
            return UnityScannerIssueSeverity.Verbose;
        }

        private static UnityScannerIssue MakeIssue(string code, string description,
            UnityScannerIssueSeverity severity, string assetPath, string groupName = null)
        {
            var issue = new UnityScannerIssue
            {
                CategoryId = "addressables",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };

            if (groupName != null)
                issue.Metadata["group"] = groupName;

            return issue;
        }
    }
}
