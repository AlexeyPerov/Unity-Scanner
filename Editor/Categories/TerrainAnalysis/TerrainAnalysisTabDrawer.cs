using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Export;
using UnityScanner.Core.Results;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.TerrainAnalysis
{
    public class TerrainAnalysisTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "terrain_analysis";

        private const int PageSize = 50;

        private TerrainAnalysisCategory _category;
        public System.Action OnScanRequested;
        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;

        private string _pathFilter = "";
        private bool _warningsOnly;
        private int _sortMode;
        private bool _settingsFoldout;
        private int _expandedRow = -1;

        private List<TerrainDataInfo> _cachedTerrains;
        private bool _cacheDirty = true;

        public void Bind(TerrainAnalysisCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;

            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category?.LastTerrains == null) return;

            DrawFilters();
            USGUIUtilities.HorizontalLine();
            DrawExportButtons();
            USGUIUtilities.HorizontalLine();
            var filtered = GetFiltered();
            USGUIPaginationUtilities.DrawPagesWidget(filtered.Count, _pagination);
            USGUIUtilities.HorizontalLine();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < filtered.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination)) continue;
                DrawTerrainRow(filtered[i], i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        public void DrawTopBar(UnityScannerResult result)
        {
            if (_category == null) return;
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Analysis Settings");
            if (!_settingsFoldout) return;

            EditorGUI.indentLevel++;
            var settings = _category.Settings as TerrainAnalysisSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }

            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.ControlMapMemoryBudgetMB = EditorGUILayout.IntField("Control Map Budget (MB)", settings.ControlMapMemoryBudgetMB);
            settings.MaxTerrainTextureSize = EditorGUILayout.IntField("Max Texture Size", settings.MaxTerrainTextureSize);
            settings.TreeDensityThreshold = EditorGUILayout.IntField("Tree Density Threshold", settings.TreeDensityThreshold);
            settings.DetailDensityThreshold = EditorGUILayout.IntField("Detail Density Threshold", settings.DetailDensityThreshold);
            settings.DetectMissingLayers = EditorGUILayout.ToggleLeft("Detect Missing Layers", settings.DetectMissingLayers);
            settings.DetectColliderMismatches = EditorGUILayout.ToggleLeft("Detect Collider Mismatches", settings.DetectColliderMismatches);
            settings.DetectTextureBudgetOverages = EditorGUILayout.ToggleLeft("Detect Texture Budget Overages", settings.DetectTextureBudgetOverages);
            settings.DetectDensityOverages = EditorGUILayout.ToggleLeft("Detect Density Overages", settings.DetectDensityOverages);
            settings.DetectExpensiveSettings = EditorGUILayout.ToggleLeft("Detect Expensive Settings", settings.DetectExpensiveSettings);
            if (EditorGUI.EndChangeCheck()) InvalidateCache();
            EditorGUI.indentLevel--;
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Path:", GUILayout.Width(40));
            var newPath = GUILayout.TextField(_pathFilter, GUILayout.Width(250));
            if (newPath != _pathFilter) { _pathFilter = newPath; InvalidateCache(); }
            EditorGUI.BeginChangeCheck();
            _warningsOnly = EditorGUILayout.ToggleLeft("Warnings Only", _warningsOnly, GUILayout.Width(140));
            if (EditorGUI.EndChangeCheck()) InvalidateCache();
            var sortLabel = _sortMode switch { 0 => "Warnings Desc", 1 => "Path A-Z", 2 => "Layers", _ => "Warnings Desc" };
            if (GUILayout.Button(new GUIContent($"Sort: {sortLabel}", "Sort entries by this criteria"), GUILayout.Width(120)))
            { _sortMode = _sortMode >= 2 ? 0 : _sortMode + 1; InvalidateCache(); }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawExportButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Copy to Clipboard", "Copy filtered data to clipboard"), GUILayout.Width(140))) ExportToClipboard();
            if (GUILayout.Button(new GUIContent("Export CSV...", "Export filtered data to a CSV file"), GUILayout.Width(100))) ExportToCsv();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTerrainRow(TerrainDataInfo t, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var isExpanded = _expandedRow == index;
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;

            var sevColor = t.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, 1 => Color.cyan, _ => Color.white };
            USGUIUtilities.DrawColoredLabel($"[{t.WarningLevel}]", sevColor, 30);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            USGUIUtilities.DrawAssetButton(t.Path, 300f, 18f);
            GUILayout.Label($"Layers:{t.LayerCount}", EditorStyles.miniLabel, GUILayout.Width(70));
            GUILayout.Label($"Trees:{t.TreeCount}", EditorStyles.miniLabel, GUILayout.Width(65));
            GUILayout.Label($"ControlMap:{t.ControlMapResolution}", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            if (isExpanded) DrawTerrainDetail(t);
        }

        private void DrawTerrainDetail(TerrainDataInfo t)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"Path: {t.Path}", EditorStyles.miniLabel);
            GUILayout.Label($"Layers: {t.LayerCount} (Missing: {t.MissingLayerCount})", EditorStyles.miniLabel);
            if (t.MissingLayerCount > 0)
                USGUIUtilities.DrawColoredLabel($"  Missing: {string.Join(", ", t.MissingLayerNames)}", Color.red);
            GUILayout.Label($"Trees: {t.TreeCount} | Detail: {t.DetailCount}", EditorStyles.miniLabel);
            GUILayout.Label($"Control Map: {t.ControlMapResolution} | Alphamap Textures: {t.AlphamapTextureCount}", EditorStyles.miniLabel);
            var controlMB = t.ControlMapMemoryBytes / (1024.0 * 1024.0);
            GUILayout.Label($"Control Map Memory: {controlMB:F1} MB", EditorStyles.miniLabel);
            GUILayout.Label($"Heightmap Resolution: {t.HeightmapResolution}", EditorStyles.miniLabel);
            if (t.HasColliderMismatch) USGUIUtilities.DrawColoredLabel("COLLIDER DATA MISMATCH", Color.red, 160);
            if (t.HasExpensiveSettings) USGUIUtilities.DrawColoredLabel("Expensive settings for platform", Color.yellow);
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            var list = GetFiltered();
            sb.AppendLine($"Terrain Analysis [{list.Count} terrains]:");
            foreach (var t in list)
                sb.AppendLine($"[{t.WarningLevel}] {t.Name} | Layers:{t.LayerCount} Trees:{t.TreeCount} | {t.Path}");
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Terrain Analysis", Application.dataPath, "terrain_analysis.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("WarningLevel,Name,Path,Layers,MissingLayers,Trees,Detail,ControlMapRes,ControlMapMB,ColliderMismatch,ExpensiveSettings");
            foreach (var t in GetFiltered())
            {
                var mb = t.ControlMapMemoryBytes / (1024.0 * 1024.0);
                sb.AppendLine($"{t.WarningLevel},{USExportUtilities.EscapeCsvField(t.Name)},{USExportUtilities.EscapeCsvField(t.Path)}," +
                    $"{t.LayerCount},{t.MissingLayerCount},{t.TreeCount},{t.DetailCount},{t.ControlMapResolution}," +
                    $"{mb:F1},{t.HasColliderMismatch},{t.HasExpensiveSettings}");
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private void InvalidateCache() { _cacheDirty = true; }

        private List<TerrainDataInfo> GetFiltered()
        {
            if (!_cacheDirty && _cachedTerrains != null) return _cachedTerrains;
            var terrains = _category?.LastTerrains;
            if (terrains == null) { _cachedTerrains = new List<TerrainDataInfo>(); _cacheDirty = false; return _cachedTerrains; }
            var filtered = terrains.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(t => t.Path.IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_warningsOnly) filtered = filtered.Where(t => t.WarningLevel > 0);
            var sorted = filtered.ToList();
            switch (_sortMode)
            {
                case 0: sorted.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel)); break;
                case 1: sorted.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal)); break;
                case 2: sorted.Sort((a, b) => a.LayerCount.CompareTo(b.LayerCount)); break;
            }
            _cachedTerrains = sorted; _cacheDirty = false;
            return _cachedTerrains;
        }
    }
}
