using System;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnityScanner.Categories.UICanvasAnalysis
{
    public static class UICanvasAnalysisScanner
    {
        public static void ScanAll(
            UICanvasAnalysisSettings settings,
            PlatformProfile profile,
            List<CanvasData> results,
            IUnityScannerIssueSink issueSink)
        {
            ScanScenes(settings, profile, results, issueSink);
            if (settings.ScanPrefabs)
                ScanPrefabs(settings, profile, results, issueSink);
            System.GC.Collect();
        }

        private static void ScanScenes(
            UICanvasAnalysisSettings settings,
            PlatformProfile profile,
            List<CanvasData> results,
            IUnityScannerIssueSink issueSink)
        {
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var total = sceneGuids.Length;

            for (var i = 0; i < sceneGuids.Length; i++)
            {
                if (i % 5 == 0)
                    issueSink.ReportProgress((float)i / total, "Scanning scenes for UI/Canvas...");

                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (scenePath.StartsWith("Packages/") || scenePath.StartsWith("Library/"))
                    continue;
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    scenePath.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    var canvases = new List<Canvas>();
                    foreach (var root in scene.GetRootGameObjects())
                        canvases.AddRange(root.GetComponentsInChildren<Canvas>(true));

                    foreach (var canvas in canvases)
                    {
                        var data = AnalyzeCanvas(canvas, scenePath, settings, profile);
                        if (data != null)
                            results.Add(data);
                    }
                }
                finally
                {
                    if (SceneManager.sceneCount > 1)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ScanPrefabs(
            UICanvasAnalysisSettings settings,
            PlatformProfile profile,
            List<CanvasData> results,
            IUnityScannerIssueSink issueSink)
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            var total = prefabGuids.Length;

            for (var i = 0; i < prefabGuids.Length; i++)
            {
                if (i % 100 == 0)
                    issueSink.ReportProgress(0.5f + (float)i / total * 0.5f, "Scanning prefabs for UI/Canvas...");

                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (prefabPath.StartsWith("Packages/") || prefabPath.StartsWith("Library/"))
                    continue;
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    prefabPath.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var prefab = AssetDatabase.LoadMainAssetAtPath(prefabPath) as GameObject;
                if (prefab == null) continue;

                var canvases = prefab.GetComponentsInChildren<Canvas>(true);
                foreach (var canvas in canvases)
                {
                    var data = AnalyzeCanvas(canvas, prefabPath, settings, profile);
                    if (data != null)
                        results.Add(data);
                }
            }
        }

        private static CanvasData AnalyzeCanvas(
            Canvas canvas, string assetPath, UICanvasAnalysisSettings settings, PlatformProfile profile)
        {
            var data = new CanvasData
            {
                AssetPath = assetPath,
                ScenePath = assetPath,
                CanvasName = canvas.name,
                RenderMode = canvas.renderMode.ToString()
            };

            var go = canvas.gameObject;

            if (settings.CheckUnusedShaderChannels)
            {
                var additionalChannels = canvas.additionalShaderChannels;
                if (additionalChannels != AdditionalCanvasShaderChannels.None)
                {
                    data.EnabledChannels = additionalChannels.ToString();
                    var usedChannels = DetectUsedShaderChannels(go);
                    data.UsedChannels = usedChannels;
                }
            }

            if (settings.CheckNestedRedundancy)
            {
                var parentCanvas = go.GetComponentInParent<Canvas>();
                if (parentCanvas != null && parentCanvas != canvas)
                {
                    var parentSorting = GetCanvasSortingInfo(parentCanvas);
                    var currentSorting = GetCanvasSortingInfo(canvas);
                    if (parentSorting == currentSorting)
                    {
                        data.IsNestedRedundant = true;
                        data.ParentCanvasPath = GetHierarchyPath(parentCanvas.transform);
                    }
                }
            }

            var vertexCount = 0;
            var childCount = 0;
            var raycastTargetCount = 0;
            var unnecessaryRaycastCount = 0;
            var legacyTextCount = 0;
            var tmpTextCount = 0;
            var unpackedSpriteCount = 0;
            var maxLayoutDepth = 0;
            var layoutTypes = new List<string>();

            var allTransforms = go.GetComponentsInChildren<Transform>(true);
            childCount = allTransforms.Length;

            foreach (var t in allTransforms)
            {
                if (t == null || t.gameObject == null) continue;
                var child = t.gameObject;

                var cr = child.GetComponent<CanvasRenderer>();
                if (cr != null)
                {
                    var mesh = cr.GetMesh();
                    if (mesh != null)
                        vertexCount += mesh.vertexCount;
                }

                if (settings.CheckRaycastTargets)
                {
                    var graphic = child.GetComponent<Graphic>();
                    if (graphic != null && graphic.raycastTarget)
                    {
                        raycastTargetCount++;
                        if (!HasEventHandlerInHierarchy(child))
                        {
                            unnecessaryRaycastCount++;
                            data.UnnecessaryRaycasts.Add(new RaycastTargetInfo
                            {
                                ObjectPath = GetHierarchyPath(t),
                                ComponentType = graphic.GetType().Name,
                                HasEventHandler = false
                            });
                        }
                    }
                }

                if (settings.CheckTextTmpMix)
                {
                    var legacyText = child.GetComponent<UnityEngine.UI.Text>();
                    if (legacyText != null) legacyTextCount++;

                    var tmpText = child.GetComponent("TMPro.TextMeshProUGUI") as UnityEngine.UI.Text;
                    if (tmpText == null)
                    {
                        var tmpComp = child.GetComponent("TMPro.TextMeshProUGUI");
                        if (tmpComp != null) tmpTextCount++;
                    }
                }

                if (settings.CheckLayoutNesting)
                {
                    var layoutGroup = child.GetComponent<LayoutGroup>();
                    if (layoutGroup != null)
                    {
                        var depth = CountLayoutAncestors(child);
                        if (depth > maxLayoutDepth)
                            maxLayoutDepth = depth;
                        var typeName = layoutGroup.GetType().Name;
                        if (!layoutTypes.Contains(typeName))
                            layoutTypes.Add(typeName);
                    }
                }

                if (settings.CheckAtlasWaste)
                {
                    var img = child.GetComponent<Image>();
                    if (img != null && img.sprite != null)
                    {
                        var spritePath = AssetDatabase.GetAssetPath(img.sprite);
                        if (!string.IsNullOrEmpty(spritePath) && !spritePath.Contains("SpriteAtlas"))
                            unpackedSpriteCount++;
                    }
                }
            }

            data.VertexCount = vertexCount;
            data.ChildCount = childCount;
            data.RaycastTargetCount = raycastTargetCount;
            data.UnnecessaryRaycastCount = unnecessaryRaycastCount;
            data.LayoutNestingDepth = maxLayoutDepth;
            data.LayoutTypes = layoutTypes;
            data.LegacyTextCount = legacyTextCount;
            data.TmpTextCount = tmpTextCount;
            data.UnpackedSpriteCount = unpackedSpriteCount;

            if (profile != null && vertexCount > profile.MaxCanvasVertexCount)
                data.TrySetWarningLevel(2);
            if (profile != null && maxLayoutDepth > profile.MaxCanvasNestingDepth)
                data.TrySetWarningLevel(2);
            if (unnecessaryRaycastCount > 5)
                data.TrySetWarningLevel(1);

            return data;
        }

        private static string DetectUsedShaderChannels(GameObject go)
        {
            var used = new HashSet<string>();
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                foreach (var mat in rend.sharedMaterials)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_UV2") || mat.HasProperty("_UV3"))
                        used.Add("UV2/UV3");
                    if (mat.HasProperty("_Tangent"))
                        used.Add("Tangent");
                    if (mat.HasProperty("_Normal"))
                        used.Add("Normal");
                }
            }
            return used.Count > 0 ? string.Join(", ", used) : "None";
        }

        private static string GetCanvasSortingInfo(Canvas canvas)
        {
            return canvas.renderMode + ":" + canvas.sortingOrder + ":" + canvas.sortingLayerName;
        }

        private static bool HasEventHandlerInHierarchy(GameObject go)
        {
            var current = go;
            while (current != null)
            {
                var components = current.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    var type = comp.GetType();
                    if (type.GetInterface("IPointerClickHandler") != null ||
                        type.GetInterface("IPointerDownHandler") != null ||
                        type.GetInterface("IPointerUpHandler") != null ||
                        type.GetInterface("IPointerEnterHandler") != null ||
                        type.GetInterface("IPointerExitHandler") != null ||
                        type.GetInterface("IDragHandler") != null ||
                        type.GetInterface("IScrollHandler") != null ||
                        type.GetInterface("ISelectHandler") != null ||
                        type.GetInterface("ISubmitHandler") != null ||
                        type.GetInterface("IToggleHandler") != null)
                        return true;

                    if (type.Name == "Button" || type.Name == "Toggle" ||
                        type.Name == "Slider" || type.Name == "Scrollbar" ||
                        type.Name == "Dropdown" || type.Name == "InputField" ||
                        type.Name == "TMP_InputField" || type.Name == "TMP_Dropdown")
                        return true;
                }
                current = current.transform.parent?.gameObject;
            }
            return false;
        }

        private static int CountLayoutAncestors(GameObject go)
        {
            var depth = 0;
            var current = go.transform.parent;
            while (current != null)
            {
                if (current.GetComponent<LayoutGroup>() != null)
                    depth++;
                current = current.parent;
            }
            var selfLayout = go.GetComponent<LayoutGroup>();
            if (selfLayout != null) depth++;
            return depth;
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
