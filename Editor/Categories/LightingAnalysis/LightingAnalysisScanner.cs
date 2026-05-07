using System;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace UnityScanner.Categories.LightingAnalysis
{
    public static class LightingAnalysisScanner
    {
        public static void ScanAll(
            LightingAnalysisSettings settings,
            PlatformProfile profile,
            List<SceneLightingData> results,
            IUnityScannerIssueSink issueSink)
        {
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var total = sceneGuids.Length;

            for (var i = 0; i < sceneGuids.Length; i++)
            {
                if (i % 5 == 0)
                    issueSink.ReportProgress((float)i / total, "Scanning scenes for lighting...");

                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (scenePath.StartsWith("Packages/") || scenePath.StartsWith("Library/"))
                    continue;
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    scenePath.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    var data = AnalyzeSceneLighting(scene, scenePath, settings, profile);
                    if (data != null)
                        results.Add(data);
                }
                finally
                {
                    if (SceneManager.sceneCount > 1)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }

            if (settings.CheckEmissiveNoGI)
                ScanEmissiveMaterials(settings, profile, results, issueSink);

            System.GC.Collect();
        }

        private static SceneLightingData AnalyzeSceneLighting(
            Scene scene, string scenePath, LightingAnalysisSettings settings, PlatformProfile profile)
        {
            var data = new SceneLightingData
            {
                ScenePath = scenePath
            };

            var rp = GraphicsSettings.currentRenderPipeline;
            data.ActivePipeline = rp != null ? rp.name : "Built-in";

            var realtimeCount = 0;
            var mixedCount = 0;
            var bakedCount = 0;

            var lights = new List<Light>();
            foreach (var root in scene.GetRootGameObjects())
                lights.AddRange(root.GetComponentsInChildren<Light>(true));

            foreach (var light in lights)
            {
                if (light.type == LightType.Directional || light.type == LightType.Point ||
                    light.type == LightType.Spot || light.type == LightType.Area)
                {
                    var lightInfo = new LightInfo
                    {
                        ObjectPath = GetHierarchyPath(light.transform),
                        LightType = light.type.ToString(),
                        LightMode = light.lightmapBakeType.ToString(),
                        ShadowsEnabled = light.shadows != LightShadows.None,
                        Range = light.range,
                        Intensity = light.intensity,
                        Color = light.color.ToString()
                    };

                    data.Lights.Add(lightInfo);

                    switch (light.lightmapBakeType)
                    {
                        case LightmapBakeType.Realtime: realtimeCount++; break;
                        case LightmapBakeType.Mixed: mixedCount++; break;
                        case LightmapBakeType.Baked: bakedCount++; break;
                    }
                }
            }

            data.RealtimeLightCount = realtimeCount;
            data.MixedLightCount = mixedCount;
            data.BakedLightCount = bakedCount;
            data.TotalLightCount = realtimeCount + mixedCount + bakedCount;

            var probes = new List<ReflectionProbe>();
            foreach (var root in scene.GetRootGameObjects())
                probes.AddRange(root.GetComponentsInChildren<ReflectionProbe>(true));

            data.ReflectionProbeCount = probes.Count;
            data.MaxReflectionProbeResolution = probes.Count > 0 ? probes.Max(p => p.resolution) : 0;

            var lightProbes = LightmapSettings.lightProbes;
            data.HasLightProbes = lightProbes != null && lightProbes.count > 0;

            var lightmaps = LightmapSettings.lightmaps;
            data.LightmapCount = lightmaps != null ? lightmaps.Length : 0;
            data.LightmapSize = 0;
            if (lightmaps != null)
            {
                foreach (var lm in lightmaps)
                {
                    if (lm.lightmapColor != null)
                    {
                        var maxDim = Math.Max(lm.lightmapColor.width, lm.lightmapColor.height);
                        if (maxDim > data.LightmapSize)
                            data.LightmapSize = maxDim;
                    }
                }
            }

            if (profile != null && realtimeCount > profile.MaxRealtimeLightsPerScene)
                data.TrySetWarningLevel(2);
            if (data.ReflectionProbeCount > (profile?.MaxReflectionProbeCount ?? 4))
                data.TrySetWarningLevel(2);

            return data;
        }

        private static void ScanEmissiveMaterials(
            LightingAnalysisSettings settings,
            PlatformProfile profile,
            List<SceneLightingData> results,
            IUnityScannerIssueSink issueSink)
        {
            issueSink.ReportProgress(0.9f, "Scanning emissive materials...");

            var matGuids = AssetDatabase.FindAssets("t:Material");
            foreach (var guid in matGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadMainAssetAtPath(path) as Material;
                if (mat == null) continue;

                if (!mat.HasProperty("_EmissionColor")) continue;
                var emissionColor = mat.GetColor("_EmissionColor");
                if (emissionColor.maxColorComponent <= 0f) continue;

                var flags = mat.globalIlluminationFlags;
                if (flags == MaterialGlobalIlluminationFlags.AnyEmissive ||
                    flags == MaterialGlobalIlluminationFlags.BakedEmissive ||
                    flags == MaterialGlobalIlluminationFlags.RealtimeEmissive)
                    continue;

                var sceneData = results.FirstOrDefault() ?? new SceneLightingData();
                sceneData.EmissiveMaterials.Add(new EmissiveMaterialInfo
                {
                    MaterialPath = path,
                    MaterialName = mat.name,
                    GlobalIlluminationFlags = flags.ToString()
                });
            }
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
