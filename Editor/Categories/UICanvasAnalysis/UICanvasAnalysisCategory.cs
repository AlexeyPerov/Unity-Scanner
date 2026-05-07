using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.UICanvasAnalysis
{
    public class UICanvasAnalysisCategory : IUnityScannerCategory
    {
        public string Id => "ui_canvas_analysis";
        public string DisplayName => "UI Canvas Analysis";
        public string ShortDisplayName => "Canvas";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly UICanvasAnalysisSettings _settings = new UICanvasAnalysisSettings();

        public List<CanvasData> LastResults { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var profile = context?.Settings?.ActivePlatformProfile;

            issueSink.ReportProgress(0f, "Scanning UI/Canvas...");
            yield return null;

            var results = new List<CanvasData>();
            UICanvasAnalysisScanner.ScanAll(_settings, profile, results, issueSink);

            issueSink.ReportProgress(0.95f, "Mapping issues...");
            yield return null;

            var issues = UICanvasAnalysisIssueMapper.MapIssues(results, _settings, profile);
            issueSink.AddRange(issues);

            LastResults = results;

            var totalCanvases = results.Count;
            var totalRaycasts = results.Sum(r => r.UnnecessaryRaycastCount);
            var errors = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
            var warns = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);

            OutputDescription = "Canvases: " + totalCanvases + ". Unnecessary raycasts: " + totalRaycasts + ". Issues: " + (errors + warns) + ".";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
