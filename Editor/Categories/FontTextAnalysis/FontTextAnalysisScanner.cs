using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Categories.FontTextAnalysis
{
    public static class FontTextAnalysisScanner
    {
        public static void ScanAll(
            FontTextAnalysisSettings settings,
            PlatformProfile profile,
            List<FontAssetData> fonts,
            IUnityScannerIssueSink issueSink)
        {
            var maxAtlas = profile?.MaxTmpAtlasSize ?? settings.MaxAtlasSize;
            var maxDepth = profile?.MaxFallbackChainDepth ?? settings.MaxFallbackChainDepth;

            var tmpFontGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            var total = tmpFontGuids.Length + 100;

            for (var i = 0; i < tmpFontGuids.Length; i++)
            {
                if (i % 50 == 0)
                    issueSink.ReportProgress((float)i / total, "Scanning TMP fonts...");

                var fontPath = AssetDatabase.GUIDToAssetPath(tmpFontGuids[i]);
                if (!string.IsNullOrEmpty(settings.PathFilter) && !fontPath.Contains(settings.PathFilter))
                    continue;

                var fontAsset = AssetDatabase.LoadMainAssetAtPath(fontPath);
                if (fontAsset == null) continue;

                var data = CreateTmpFontData(fontAsset, fontPath, maxAtlas, maxDepth);
                if (data != null)
                    fonts.Add(data);
            }

            var unityFontGuids = AssetDatabase.FindAssets("t:Font");
            for (var i = 0; i < unityFontGuids.Length; i++)
            {
                issueSink.ReportProgress(0.7f + (float)i / (unityFontGuids.Length + 1) * 0.2f, "Scanning Unity fonts...");

                var fontPath = AssetDatabase.GUIDToAssetPath(unityFontGuids[i]);
                if (!string.IsNullOrEmpty(settings.PathFilter) && !fontPath.Contains(settings.PathFilter))
                    continue;

                var font = AssetDatabase.LoadMainAssetAtPath(fontPath) as Font;
                if (font == null) continue;

                fonts.Add(new FontAssetData
                {
                    Path = fontPath,
                    Name = Path.GetFileName(fontPath),
                    FontAsset = font,
                    IsTmpFont = false,
                    IsDynamic = font.dynamic
                });
            }

            GC.Collect();
        }

        private static FontAssetData CreateTmpFontData(UnityEngine.Object fontAsset, string path, int maxAtlas, int maxDepth)
        {
            var data = new FontAssetData
            {
                Path = path,
                Name = Path.GetFileName(path),
                FontAsset = fontAsset,
                IsTmpFont = true
            };

            try
            {
                var so = new SerializedObject(fontAsset);
                var atlasTexProp = so.FindProperty("m_AtlasTexture");
                if (atlasTexProp != null && atlasTexProp.objectReferenceValue is Texture2D tex)
                {
                    data.AtlasWidth = tex.width;
                    data.AtlasHeight = tex.height;
                }

                var atlasWidthProp = so.FindProperty("m_AtlasWidth");
                var atlasHeightProp = so.FindProperty("m_AtlasHeight");
                if (atlasWidthProp != null) data.AtlasWidth = atlasWidthProp.intValue;
                if (atlasHeightProp != null) data.AtlasHeight = atlasHeightProp.intValue;

                var isDynamicProp = so.FindProperty("m_IsDynamic");
                if (isDynamicProp != null) data.IsDynamic = isDynamicProp.boolValue;

                var faceInfo = so.FindProperty("m_FaceInfo");
                if (faceInfo != null)
                {
                    var glyphTable = so.FindProperty("m_GlyphTable");
                    if (glyphTable != null)
                        data.GlyphCount = glyphTable.arraySize;
                }

                var fallbackProp = so.FindProperty("m_FallbackFontAssetTable");
                if (fallbackProp != null)
                {
                    var depth = 0;
                    var current = fontAsset;
                    var visited = new HashSet<UnityEngine.Object> { current };

                    while (fallbackProp.arraySize > 0 && depth < 20)
                    {
                        var fb = fallbackProp.GetArrayElementAtIndex(0).objectReferenceValue;
                        if (fb == null || visited.Contains(fb)) break;
                        visited.Add(fb);
                        depth++;
                        data.FallbackChainNames.Add(fb.name);

                        var fbSo = new SerializedObject(fb);
                        fallbackProp = fbSo.FindProperty("m_FallbackFontAssetTable");
                        if (fallbackProp == null) break;
                    }
                    data.FallbackChainDepth = depth;
                }

                so.Dispose();
            }
            catch { }

            if (data.AtlasWidth > maxAtlas || data.AtlasHeight > maxAtlas)
                data.TrySetWarningLevel(2);
            if (data.FallbackChainDepth > maxDepth)
                data.TrySetWarningLevel(1);
            if (data.IsDynamic)
                data.TrySetWarningLevel(1);

            return data;
        }

        public static List<FontFeatureSet> DetectDuplicateFallbackChains(List<FontAssetData> fonts)
        {
            var groups = new Dictionary<string, FontFeatureSet>();
            foreach (var font in fonts)
            {
                if (font.FallbackChainNames.Count == 0) continue;
                var key = string.Join("|", font.FallbackChainNames);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new FontFeatureSet { NormalizedFallbackKey = key };
                    groups[key] = group;
                }
                group.Fonts.Add(font);
            }
            return groups.Values.Where(g => g.Fonts.Count > 1).ToList();
        }
    }
}
