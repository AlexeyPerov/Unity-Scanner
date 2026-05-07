using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Results;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.RegressionTrend
{
    public class RegressionTrendCategory : IUnityScannerCategory
    {
        public string Id => "regression_trend";
        public string DisplayName => "Regression Trend";
        public string ShortDisplayName => "Trends";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly RegressionTrendSettings _settings = new RegressionTrendSettings();

        public List<CategoryComparison> LastComparisons { get; private set; }
        public BaselineSnapshot LastBaseline { get; private set; }
        public List<UnityScannerResult> LastResults { get; private set; }
        public string LastPlatformProfile { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            issueSink.ReportProgress(0f, "Loading baseline...");
            yield return null;

            var baselinePath = _settings.BaselinePath;
            if (string.IsNullOrEmpty(baselinePath))
                baselinePath = "Library/UnityScanner/baseline.json";

            LastBaseline = RegressionTrendScanner.LoadBaseline(baselinePath);

            issueSink.ReportProgress(0.3f, "Gathering current results...");
            yield return null;

            var results = context?.PreviousResults?.Results ?? new List<UnityScannerResult>();
            LastResults = results;
            LastPlatformProfile = context?.Settings?.ActivePlatformProfileId ?? "";

            issueSink.ReportProgress(0.5f, "Comparing with current results...");
            yield return null;

            if (LastBaseline != null)
            {
                LastComparisons = RegressionTrendScanner.Compare(LastBaseline, results);
                var issues = RegressionTrendIssueMapper.MapIssues(LastComparisons, _settings);
                issueSink.AddRange(issues);

                var regressed = LastComparisons.Count(c => c.HasRegression);
                var improved = LastComparisons.Count(c => c.HasImprovement);
                OutputDescription = $"Baseline: {LastBaseline.Timestamp}. Compared {LastComparisons.Count} categories. Regressed: {regressed}. Improved: {improved}.";
            }
            else
            {
                var snapshot = RegressionTrendScanner.CreateBaseline(results, context?.Settings?.ActivePlatformProfileId);
                RegressionTrendScanner.SaveBaseline(snapshot, baselinePath);
                LastComparisons = new List<CategoryComparison>();

                OutputDescription = $"No baseline found. Created new baseline at {baselinePath} with {results.Count} categories.";
            }

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }

        public void SaveBaseline(List<UnityScannerResult> results, string path, string platformProfile)
        {
            var snapshot = RegressionTrendScanner.CreateBaseline(results, platformProfile);
            RegressionTrendScanner.SaveBaseline(snapshot, path);
            LastBaseline = snapshot;
        }
    }
}
