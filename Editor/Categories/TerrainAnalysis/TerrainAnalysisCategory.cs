using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;

namespace UnityScanner.Categories.TerrainAnalysis
{
    public class TerrainAnalysisCategory : IUnityScannerCategory
    {
        public string Id => "terrain_analysis";
        public string DisplayName => "Terrain";
        public string ShortDisplayName => DisplayName;
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.ScanFiltered |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly TerrainAnalysisSettings _settings = new TerrainAnalysisSettings();

        public List<TerrainDataInfo> LastTerrains { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var settings = _settings;
            var profile = context?.Settings?.ActivePlatformProfile;

            issueSink.ReportProgress(0f, "Scanning terrains...");
            yield return null;

            var yieldInterval = USCoroutineHelper.ComputeYieldInterval(
                AssetDatabase.GetAllAssetPaths().Length,
                context?.Settings?.YieldAssetThreshold ?? 5000,
                context?.Settings?.YieldIntervalDivisor ?? 10);

            var terrains = new List<TerrainDataInfo>();
            var enumerator = TerrainAnalysisScanner.ScanAll(settings, profile, terrains, issueSink, yieldInterval);
            while (enumerator.MoveNext())
                yield return enumerator.Current;

            issueSink.ReportProgress(0.9f, "Mapping issues...");
            yield return null;

            var issues = TerrainAnalysisIssueMapper.MapIssues(terrains, settings, profile);
            issueSink.AddRange(issues);

            LastTerrains = terrains;

            var errorCount = terrains.Count(t => t.WarningLevel >= 3);
            var warnCount = terrains.Count(t => t.WarningLevel >= 1 && t.WarningLevel < 3);

            OutputDescription = $"Terrains: {terrains.Count}. Errors: {errorCount}. Warnings: {warnCount}.";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
