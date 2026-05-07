using System.Collections.Generic;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.Textures
{
    public static class TexturesIssueMapper
    {
        public const string CodeDuplicateInAtlas = "duplicate_in_atlas";
        public const string CodeDuplicateInAddressables = "duplicate_in_addressables";
        public const string CodeDuplicateInResources = "duplicate_in_resources";
        public const string CodeAmbiguousAtlasLink = "ambiguous_atlas_link";
        public const string CodeAmbiguousAtlasPackables = "ambiguous_atlas_packables";
        public const string CodeAtlasEmptyPackables = "atlas_empty_packables";
        public const string CodeAtlasNoSprites = "atlas_no_sprites";
        public const string CodeAtlasBundleMismatch = "atlas_bundle_mismatch";
        public const string CodeAtlasCompressionIssue = "atlas_compression_issue";
        public const string CodeAtlasRecommendedFormat = "atlas_recommended_format";
        public const string CodeAtlasRecommendedQuality = "atlas_recommended_quality";
        public const string CodeAtlasMipmap = "atlas_mipmap";
        public const string CodeAtlasAutomaticCompression = "atlas_automatic_compression";
        public const string CodeAtlasDefaultImporter = "atlas_default_importer";
        public const string CodeDimensionsFallback = "dimensions_fallback";
        public const string CodeSizeOver4K = "size_over_4k";
        public const string CodeImporterFailed = "importer_failed";
        public const string CodeMipmapEnabled = "mipmap_enabled";
        public const string CodeReadableTexture = "readable_texture";
        public const string CodeAutomaticCompression = "automatic_compression";
        public const string CodeRecommendedFormat = "recommended_format";
        public const string CodeRecommendedQuality = "recommended_quality";
        public const string CodeCrunchNonMultiple4 = "crunch_non_multiple_4";
        public const string CodePvrtcNonPot = "pvrtc_non_pot";
        public const string CodeDoubleCompression = "double_compression";

        public static List<UnityScannerIssue> MapAtlasIssues(List<AtlasData> atlases, TexturesSettings settings)
        {
            var issues = new List<UnityScannerIssue>();

            foreach (var atlas in atlases)
            {
                if (atlas.CustomWarnings == null) continue;

                foreach (var warning in atlas.CustomWarnings)
                {
                    var issue = MapAtlasWarning(warning, atlas);
                    if (issue != null)
                        issues.Add(issue);
                }
            }

            return issues;
        }

        public static List<UnityScannerIssue> MapTextureIssues(List<TextureData> textures, TexturesSettings settings)
        {
            var issues = new List<UnityScannerIssue>();

            foreach (var tex in textures)
            {
                if (tex.CustomWarnings == null) continue;

                foreach (var warning in tex.CustomWarnings)
                {
                    var issue = MapTextureWarning(warning, tex);
                    if (issue != null)
                        issues.Add(issue);
                }
            }

            return issues;
        }

        private static UnityScannerIssue MapAtlasWarning(string warning, AtlasData atlas)
        {
            if (warning.Contains("ambiguous packables"))
                return MakeIssue(CodeAmbiguousAtlasPackables, warning, UnityScannerIssueSeverity.Error, atlas.Path);

            if (warning.Contains("empty"))
                return MakeIssue(CodeAtlasEmptyPackables, warning, UnityScannerIssueSeverity.Error, atlas.Path);

            if (warning.Contains("Unable to detect sprites"))
                return MakeIssue(CodeAtlasNoSprites, warning, UnityScannerIssueSeverity.Info, atlas.Path);

            if (warning.Contains("different bundles"))
                return MakeIssue(CodeAtlasBundleMismatch, warning, UnityScannerIssueSeverity.Warning, atlas.Path);

            if (warning.Contains("recommended compression"))
                return MakeIssue(CodeAtlasRecommendedFormat, warning, UnityScannerIssueSeverity.Warning, atlas.Path);

            if (warning.Contains("recommended quality"))
                return MakeIssue(CodeAtlasRecommendedQuality, warning, UnityScannerIssueSeverity.Warning, atlas.Path);

            if (warning.Contains("Mipmap is enabled"))
                return MakeIssue(CodeAtlasMipmap, warning, UnityScannerIssueSeverity.Warning, atlas.Path);

            if (warning.Contains("Automatic compression"))
                return MakeIssue(CodeAtlasAutomaticCompression, warning, UnityScannerIssueSeverity.Warning, atlas.Path);

            if (warning.Contains("Unable to retrieve default"))
                return MakeIssue(CodeAtlasDefaultImporter, warning, UnityScannerIssueSeverity.Error, atlas.Path);

            return null;
        }

        private static UnityScannerIssue MapTextureWarning(string warning, TextureData tex)
        {
            if (warning.StartsWith("Duplicate in atlas"))
                return MakeIssue(CodeDuplicateInAtlas, warning, UnityScannerIssueSeverity.Error, tex.Path);

            if (warning.Contains("addressable and in atlas"))
                return MakeIssue(CodeDuplicateInAddressables, warning, UnityScannerIssueSeverity.Info, tex.Path);

            if (warning.Contains("Resources and in atlas"))
                return MakeIssue(CodeDuplicateInResources, warning, UnityScannerIssueSeverity.Error, tex.Path);

            if (warning.Contains("ambiguous"))
                return MakeIssue(CodeAmbiguousAtlasLink, warning, UnityScannerIssueSeverity.Error, tex.Path);

            if (warning.Contains("double-compression"))
                return MakeIssue(CodeDoubleCompression, warning, UnityScannerIssueSeverity.Info, tex.Path);

            if (warning.Contains("neither POT nor multiple of 4"))
                return MakeIssue(CodeDimensionsFallback, warning, UnityScannerIssueSeverity.Warning, tex.Path);

            if (warning.Contains("Size over 4096"))
                return MakeIssue(CodeSizeOver4K, warning, UnityScannerIssueSeverity.Warning, tex.Path);

            if (warning.Contains("Unable to load an importer"))
                return MakeIssue(CodeImporterFailed, warning, UnityScannerIssueSeverity.Error, tex.Path);

            if (warning.Contains("Mipmap is enabled"))
                return MakeIssue(CodeMipmapEnabled, warning, UnityScannerIssueSeverity.Warning, tex.Path);

            if (warning.Contains("readable"))
                return MakeIssue(CodeReadableTexture, warning, UnityScannerIssueSeverity.Warning, tex.Path);

            if (warning.Contains("Automatic compression"))
                return MakeIssue(CodeAutomaticCompression, warning, UnityScannerIssueSeverity.Warning, tex.Path);

            if (warning.Contains("recommended compression"))
                return MakeIssue(CodeRecommendedFormat, warning, UnityScannerIssueSeverity.Warning, tex.Path);

            if (warning.Contains("recommended quality"))
                return MakeIssue(CodeRecommendedQuality, warning, UnityScannerIssueSeverity.Warning, tex.Path);

            if (warning.Contains("multiple of 4") && warning.Contains("crunch"))
                return MakeIssue(CodeCrunchNonMultiple4, warning, UnityScannerIssueSeverity.Error, tex.Path);

            if (warning.Contains("POT") && warning.Contains("PVRTC"))
                return MakeIssue(CodePvrtcNonPot, warning, UnityScannerIssueSeverity.Error, tex.Path);

            return null;
        }

        private static UnityScannerIssue MakeIssue(string code, string description,
            UnityScannerIssueSeverity severity, string assetPath)
        {
            return new UnityScannerIssue
            {
                CategoryId = "textures",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };
        }
    }
}
