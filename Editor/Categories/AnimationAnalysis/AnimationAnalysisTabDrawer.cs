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

namespace UnityScanner.Categories.AnimationAnalysis
{
    public class AnimationAnalysisTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "animation_analysis";
        private const int PageSize = 50;
        private AnimationAnalysisCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _animPagination = new() { PageToShow = 0, PageSize = PageSize };
        private readonly USPaginationSettings _clipPagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _animScroll;
        private Vector2 _clipScroll;
        private string _pathFilter = "";
        private bool _warningsOnly;
        private int _sortMode;
        private bool _settingsFoldout;
        private int _expandedAnim = -1;
        private int _expandedClip = -1;
        private List<AnimatorData> _cachedAnimators;
        private List<AnimationClipData> _cachedClips;
        private bool _cacheDirty = true;
        private int _subTab;
        private List<AnimatorData> _lastSourceAnimators;
        private List<AnimationClipData> _lastSourceClips;

        public void Bind(AnimationAnalysisCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category == null) return;
            if (_category.LastAnimators == null && _category.LastClips == null) return;
            DrawFilters();
            USGUIUtilities.HorizontalLine();
            DrawExportButtons();
            USGUIUtilities.HorizontalLine();

            var prevSubTab = _subTab;
            _subTab = GUILayout.Toolbar(_subTab, new[] { "Animators", "Clips" });
            if (_subTab != prevSubTab) { _expandedAnim = -1; _expandedClip = -1; }

            if (_subTab == 0) DrawAnimatorsList();
            else DrawClipsList();
        }

        public void DrawTopBar(UnityScannerResult result)
        {
            if (_category == null) return;
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Analysis Settings");
            if (!_settingsFoldout) return;
            EditorGUI.indentLevel++;
            var settings = _category.Settings as AnimationAnalysisSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.StateMachineComplexityThreshold = EditorGUILayout.IntField("State Complexity Threshold", settings.StateMachineComplexityThreshold);
            settings.AnyStateTransitionThreshold = EditorGUILayout.IntField("AnyState Transition Threshold", settings.AnyStateTransitionThreshold);
            settings.CurveKeyframeDensityThreshold = EditorGUILayout.IntField("Keyframe Density Threshold", settings.CurveKeyframeDensityThreshold);
            settings.CurveCountThreshold = EditorGUILayout.IntField("Curve Count Threshold", settings.CurveCountThreshold);
            settings.MaxTransitionCount = EditorGUILayout.IntField("Max Transition Count", settings.MaxTransitionCount);
            GUILayout.Space(4);
            settings.DetectMissingReferences = EditorGUILayout.ToggleLeft("Detect Missing References", settings.DetectMissingReferences);
            settings.DetectUnreachableStates = EditorGUILayout.ToggleLeft("Detect Unreachable States", settings.DetectUnreachableStates);
            settings.DetectComplexityOverThreshold = EditorGUILayout.ToggleLeft("Detect Complexity Over Threshold", settings.DetectComplexityOverThreshold);
            settings.DetectAnyStateOveruse = EditorGUILayout.ToggleLeft("Detect AnyState Overuse", settings.DetectAnyStateOveruse);
            settings.DetectDuplicateClips = EditorGUILayout.ToggleLeft("Detect Duplicate Clips", settings.DetectDuplicateClips);
            settings.DetectExpensiveCurves = EditorGUILayout.ToggleLeft("Detect Expensive Curves", settings.DetectExpensiveCurves);
            settings.DetectParameterMismatches = EditorGUILayout.ToggleLeft("Detect Parameter Mismatches", settings.DetectParameterMismatches);
            settings.PathFilter = EditorGUILayout.TextField("Path Filter", settings.PathFilter);
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
            if (GUILayout.Button(new GUIContent("Sort: " + sl, "Sort entries by this criteria"), GUILayout.Width(120))) { _sortMode = _sortMode >= 2 ? 0 : _sortMode + 1; InvalidateCache(); }
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

        private void DrawAnimatorsList()
        {
            var filtered = GetFilteredAnimators();
            USGUIPaginationUtilities.DrawPagesWidget(filtered.Count, _animPagination);
            USGUIUtilities.HorizontalLine();
            _animScroll = EditorGUILayout.BeginScrollView(_animScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < filtered.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _animPagination)) continue;
                DrawAnimatorRow(filtered[i], i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawClipsList()
        {
            var filtered = GetFilteredClips();
            USGUIPaginationUtilities.DrawPagesWidget(filtered.Count, _clipPagination);
            USGUIUtilities.HorizontalLine();
            _clipScroll = EditorGUILayout.BeginScrollView(_clipScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < filtered.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _clipPagination)) continue;
                DrawClipRow(filtered[i], i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawAnimatorRow(AnimatorData a, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var isExpanded = _expandedAnim == index;
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedAnim = isExpanded ? -1 : index;
            var sevColor = a.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, 1 => Color.cyan, _ => Color.white };
            USGUIUtilities.DrawColoredLabel("[" + a.WarningLevel + "]", sevColor, 30);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            USGUIUtilities.DrawAssetButton(a.Path, 280f, 18f);
            GUILayout.Label("States:" + a.StateCount, EditorStyles.miniLabel, GUILayout.Width(70));
            GUILayout.Label("Trans:" + a.TransitionCount, EditorStyles.miniLabel, GUILayout.Width(70));
            GUILayout.Label("Layers:" + a.LayerCount, EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            if (isExpanded) DrawAnimatorDetail(a);
        }

        private void DrawAnimatorDetail(AnimatorData a)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Path: " + a.Path, EditorStyles.miniLabel);
            GUILayout.Label("States: " + a.StateCount + " | Transitions: " + a.TransitionCount + " | AnyState: " + a.AnyStateTransitionCount + " | Layers: " + a.LayerCount, EditorStyles.miniLabel);
            if (a.MissingReferences.Count > 0)
            {
                var prev = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label("Missing References (" + a.MissingReferences.Count + "):", EditorStyles.miniLabel);
                GUI.color = prev;
                EditorGUI.indentLevel++;
                foreach (var mr in a.MissingReferences.Take(10))
                    GUILayout.Label(mr, EditorStyles.miniLabel);
                if (a.MissingReferences.Count > 10)
                    GUILayout.Label("... and " + (a.MissingReferences.Count - 10) + " more", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            if (a.UnreachableStates.Count > 0)
            {
                var prev = GUI.color;
                GUI.color = Color.yellow;
                GUILayout.Label("Unreachable States (" + a.UnreachableStates.Count + "):", EditorStyles.miniLabel);
                GUI.color = prev;
                EditorGUI.indentLevel++;
                foreach (var us in a.UnreachableStates.Take(10))
                    GUILayout.Label(us, EditorStyles.miniLabel);
                if (a.UnreachableStates.Count > 10)
                    GUILayout.Label("... and " + (a.UnreachableStates.Count - 10) + " more", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            if (a.ParameterMismatches.Count > 0)
            {
                var prev = GUI.color;
                GUI.color = Color.cyan;
                GUILayout.Label("Parameter Mismatches (" + a.ParameterMismatches.Count + "):", EditorStyles.miniLabel);
                GUI.color = prev;
                EditorGUI.indentLevel++;
                foreach (var pm in a.ParameterMismatches.Take(10))
                    GUILayout.Label(pm, EditorStyles.miniLabel);
                if (a.ParameterMismatches.Count > 10)
                    GUILayout.Label("... and " + (a.ParameterMismatches.Count - 10) + " more", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void DrawClipRow(AnimationClipData c, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var isExpanded = _expandedClip == index;
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedClip = isExpanded ? -1 : index;
            var sevColor = c.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, 1 => Color.cyan, _ => Color.white };
            USGUIUtilities.DrawColoredLabel("[" + c.WarningLevel + "]", sevColor, 30);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            USGUIUtilities.DrawAssetButton(c.Path, 280f, 18f);
            GUILayout.Label(USExportUtilities.GetReadableSize(c.FileSizeBytes), EditorStyles.miniLabel, GUILayout.Width(70));
            GUILayout.Label(c.Duration.ToString("F1") + "s", EditorStyles.miniLabel, GUILayout.Width(50));
            GUILayout.Label("Curves:" + c.CurveCount, EditorStyles.miniLabel, GUILayout.Width(70));
            if (c.IsDuplicate) { var prev = GUI.color; GUI.color = Color.cyan; GUILayout.Label("DUP", EditorStyles.miniLabel, GUILayout.Width(30)); GUI.color = prev; }
            EditorGUILayout.EndHorizontal();
            if (isExpanded) DrawClipDetail(c);
        }

        private void DrawClipDetail(AnimationClipData c)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Path: " + c.Path, EditorStyles.miniLabel);
            GUILayout.Label("Size: " + USExportUtilities.GetReadableSize(c.FileSizeBytes) + " | Duration: " + c.Duration.ToString("F1") + "s | Curves: " + c.CurveCount + " | Keyframes: " + c.TotalKeyframes + " | Density: " + c.KeyframeDensity + " kf/s", EditorStyles.miniLabel);
            if (c.IsDuplicate && c.DuplicatePaths.Count > 0)
            {
                GUILayout.Label("Duplicate of (" + c.DuplicatePaths.Count + "):", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                foreach (var dp in c.DuplicatePaths.Take(5))
                    GUILayout.Label(dp, EditorStyles.miniLabel);
                if (c.DuplicatePaths.Count > 5)
                    GUILayout.Label("... and " + (c.DuplicatePaths.Count - 5) + " more", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            var animators = GetFilteredAnimators();
            var clips = GetFilteredClips();
            sb.AppendLine("Animation Analysis [" + animators.Count + " animators, " + clips.Count + " clips]:");
            sb.AppendLine("--- Animators ---");
            foreach (var a in animators)
                sb.AppendLine("[" + a.WarningLevel + "] " + a.Name + " | States:" + a.StateCount + " Trans:" + a.TransitionCount + " | " + a.Path);
            sb.AppendLine("--- Clips ---");
            foreach (var c in clips)
                sb.AppendLine("[" + c.WarningLevel + "] " + c.Name + " | " + USExportUtilities.GetReadableSize(c.FileSizeBytes) + " " + c.Duration.ToString("F1") + "s Curves:" + c.CurveCount + " | " + c.Path);
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Animation Analysis", Application.dataPath, "animation_analysis.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("Type,WarningLevel,Name,Path,SizeBytes,States,Transitions,AnyStateTrans,Layers,Curves,Keyframes,Duration,Density,Duplicate");
            foreach (var a in GetFilteredAnimators())
                sb.AppendLine("Animator," + a.WarningLevel + "," + USExportUtilities.EscapeCsvField(a.Name) + "," + USExportUtilities.EscapeCsvField(a.Path) + ",0," + a.StateCount + "," + a.TransitionCount + "," + a.AnyStateTransitionCount + "," + a.LayerCount + ",0,0,0,0,False");
            foreach (var c in GetFilteredClips())
                sb.AppendLine("Clip," + c.WarningLevel + "," + USExportUtilities.EscapeCsvField(c.Name) + "," + USExportUtilities.EscapeCsvField(c.Path) + "," + c.FileSizeBytes + ",0,0,0,0," + c.CurveCount + "," + c.TotalKeyframes + "," + c.Duration.ToString("F1") + "," + c.KeyframeDensity + "," + c.IsDuplicate);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private void InvalidateCache() { _cacheDirty = true; }

        private List<AnimatorData> GetFilteredAnimators()
        {
            EnsureCache();
            return _cachedAnimators;
        }

        private List<AnimationClipData> GetFilteredClips()
        {
            EnsureCache();
            return _cachedClips;
        }

        private void EnsureCache()
        {
            var srcAnim = _category?.LastAnimators;
            var srcClips = _category?.LastClips;
            if (!_cacheDirty && _cachedAnimators != null && _cachedClips != null
                && ReferenceEquals(srcAnim, _lastSourceAnimators)
                && ReferenceEquals(srcClips, _lastSourceClips))
                return;
            _lastSourceAnimators = srcAnim;
            _lastSourceClips = srcClips;
            _cachedAnimators = FilterAnimators(srcAnim);
            _cachedClips = FilterClips(srcClips);
            _cacheDirty = false;
        }

        private List<AnimatorData> FilterAnimators(List<AnimatorData> items)
        {
            if (items == null) return new List<AnimatorData>();
            var filtered = items.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(x => x.Path.IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_warningsOnly) filtered = filtered.Where(x => x.WarningLevel > 0);
            var sorted = filtered.ToList();
            switch (_sortMode)
            {
                case 0: sorted.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel)); break;
                case 1: sorted.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal)); break;
                case 2: sorted.Sort((a, b) => b.StateCount.CompareTo(a.StateCount)); break;
            }
            return sorted;
        }

        private List<AnimationClipData> FilterClips(List<AnimationClipData> items)
        {
            if (items == null) return new List<AnimationClipData>();
            var filtered = items.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(x => x.Path.IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_warningsOnly) filtered = filtered.Where(x => x.WarningLevel > 0);
            var sorted = filtered.ToList();
            switch (_sortMode)
            {
                case 0: sorted.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel)); break;
                case 1: sorted.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal)); break;
                case 2: sorted.Sort((a, b) => b.FileSizeBytes.CompareTo(a.FileSizeBytes)); break;
            }
            return sorted;
        }
    }
}
