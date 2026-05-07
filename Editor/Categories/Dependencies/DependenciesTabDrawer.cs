using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Results;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.Dependencies
{
    public class DependenciesTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "dependencies";

        private const int PageSize = 50;

        private DependenciesCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _pagesScroll;
        private Vector2 _assetsScroll;
        private Vector2 _typesScroll;
        private Vector2 _referencedByScroll;

        private string _pathFilter = "";
        private string _typeFilter = "";
        private bool _showAddressables = true;
        private bool _showUnreferencedOnly;
        private bool _showFalsePositivesOnly;
        private int _sortType = 2;

        private bool _analysisSettingsFoldout;
        private bool _ignorePatternsFoldout;

        private string _backupDirectory = "";

        private List<DependenciesAssetData> _cachedFilteredAssets;
        private bool _cacheDirty = true;

        public void Bind(DependenciesCategory category)
        {
            _category = category;
        }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;

            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);

            if (_category.RefsByTypes.Count > 0)
            {
                GUILayout.Label($"Types: {_category.RefsByTypes.Count}  |  Total: {_category.LastAssets?.Count ?? 0}",
                    EditorStyles.miniLabel);
            }
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category?.LastAssets == null || _category.LastAssets.Count == 0) return;

            DrawFilters();
            USGUIUtilities.HorizontalLine();
            DrawSortButtons();
            USGUIUtilities.HorizontalLine();
            DrawExportButtons();
            USGUIUtilities.HorizontalLine();
            DrawPagination();
            USGUIUtilities.HorizontalLine();
            DrawAssetsList();
            USGUIUtilities.HorizontalLine();
            DrawSelectionAndActions();
        }

        public void DrawTopBar(UnityScannerResult result)
        {
            if (_category == null) return;

            _analysisSettingsFoldout = EditorGUILayout.Foldout(_analysisSettingsFoldout, "Analysis Settings");
            if (!_analysisSettingsFoldout) return;

            EditorGUI.indentLevel++;
            var settings = _category.Settings as DependenciesSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }

            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            settings.FindUnreferencedOnly = EditorGUILayout.ToggleLeft("Find Unreferenced Assets Only", settings.FindUnreferencedOnly);
            settings.ScanForAssetReferences = EditorGUILayout.ToggleLeft("Scan Addressables AssetReferences", settings.ScanForAssetReferences, GUILayout.Width(350));
            settings.TryUseReflectionForAddressablesDetection = EditorGUILayout.ToggleLeft("Detect Addressables", settings.TryUseReflectionForAddressablesDetection);
            settings.ScanForTerrainDataReferences = EditorGUILayout.ToggleLeft("Scan Terrain References", settings.ScanForTerrainDataReferences);
            if (EditorGUI.EndChangeCheck())
                InvalidateCache();

            EditorGUI.indentLevel--;
        }

        #region Filters

        private void DrawFilters()
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

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Filter by Type:", GUILayout.Width(100));
            _typesScroll = EditorGUILayout.BeginScrollView(_typesScroll, GUILayout.Height(42));
            EditorGUILayout.BeginHorizontal();

            var prevColor = GUI.color;
            var isAllSelected = string.IsNullOrEmpty(_typeFilter) || _typeFilter == _category.RefsByTypes.Count + " types";
            GUI.color = isAllSelected ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("All Types", "Show all asset types"), GUILayout.Width(100)))
            {
                _typeFilter = "";
                InvalidateCache();
            }
            GUI.color = prevColor;

            foreach (var kvp in _category.RefsByTypes)
            {
                var displayName = kvp.Key;
                if (displayName.Length > 20)
                {
                    var lastDot = displayName.LastIndexOf('.');
                    if (lastDot >= 0 && displayName.Length - lastDot - 1 > 3)
                        displayName = displayName.Substring(lastDot + 1);
                }

                var buttonLabel = $"[{kvp.Value}] {displayName}";
                GUI.color = _typeFilter == kvp.Key ? Color.yellow : Color.white;
                if (GUILayout.Button(new GUIContent(buttonLabel, "Filter by this asset type"), GUILayout.Width(150)))
                {
                    _typeFilter = _typeFilter == kvp.Key ? "" : kvp.Key;
                    InvalidateCache();
                }
                GUI.color = prevColor;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            var settings = _category.Settings as DependenciesSettings;
            if (settings?.TryUseReflectionForAddressablesDetection == true)
            {
                EditorGUI.BeginChangeCheck();
                _showAddressables = EditorGUILayout.Toggle("Show Addressables:", _showAddressables, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                    InvalidateCache();
            }

            if (settings != null && !settings.FindUnreferencedOnly)
            {
                EditorGUI.BeginChangeCheck();
                _showUnreferencedOnly = EditorGUILayout.Toggle("Unreferenced Only:", _showUnreferencedOnly, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                    InvalidateCache();
            }

            EditorGUI.BeginChangeCheck();
            _showFalsePositivesOnly = EditorGUILayout.Toggle("Show Only False Positive", _showFalsePositivesOnly, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
                InvalidateCache();

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Sort

        private void DrawSortButtons()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Sort:", GUILayout.Width(50));

            var prevColor = GUI.color;

            GUI.color = _sortType == 0 || _sortType == 1 ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent($"Sort by type {GetSortSuffix(0)}", "Sort entries by type"), GUILayout.Width(150)))
                ToggleSort(0, 1);

            GUI.color = _sortType == 2 || _sortType == 3 ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent($"Sort by path {GetSortSuffix(2)}", "Sort entries by path"), GUILayout.Width(150)))
                ToggleSort(2, 3);

            GUI.color = _sortType == 4 || _sortType == 5 ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent($"Sort by size {GetSortSuffix(4)}", "Sort entries by size"), GUILayout.Width(150)))
                ToggleSort(4, 5);

            GUI.color = prevColor;
            EditorGUILayout.EndHorizontal();
        }

        private string GetSortSuffix(int baseSort)
        {
            return _sortType == baseSort ? "A-Z" : _sortType == baseSort + 1 ? "Z-A" : "A-Z";
        }

        private void ToggleSort(int ascending, int descending)
        {
            if (_sortType == ascending)
                _sortType = descending;
            else
                _sortType = ascending;
            InvalidateCache();
        }

        #endregion

        #region Export

        private void DrawExportButtons()
        {
            var filtered = GetFilteredAssets();
            EditorGUILayout.BeginHorizontal();

            if (filtered.Count < 1000)
            {
                if (GUILayout.Button(new GUIContent("Export to Clipboard", "Copy filtered assets list to clipboard"), GUILayout.Width(170)))
                    ExportToClipboard(filtered);
            }

            if (filtered.Count > 0)
            {
                if (GUILayout.Button(new GUIContent("Export to CSV", "Export filtered assets to a CSV file"), GUILayout.Width(170)))
                    ExportToCsv(filtered);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void ExportToClipboard(List<DependenciesAssetData> assets)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Assets [{assets.Count}]:");

            foreach (var asset in assets)
                sb.AppendLine($"[{asset.TypeName}][{asset.ReadableSize}][Refs:{asset.ReferencesCount}] {asset.Path}");

            GUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log($"[US] Exported {assets.Count} assets to clipboard.");
        }

        private void ExportToCsv(List<DependenciesAssetData> assets)
        {
            var path = EditorUtility.SaveFilePanel("Export to CSV", Application.dataPath,
                "dependencies_hunter_export.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder();
            sb.AppendLine("Type,Size,Path,References,Addressable,Warning");

            foreach (var asset in assets)
            {
                sb.AppendLine($"{EscapeCsv(asset.TypeName)},{EscapeCsv(asset.ReadableSize)}," +
                              $"{EscapeCsv(asset.Path)},{asset.ReferencesCount}," +
                              $"{asset.IsAddressable},{EscapeCsv(asset.FalsePositiveWarning)}");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains("\"") || value.Contains(",") || value.Contains("\n") || value.Contains("\r"))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        #endregion

        #region Pagination

        private void DrawPagination()
        {
            var filtered = GetFilteredAssets();
            _pagesScroll = GUILayout.BeginScrollView(_pagesScroll, GUILayout.Height(30));
            EditorGUILayout.BeginHorizontal();

            var totalCount = filtered.Count;
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

        private void DrawAssetRow(DependenciesAssetData asset, int index)
        {
            EditorGUILayout.BeginHorizontal();

            if (asset.IsEligibleForDeletion)
            {
                asset.Selected = EditorGUILayout.Toggle(asset.Selected, GUILayout.Width(16f));
            }
            else
            {
                GUILayout.Space(20f);
            }

            var rowColor = !asset.ValidType ? Color.red
                : !string.IsNullOrEmpty(asset.FalsePositiveWarning) ? Color.yellow
                : Color.white;

            var prevColor = GUI.color;
            GUI.color = rowColor;

            if (!string.IsNullOrEmpty(asset.FalsePositiveWarning))
            {
                asset.Foldout = EditorGUILayout.Foldout(asset.Foldout, $"{index} (i)");
            }
            else
            {
                EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(40f));
            }

            var typeName = asset.TypeName;
            if (typeName.Length > 16)
                typeName = typeName.Substring(0, 13) + "..";
            GUILayout.Label(typeName, GUILayout.Width(100f));

            GUI.color = prevColor;

            if (asset.ValidType)
                USGUIUtilities.DrawAssetButtonWithFixedWidth(asset.Path, 300f, 18f);
            else
                GUILayout.Space(300f);

            GUILayout.Label(asset.ReadableSize, GUILayout.Width(70f));

            var settings = _category.Settings as DependenciesSettings;
            if (settings?.TryUseReflectionForAddressablesDetection == true && _showAddressables)
            {
                if (asset.IsAddressable)
                {
                    prevColor = GUI.color;
                    GUI.color = Color.cyan;
                    GUILayout.Label("Addressable", GUILayout.Width(70f));
                    GUI.color = prevColor;
                }
                else
                {
                    GUILayout.Space(70f);
                }
            }

            DrawRefsButton(asset);

            GUILayout.Label(new GUIContent(asset.ShortPath, asset.Path));

            EditorGUILayout.EndHorizontal();

            if (asset.ShowReferencedByAssets && asset.ReferencedByPaths.Count > 0)
                DrawReferencedByList(asset);

            if (asset.Foldout && !string.IsNullOrEmpty(asset.FalsePositiveWarning))
                DrawFalsePositiveWarning(asset);
        }

        private void DrawRefsButton(DependenciesAssetData asset)
        {
            if (asset.ReferencesCount > 0 && asset.ReferencedByPaths.Count > 0)
            {
                var prevColor = GUI.color;
                GUI.color = asset.ShowReferencedByAssets ? Color.yellow : Color.white;

                var label = asset.ShowReferencedByAssets
                    ? $"Refs:{asset.ReferencesCount} >>"
                    : $"Refs:{asset.ReferencesCount}";

                if (GUILayout.Button(new GUIContent(label, "Show/hide assets that reference this asset"), GUILayout.Width(90f)))
                    asset.ShowReferencedByAssets = !asset.ShowReferencedByAssets;

                GUI.color = prevColor;
            }
            else
            {
                var prevColor = GUI.color;
                GUI.color = Color.yellow;
                GUILayout.Label($"        Refs:{asset.ReferencesCount}", GUILayout.Width(90f));
                GUI.color = prevColor;
            }
        }

        private void DrawReferencedByList(DependenciesAssetData asset)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(50f);

            EditorGUILayout.BeginVertical();

            GUILayout.Label("Used by:", EditorStyles.miniLabel);

            foreach (var refPath in asset.ReferencedByPaths)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16f);
                USGUIUtilities.DrawAssetButton(refPath, 300f, 18f);
                GUILayout.Space(8f);
                GUILayout.Label(refPath, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(10f);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFalsePositiveWarning(DependenciesAssetData asset)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(50f);
            USGUIUtilities.DrawColoredLabel($"[{asset.FalsePositiveWarning}]", Color.yellow);
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Selection & Actions

        private void DrawSelectionAndActions()
        {
            var filtered = GetFilteredAssets();
            var eligibleAssets = filtered.Where(a => a.IsEligibleForDeletion).ToList();
            var selectedCount = eligibleAssets.Count(a => a.Selected);

            EditorGUILayout.BeginHorizontal();

            var allSelected = eligibleAssets.Count > 0 && eligibleAssets.All(a => a.Selected);
            EditorGUI.BeginChangeCheck();
            allSelected = EditorGUILayout.Toggle(allSelected, GUILayout.Width(16f));
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var asset in eligibleAssets)
                    asset.Selected = allSelected;
            }

            GUILayout.Label("Select All Unreferenced", GUILayout.Width(150f));
            GUILayout.Space(10f);

            var countColor = selectedCount > 0 ? Color.yellow : Color.gray;
            USGUIUtilities.DrawColoredLabel($"Selected: {selectedCount}", countColor, 90);

            EditorGUILayout.EndHorizontal();

            if (selectedCount == 0) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Backup Dir:", GUILayout.Width(75f));

            if (string.IsNullOrEmpty(_backupDirectory))
                _backupDirectory = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, "Backups", "DependenciesHunter");

            _backupDirectory = GUILayout.TextField(_backupDirectory);
            if (GUILayout.Button(new GUIContent("Browse", "Browse for backup directory"), GUILayout.Width(60f)))
            {
                var dir = EditorUtility.OpenFolderPanel("Select Backup Directory", _backupDirectory, "");
                if (!string.IsNullOrEmpty(dir))
                    _backupDirectory = dir;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var prevBg = GUI.backgroundColor;

            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button(new GUIContent($"Backup Selected ({selectedCount})", "Backup selected unreferenced assets to the backup directory"), GUILayout.Width(170f)))
                DoBackup(eligibleAssets);

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button(new GUIContent($"Delete Selected ({selectedCount})", "Permanently delete selected unreferenced assets"), GUILayout.Width(170f)))
                DoDelete(eligibleAssets);

            GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
            if (GUILayout.Button(new GUIContent($"Backup + Delete ({selectedCount})", "Backup then delete selected unreferenced assets"), GUILayout.Width(170f)))
                DoBackupAndDelete(eligibleAssets);

            GUI.backgroundColor = prevBg;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DoBackup(List<DependenciesAssetData> eligibleAssets)
        {
            var selected = eligibleAssets.Where(a => a.Selected).ToList();
            if (!EditorUtility.DisplayDialog("Unity Scanner",
                    $"Back up {selected.Count} asset(s) to\n{_backupDirectory}?", "Ok", "Cancel"))
                return;

            var count = DependenciesFixProvider.BackupAssets(selected, _backupDirectory);
            EditorUtility.DisplayDialog("Unity Scanner", $"Backed up {count} asset(s) to\n{_backupDirectory}", "Ok");
        }

        private void DoDelete(List<DependenciesAssetData> eligibleAssets)
        {
            var selected = eligibleAssets.Where(a => a.Selected).ToList();
            if (!EditorUtility.DisplayDialog("Unity Scanner",
                    $"Delete {selected.Count} asset(s)? This cannot be undone.", "Delete", "Cancel"))
                return;

            var deletedPaths = DependenciesFixProvider.DeleteAssets(selected);
            RemoveDeletedAssets(deletedPaths);
            EditorUtility.DisplayDialog("Unity Scanner", $"Deleted {deletedPaths.Count} asset(s).", "Ok");
        }

        private void DoBackupAndDelete(List<DependenciesAssetData> eligibleAssets)
        {
            var selected = eligibleAssets.Where(a => a.Selected).ToList();
            if (!EditorUtility.DisplayDialog("Unity Scanner",
                    $"Back up and delete {selected.Count} asset(s)?\n\nBackup: {_backupDirectory}", "Ok", "Cancel"))
                return;

            var backedUpCount = DependenciesFixProvider.BackupAssets(selected, _backupDirectory);
            var deletedPaths = DependenciesFixProvider.DeleteAssets(selected);
            RemoveDeletedAssets(deletedPaths);
            EditorUtility.DisplayDialog("Unity Scanner",
                $"Backed up {backedUpCount}, Deleted {deletedPaths.Count} asset(s).", "Ok");
        }

        private void RemoveDeletedAssets(List<string> deletedPaths)
        {
            if (_category?.LastAssets == null) return;
            var pathSet = new HashSet<string>(deletedPaths);
            _category.LastAssets.RemoveAll(a => pathSet.Contains(a.Path));
            InvalidateCache();
        }

        #endregion

        #region Filtering & Caching

        private void InvalidateCache()
        {
            _cacheDirty = true;
        }

        private List<DependenciesAssetData> GetFilteredAssets()
        {
            if (!_cacheDirty && _cachedFilteredAssets != null)
                return _cachedFilteredAssets;

            var assets = _category?.LastAssets;
            if (assets == null)
            {
                _cachedFilteredAssets = new List<DependenciesAssetData>();
                _cacheDirty = false;
                return _cachedFilteredAssets;
            }

            var filtered = assets.AsEnumerable();

            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(x => x.Path.IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!string.IsNullOrEmpty(_typeFilter))
                filtered = filtered.Where(x => x.TypeName == _typeFilter);

            var settings = _category.Settings as DependenciesSettings;
            if (settings?.TryUseReflectionForAddressablesDetection == true && !_showAddressables)
                filtered = filtered.Where(x => !x.IsAddressable);

            if (settings != null && !settings.FindUnreferencedOnly && _showUnreferencedOnly)
                filtered = filtered.Where(x => x.ReferencesCount == 0);

            if (_showFalsePositivesOnly)
                filtered = filtered.Where(x => !string.IsNullOrEmpty(x.FalsePositiveWarning));

            var sorted = filtered.ToList();
            ApplySorting(sorted);

            _cachedFilteredAssets = sorted;
            _cacheDirty = false;
            return _cachedFilteredAssets;
        }

        private void ApplySorting(List<DependenciesAssetData> assets)
        {
            switch (_sortType)
            {
                case 0:
                    assets.Sort((a, b) => string.Compare(a.TypeName, b.TypeName, StringComparison.Ordinal));
                    break;
                case 1:
                    assets.Sort((a, b) => string.Compare(b.TypeName, a.TypeName, StringComparison.Ordinal));
                    break;
                case 2:
                    assets.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));
                    break;
                case 3:
                    assets.Sort((a, b) => string.Compare(b.Path, a.Path, StringComparison.Ordinal));
                    break;
                case 4:
                    assets.Sort((a, b) => a.BytesSize.CompareTo(b.BytesSize));
                    break;
                case 5:
                    assets.Sort((a, b) => b.BytesSize.CompareTo(a.BytesSize));
                    break;
            }
        }

        #endregion
    }
}
