using System.Collections.Generic;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.ProjectHealth
{
    public static class ProjectHealthIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(
            List<ProjectHealthEntry> results,
            ProjectHealthSettings settings)
        {
            var issues = new List<UnityScannerIssue>();

            foreach (var entry in results)
            {
                var (code, severity) = entry.IssueType switch
                {
                    ProjectHealthIssueType.EmptyFolder => ("project_empty_folder", UnityScannerIssueSeverity.Info),
                    ProjectHealthIssueType.MetaOnlyFolder => ("project_meta_only_folder", UnityScannerIssueSeverity.Info),
                    ProjectHealthIssueType.OrphanedMeta => ("project_orphaned_meta", UnityScannerIssueSeverity.Warning),
                    ProjectHealthIssueType.BrokenAsset => ("project_broken_asset", UnityScannerIssueSeverity.Error),
                    ProjectHealthIssueType.EmptyScene => ("project_empty_scene", UnityScannerIssueSeverity.Info),
                    ProjectHealthIssueType.DeepNesting => ("project_deep_nesting", UnityScannerIssueSeverity.Info),
                    ProjectHealthIssueType.LargeFolder => ("project_large_folder", UnityScannerIssueSeverity.Info),
                    _ => ("project_unknown", UnityScannerIssueSeverity.Info)
                };

                issues.Add(MakeIssue(code, entry.Detail, severity, entry.Path,
                    "IssueType", entry.IssueType.ToString(),
                    "FileSizeBytes", entry.FileSizeBytes));
            }

            return issues;
        }

        private static UnityScannerIssue MakeIssue(
            string code, string description, UnityScannerIssueSeverity severity,
            string assetPath, params object[] metadataPairs)
        {
            var issue = new UnityScannerIssue
            {
                CategoryId = "project_health",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };

            for (var i = 0; i + 1 < metadataPairs.Length; i += 2)
            {
                if (metadataPairs[i] is string key)
                    issue.Metadata[key] = metadataPairs[i + 1];
            }

            return issue;
        }
    }
}
