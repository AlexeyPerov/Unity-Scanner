using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Utilities.BuildLayout;
using UnityEditor;

namespace UnityScanner.Categories.Textures
{
    public class TexturesCategory : IUnityScannerCategory
    {
        public string Id => "textures";
        public string DisplayName => "Textures Compression";
        public string ShortDisplayName => "Textures";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.ScanFiltered |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly TexturesSettings _settings = new TexturesSettings();

        public List<AtlasData> LastAtlases { get; private set; }
        public List<TextureData> LastTextures { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var settings = _settings;
            USLiteBuildLayoutProvider buildLayout = null;
            if (context?.Settings != null && !string.IsNullOrEmpty(context.Settings.BuildLayoutPath))
                buildLayout = USLiteBuildLayoutProvider.Load(context.Settings.BuildLayoutPath);

            issueSink.ReportProgress(0f, "Scanning textures and atlases...");
            yield return null;

            var yieldInterval = USCoroutineHelper.ComputeYieldInterval(
                AssetDatabase.GetAllAssetPaths().Length,
                context?.Settings?.YieldAssetThreshold ?? 5000,
                context?.Settings?.YieldIntervalDivisor ?? 10);

            var atlases = new List<AtlasData>();
            var textures = new List<TextureData>();
            var enumerator = TexturesScanner.ScanAll(settings, buildLayout, issueSink, atlases, textures, yieldInterval);
            while (enumerator.MoveNext())
                yield return enumerator.Current;

            LastAtlases = atlases;
            LastTextures = textures;

            issueSink.ReportProgress(0.8f, "Mapping issues...");
            yield return null;

            var atlasIssues = TexturesIssueMapper.MapAtlasIssues(atlases, settings);
            issueSink.AddRange(atlasIssues);

            var textureIssues = TexturesIssueMapper.MapTextureIssues(textures, settings);
            issueSink.AddRange(textureIssues);

            OutputDescription = $"Atlases: {atlases.Count}. Textures: {textures.Count}.";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
