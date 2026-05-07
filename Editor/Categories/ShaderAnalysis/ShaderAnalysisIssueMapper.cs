using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;

namespace UnityScanner.Categories.ShaderAnalysis
{
    public static class ShaderAnalysisIssueMapper
    {
        public const string CodeErrorShader = "error_shader";
        public const string CodeVariantExplosion = "variant_explosion";
        public const string CodeExpensiveFeatureForPlatform = "expensive_feature_platform";
        public const string CodePlatformKeywordMismatch = "platform_keyword_mismatch";
        public const string CodeDuplicateKeywordProfiles = "duplicate_keyword_profiles";
        public const string CodePassCountExceeded = "pass_count_exceeded";
        public const string CodeFallbackShader = "fallback_shader";

        public static List<UnityScannerIssue> MapIssues(
            List<ShaderData> shaders,
            List<MaterialData> materials,
            ShaderAnalysisSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();

            var variantThreshold = settings.VariantThreshold;
            var passThreshold = settings.PassThreshold;
            var keywordThreshold = settings.KeywordThreshold;

            if (profile != null)
            {
                if (profile.ShaderVariantThreshold > 0)
                    variantThreshold = profile.ShaderVariantThreshold;
                if (profile.ShaderPassThreshold > 0)
                    passThreshold = profile.ShaderPassThreshold;
                if (profile.ShaderKeywordThreshold > 0)
                    keywordThreshold = profile.ShaderKeywordThreshold;
            }

            foreach (var shader in shaders)
            {
                if (shader.IsErrorShader)
                {
                    shader.AddCustomWarning("Error shader — resolves to internal error");
                    var matCount = shader.ReferencingMaterials.Count;
                    issues.Add(MakeIssue(
                        CodeErrorShader,
                        $"Shader '{shader.Name}' resolves to internal error shader. Referenced by {matCount} material(s).",
                        UnityScannerIssueSeverity.Error,
                        shader.Path,
                        "shader_name", shader.Name,
                        "material_count", matCount));

                    continue;
                }

                if (settings.DetectFallbackShaders && shader.IsFallbackShader)
                {
                    var fallback = ShaderAnalysisScanner.GetShaderFallbackName(shader.Shader, shader.Path);
                    shader.AddCustomWarning($"Has fallback shader: {fallback}");
                    issues.Add(MakeIssue(
                        CodeFallbackShader,
                        $"Shader '{shader.Name}' has fallback shader '{fallback}'.",
                        UnityScannerIssueSeverity.Info,
                        shader.Path,
                        "fallback_shader", fallback));
                }

                if (shader.VariantCount > variantThreshold)
                {
                    shader.AddCustomWarning($"Variant explosion: {shader.VariantCount} estimated variants (threshold: {variantThreshold})");
                    issues.Add(MakeIssue(
                        CodeVariantExplosion,
                        $"Shader '{shader.Name}' estimated variant count ({shader.VariantCount}) exceeds threshold ({variantThreshold}). " +
                        $"Keywords: {shader.KeywordCount}, Passes: {shader.PassCount}.",
                        UnityScannerIssueSeverity.Warning,
                        shader.Path,
                        "variant_count", shader.VariantCount,
                        "threshold", variantThreshold,
                        "keyword_count", shader.KeywordCount,
                        "pass_count", shader.PassCount));
                }

                if (shader.PassCount > passThreshold)
                {
                    shader.AddCustomWarning($"Pass count exceeded: {shader.PassCount} passes (threshold: {passThreshold})");
                    issues.Add(MakeIssue(
                        CodePassCountExceeded,
                        $"Shader '{shader.Name}' has {shader.PassCount} passes (threshold: {passThreshold}).",
                        UnityScannerIssueSeverity.Warning,
                        shader.Path,
                        "pass_count", shader.PassCount,
                        "threshold", passThreshold));
                }

                if (settings.DetectExpensiveFeatures && profile != null)
                {
                    var expensiveKws = ShaderAnalysisScanner.GetExpensiveKeywordsForProfile(shader.Keywords, profile);
                    if (expensiveKws.Count > 0)
                    {
                        shader.AddCustomWarning($"Expensive keywords for {profile.DisplayName}: {string.Join(", ", expensiveKws)}");
                        issues.Add(MakeIssue(
                            CodeExpensiveFeatureForPlatform,
                            $"Shader '{shader.Name}' uses expensive keywords for {profile.DisplayName} profile: {string.Join(", ", expensiveKws)}.",
                            UnityScannerIssueSeverity.Warning,
                            shader.Path,
                            "expensive_keywords", string.Join(";", expensiveKws),
                            "profile", profile.DisplayName));
                    }
                }

                if (settings.DetectPlatformMismatches && profile != null)
                {
                    if (profile.Id == PlatformProfilePresets.Mobile &&
                        shader.RenderPipeline == "HDRP")
                    {
                        shader.AddCustomWarning($"Pipeline mismatch: HDRP shader on Mobile profile");
                        issues.Add(MakeIssue(
                            CodePlatformKeywordMismatch,
                            $"Shader '{shader.Name}' is an HDRP shader but target profile is Mobile.",
                            UnityScannerIssueSeverity.Warning,
                            shader.Path,
                            "render_pipeline", shader.RenderPipeline,
                            "profile", profile.DisplayName));
                    }
                }
            }

            foreach (var mat in materials)
            {
                if (mat.IsUsingErrorShader)
                {
                    mat.AddCustomWarning("Uses error shader");
                    issues.Add(MakeIssue(
                        CodeErrorShader,
                        $"Material '{mat.Name}' uses error shader.",
                        UnityScannerIssueSeverity.Error,
                        mat.Path,
                        "material_name", mat.Name));
                }
            }

            if (settings.DetectDuplicateKeywords)
            {
                var duplicates = ShaderAnalysisScanner.DetectDuplicateFeatureSets(materials);
                foreach (var group in duplicates)
                {
                    if (group.Materials.Count < 2) continue;

                    var names = group.Materials.Select(m => m.Name).Take(5).ToList();
                    issues.Add(MakeIssue(
                        CodeDuplicateKeywordProfiles,
                        $"{group.Materials.Count} materials share identical keyword profiles: {string.Join(", ", names)}. " +
                        "These could potentially be consolidated.",
                        UnityScannerIssueSeverity.Info,
                        group.Materials[0].Path,
                        "material_count", group.Materials.Count,
                        "keywords", group.NormalizedKeywordsKey));
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
                CategoryId = "shader_analysis",
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
                    if (key != null)
                        issue.Metadata[key] = metadataPairs[i + 1];
                }
            }

            return issue;
        }
    }
}
