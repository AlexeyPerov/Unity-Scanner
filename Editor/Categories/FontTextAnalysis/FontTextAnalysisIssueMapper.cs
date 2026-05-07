using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.FontTextAnalysis
{
    public static class FontTextAnalysisIssueMapper
    {
        public const string CodeAtlasGrowthRisk = "atlas_growth_risk";
        public const string CodeOversizedAtlas = "oversized_atlas";
        public const string CodeDeepFallbackChain = "deep_fallback_chain";
        public const string CodeDuplicateFallbackChains = "duplicate_fallback_chains";
        public const string CodeMissingFontAssignment = "missing_font_assignment";

        public static List<UnityScannerIssue> MapIssues(
            List<FontAssetData> fonts,
            FontTextAnalysisSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();
            var maxAtlas = profile?.MaxTmpAtlasSize ?? settings.MaxAtlasSize;
            var maxDepth = profile?.MaxFallbackChainDepth ?? settings.MaxFallbackChainDepth;

            foreach (var font in fonts)
            {
                if (!font.IsTmpFont) continue;

                if (settings.DetectAtlasGrowth && font.IsDynamic)
                {
                    issues.Add(MakeIssue(CodeAtlasGrowthRisk,
                        $"TMP font '{font.Name}' is dynamic and atlas may grow at runtime ({font.AtlasWidth}x{font.AtlasHeight}).",
                        UnityScannerIssueSeverity.Warning, font.Path,
                        "atlas_width", font.AtlasWidth,
                        "atlas_height", font.AtlasHeight));
                }

                if (settings.DetectOversizedAtlases && (font.AtlasWidth > maxAtlas || font.AtlasHeight > maxAtlas))
                {
                    issues.Add(MakeIssue(CodeOversizedAtlas,
                        $"TMP font '{font.Name}' atlas ({font.AtlasWidth}x{font.AtlasHeight}) exceeds budget ({maxAtlas}).",
                        UnityScannerIssueSeverity.Warning, font.Path,
                        "atlas_width", font.AtlasWidth,
                        "atlas_height", font.AtlasHeight,
                        "budget", maxAtlas));
                }

                if (settings.DetectDeepFallbackChains && font.FallbackChainDepth > maxDepth)
                {
                    issues.Add(MakeIssue(CodeDeepFallbackChain,
                        $"TMP font '{font.Name}' fallback chain depth ({font.FallbackChainDepth}) exceeds limit ({maxDepth}). Chain: {string.Join(" -> ", font.FallbackChainNames)}.",
                        UnityScannerIssueSeverity.Warning, font.Path,
                        "chain_depth", font.FallbackChainDepth,
                        "limit", maxDepth));
                }
            }

            if (settings.DetectDuplicateFallbackChains)
            {
                var duplicates = FontTextAnalysisScanner.DetectDuplicateFallbackChains(fonts);
                foreach (var group in duplicates)
                {
                    var names = group.Fonts.Select(f => f.Name).Take(5).ToList();
                    issues.Add(MakeIssue(CodeDuplicateFallbackChains,
                        $"{group.Fonts.Count} fonts share identical fallback chains: {string.Join(", ", names)}. These can be consolidated.",
                        UnityScannerIssueSeverity.Info, group.Fonts[0].Path,
                        "font_count", group.Fonts.Count));
                }
            }

            return issues;
        }

        private static UnityScannerIssue MakeIssue(
            string code, string description, UnityScannerIssueSeverity severity,
            string assetPath, params object[] metadataPairs)
        {
            var issue = new UnityScannerIssue
            {
                CategoryId = "font_text_analysis",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };
            if (metadataPairs != null)
            {
                for (var i = 0; i + 1 < metadataPairs.Length; i += 2)
                {
                    var key = metadataPairs[i]?.ToString();
                    if (key != null) issue.Metadata[key] = metadataPairs[i + 1];
                }
            }
            return issue;
        }
    }
}
