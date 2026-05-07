using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Utilities.BuildLayout;

namespace UnityScanner.Categories.Materials
{
    public class MaterialsCategory : IUnityScannerCategory
    {
        public string Id => "materials";
        public string DisplayName => "Materials";
        public string ShortDisplayName => DisplayName;
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Fix |
                                                ScanCapabilities.Progress;

        private readonly MaterialsSettings _settings = new MaterialsSettings();

        public List<RendererComponentData> LastRenderers { get; private set; }
        public List<MaterialAssetData> LastMaterials { get; private set; }
        public Dictionary<string, int> ShaderUsageCounts { get; private set; } = new Dictionary<string, int>();
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var settings = _settings;
            USLiteBuildLayoutProvider buildLayout = null;
            if (context?.Settings != null)
            {
                var buildLayoutPath = context.Settings.BuildLayoutPath;
                if (!string.IsNullOrEmpty(buildLayoutPath))
                    buildLayout = USLiteBuildLayoutProvider.Load(buildLayoutPath);
            }

            issueSink.ReportProgress(0f, "Scanning renderers...");
            yield return null;

            LastRenderers = MaterialsScanner.ScanRenderers(settings, buildLayout, issueSink);

            issueSink.ReportProgress(0.4f, "Scanning materials...");
            yield return null;

            LastMaterials = MaterialsScanner.ScanMaterials(settings, buildLayout, LastRenderers, issueSink);

            issueSink.ReportProgress(0.8f, "Mapping issues...");
            yield return null;

            ShaderUsageCounts = LastMaterials
                .GroupBy(m => m.ShaderName ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            var rendererIssues = MaterialsIssueMapper.MapRendererIssues(LastRenderers, settings);
            issueSink.AddRange(rendererIssues);

            var materialIssues = MaterialsIssueMapper.MapMaterialIssues(LastMaterials, settings);
            issueSink.AddRange(materialIssues);

            OutputDescription = BuildOutputDescription(LastRenderers, LastMaterials);

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }

        private static string BuildOutputDescription(
            List<RendererComponentData> renderers,
            List<MaterialAssetData> materials)
        {
            var duplicateCount = materials.Count(m => m.DuplicatePaths != null && m.DuplicatePaths.Count > 0);
            var unusedCount = materials.Count(m =>
                m.CustomWarnings != null &&
                m.CustomWarnings.Any(w => w.StartsWith(MaterialsWarningMessages.NotReferencedPrefix)));
            var variantCount = materials.Count(m => m.IsVariant);
            var deepChainCount = materials.Count(m =>
                m.CustomWarnings != null &&
                m.CustomWarnings.Any(w => w.Contains(MaterialsWarningMessages.TokenVariantChainDepth)));
            var heavyOverrideCount = materials.Count(m =>
                m.CustomWarnings != null &&
                m.CustomWarnings.Any(w => w.Contains(MaterialsWarningMessages.TokenHeavyVariantOverrides)));
            var instancingWarn = materials.Count(m =>
                m.CustomWarnings != null &&
                m.CustomWarnings.Any(w => w.Contains(MaterialsWarningMessages.TokenGpuInstancingDisabled)));
            var srpWarn = materials.Count(m =>
                m.CustomWarnings != null &&
                m.CustomWarnings.Any(w => w.Contains(MaterialsWarningMessages.TokenSrpBatcher)));

            return $"Renderers: {renderers.Count}. Materials: {materials.Count}. " +
                   $"Duplicates: {duplicateCount}. Unused: {unusedCount}. " +
                   $"Variants: {variantCount}. DeepChains: {deepChainCount}. HeavyVarOverrides: {heavyOverrideCount}. " +
                   $"InstancingOff: {instancingWarn}. SRPBatcher: {srpWarn}";
        }
    }
}
