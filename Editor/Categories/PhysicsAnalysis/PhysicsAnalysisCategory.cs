using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;

namespace UnityScanner.Categories.PhysicsAnalysis
{
    public class PhysicsAnalysisCategory : IUnityScannerCategory
    {
        public string Id => "physics_analysis";
        public string DisplayName => "Physics Analysis";
        public string ShortDisplayName => "Physics";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly PhysicsAnalysisSettings _settings = new PhysicsAnalysisSettings();

        public List<ScenePhysicsData> LastResults { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var profile = context?.Settings?.ActivePlatformProfile;

            issueSink.ReportProgress(0f, "Scanning physics...");
            yield return null;

            var yieldInterval = USCoroutineHelper.ComputeYieldInterval(
                AssetDatabase.GetAllAssetPaths().Length,
                context?.Settings?.YieldAssetThreshold ?? 5000,
                context?.Settings?.YieldIntervalDivisor ?? 10);

            var results = new List<ScenePhysicsData>();
            var enumerator = PhysicsAnalysisScanner.ScanAll(_settings, profile, results, issueSink, yieldInterval);
            while (enumerator.MoveNext())
                yield return enumerator.Current;

            issueSink.ReportProgress(0.95f, "Mapping issues...");
            yield return null;

            var issues = PhysicsAnalysisIssueMapper.MapIssues(results, _settings, profile);
            issueSink.AddRange(issues);

            LastResults = results;

            var totalRb = results.Sum(r => r.RigidbodyCount);
            var totalCol = results.Sum(r => r.ColliderCount);
            var errors = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
            var warns = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);

            OutputDescription = "Scenes: " + results.Count + " | Rigidbodies: " + totalRb + " | Colliders: " + totalCol + ". Issues: " + (errors + warns) + ".";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
