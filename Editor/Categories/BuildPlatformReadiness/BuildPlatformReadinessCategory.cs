using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;

namespace UnityScanner.Categories.BuildPlatformReadiness
{
    public class BuildPlatformReadinessCategory : IUnityScannerCategory
    {
        public string Id => "build_platform_readiness";
        public string DisplayName => "Build Platform Readiness";
        public string ShortDisplayName => "Platform";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly BuildPlatformReadinessSettings _settings = new BuildPlatformReadinessSettings();

        public List<ImportPolicyViolation> LastViolations { get; private set; }
        public List<PlatformIncompatibility> LastIncompatibilities { get; private set; }
        public List<StrippingRisk> LastStrippingRisks { get; private set; }
        public List<StartupBudgetStatus> LastBudgetStatuses { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var settings = _settings;
            var profile = context?.Settings?.ActivePlatformProfile;

            issueSink.ReportProgress(0f, "Scanning build/platform readiness...");
            yield return null;

            var yieldInterval = USCoroutineHelper.ComputeYieldInterval(
                AssetDatabase.GetAllAssetPaths().Length,
                context?.Settings?.YieldAssetThreshold ?? 5000,
                context?.Settings?.YieldIntervalDivisor ?? 10);

            var violations = new List<ImportPolicyViolation>();
            var incompatibilities = new List<PlatformIncompatibility>();
            var strippingRisks = new List<StrippingRisk>();
            var budgetStatuses = new List<StartupBudgetStatus>();

            var enumerator = BuildPlatformReadinessScanner.ScanAll(
                settings, profile, violations, incompatibilities, strippingRisks, budgetStatuses, issueSink, yieldInterval);
            while (enumerator.MoveNext())
                yield return enumerator.Current;

            issueSink.ReportProgress(0.95f, "Mapping issues...");
            yield return null;

            var issues = BuildPlatformReadinessIssueMapper.MapIssues(
                violations, incompatibilities, strippingRisks, budgetStatuses, settings, profile);
            issueSink.AddRange(issues);

            LastViolations = violations;
            LastIncompatibilities = incompatibilities;
            LastStrippingRisks = strippingRisks;
            LastBudgetStatuses = budgetStatuses;

            var total = violations.Count + incompatibilities.Count + strippingRisks.Count + budgetStatuses.Count;
            var errors = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
            var warns = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);

            OutputDescription = "Profile: " + (profile?.DisplayName ?? "None") + ". Violations: " + total + ". Errors: " + errors + ". Warnings: " + warns + ".";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
