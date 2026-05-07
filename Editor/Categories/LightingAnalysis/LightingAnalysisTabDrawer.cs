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

namespace UnityScanner.Categories.LightingAnalysis
{
    public class LightingAnalysisTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "lighting_analysis";
        private const int PageSize = 50;
        private LightingAnalysisCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;
        private int _subTab;
        private bool _settingsFoldout;
        private bool _cacheDirty = true;
        private int _expandedRow = -1;
        private string _pathFilter = "";
        private int _severityFilter;
        private List<SceneLightingData> _cachedResults;
        private List<SceneLightingData> _lastSourceResults;

        public void Bind(LightingAnalysisCategory category) { _category = category; }

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
            _subTab = GUILayout.Toolbar(_subTab, new[] { "Lights", "Baking", "Probes", "Emissives" });
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
            var settings = _category.Settings as LightingAnalysisSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.CheckRealtimeExceeded = EditorGUILayout.ToggleLeft("Check Realtime Exceeded", settings.CheckRealtimeExceeded);
            settings.CheckShadowsOnMobile = EditorGUILayout.ToggleLeft("Check Shadows on Mobile", settings.CheckShadowsOnMobile);
            settings.CheckLightmapOversized = EditorGUILayout.ToggleLeft("Check Lightmap Oversized", settings.CheckLightmapOversized);
            settings.CheckModeInconsistent = EditorGUILayout.ToggleLeft("Check Mode Inconsistent", settings.CheckModeInconsistent);
            settings.CheckBakedSetToRealtime = EditorGUILayout.ToggleLeft("Check Baked Set to Realtime", settings.CheckBakedSetToRealtime);
            settings.CheckProbeMissing = EditorGUILayout.ToggleLeft("Check Probe Missing", settings.CheckProbeMissing);
            settings.CheckReflectionProbeExceeded = EditorGUILayout.ToggleLeft("Check Reflection Probes", settings.CheckReflectionProbeExceeded);
            settings.CheckEmissiveNoGI = EditorGUILayout.ToggleLeft("Check Emissive No GI", settings.CheckEmissiveNoGI);
            settings.CheckPipelineMismatch = EditorGUILayout.ToggleLeft("Check Pipeline Mismatch", settings.CheckPipelineMismatch);
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
                    case 0: DrawLightsRow(items[i], i); break;
                    case 1: DrawBakingRow(items[i], i); break;
                    case 2: DrawProbesRow(items[i], i); break;
                    case 3: DrawEmissivesRow(items[i], i); break;
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawLightsRow(SceneLightingData d, int idx)
        {
            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            var rtColor = d.RealtimeLightCount > 8 ? Color.yellow : Color.white;
            USGUIUtilities.DrawColoredLabel("RT:" + d.RealtimeLightCount, rtColor, 50);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.ScenePath))
                USGUIUtilities.DrawAssetButton(d.ScenePath, 250f, 18f);
            GUILayout.Label("M:" + d.MixedLightCount + " B:" + d.BakedLightCount, EditorStyles.miniLabel, GUILayout.Width(90));
            GUILayout.Label(d.ActivePipeline ?? "", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Realtime: " + d.RealtimeLightCount + " | Mixed: " + d.MixedLightCount + " | Baked: " + d.BakedLightCount, EditorStyles.miniLabel);
                GUILayout.Label("Pipeline: " + d.ActivePipeline, EditorStyles.miniLabel);
                foreach (var light in d.Lights.Take(15))
                {
                    var shadowIndicator = light.ShadowsEnabled ? " [SHADOW]" : "";
                    GUILayout.Label("  " + light.LightType + " (" + light.LightMode + ") " + light.ObjectPath + shadowIndicator, EditorStyles.miniLabel);
                }
                if (d.Lights.Count > 15)
                    GUILayout.Label("  ... and " + (d.Lights.Count - 15) + " more", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawBakingRow(SceneLightingData d, int idx)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("LM:" + d.LightmapCount + " Size:" + d.LightmapSize, EditorStyles.miniLabel, GUILayout.Width(140));
            if (!string.IsNullOrEmpty(d.ScenePath))
                USGUIUtilities.DrawAssetButton(d.ScenePath, 250f, 18f);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProbesRow(SceneLightingData d, int idx)
        {
            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            var probeColor = d.ReflectionProbeCount > 4 ? Color.yellow : Color.white;
            USGUIUtilities.DrawColoredLabel("RP:" + d.ReflectionProbeCount, probeColor, 45);
            var lpColor = !d.HasLightProbes && (d.RealtimeLightCount + d.MixedLightCount > 0) ? Color.red : Color.green;
            USGUIUtilities.DrawColoredLabel(d.HasLightProbes ? "LP:yes" : "LP:NO", lpColor, 50);
            if (!string.IsNullOrEmpty(d.ScenePath))
                USGUIUtilities.DrawAssetButton(d.ScenePath, 250f, 18f);
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Reflection probes: " + d.ReflectionProbeCount + " | Max resolution: " + d.MaxReflectionProbeResolution, EditorStyles.miniLabel);
                GUILayout.Label("Light probes: " + (d.HasLightProbes ? "Present" : "Missing"), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawEmissivesRow(SceneLightingData d, int idx)
        {
            if (d.EmissiveMaterials.Count == 0) return;
            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            USGUIUtilities.DrawColoredLabel("EM:" + d.EmissiveMaterials.Count, Color.cyan, 45);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var em in d.EmissiveMaterials)
                    GUILayout.Label(em.MaterialName + " — Flags: " + em.GlobalIlluminationFlags + " | " + em.MaterialPath, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Lighting Analysis:");
            foreach (var d in GetFilteredResults())
            {
                sb.AppendLine("Scene: " + d.ScenePath + " | RT:" + d.RealtimeLightCount +
                    " Mixed:" + d.MixedLightCount + " Baked:" + d.BakedLightCount +
                    " RP:" + d.ReflectionProbeCount + " LP:" + d.HasLightProbes +
                    " LM:" + d.LightmapCount + " Size:" + d.LightmapSize);
            }
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Lighting Analysis", Application.dataPath, "lighting_analysis.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("ScenePath,RealtimeLights,MixedLights,BakedLights,TotalLights,ReflectionProbes,MaxProbeResolution,HasLightProbes,LightmapCount,LightmapSize,Pipeline");
            foreach (var d in GetFilteredResults())
                sb.AppendLine(USExportUtilities.EscapeCsvField(d.ScenePath) + "," +
                    d.RealtimeLightCount + "," + d.MixedLightCount + "," +
                    d.BakedLightCount + "," + d.TotalLightCount + "," +
                    d.ReflectionProbeCount + "," + d.MaxReflectionProbeResolution + "," +
                    d.HasLightProbes + "," + d.LightmapCount + "," +
                    d.LightmapSize + "," + USExportUtilities.EscapeCsvField(d.ActivePipeline ?? ""));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private List<SceneLightingData> GetFilteredResults()
        {
            EnsureCache();
            return _cachedResults;
        }

        private void EnsureCache()
        {
            var src = _category?.LastResults;
            if (!_cacheDirty && _cachedResults != null && ReferenceEquals(src, _lastSourceResults)) return;
            _lastSourceResults = src;
            var raw = src ?? new List<SceneLightingData>();
            var f = raw.AsEnumerable();
            if (!string.IsNullOrEmpty(_pathFilter))
                f = f.Where(x => (x.ScenePath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            if (_severityFilter == 1)
                f = f.Where(x => x.WarningLevel >= 3);
            else if (_severityFilter == 2)
                f = f.Where(x => x.WarningLevel >= 2);
            _cachedResults = f.ToList();
            _cacheDirty = false;
        }
    }
}
