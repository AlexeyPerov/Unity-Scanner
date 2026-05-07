using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Export;
using UnityScanner.Core.Results;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.ProjectHealth
{
    public class ProjectHealthTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "project_health";
        private const int PageSize = 50;

        private ProjectHealthCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;
        private string _pathFilter = "";
        private int _severityFilter;
        private int _typeFilter;
        private int _sortMode;
        private int _expandedRow = -1;
        private List<ProjectHealthEntry> _cached;
        private bool _cacheDirty = true;

        public void Bind(ProjectHealthCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category?.LastResults == null || _category.LastResults.Count == 0)
            {
                if (result == null)
                    GUILayout.Label("No results yet.", EditorStyles.miniLabel);
                else
                    GUILayout.Label("No issues.", EditorStyles.miniLabel);
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
                DrawRow(filtered[i], i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        public void DrawTopBar(UnityScannerResult result)
        {
            
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Path:", GUILayout.Width(40));
            var newPath = GUILayout.TextField(_pathFilter, GUILayout.Width(200));
            if (newPath != _pathFilter) { _pathFilter = newPath; _cacheDirty = true; }

            GUILayout.Label("Type:", GUILayout.Width(35));
            var prevType = _typeFilter;
            _typeFilter = EditorGUILayout.Popup(_typeFilter, new[] { "All", "Empty Folder", "Meta Only", "Orphaned Meta", "Broken Asset", "Empty Scene", "Deep Nesting", "Large Folder" }, GUILayout.Width(130));
            if (_typeFilter != prevType) _cacheDirty = true;

            var sevLabel = _severityFilter switch { 1 => "Errors", 2 => "Errors+Warn", _ => "All" };
            if (GUILayout.Button(new GUIContent("Severity: " + sevLabel, "Filter by severity"), GUILayout.Width(200)))
            { _severityFilter = _severityFilter >= 2 ? 0 : _severityFilter + 1; _cacheDirty = true; }

            var sortLabel = _sortMode switch { 0 => "Sev Desc", 1 => "Path A-Z", 2 => "Type", _ => "Severity Desc" };
            if (GUILayout.Button(new GUIContent("Severity: " + sortLabel, "Sort entries"), GUILayout.Width(200)))
            { _sortMode = _sortMode >= 2 ? 0 : _sortMode + 1; _cacheDirty = true; }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawExportButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Copy to Clipboard", "Copy filtered data to clipboard"), GUILayout.Width(140))) ExportToClipboard();
            if (GUILayout.Button(new GUIContent("Export CSV...", "Export filtered data to CSV"), GUILayout.Width(100))) ExportToCsv();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRow(ProjectHealthEntry entry, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var isExpanded = _expandedRow == index;
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;

            var sevColor = entry.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, _ => Color.cyan };
            USGUIUtilities.DrawColoredLabel($"[{entry.WarningLevel}]", sevColor, 30);

            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));

            var typeLabel = entry.IssueType switch
            {
                ProjectHealthIssueType.EmptyFolder => "Empty Folder",
                ProjectHealthIssueType.MetaOnlyFolder => "Meta Only",
                ProjectHealthIssueType.OrphanedMeta => "Orphaned Meta",
                ProjectHealthIssueType.BrokenAsset => "Broken Asset",
                ProjectHealthIssueType.EmptyScene => "Empty Scene",
                ProjectHealthIssueType.DeepNesting => "Deep Nesting",
                ProjectHealthIssueType.LargeFolder => "Large Folder",
                _ => "Unknown"
            };
            GUILayout.Label(typeLabel, EditorStyles.miniLabel, GUILayout.Width(100));

            USGUIUtilities.DrawAssetButton(entry.Path, 200f, 18f);

            EditorGUILayout.EndHorizontal();

            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Path: " + entry.Path, EditorStyles.miniLabel);
                GUILayout.Label("Issue Type: " + entry.IssueType, EditorStyles.miniLabel);
                if (entry.FileSizeBytes > 0)
                    GUILayout.Label("Size: " + USExportUtilities.GetReadableSize(entry.FileSizeBytes), EditorStyles.miniLabel);
                GUILayout.Label("Detail: " + entry.Detail, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Project Health [" + GetFiltered().Count + " entries]:");
            foreach (var e in GetFiltered())
                sb.AppendLine($"[{e.WarningLevel}] {e.IssueType} | {e.Path} | {e.Detail}");
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Project Health", Application.dataPath, "project_health.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("WarningLevel,Path,IssueType,Detail,FileSize");
            foreach (var e in GetFiltered())
                sb.AppendLine($"{e.WarningLevel},{USExportUtilities.EscapeCsvField(e.Path)},{e.IssueType},{USExportUtilities.EscapeCsvField(e.Detail)},{e.FileSizeBytes}");
            System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private List<ProjectHealthEntry> GetFiltered()
        {
            if (!_cacheDirty && _cached != null) return _cached;

            var entries = _category?.LastResults ?? new List<ProjectHealthEntry>();
            var filtered = entries.AsEnumerable();

            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(e => e.Path.IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            if (_typeFilter > 0)
            {
                var targetType = (ProjectHealthIssueType)(_typeFilter - 1);
                filtered = filtered.Where(e => e.IssueType == targetType);
            }

            if (_severityFilter == 1)
                filtered = filtered.Where(e => e.WarningLevel >= 3);
            else if (_severityFilter == 2)
                filtered = filtered.Where(e => e.WarningLevel >= 2);

            var sorted = filtered.ToList();
            switch (_sortMode)
            {
                case 0: sorted.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel)); break;
                case 1: sorted.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal)); break;
                case 2: sorted.Sort((a, b) => a.IssueType.CompareTo(b.IssueType)); break;
            }

            _cached = sorted;
            _cacheDirty = false;
            return _cached;
        }
    }
}