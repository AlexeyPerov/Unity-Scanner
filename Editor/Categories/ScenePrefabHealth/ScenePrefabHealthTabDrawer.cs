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

namespace UnityScanner.Categories.ScenePrefabHealth
{
    public class ScenePrefabHealthTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "scene_prefab_health";
        private const int PageSize = 50;
        private ScenePrefabHealthCategory _category;
        public System.Action OnScanRequested;
        private readonly USPaginationSettings _scenePagination = new() { PageToShow = 0, PageSize = PageSize };
        private readonly USPaginationSettings _prefabPagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _sceneScroll;
        private Vector2 _prefabScroll;
        private string _pathFilter = "";
        private bool _warningsOnly;
        private int _sortMode;
        private bool _settingsFoldout;
        private int _expandedScene = -1;
        private int _expandedPrefab = -1;
        private List<SceneData> _cachedScenes;
        private List<PrefabData> _cachedPrefabs;
        private bool _cacheDirty = true;
        private int _subTab;
        private List<SceneData> _lastSourceScenes;
        private List<PrefabData> _lastSourcePrefabs;

        public void Bind(ScenePrefabHealthCategory category) { _category = category; }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;
            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category == null) return;
            if (_category.LastScenes == null && _category.LastPrefabs == null) return;
            DrawFilters();
            USGUIUtilities.HorizontalLine();
            DrawExportButtons();
            USGUIUtilities.HorizontalLine();

            var prevSubTab = _subTab;
            _subTab = GUILayout.Toolbar(_subTab, new[] { "Scenes", "Prefabs" });
            if (_subTab != prevSubTab) { _expandedScene = -1; _expandedPrefab = -1; }

            if (_subTab == 0) DrawScenesList();
            else DrawPrefabsList();
        }

        public void DrawTopBar(UnityScannerResult result)
        {
            if (_category == null) return;
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Analysis Settings");
            if (!_settingsFoldout) return;
            EditorGUI.indentLevel++;
            var settings = _category.Settings as ScenePrefabHealthSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.MaxPrefabNestingDepth = EditorGUILayout.IntField("Max Nesting Depth", settings.MaxPrefabNestingDepth);
            settings.MaxPrefabOverrideCount = EditorGUILayout.IntField("Max Override Count", settings.MaxPrefabOverrideCount);
            settings.MaxSceneObjectCount = EditorGUILayout.IntField("Max Scene Object Count", settings.MaxSceneObjectCount);
            settings.MaxComponentCountPerObject = EditorGUILayout.IntField("Max Components Per Object", settings.MaxComponentCountPerObject);
            settings.MaxInactiveObjectThreshold = EditorGUILayout.IntField("Inactive Object Threshold", settings.MaxInactiveObjectThreshold);
            GUILayout.Space(4);
            settings.DetectDeepNesting = EditorGUILayout.ToggleLeft("Detect Deep Nesting", settings.DetectDeepNesting);
            settings.DetectOverrideExplosion = EditorGUILayout.ToggleLeft("Detect Override Explosion", settings.DetectOverrideExplosion);
            settings.DetectHierarchyHotspots = EditorGUILayout.ToggleLeft("Detect Hierarchy Hotspots", settings.DetectHierarchyHotspots);
            settings.DetectBrokenReferences = EditorGUILayout.ToggleLeft("Detect Broken References", settings.DetectBrokenReferences);
            settings.DetectInactiveAntiPatterns = EditorGUILayout.ToggleLeft("Detect Inactive Anti-Patterns", settings.DetectInactiveAntiPatterns);
            settings.DetectHighRiskBootstrap = EditorGUILayout.ToggleLeft("Detect High-Risk Bootstrap", settings.DetectHighRiskBootstrap);
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

        private void DrawScenesList()
        {
            var filtered = GetFilteredScenes();
            USGUIPaginationUtilities.DrawPagesWidget(filtered.Count, _scenePagination);
            USGUIUtilities.HorizontalLine();
            _sceneScroll = EditorGUILayout.BeginScrollView(_sceneScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < filtered.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _scenePagination)) continue;
                DrawSceneRow(filtered[i], i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawPrefabsList()
        {
            var filtered = GetFilteredPrefabs();
            USGUIPaginationUtilities.DrawPagesWidget(filtered.Count, _prefabPagination);
            USGUIUtilities.HorizontalLine();
            _prefabScroll = EditorGUILayout.BeginScrollView(_prefabScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < filtered.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _prefabPagination)) continue;
                DrawPrefabRow(filtered[i], i);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneRow(SceneData s, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var isExpanded = _expandedScene == index;
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedScene = isExpanded ? -1 : index;
            var sevColor = s.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, 1 => Color.cyan, _ => Color.white };
            USGUIUtilities.DrawColoredLabel("[" + s.WarningLevel + "]", sevColor, 30);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            USGUIUtilities.DrawAssetButton(s.Path, 200f, 18f);
            GUILayout.Label("Objs:" + s.TotalObjectCount, EditorStyles.miniLabel, GUILayout.Width(65));
            GUILayout.Label("Comps:" + s.TotalComponentCount, EditorStyles.miniLabel, GUILayout.Width(65));
            if (s.IsBootstrapScene) { var prev = GUI.color; GUI.color = Color.yellow; GUILayout.Label("BOOT", EditorStyles.miniLabel, GUILayout.Width(35)); GUI.color = prev; }
            EditorGUILayout.EndHorizontal();
            if (isExpanded) DrawSceneDetail(s);
        }

        private void DrawSceneDetail(SceneData s)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Path: " + s.Path, EditorStyles.miniLabel);
            GUILayout.Label("Objects: " + s.TotalObjectCount + " | Components: " + s.TotalComponentCount + " | Roots: " + s.RootCount + " | Inactive: " + s.InactiveObjectCount, EditorStyles.miniLabel);
            if (s.IsBootstrapScene)
                GUILayout.Label("Bootstrap Scene: Yes", EditorStyles.miniLabel);
            if (s.BrokenReferences.Count > 0)
            {
                var prev = GUI.color; GUI.color = Color.red;
                GUILayout.Label("Broken References (" + s.BrokenReferences.Count + "):", EditorStyles.miniLabel);
                GUI.color = prev;
                EditorGUI.indentLevel++;
                foreach (var br in s.BrokenReferences.Take(10))
                    GUILayout.Label(br, EditorStyles.miniLabel);
                if (s.BrokenReferences.Count > 10)
                    GUILayout.Label("... and " + (s.BrokenReferences.Count - 10) + " more", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            if (s.HotspotPaths.Count > 0)
            {
                GUILayout.Label("Hotspots (" + s.HotspotPaths.Count + "):", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                foreach (var hp in s.HotspotPaths.Take(10))
                    GUILayout.Label(hp, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            if (s.ExpensiveInactiveObjects.Count > 0)
            {
                GUILayout.Label("Expensive Inactive (" + s.ExpensiveInactiveObjects.Count + "):", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                foreach (var io in s.ExpensiveInactiveObjects.Take(10))
                    GUILayout.Label(io.ObjectPath + " [" + io.ComponentType + "]", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void DrawPrefabRow(PrefabData p, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var isExpanded = _expandedPrefab == index;
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedPrefab = isExpanded ? -1 : index;
            var sevColor = p.WarningLevel switch { >= 3 => Color.red, 2 => Color.yellow, 1 => Color.cyan, _ => Color.white };
            USGUIUtilities.DrawColoredLabel("[" + p.WarningLevel + "]", sevColor, 30);
            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            USGUIUtilities.DrawAssetButton(p.Path, 200f, 18f);
            GUILayout.Label("Depth:" + p.NestingDepth, EditorStyles.miniLabel, GUILayout.Width(60));
            GUILayout.Label("Ovr:" + p.OverrideCount, EditorStyles.miniLabel, GUILayout.Width(55));
            GUILayout.Label(USExportUtilities.GetReadableSize(p.FileSizeBytes), EditorStyles.miniLabel, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
            if (isExpanded) DrawPrefabDetail(p);
        }

        private void DrawPrefabDetail(PrefabData p)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Path: " + p.Path, EditorStyles.miniLabel);
            GUILayout.Label("Nesting: " + p.NestingDepth + " | Overrides: " + p.OverrideCount + " | Children: " + p.ChildCount + " | Components: " + p.ComponentCount + " | Size: " + USExportUtilities.GetReadableSize(p.FileSizeBytes), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            var scenes = GetFilteredScenes();
            var prefabs = GetFilteredPrefabs();
            sb.AppendLine("Scene/Prefab Health [" + scenes.Count + " scenes, " + prefabs.Count + " prefabs]:");
            sb.AppendLine("--- Scenes ---");
            foreach (var s in scenes)
                sb.AppendLine("[" + s.WarningLevel + "] " + s.Name + " | Objs:" + s.TotalObjectCount + " Comps:" + s.TotalComponentCount + " Inactive:" + s.InactiveObjectCount + (s.IsBootstrapScene ? " [BOOTSTRAP]" : "") + " | " + s.Path);
            sb.AppendLine("--- Prefabs ---");
            foreach (var p in prefabs)
                sb.AppendLine("[" + p.WarningLevel + "] " + p.Name + " | Depth:" + p.NestingDepth + " Ovr:" + p.OverrideCount + " Children:" + p.ChildCount + " | " + p.Path);
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Scene/Prefab Health", Application.dataPath, "scene_prefab_health.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("Type,WarningLevel,Name,Path,SizeBytes,Objects,Components,Roots,Inactive,NestingDepth,Overrides,Children,Bootstrap");
            foreach (var s in GetFilteredScenes())
                sb.AppendLine("Scene," + s.WarningLevel + "," + USExportUtilities.EscapeCsvField(s.Name) + "," + USExportUtilities.EscapeCsvField(s.Path) + "," + s.FileSizeBytes + "," + s.TotalObjectCount + "," + s.TotalComponentCount + "," + s.RootCount + "," + s.InactiveObjectCount + ",0,0,0," + s.IsBootstrapScene);
            foreach (var p in GetFilteredPrefabs())
                sb.AppendLine("Prefab," + p.WarningLevel + "," + USExportUtilities.EscapeCsvField(p.Name) + "," + USExportUtilities.EscapeCsvField(p.Path) + "," + p.FileSizeBytes + ",0," + p.ComponentCount + ",0,0," + p.NestingDepth + "," + p.OverrideCount + "," + p.ChildCount + ",False");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private void InvalidateCache() { _cacheDirty = true; }

        private List<SceneData> GetFilteredScenes()
        {
            EnsureCache();
            return _cachedScenes;
        }

        private List<PrefabData> GetFilteredPrefabs()
        {
            EnsureCache();
            return _cachedPrefabs;
        }

        private void EnsureCache()
        {
            var srcScenes = _category?.LastScenes;
            var srcPrefabs = _category?.LastPrefabs;
            if (!_cacheDirty && _cachedScenes != null && _cachedPrefabs != null
                && ReferenceEquals(srcScenes, _lastSourceScenes)
                && ReferenceEquals(srcPrefabs, _lastSourcePrefabs))
                return;
            _lastSourceScenes = srcScenes;
            _lastSourcePrefabs = srcPrefabs;
            _cachedScenes = FilterScenes(srcScenes);
            _cachedPrefabs = FilterPrefabs(srcPrefabs);
            _cacheDirty = false;
        }

        private List<SceneData> FilterScenes(List<SceneData> items)
        {
            if (items == null) return new List<SceneData>();
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

        private List<PrefabData> FilterPrefabs(List<PrefabData> items)
        {
            if (items == null) return new List<PrefabData>();
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
