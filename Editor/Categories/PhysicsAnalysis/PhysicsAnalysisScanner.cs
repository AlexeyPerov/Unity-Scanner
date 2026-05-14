using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Categories.PhysicsAnalysis
{
    public static class PhysicsAnalysisScanner
    {
        public static IEnumerator ScanAll(
            PhysicsAnalysisSettings settings,
            PlatformProfile profile,
            List<ScenePhysicsData> results,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            if (profile == null) yield break;

            var e = ScanScenes(settings, profile, results, issueSink, yieldInterval);
            while (e.MoveNext()) yield return e.Current;
        }

        private static IEnumerator ScanScenes(
            PhysicsAnalysisSettings settings,
            PlatformProfile profile,
            List<ScenePhysicsData> results,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            issueSink.ReportProgress(0f, "Scanning scenes for physics...");

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
                    issueSink.ReportProgress((float)i / total, "Scanning scenes for physics...");

                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (scenePath.StartsWith("Packages/") || scenePath.StartsWith("Library/"))
                    continue;
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    scenePath.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                try
                {
                    var data = AnalyzeScene(scene, scenePath, settings, profile);
                    if (data != null)
                        results.Add(data);
                }
                finally
                {
                    if (UnityEngine.SceneManagement.SceneManager.sceneCount > 1)
                        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static ScenePhysicsData AnalyzeScene(
            UnityEngine.SceneManagement.Scene scene, string scenePath,
            PhysicsAnalysisSettings settings, PlatformProfile profile)
        {
            var rootObjects = scene.GetRootGameObjects();
            var data = new ScenePhysicsData { ScenePath = scenePath };

            var allTransforms = new List<Transform>();
            foreach (var root in rootObjects)
                CollectTransforms(root.transform, allTransforms);

            var rigidbodySet = new HashSet<Transform>();
            var movingSet = new HashSet<Transform>();

            foreach (var t in allTransforms)
            {
                var rb = t.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rigidbodySet.Add(t);
                    if (!rb.isKinematic)
                        movingSet.Add(t);

                    data.Rigidbodies.Add(new RigidbodyData
                    {
                        ObjectPath = GetPath(t),
                        IsKinematic = rb.isKinematic,
                        UseGravity = rb.useGravity,
                        Constraints = (int)rb.constraints,
                        InterpolationMode = (int)rb.interpolation,
                        IsTrigger = HasTriggerCollider(t),
                        ScenePath = scenePath,
                        AssetPath = scenePath
                    });
                }
            }

            for (var p = movingSet.Count - 1; p >= 0; p--)
            {
                var current = movingSet.ElementAt(p);
                var parent = current.parent;
                while (parent != null)
                {
                    movingSet.Add(parent);
                    parent = parent.parent;
                }
            }

            foreach (var t in allTransforms)
            {
                var colliders = t.GetComponents<Collider>();
                foreach (var col in colliders)
                {
                    var hasRb = rigidbodySet.Contains(t);
                    var isStatic = !hasRb && !movingSet.Contains(t);
                    var hasMovingParent = false;

                    if (!hasRb)
                    {
                        var parent = t.parent;
                        while (parent != null)
                        {
                            if (movingSet.Contains(parent))
                            {
                                hasMovingParent = true;
                                break;
                            }
                            parent = parent.parent;
                        }
                    }

                    var triCount = 0;
                    var isConvex = false;
                    var meshCol = col as MeshCollider;
                    if (meshCol != null)
                    {
                        isConvex = meshCol.convex;
                        if (meshCol.sharedMesh != null)
                            triCount = meshCol.sharedMesh.triangles.Length / 3;
                    }

                    data.Colliders.Add(new ColliderData
                    {
                        ObjectPath = GetPath(col.transform),
                        ColliderType = col.GetType().Name,
                        IsTrigger = col.isTrigger,
                        HasPhysicsMaterial = col.sharedMaterial != null,
                        TriangleCount = triCount,
                        IsConvex = isConvex,
                        IsStatic = isStatic,
                        HasMovingParent = hasMovingParent,
                        ScenePath = scenePath,
                        AssetPath = scenePath
                    });
                }
            }

            data.RigidbodyCount = data.Rigidbodies.Count;
            data.ColliderCount = data.Colliders.Count;
            data.TriggerCount = data.Colliders.Count(c => c.IsTrigger);

            if (settings.CheckLayerMatrixBloat)
                AnalyzeLayerMatrix(data, allTransforms);

            return data;
        }

        private static void CollectTransforms(Transform parent, List<Transform> list)
        {
            list.Add(parent);
            for (var i = 0; i < parent.childCount; i++)
                CollectTransforms(parent.GetChild(i), list);
        }

        private static bool HasTriggerCollider(Transform t)
        {
            var colliders = t.GetComponents<Collider>();
            foreach (var col in colliders)
                if (col.isTrigger) return true;
            return false;
        }

        private static string GetPath(Transform t)
        {
            var parts = new List<string>();
            var current = t;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static void AnalyzeLayerMatrix(ScenePhysicsData data, List<Transform> allTransforms)
        {
            var layerColliderCounts = new Dictionary<int, int>();
            foreach (var t in allTransforms)
            {
                if (t.GetComponents<Collider>().Length > 0 || t.GetComponent<Rigidbody>() != null)
                {
                    var layer = t.gameObject.layer;
                    if (!layerColliderCounts.ContainsKey(layer))
                        layerColliderCounts[layer] = 0;
                    layerColliderCounts[layer]++;
                }
            }

            for (var i = 0; i < 32; i++)
            {
                for (var j = i + 1; j < 32; j++)
                {
                    var enabled = !Physics.GetIgnoreLayerCollision(i, j);
                    if (!enabled) continue;

                    var countA = layerColliderCounts.ContainsKey(i) ? layerColliderCounts[i] : 0;
                    var countB = layerColliderCounts.ContainsKey(j) ? layerColliderCounts[j] : 0;

                    data.LayerCollisions.Add(new LayerCollisionPair
                    {
                        LayerA = i,
                        LayerB = j,
                        Enabled = enabled,
                        ColliderCountA = countA,
                        ColliderCountB = countB
                    });
                }
            }
        }
    }
}
