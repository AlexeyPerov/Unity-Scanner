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

namespace UnityScanner.Categories.RegressionTrend
{
    public class RegressionTrendTabDrawer : IUnityScannerTabDrawer
    {
        private const string BaselinePathPref = "US_TrendsBaselinePath";

        public string CategoryId => "regression_trend";
        public System.Action OnScanRequested;
        public Func<bool> HasResultsToCompare;
        public Func<string> GetPlatformProfile;

        private RegressionTrendCategory _category;
        private Vector2 _scroll;
        private string _baselinePath;
        private bool _settingsFoldout;

        public void Bind(RegressionTrendCategory category)
        {
            _category = category;
            _baselinePath = EditorPrefs.GetString(BaselinePathPref, "Library/UnityScanner/baseline.json");
            var settings = _category?.Settings as RegressionTrendSettings;
            if (settings != null && !string.IsNullOrEmpty(settings.BaselinePath))
                _baselinePath = settings.BaselinePath;
        }

        private void SetBaselinePath(string path)
        {
            _baselinePath = path;
            EditorPrefs.SetString(BaselinePathPref, path);
            var settings = _category?.Settings as RegressionTrendSettings;
            if (settings != null)
                settings.BaselinePath = path;
        }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Baseline:", GUILayout.Width(60));
            EditorGUI.BeginChangeCheck();
            _baselinePath = GUILayout.TextField(_baselinePath, GUILayout.Width(300));
            if (EditorGUI.EndChangeCheck())
                SetBaselinePath(_baselinePath);
            if (GUILayout.Button(new GUIContent("Browse...", "Browse for a baseline JSON file"), GUILayout.Width(70)))
            {
                var path = EditorUtility.OpenFilePanel("Select Baseline", "Library/UnityScanner", "json");
                if (!string.IsNullOrEmpty(path))
                    SetBaselinePath(path);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(5);
            var hasResults = HasResultsToCompare?.Invoke() ?? false;
            EditorGUI.BeginDisabledGroup(!hasResults);
            var btnLabel = hasResults ? "Run Analysis" : "Run Analysis (scan other categories first)";
            if (GUILayout.Button(new GUIContent(btnLabel, hasResults
                    ? "Compare current scan results against the loaded baseline"
                    : "Run at least one other category scan before analyzing trends"),
                    GUILayout.Width(350), GUILayout.Height(24)))
            {
                OnScanRequested?.Invoke();
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category == null) return;

            if (_category.LastBaseline != null)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label($"Baseline: {_category.LastBaseline.Timestamp}  |  Unity: {_category.LastBaseline.UnityVersion}  |  Profile: {_category.LastBaseline.PlatformProfile}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                var canSave = _category.LastResults != null && _category.LastResults.Count > 0;
                EditorGUI.BeginDisabledGroup(!canSave);
                if (GUILayout.Button(new GUIContent("Save Snapshot...",
                    canSave ? "Save the current results as a baseline snapshot" : "No results to save — run an analysis first"),
                    GUILayout.Width(120)))
                    SaveSnapshotDialog();
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }

            USGUIUtilities.HorizontalLine();
            DrawExportButtons();
            USGUIUtilities.HorizontalLine();

            var comparisons = _category.LastComparisons;
            if (comparisons == null || comparisons.Count == 0)
            {
                EditorGUILayout.HelpBox("No comparison data yet. Run an analysis to compare against the baseline.", MessageType.Info);
                return;
            }

            DrawComparisonTable(comparisons);
            USGUIUtilities.HorizontalLine();
            DrawCategoryBreakdown(comparisons);
        }

        public void DrawTopBar(UnityScannerResult result)
        {
            if (_category == null) return;
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Analysis Settings");
            if (!_settingsFoldout) return;
            EditorGUI.indentLevel++;
            var settings = _category.Settings as RegressionTrendSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);

            settings.RegressionWarningThreshold = EditorGUILayout.IntField(new GUIContent("Warn Threshold", "Minimum issue-count increase to flag as a warning regression"), settings.RegressionWarningThreshold);
            settings.RegressionErrorThreshold = EditorGUILayout.IntField(new GUIContent("Error Threshold", "Minimum issue-count increase to flag as an error regression"), settings.RegressionErrorThreshold);

            EditorGUI.indentLevel--;
        }

        private void DrawExportButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Copy to Clipboard", "Copy filtered data to clipboard"), GUILayout.Width(140))) ExportToClipboard();
            if (GUILayout.Button(new GUIContent("Export CSV...", "Export filtered data to a CSV file"), GUILayout.Width(100))) ExportToCsv();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawComparisonTable(List<CategoryComparison> comparisons)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Category", EditorStyles.boldLabel, GUILayout.Width(160));
            GUILayout.Label("Errors", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label("Warnings", EditorStyles.boldLabel, GUILayout.Width(100));
            GUILayout.Label("Total Delta", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("Status", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            foreach (var comp in comparisons)
            {
                EditorGUILayout.BeginHorizontal();

                GUILayout.Label(comp.DisplayName, GUILayout.Width(160));

                var errColor = comp.ErrorDelta > 0 ? Color.red : comp.ErrorDelta < 0 ? Color.green : Color.white;
                USGUIUtilities.DrawColoredLabel(
                    $"{comp.BaselineErrors} → {comp.CurrentErrors}",
                    errColor, 100);

                var warnColor = comp.WarningDelta > 0 ? Color.yellow : comp.WarningDelta < 0 ? Color.green : Color.white;
                USGUIUtilities.DrawColoredLabel(
                    $"{comp.BaselineWarnings} → {comp.CurrentWarnings}",
                    warnColor, 100);

                var totalColor = comp.TotalDelta > 0 ? Color.red : comp.TotalDelta < 0 ? Color.green : Color.white;
                USGUIUtilities.DrawColoredLabel(
                    comp.TotalDelta.ToString("+#;-#;0"),
                    totalColor, 80);

                if (comp.HasRegression)
                    USGUIUtilities.DrawColoredLabel("Regressed", Color.red, 80);
                else if (comp.HasImprovement)
                    USGUIUtilities.DrawColoredLabel("Improved", Color.green, 80);
                else
                    GUILayout.Label("Stable", GUILayout.Width(80));

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCategoryBreakdown(List<CategoryComparison> comparisons)
        {
            var regressed = comparisons.Where(c => c.HasRegression).ToList();
            var improved = comparisons.Where(c => c.HasImprovement).ToList();

            if (regressed.Count > 0)
            {
                var prev = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label($"Top Regressions ({regressed.Count}):", EditorStyles.boldLabel);
                GUI.color = prev;
                EditorGUI.indentLevel++;
                foreach (var c in regressed.OrderBy(c => c.ErrorDelta).ThenBy(c => c.WarningDelta))
                {
                    var parts = new List<string>();
                    if (c.ErrorDelta != 0) parts.Add($"errors {c.ErrorDelta:+#;-#}");
                    if (c.WarningDelta != 0) parts.Add($"warnings {c.WarningDelta:+#;-#}");
                    GUILayout.Label($"{c.DisplayName}: {string.Join(", ", parts)}", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }

            if (improved.Count > 0)
            {
                GUILayout.Space(4);
                var prev = GUI.color;
                GUI.color = Color.green;
                GUILayout.Label($"Improved ({improved.Count}):", EditorStyles.boldLabel);
                GUI.color = prev;
                EditorGUI.indentLevel++;
                foreach (var c in improved)
                {
                    var parts = new List<string>();
                    if (c.ErrorDelta != 0) parts.Add($"errors {c.ErrorDelta:+#;-#}");
                    if (c.WarningDelta != 0) parts.Add($"warnings {c.WarningDelta:+#;-#}");
                    GUILayout.Label($"{c.DisplayName}: {string.Join(", ", parts)}", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }
        }

        private void SaveSnapshotDialog()
        {
            if (_category?.LastResults == null || _category.LastResults.Count == 0)
            {
                EditorUtility.DisplayDialog("Cannot Save",
                    "No scan results available to save. Run an analysis first.", "OK");
                return;
            }

            var path = EditorUtility.SaveFilePanel("Save Baseline Snapshot", "Library/UnityScanner", "baseline", "json");
            if (string.IsNullOrEmpty(path)) return;

            var profile = !string.IsNullOrEmpty(_category.LastPlatformProfile)
                ? _category.LastPlatformProfile
                : GetPlatformProfile?.Invoke() ?? "";

            _category?.SaveBaseline(_category.LastResults, path, profile);
            Debug.Log($"[US] Baseline snapshot saved to {path}");
        }

        private void ExportToClipboard()
        {
            var comparisons = _category?.LastComparisons;
            if (comparisons == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("Regression/Trend Analysis:");
            foreach (var c in comparisons)
                sb.AppendLine($"[{c.DisplayName}] Errors: {c.CurrentErrors} ({c.ErrorDelta:+#;-#;0}) | Warnings: {c.CurrentWarnings} ({c.WarningDelta:+#;-#;0}) | Status: {(c.HasRegression ? "Regressed" : c.HasImprovement ? "Improved" : "Stable")}");
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var comparisons = _category?.LastComparisons;
            if (comparisons == null) return;
            var path = EditorUtility.SaveFilePanel("Export Regression/Trend", Application.dataPath, "regression_trend.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("Category,BaselineErrors,CurrentErrors,ErrorDelta,BaselineWarnings,CurrentWarnings,WarningDelta,Status");
            foreach (var c in comparisons)
                sb.AppendLine($"{USExportUtilities.EscapeCsvField(c.DisplayName)},{c.BaselineErrors},{c.CurrentErrors},{c.ErrorDelta},{c.BaselineWarnings},{c.CurrentWarnings},{c.WarningDelta},{(c.HasRegression ? "Regressed" : c.HasImprovement ? "Improved" : "Stable")}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }
    }
}
