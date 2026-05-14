using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Categories.LODAnalysis
{
    public static class LODAnalysisScanner
    {
        public static IEnumerator ScanAll(
            LODAnalysisSettings settings,
            PlatformProfile profile,
            List<LODGroupData> results,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            if (profile == null) yield break;

            var e1 = ScanAssetLODGroups(settings, profile, results, issueSink, yieldInterval);
            while (e1.MoveNext()) yield return e1.Current;

            var e2 = ScanSceneLODGroups(settings, profile, results, issueSink, yieldInterval);
            while (e2.MoveNext()) yield return e2.Current;
        }

        private static IEnumerator ScanAssetLODGroups(
            LODAnalysisSettings settings,
            PlatformProfile profile,
            List<LODGroupData> results,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            issueSink.ReportProgress(0f, "Scanning prefabs for LOD groups...");

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            var total = prefabGuids.Length;

            for (var i = 0; i < prefabGuids.Length; i++)
            {
                if (yieldInterval > 0 && i > 0 && i % yieldInterval == 0)
                {
                    System.GC.Collect();
                    yield return 0.05f;
                    System.GC.Collect();
                }

                if (i % 100 == 0)
                    issueSink.ReportProgress((float)i / total * 0.5f, "Scanning prefabs for LOD groups...");

                var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    path.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var prefab = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
                if (prefab == null) continue;

                var lodGroups = prefab.GetComponentsInChildren<LODGroup>(true);
                foreach (var lg in lodGroups)
                {
                    var data = AnalyzeLODGroup(lg, path, "", profile, settings);
                    if (data != null)
                        results.Add(data);
                }
            }
        }

        private static IEnumerator ScanSceneLODGroups(
            LODAnalysisSettings settings,
            PlatformProfile profile,
            List<LODGroupData> results,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            issueSink.ReportProgress(0.5f, "Scanning scenes for LOD groups...");

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var total = sceneGuids.Length;

            for (var i = 0; i < sceneGuids.Length; i++)
            {
                if (yieldInterval > 0 && i > 0 && i % yieldInterval == 0)
                {
                    System.GC.Collect();
                    yield return 0.05f;
                    System.GC.Collect();
                }

                if (i % 50 == 0)
                    issueSink.ReportProgress(0.5f + (float)i / total * 0.5f, "Scanning scenes for LOD groups...");

                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (scenePath.StartsWith("Packages/") || scenePath.StartsWith("Library/"))
                    continue;
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    scenePath.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                try
                {
                    var rootObjects = scene.GetRootGameObjects();
                    foreach (var root in rootObjects)
                    {
                        var lodGroups = root.GetComponentsInChildren<LODGroup>(true);
                        foreach (var lg in lodGroups)
                        {
                            var data = AnalyzeLODGroup(lg, scenePath, scenePath, profile, settings);
                            if (data != null)
                                results.Add(data);
                        }
                    }
                }
                finally
                {
                    if (UnityEngine.SceneManagement.SceneManager.sceneCount > 1)
                        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static LODGroupData AnalyzeLODGroup(
            LODGroup lodGroup, string assetPath, string scenePath,
            PlatformProfile profile, LODAnalysisSettings settings)
        {
            var lods = lodGroup.GetLODs();
            if (lods == null || lods.Length == 0) return null;

            var data = new LODGroupData
            {
                AssetPath = assetPath,
                ScenePath = scenePath,
                LODLevelCount = lods.Length,
                AnimateCrossFading = lodGroup.animateCrossFading,
                FadeMode = (int)lodGroup.fadeMode,
                ObjectName = lodGroup.gameObject.name,
                IsUIElement = lodGroup.GetComponent<UnityEngine.RectTransform>() != null
            };

            var scale = lodGroup.transform.lossyScale;
            data.IsSmallObject = scale.x < 0.5f && scale.y < 0.5f && scale.z < 0.5f;

            var lastCull = true;
            foreach (var lod in lods)
            {
                if (lod.screenRelativeTransitionHeight > 0f)
                    lastCull = false;
            }
            data.HasCullLOD = lastCull;

            for (var i = 0; i < lods.Length; i++)
            {
                var lod = lods[i];
                var levelData = new LODLevelData
                {
                    LevelIndex = i,
                    ScreenTransitionHeight = lod.screenRelativeTransitionHeight,
                    RendererCount = lod.renderers?.Length ?? 0
                };

                var nullCount = 0;
                var totalTris = 0;
                var materials = new HashSet<string>();

                if (lod.renderers != null)
                {
                    foreach (var r in lod.renderers)
                    {
                        if (r == null)
                        {
                            nullCount++;
                            continue;
                        }
                        totalTris += GetTriangleCount(r);
                        if (r.sharedMaterials != null)
                        {
                            foreach (var mat in r.sharedMaterials)
                            {
                                if (mat != null)
                                    materials.Add(mat.name);
                            }
                        }
                    }
                }

                levelData.HasNullRenderers = nullCount > 0;
                levelData.NullRendererCount = nullCount;
                levelData.TriangleCount = totalTris;
                levelData.MaterialNames = materials.ToList();
                data.Levels.Add(levelData);
            }

            return data;
        }

        private static int GetTriangleCount(Renderer renderer)
        {
            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
                return meshFilter.sharedMesh.triangles.Length / 3;

            var skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null && skinned.sharedMesh != null)
                return skinned.sharedMesh.triangles.Length / 3;

            return 0;
        }
    }
}
