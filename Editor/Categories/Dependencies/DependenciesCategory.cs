using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityEditor;

namespace UnityScanner.Categories.Dependencies
{
    public class DependenciesCategory : IUnityScannerCategory
    {
        public string Id => "dependencies";
        public string DisplayName => "Dependencies";
        public string ShortDisplayName => DisplayName;
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll | ScanCapabilities.ScanSelected |
                                                ScanCapabilities.Export | ScanCapabilities.Fix |
                                                ScanCapabilities.Progress;

        private readonly DependenciesSettings _settings = new DependenciesSettings();
        public List<DependenciesAssetData> LastAssets { get; private set; }
        public Dictionary<string, int> RefsByTypes { get; private set; } = new Dictionary<string, int>();
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var settings = _settings;

            issueSink.ReportProgress(0f, "Scanning unreferenced assets...");
            yield return null;

            var yieldInterval = USCoroutineHelper.ComputeYieldInterval(
                AssetDatabase.GetAllAssetPaths().Length,
                context?.Settings?.YieldAssetThreshold ?? 5000,
                context?.Settings?.YieldIntervalDivisor ?? 10);

            LastAssets = new List<DependenciesAssetData>();
            var enumerator = DependenciesScanner.ScanUnreferencedAssets(settings, issueSink, LastAssets, yieldInterval);
            while (enumerator.MoveNext())
                yield return enumerator.Current;

            issueSink.ReportProgress(0.8f, "Mapping issues...");
            yield return null;

            RefsByTypes.Clear();
            foreach (var group in LastAssets.GroupBy(x => x.TypeName))
                RefsByTypes[group.Key] = group.Count();

            OutputDescription = BuildOutputDescription(LastAssets, settings);

            var issues = DependenciesIssueMapper.MapToIssues(LastAssets, settings);
            issueSink.AddRange(issues);

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }

        private static string BuildOutputDescription(List<DependenciesAssetData> assets, DependenciesSettings settings)
        {
            if (settings.TryUseReflectionForAddressablesDetection)
            {
                if (settings.FindUnreferencedOnly)
                {
                    var addrCount = assets.Count;
                    return $"Analysis Done. Unreferenced Assets: Total = {assets.Count}";
                }
                else
                {
                    var unrefTotal = assets.Count;
                    return $"Analysis Done. Assets: Total = {assets.Count} Unreferenced = {unrefTotal}";
                }
            }

            if (settings.FindUnreferencedOnly)
                return $"Analysis Done. Unreferenced Assets: {assets.Count}";

            return $"Analysis Done. Assets: Total = {assets.Count}";
        }
    }
}
