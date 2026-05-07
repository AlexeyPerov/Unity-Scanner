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

namespace UnityScanner.Categories.FontTextAnalysis
{
    public class FontTextAnalysisTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "font_text_analysis";
        private const int PageSize = 50;
        private FontTextAnalysisCategory _category;
        public System.Action OnScanRequested;
        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;
        private string _pathFilter = "";
        private bool _warningsOnly;
        private int _sortMode;
        private bool _settingsFoldout;
        private int _expandedRow = -1;
        private List<FontAssetData> _cached;
        private bool _cacheDirty = true;

        public void Bind(FontTextAnalysisCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category?.LastFonts == null) return;
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
                DrawFontRow(filtered[i], i);
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
            var settings = _category.Settings as FontTextAnalysisSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.MaxAtlasSize = EditorGUILayout.IntField("Max Atlas Size", settings.MaxAtlasSize);
            settings.MaxFallbackChainDepth = EditorGUILayout.IntField("Max Fallback Chain Depth", settings.MaxFallbackChainDepth);
            settings.DetectAtlasGrowth = EditorGUILayout.ToggleLeft("Detect Dynamic Atlas Growth", settings.DetectAtlasGrowth);
            settings.DetectOversizedAtlases = EditorGUILayout.ToggleLeft("Detect Oversized Atlases", settings.DetectOversizedAtlases);
            settings.DetectDeepFallbackChains = EditorGUILayout.ToggleLeft("Detect Deep Fallback Chains", settings.DetectDeepFallbackChains);
            settings.DetectDuplicateFallbackChains = EditorGUILayout.ToggleLeft("Detect Duplicate Fallback Chains", settings.DetectDuplicateFallbackChains);
            settings.DetectMissingFontAssignments = EditorGUILayout.ToggleLeft("Detect Missing Font Assignments", settings.DetectMissingFontAssignments);
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
            var sl = _sortMode switch { 0 => "Warnings Desc", 1 => "Path A-Z", 2 => "Atlas Size", _ => "Warnings Desc" };
            if (GUILayout.Button(new GUIContent($"Sort: {sl}", "Sort entries by this criteria"), GUILayout.Width(120))) { _sortMode = _sortMode >= 2 ? 0 : _sortMode + 1; InvalidateCache(); }
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

        private void DrawFontRow(FontAssetData f, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var isExpanded = _expandedRow == index;
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;
            var sevColor = f.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, 1 => Color.cyan, _ => Color.white };
            USGUIUtilities.DrawColoredLabel($"[{f.WarningLevel}]", sevColor, 30);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            USGUIUtilities.DrawAssetButton(f.Path, 300f, 18f);
            GUILayout.Label(f.IsTmpFont ? "TMP" : "Unity", EditorStyles.miniLabel, GUILayout.Width(50));
            GUILayout.Label($"{f.AtlasWidth}x{f.AtlasHeight}", EditorStyles.miniLabel, GUILayout.Width(70));
            GUILayout.Label($"FB:{f.FallbackChainDepth}", EditorStyles.miniLabel, GUILayout.Width(45));
            EditorGUILayout.EndHorizontal();
            if (isExpanded) DrawFontDetail(f);
        }

        private void DrawFontDetail(FontAssetData f)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"Path: {f.Path}", EditorStyles.miniLabel);
            GUILayout.Label($"Type: {(f.IsTmpFont ? "TextMeshPro" : "Unity Font")} | Dynamic: {f.IsDynamic}", EditorStyles.miniLabel);
            if (f.IsTmpFont)
            {
                GUILayout.Label($"Atlas: {f.AtlasWidth}x{f.AtlasHeight} | Glyphs: {f.GlyphCount}", EditorStyles.miniLabel);
                if (f.FallbackChainDepth > 0)
                    GUILayout.Label($"Fallback Chain ({f.FallbackChainDepth}): {string.Join(" -> ", f.FallbackChainNames)}", EditorStyles.miniLabel);
            }
            if (f.CustomWarnings != null)
                foreach (var w in f.CustomWarnings) USGUIUtilities.DrawColoredLabel(w, Color.yellow);
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            var list = GetFiltered();
            sb.AppendLine($"Font Analysis [{list.Count} fonts]:");
            foreach (var f in list)
                sb.AppendLine($"[{f.WarningLevel}] {f.Name} | {(f.IsTmpFont ? "TMP" : "Unity")} {f.AtlasWidth}x{f.AtlasHeight} FB:{f.FallbackChainDepth} | {f.Path}");
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Font Analysis", Application.dataPath, "font_analysis.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("WarningLevel,Name,Path,Type,AtlasWidth,AtlasHeight,Dynamic,FallbackDepth,GlyphCount");
            foreach (var f in GetFiltered())
                sb.AppendLine($"{f.WarningLevel},{USExportUtilities.EscapeCsvField(f.Name)},{USExportUtilities.EscapeCsvField(f.Path)}," +
                    $"{(f.IsTmpFont ? "TMP" : "Unity")},{f.AtlasWidth},{f.AtlasHeight},{f.IsDynamic},{f.FallbackChainDepth},{f.GlyphCount}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private void InvalidateCache() { _cacheDirty = true; }

        private List<FontAssetData> GetFiltered()
        {
            if (!_cacheDirty && _cached != null) return _cached;
            var fonts = _category?.LastFonts;
            if (fonts == null) { _cached = new List<FontAssetData>(); _cacheDirty = false; return _cached; }
            var filtered = fonts.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(f => f.Path.IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_warningsOnly) filtered = filtered.Where(f => f.WarningLevel > 0);
            var sorted = filtered.ToList();
            switch (_sortMode)
            {
                case 0: sorted.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel)); break;
                case 1: sorted.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal)); break;
                case 2: sorted.Sort((a, b) => (b.AtlasWidth * b.AtlasHeight).CompareTo(a.AtlasWidth * a.AtlasHeight)); break;
            }
            _cached = sorted; _cacheDirty = false;
            return _cached;
        }
    }
}
