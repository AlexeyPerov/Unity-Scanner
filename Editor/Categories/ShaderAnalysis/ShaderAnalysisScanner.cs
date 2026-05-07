using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Categories.ShaderAnalysis
{
    public static class ShaderAnalysisScanner
    {
        private static readonly HashSet<string> ErrorShaderNames = new HashSet<string>
        {
            "Hidden/InternalErrorShader",
            "InternalErrorShader"
        };

        private static readonly HashSet<string> ExpensiveKeywordsMobile = new HashSet<string>
        {
            "DIRECTIONAL_COOKIE",
            "POINT_COOKIE",
            "SHADOWS_CUBE",
            "SHADOWS_SCREEN",
            "LIGHTMAP_SHADOW_MIXING",
            "SHADOWS_SHADOWMASK",
            "LIGHTPROBE_SH",
            "VERTEXLIGHT_ON",
            "DIRLIGHTMAP_COMBINED",
            "DYNAMICLIGHTMAP_ON"
        };

        private static readonly HashSet<string> ExpensiveKeywordsConsole = new HashSet<string>
        {
        };

        public static void ScanAll(
            ShaderAnalysisSettings settings,
            PlatformProfile profile,
            List<ShaderData> shaders,
            List<MaterialData> materials,
            IUnityScannerIssueSink issueSink)
        {
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

            var materialMap = new Dictionary<Shader, List<MaterialData>>();
            var assetPaths = AssetDatabase.GetAllAssetPaths();
            var total = assetPaths.Length;

            for (var i = 0; i < assetPaths.Length; i++)
            {
                if (i % 500 == 0)
                {
                    GC.Collect();
                    issueSink.ReportProgress((float)i / total * 0.3f, "Scanning materials...");
                }

                var assetPath = assetPaths[i];
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    !assetPath.Contains(settings.PathFilter))
                    continue;

                if (!assetPath.StartsWith("Assets/") && !assetPath.StartsWith("Packages/"))
                    continue;

                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (type != typeof(Material))
                    continue;

                var mat = AssetDatabase.LoadMainAssetAtPath(assetPath) as Material;
                if (mat == null || mat.shader == null)
                    continue;

                var materialData = CreateMaterialData(mat, assetPath);
                materials.Add(materialData);

                if (!materialMap.ContainsKey(mat.shader))
                    materialMap[mat.shader] = new List<MaterialData>();
                materialMap[mat.shader].Add(materialData);
            }

            var shaderGuids = AssetDatabase.FindAssets("t:Shader");
            for (var i = 0; i < shaderGuids.Length; i++)
            {
                if (i % 100 == 0)
                {
                    GC.Collect();
                    issueSink.ReportProgress(0.3f + (float)i / shaderGuids.Length * 0.4f, "Scanning shaders...");
                }

                var shaderPath = AssetDatabase.GUIDToAssetPath(shaderGuids[i]);

                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    !shaderPath.Contains(settings.PathFilter))
                    continue;

                var shader = AssetDatabase.LoadMainAssetAtPath(shaderPath) as Shader;
                if (shader == null)
                    continue;

                var shaderData = CreateShaderData(shader, shaderPath, variantThreshold);
                if (materialMap.TryGetValue(shader, out var mats))
                    shaderData.ReferencingMaterials.AddRange(mats);

                shaders.Add(shaderData);
            }

            for (var i = 0; i < materials.Count; i++)
            {
                var mat = materials[i];
                if (mat.Shader != null)
                {
                    var shaderData = shaders.FirstOrDefault(s => s.Shader == mat.Material.shader);
                    if (shaderData != null)
                        mat.Shader = shaderData;
                }
            }

            GC.Collect();
            issueSink.ReportProgress(0.9f, "Analyzing shader features...");
        }

        private static MaterialData CreateMaterialData(Material mat, string path)
        {
            var data = new MaterialData
            {
                Path = path,
                Name = Path.GetFileName(path),
                Material = mat,
                IsUsingErrorShader = IsErrorShader(mat.shader),
                ShaderKeywords = new List<string>(mat.shaderKeywords)
            };

            if (data.IsUsingErrorShader)
                data.TrySetWarningLevel(3);

            for (var i = 0; i < mat.passCount; i++)
            {
                // Material passes tracked at shader level
            }

            return data;
        }

        private static ShaderData CreateShaderData(Shader shader, string path, int variantThreshold)
        {
            var data = new ShaderData
            {
                Path = path,
                Name = shader.name,
                Shader = shader,
                PassCount = shader.passCount,
                IsErrorShader = IsErrorShader(shader),
                IsFallbackShader = HasShaderFallback(shader, path),
                RenderPipeline = DetectRenderPipeline(shader)
            };

            var keywordSet = new HashSet<string>();

            try
            {
                var ks = shader.keywordSpace;
                var keywordCount = ks.keywordCount;

                for (var i = 0; i < ks.keywords.Length; i++)
                {
                    var kwName = ks.keywords[i].name;
                    keywordSet.Add(kwName);
                    data.Keywords.Add(kwName);
                }
            }
            catch { }

            data.KeywordCount = keywordSet.Count;

            for (var i = 0; i < shader.passCount; i++)
            {
                data.PassNames.Add($"Pass {i}");
            }

            data.VariantCount = EstimateVariantCount(data);

            if (data.IsErrorShader)
                data.TrySetWarningLevel(3);
            else if (data.VariantCount > variantThreshold)
                data.TrySetWarningLevel(2);
            else if (data.PassCount > 8)
                data.TrySetWarningLevel(1);

            return data;
        }

        private static bool HasShaderFallback(Shader shader, string shaderPath)
        {
            try
            {
                if (!File.Exists(shaderPath))
                    return false;
                var lines = File.ReadAllLines(shaderPath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Fallback", StringComparison.Ordinal))
                        return !trimmed.Contains("Off");
                }
            }
            catch { }
            return false;
        }

        public static string GetShaderFallbackName(Shader shader, string shaderPath)
        {
            try
            {
                if (!File.Exists(shaderPath))
                    return "";
                var lines = File.ReadAllLines(shaderPath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith("Fallback", StringComparison.Ordinal))
                        continue;
                    if (trimmed.Contains("Off"))
                        return "";
                    var start = trimmed.IndexOf('"');
                    var end = trimmed.LastIndexOf('"');
                    if (start >= 0 && end > start)
                        return trimmed.Substring(start + 1, end - start - 1);
                    return "Yes";
                }
            }
            catch { }
            return "";
        }

        private static int EstimateVariantCount(ShaderData data)
        {
            if (data.KeywordCount == 0)
                return data.PassCount;
            return (int)Math.Pow(2, Math.Min(data.KeywordCount, 20)) * data.PassCount;
        }

        private static bool IsErrorShader(Shader shader)
        {
            if (shader == null) return true;
            return ErrorShaderNames.Contains(shader.name);
        }

        private static string DetectRenderPipeline(Shader shader)
        {
            if (shader == null) return "Unknown";
            var name = shader.name;
            if (name.Contains("Universal") || name.Contains("URP"))
                return "URP";
            if (name.Contains("HDRenderPipeline") || name.Contains("HDRP"))
                return "HDRP";
            return "Built-in";
        }

        public static List<ShaderFeatureSet> DetectDuplicateFeatureSets(List<MaterialData> materials)
        {
            var groups = new Dictionary<string, ShaderFeatureSet>();

            foreach (var mat in materials)
            {
                if (mat.ShaderKeywords == null || mat.ShaderKeywords.Count == 0)
                    continue;

                var sorted = new List<string>(mat.ShaderKeywords);
                sorted.Sort(StringComparer.Ordinal);
                var key = string.Join(",", sorted);

                if (!groups.TryGetValue(key, out var group))
                {
                    group = new ShaderFeatureSet { NormalizedKeywordsKey = key };
                    groups[key] = group;
                }
                group.Materials.Add(mat);
            }

            return groups.Values.Where(g => g.Materials.Count > 1).ToList();
        }

        public static List<string> GetExpensiveKeywordsForProfile(List<string> keywords, PlatformProfile profile)
        {
            var expensive = new List<string>();
            var expensiveSet = profile?.Id == PlatformProfilePresets.Mobile
                ? ExpensiveKeywordsMobile
                : ExpensiveKeywordsConsole;

            foreach (var kw in keywords)
            {
                if (expensiveSet.Contains(kw))
                    expensive.Add(kw);
            }

            return expensive;
        }
    }
}
