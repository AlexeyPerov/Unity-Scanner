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

namespace UnityScanner.Categories.ParticleSystemAnalysis
{
    public class ParticleSystemAnalysisTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "particle_analysis";
        private const int PageSize = 50;
        private ParticleSystemAnalysisCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;
        private int _subTab;
        private bool _settingsFoldout;
        private bool _cacheDirty = true;
        private int _expandedRow = -1;
        private string _pathFilter = "";
        private int _severityFilter;
        private int _emissionRangeFilter;
        private List<ParticleSystemData> _cachedResults;
        private List<ParticleSystemData> _lastSourceResults;

        public void Bind(ParticleSystemAnalysisCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category == null) return;
            if (_category.LastResults == null) return;

            DrawFilters();
            USGUIUtilities.HorizontalLine();
            DrawExportButtons();
            USGUIUtilities.HorizontalLine();

            var prevSubTab = _subTab;
            _subTab = GUILayout.Toolbar(_subTab, new[] { "Emission", "Modules", "Textures", "Collisions" });
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
            var settings = _category.Settings as ParticleSystemAnalysisSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.CheckEmission = EditorGUILayout.ToggleLeft("Check Emission", settings.CheckEmission);
            settings.CheckCollision = EditorGUILayout.ToggleLeft("Check Collision", settings.CheckCollision);
            settings.CheckOverdraw = EditorGUILayout.ToggleLeft("Check Overdraw/Trails", settings.CheckOverdraw);
            settings.CheckSubEmitters = EditorGUILayout.ToggleLeft("Check Sub-Emitters", settings.CheckSubEmitters);
            settings.CheckLOD = EditorGUILayout.ToggleLeft("Check LOD", settings.CheckLOD);
            settings.CheckSimulationMismatch = EditorGUILayout.ToggleLeft("Check CPU/GPU Simulation", settings.CheckSimulationMismatch);
            settings.CheckTextures = EditorGUILayout.ToggleLeft("Check Textures", settings.CheckTextures);
            settings.CheckModuleCount = EditorGUILayout.ToggleLeft("Check Module Count", settings.CheckModuleCount);
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
            if (GUILayout.Button(new GUIContent("Severity: " + sevLabel, "Filter by severity level"), GUILayout.Width(130)))
            { _severityFilter = _severityFilter >= 2 ? 0 : _severityFilter + 1; _cacheDirty = true; }

            var emLabel = _emissionRangeFilter switch { 1 => "Emission > 0", 2 => "Emission > Threshold", _ => "All Emission" };
            if (GUILayout.Button(new GUIContent("Emission: " + emLabel, "Filter by emission rate"), GUILayout.Width(140)))
            { _emissionRangeFilter = _emissionRangeFilter >= 2 ? 0 : _emissionRangeFilter + 1; _cacheDirty = true; }

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

        private void DrawFilteredList()
        {
            var items = GetFilteredResults();
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

                switch (_subTab)
                {
                    case 0: DrawEmissionRow(items[i], i); break;
                    case 1: DrawModulesRow(items[i], i); break;
                    case 2: DrawTexturesRow(items[i], i); break;
                    case 3: DrawCollisionsRow(items[i], i); break;
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawEmissionRow(ParticleSystemData data, int index)
        {
            var isExpanded = _expandedRow == index;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;

            var rateColor = data.EmissionRate > 500 ? Color.yellow : Color.white;
            USGUIUtilities.DrawColoredLabel("E:" + data.EmissionRate, rateColor, 60);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));

            if (!string.IsNullOrEmpty(data.AssetPath))
                USGUIUtilities.DrawAssetButton(data.AssetPath, 220f, 18f);

            if (data.IsBurst)
                USGUIUtilities.DrawColoredLabel("BURST x" + data.BurstCount, Color.cyan, 80);

            GUILayout.Label("Max:" + data.MaxParticles, EditorStyles.miniLabel, GUILayout.Width(70));
            GUILayout.Label(data.SimulationSpace, EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Emission rate: " + data.EmissionRate + (data.IsBurst ? " (burst count: " + data.BurstCount + ")" : ""), EditorStyles.miniLabel);
                GUILayout.Label("Max particles: " + data.MaxParticles + " | Simulation space: " + data.SimulationSpace, EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(data.ScenePath))
                    GUILayout.Label("Scene: " + data.ScenePath, EditorStyles.miniLabel);
                if (data.TrailEnabled)
                {
                    var prev = GUI.color;
                    GUI.color = Color.yellow;
                    GUILayout.Label("Trail lifetime: " + data.TrailLifetime.ToString("F1") + "s", EditorStyles.miniLabel);
                    GUI.color = prev;
                }
                if (data.SubEmitterCount > 0)
                {
                    var prev = GUI.color;
                    GUI.color = data.SubEmitterChainDepth > 2 ? Color.red : Color.cyan;
                    GUILayout.Label("Sub-emitters: " + data.SubEmitterCount + " (chain depth: " + data.SubEmitterChainDepth + ")", EditorStyles.miniLabel);
                    GUI.color = prev;
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawModulesRow(ParticleSystemData data, int index)
        {
            var isExpanded = _expandedRow == index;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;

            var modColor = data.ActiveModuleCount > 10 ? Color.yellow : Color.white;
            USGUIUtilities.DrawColoredLabel("M:" + data.ActiveModuleCount, modColor, 45);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));

            if (!string.IsNullOrEmpty(data.AssetPath))
                USGUIUtilities.DrawAssetButton(data.AssetPath, 220f, 18f);

            GUILayout.Label(data.ActiveModules.Count + " modules", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Active modules (" + data.ActiveModuleCount + "):", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                foreach (var mod in data.ActiveModules)
                    GUILayout.Label("- " + mod, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawTexturesRow(ParticleSystemData data, int index)
        {
            var isExpanded = _expandedRow == index;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;

            if (!string.IsNullOrEmpty(data.MainTexturePath))
            {
                var texColor = data.MainTextureSize > 2048 ? Color.yellow : Color.white;
                USGUIUtilities.DrawColoredLabel(data.MainTextureSize + "px", texColor, 50);
            }
            else
            {
                GUILayout.Label("No tex", EditorStyles.miniLabel, GUILayout.Width(50));
            }

            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));

            if (!string.IsNullOrEmpty(data.AssetPath))
                USGUIUtilities.DrawAssetButton(data.AssetPath, 220f, 18f);

            if (!string.IsNullOrEmpty(data.MainTexturePath))
                GUILayout.Label(Path.GetFileName(data.MainTexturePath), EditorStyles.miniLabel, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (!string.IsNullOrEmpty(data.MainTexturePath))
                {
                    GUILayout.Label("Texture: " + data.MainTexturePath, EditorStyles.miniLabel);
                    GUILayout.Label("Size: " + data.MainTextureSize + "px", EditorStyles.miniLabel);
                    if (!string.IsNullOrEmpty(data.AssetPath))
                        GUILayout.Label("Particle system: " + data.AssetPath, EditorStyles.miniLabel);
                }
                else
                {
                    GUILayout.Label("No main texture assigned to particle material.", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawCollisionsRow(ParticleSystemData data, int index)
        {
            var isExpanded = _expandedRow == index;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : index;

            var colColor = data.CollisionEnabled && !data.CollisionSendMessages ? Color.yellow : Color.white;
            USGUIUtilities.DrawColoredLabel(data.CollisionEnabled ? "ON" : "OFF", colColor, 35);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));

            if (!string.IsNullOrEmpty(data.AssetPath))
                USGUIUtilities.DrawAssetButton(data.AssetPath, 220f, 18f);

            GUILayout.Label("Send: " + data.CollisionSendMessages, EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Collision enabled: " + data.CollisionEnabled, EditorStyles.miniLabel);
                GUILayout.Label("Send messages: " + data.CollisionSendMessages, EditorStyles.miniLabel);
                if (data.CollisionEnabled && !data.CollisionSendMessages)
                {
                    var prev = GUI.color;
                    GUI.color = new Color(0.5f, 0.9f, 0.5f);
                    GUILayout.Label("Fix: Disable collision module or enable send collision messages if gameplay requires it.", EditorStyles.wordWrappedMiniLabel);
                    GUI.color = prev;
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Particle System Analysis:");
            sb.AppendLine("--- Emission ---");
            foreach (var d in GetFilteredResults())
                sb.AppendLine("Rate:" + d.EmissionRate + " Burst:" + d.IsBurst + " MaxP:" + d.MaxParticles + " Space:" + d.SimulationSpace + " | " + d.AssetPath);
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Particle Analysis", Application.dataPath, "particle_analysis.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("AssetPath,ScenePath,EmissionRate,IsBurst,BurstCount,MaxParticles,SimulationSpace,ActiveModules,CollisionEnabled,TrailEnabled,TrailLifetime,SubEmitterCount,ChainDepth,TextureSize");
            foreach (var d in GetFilteredResults())
                sb.AppendLine(USExportUtilities.EscapeCsvField(d.AssetPath) + "," +
                    USExportUtilities.EscapeCsvField(d.ScenePath) + "," +
                    d.EmissionRate + "," +
                    d.IsBurst + "," +
                    d.BurstCount + "," +
                    d.MaxParticles + "," +
                    USExportUtilities.EscapeCsvField(d.SimulationSpace) + "," +
                    d.ActiveModuleCount + "," +
                    d.CollisionEnabled + "," +
                    d.TrailEnabled + "," +
                    d.TrailLifetime.ToString("F1") + "," +
                    d.SubEmitterCount + "," +
                    d.SubEmitterChainDepth + "," +
                    d.MainTextureSize);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private List<ParticleSystemData> GetFilteredResults()
        {
            EnsureCache();
            return _cachedResults;
        }

        private void EnsureCache()
        {
            var src = _category?.LastResults;
            if (!_cacheDirty && _cachedResults != null && ReferenceEquals(src, _lastSourceResults))
                return;

            _lastSourceResults = src;
            var raw = src ?? new List<ParticleSystemData>();

            var f = raw.AsEnumerable();

            if (!string.IsNullOrEmpty(_pathFilter))
                f = f.Where(x => (x.AssetPath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 (x.ScenePath ?? "").IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            if (_emissionRangeFilter == 1)
                f = f.Where(x => x.EmissionRate > 0);
            else if (_emissionRangeFilter == 2)
                f = f.Where(x => x.EmissionRate > 500);

            _cachedResults = f.ToList();
            _cacheDirty = false;
        }
    }
}
