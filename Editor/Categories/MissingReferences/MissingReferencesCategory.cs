using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityEditor;

namespace UnityScanner.Categories.MissingReferences
{
    public class MissingReferencesCategory : IUnityScannerCategory
    {
        public string Id => "missing_references";
        public string DisplayName => "Missing References";
        public string ShortDisplayName => "Missings";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll | ScanCapabilities.ScanFiltered |
                                                ScanCapabilities.Export | ScanCapabilities.Progress;

        private readonly MissingReferencesSettings _settings = new MissingReferencesSettings();
        public List<MissingRefAssetData> LastAssets { get; private set; }

        public Dictionary<string, int> FieldTypeCounters { get; private set; } = new Dictionary<string, int>();
        public int FieldTypeSum { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            issueSink.ReportProgress(0f, "Scanning for missing references...");
            yield return null;

            var yieldInterval = USCoroutineHelper.ComputeYieldInterval(
                AssetDatabase.GetAllAssetPaths().Length,
                context?.Settings?.YieldAssetThreshold ?? 5000,
                context?.Settings?.YieldIntervalDivisor ?? 10);

            LastAssets = new List<MissingRefAssetData>();
            var enumerator = MissingReferencesScanner.ScanAllAssets(_settings, issueSink, LastAssets, yieldInterval);
            while (enumerator.MoveNext())
                yield return enumerator.Current;

            issueSink.ReportProgress(0.8f, "Mapping issues...");
            yield return null;

            FieldTypeCounters.Clear();
            FieldTypeSum = 0;

            foreach (var asset in LastAssets)
            {
                foreach (var ft in asset.MissingFieldTypes)
                {
                    FieldTypeCounters.TryGetValue(ft, out var count);
                    FieldTypeCounters[ft] = count + 1;
                    FieldTypeSum++;
                }
            }

            var issues = MissingReferencesIssueMapper.MapToIssues(LastAssets);
            issueSink.AddRange(issues);

            var totalAssets = LastAssets.Count;
            var assetsWithWarnings = LastAssets.Count(a => a.RefsData.HasWarnings);
            OutputDescription = $"Analysis Done. Assets Scanned: {totalAssets}, Assets with Issues: {assetsWithWarnings}";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
