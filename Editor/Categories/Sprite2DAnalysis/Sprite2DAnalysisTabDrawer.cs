using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Export;
using UnityScanner.Core.Issues;
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
        private bool _settingsFoldout;
        private int _expandedRow = -1;
        private string _pathFilter = "";
        private int _severityFilter;
        private int _typeFilter;
        private int _sortMode;
        private List<UnityScannerIssue> _cached;
        private bool _cacheDirty = true;

        private static readonly string[] TypeLabels = { "All", "Atlas Waste", "Not Packed", "Polygon Vertices", "Uneven Cells", "Full Rect", "Duplicate" };
        private static readonly string[] TypeCodes = { "", "sprite_atlas_low_efficiency", "sprite_not_packed", "sprite_polygon_vertices_excessive", "sprite_sheet_uneven_cells", "sprite_full_rect_unnecessary", "sprite_duplicate_content" };

        public void Bind(Sprite2DAnalysisCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
            
            DrawSettings();
            USGUIUtilities.HorizontalLine();
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category == null) return;

            var issues = _category.LastIssues;
            if (issues == null)
            {
                GUILayout.Label("No results yet. Run analysis.", EditorStyles.miniLabel);
                return;
            }

            if (issues.Count == 0)
            {
                GUILayout.Label("No issues found.", EditorStyles.miniLabel);
                return;
            }

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
                DrawIssueRow(filtered[i], i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        public void DrawTopBar(UnityScannerResult result)
        {
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
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Min Not Packed Sprite Size:", GUILayout.Width(200));
            settings.MinNotPackedSpriteSize = EditorGUILayout.IntField(settings.MinNotPackedSpriteSize, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Max Polygon Vertex Count:", GUILayout.Width(200));
            settings.MaxPolygonVertexCount = EditorGUILayout.IntField(settings.MaxPolygonVertexCount, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Min Full Rect Sprite Size:", GUILayout.Width(200));
            settings.MinFullRectSpriteSize = EditorGUILayout.IntField(settings.MinFullRectSpriteSize, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) _cacheDirty = true;
            EditorGUI.indentLevel--;
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Path:", GUILayout.Width(40));
            var newPath = GUILayout.TextField(_pathFilter, GUILayout.Width(200));
            if (newPath != _pathFilter) { _pathFilter = newPath; _cacheDirty = true; }

            GUILayout.Label("Type:", GUILayout.Width(35));
            var prevType = _typeFilter;
            _typeFilter = EditorGUILayout.Popup(_typeFilter, TypeLabels, GUILayout.Width(120));
            if (_typeFilter != prevType) _cacheDirty = true;

            var sevLabel = _severityFilter switch { 1 => "Errors", 2 => "Errors+Warn", _ => "All" };
            if (GUILayout.Button(new GUIContent("Severity: " + sevLabel, "Filter by severity"), GUILayout.Width(130)))
            { _severityFilter = _severityFilter >= 2 ? 0 : _severityFilter + 1; _cacheDirty = true; }

            var sortLabel = _sortMode switch { 0 => "Sev Desc", 1 => "Path A-Z", 2 => "Type", _ => "Sev Desc" };
            if (GUILayout.Button(new GUIContent("Sort: " + sortLabel, "Sort issues"), GUILayout.Width(100)))
            { _sortMode = _sortMode >= 2 ? 0 : _sortMode + 1; _cacheDirty = true; }

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

        private void DrawIssueRow(UnityScannerIssue issue, int index)
        {
            var isExpanded = _expandedRow == index;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;

            var sevColor = issue.Severity switch
            {
                UnityScannerIssueSeverity.Error => Color.red,
                UnityScannerIssueSeverity.Warning => Color.yellow,
                UnityScannerIssueSeverity.Info => Color.cyan,
                _ => Color.white
            };
            USGUIUtilities.DrawColoredLabel(USGUIUtilities.GetSeverityTag(issue.Severity == UnityScannerIssueSeverity.Error ? 3 : issue.Severity == UnityScannerIssueSeverity.Warning ? 2 : 1), sevColor, 75);

            var typeShort = IssueCodeToLabel(issue.IssueCode);
            GUILayout.Label(typeShort, EditorStyles.miniLabel, GUILayout.Width(100));

            if (!string.IsNullOrEmpty(issue.AssetPath))
                USGUIUtilities.DrawAssetButton(issue.AssetPath, 250f, 18f);

            EditorGUILayout.EndHorizontal();

            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(issue.Description, EditorStyles.wordWrappedLabel);
                if (issue.Metadata != null && issue.Metadata.Count > 0)
                {
                    GUILayout.Space(2);
                    foreach (var kvp in issue.Metadata)
                        GUILayout.Label("  " + kvp.Key + ": " + kvp.Value, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private static string IssueCodeToLabel(string code)
        {
            return code switch
            {
                "sprite_atlas_low_efficiency" => "Atlas Waste",
                "sprite_not_packed" => "Not Packed",
                "sprite_polygon_vertices_excessive" => "Polygon Vertices",
                "sprite_sheet_uneven_cells" => "Uneven Cells",
                "sprite_full_rect_unnecessary" => "Full Rect",
                "sprite_duplicate_content" => "Duplicate",
                _ => code
            };
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            var filtered = GetFiltered();
            sb.AppendLine("Sprite Packing Analysis [" + filtered.Count + " issues]:");
            foreach (var issue in filtered)
                sb.AppendLine($"[{issue.Severity}] {IssueCodeToLabel(issue.IssueCode)} | {issue.AssetPath} | {issue.Description}");
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Sprite Packing Analysis", Application.dataPath, "sprite_packing_analysis.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("Severity,IssueCode,IssueType,AssetPath,Description");
            foreach (var issue in GetFiltered())
                sb.AppendLine($"{issue.Severity},{USExportUtilities.EscapeCsvField(issue.IssueCode)},{USExportUtilities.EscapeCsvField(IssueCodeToLabel(issue.IssueCode))},{USExportUtilities.EscapeCsvField(issue.AssetPath)},{USExportUtilities.EscapeCsvField(issue.Description)}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private List<UnityScannerIssue> GetFiltered()
        {
            if (!_cacheDirty && _cached != null) return _cached;

            var issues = _category?.LastIssues ?? new List<UnityScannerIssue>();
            var filtered = issues.AsEnumerable();

            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(i => (i.AssetPath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               (i.Description ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            if (_typeFilter > 0 && _typeFilter < TypeCodes.Length)
            {
                var targetCode = TypeCodes[_typeFilter];
                filtered = filtered.Where(i => i.IssueCode == targetCode);
            }

            if (_severityFilter == 1)
                filtered = filtered.Where(i => i.Severity == UnityScannerIssueSeverity.Error);
            else if (_severityFilter == 2)
                filtered = filtered.Where(i => i.Severity >= UnityScannerIssueSeverity.Warning);

            var sorted = filtered.ToList();
            switch (_sortMode)
            {
                case 0:
                    sorted.Sort((a, b) => b.Severity.CompareTo(a.Severity));
                    break;
                case 1:
                    sorted.Sort((a, b) => string.Compare(a.AssetPath, b.AssetPath, StringComparison.Ordinal));
                    break;
                case 2:
                    sorted.Sort((a, b) => string.Compare(a.IssueCode, b.IssueCode, StringComparison.Ordinal));
                    break;
            }

            _cached = sorted;
            _cacheDirty = false;
            return _cached;
        }
    }
}
