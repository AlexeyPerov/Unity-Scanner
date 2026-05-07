using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Categories.ParticleSystemAnalysis
{
    public static class ParticleSystemAnalysisScanner
    {
        public static void ScanAll(
            ParticleSystemAnalysisSettings settings,
            PlatformProfile profile,
            List<ParticleSystemData> results,
            IUnityScannerIssueSink issueSink)
        {
            if (profile == null) return;

            ScanAssetParticleSystems(settings, profile, results, issueSink);
            ScanSceneParticleSystems(settings, profile, results, issueSink);

            GC.Collect();
        }

        private static void ScanAssetParticleSystems(
            ParticleSystemAnalysisSettings settings,
            PlatformProfile profile,
            List<ParticleSystemData> results,
            IUnityScannerIssueSink issueSink)
        {
            issueSink.ReportProgress(0f, "Scanning particle system assets...");

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            var total = prefabGuids.Length;

            for (var i = 0; i < prefabGuids.Length; i++)
            {
                if (i % 100 == 0)
                    issueSink.ReportProgress((float)i / total * 0.5f, "Scanning prefabs for particle systems...");

                var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    path.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var prefab = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
                if (prefab == null) continue;

                var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in systems)
                {
                    var data = AnalyzeParticleSystem(ps, path, "", profile, settings);
                    if (data != null)
                        results.Add(data);
                }
            }
        }

        private static void ScanSceneParticleSystems(
            ParticleSystemAnalysisSettings settings,
            PlatformProfile profile,
            List<ParticleSystemData> results,
            IUnityScannerIssueSink issueSink)
        {
            issueSink.ReportProgress(0.5f, "Scanning scenes for particle systems...");

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var total = sceneGuids.Length;

            for (var i = 0; i < sceneGuids.Length; i++)
            {
                if (i % 50 == 0)
                    issueSink.ReportProgress(0.5f + (float)i / total * 0.5f, "Scanning scenes for particle systems...");

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
                        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
                        foreach (var ps in systems)
                        {
                            var data = AnalyzeParticleSystem(ps, scenePath, scenePath, profile, settings);
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

        private static ParticleSystemData AnalyzeParticleSystem(
            ParticleSystem ps, string assetPath, string scenePath,
            PlatformProfile profile, ParticleSystemAnalysisSettings settings)
        {
            var data = new ParticleSystemData
            {
                AssetPath = assetPath,
                ScenePath = scenePath
            };

            var main = ps.main;
            data.MaxParticles = main.maxParticles;
            data.SimulationSpace = main.simulationSpace.ToString();

            var activeModules = new List<string>();

            if (settings.CheckEmission)
            {
                var emission = ps.emission;
                var rateOverTime = emission.rateOverTime.constantMax;
                var burstCount = emission.burstCount;
                data.IsBurst = burstCount > 0;
                data.BurstCount = burstCount;

                var totalRate = (int)rateOverTime;
                if (burstCount > 0)
                {
                    for (var b = 0; b < burstCount; b++)
                        totalRate += emission.GetBurst(b).maxCount;
                }

                data.EmissionRate = totalRate;
                if (emission.enabled) activeModules.Add("Emission");
            }

            if (settings.CheckCollision)
            {
                var collision = ps.collision;
                data.CollisionEnabled = collision.enabled;
                data.CollisionSendMessages = collision.sendCollisionMessages;
                if (collision.enabled) activeModules.Add("Collision");
            }

            if (settings.CheckOverdraw)
            {
                var trails = ps.trails;
                data.TrailEnabled = trails.enabled;
                if (trails.enabled)
                {
                    data.TrailLifetime = trails.lifetime.constantMax;
                    activeModules.Add("Trails");
                }
            }

            if (settings.CheckSubEmitters)
            {
                var subEmitters = ps.subEmitters;
                data.SubEmitterCount = subEmitters.subEmittersCount;
                if (data.SubEmitterCount > 0) activeModules.Add("SubEmitters");
            }

            if (ps.shape.enabled) activeModules.Add("Shape");
            if (ps.colorOverLifetime.enabled) activeModules.Add("ColorOverLifetime");
            if (ps.sizeOverLifetime.enabled) activeModules.Add("SizeOverLifetime");
            if (ps.velocityOverLifetime.enabled) activeModules.Add("VelocityOverLifetime");
            if (ps.rotationOverLifetime.enabled) activeModules.Add("RotationOverLifetime");
            if (ps.noise.enabled) activeModules.Add("Noise");
            if (ps.textureSheetAnimation.enabled) activeModules.Add("TextureSheetAnimation");
            if (ps.colorBySpeed.enabled) activeModules.Add("ColorBySpeed");
            if (ps.sizeBySpeed.enabled) activeModules.Add("SizeBySpeed");
            if (ps.rotationBySpeed.enabled) activeModules.Add("RotationBySpeed");
            if (ps.externalForces.enabled) activeModules.Add("ExternalForces");
            if (ps.inheritVelocity.enabled) activeModules.Add("InheritVelocity");
            if (ps.lights.enabled) activeModules.Add("Lights");
            if (ps.trigger.enabled) activeModules.Add("Trigger");
            if (ps.customData.enabled) activeModules.Add("CustomData");

            data.ActiveModuleCount = activeModules.Count;
            data.ActiveModules = activeModules;

            if (settings.CheckLOD && !string.IsNullOrEmpty(scenePath))
            {
                var current = ps.transform.parent;
                var hasLOD = false;
                while (current != null)
                {
                    if (current.GetComponent<LODGroup>() != null)
                    {
                        hasLOD = true;
                        break;
                    }
                    current = current.parent;
                }
                data.HasLOD = hasLOD;
            }

            if (settings.CheckTextures)
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    var mat = renderer.sharedMaterial;
                    if (mat.mainTexture != null)
                    {
                        data.MainTexturePath = AssetDatabase.GetAssetPath(mat.mainTexture);
                        var tex = mat.mainTexture as Texture2D;
                        if (tex != null)
                        {
                            data.MainTextureSize = Math.Max(tex.width, tex.height);
                        }
                    }
                }
            }

            return data;
        }

        public static int ComputeSubEmitterChainDepth(ParticleSystem ps)
        {
            var visited = new HashSet<ParticleSystem> { ps };
            return ComputeChainDepthRecursive(ps, visited, 0);
        }

        private static int ComputeChainDepthRecursive(ParticleSystem ps, HashSet<ParticleSystem> visited, int currentDepth)
        {
            var subEmitters = ps.subEmitters;
            var count = subEmitters.subEmittersCount;
            if (count == 0) return currentDepth;

            var maxChildDepth = currentDepth;
            for (var i = 0; i < count; i++)
            {
                var subEmitter = subEmitters.GetSubEmitterSystem(i);
                if (subEmitter == null || visited.Contains(subEmitter)) continue;
                visited.Add(subEmitter);
                var childDepth = ComputeChainDepthRecursive(subEmitter, visited, currentDepth + 1);
                if (childDepth > maxChildDepth)
                    maxChildDepth = childDepth;
                visited.Remove(subEmitter);
            }

            return maxChildDepth;
        }
    }
}
