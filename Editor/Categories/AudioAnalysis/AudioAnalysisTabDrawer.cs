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

namespace UnityScanner.Categories.AudioAnalysis
{
    public class AudioAnalysisTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "audio_analysis";
        private const int PageSize = 50;
        private AudioAnalysisCategory _category;
        public System.Action OnScanRequested;
        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;
        private string _pathFilter = "";
        private bool _warningsOnly;
        private int _sortMode;
        private bool _settingsFoldout;
        private int _expandedRow = -1;
        private List<AudioClipData> _cached;
        private bool _cacheDirty = true;

        public void Bind(AudioAnalysisCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category?.LastClips == null) return;
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
                DrawClipRow(filtered[i], i);
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
            var settings = _category.Settings as AudioAnalysisSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.MaxClipSizeMB = EditorGUILayout.IntField("Max Clip Size (MB)", settings.MaxClipSizeMB);
            settings.MaxStartupAudioTotalMB = EditorGUILayout.IntField("Max Startup Audio Total (MB)", settings.MaxStartupAudioTotalMB);
            settings.DetectImportMismatches = EditorGUILayout.ToggleLeft("Detect Import Mismatches", settings.DetectImportMismatches);
            settings.DetectStartupOversized = EditorGUILayout.ToggleLeft("Detect Startup Oversized", settings.DetectStartupOversized);
            settings.DetectDuplicates = EditorGUILayout.ToggleLeft("Detect Duplicates", settings.DetectDuplicates);
            settings.DetectMissingMixerGroups = EditorGUILayout.ToggleLeft("Detect Missing Mixer Groups", settings.DetectMissingMixerGroups);
            settings.DetectChannelSampleRateIssues = EditorGUILayout.ToggleLeft("Detect Channel/Sample Rate Issues", settings.DetectChannelSampleRateIssues);
            if (EditorGUI.EndChangeCheck()) InvalidateCache();
            EditorGUI.indentLevel--;
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Path:", GUILayout.Width(40));
            var newPath = GUILayout.TextField(_pathFilter, GUILayout.Width(250));
            if (newPath != _pathFilter) { _pathFilter = newPath; InvalidateCache(); }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _warningsOnly = EditorGUILayout.ToggleLeft("Warnings Only", _warningsOnly, GUILayout.Width(140));
            if (EditorGUI.EndChangeCheck()) InvalidateCache();
            var sl = _sortMode switch { 0 => "Warnings Desc", 1 => "Path A-Z", 2 => "Size Desc", _ => "Warnings Desc" };
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

        private void DrawClipRow(AudioClipData c, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var isExpanded = _expandedRow == index;
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;
            var sevColor = c.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, 1 => Color.cyan, _ => Color.white };
            USGUIUtilities.DrawColoredLabel($"[{c.WarningLevel}]", sevColor, 30);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            USGUIUtilities.DrawAssetButton(c.Path, 300f, 18f);
            GUILayout.Label(USExportUtilities.GetReadableSize(c.FileSizeBytes), EditorStyles.miniLabel, GUILayout.Width(70));
            GUILayout.Label($"{c.Duration:F1}s", EditorStyles.miniLabel, GUILayout.Width(50));
            GUILayout.Label(c.LoadType ?? "?", EditorStyles.miniLabel, GUILayout.Width(110));
            if (c.IsDuplicate) { var prev = GUI.color; GUI.color = Color.cyan; GUILayout.Label("DUP", EditorStyles.miniLabel, GUILayout.Width(30)); GUI.color = prev; }
            EditorGUILayout.EndHorizontal();
            if (isExpanded) DrawClipDetail(c);
        }

        private void DrawClipDetail(AudioClipData c)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"Path: {c.Path}", EditorStyles.miniLabel);
            GUILayout.Label($"Size: {USExportUtilities.GetReadableSize(c.FileSizeBytes)} | Duration: {c.Duration:F1}s | Channels: {c.Channels}", EditorStyles.miniLabel);
            GUILayout.Label($"Load Type: {c.LoadType} | Format: {c.CompressionFormat}", EditorStyles.miniLabel);
            if (c.IsDuplicate && c.DuplicatePaths.Count > 0)
            {
                GUILayout.Label($"Duplicate of ({c.DuplicatePaths.Count}):", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                foreach (var dp in c.DuplicatePaths.Take(5))
                    GUILayout.Label(dp, EditorStyles.miniLabel);
                if (c.DuplicatePaths.Count > 5) GUILayout.Label($"... and {c.DuplicatePaths.Count - 5} more", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            USGUIUtilities.DrawCustomWarnings(c);
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            var list = GetFiltered();
            sb.AppendLine($"Audio Analysis [{list.Count} clips]:");
            foreach (var c in list)
                sb.AppendLine($"[{c.WarningLevel}] {c.Name} | {USExportUtilities.GetReadableSize(c.FileSizeBytes)} {c.Duration:F1}s {c.LoadType} | {c.Path}");
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Audio Analysis", Application.dataPath, "audio_analysis.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("WarningLevel,Name,Path,SizeBytes,DurationS,Channels,LoadType,Compression,Duplicate");
            foreach (var c in GetFiltered())
                sb.AppendLine($"{c.WarningLevel},{USExportUtilities.EscapeCsvField(c.Name)},{USExportUtilities.EscapeCsvField(c.Path)}," +
                    $"{c.FileSizeBytes},{c.Duration:F1},{c.Channels},{c.LoadType},{c.CompressionFormat},{c.IsDuplicate}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private void InvalidateCache() { _cacheDirty = true; }

        private List<AudioClipData> GetFiltered()
        {
            if (!_cacheDirty && _cached != null) return _cached;
            var clips = _category?.LastClips;
            if (clips == null) { _cached = new List<AudioClipData>(); _cacheDirty = false; return _cached; }
            var filtered = clips.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(c => c.Path.IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_warningsOnly) filtered = filtered.Where(c => c.WarningLevel > 0);
            var sorted = filtered.ToList();
            switch (_sortMode)
            {
                case 0: sorted.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel)); break;
                case 1: sorted.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal)); break;
                case 2: sorted.Sort((a, b) => b.FileSizeBytes.CompareTo(a.FileSizeBytes)); break;
            }
            _cached = sorted; _cacheDirty = false;
            return _cached;
        }
    }
}
