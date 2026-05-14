using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace UnityScanner.Categories.Sprite2DAnalysis
{
    public static class Sprite2DAnalysisScanner
    {
        public static IEnumerator ScanAll(
            Sprite2DAnalysisSettings settings,
            PlatformProfile profile,
            List<SpriteAtlasData> atlasResults,
            List<SpriteEntry> spriteResults,
            List<DuplicateGroup> duplicateResults,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            var e1 = ScanAtlases(settings, profile, atlasResults, spriteResults, issueSink, yieldInterval);
            while (e1.MoveNext()) yield return e1.Current;

            var e2 = ScanLooseSprites(settings, spriteResults, issueSink, yieldInterval);
            while (e2.MoveNext()) yield return e2.Current;

            if (settings.CheckDuplicateContent)
            {
                var e3 = FindDuplicateSprites(settings, duplicateResults, issueSink, yieldInterval);
                while (e3.MoveNext()) yield return e3.Current;
            }
        }

        private static IEnumerator ScanAtlases(
            Sprite2DAnalysisSettings settings,
            PlatformProfile profile,
            List<SpriteAtlasData> atlasResults,
            List<SpriteEntry> spriteResults,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            issueSink.ReportProgress(0f, "Scanning sprite atlases...");

            var atlasGuids = AssetDatabase.FindAssets("t:SpriteAtlas");
            var total = atlasGuids.Length;

            for (var i = 0; i < total; i++)
            {
                if (yieldInterval > 0 && i > 0 && i % yieldInterval == 0)
                {
                    System.GC.Collect();
                    yield return 0.05f;
                    System.GC.Collect();
                }

                if (i % 10 == 0)
                    issueSink.ReportProgress((float)i / total * 0.5f, "Scanning sprite atlases...");

                var path = AssetDatabase.GUIDToAssetPath(atlasGuids[i]);
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    path.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var atlas = AssetDatabase.LoadMainAssetAtPath(path) as SpriteAtlas;
                if (atlas == null) continue;

                var data = new SpriteAtlasData { AssetPath = path };

                var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                var totalSprites = allAssets.OfType<Sprite>().ToList();

                data.SpriteCount = totalSprites.Count;

                var atlasTexture = GetAtlasTexture(atlas);
                if (atlasTexture != null)
                    data.AtlasPixelArea = (long)atlasTexture.width * atlasTexture.height;

                long usedArea = 0;
                foreach (var sprite in totalSprites)
                {
                    var entry = CreateSpriteEntry(sprite, path, true);
                    spriteResults.Add(entry);
                    usedArea += entry.PixelArea;
                    data.Sprites.Add(entry);
                }

                data.UsedPixelArea = usedArea;
                if (data.AtlasPixelArea > 0)
                    data.UnusedRatio = 1f - (float)usedArea / data.AtlasPixelArea;

                atlasResults.Add(data);
            }
        }

        private static IEnumerator ScanLooseSprites(
            Sprite2DAnalysisSettings settings,
            List<SpriteEntry> spriteResults,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            issueSink.ReportProgress(0.5f, "Scanning loose sprites...");

            var spriteGuids = AssetDatabase.FindAssets("t:Sprite");
            var total = spriteGuids.Length;

            var inAtlas = new HashSet<string>();
            foreach (var s in spriteResults)
                if (!string.IsNullOrEmpty(s.AssetPath))
                    inAtlas.Add(s.AssetPath);

            for (var i = 0; i < total; i++)
            {
                if (yieldInterval > 0 && i > 0 && i % yieldInterval == 0)
                {
                    System.GC.Collect();
                    yield return 0.05f;
                    System.GC.Collect();
                }

                if (i % 100 == 0)
                    issueSink.ReportProgress(0.5f + (float)i / total * 0.4f, "Scanning loose sprites...");

                var path = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
                if (inAtlas.Contains(path)) continue;
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    path.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>();
                foreach (var sprite in sprites)
                {
                    var entry = CreateSpriteEntry(sprite, "", false);
                    spriteResults.Add(entry);
                }
            }
        }

        private static IEnumerator FindDuplicateSprites(
            Sprite2DAnalysisSettings settings,
            List<DuplicateGroup> duplicateResults,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            issueSink.ReportProgress(0.9f, "Checking for duplicate sprites...");

            var spriteGuids = AssetDatabase.FindAssets("t:Sprite");
            var hashMap = new Dictionary<string, List<string>>();

            for (var i = 0; i < spriteGuids.Length; i++)
            {
                if (yieldInterval > 0 && i > 0 && i % yieldInterval == 0)
                {
                    System.GC.Collect();
                    yield return 0.05f;
                    System.GC.Collect();
                }

                var path = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    path.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var tex = AssetDatabase.LoadMainAssetAtPath(path) as Texture2D;
                if (tex == null) continue;

                var hash = tex.imageContentsHash.ToString();
                if (!hashMap.ContainsKey(hash))
                    hashMap[hash] = new List<string>();
                hashMap[hash].Add(path);
            }

            foreach (var kvp in hashMap)
            {
                if (kvp.Value.Count > 1)
                {
                    var tex = AssetDatabase.LoadMainAssetAtPath(kvp.Value[0]) as Texture2D;
                    duplicateResults.Add(new DuplicateGroup
                    {
                        ContentHash = kvp.Key,
                        ContentSizeBytes = tex != null ? (long)tex.width * tex.height * 4 : 0,
                        AssetPaths = kvp.Value
                    });
                }
            }
        }

        private static SpriteEntry CreateSpriteEntry(Sprite sprite, string atlasPath, bool isInAtlas)
        {
            var path = AssetDatabase.GetAssetPath(sprite);
            var entry = new SpriteEntry
            {
                AssetPath = path,
                SpriteName = sprite.name,
                Width = sprite.texture != null ? sprite.texture.width : 0,
                Height = sprite.texture != null ? sprite.texture.height : 0,
                IsInAtlas = isInAtlas,
                AtlasPath = atlasPath
            };
            entry.PixelArea = (long)entry.Width * entry.Height;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType == TextureImporterType.Sprite)
            {
                entry.MeshType = "Sprite";
            }

            return entry;
        }

        private static Texture2D GetAtlasTexture(SpriteAtlas atlas)
        {
            var atlasPath = AssetDatabase.GetAssetPath(atlas);
            if (string.IsNullOrEmpty(atlasPath)) return null;
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(atlasPath);
            return allAssets.OfType<Texture2D>().FirstOrDefault();
        }
    }
}
