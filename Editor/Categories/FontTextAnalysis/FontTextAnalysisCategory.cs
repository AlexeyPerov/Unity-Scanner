using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;

namespace UnityScanner.Categories.FontTextAnalysis
{
    public class FontTextAnalysisCategory : IUnityScannerCategory
    {
        public string Id => "font_text_analysis";
        public string DisplayName => "Font & Text";
        public string ShortDisplayName => "Text";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.ScanFiltered |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly FontTextAnalysisSettings _settings = new FontTextAnalysisSettings();

        public List<FontAssetData> LastFonts { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var settings = _settings;
            var profile = context?.Settings?.ActivePlatformProfile;

            issueSink.ReportProgress(0f, "Scanning fonts and text assets...");
            yield return null;

            var yieldInterval = USCoroutineHelper.ComputeYieldInterval(
                AssetDatabase.GetAllAssetPaths().Length,
                context?.Settings?.YieldAssetThreshold ?? 5000,
                context?.Settings?.YieldIntervalDivisor ?? 10);

            var fonts = new List<FontAssetData>();
            var enumerator = FontTextAnalysisScanner.ScanAll(settings, profile, fonts, issueSink, yieldInterval);
            while (enumerator.MoveNext())
                yield return enumerator.Current;

            issueSink.ReportProgress(0.9f, "Mapping issues...");
            yield return null;

            var issues = FontTextAnalysisIssueMapper.MapIssues(fonts, settings, profile);
            issueSink.AddRange(issues);

            LastFonts = fonts;

            var tmpCount = fonts.Count(f => f.IsTmpFont);
            var unityCount = fonts.Count - tmpCount;
            OutputDescription = $"Fonts: {fonts.Count} (TMP: {tmpCount}, Unity: {unityCount}).";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
