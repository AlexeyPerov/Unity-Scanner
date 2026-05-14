using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityEditor;

namespace UnityScanner.Categories.AsmDefAudit
{
    public class AsmDefAuditCategory : IUnityScannerCategory
    {
        public string Id => "asmdef_audit";
        public string DisplayName => "Assembly Definition Audit";
        public string ShortDisplayName => "ASM";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly AsmDefAuditSettings _settings = new AsmDefAuditSettings();

        public List<AsmDefData> LastResults { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            issueSink.ReportProgress(0f, "Scanning assembly definitions...");
            yield return null;

            var yieldInterval = USCoroutineHelper.ComputeYieldInterval(
                AssetDatabase.GetAllAssetPaths().Length,
                context?.Settings?.YieldAssetThreshold ?? 5000,
                context?.Settings?.YieldIntervalDivisor ?? 10);

            var results = new List<AsmDefData>();
            var enumerator = AsmDefAuditScanner.ScanAll(_settings, results, issueSink, yieldInterval);
            while (enumerator.MoveNext())
                yield return enumerator.Current;

            issueSink.ReportProgress(0.9f, "Mapping issues...");
            yield return null;

            var issues = AsmDefAuditIssueMapper.MapIssues(results, _settings);
            issueSink.AddRange(issues);

            LastResults = results;

            var errors = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
            var warns = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);

            OutputDescription = "Assembly definitions: " + results.Count + ". Issues: " + (errors + warns) + " (" + errors + " errors, " + warns + " warnings).";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
