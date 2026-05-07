using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.ProjectHealth
{
    public class ProjectHealthCategory : IUnityScannerCategory
    {
        public string Id => "project_health";
        public string DisplayName => "Project Health";
        public string ShortDisplayName => "Project";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly ProjectHealthSettings _settings = new ProjectHealthSettings();

        public List<ProjectHealthEntry> LastResults { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            issueSink.ReportProgress(0f, "Scanning project health...");
            yield return null;

            var results = new List<ProjectHealthEntry>();
            ProjectHealthScanner.ScanAll(_settings, results, issueSink);

            issueSink.ReportProgress(0.95f, "Mapping issues...");
            yield return null;

            var issues = ProjectHealthIssueMapper.MapIssues(results, _settings);
            issueSink.AddRange(issues);

            LastResults = results;

            var errors = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
            var warns = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);
            var infos = issues.Count - errors - warns;

            OutputDescription = "Entries: " + results.Count + ". Issues: " + errors + " errors, " + warns + " warnings, " + infos + " info.";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}