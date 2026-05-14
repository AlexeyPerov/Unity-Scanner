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

namespace UnityScanner.Categories.BuildPlatformReadiness
{
    public class BuildPlatformReadinessTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "build_platform_readiness";
        private const int PageSize = 50;
        private BuildPlatformReadinessCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;
        private int _sortMode;
        private bool _settingsFoldout;
        private int _subTab;
        private bool _cacheDirty = true;
        private int _expandedRow = -1;
        private string _pathFilter = "";
        private int _severityFilter;
        private string _typeFilter = "";
        private List<ImportPolicyViolation> _cachedViolations;
        private List<PlatformIncompatibility> _cachedIncompat;
        private List<StrippingRisk> _cachedStripping;
        private List<StartupBudgetStatus> _cachedBudget;
        private List<ImportPolicyViolation> _lastSourceViolations;
        private List<PlatformIncompatibility> _lastSourceIncompat;
        private List<StrippingRisk> _lastSourceStripping;
        private List<StartupBudgetStatus> _lastSourceBudget;

        public void Bind(BuildPlatformReadinessCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category == null) return;
            if (_category.LastViolations == null && _category.LastIncompatibilities == null
                && _category.LastStrippingRisks == null && _category.LastBudgetStatuses == null) return;
            DrawFilters();
            USGUIUtilities.HorizontalLine();
            DrawExportButtons();
            USGUIUtilities.HorizontalLine();

            var prevSubTab = _subTab;
            _subTab = GUILayout.Toolbar(_subTab, new[] { "Platform Violations", "Incompat.", "Stripping", "Budget" });
            if (_subTab != prevSubTab) _expandedRow = -1;

            switch (_subTab)
            {
                case 0: DrawViolationsList(); break;
                case 1: DrawIncompatibilitiesList(); break;
                case 2: DrawStrippingList(); break;
                case 3: DrawBudgetList(); break;
            }
        }

        public void DrawTopBar(UnityScannerResult result)
        {
            if (_category == null) return;
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Analysis Settings");
            if (!_settingsFoldout) return;
            EditorGUI.indentLevel++;
            var settings = _category.Settings as BuildPlatformReadinessSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.CheckImportPolicies = EditorGUILayout.ToggleLeft("Check Import Policies", settings.CheckImportPolicies);
            settings.CheckPlatformCompatibility = EditorGUILayout.ToggleLeft("Check Platform Compatibility", settings.CheckPlatformCompatibility);
            settings.CheckStartupBudget = EditorGUILayout.ToggleLeft("Check Startup Budget", settings.CheckStartupBudget);
            settings.CheckStrippingRisk = EditorGUILayout.ToggleLeft("Check Stripping Risk", settings.CheckStrippingRisk);
            settings.CheckProfileConformance = EditorGUILayout.ToggleLeft("Check Profile Conformance", settings.CheckProfileConformance);
            settings.PathFilter = EditorGUILayout.TextField("Path Filter", settings.PathFilter);
            if (EditorGUI.EndChangeCheck()) _cacheDirty = true;
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

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Path:", GUILayout.Width(40));
            var newPath = GUILayout.TextField(_pathFilter, GUILayout.Width(200));
            if (newPath != _pathFilter) { _pathFilter = newPath; _cacheDirty = true; }

            var sevLabel = _severityFilter switch { 1 => "Errors", 2 => "Errors+Warn", _ => "All Severity" };
            if (GUILayout.Button(new GUIContent("Severity: " + sevLabel, "Filter by severity level"), GUILayout.Width(130)))
            { _severityFilter = _severityFilter >= 2 ? 0 : _severityFilter + 1; _cacheDirty = true; }

            if (_subTab == 0)
            {
                var typeLabel = string.IsNullOrEmpty(_typeFilter) ? "All Types" : _typeFilter;
                if (GUILayout.Button(new GUIContent("Type: " + typeLabel, "Filter by issue type"), GUILayout.Width(150)))
                {
                    var types = new[] { "", "oversized_texture", "compression_mismatch", "oversized_clip", "audio_load_mismatch" };
                    var idx = Array.IndexOf(types, _typeFilter);
                    _typeFilter = types[(idx + 1) % types.Length];
                    _cacheDirty = true;
                }
            }
            else if (_subTab == 2)
            {
                var typeLabel = string.IsNullOrEmpty(_typeFilter) ? "All Types" : _typeFilter;
                if (GUILayout.Button(new GUIContent("Type: " + typeLabel, "Filter by issue type"), GUILayout.Width(150)))
                {
                    var types = new[] { "", "reflection", "addressables" };
                    var idx = Array.IndexOf(types, _typeFilter);
                    _typeFilter = types[(idx + 1) % types.Length];
                    _cacheDirty = true;
                }
            }
            else
            {
                _typeFilter = "";
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawViolationsList()
        {
            var items = GetFilteredViolations();
            if (items.Count == 0)
            {
                GUILayout.Label("No issues", EditorStyles.miniLabel);
                return;
            }
            USGUIPaginationUtilities.DrawPagesWidget(items.Count, _pagination);
            USGUIUtilities.HorizontalLine();
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < items.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination)) continue;
                DrawViolationRow(items[i], i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawIncompatibilitiesList()
        {
            var items = GetFilteredIncompat();
            if (items.Count == 0)
            {
                GUILayout.Label("No issues", EditorStyles.miniLabel);
                return;
            }
            USGUIPaginationUtilities.DrawPagesWidget(items.Count, _pagination);
            USGUIUtilities.HorizontalLine();
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < items.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination)) continue;
                DrawIncompatRow(items[i], i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawStrippingList()
        {
            var items = GetFilteredStripping();
            if (items.Count == 0)
            {
                GUILayout.Label("No issues", EditorStyles.miniLabel);
                return;
            }
            USGUIPaginationUtilities.DrawPagesWidget(items.Count, _pagination);
            USGUIUtilities.HorizontalLine();
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < items.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination)) continue;
                DrawStrippingRow(items[i], i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawBudgetList()
        {
            var items = GetFilteredBudget();
            if (items.Count == 0)
            {
                GUILayout.Label("No issues", EditorStyles.miniLabel);
                return;
            }
            USGUIPaginationUtilities.DrawPagesWidget(items.Count, _pagination);
            USGUIUtilities.HorizontalLine();
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < items.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination)) continue;
                DrawBudgetRow(items[i], i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawViolationRow(ImportPolicyViolation v, int index)
        {
            var isExpanded = _expandedRow == index;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;
            var sevColor = v.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, 1 => Color.cyan, _ => Color.white };
            USGUIUtilities.DrawColoredLabel("[" + v.WarningLevel + "]", sevColor, 30);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(v.AssetPath))
                USGUIUtilities.DrawAssetButton(v.AssetPath, 220f, 18f);
            else
                GUILayout.Label(v.AssetName ?? "", EditorStyles.miniLabel, GUILayout.Width(220));
            GUILayout.Label(new GUIContent(v.ViolationType, "Violation type identifier"), EditorStyles.miniLabel, GUILayout.Width(120));
            GUILayout.Label(new GUIContent(v.AssetType, "Asset type (Texture, Audio, etc.)"), EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(v.Description, EditorStyles.wordWrappedMiniLabel);
                GUILayout.Label(new GUIContent("Current: " + v.CurrentValue + "  |  Expected: " + v.ExpectedValue, "Current value vs. expected policy value"), EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(v.RecommendedFix))
                {
                    GUILayout.Space(2);
                    var prev = GUI.color;
                    GUI.color = new Color(0.5f, 0.9f, 0.5f);
                    GUILayout.Label("Fix: " + v.RecommendedFix, EditorStyles.wordWrappedMiniLabel);
                    GUI.color = prev;
                }
                USGUIUtilities.DrawCustomWarnings(v);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawIncompatRow(PlatformIncompatibility inc, int index)
        {
            var isExpanded = _expandedRow == index;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;
            var sevColor = inc.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, 1 => Color.cyan, _ => Color.white };
            USGUIUtilities.DrawColoredLabel("[" + inc.WarningLevel + "]", sevColor, 30);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(inc.AssetPath))
                USGUIUtilities.DrawAssetButton(inc.AssetPath, 220f, 18f);
            else
                GUILayout.Label(inc.AssetName ?? "", EditorStyles.miniLabel, GUILayout.Width(220));
            GUILayout.Label(new GUIContent(inc.SettingName ?? "", "Setting that is incompatible"), EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(inc.Description, EditorStyles.wordWrappedMiniLabel);
                GUILayout.Label("Current: " + inc.CurrentValue + "  |  Required: " + inc.RequiredValue, EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(inc.RecommendedFix))
                {
                    GUILayout.Space(2);
                    var prev = GUI.color;
                    GUI.color = new Color(0.5f, 0.9f, 0.5f);
                    GUILayout.Label("Fix: " + inc.RecommendedFix, EditorStyles.wordWrappedMiniLabel);
                    GUI.color = prev;
                }
                USGUIUtilities.DrawCustomWarnings(inc);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawStrippingRow(StrippingRisk risk, int index)
        {
            var isExpanded = _expandedRow == index;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;
            var sevColor = risk.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, 1 => Color.cyan, _ => Color.white };
            USGUIUtilities.DrawColoredLabel("[" + risk.WarningLevel + "]", sevColor, 30);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(risk.ScriptPath))
                USGUIUtilities.DrawAssetButton(risk.ScriptPath, 220f, 18f);
            else
                GUILayout.Label(risk.ScriptName ?? "", EditorStyles.miniLabel, GUILayout.Width(220));
            GUILayout.Label(new GUIContent(risk.RiskType, "Risk type: reflection or addressables"), EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(risk.Description, EditorStyles.wordWrappedMiniLabel);
                if (!string.IsNullOrEmpty(risk.RecommendedFix))
                {
                    GUILayout.Space(2);
                    var prev = GUI.color;
                    GUI.color = new Color(0.5f, 0.9f, 0.5f);
                    GUILayout.Label("Fix: " + risk.RecommendedFix, EditorStyles.wordWrappedMiniLabel);
                    GUI.color = prev;
                }
                USGUIUtilities.DrawCustomWarnings(risk);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawBudgetRow(StartupBudgetStatus budget, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var sevColor = budget.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, 1 => Color.cyan, _ => Color.white };
            USGUIUtilities.DrawColoredLabel("[" + budget.WarningLevel + "]", sevColor, 30);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            GUILayout.Label(new GUIContent(budget.Category, budget.Tooltip ?? budget.Category), EditorStyles.miniLabel, GUILayout.Width(120));
            var pctColor = budget.PercentUsed > 100 ? Color.red : budget.PercentUsed > 80 ? Color.yellow : Color.green;
            USGUIUtilities.DrawColoredLabel(new GUIContent(budget.PercentUsed.ToString("F0") + "%", "Percentage of budget used. Over 80% is a warning, over 100% is critical."), pctColor, 50);
            GUILayout.Label(new GUIContent(
                USExportUtilities.GetReadableSize(budget.CurrentBytes) + " / " + USExportUtilities.GetReadableSize(budget.BudgetBytes),
                "Current usage vs. platform budget limit."), EditorStyles.miniLabel, GUILayout.Width(180));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(new GUIContent("  " + budget.Description, budget.Tooltip ?? budget.Description), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(budget.Explanation))
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                var prev = GUI.color;
                GUI.color = new Color(0.7f, 0.85f, 1f);
                GUILayout.Label(new GUIContent("  " + budget.Explanation, budget.Explanation), EditorStyles.wordWrappedMiniLabel);
                GUI.color = prev;
                EditorGUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(budget.RecommendedFix))
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                var prev = GUI.color;
                GUI.color = new Color(0.5f, 0.9f, 0.5f);
                GUILayout.Label("Fix: " + budget.RecommendedFix, EditorStyles.wordWrappedMiniLabel);
                GUI.color = prev;
                EditorGUILayout.EndHorizontal();
            }
            USGUIUtilities.DrawCustomWarnings(budget);
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Build/Platform Readiness:");
            sb.AppendLine("--- Import Policy Violations ---");
            foreach (var v in GetFilteredViolations())
                sb.AppendLine("[" + v.WarningLevel + "] " + v.AssetType + " " + v.AssetName + " | " + v.ViolationType + " | " + v.Description);
            sb.AppendLine("--- Platform Incompatibilities ---");
            foreach (var inc in GetFilteredIncompat())
                sb.AppendLine("[" + inc.WarningLevel + "] " + inc.AssetName + " | " + inc.SettingName + " | " + inc.Description);
            sb.AppendLine("--- Stripping Risks ---");
            foreach (var r in GetFilteredStripping())
                sb.AppendLine("[" + r.WarningLevel + "] " + r.ScriptName + " | " + r.RiskType + " | " + r.Description);
            sb.AppendLine("--- Startup Budget ---");
            foreach (var b in GetFilteredBudget())
                sb.AppendLine("[" + b.WarningLevel + "] " + b.Category + " | " + b.PercentUsed.ToString("F0") + "% | " + b.Description);
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Build/Platform Readiness", Application.dataPath, "build_platform_readiness.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("Type,WarningLevel,Name,Path,ViolationType,Description");
            foreach (var v in GetFilteredViolations())
                sb.AppendLine("Violation," + v.WarningLevel + "," + USExportUtilities.EscapeCsvField(v.AssetName) + "," + USExportUtilities.EscapeCsvField(v.AssetPath) + "," + USExportUtilities.EscapeCsvField(v.ViolationType) + "," + USExportUtilities.EscapeCsvField(v.Description));
            foreach (var inc in GetFilteredIncompat())
                sb.AppendLine("Incompatibility," + inc.WarningLevel + "," + USExportUtilities.EscapeCsvField(inc.AssetName) + "," + USExportUtilities.EscapeCsvField(inc.AssetPath) + "," + USExportUtilities.EscapeCsvField(inc.SettingName) + "," + USExportUtilities.EscapeCsvField(inc.Description));
            foreach (var r in GetFilteredStripping())
                sb.AppendLine("StrippingRisk," + r.WarningLevel + "," + USExportUtilities.EscapeCsvField(r.ScriptName) + "," + USExportUtilities.EscapeCsvField(r.ScriptPath) + "," + USExportUtilities.EscapeCsvField(r.RiskType) + "," + USExportUtilities.EscapeCsvField(r.Description));
            foreach (var b in GetFilteredBudget())
                sb.AppendLine("Budget," + b.WarningLevel + "," + USExportUtilities.EscapeCsvField(b.Category) + ",," + b.PercentUsed.ToString("F0") + "%," + USExportUtilities.EscapeCsvField(b.Description));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private List<ImportPolicyViolation> GetFilteredViolations()
        {
            if (!_cacheDirty && _cachedViolations != null) return _cachedViolations;
            EnsureCache();
            return _cachedViolations;
        }

        private List<PlatformIncompatibility> GetFilteredIncompat()
        {
            EnsureCache();
            return _cachedIncompat;
        }

        private List<StrippingRisk> GetFilteredStripping()
        {
            EnsureCache();
            return _cachedStripping;
        }

        private List<StartupBudgetStatus> GetFilteredBudget()
        {
            EnsureCache();
            return _cachedBudget;
        }

        private void EnsureCache()
        {
            var srcV = _category?.LastViolations;
            var srcI = _category?.LastIncompatibilities;
            var srcS = _category?.LastStrippingRisks;
            var srcB = _category?.LastBudgetStatuses;
            if (!_cacheDirty && _cachedViolations != null
                && ReferenceEquals(srcV, _lastSourceViolations)
                && ReferenceEquals(srcI, _lastSourceIncompat)
                && ReferenceEquals(srcS, _lastSourceStripping)
                && ReferenceEquals(srcB, _lastSourceBudget))
                return;
            _lastSourceViolations = srcV;
            _lastSourceIncompat = srcI;
            _lastSourceStripping = srcS;
            _lastSourceBudget = srcB;

            var rawV = srcV ?? new List<ImportPolicyViolation>();
            var rawI = srcI ?? new List<PlatformIncompatibility>();
            var rawS = srcS ?? new List<StrippingRisk>();
            var rawB = srcB ?? new List<StartupBudgetStatus>();

            _cachedViolations = ApplyFilters(rawV);
            _cachedIncompat = ApplyFilters(rawI);
            _cachedStripping = ApplyStrippingFilters(rawS);
            _cachedBudget = rawB;
            _cacheDirty = false;
        }

        private List<ImportPolicyViolation> ApplyFilters(List<ImportPolicyViolation> items)
        {
            var f = items.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                f = f.Where(x => (x.AssetPath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_severityFilter == 1)
                f = f.Where(x => x.WarningLevel >= 3);
            else if (_severityFilter == 2)
                f = f.Where(x => x.WarningLevel >= 2);
            if (!string.IsNullOrEmpty(_typeFilter))
                f = f.Where(x => x.ViolationType == _typeFilter);
            return f.ToList();
        }

        private List<PlatformIncompatibility> ApplyFilters(List<PlatformIncompatibility> items)
        {
            var f = items.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                f = f.Where(x => (x.AssetPath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_severityFilter == 1)
                f = f.Where(x => x.WarningLevel >= 3);
            else if (_severityFilter == 2)
                f = f.Where(x => x.WarningLevel >= 2);
            return f.ToList();
        }

        private List<StrippingRisk> ApplyStrippingFilters(List<StrippingRisk> items)
        {
            var f = items.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                f = f.Where(x => (x.ScriptPath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_severityFilter == 1)
                f = f.Where(x => x.WarningLevel >= 3);
            else if (_severityFilter == 2)
                f = f.Where(x => x.WarningLevel >= 2);
            if (!string.IsNullOrEmpty(_typeFilter))
                f = f.Where(x => x.RiskType == _typeFilter);
            return f.ToList();
        }
    }
}
