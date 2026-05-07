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

namespace UnityScanner.Categories.UICanvasAnalysis
{
    public class UICanvasAnalysisTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "ui_canvas_analysis";
        private const int PageSize = 50;
        private UICanvasAnalysisCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;
        private int _subTab;
        private bool _settingsFoldout;
        private bool _cacheDirty = true;
        private int _expandedRow = -1;
        private string _pathFilter = "";
        private int _severityFilter;
        private List<CanvasData> _cachedResults;
        private List<CanvasData> _lastSourceResults;

        public void Bind(UICanvasAnalysisCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
            if (result != null && result.Issues.Count > 0)
            {
                var warns = result.Issues.Count(i => i.Severity == UnityScanner.Core.Issues.UnityScannerIssueSeverity.Warning);
                var infos = result.Issues.Count(i => i.Severity == UnityScanner.Core.Issues.UnityScannerIssueSeverity.Info);
                if (warns > 0 || infos > 0)
                    GUILayout.Label("Issues: " + warns + " warnings, " + infos + " info", EditorStyles.miniLabel);
            }
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category == null)
            {
                return;
            }
            
            var results = _category.LastResults;
            
            if (results == null || results.Count == 0)
            {
                return;
            }
            
            DrawFilters();
            USGUIUtilities.HorizontalLine();
            DrawExportButtons();
            USGUIUtilities.HorizontalLine();

            var prevSubTab = _subTab;
            _subTab = GUILayout.Toolbar(_subTab, new[] { "Canvases", "Raycasts", "Layout", "Text/TMP" });
            if (_subTab != prevSubTab) _expandedRow = -1;

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
            var settings = _category.Settings as UICanvasAnalysisSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.CheckUnusedShaderChannels = EditorGUILayout.ToggleLeft("Check Unused Shader Channels", settings.CheckUnusedShaderChannels);
            settings.CheckNestedRedundancy = EditorGUILayout.ToggleLeft("Check Nested Redundancy", settings.CheckNestedRedundancy);
            settings.CheckRaycastTargets = EditorGUILayout.ToggleLeft("Check Raycast Targets", settings.CheckRaycastTargets);
            settings.CheckTextTmpMix = EditorGUILayout.ToggleLeft("Check Text/TMP Mix", settings.CheckTextTmpMix);
            settings.CheckLayoutNesting = EditorGUILayout.ToggleLeft("Check Layout Nesting", settings.CheckLayoutNesting);
            settings.CheckVertexCount = EditorGUILayout.ToggleLeft("Check Vertex Count", settings.CheckVertexCount);
            settings.CheckAtlasWaste = EditorGUILayout.ToggleLeft("Check Atlas Waste", settings.CheckAtlasWaste);
            settings.ScanPrefabs = EditorGUILayout.ToggleLeft("Scan Prefabs", settings.ScanPrefabs);
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
            if (items.Count == 0) { GUILayout.Label("No issues", EditorStyles.miniLabel); return; }
            USGUIPaginationUtilities.DrawPagesWidget(items.Count, _pagination);
            USGUIUtilities.HorizontalLine();
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < items.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination)) continue;
                switch (_subTab)
                {
                    case 0: DrawCanvasRow(items[i], i); break;
                    case 1: DrawRaycastRow(items[i], i); break;
                    case 2: DrawLayoutRow(items[i], i); break;
                    case 3: DrawTextRow(items[i], i); break;
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawCanvasRow(CanvasData d, int idx)
        {
            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            var vColor = d.VertexCount > 10000 ? Color.yellow : Color.white;
            USGUIUtilities.DrawColoredLabel("V:" + d.VertexCount, vColor, 55);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.ScenePath))
                USGUIUtilities.DrawAssetButton(d.ScenePath, 200f, 18f);
            GUILayout.Label(d.CanvasName, EditorStyles.miniLabel, GUILayout.Width(120));
            GUILayout.Label(d.RenderMode ?? "", EditorStyles.miniLabel, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Vertex count: " + d.VertexCount + " | Children: " + d.ChildCount, EditorStyles.miniLabel);
                if (d.IsNestedRedundant)
                { var prev = GUI.color; GUI.color = Color.yellow; GUILayout.Label("Redundant nested canvas. Parent: " + d.ParentCanvasPath, EditorStyles.miniLabel); GUI.color = prev; }
                if (!string.IsNullOrEmpty(d.EnabledChannels) && d.EnabledChannels != "None")
                { var prev = GUI.color; GUI.color = Color.cyan; GUILayout.Label("Unused shader channels: " + d.EnabledChannels + " (used: " + (d.UsedChannels ?? "None") + ")", EditorStyles.miniLabel); GUI.color = prev; }
                GUILayout.Label("Unpacked sprites: " + d.UnpackedSpriteCount, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawRaycastRow(CanvasData d, int idx)
        {
            if (d.UnnecessaryRaycastCount == 0) return;
            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            USGUIUtilities.DrawColoredLabel("RT:" + d.UnnecessaryRaycastCount, Color.yellow, 50);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.ScenePath))
                USGUIUtilities.DrawAssetButton(d.ScenePath, 200f, 18f);
            GUILayout.Label(d.CanvasName, EditorStyles.miniLabel, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Total raycast targets: " + d.RaycastTargetCount + " | Unnecessary: " + d.UnnecessaryRaycastCount, EditorStyles.miniLabel);
                foreach (var rt in d.UnnecessaryRaycasts.Take(20))
                    GUILayout.Label("  " + rt.ComponentType + " — " + rt.ObjectPath, EditorStyles.miniLabel);
                if (d.UnnecessaryRaycasts.Count > 20)
                    GUILayout.Label("  ... and " + (d.UnnecessaryRaycasts.Count - 20) + " more", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawLayoutRow(CanvasData d, int idx)
        {
            if (d.LayoutNestingDepth <= 1) return;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var depthColor = d.LayoutNestingDepth > 5 ? Color.yellow : Color.white;
            USGUIUtilities.DrawColoredLabel("D:" + d.LayoutNestingDepth, depthColor, 35);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.ScenePath))
                USGUIUtilities.DrawAssetButton(d.ScenePath, 200f, 18f);
            GUILayout.Label(d.CanvasName, EditorStyles.miniLabel, GUILayout.Width(120));
            GUILayout.Label("Layouts: " + string.Join(", ", d.LayoutTypes), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTextRow(CanvasData d, int idx)
        {
            if (d.LegacyTextCount == 0 && d.TmpTextCount == 0) return;
            var hasMix = d.LegacyTextCount > 0 && d.TmpTextCount > 0;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (hasMix) USGUIUtilities.DrawColoredLabel("MIX", Color.yellow, 35);
            else GUILayout.Label("", GUILayout.Width(35));
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.ScenePath))
                USGUIUtilities.DrawAssetButton(d.ScenePath, 200f, 18f);
            GUILayout.Label(d.CanvasName, EditorStyles.miniLabel, GUILayout.Width(120));
            GUILayout.Label("Legacy: " + d.LegacyTextCount + " | TMP: " + d.TmpTextCount, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("UI/Canvas Analysis:");
            foreach (var d in GetFilteredResults())
            {
                sb.AppendLine("Canvas: " + d.CanvasName + " | V:" + d.VertexCount + " Children:" + d.ChildCount +
                    " RT:" + d.UnnecessaryRaycastCount + " Layout:" + d.LayoutNestingDepth +
                    " Legacy:" + d.LegacyTextCount + " TMP:" + d.TmpTextCount + " | " + d.ScenePath);
            }
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export UI/Canvas Analysis", Application.dataPath, "ui_canvas_analysis.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("ScenePath,CanvasName,VertexCount,ChildCount,RaycastTargets,UnnecessaryRaycasts,LayoutDepth,LegacyText,TmpText,UnpackedSprites,IsRedundant,RenderMode");
            foreach (var d in GetFilteredResults())
                sb.AppendLine(USExportUtilities.EscapeCsvField(d.ScenePath) + "," +
                    USExportUtilities.EscapeCsvField(d.CanvasName) + "," +
                    d.VertexCount + "," + d.ChildCount + "," +
                    d.RaycastTargetCount + "," + d.UnnecessaryRaycastCount + "," +
                    d.LayoutNestingDepth + "," + d.LegacyTextCount + "," +
                    d.TmpTextCount + "," + d.UnpackedSpriteCount + "," +
                    d.IsNestedRedundant + "," + USExportUtilities.EscapeCsvField(d.RenderMode ?? ""));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private List<CanvasData> GetFilteredResults()
        {
            EnsureCache();
            return _cachedResults;
        }

        private void EnsureCache()
        {
            var src = _category?.LastResults;
            if (!_cacheDirty && _cachedResults != null && ReferenceEquals(src, _lastSourceResults)) return;
            _lastSourceResults = src;
            var raw = src ?? new List<CanvasData>();
            var f = raw.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                f = f.Where(x => (x.ScenePath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 (x.CanvasName ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_severityFilter == 1)
                f = f.Where(x => x.WarningLevel >= 3);
            else if (_severityFilter == 2)
                f = f.Where(x => x.WarningLevel >= 2);
            _cachedResults = f.ToList();
            _cacheDirty = false;
        }
    }
}
