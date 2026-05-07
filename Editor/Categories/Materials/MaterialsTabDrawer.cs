using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Results;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.Materials
{
    public class MaterialsTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "materials";
        public System.Action OnScanRequested;

        private const int PageSize = 50;

        private enum OutputFilterType
        {
            MaterialAssets,
            RendererComponents
        }

        private OutputFilterType _typeFilter;
        private string _pathFilter = "";
        private int _materialSortType = 1;
        private bool _materialWarningsOnly;
        private int? _materialPage = 0;
        private Vector2 _materialPagesScroll;
        private Vector2 _materialScroll;

        private int _rendererSortType = 1;
        private bool _rendererWarningsOnly;
        private int? _rendererPage = 0;
        private Vector2 _rendererPagesScroll;
        private Vector2 _rendererScroll;

        private bool _shaderUsageFoldout;
        private bool _analysisSettingsFoldout;
        private bool _batchOperationsFoldout;
        private bool _batchOperationsJustLog;
        private bool _batchTargetOnlyFilteredRenderers = true;

        private Material _batchReplaceSourceMaterial;
        private Material _batchReplaceTargetMaterial;
        private Material _batchBuiltinFallbackMaterial;
        private Shader _batchMissingShaderFallback;

        private MaterialsCategory _category;

        public void Bind(MaterialsCategory category)
        {
            _category = category;
        }

        public void DrawHeader(UnityScannerResult result)
        {
            DrawAnalysisSettings();
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category == null) return;
            if (_category.LastRenderers == null && _category.LastMaterials == null) return;

            var renderers = _category.LastRenderers ?? new List<RendererComponentData>();
            var materials = _category.LastMaterials ?? new List<MaterialAssetData>();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_category.OutputDescription ?? "");
            EditorGUILayout.EndHorizontal();

            USGUIUtilities.HorizontalLine();

            DrawBatchOperations(renderers, materials);

            USGUIUtilities.HorizontalLine();

            DrawShaderUsageSummary(materials);

            EditorGUILayout.BeginHorizontal();

            var prevColor = GUI.color;
            var prevAlignment = GUI.skin.button.alignment;
            GUI.skin.button.alignment = TextAnchor.MiddleLeft;

            GUI.color = _typeFilter == OutputFilterType.MaterialAssets ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent($"[{materials.Count}] Materials", "Switch to materials view"), GUILayout.Width(200f)))
                _typeFilter = OutputFilterType.MaterialAssets;

            GUI.color = _typeFilter == OutputFilterType.RendererComponents ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent($"[{renderers.Count}] Renderers", "Switch to renderers view"), GUILayout.Width(200f)))
                _typeFilter = OutputFilterType.RendererComponents;

            GUI.skin.button.alignment = prevAlignment;
            GUI.color = prevColor;

            EditorGUILayout.EndHorizontal();

            USGUIUtilities.HorizontalLine();

            EditorGUILayout.BeginHorizontal();
            var textFieldStyle = EditorStyles.textField;
            var prevTextFieldAlignment = textFieldStyle.alignment;
            textFieldStyle.alignment = TextAnchor.MiddleCenter;
            _pathFilter = EditorGUILayout.TextField(
                new GUIContent("Path Contains:", "Filter by path substring."),
                _pathFilter, GUILayout.Width(400f));
            textFieldStyle.alignment = prevTextFieldAlignment;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Export Materials CSV", "Export filtered data to a CSV file"), GUILayout.Width(180f)))
            {
                var outputPath = EditorUtility.SaveFilePanel("Export Materials CSV",
                    Application.dataPath, "materials_scan.csv", "csv");
                if (!string.IsNullOrEmpty(outputPath))
                    ExportMaterialsCsv(outputPath, materials);
            }

            if (GUILayout.Button(new GUIContent("Export Renderers CSV", "Export filtered data to a CSV file"), GUILayout.Width(180f)))
            {
                var outputPath = EditorUtility.SaveFilePanel("Export Renderers CSV",
                    Application.dataPath, "renderers_scan.csv", "csv");
                if (!string.IsNullOrEmpty(outputPath))
                    ExportRenderersCsv(outputPath, renderers);
            }
            EditorGUILayout.EndHorizontal();

            USGUIUtilities.HorizontalLine();

            switch (_typeFilter)
            {
                case OutputFilterType.RendererComponents:
                    DrawRenderers(renderers);
                    break;
                case OutputFilterType.MaterialAssets:
                    DrawMaterials(materials);
                    break;
            }
        }

        public void DrawTopBar(UnityScannerResult result)
        {
        }

        private void DrawAnalysisSettings()
        {
            if (_category == null) return;
            var settings = (MaterialsSettings)_category.Settings;

            _analysisSettingsFoldout = EditorGUILayout.Foldout(_analysisSettingsFoldout,
                new GUIContent("Analysis Settings", "Toggles for which conditions raise warnings. Changes apply on the next scan."));
            if (!_analysisSettingsFoldout) return;

            USGUIUtilities.HorizontalLine();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            settings.DefaultMaterialsAreErrors = EditorGUILayout.ToggleLeft("Default Materials are errors", settings.DefaultMaterialsAreErrors);
            settings.NullMaterialsAreErrors = EditorGUILayout.ToggleLeft("Null Materials are errors", settings.NullMaterialsAreErrors);
            settings.DefaultTexturesAreErrors = EditorGUILayout.ToggleLeft("Default Textures are errors", settings.DefaultTexturesAreErrors);
            settings.NullTexturesAreErrors = EditorGUILayout.ToggleLeft("Null Textures are errors", settings.NullTexturesAreErrors);
            settings.DuplicateMaterialsAreErrors = EditorGUILayout.ToggleLeft("Duplicate Materials are errors", settings.DuplicateMaterialsAreErrors);
            settings.UnusedMaterialsAreErrors = EditorGUILayout.ToggleLeft("Unused Materials are errors", settings.UnusedMaterialsAreErrors);
            settings.BuiltinShadersAreErrors = EditorGUILayout.ToggleLeft("Builtin Shaders are errors", settings.BuiltinShadersAreErrors);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            settings.VariantChainsAreErrors = EditorGUILayout.ToggleLeft("Variant deep chains are errors", settings.VariantChainsAreErrors);
            settings.VariantHeavyOverridesAreErrors = EditorGUILayout.ToggleLeft("Heavy variant overrides are errors", settings.VariantHeavyOverridesAreErrors);
            settings.InstancingDisabledAreErrors = EditorGUILayout.ToggleLeft("Instancing disabled is error", settings.InstancingDisabledAreErrors);
            settings.SrpBatcherIncompatibleAreErrors = EditorGUILayout.ToggleLeft("SRP Batcher incompatible is error", settings.SrpBatcherIncompatibleAreErrors);
            settings.TryUseReflectionForAddressablesDetection = EditorGUILayout.ToggleLeft("Try Detect Addressables", settings.TryUseReflectionForAddressablesDetection);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Deep chain threshold", GUILayout.Width(150f));
            settings.VariantDeepChainThreshold = EditorGUILayout.IntField(settings.VariantDeepChainThreshold, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Heavy override threshold", GUILayout.Width(150f));
            settings.VariantHeavyOverridesThreshold = EditorGUILayout.IntField(settings.VariantHeavyOverridesThreshold, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            USGUIUtilities.HorizontalLine();

            settings.GarbageCollectStep = EditorGUILayout.IntField(
                new GUIContent("GC once in (iterations):", "Run GC every N steps. Zero disables."),
                settings.GarbageCollectStep);
            settings.DebugLimit = EditorGUILayout.IntField(
                new GUIContent("Assets Debug Limit:", "Stops scanning after this many assets. Zero means no limit."),
                settings.DebugLimit);
        }

        private void DrawShaderUsageSummary(List<MaterialAssetData> materials)
        {
            var shaderUsage = _category?.ShaderUsageCounts;
            if (shaderUsage == null || shaderUsage.Count == 0) return;

            _shaderUsageFoldout = EditorGUILayout.Foldout(_shaderUsageFoldout,
                new GUIContent($"Shader Usage Summary [{shaderUsage.Count} shaders]"));

            if (_shaderUsageFoldout)
            {
                var sorted = shaderUsage.OrderByDescending(kvp => kvp.Value).ToList();
                foreach (var kvp in sorted)
                {
                    var isBuiltin = MaterialsScanner.IsBuiltinShaderPublic(kvp.Key);
                    var isMissing = kvp.Key == "Unknown" || kvp.Key.Contains("InternalErrorShader");

                    if (isMissing)
                        USGUIUtilities.DrawColoredLabel($"  {kvp.Key}: {kvp.Value} material(s)", Color.red);
                    else if (isBuiltin)
                        USGUIUtilities.DrawColoredLabel($"  {kvp.Key}: {kvp.Value} material(s)", Color.yellow);
                    else
                        EditorGUILayout.LabelField($"  {kvp.Key}: {kvp.Value} material(s)");
                }
            }

            USGUIUtilities.HorizontalLine();
        }

        private void DrawBatchOperations(List<RendererComponentData> renderers, List<MaterialAssetData> materials)
        {
            _batchOperationsFoldout = EditorGUILayout.Foldout(_batchOperationsFoldout,
                new GUIContent("Batch Operations", "Destructive fixes. Use dry-run first."));
            if (!_batchOperationsFoldout) return;

            USGUIUtilities.HorizontalLine();

            if (renderers.Count == 0)
            {
                EditorGUILayout.HelpBox("Collect renderers first to enable batch operations.", MessageType.Info);
                return;
            }

            _batchOperationsJustLog = EditorGUILayout.Toggle(
                new GUIContent("Just log (dry run)", "Only logs changes without applying."),
                _batchOperationsJustLog);
            USGUIUtilities.HorizontalLine();

            _batchTargetOnlyFilteredRenderers = EditorGUILayout.Toggle(
                new GUIContent("Apply to filtered", "When enabled, batch actions only touch filtered renderers."),
                _batchTargetOnlyFilteredRenderers);

            EditorGUILayout.LabelField("Renderer-only operation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Remove null material slots");
            if (GUILayout.Button(new GUIContent("Apply: Remove null slots", "Remove null material slots from renderers (batch operation)"), GUILayout.Width(250f)))
            {
                Debug.Log("[US] Batch remove null slots requested (coroutine-driven, see console).");
            }

            USGUIUtilities.HorizontalLine();

            if (materials.Count == 0)
            {
                EditorGUILayout.HelpBox("Collect materials first to enable material-based batch operations.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Material operations", EditorStyles.boldLabel);

            USGUIUtilities.HorizontalLine();
            EditorGUILayout.LabelField("Replace a specific material", EditorStyles.boldLabel);
            _batchReplaceSourceMaterial = (Material)EditorGUILayout.ObjectField("Source material", _batchReplaceSourceMaterial, typeof(Material), false);
            _batchReplaceTargetMaterial = (Material)EditorGUILayout.ObjectField("Target material", _batchReplaceTargetMaterial, typeof(Material), false);
            if (GUILayout.Button(new GUIContent("Apply: Replace source -> target", "Replace source material with target material (batch)"), GUILayout.Width(300f)))
            {
                Debug.Log("[US] Batch replace material requested (coroutine-driven, see console).");
            }

            USGUIUtilities.HorizontalLine();
            EditorGUILayout.LabelField("Replace unity_builtin/default material references", EditorStyles.boldLabel);
            _batchBuiltinFallbackMaterial = (Material)EditorGUILayout.ObjectField("Fallback material", _batchBuiltinFallbackMaterial, typeof(Material), false);
            if (GUILayout.Button(new GUIContent("Apply: Replace unity_builtin with fallback", "Replace builtin material references with fallback (batch)"), GUILayout.Width(320f)))
            {
                Debug.Log("[US] Batch replace builtin materials requested (coroutine-driven, see console).");
            }

            USGUIUtilities.HorizontalLine();
            EditorGUILayout.LabelField("Fix missing shaders", EditorStyles.boldLabel);
            _batchMissingShaderFallback = (Shader)EditorGUILayout.ObjectField("Fallback shader", _batchMissingShaderFallback, typeof(Shader), false);
            if (GUILayout.Button(new GUIContent("Apply: Fix missing shaders to fallback", "Replace missing/error shaders with fallback shader (batch)"), GUILayout.Width(320f)))
            {
                Debug.Log("[US] Batch fix missing shaders requested (coroutine-driven, see console).");
            }
        }

        private void DrawRenderers(List<RendererComponentData> renderers)
        {
            if (renderers.Count == 0)
            {
                EditorGUILayout.LabelField("No renderers found");
                return;
            }

            EditorGUILayout.BeginHorizontal();
            var prevColor = GUI.color;

            GUI.color = _rendererSortType == 0 || _rendererSortType == 1 ? Color.yellow : Color.white;
            var orderType = _rendererSortType == 1 ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by warnings {orderType}", "Sort entries by this criteria"), GUILayout.Width(150f)))
            {
                if (_rendererSortType == 0) { _rendererSortType = 1; renderers.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel)); }
                else { _rendererSortType = 0; renderers.Sort((a, b) => a.WarningLevel.CompareTo(b.WarningLevel)); }
            }

            GUI.color = _rendererSortType == 2 || _rendererSortType == 3 ? Color.yellow : Color.white;
            orderType = _rendererSortType == 3 ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by path {orderType}", "Sort entries by this criteria"), GUILayout.Width(150f)))
            {
                if (_rendererSortType == 2) { _rendererSortType = 3; renderers.Sort((a, b) => string.Compare(b.Path, a.Path, StringComparison.Ordinal)); }
                else { _rendererSortType = 2; renderers.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal)); }
            }

            GUI.color = _rendererSortType == 4 || _rendererSortType == 5 ? Color.yellow : Color.white;
            orderType = _rendererSortType == 5 ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by material slots {orderType}", "Sort entries by this criteria"), GUILayout.Width(180f)))
            {
                if (_rendererSortType == 4)
                {
                    _rendererSortType = 5;
                    renderers.Sort((a, b) => { var c = b.MaterialSlotsCount.CompareTo(a.MaterialSlotsCount); return c != 0 ? c : string.Compare(a.Path, b.Path, StringComparison.Ordinal); });
                }
                else
                {
                    _rendererSortType = 4;
                    renderers.Sort((a, b) => { var c = a.MaterialSlotsCount.CompareTo(b.MaterialSlotsCount); return c != 0 ? c : string.Compare(a.Path, b.Path, StringComparison.Ordinal); });
                }
            }

            GUI.color = _rendererWarningsOnly ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("Warnings Level 2+ Only", "Toggle: show only entries with warnings level 2 or higher"), GUILayout.Width(250f)))
                _rendererWarningsOnly = !_rendererWarningsOnly;

            GUI.color = prevColor;
            EditorGUILayout.EndHorizontal();

            var filtered = GetFilteredRenderers(renderers);
            DrawPagesWidget(filtered.Count, ref _rendererPage, ref _rendererPagesScroll);
            USGUIUtilities.HorizontalLine();

            _rendererScroll = GUILayout.BeginScrollView(_rendererScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filtered.Count; i++)
            {
                if (_rendererPage.HasValue)
                {
                    var page = _rendererPage.Value;
                    if (i < page * PageSize || i >= (page + 1) * PageSize)
                        continue;
                }

                var asset = filtered[i];
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(new GUIContent(asset.Foldout ? ">Minimize" : ">Expand", "Toggle detailed view for this asset"), GUILayout.Width(70)))
                    asset.Foldout = !asset.Foldout;

                USGUIUtilities.DrawColoredLabelByWarning(i.ToString(), asset.WarningLevel, 40);
                USGUIUtilities.DrawColoredLabelByWarning(asset.ChildName, asset.WarningLevel, 150);
                EditorGUILayout.LabelField(asset.GameObjectName, GUILayout.Width(150f));
                EditorGUILayout.LabelField($"Materials: {asset.MaterialSlotsCount}", GUILayout.Width(90f));
                EditorGUILayout.LabelField($"Warnings: {asset.WarningsCount}", GUILayout.Width(90f));
                EditorGUILayout.LabelField($"Warning: {asset.WarningLevel}", GUILayout.Width(70f));

                EditorGUILayout.EndHorizontal();

                if (asset.Foldout)
                {
                    GUILayout.Space(3);
                    EditorGUILayout.LabelField($"Renderer Path: {asset.Path}");
                    USGUIUtilities.HorizontalLine();
                    USGUIUtilities.DrawAssetButton(asset.Path);
                    if (!string.IsNullOrEmpty(asset.Bundle))
                        EditorGUILayout.LabelField($"[{asset.Bundle}]", GUILayout.Width(250));
                    USGUIUtilities.HorizontalLine();

                    if (asset.CustomWarnings != null)
                    {
                        var prev = GUI.color;
                        GUI.color = Color.yellow;
                        EditorGUILayout.LabelField($"Warnings [{asset.CustomWarnings.Count}]:");
                        foreach (var w in asset.CustomWarnings)
                            EditorGUILayout.LabelField(new GUIContent("  " + w, CustomWarningTooltips.GetTooltipOrEmpty(w)));
                        GUI.color = prev;
                        USGUIUtilities.HorizontalLine();
                    }
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawMaterials(List<MaterialAssetData> materials)
        {
            if (materials.Count == 0)
            {
                EditorGUILayout.LabelField("No materials found");
                return;
            }

            EditorGUILayout.BeginHorizontal();
            var prevColor = GUI.color;

            GUI.color = _materialSortType == 0 || _materialSortType == 1 ? Color.yellow : Color.white;
            var orderType = _materialSortType == 1 ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by warnings {orderType}", "Sort entries by this criteria"), GUILayout.Width(150f)))
            {
                if (_materialSortType == 0) { _materialSortType = 1; materials.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel)); }
                else { _materialSortType = 0; materials.Sort((a, b) => a.WarningLevel.CompareTo(b.WarningLevel)); }
            }

            GUI.color = _materialSortType == 2 || _materialSortType == 3 ? Color.yellow : Color.white;
            orderType = _materialSortType == 3 ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by path {orderType}", "Sort entries by this criteria"), GUILayout.Width(150f)))
            {
                if (_materialSortType == 2) { _materialSortType = 3; materials.Sort((a, b) => string.Compare(b.Path, a.Path, StringComparison.Ordinal)); }
                else { _materialSortType = 2; materials.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal)); }
            }

            GUI.color = _materialSortType == 4 || _materialSortType == 5 ? Color.yellow : Color.white;
            orderType = _materialSortType == 5 ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by size {orderType}", "Sort entries by this criteria"), GUILayout.Width(150f)))
            {
                if (_materialSortType == 4) { _materialSortType = 5; materials.Sort((a, b) => b.BytesSize.CompareTo(a.BytesSize)); }
                else { _materialSortType = 4; materials.Sort((a, b) => a.BytesSize.CompareTo(b.BytesSize)); }
            }

            GUI.color = _materialWarningsOnly ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("Warnings Level 2+ Only", "Toggle: show only entries with warnings level 2 or higher"), GUILayout.Width(250f)))
                _materialWarningsOnly = !_materialWarningsOnly;

            GUI.color = prevColor;
            EditorGUILayout.EndHorizontal();

            var filtered = GetFilteredMaterials(materials);
            DrawPagesWidget(filtered.Count, ref _materialPage, ref _materialPagesScroll);
            USGUIUtilities.HorizontalLine();

            _materialScroll = GUILayout.BeginScrollView(_materialScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filtered.Count; i++)
            {
                if (_materialPage.HasValue)
                {
                    var page = _materialPage.Value;
                    if (i < page * PageSize || i >= (page + 1) * PageSize)
                        continue;
                }

                var asset = filtered[i];
                DrawMaterialRow(i, asset);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawMaterialRow(int i, MaterialAssetData asset)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent(asset.Foldout ? "Minimize" : "Expand", "Toggle detailed view for this asset"), GUILayout.Width(70)))
                asset.Foldout = !asset.Foldout;

            USGUIUtilities.DrawColoredLabelByWarning(i.ToString(), asset.WarningLevel, 40);
            EditorGUILayout.LabelField(asset.TypeName, GUILayout.Width(70f));
            EditorGUILayout.LabelField($"Warning: {asset.WarningLevel}", GUILayout.Width(70f));

            USGUIUtilities.DrawAssetButton(asset.Path);
            EditorGUILayout.LabelField(asset.ReadableSize, GUILayout.Width(70f));

            if (!string.IsNullOrEmpty(asset.Bundle))
                EditorGUILayout.LabelField($"[{asset.Bundle}]", GUILayout.Width(250));

            EditorGUILayout.EndHorizontal();

            if (!asset.Foldout) return;

            GUILayout.Space(3);
            EditorGUILayout.LabelField($"Path: {asset.Path}");
            USGUIUtilities.HorizontalLine();

            EditorGUILayout.LabelField($"Shader: {asset.ShaderName}");
            if (asset.HasRenderQueueOverride)
            {
                USGUIUtilities.DrawColoredLabel(
                    $"Render Queue: {asset.RenderQueue} (override, shader default: {asset.ShaderDefaultRenderQueue})",
                    Color.yellow);
            }
            else
            {
                EditorGUILayout.LabelField($"Render Queue: {asset.RenderQueue}");
            }

            USGUIUtilities.HorizontalLine();
            EditorGUILayout.LabelField("Variant", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Is variant: {asset.IsVariant}");

            if (asset.ParentLinkBroken)
            {
                USGUIUtilities.DrawColoredLabel("Parent: missing or invalid", Color.red);
            }
            else if (!string.IsNullOrEmpty(asset.ParentMaterialPath))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Parent", GUILayout.Width(50));
                USGUIUtilities.DrawAssetButton(asset.ParentMaterialPath);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField($"Chain depth: {asset.VariantChainDepth}");
            if (asset.VariantOverrideCount.HasValue)
                EditorGUILayout.LabelField($"Override count vs parent: {asset.VariantOverrideCount}");
            else
                EditorGUILayout.LabelField("Override count vs parent: —");

            if (asset.VariantChildrenPaths != null && asset.VariantChildrenPaths.Count > 0)
            {
                EditorGUILayout.LabelField($"Child variants [{asset.VariantChildrenPaths.Count}]:");
                var prevAlignment = GUI.skin.button.alignment;
                GUI.skin.button.alignment = TextAnchor.MiddleLeft;
                foreach (var ch in asset.VariantChildrenPaths)
                    USGUIUtilities.DrawAssetButton(ch);
                GUI.skin.button.alignment = prevAlignment;
            }

            USGUIUtilities.HorizontalLine();
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);

            var supInst = !asset.SupportsGpuInstancing.HasValue ? "Unknown" : (asset.SupportsGpuInstancing.Value ? "Yes" : "No");
            var instLine = asset.GpuInstancingEnabled ? "Enabled" : "Disabled";
            EditorGUILayout.LabelField($"GPU instancing: {instLine} (shader support: {supInst}, material: {(asset.GpuInstancingEnabled ? "on" : "off")})");

            var srpLine = !asset.SrpBatcherCompatible.HasValue ? "Unknown" : (asset.SrpBatcherCompatible.Value ? "Compatible" : "Incompatible");
            EditorGUILayout.LabelField($"SRP Batcher: {srpLine}");

            if (asset.EnabledKeywords != null && asset.EnabledKeywords.Count > 0)
                EditorGUILayout.LabelField($"Keywords: {string.Join(", ", asset.EnabledKeywords)}");

            if (asset.Properties != null && asset.Properties.Count > 0)
            {
                asset.PropertiesFoldout = EditorGUILayout.Foldout(asset.PropertiesFoldout, $"Properties [{asset.Properties.Count}]");
                if (asset.PropertiesFoldout)
                {
                    var prevAlignment = GUI.skin.button.alignment;
                    GUI.skin.button.alignment = TextAnchor.MiddleLeft;
                    foreach (var prop in asset.Properties)
                    {
                        if (prop.Type == "TexEnv")
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"  {prop.Type} {prop.Name} =", GUILayout.Width(200f));
                            if (!string.IsNullOrEmpty(prop.Value) && prop.Value != "null")
                            {
                                USGUIUtilities.DrawAssetButton(prop.Value);
                                if (prop.UsedByMaterialPaths != null && prop.UsedByMaterialPaths.Count > 0)
                                    EditorGUILayout.LabelField($"used by {prop.UsedByMaterialPaths.Count} material(s)", GUILayout.Width(180f));
                            }
                            else
                            {
                                EditorGUILayout.LabelField("None", GUILayout.Width(100f));
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"  {prop.Type} {prop.Name} = {prop.Value}");
                        }
                    }
                    GUI.skin.button.alignment = prevAlignment;
                }
            }

            if (asset.ReferencedTexturePaths != null && asset.ReferencedTexturePaths.Count > 0)
            {
                USGUIUtilities.HorizontalLine();
                asset.TextureReferencesFoldout = EditorGUILayout.Foldout(asset.TextureReferencesFoldout,
                    $"Texture References [{asset.ReferencedTexturePaths.Count}]:");
                if (asset.TextureReferencesFoldout)
                {
                    var prevAlignment = GUI.skin.button.alignment;
                    GUI.skin.button.alignment = TextAnchor.MiddleLeft;
                    foreach (var texPath in asset.ReferencedTexturePaths)
                    {
                        USGUIUtilities.DrawAssetButton(texPath);
                        var prop = asset.Properties?.FirstOrDefault(p => p.Type == "TexEnv" && p.Value == texPath);
                        if (prop?.UsedByMaterialPaths != null && prop.UsedByMaterialPaths.Count > 0)
                        {
                            if (!asset.TextureUsedByMaterialsFoldout.TryGetValue(texPath, out var foldout))
                                foldout = false;
                            foldout = EditorGUILayout.Foldout(foldout, $"Used by [{prop.UsedByMaterialPaths.Count}] materials:");
                            asset.TextureUsedByMaterialsFoldout[texPath] = foldout;
                            if (foldout)
                            {
                                foreach (var matPath in prop.UsedByMaterialPaths)
                                    USGUIUtilities.DrawAssetButton(matPath);
                            }
                        }
                        EditorGUILayout.Space(6f);
                    }
                    GUI.skin.button.alignment = prevAlignment;
                }
            }

            USGUIUtilities.HorizontalLine();

            if (asset.ReferencedByPaths != null && asset.ReferencedByPaths.Count > 0)
            {
                EditorGUILayout.LabelField($"Referenced By [{asset.ReferencedByPaths.Count}]:");
                var prevAlignment = GUI.skin.button.alignment;
                GUI.skin.button.alignment = TextAnchor.MiddleLeft;
                foreach (var refPath in asset.ReferencedByPaths)
                    USGUIUtilities.DrawAssetButton(refPath);
                GUI.skin.button.alignment = prevAlignment;
                USGUIUtilities.HorizontalLine();
            }

            if (asset.CustomWarnings != null)
            {
                var prev = GUI.color;
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField($"Warnings [{asset.CustomWarnings.Count}]:");
                foreach (var w in asset.CustomWarnings)
                    EditorGUILayout.LabelField(new GUIContent("  " + w, CustomWarningTooltips.GetTooltipOrEmpty(w)));
                GUI.color = prev;
                USGUIUtilities.HorizontalLine();
            }

            if (asset.DuplicatePaths != null && asset.DuplicatePaths.Count > 0)
            {
                EditorGUILayout.LabelField($"Duplicates [{asset.DuplicatePaths.Count}]:");
                var prevAlignment = GUI.skin.button.alignment;
                GUI.skin.button.alignment = TextAnchor.MiddleLeft;
                foreach (var dupPath in asset.DuplicatePaths)
                    USGUIUtilities.DrawAssetButton(dupPath);
                GUI.skin.button.alignment = prevAlignment;
                USGUIUtilities.HorizontalLine();
            }
        }

        private void DrawPagesWidget(int assetsCount, ref int? pageToShow, ref Vector2 scroll)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.BeginHorizontal();

            var pagesCount = assetsCount / PageSize + (assetsCount % PageSize > 0 ? 1 : 0);
            var showAllButton = assetsCount <= 150;
            if (!showAllButton && !pageToShow.HasValue && pagesCount > 0)
                pageToShow = 0;

            var prevColor = GUI.color;
            if (showAllButton)
            {
                GUI.color = !pageToShow.HasValue ? Color.yellow : Color.white;
                if (GUILayout.Button(new GUIContent("All", "Show all entries on one page"), GUILayout.Width(30f)))
                    pageToShow = null;
                GUI.color = prevColor;
            }

            for (var i = 0; i < pagesCount; i++)
            {
                prevColor = GUI.color;
                GUI.color = pageToShow == i ? Color.yellow : Color.white;
                if (GUILayout.Button(new GUIContent((i + 1).ToString(), $"Go to page {i + 1}"), GUILayout.Width(30f)))
                    pageToShow = i;
                GUI.color = prevColor;
            }

            if (pageToShow.HasValue && pageToShow > pagesCount - 1)
                pageToShow = pagesCount - 1;
            if (pageToShow.HasValue && pagesCount == 0)
                pageToShow = null;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private List<RendererComponentData> GetFilteredRenderers(List<RendererComponentData> renderers)
        {
            var filtered = renderers.AsEnumerable();
            if (_rendererWarningsOnly)
                filtered = filtered.Where(x => x.WarningLevel > 1);
            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(x => x.Path.Contains(_pathFilter));
            return filtered.ToList();
        }

        private List<MaterialAssetData> GetFilteredMaterials(List<MaterialAssetData> materials)
        {
            var filtered = materials.AsEnumerable();
            if (_materialWarningsOnly)
                filtered = filtered.Where(x => x.WarningLevel > 1);
            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(x => x.Path.Contains(_pathFilter));
            return filtered.ToList();
        }

        private void ExportMaterialsCsv(string filePath, List<MaterialAssetData> materials)
        {
            var rows = GetFilteredMaterials(materials);
            var sb = new StringBuilder();
            sb.AppendLine("Path,Name,WarningLevel,ShaderName,RenderQueue,ReadableSize,IsVariant,VariantChainDepth,VariantOverrideCount,GpuInstancingEnabled,SupportsGpuInstancing,SrpBatcherCompatible,Warnings,ReferencedByCount,ReferencedTexturesCount");
            foreach (var row in rows)
            {
                var warnings = row.CustomWarnings == null ? string.Empty : string.Join(" | ", row.CustomWarnings);
                sb.AppendLine(string.Join(",",
                    EscapeCsv(row.Path),
                    EscapeCsv(row.Name),
                    row.WarningLevel.ToString(),
                    EscapeCsv(row.ShaderName),
                    row.RenderQueue.ToString(),
                    EscapeCsv(row.ReadableSize),
                    row.IsVariant.ToString(),
                    row.VariantChainDepth.ToString(),
                    EscapeCsv(row.VariantOverrideCount?.ToString() ?? string.Empty),
                    row.GpuInstancingEnabled.ToString(),
                    EscapeCsv(row.SupportsGpuInstancing?.ToString() ?? "Unknown"),
                    EscapeCsv(row.SrpBatcherCompatible?.ToString() ?? "Unknown"),
                    EscapeCsv(warnings),
                    (row.ReferencedByPaths?.Count ?? 0).ToString(),
                    (row.ReferencedTexturePaths?.Count ?? 0).ToString()));
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[US] Exported {rows.Count} material rows to {filePath}");
        }

        private void ExportRenderersCsv(string filePath, List<RendererComponentData> renderers)
        {
            var rows = GetFilteredRenderers(renderers);
            var sb = new StringBuilder();
            sb.AppendLine("Path,ChildName,WarningLevel,WarningsCount,MaterialSlotsCount,Warnings");
            foreach (var row in rows)
            {
                var warnings = row.CustomWarnings == null ? string.Empty : string.Join(" | ", row.CustomWarnings);
                sb.AppendLine(string.Join(",",
                    EscapeCsv(row.Path),
                    EscapeCsv(row.ChildName),
                    row.WarningLevel.ToString(),
                    row.WarningsCount.ToString(),
                    row.MaterialSlotsCount.ToString(),
                    EscapeCsv(warnings)));
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[US] Exported {rows.Count} renderer rows to {filePath}");
        }

        private static string EscapeCsv(string value)
        {
            if (value == null) return "\"\"";
            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
    }
}
