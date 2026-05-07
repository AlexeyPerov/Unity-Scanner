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

namespace UnityScanner.Categories.Sprite2DAnalysis
{
    public class Sprite2DAnalysisTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "sprite_2d_analysis";
        private const int PageSize = 50;
        private Sprite2DAnalysisCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;
        private int _subTab;
        private bool _settingsFoldout;
        private bool _cacheDirty = true;
        private int _expandedRow = -1;
        private string _pathFilter = "";
        private int _severityFilter;
        private List<object> _cachedResults;
        private int _lastSubTab = -1;
        private List<SpriteAtlasData> _lastAtlasSrc;
        private List<SpriteEntry> _lastSpriteSrc;
        private List<DuplicateGroup> _lastDupSrc;

        public void Bind(Sprite2DAnalysisCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category == null) return;
            if (_category.LastAtlasResults == null) return;
            DrawFilters();
            USGUIUtilities.HorizontalLine();
            DrawExportButtons();
            USGUIUtilities.HorizontalLine();

            var prevSubTab = _subTab;
            _subTab = GUILayout.Toolbar(_subTab, new[] { "Duplicates", "Atlases", "Sprites", "Packing" });
            if (_subTab != prevSubTab) { _expandedRow = -1; _cacheDirty = true; }

            DrawFilteredList();
        }

        public void DrawTopBar(UnityScannerResult result)
        {
            if (_category == null) return;
            DrawSettings();
        }

        private void DrawSettings()
        {
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Analysis Settings");
            if (!_settingsFoldout) return;
            EditorGUI.indentLevel++;
            var settings = _category.Settings as Sprite2DAnalysisSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.CheckAtlasEfficiency = EditorGUILayout.ToggleLeft("Check Atlas Efficiency", settings.CheckAtlasEfficiency);
            settings.CheckNotPacked = EditorGUILayout.ToggleLeft("Check Not Packed", settings.CheckNotPacked);
            settings.CheckAtlasPlatformInconsistent = EditorGUILayout.ToggleLeft("Check Atlas Platform Inconsistent", settings.CheckAtlasPlatformInconsistent);
            settings.CheckPolygonVerticesExcessive = EditorGUILayout.ToggleLeft("Check Polygon Vertices Excessive", settings.CheckPolygonVerticesExcessive);
            settings.CheckSheetUnevenCells = EditorGUILayout.ToggleLeft("Check Sheet Uneven Cells", settings.CheckSheetUnevenCells);
            settings.CheckFullRectUnnecessary = EditorGUILayout.ToggleLeft("Check Full Rect Unnecessary", settings.CheckFullRectUnnecessary);
            settings.CheckDuplicateContent = EditorGUILayout.ToggleLeft("Check Duplicate Content", settings.CheckDuplicateContent);
            settings.PathFilter = EditorGUILayout.TextField("Path Filter", settings.PathFilter);
            if (EditorGUI.EndChangeCheck()) _cacheDirty = true;
            EditorGUI.indentLevel--;
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Path:", GUILayout.Width(40));
            var newPath = GUILayout.TextField(_pathFilter, GUILayout.Width(200));
            if (newPath != _pathFilter) { _pathFilter = newPath; _cacheDirty = true; }

            var sevLabel = _severityFilter switch { 1 => "Errors", 2 => "Errors+Warn", _ => "All Severity" };
            if (GUILayout.Button(new GUIContent("Severity: " + sevLabel, "Filter by severity"), GUILayout.Width(130)))
            { _severityFilter = _severityFilter >= 2 ? 0 : _severityFilter + 1; _cacheDirty = true; }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawExportButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Copy to Clipboard", "Copy filtered data"), GUILayout.Width(140))) ExportToClipboard();
            if (GUILayout.Button(new GUIContent("Export CSV...", "Export to CSV"), GUILayout.Width(100))) ExportToCsv();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilteredList()
        {
            var items = GetFilteredResults();
            if (items != null && items.Count == 0) { GUILayout.Label("Nothing found."); return; }

            if (items == null) return;
            USGUIPaginationUtilities.DrawPagesWidget(items.Count, _pagination);
            USGUIUtilities.HorizontalLine();
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < items.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination)) continue;
                switch (_subTab)
                {
                    case 0: DrawAtlasRow(items[i] as SpriteAtlasData, i); break;
                    case 1: DrawSpriteRow(items[i] as SpriteEntry, i); break;
                    case 2: DrawPackingRow(items[i] as SpriteEntry, i); break;
                    case 3: DrawDuplicateRow(items[i] as DuplicateGroup, i); break;
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawAtlasRow(SpriteAtlasData d, int idx)
        {
            if (d == null) return;
            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            var wasteColor = d.UnusedRatio > 0.3f ? Color.yellow : Color.white;
            USGUIUtilities.DrawColoredLabel("Waste:" + (d.UnusedRatio * 100).ToString("F0") + "%", wasteColor, 70);
            EditorGUILayout.LabelField("Sprites:" + d.SpriteCount, EditorStyles.miniLabel, GUILayout.Width(70));
            if (!string.IsNullOrEmpty(d.AssetPath))
                USGUIUtilities.DrawAssetButton(d.AssetPath, 250f, 18f);
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Atlas area: " + d.AtlasPixelArea + " | Used: " + d.UsedPixelArea + " | Waste: " + (d.UnusedRatio * 100).ToString("F1") + "%", EditorStyles.miniLabel);
                foreach (var s in d.Sprites.Take(15))
                    GUILayout.Label("  " + s.SpriteName + " " + s.Width + "x" + s.Height, EditorStyles.miniLabel);
                if (d.Sprites.Count > 15)
                    GUILayout.Label("  ... and " + (d.Sprites.Count - 15) + " more", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawSpriteRow(SpriteEntry d, int idx)
        {
            if (d == null) return;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(d.SpriteName, EditorStyles.miniLabel, GUILayout.Width(150));
            GUILayout.Label(d.Width + "x" + d.Height, EditorStyles.miniLabel, GUILayout.Width(70));
            GUILayout.Label(d.MeshType ?? "", EditorStyles.miniLabel, GUILayout.Width(60));
            if (!string.IsNullOrEmpty(d.AssetPath))
                USGUIUtilities.DrawAssetButton(d.AssetPath, 250f, 18f);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPackingRow(SpriteEntry d, int idx)
        {
            if (d == null || d.IsInAtlas) return;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            USGUIUtilities.DrawColoredLabel("UNPACKED", Color.cyan, 70);
            GUILayout.Label(d.SpriteName + " " + d.Width + "x" + d.Height, EditorStyles.miniLabel, GUILayout.Width(180));
            if (!string.IsNullOrEmpty(d.AssetPath))
                USGUIUtilities.DrawAssetButton(d.AssetPath, 250f, 18f);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDuplicateRow(DuplicateGroup d, int idx)
        {
            if (d == null) return;
            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            USGUIUtilities.DrawColoredLabel("DUP:" + d.AssetPaths.Count, Color.yellow, 55);
            GUILayout.Label((d.ContentSizeBytes / 1024) + " KB", EditorStyles.miniLabel, GUILayout.Width(60));
            if (d.AssetPaths.Count > 0 && !string.IsNullOrEmpty(d.AssetPaths[0]))
                USGUIUtilities.DrawAssetButton(d.AssetPaths[0], 250f, 18f);
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Duplicate content detected — these files have identical binary content.", EditorStyles.miniLabel);
                GUILayout.Label("Fix: Keep one file, update all references to point to the retained copy, delete the duplicates.", EditorStyles.miniLabel);
                GUILayout.Space(4);
                foreach (var p in d.AssetPaths)
                {
                    GUILayout.Label("  " + p, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Sprite & 2D Analysis:");
            if (_subTab == 0)
            {
                foreach (var d in GetFilteredResults().OfType<SpriteAtlasData>())
                    sb.AppendLine("Atlas: " + d.AssetPath + " Sprites:" + d.SpriteCount + " Waste:" + (d.UnusedRatio * 100).ToString("F1") + "%");
            }
            else
            {
                foreach (var d in GetFilteredResults().OfType<SpriteEntry>())
                    sb.AppendLine("Sprite: " + d.SpriteName + " " + d.Width + "x" + d.Height + " Atlas:" + d.IsInAtlas);
            }
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Sprite 2D Analysis", Application.dataPath, "sprite_2d_analysis.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("Name,AssetPath,Width,Height,IsInAtlas,AtlasPath,MeshType,PolygonVertices");
            foreach (var item in GetFilteredResults().OfType<SpriteEntry>())
                sb.AppendLine(USExportUtilities.EscapeCsvField(item.SpriteName) + "," +
                    USExportUtilities.EscapeCsvField(item.AssetPath) + "," +
                    item.Width + "," + item.Height + "," + item.IsInAtlas + "," +
                    USExportUtilities.EscapeCsvField(item.AtlasPath ?? "") + "," +
                    USExportUtilities.EscapeCsvField(item.MeshType ?? "") + "," +
                    item.PolygonVertexCount);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private List<object> GetFilteredResults()
        {
            EnsureCache();
            return _cachedResults;
        }

        private void EnsureCache()
        {
            if (!_cacheDirty && _cachedResults != null && _lastSubTab == _subTab) return;

            var filtered = new List<object>();

            if (_subTab == 0)
            {
                var src = _category?.LastAtlasResults ?? new List<SpriteAtlasData>();
                filtered = src.OfType<object>().ToList();
                if (!string.IsNullOrEmpty(_pathFilter))
                    filtered = filtered.Cast<SpriteAtlasData>()
                        .Where(x => (x.AssetPath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                        .OfType<object>().ToList();
            }
            else if (_subTab == 1 || _subTab == 2)
            {
                var src = _category?.LastSpriteResults ?? new List<SpriteEntry>();
                var f = src.AsEnumerable();
                if (_subTab == 2) f = f.Where(s => !s.IsInAtlas);
                if (!string.IsNullOrEmpty(_pathFilter))
                    f = f.Where(x => (x.AssetPath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     (x.SpriteName ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                filtered = f.OfType<object>().ToList();
            }
            else if (_subTab == 3)
            {
                var src = _category?.LastDuplicateResults ?? new List<DuplicateGroup>();
                filtered = src.OfType<object>().ToList();
            }

            _cachedResults = filtered;
            _cacheDirty = false;
            _lastSubTab = _subTab;
        }
    }
}
