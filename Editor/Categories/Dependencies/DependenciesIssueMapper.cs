using System.Collections.Generic;
using System.IO;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.Dependencies
{
    public static class DependenciesIssueMapper
    {
        public const string CodeUnreferencedCandidate = "unreferenced_candidate";
        public const string CodeFalsePositive = "potential_false_positive";
        public const string CodeAddressableUnreferenced = "addressable_unreferenced";
        public const string CodeReflectionWarning = "addressables_reflection_warning";
        public const string CodeReflectionFailure = "reflection_failure";
        public const string CodeMetadataFailure = "metadata_failure";

        public static List<UnityScannerIssue> MapToIssues(List<DependenciesAssetData> assets, DependenciesSettings settings)
        {
            var issues = new List<UnityScannerIssue>();

            foreach (var asset in assets)
            {
                if (!string.IsNullOrEmpty(asset.FalsePositiveWarning))
                {
                    issues.Add(new UnityScannerIssue
                    {
                        CategoryId = "dependencies",
                        IssueCode = CodeFalsePositive,
                        Description = asset.FalsePositiveWarning,
                        Severity = UnityScannerIssueSeverity.Info,
                        AssetPath = asset.Path,
                        FixId = "none"
                    });
                    continue;
                }

                if (asset.ReferencesCount == 0)
                {
                    var severity = asset.IsAddressable
                        ? UnityScannerIssueSeverity.Info
                        : UnityScannerIssueSeverity.Warning;

                    var issueCode = asset.IsAddressable
                        ? CodeAddressableUnreferenced
                        : CodeUnreferencedCandidate;

                    var fileName = !string.IsNullOrEmpty(asset.Path)
                        ? Path.GetFileName(asset.Path)
                        : asset.TypeName;

                    issues.Add(new UnityScannerIssue
                    {
                        CategoryId = "dependencies",
                        IssueCode = issueCode,
                        Description = asset.IsAddressable
                            ? $"Unreferenced addressable asset: {fileName} ({asset.ReadableSize})"
                            : $"Unreferenced cleanup candidate: {fileName} ({asset.ReadableSize})",
                        Severity = severity,
                        AssetPath = asset.Path,
                        FixId = asset.IsEligibleForDeletion ? "delete" : "none"
                    });
                }
            }

            return issues;
        }
    }
}
