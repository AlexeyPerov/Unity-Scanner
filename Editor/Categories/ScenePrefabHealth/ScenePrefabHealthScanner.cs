using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityScanner.Categories.ScenePrefabHealth
{
    public static class ScenePrefabHealthScanner
    {
        public static void ScanAll(
            ScenePrefabHealthSettings settings,
            PlatformProfile profile,
            List<SceneData> scenes,
            List<PrefabData> prefabs,
            IUnityScannerIssueSink issueSink)
        {
            ScanScenes(settings, profile, scenes, issueSink);
            ScanPrefabs(settings, profile, prefabs, issueSink);
            GC.Collect();
        }

        private static void ScanScenes(
            ScenePrefabHealthSettings settings,
            PlatformProfile profile,
            List<SceneData> scenes,
            IUnityScannerIssueSink issueSink)
        {
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var total = sceneGuids.Length;
            var buildScenes = GetBuildScenePaths();

            for (var i = 0; i < sceneGuids.Length; i++)
            {
                if (i % 5 == 0)
                    issueSink.ReportProgress((float)i / total * 0.6f, "Scanning scenes...");

                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (scenePath.StartsWith("Packages/") || scenePath.StartsWith("Library/"))
                    continue;
                if (!string.IsNullOrEmpty(settings.PathFilter) && !scenePath.Contains(settings.PathFilter))
                    continue;

                var data = AnalyzeScene(scenePath, settings, profile, buildScenes);
                if (data != null)
                    scenes.Add(data);
            }
        }

        private static SceneData AnalyzeScene(
            string scenePath, ScenePrefabHealthSettings settings, PlatformProfile profile, HashSet<string> buildScenes)
        {
            long fileBytes = 0;
            try { if (File.Exists(scenePath)) fileBytes = new FileInfo(scenePath).Length; } catch { }

            var data = new SceneData
            {
                Path = scenePath,
                Name = Path.GetFileName(scenePath),
                FileSizeBytes = fileBytes,
                IsBootstrapScene = buildScenes.Contains(scenePath) ||
                                   scenePath.IndexOf("bootstrap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   scenePath.IndexOf("startup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   scenePath.IndexOf("init", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   scenePath.IndexOf("preload", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   scenePath.IndexOf("splash", StringComparison.OrdinalIgnoreCase) >= 0
            };

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                data.RootCount = roots.Length;

                var totalObjects = 0;
                var totalComponents = 0;
                var inactiveObjects = 0;
                var inactiveRenderers = 0;

                foreach (var root in roots)
                {
                    var transforms = root.GetComponentsInChildren<Transform>(true);
                    totalObjects += transforms.Length;

                    foreach (var t in transforms)
                    {
                        if (t == null || t.gameObject == null) continue;

                        var components = t.GetComponents<Component>();
                        var validComponents = 0;
                        foreach (var c in components)
                        {
                            if (c == null)
                            {
                                if (settings.DetectBrokenReferences)
                                    data.BrokenReferences.Add("Missing script on '" + GetHierarchyPath(t) + "'.");
                                continue;
                            }
                            validComponents++;
                        }

                        totalComponents += validComponents;

                        if (!t.gameObject.activeSelf)
                        {
                            inactiveObjects++;
                            var renderers = t.GetComponents<Renderer>();
                            if (renderers != null && renderers.Length > 0)
                            {
                                inactiveRenderers++;
                                if (settings.DetectInactiveAntiPatterns)
                                {
                                    data.ExpensiveInactiveObjects.Add(new InactiveObjectInfo
                                    {
                                        ObjectPath = GetHierarchyPath(t),
                                        ComponentType = "Renderer",
                                        Description = "Inactive object with Renderer"
                                    });
                                }
                            }
                        }

                        if (settings.DetectHierarchyHotspots && validComponents > settings.MaxComponentCountPerObject)
                        {
                            data.HotspotPaths.Add(GetHierarchyPath(t) + " (" + validComponents + " components)");
                        }
                    }
                }

                data.TotalObjectCount = totalObjects;
                data.TotalComponentCount = totalComponents;
                data.InactiveObjectCount = inactiveObjects;
                data.InactiveRendererCount = inactiveRenderers;

                if (data.BrokenReferences.Count > 0)
                    data.TrySetWarningLevel(3);
                if (data.TotalObjectCount > (profile?.MaxSceneObjectCount ?? settings.MaxSceneObjectCount))
                    data.TrySetWarningLevel(2);
                if (data.IsBootstrapScene && data.TotalObjectCount > (profile?.MaxSceneObjectCount ?? settings.MaxSceneObjectCount) / 2)
                    data.TrySetWarningLevel(2);
            }
            finally
            {
                if (SceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(scene, true);
            }

            return data;
        }

        private static void ScanPrefabs(
            ScenePrefabHealthSettings settings,
            PlatformProfile profile,
            List<PrefabData> prefabs,
            IUnityScannerIssueSink issueSink)
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            var total = prefabGuids.Length;

            for (var i = 0; i < prefabGuids.Length; i++)
            {
                if (i % 50 == 0)
                    issueSink.ReportProgress(0.6f + (float)i / total * 0.35f, "Scanning prefabs...");

                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (prefabPath.StartsWith("Packages/") || prefabPath.StartsWith("Library/"))
                    continue;
                if (!string.IsNullOrEmpty(settings.PathFilter) && !prefabPath.Contains(settings.PathFilter))
                    continue;

                var data = AnalyzePrefab(prefabPath, settings, profile);
                if (data != null)
                    prefabs.Add(data);
            }
        }

        private static PrefabData AnalyzePrefab(
            string prefabPath, ScenePrefabHealthSettings settings, PlatformProfile profile)
        {
            long fileBytes = 0;
            try { if (File.Exists(prefabPath)) fileBytes = new FileInfo(prefabPath).Length; } catch { }

            var prefab = AssetDatabase.LoadMainAssetAtPath(prefabPath) as GameObject;
            if (prefab == null) return null;

            var data = new PrefabData
            {
                Path = prefabPath,
                Name = Path.GetFileName(prefabPath),
                FileSizeBytes = fileBytes
            };

            var nestingDepth = CalculateNestingDepth(prefab);
            data.NestingDepth = nestingDepth;

            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            data.ChildCount = transforms.Length;

            var componentCount = 0;
            foreach (var t in transforms)
            {
                if (t == null) continue;
                var comps = t.GetComponents<Component>();
                if (comps != null) componentCount += comps.Count(c => c != null);
            }
            data.ComponentCount = componentCount;

            var overrideCount = CountPrefabOverrides(prefab);
            data.OverrideCount = overrideCount;

            if (settings.DetectDeepNesting && nestingDepth > (profile?.MaxPrefabNestingDepth ?? settings.MaxPrefabNestingDepth))
                data.TrySetWarningLevel(2);
            if (settings.DetectOverrideExplosion && overrideCount > (profile?.MaxPrefabOverrideCount ?? settings.MaxPrefabOverrideCount))
                data.TrySetWarningLevel(2);
            if (nestingDepth > (profile?.MaxPrefabNestingDepth ?? settings.MaxPrefabNestingDepth) / 2)
                data.TrySetWarningLevel(1);

            return data;
        }

        private static int CalculateNestingDepth(GameObject prefab)
        {
            var depth = 0;
            var current = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            while (current != null)
            {
                depth++;
                current = PrefabUtility.GetCorrespondingObjectFromSource(current);
            }
            return depth;
        }

        private static int CountPrefabOverrides(GameObject prefab)
        {
            var count = 0;
            var modifications = PrefabUtility.GetPropertyModifications(prefab);
            if (modifications != null)
                count = modifications.Length;

            return count;
        }

        private static HashSet<string> GetBuildScenePaths()
        {
            var result = new HashSet<string>();
            for (var i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                if (EditorBuildSettings.scenes[i].enabled)
                    result.Add(EditorBuildSettings.scenes[i].path);
            }
            return result;
        }

        private static string GetHierarchyPath(Transform t)
        {
            var parts = new List<string>();
            var current = t;
            while (current != null)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", parts);
        }
    }
}
