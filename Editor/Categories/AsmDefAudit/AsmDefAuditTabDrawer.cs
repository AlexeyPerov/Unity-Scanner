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

namespace UnityScanner.Categories.AsmDefAudit
{
    public class AsmDefAuditTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "asmdef_audit";
        private const int PageSize = 50;
        private AsmDefAuditCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;
        private int _subTab;
        private bool _settingsFoldout;
        private bool _cacheDirty = true;
        private int _expandedRow = -1;
        private string _pathFilter = "";
        private int _severityFilter;
        private List<AsmDefData> _cachedResults;
        private List<AsmDefData> _lastSourceResults;

        public void Bind(AsmDefAuditCategory category) { _category = category; }

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
            _subTab = GUILayout.Toolbar(_subTab, new[] { "References", "Platform", "Editor Leakage", "Duplicates" });
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
            var settings = _category.Settings as AsmDefAuditSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.CheckCircularReferences = EditorGUILayout.ToggleLeft("Check Circular References", settings.CheckCircularReferences);
            settings.CheckEditorInRuntime = EditorGUILayout.ToggleLeft("Check Editor in Runtime", settings.CheckEditorInRuntime);
            settings.CheckAutoReferencedOrphan = EditorGUILayout.ToggleLeft("Check Auto-Referenced Orphan", settings.CheckAutoReferencedOrphan);
            settings.CheckPlatformFilterBroad = EditorGUILayout.ToggleLeft("Check Platform Filter Broad", settings.CheckPlatformFilterBroad);
            settings.CheckPlatformFilterContradict = EditorGUILayout.ToggleLeft("Check Platform Filter Contradict", settings.CheckPlatformFilterContradict);
            settings.CheckDuplicateName = EditorGUILayout.ToggleLeft("Check Duplicate Name", settings.CheckDuplicateName);
            settings.CheckVersionDefineInvalid = EditorGUILayout.ToggleLeft("Check Version Define Invalid", settings.CheckVersionDefineInvalid);
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
            if (items.Count == 0) { GUILayout.Label("No assembly definitions found.", EditorStyles.miniLabel); return; }
            USGUIPaginationUtilities.DrawPagesWidget(items.Count, _pagination);
            USGUIUtilities.HorizontalLine();
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < items.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination)) continue;
                switch (_subTab)
                {
                    case 0: DrawReferencesRow(items[i], i); break;
                    case 1: DrawPlatformRow(items[i], i); break;
                    case 2: DrawEditorLeakageRow(items[i], i); break;
                    case 3: DrawDuplicatesRow(items[i], i); break;
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawReferencesRow(AsmDefData d, int idx)
        {
            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            USGUIUtilities.DrawColoredLabel("Refs:" + d.References.Count, Color.white, 55);
            EditorGUILayout.LabelField(d.AssemblyName, EditorStyles.miniLabel, GUILayout.Width(200));
            if (!string.IsNullOrEmpty(d.AssemblyPath))
                USGUIUtilities.DrawAssetButton(d.AssemblyPath, 250f, 18f);
            var orphan = !d.AutoReferenced ? " [NO-AUTO]" : "";
            GUILayout.Label(orphan, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("AutoReferenced: " + d.AutoReferenced + " | AnyPlatform: " + d.AnyPlatform, EditorStyles.miniLabel);
                foreach (var r in d.References.Take(20))
                    GUILayout.Label("  -> " + r, EditorStyles.miniLabel);
                if (d.References.Count > 20)
                    GUILayout.Label("  ... and " + (d.References.Count - 20) + " more", EditorStyles.miniLabel);
                USGUIUtilities.DrawCustomWarnings(d);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawPlatformRow(AsmDefData d, int idx)
        {
            var hasInclude = d.IncludePlatforms.Count > 0;
            var hasExclude = d.ExcludePlatforms.Count > 0;
            var isBroad = !hasInclude && !hasExclude && d.AnyPlatform;
            var isContradict = hasInclude && hasExclude;

            if (!isBroad && !isContradict && hasInclude) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (isContradict)
                USGUIUtilities.DrawColoredLabel("CONTRADICT", Color.red, 80);
            else if (isBroad)
                USGUIUtilities.DrawColoredLabel("BROAD", Color.cyan, 55);
            EditorGUILayout.LabelField(d.AssemblyName, EditorStyles.miniLabel, GUILayout.Width(200));
            if (!string.IsNullOrEmpty(d.AssemblyPath))
                USGUIUtilities.DrawAssetButton(d.AssemblyPath, 250f, 18f);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEditorLeakageRow(AsmDefData d, int idx)
        {
            if (d.IsEditorOnly) return;
            var editorRefs = d.References.Where(r => r.Contains("UnityEditor") || r.Contains(".Editor")).ToList();
            if (editorRefs.Count == 0) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            USGUIUtilities.DrawColoredLabel("ED-LEAK", Color.yellow, 60);
            EditorGUILayout.LabelField(d.AssemblyName, EditorStyles.miniLabel, GUILayout.Width(200));
            if (!string.IsNullOrEmpty(d.AssemblyPath))
                USGUIUtilities.DrawAssetButton(d.AssemblyPath, 250f, 18f);
            GUILayout.Label(string.Join(", ", editorRefs.Take(3)), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDuplicatesRow(AsmDefData d, int idx)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(d.AssemblyName, EditorStyles.miniLabel, GUILayout.Width(200));
            if (!string.IsNullOrEmpty(d.AssemblyPath))
                USGUIUtilities.DrawAssetButton(d.AssemblyPath, 250f, 18f);
            GUILayout.Label("Include: " + (d.IncludePlatforms.Count > 0 ? string.Join(",", d.IncludePlatforms) : "all") +
                " | Exclude: " + (d.ExcludePlatforms.Count > 0 ? string.Join(",", d.ExcludePlatforms) : "none"), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Assembly Definition Audit:");
            foreach (var d in GetFilteredResults())
            {
                sb.AppendLine(d.AssemblyName + " | " + d.AssemblyPath + " | Refs:" + d.References.Count +
                    " AutoRef:" + d.AutoReferenced + " Editor:" + d.IsEditorOnly);
                foreach (var r in d.References)
                    sb.AppendLine("  -> " + r);
            }
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export AsmDef Audit", Application.dataPath, "asmdef_audit.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("AssemblyName,Path,ReferenceCount,AutoReferenced,IsEditorOnly,IncludePlatforms,ExcludePlatforms");
            foreach (var d in GetFilteredResults())
                sb.AppendLine(USExportUtilities.EscapeCsvField(d.AssemblyName) + "," +
                    USExportUtilities.EscapeCsvField(d.AssemblyPath) + "," +
                    d.References.Count + "," + d.AutoReferenced + "," + d.IsEditorOnly + "," +
                    USExportUtilities.EscapeCsvField(string.Join(";", d.IncludePlatforms)) + "," +
                    USExportUtilities.EscapeCsvField(string.Join(";", d.ExcludePlatforms)));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private List<AsmDefData> GetFilteredResults()
        {
            EnsureCache();
            return _cachedResults;
        }

        private void EnsureCache()
        {
            var src = _category?.LastResults;
            if (!_cacheDirty && _cachedResults != null && ReferenceEquals(src, _lastSourceResults)) return;
            _lastSourceResults = src;
            var raw = src ?? new List<AsmDefData>();
            var f = raw.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                f = f.Where(x => (x.AssemblyPath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 (x.AssemblyName ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_severityFilter == 1)
                f = f.Where(x => x.WarningLevel >= 3);
            else if (_severityFilter == 2)
                f = f.Where(x => x.WarningLevel >= 2);
            _cachedResults = f.ToList();
            _cacheDirty = false;
        }
    }
}
