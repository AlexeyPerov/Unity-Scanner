using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;

namespace UnityScanner.Categories.ShaderAnalysis
{
    public class ShaderAnalysisCategory : IUnityScannerCategory
    {
        public string Id => "shader_analysis";
        public string DisplayName => "Shaders";
        public string ShortDisplayName => DisplayName;
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.ScanFiltered |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly ShaderAnalysisSettings _settings = new ShaderAnalysisSettings();

        public List<ShaderData> LastShaders { get; private set; }
        public List<MaterialData> LastMaterials { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var settings = _settings;
            var profile = context?.Settings?.ActivePlatformProfile;

            if (context?.Settings != null &&
                !string.IsNullOrEmpty(context.Settings.ActivePlatformProfileId))
            {
                profile = context.Settings.ActivePlatformProfile;
            }

            ApplyBatchOverrides(context, settings);

            issueSink.ReportProgress(0f, "Scanning shaders and materials...");
            yield return null;

            var yieldInterval = USCoroutineHelper.ComputeYieldInterval(
                AssetDatabase.GetAllAssetPaths().Length,
                context?.Settings?.YieldAssetThreshold ?? 5000,
                context?.Settings?.YieldIntervalDivisor ?? 10);

            var shaders = new List<ShaderData>();
            var materials = new List<MaterialData>();

            var enumerator = ShaderAnalysisScanner.ScanAll(settings, profile, shaders, materials, issueSink, yieldInterval);
            while (enumerator.MoveNext())
                yield return enumerator.Current;

            issueSink.ReportProgress(0.9f, "Mapping issues...");
            yield return null;

            var issues = ShaderAnalysisIssueMapper.MapIssues(shaders, materials, settings, profile);
            issueSink.AddRange(issues);

            LastShaders = shaders;
            LastMaterials = materials;

            var errorShaders = shaders.Count(s => s.IsErrorShader);
            var overThreshold = shaders.Count(s => s.VariantCount > settings.VariantThreshold);

            OutputDescription = $"Shaders: {shaders.Count}. Materials: {materials.Count}. " +
                                $"Error shaders: {errorShaders}. Over variant threshold: {overThreshold}.";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }

        private void ApplyBatchOverrides(UnityScannerScanContext context, ShaderAnalysisSettings settings)
        {
            // Batch overrides are applied through BatchOptions -> context.Settings
            // The settings thresholds can be overridden by platform profile
        }
    }
}
