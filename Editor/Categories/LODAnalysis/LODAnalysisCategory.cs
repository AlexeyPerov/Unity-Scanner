using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;

namespace UnityScanner.Categories.LODAnalysis
{
    public class LODAnalysisCategory : IUnityScannerCategory
    {
        public string Id => "lod_analysis";
        public string DisplayName => "LOD Analysis";
        public string ShortDisplayName => "LOD";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly LODAnalysisSettings _settings = new LODAnalysisSettings();

        public List<LODGroupData> LastResults { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var profile = context?.Settings?.ActivePlatformProfile;

            issueSink.ReportProgress(0f, "Scanning LOD groups...");
            yield return null;

            var yieldInterval = USCoroutineHelper.ComputeYieldInterval(
                AssetDatabase.GetAllAssetPaths().Length,
                context?.Settings?.YieldAssetThreshold ?? 5000,
                context?.Settings?.YieldIntervalDivisor ?? 10);

            var results = new List<LODGroupData>();
            var enumerator = LODAnalysisScanner.ScanAll(_settings, profile, results, issueSink, yieldInterval);
            while (enumerator.MoveNext())
                yield return enumerator.Current;

            issueSink.ReportProgress(0.95f, "Mapping issues...");
            yield return null;

            var issues = LODAnalysisIssueMapper.MapIssues(results, _settings, profile);
            issueSink.AddRange(issues);

            LastResults = results;

            var total = results.Count;
            var errors = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
            var warns = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);

            OutputDescription = "LOD groups: " + total + ". Issues: " + (errors + warns) + " (" + errors + " errors, " + warns + " warnings).";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
