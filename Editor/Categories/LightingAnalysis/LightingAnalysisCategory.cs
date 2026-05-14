using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;

namespace UnityScanner.Categories.LightingAnalysis
{
    public class LightingAnalysisCategory : IUnityScannerCategory
    {
        public string Id => "lighting_analysis";
        public string DisplayName => "Lighting & Baking Analysis";
        public string ShortDisplayName => "Lighting";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly LightingAnalysisSettings _settings = new LightingAnalysisSettings();

        public List<SceneLightingData> LastResults { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var profile = context?.Settings?.ActivePlatformProfile;

            issueSink.ReportProgress(0f, "Scanning lighting...");
            yield return null;

            var yieldInterval = USCoroutineHelper.ComputeYieldInterval(
                AssetDatabase.GetAllAssetPaths().Length,
                context?.Settings?.YieldAssetThreshold ?? 5000,
                context?.Settings?.YieldIntervalDivisor ?? 10);

            var results = new List<SceneLightingData>();
            var enumerator = LightingAnalysisScanner.ScanAll(_settings, profile, results, issueSink, yieldInterval);
            while (enumerator.MoveNext())
                yield return enumerator.Current;

            issueSink.ReportProgress(0.95f, "Mapping issues...");
            yield return null;

            var issues = LightingAnalysisIssueMapper.MapIssues(results, _settings, profile);
            issueSink.AddRange(issues);

            LastResults = results;

            var totalScenes = results.Count;
            var totalLights = results.Sum(r => r.TotalLightCount);
            var errors = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
            var warns = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);

            OutputDescription = "Scenes: " + totalScenes + ". Lights: " + totalLights + ". Issues: " + (errors + warns) + ".";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
