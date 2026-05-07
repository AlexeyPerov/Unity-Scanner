using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Results;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.MissingReferences
{
    public class MissingReferencesTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "missing_references";

        private const int PageSize = 35;

        private MissingReferencesCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _pagesScroll;
        private Vector2 _assetsScroll;
        private Vector2 _fieldTypesScroll;

        private string _pathFilter = "";
        private readonly HashSet<string> _fieldTypesToShow = new HashSet<string>();

        private bool _analysisSettingsFoldout;
        private bool _infoFoldout;

        private List<MissingRefAssetData> _cachedFilteredAssets;
        private bool _cacheDirty = true;

        public void Bind(MissingReferencesCategory category)
        {
            _category = category;
        }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;

            DrawInfoSection();
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category?.LastAssets == null || _category.LastAssets.Count == 0) return;

            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);

            USGUIUtilities.HorizontalLine();
            DrawPagination();
            USGUIUtilities.HorizontalLine();
            DrawPathFilter();
            USGUIUtilities.HorizontalLine();
            DrawFieldTypeChips();
            USGUIUtilities.HorizontalLine();
            DrawVisibilityToggles();
            USGUIUtilities.HorizontalLine();
            DrawAssetsList();
        }

        public void DrawTopBar(UnityScannerResult result)
        {
            if (_category == null) return;

            _analysisSettingsFoldout = EditorGUILayout.Foldout(_analysisSettingsFoldout, "Analysis Settings");
            if (!_analysisSettingsFoldout) return;

            EditorGUI.indentLevel++;
            var settings = _category.Settings as MissingReferencesSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }

            GUILayout.Label("Additionally Scan For:", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            settings.EnableMissingMethodScan = EditorGUILayout.ToggleLeft("<MISSING> Methods", settings.EnableMissingMethodScan);
            settings.EnableTypeMismatchScan = EditorGUILayout.ToggleLeft("Type Mismatches", settings.EnableTypeMismatchScan);
            settings.EnableMissingScriptScan = EditorGUILayout.ToggleLeft("Missing Scripts", settings.EnableMissingScriptScan);
            settings.EnableDuplicateComponentScan = EditorGUILayout.ToggleLeft("Duplicate Components", settings.EnableDuplicateComponentScan);
            settings.EnableInvalidLayerScan = EditorGUILayout.ToggleLeft("Invalid Layers", settings.EnableInvalidLayerScan);
            if (EditorGUI.EndChangeCheck())
                InvalidateCache();

            EditorGUILayout.HelpBox("Settings are saved immediately. Re-run analysis to apply.", MessageType.Info);

            EditorGUI.indentLevel--;
        }

        #region Info Section

        private void DrawInfoSection()
        {
            _infoFoldout = EditorGUILayout.Foldout(_infoFoldout, "Full Info");
            if (!_infoFoldout) return;

            EditorGUI.indentLevel++;

            GUILayout.Label("Unity uses FileID and GUID entities to link assets to each other.", EditorStyles.wordWrappedLabel);
            USGUIUtilities.HorizontalLine();

            USGUIUtilities.DrawColoredLabel("[Missing FileID and Guid] - both identifiers do not exist.", Color.red);
            USGUIUtilities.DrawColoredLabel("[Missing Guid] - only Guid does not exist.", Color.yellow);
            USGUIUtilities.DrawColoredLabel("[Missing FileId] - only FileId does not exist.", Color.yellow);
            USGUIUtilities.HorizontalLine();

            var settings = _category?.Settings as MissingReferencesSettings;

            if (settings?.EnableMissingMethodScan == true)
                USGUIUtilities.DrawColoredLabel("[<MISSING> Methods] - UnityEvent method references that no longer exist.", Color.magenta);
            if (settings?.EnableTypeMismatchScan == true)
                USGUIUtilities.DrawColoredLabel("[Type Mismatch] - UnityEvent argument types that cannot be resolved.", new Color(1f, 0.5f, 0f));
            if (settings?.EnableMissingScriptScan == true)
                USGUIUtilities.DrawColoredLabel("[Missing Scripts] - MonoBehaviour components referencing deleted scripts.", Color.red);
            if (settings?.EnableDuplicateComponentScan == true)
                USGUIUtilities.DrawColoredLabel("[Duplicate Components] - duplicate component types on GameObjects in prefabs.", Color.cyan);
            if (settings?.EnableInvalidLayerScan == true)
                USGUIUtilities.DrawColoredLabel("[Invalid Layers] - GameObject layer values not defined in TagManager.", Color.yellow);

            EditorGUI.indentLevel--;
        }

        #endregion

        #region Pagination

        private void DrawPagination()
        {
            var filtered = GetFilteredAssets();
            var totalCount = filtered.Count;
            if (totalCount == 0) return;

            _pagesScroll = GUILayout.BeginScrollView(_pagesScroll, GUILayout.Height(30));
            EditorGUILayout.BeginHorizontal();

            var pagesCount = totalCount / PageSize + (totalCount % PageSize > 0 ? 1 : 0);
            var prevColor = GUI.color;

            if (totalCount <= 150)
            {
                GUI.color = !_pagination.PageToShow.HasValue ? Color.yellow : Color.white;
                if (GUILayout.Button(new GUIContent("All", "Show all entries on one page"), GUILayout.Width(30f)))
                    _pagination.PageToShow = null;
                GUI.color = prevColor;
            }
            else
            {
                _pagination.PageToShow ??= 0;
            }

            for (var i = 0; i < pagesCount; i++)
            {
                GUI.color = _pagination.PageToShow == i ? Color.yellow : Color.white;
                if (GUILayout.Button(new GUIContent((i + 1).ToString(), $"Go to page {i + 1}"), GUILayout.Width(30f)))
                    _pagination.PageToShow = i;
                GUI.color = prevColor;
            }

            if (_pagination.PageToShow.HasValue && _pagination.PageToShow > pagesCount - 1)
                _pagination.PageToShow = Mathf.Max(0, pagesCount - 1);
            if (pagesCount == 0)
                _pagination.PageToShow = null;

            EditorGUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        #endregion

        #region Filters

        private void DrawPathFilter()
        {
            EditorGUILayout.BeginHorizontal();
            var textFieldStyle = EditorStyles.textField;
            var prevAlignment = textFieldStyle.alignment;
            textFieldStyle.alignment = TextAnchor.MiddleCenter;

            GUILayout.Label("Path Contains:", GUILayout.Width(100));
            var newPathFilter = GUILayout.TextField(_pathFilter, GUILayout.Width(400));
            if (newPathFilter != _pathFilter)
            {
                _pathFilter = newPathFilter;
                InvalidateCache();
            }

            textFieldStyle.alignment = prevAlignment;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFieldTypeChips()
        {
            if (_category.FieldTypeCounters.Count == 0) return;

            GUILayout.Label("Filter by Type:");
            _fieldTypesScroll = EditorGUILayout.BeginScrollView(_fieldTypesScroll, GUILayout.Height(30));
            EditorGUILayout.BeginHorizontal();

            var prevColor = GUI.color;
            var isAllSelected = _fieldTypesToShow.Count == 0 || _fieldTypesToShow.Count == _category.FieldTypeCounters.Count;

            GUI.color = isAllSelected ? Color.cyan : Color.white;
            if (GUILayout.Button(new GUIContent($"All [{_category.FieldTypeSum}]", "Show all field types"), GUILayout.Width(120)))
            {
                if (isAllSelected)
                    _fieldTypesToShow.Clear();
                else
                    foreach (var kvp in _category.FieldTypeCounters)
                        _fieldTypesToShow.Add(kvp.Key);
                InvalidateCache();
            }
            GUI.color = prevColor;

            foreach (var kvp in _category.FieldTypeCounters)
            {
                GUI.color = _fieldTypesToShow.Contains(kvp.Key) ? Color.cyan : Color.white;
                if (GUILayout.Button(new GUIContent(kvp.Key, "Filter by this field type"), GUILayout.Width(150)))
                {
                    if (!_fieldTypesToShow.Remove(kvp.Key))
                        _fieldTypesToShow.Add(kvp.Key);
                    InvalidateCache();
                }
                GUI.color = prevColor;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Visibility Toggles

        private void DrawVisibilityToggles()
        {
            var settings = _category.Settings as MissingReferencesSettings;
            if (settings == null) return;

            EditorGUILayout.BeginHorizontal();

            DrawVisibilityToggle(ref settings.ShowMissingFileIDAndGuid, "Missing both FileID and Guid",
                CountMissingFileIDAndGuid(), Color.red);
            DrawVisibilityToggle(ref settings.ShowMissingGuid, "Missing Guid",
                CountMissingGuid(), Color.yellow);

            EditorGUILayout.EndHorizontal();

            if (settings.EnableMissingMethodScan || settings.EnableTypeMismatchScan)
            {
                EditorGUILayout.BeginHorizontal();

                if (settings.EnableMissingMethodScan)
                    DrawVisibilityToggle(ref settings.ShowMissingMethods, "<MISSING> Methods",
                        CountType(a => a.RefsData.MissingMethods.Count), Color.magenta);
                if (settings.EnableTypeMismatchScan)
                    DrawVisibilityToggle(ref settings.ShowTypeMismatches, "Type Mismatch",
                        CountType(a => a.RefsData.TypeMismatches.Count), new Color(1f, 0.5f, 0f));

                EditorGUILayout.EndHorizontal();
            }

            if (settings.EnableMissingScriptScan || settings.EnableDuplicateComponentScan || settings.EnableInvalidLayerScan)
            {
                EditorGUILayout.BeginHorizontal();

                if (settings.EnableMissingScriptScan)
                    DrawVisibilityToggle(ref settings.ShowMissingScripts, "Missing Scripts",
                        CountType(a => a.RefsData.MissingScripts.Count), Color.red);
                if (settings.EnableDuplicateComponentScan)
                    DrawVisibilityToggle(ref settings.ShowDuplicateComponents, "Dup Components",
                        CountType(a => a.RefsData.DuplicateComponents.Count), Color.cyan);
                if (settings.EnableInvalidLayerScan)
                    DrawVisibilityToggle(ref settings.ShowInvalidLayers, "Invalid Layers",
                        CountType(a => a.RefsData.InvalidLayers.Count), Color.yellow);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            settings.ShowFileIDIssues = EditorGUILayout.Toggle("Show FileID Issues:", settings.ShowFileIDIssues);
            if (EditorGUI.EndChangeCheck())
                InvalidateCache();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (settings.ShowFileIDIssues)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);

                DrawVisibilityToggle(ref settings.ShowMissingFileID, "Missing FileID",
                    CountType(a => a.RefsData.MissingFileID), Color.cyan);
                DrawVisibilityToggle(ref settings.ShowMissingLocalFileID, "Missing Local FileID",
                    CountType(a => a.RefsData.MissingLocalFileID), Color.yellow);
                DrawVisibilityToggle(ref settings.ShowEmptyLocalRefs, "Empty Local FileID",
                    CountEmptyLocalFileID(), Color.white);

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawVisibilityToggle(ref bool value, string label, int count, Color activeColor)
        {
            var prevColor = GUI.color;
            GUI.color = value ? activeColor : Color.gray;
            var suffix = value ? "Shown" : "Hidden";
            if (GUILayout.Button(new GUIContent($"{label} [{count}]: {suffix}", "Expand or collapse this asset's details"), GUILayout.Width(250)))
            {
                value = !value;
                InvalidateCache();
            }
            GUI.color = prevColor;
        }

        #endregion

        #region Assets List

        private void DrawAssetsList()
        {
            var filtered = GetFilteredAssets();

            _assetsScroll = EditorGUILayout.BeginScrollView(_assetsScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filtered.Count; i++)
            {
                if (_pagination.PageToShow.HasValue)
                {
                    var page = _pagination.PageToShow.Value;
                    if (i < page * PageSize || i >= (page + 1) * PageSize)
                        continue;
                }

                DrawAssetRow(filtered[i], i);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetRow(MissingRefAssetData asset, int index)
        {
            EditorGUILayout.BeginHorizontal();

            var expandLabel = asset.Foldout ? "Collapse <<<" : "Expand >>>";
            if (GUILayout.Button(new GUIContent(expandLabel, "Expand or collapse this asset's details"), GUILayout.Width(100)))
                asset.Foldout = !asset.Foldout;

            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(25));

            var prevColor = GUI.color;
            GUI.color = asset.ValidType ? Color.white : Color.red;
            var typeName = asset.TypeName;
            if (typeName.Length > 16)
                typeName = typeName.Substring(0, 14) + "..";
            GUILayout.Label(typeName, GUILayout.Width(100));
            GUI.color = prevColor;

            if (asset.ValidType)
                USGUIUtilities.DrawAssetButtonWithFixedWidth(asset.Path, 280f, 18f);
            else
                GUILayout.Space(280f);

            var settings = _category.Settings as MissingReferencesSettings;

            DrawCounterColumn(asset.RefsData.MissingFileIDAndGuid, "Missing FileID and Guid: ", 160, Color.red, Color.white);
            DrawCounterColumn(asset.RefsData.MissingGuid, "Missing Guid: ", 120, Color.yellow, Color.white);

            if (settings?.EnableMissingMethodScan == true)
                DrawCounterColumn(asset.RefsData.MissingMethods.Count, "Missing Methods: ", 130, Color.magenta, Color.gray);
            if (settings?.EnableTypeMismatchScan == true)
                DrawCounterColumn(asset.RefsData.TypeMismatches.Count, "Type Mismatch: ", 130, new Color(1f, 0.5f, 0f), Color.gray);
            if (settings?.EnableMissingScriptScan == true)
                DrawCounterColumn(asset.RefsData.MissingScripts.Count, "Miss Scripts: ", 110, Color.red, Color.gray);
            if (settings?.EnableDuplicateComponentScan == true && asset.Type == typeof(GameObject))
                DrawCounterColumn(asset.RefsData.DuplicateComponents.Count, "Dup Comp: ", 100, Color.cyan, Color.gray);
            if (settings?.EnableInvalidLayerScan == true)
                DrawCounterColumn(asset.RefsData.InvalidLayers.Count, "Inv Layers: ", 110, Color.yellow, Color.gray);

            if (settings?.ShowFileIDIssues == true)
            {
                DrawCounterColumn(asset.RefsData.MissingFileID, "Missing FileID: ", 120, Color.cyan, Color.white);
                DrawCounterColumn(asset.RefsData.MissingLocalFileID, "Missing Local FileID: ", 140, Color.yellow, Color.white);
                DrawCounterColumn(asset.RefsData.EmptyFileIDs.Count, "Empty FileID: ", 140, Color.white, Color.gray);
            }

            EditorGUILayout.EndHorizontal();

            if (asset.Foldout)
                DrawAssetDetail(asset, settings);
        }

        private void DrawCounterColumn(int count, string label, int width, Color activeColor, Color inactiveColor)
        {
            var prevColor = GUI.color;
            GUI.color = count > 0 ? activeColor : inactiveColor;
            GUILayout.Label($"{label}{count}", GUILayout.Width(width));
            GUI.color = prevColor;
        }

        #endregion

        #region Asset Detail

        private void DrawAssetDetail(MissingRefAssetData asset, MissingReferencesSettings settings)
        {
            EditorGUI.indentLevel++;
            USGUIUtilities.HorizontalLine();

            DrawExternalReferences(asset, settings);
            DrawLocalReferences(asset, settings);
            DrawMissingMethodsDetail(asset, settings);
            DrawTypeMismatchesDetail(asset, settings);
            DrawMissingScriptsDetail(asset, settings);
            DrawDuplicateComponentsDetail(asset, settings);
            DrawInvalidLayersDetail(asset, settings);

            USGUIUtilities.HorizontalLine();
            EditorGUI.indentLevel--;
        }

        private void DrawExternalReferences(MissingRefAssetData asset, MissingReferencesSettings settings)
        {
            var refs = asset.RefsData.ExternalReferences
                .Where(x => x.WarningLevel > 0)
                .ToList();

            if (refs.Count == 0) return;

            var shouldShowAny = settings.ShowMissingFileIDAndGuid || settings.ShowMissingGuid
                || settings.ShowFileIDIssues;

            foreach (var reg in refs)
            {
                var guidIssue = reg.GuidValid && !reg.GuidExistsInAssets;
                var fileIdIssue = reg.FileIDValid && !reg.FileIDExistsInAssets;
                var bothMissing = guidIssue && fileIdIssue;

                if (!shouldShowAny) continue;

                if (bothMissing && !settings.ShowMissingFileIDAndGuid && !guidIssue)
                    continue;

                if (!settings.ShowFileIDIssues && fileIdIssue && !guidIssue)
                    continue;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);

                if (fileIdIssue && (settings.ShowFileIDIssues || guidIssue))
                {
                    var prevColor = GUI.color;
                    GUI.color = Color.yellow;
                    GUILayout.Label($"Missing FileID {reg.FileID}", GUILayout.Width(200));
                    GUI.color = prevColor;

                    if (GUILayout.Button(new GUIContent("Copy", "Copy details to clipboard"), GUILayout.Width(50)))
                        GUIUtility.systemCopyBuffer = reg.FileID.ToString();
                }

                if (guidIssue)
                {
                    var prevColor = GUI.color;
                    GUI.color = Color.yellow;
                    GUILayout.Label($"Missing GUID {reg.Guid}", GUILayout.Width(310));
                    GUI.color = prevColor;

                    if (GUILayout.Button(new GUIContent("Copy", "Copy details to clipboard"), GUILayout.Width(50)))
                        GUIUtility.systemCopyBuffer = reg.Guid;
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);

                var context = "";
                if (!string.IsNullOrEmpty(reg.HolderName))
                    context += $"in [{reg.HolderName}] ";
                context += $"at line [{reg.Line + 1}]";
                if (!string.IsNullOrEmpty(reg.FieldType))
                    context += $" [{reg.FieldType}]";

                GUILayout.Label(context, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                foreach (var sampleLine in reg.Sample)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    var lineColor = sampleLine.Contains(reg.Guid) ? Color.white : Color.gray;
                    USGUIUtilities.DrawColoredLabel($"> {sampleLine}", lineColor);
                    EditorGUILayout.EndHorizontal();
                }

                USGUIUtilities.HorizontalLine();
            }
        }

        private void DrawLocalReferences(MissingRefAssetData asset, MissingReferencesSettings settings)
        {
            if (!settings.ShowEmptyLocalRefs && !settings.ShowMissingLocalFileID) return;

            var unknownRefs = asset.RefsData.LocalReferences
                .Where(x => x.IdValid && x.LocalUsagesCount == 0 && !x.ExistsInAssets)
                .ToList();

            foreach (var localRef in unknownRefs)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label($"Unknown FileID at [{localRef.Line + 1}] {localRef.Id}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawMissingMethodsDetail(MissingRefAssetData asset, MissingReferencesSettings settings)
        {
            if (!settings.EnableMissingMethodScan || !settings.ShowMissingMethods) return;
            if (asset.RefsData.MissingMethods.Count == 0) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            USGUIUtilities.DrawColoredLabel("<MISSING> Method References:", Color.magenta);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            foreach (var entry in asset.RefsData.MissingMethods)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(30);
                USGUIUtilities.DrawColoredLabel($"  {entry.ClassName}.{entry.MethodName}", Color.magenta, 400);
                GUILayout.Label($"at line [{entry.Line + 1}]", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            USGUIUtilities.HorizontalLine();
        }

        private void DrawTypeMismatchesDetail(MissingRefAssetData asset, MissingReferencesSettings settings)
        {
            if (!settings.EnableTypeMismatchScan || !settings.ShowTypeMismatches) return;
            if (asset.RefsData.TypeMismatches.Count == 0) return;

            var orange = new Color(1f, 0.5f, 0f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            USGUIUtilities.DrawColoredLabel("Type Mismatch References:", orange);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            foreach (var entry in asset.RefsData.TypeMismatches)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(30);
                USGUIUtilities.DrawColoredLabel($"  {entry.TypeName}", orange, 400);
                GUILayout.Label($"at line [{entry.Line + 1}]", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            USGUIUtilities.HorizontalLine();
        }

        private void DrawMissingScriptsDetail(MissingRefAssetData asset, MissingReferencesSettings settings)
        {
            if (!settings.EnableMissingScriptScan || !settings.ShowMissingScripts) return;
            if (asset.RefsData.MissingScripts.Count == 0) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            USGUIUtilities.DrawColoredLabel("Missing Script References:", Color.red);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            foreach (var entry in asset.RefsData.MissingScripts)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(30);
                USGUIUtilities.DrawColoredLabel($"  GUID: {entry.ScriptGuid}", Color.red, 400);
                GUILayout.Label($"at line [{entry.Line + 1}]", EditorStyles.miniLabel);
                if (GUILayout.Button(new GUIContent("Copy", "Copy details to clipboard"), GUILayout.Width(50)))
                    GUIUtility.systemCopyBuffer = entry.ScriptGuid;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            USGUIUtilities.HorizontalLine();
        }

        private void DrawDuplicateComponentsDetail(MissingRefAssetData asset, MissingReferencesSettings settings)
        {
            if (!settings.EnableDuplicateComponentScan || !settings.ShowDuplicateComponents) return;
            if (asset.RefsData.DuplicateComponents.Count == 0) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            USGUIUtilities.DrawColoredLabel("Duplicate Components:", Color.cyan);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            foreach (var entry in asset.RefsData.DuplicateComponents)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(30);
                USGUIUtilities.DrawColoredLabel($"  [{entry.GameObjectName}] {entry.ComponentType} x{entry.Count}",
                    Color.cyan, 400);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            USGUIUtilities.HorizontalLine();
        }

        private void DrawInvalidLayersDetail(MissingRefAssetData asset, MissingReferencesSettings settings)
        {
            if (!settings.EnableInvalidLayerScan || !settings.ShowInvalidLayers) return;
            if (asset.RefsData.InvalidLayers.Count == 0) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            USGUIUtilities.DrawColoredLabel("Invalid Layer References:", Color.yellow);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            foreach (var entry in asset.RefsData.InvalidLayers)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(30);
                USGUIUtilities.DrawColoredLabel($"  Layer {entry.LayerIndex} (undefined)", Color.yellow, 400);
                GUILayout.Label($"at line [{entry.Line + 1}]", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            USGUIUtilities.HorizontalLine();
        }

        #endregion

        #region Counting Helpers

        private int CountMissingFileIDAndGuid()
        {
            if (_category?.LastAssets == null) return 0;
            return _category.LastAssets.Sum(a => a.RefsData.MissingFileIDAndGuid);
        }

        private int CountMissingGuid()
        {
            if (_category?.LastAssets == null) return 0;
            return _category.LastAssets.Sum(a => a.RefsData.MissingGuid);
        }

        private int CountType(Func<MissingRefAssetData, int> selector)
        {
            if (_category?.LastAssets == null) return 0;
            return _category.LastAssets.Sum(selector);
        }

        private int CountEmptyLocalFileID()
        {
            if (_category?.LastAssets == null) return 0;
            return _category.LastAssets.Sum(a => a.RefsData.EmptyFileIDs.Count);
        }

        #endregion

        #region Filtering & Caching

        private void InvalidateCache()
        {
            _cacheDirty = true;
        }

        private List<MissingRefAssetData> GetFilteredAssets()
        {
            if (!_cacheDirty && _cachedFilteredAssets != null)
                return _cachedFilteredAssets;

            var assets = _category?.LastAssets;
            if (assets == null)
            {
                _cachedFilteredAssets = new List<MissingRefAssetData>();
                _cacheDirty = false;
                return _cachedFilteredAssets;
            }

            var settings = _category.Settings as MissingReferencesSettings;
            var filtered = new List<MissingRefAssetData>();

            foreach (var asset in assets)
            {
                if (!string.IsNullOrEmpty(_pathFilter) && !asset.Path.Contains(_pathFilter))
                    continue;

                if (_fieldTypesToShow.Count > 0 && !_fieldTypesToShow.Any(ft => asset.MissingFieldTypes.Contains(ft)))
                    continue;

                var shouldShow = false;

                if (settings != null)
                {
                    if (settings.ShowMissingFileIDAndGuid && asset.RefsData.MissingFileIDAndGuid > 0) shouldShow = true;
                    if (settings.ShowMissingGuid && asset.RefsData.MissingGuid > 0) shouldShow = true;
                    if (settings.ShowMissingMethods && asset.RefsData.MissingMethods.Count > 0) shouldShow = true;
                    if (settings.ShowTypeMismatches && asset.RefsData.TypeMismatches.Count > 0) shouldShow = true;
                    if (settings.ShowMissingScripts && asset.RefsData.MissingScripts.Count > 0) shouldShow = true;
                    if (settings.ShowDuplicateComponents && asset.RefsData.DuplicateComponents.Count > 0) shouldShow = true;
                    if (settings.ShowInvalidLayers && asset.RefsData.InvalidLayers.Count > 0) shouldShow = true;

                    if (settings.ShowFileIDIssues)
                    {
                        if (settings.ShowMissingFileID && asset.RefsData.MissingFileID > 0) shouldShow = true;
                        if (settings.ShowMissingLocalFileID && asset.RefsData.MissingLocalFileID > 0) shouldShow = true;
                        if (settings.ShowEmptyLocalRefs && asset.RefsData.EmptyFileIDs.Count > 0) shouldShow = true;
                    }
                }

                if (!shouldShow) continue;

                filtered.Add(asset);
            }

            _cachedFilteredAssets = filtered;
            _cacheDirty = false;
            return _cachedFilteredAssets;
        }

        #endregion
    }
}
