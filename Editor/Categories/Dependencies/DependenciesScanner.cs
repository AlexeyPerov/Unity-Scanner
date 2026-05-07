using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.U2D;
using UnityScanner.Core.Issues;
using UnityScanner.Utilities.Addressables;
using UnityScanner.Utilities.AssetDatabase;

namespace UnityScanner.Categories.Dependencies
{
    public static class DependenciesScanner
    {
        private static readonly Regex GuidRegex = new Regex(
            @"m_AssetGUID:\s*([0-9a-fA-F]{32})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static void FillReverseDependenciesMap(
            bool scanAssetReferences,
            bool binarySerialization,
            bool scanTerrainDataReferences,
            IUnityScannerIssueSink issueSink,
            out Dictionary<string, List<string>> reverseDependencies)
        {
            var assetPaths = AssetDatabase.GetAllAssetPaths().ToList();
            reverseDependencies = assetPaths.ToDictionary(p => p, _ => new List<string>());

            var totalAssets = assetPaths.Count;
            var progressInterval = Math.Max(1, totalAssets / 100);

            for (var i = 0; i < totalAssets; i++)
            {
                if (i % progressInterval == 0)
                {
                    issueSink.ReportProgress((float)i / totalAssets, "Building dependency map");
                }

                var assetDeps = scanAssetReferences
                    ? GetAllDependencies(assetPaths[i], binarySerialization, false)
                    : AssetDatabase.GetDependencies(assetPaths[i], false);

                foreach (var dep in assetDeps)
                {
                    if (reverseDependencies.TryGetValue(dep, out var list) && dep != assetPaths[i])
                    {
                        list.Add(assetPaths[i]);
                    }
                }
            }

            if (scanTerrainDataReferences)
                ScanTerrainDataReferences(reverseDependencies);
        }

        public static List<DependenciesAssetData> ScanUnreferencedAssets(
            DependenciesSettings settings,
            IUnityScannerIssueSink issueSink)
        {
            USAddressablesReflection.ClearCache();

            EditorUtility.UnloadUnusedAssetsImmediate();

            var ignoreManager = new USIgnorePatternsManager();
            if (settings.CustomIgnorePatterns != null && settings.CustomIgnorePatterns.Count > 0)
            {
                ignoreManager.DefaultIgnorePatterns.Clear();
                ignoreManager.DefaultIgnorePatterns.AddRange(settings.CustomIgnorePatterns);
                ignoreManager.InvalidateCompiledPatterns();
            }

            var compiledPatterns = ignoreManager.CompiledPatterns;
            var iconChecker = new IconPathCache();

            FillReverseDependenciesMap(
                settings.ScanForAssetReferences,
                EditorSettings.serializationMode != SerializationMode.ForceText,
                settings.ScanForTerrainDataReferences,
                issueSink,
                out var map);

            var assets = new List<DependenciesAssetData>();

            var totalAssets = map.Count;
            var progressInterval = Math.Max(1, totalAssets / 100);
            var count = 0;

            foreach (var mapElement in map)
            {
                if (count % progressInterval == 0)
                    issueSink.ReportProgress((float)count / totalAssets, "Analyzing assets");

                count++;

                var assetPath = mapElement.Key;
                var falsePositiveWarning = string.Empty;
                var referencesCount = mapElement.Value.Count;

                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);

                if (referencesCount == 1 && type == typeof(Texture2D))
                {
                    var reference = mapElement.Value[0];
                    var referenceType = AssetDatabase.GetMainAssetTypeAtPath(reference);
                    if (referenceType == typeof(SpriteAtlas))
                    {
                        falsePositiveWarning = $"Sprite references only its atlas {reference}";
                        referencesCount = 0;
                    }
                }

                if (settings.FindUnreferencedOnly && referencesCount != 0)
                    continue;

                var validForOutput = USPathFilterUtilities.IsValidForOutput(assetPath, compiledPatterns);
                var validAssetType = IsValidAssetType(assetPath, type, validForOutput, iconChecker);

                if (!validAssetType)
                    continue;

                if (validForOutput)
                {
                    assets.Add(DependenciesAssetData.Create(
                        assetPath,
                        type,
                        referencesCount,
                        mapElement.Value,
                        falsePositiveWarning,
                        settings.TryUseReflectionForAddressablesDetection));
                }
            }

            return assets;
        }

        public static Dictionary<UnityEngine.Object, List<string>> GetReferencesForSelectedAssets(
            UnityEngine.Object[] selectedObjects,
            bool scanAssetReferences,
            bool binarySerialization)
        {
            var reverseDeps = new Dictionary<string, List<string>>();
            FillReverseDependenciesMap(scanAssetReferences, binarySerialization, false, new UnityScannerIssueSink(), out reverseDeps);

            var results = new Dictionary<UnityEngine.Object, List<string>>();

            foreach (var obj in selectedObjects)
            {
                if (obj == null) continue;
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (reverseDeps.TryGetValue(path, out var deps))
                    results[obj] = new List<string>(deps);
                else
                    results[obj] = new List<string>();
            }

            return results;
        }

        private static string[] GetAllDependencies(string assetPath, bool binarySerialization, bool recursive = true)
        {
            var regularDeps = AssetDatabase.GetDependencies(assetPath, recursive);

            if (!CanContainAssetReferencesByExtension(assetPath))
                return regularDeps;

            if (binarySerialization)
            {
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (obj == null) return regularDeps;

                HashSet<string> result = null;
                var serializedObj = new SerializedObject(obj);
                var iterator = serializedObj.GetIterator();

                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.Generic ||
                        !iterator.type.Contains("AssetReference"))
                        continue;

                    var guidProp = iterator.FindPropertyRelative("m_AssetGUID");
                    if (guidProp == null || string.IsNullOrEmpty(guidProp.stringValue))
                        continue;

                    var refPath = AssetDatabase.GUIDToAssetPath(guidProp.stringValue);
                    if (!string.IsNullOrEmpty(refPath))
                    {
                        result ??= regularDeps.ToHashSet();
                        result.Add(refPath);
                    }
                }

                return result != null ? result.ToArray() : regularDeps;
            }
            else
            {
                if (!File.Exists(assetPath))
                    return regularDeps;

                var content = File.ReadAllText(assetPath);
                if (!content.Contains("m_AssetGUID"))
                    return regularDeps;

                HashSet<string> result = null;

                foreach (Match match in GuidRegex.Matches(content))
                {
                    if (match == null || match.Groups.Count <= 1) continue;
                    var guid = match.Groups[1].Value;
                    if (string.IsNullOrEmpty(guid)) continue;

                    var refPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(refPath))
                    {
                        result ??= regularDeps.ToHashSet();
                        result.Add(refPath);
                    }
                }

                return result != null ? result.ToArray() : regularDeps;
            }
        }

        private static bool CanContainAssetReferencesByExtension(string assetPath)
        {
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return ext == ".asset" || ext == ".prefab" || ext == ".unity";
        }

        private static void ScanTerrainDataReferences(Dictionary<string, List<string>> reverseDependencies)
        {
            var terrainDataGuids = AssetDatabase.FindAssets("t:TerrainData");
            if (terrainDataGuids.Length == 0) return;

            var guidToPath = new Dictionary<string, string>();
            foreach (var guid in terrainDataGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    guidToPath[guid] = path;
            }

            if (guidToPath.Count == 0) return;

            var candidateExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".prefab", ".unity" };
            var candidates = AssetDatabase.GetAllAssetPaths()
                .Where(p => candidateExts.Contains(Path.GetExtension(p)))
                .ToList();

            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate)) continue;
                string content = null;
                foreach (var kvp in guidToPath)
                {
                    content ??= File.ReadAllText(candidate);
                    if (!content.Contains(kvp.Key)) continue;

                    if (reverseDependencies.TryGetValue(kvp.Value, out var list) && !list.Contains(candidate))
                        list.Add(candidate);
                }
            }
        }

        private static bool IsValidAssetType(string path, Type type, bool validForOutput, IconPathCache iconChecker)
        {
            if (type == null)
            {
                if (validForOutput)
                    Debug.Log($"Unable to detect asset type at {path}");
                return false;
            }

            if (type == typeof(MonoScript) || type == typeof(DefaultAsset))
                return false;

            if (type == typeof(SceneAsset))
            {
                var scenes = EditorBuildSettings.scenes;
                if (scenes.Any(scene => scene.path == path))
                    return false;
            }

            return type != typeof(Texture2D) || !iconChecker.IsIcon(path);
        }

        private class IconPathCache
        {
            private HashSet<string> _iconPaths;

            public bool IsIcon(string texturePath)
            {
                if (_iconPaths == null) FindAllIcons();
                return _iconPaths != null && _iconPaths.Contains(texturePath);
            }

            private void FindAllIcons()
            {
                _iconPaths = new HashSet<string>();
                var icons = new List<Texture2D>();

#if UNITY_2021_2_OR_NEWER
                foreach (var field in typeof(NamedBuildTarget).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    if (field.Name == "Unknown") continue;
                    if (field.FieldType != typeof(NamedBuildTarget)) continue;
                    var buildTarget = (NamedBuildTarget)field.GetValue(null);
                    icons.AddRange(PlayerSettings.GetIcons(buildTarget, IconKind.Any));
                }
#else
                foreach (var targetGroup in Enum.GetValues(typeof(BuildTargetGroup)))
                    icons.AddRange(PlayerSettings.GetIconsForTargetGroup((BuildTargetGroup)targetGroup));
#endif
                foreach (var icon in icons)
                    _iconPaths.Add(AssetDatabase.GetAssetPath(icon));
            }
        }
    }
}
