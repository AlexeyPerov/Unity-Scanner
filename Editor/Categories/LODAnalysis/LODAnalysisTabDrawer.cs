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

namespace UnityScanner.Categories.LODAnalysis
{
    public class LODAnalysisTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "lod_analysis";
        private const int PageSize = 50;
        private LODAnalysisCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;
        private int _subTab;
        private bool _settingsFoldout;
        private bool _cacheDirty = true;
        private int _expandedRow = -1;
        private string _pathFilter = "";
        private int _severityFilter;
        private List<LODGroupData> _cachedResults;
        private List<LODGroupData> _lastSourceResults;

        public void Bind(LODAnalysisCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category == null || _category.LastResults == null) return;
            DrawFilters();
            USGUIUtilities.HorizontalLine();
            DrawExportButtons();
            USGUIUtilities.HorizontalLine();

            var prevSubTab = _subTab;
            _subTab = GUILayout.Toolbar(_subTab, new[] { "Groups", "Missing Levels", "Material Mismatch", "Ratios" });
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
            var settings = _category.Settings as LODAnalysisSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.CheckMissingLevels = EditorGUILayout.ToggleLeft("Check Missing Levels", settings.CheckMissingLevels);
            settings.CheckNullRenderers = EditorGUILayout.ToggleLeft("Check Null Renderers", settings.CheckNullRenderers);
            settings.CheckRendererCountMismatch = EditorGUILayout.ToggleLeft("Check Renderer Count Mismatch", settings.CheckRendererCountMismatch);
            settings.CheckLastLevelComplex = EditorGUILayout.ToggleLeft("Check Last Level Complex", settings.CheckLastLevelComplex);
            settings.CheckMaterialMismatch = EditorGUILayout.ToggleLeft("Check Material Mismatch", settings.CheckMaterialMismatch);
            settings.CheckTransitionTooClose = EditorGUILayout.ToggleLeft("Check Transition Too Close", settings.CheckTransitionTooClose);
            settings.CheckNoCrossfade = EditorGUILayout.ToggleLeft("Check No Crossfade", settings.CheckNoCrossfade);
            settings.CheckUnnecessary = EditorGUILayout.ToggleLeft("Check Unnecessary LOD Groups", settings.CheckUnnecessary);
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
            if (items.Count == 0) { GUILayout.Label("No LOD groups found.", EditorStyles.miniLabel); return; }
            USGUIPaginationUtilities.DrawPagesWidget(items.Count, _pagination);
            USGUIUtilities.HorizontalLine();
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < items.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination)) continue;
                switch (_subTab)
                {
                    case 0: DrawGroupsRow(items[i], i); break;
                    case 1: DrawMissingLevelsRow(items[i], i); break;
                    case 2: DrawMaterialMismatchRow(items[i], i); break;
                    case 3: DrawRatiosRow(items[i], i); break;
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawGroupsRow(LODGroupData d, int idx)
        {
            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            var countColor = d.LODLevelCount < 2 ? Color.yellow : Color.white;
            USGUIUtilities.DrawColoredLabel("LODs:" + d.LODLevelCount, countColor, 55);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.AssetPath))
                USGUIUtilities.DrawAssetButton(d.AssetPath, 250f, 18f);
            var crossfadeLabel = d.AnimateCrossFading ? "CF:yes" : "CF:no";
            GUILayout.Label(crossfadeLabel, EditorStyles.miniLabel, GUILayout.Width(55));
            GUILayout.Label(d.ObjectName ?? "", EditorStyles.miniLabel, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Levels: " + d.LODLevelCount + " | Cross-fade: " + d.AnimateCrossFading + " | FadeMode: " + (LODFadeMode)d.FadeMode, EditorStyles.miniLabel);
                if (d.IsUIElement) GUILayout.Label("WARNING: LOD on UI element", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.yellow } });
                if (d.IsSmallObject) GUILayout.Label("NOTE: Small object (scale < 0.5)", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.cyan } });
                foreach (var level in d.Levels)
                {
                    var nullWarn = level.HasNullRenderers ? " [NULL RENDERERS: " + level.NullRendererCount + "]" : "";
                    GUILayout.Label("  LOD" + level.LevelIndex + ": " + level.ScreenTransitionHeight.ToString("F3") +
                        " height, " + level.RendererCount + " renderers, " + level.TriangleCount + " tris" + nullWarn, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawMissingLevelsRow(LODGroupData d, int idx)
        {
            if (!d.Levels.Any(l => l.HasNullRenderers) && d.LODLevelCount >= 2) return;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var missColor = d.Levels.Any(l => l.HasNullRenderers) ? Color.red : (d.LODLevelCount < 2 ? Color.yellow : Color.white);
            USGUIUtilities.DrawColoredLabel("Levels:" + d.LODLevelCount, missColor, 60);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.AssetPath))
                USGUIUtilities.DrawAssetButton(d.AssetPath, 250f, 18f);
            var nullInfo = d.Levels.Where(l => l.HasNullRenderers)
                .Select(l => "LOD" + l.LevelIndex + ":" + l.NullRendererCount + " null");
            GUILayout.Label(string.Join(", ", nullInfo), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMaterialMismatchRow(LODGroupData d, int idx)
        {
            if (d.Levels.Count < 2) return;
            var lod0Materials = d.Levels[0].MaterialNames;
            var hasMismatch = false;
            foreach (var level in d.Levels.Skip(1))
            {
                if (level.ScreenTransitionHeight > 0f && level.MaterialNames.Count > 0)
                {
                    if (!lod0Materials.All(m => level.MaterialNames.Contains(m)) ||
                        !level.MaterialNames.All(m => lod0Materials.Contains(m)))
                    {
                        hasMismatch = true;
                        break;
                    }
                }
            }
            if (!hasMismatch) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            USGUIUtilities.DrawColoredLabel("Mat Mismatch", Color.yellow, 90);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.AssetPath))
                USGUIUtilities.DrawAssetButton(d.AssetPath, 250f, 18f);
            GUILayout.Label(d.ObjectName ?? "", EditorStyles.miniLabel, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRatiosRow(LODGroupData d, int idx)
        {
            var hasCloseTransitions = false;
            for (var i = 0; i < d.Levels.Count - 1; i++)
            {
                var a = d.Levels[i];
                var b = d.Levels[i + 1];
                if (a.ScreenTransitionHeight > 0f && b.ScreenTransitionHeight > 0f)
                {
                    var diff = a.ScreenTransitionHeight - b.ScreenTransitionHeight;
                    if (diff > 0f && diff < 0.05f)
                    {
                        hasCloseTransitions = true;
                        break;
                    }
                }
            }
            if (!hasCloseTransitions && d.AnimateCrossFading) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (hasCloseTransitions)
                USGUIUtilities.DrawColoredLabel("Close Trans.", Color.cyan, 80);
            else
                USGUIUtilities.DrawColoredLabel("No Crossfade", Color.gray, 80);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.AssetPath))
                USGUIUtilities.DrawAssetButton(d.AssetPath, 250f, 18f);
            var heights = string.Join(" > ", d.Levels.Select(l => l.ScreenTransitionHeight.ToString("F3")));
            GUILayout.Label(heights, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("LOD Analysis:");
            foreach (var d in GetFilteredResults())
            {
                sb.AppendLine(d.ObjectName + " | " + d.AssetPath + " | Levels:" + d.LODLevelCount +
                    " CrossFade:" + d.AnimateCrossFading);
                foreach (var l in d.Levels)
                    sb.AppendLine("  LOD" + l.LevelIndex + " height:" + l.ScreenTransitionHeight.ToString("F3") +
                        " renderers:" + l.RendererCount + " tris:" + l.TriangleCount);
            }
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export LOD Analysis", Application.dataPath, "lod_analysis.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("ObjectName,AssetPath,ScenePath,LODLevelCount,AnimateCrossFading,FadeMode,LevelIndex,ScreenTransitionHeight,RendererCount,TriangleCount,HasNullRenderers,MaterialCount");
            foreach (var d in GetFilteredResults())
            {
                foreach (var l in d.Levels)
                    sb.AppendLine(USExportUtilities.EscapeCsvField(d.ObjectName ?? "") + "," +
                        USExportUtilities.EscapeCsvField(d.AssetPath) + "," +
                        USExportUtilities.EscapeCsvField(d.ScenePath ?? "") + "," +
                        d.LODLevelCount + "," + d.AnimateCrossFading + "," + d.FadeMode + "," +
                        l.LevelIndex + "," + l.ScreenTransitionHeight.ToString("F4") + "," +
                        l.RendererCount + "," + l.TriangleCount + "," +
                        l.HasNullRenderers + "," + l.MaterialNames.Count);
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private List<LODGroupData> GetFilteredResults()
        {
            EnsureCache();
            return _cachedResults;
        }

        private void EnsureCache()
        {
            var src = _category?.LastResults;
            if (!_cacheDirty && _cachedResults != null && ReferenceEquals(src, _lastSourceResults)) return;
            _lastSourceResults = src;
            var raw = src ?? new List<LODGroupData>();
            var f = raw.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                f = f.Where(x => (x.AssetPath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 (x.ObjectName ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_severityFilter == 1)
                f = f.Where(x => x.WarningLevel >= 3);
            else if (_severityFilter == 2)
                f = f.Where(x => x.WarningLevel >= 2);
            _cachedResults = f.ToList();
            _cacheDirty = false;
        }
    }
}
