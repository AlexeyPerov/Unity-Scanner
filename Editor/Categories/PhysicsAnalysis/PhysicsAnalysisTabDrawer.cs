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

namespace UnityScanner.Categories.PhysicsAnalysis
{
    public class PhysicsAnalysisTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "physics_analysis";
        private const int PageSize = 50;
        private PhysicsAnalysisCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;
        private int _subTab;
        private bool _settingsFoldout;
        private bool _cacheDirty = true;
        private int _expandedRow = -1;
        private string _pathFilter = "";
        private int _severityFilter;
        private List<ScenePhysicsData> _cachedResults;
        private List<ScenePhysicsData> _lastSourceResults;

        public void Bind(PhysicsAnalysisCategory category) { _category = category; }

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
            _subTab = GUILayout.Toolbar(_subTab, new[] { "Rigidbodies", "Colliders", "Layer Matrix", "Materials" });
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
            var settings = _category.Settings as PhysicsAnalysisSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }
            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            settings.CheckRigidbodyExceeded = EditorGUILayout.ToggleLeft("Check Rigidbody Exceeded", settings.CheckRigidbodyExceeded);
            settings.CheckStaticColliderOnMovingParent = EditorGUILayout.ToggleLeft("Check Static Collider on Moving Parent", settings.CheckStaticColliderOnMovingParent);
            settings.CheckNoGravityNoConstraints = EditorGUILayout.ToggleLeft("Check No Gravity No Constraints", settings.CheckNoGravityNoConstraints);
            settings.CheckTriggerNonKinematic = EditorGUILayout.ToggleLeft("Check Trigger Non-Kinematic", settings.CheckTriggerNonKinematic);
            settings.CheckInterpolationUnnecessary = EditorGUILayout.ToggleLeft("Check Interpolation Unnecessary", settings.CheckInterpolationUnnecessary);
            settings.CheckMeshColliderComplex = EditorGUILayout.ToggleLeft("Check Mesh Collider Complex", settings.CheckMeshColliderComplex);
            settings.CheckConcaveMeshKinematic = EditorGUILayout.ToggleLeft("Check Concave Mesh Kinematic", settings.CheckConcaveMeshKinematic);
            settings.CheckMissingMaterial = EditorGUILayout.ToggleLeft("Check Missing Material", settings.CheckMissingMaterial);
            settings.CheckLayerMatrixBloat = EditorGUILayout.ToggleLeft("Check Layer Matrix Bloat", settings.CheckLayerMatrixBloat);
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
            if (items.Count == 0) { GUILayout.Label("No physics data found.", EditorStyles.miniLabel); return; }
            USGUIPaginationUtilities.DrawPagesWidget(items.Count, _pagination);
            USGUIUtilities.HorizontalLine();
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.BeginVertical();
            for (var i = 0; i < items.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination)) continue;
                switch (_subTab)
                {
                    case 0: DrawRigidbodiesRow(items[i], i); break;
                    case 1: DrawCollidersRow(items[i], i); break;
                    case 2: DrawLayerMatrixRow(items[i], i); break;
                    case 3: DrawMaterialsRow(items[i], i); break;
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawRigidbodiesRow(ScenePhysicsData d, int idx)
        {
            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            var rbColor = d.RigidbodyCount > 500 ? Color.yellow : Color.white;
            USGUIUtilities.DrawColoredLabel("RB:" + d.RigidbodyCount, rbColor, 50);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.ScenePath))
                USGUIUtilities.DrawAssetButton(d.ScenePath, 300f, 18f);
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var rb in d.Rigidbodies.Take(20))
                {
                    var flags = "";
                    if (rb.IsKinematic) flags += " [KINEMATIC]";
                    if (rb.UseGravity) flags += " [GRAVITY]";
                    if (rb.IsTrigger) flags += " [TRIGGER]";
                    if (rb.InterpolationMode != 0) flags += " [INTERP:" + (RigidbodyInterpolation)rb.InterpolationMode + "]";
                    GUILayout.Label("  " + rb.ObjectPath + flags, EditorStyles.miniLabel);
                }
                if (d.Rigidbodies.Count > 20)
                    GUILayout.Label("  ... and " + (d.Rigidbodies.Count - 20) + " more", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawCollidersRow(ScenePhysicsData d, int idx)
        {
            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            GUILayout.Label("Col:" + d.ColliderCount + " Trg:" + d.TriggerCount, EditorStyles.miniLabel, GUILayout.Width(110));
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.ScenePath))
                USGUIUtilities.DrawAssetButton(d.ScenePath, 300f, 18f);
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var complexColliders = d.Colliders.Where(c => c.TriangleCount > 0 || c.HasMovingParent).Take(20);
                foreach (var col in complexColliders)
                {
                    var warn = "";
                    if (col.HasMovingParent) warn += " [MOVING PARENT]";
                    if (col.TriangleCount > 0) warn += " [" + col.TriangleCount + " tris]";
                    GUILayout.Label("  " + col.ObjectPath + " (" + col.ColliderType + ")" + warn, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawLayerMatrixRow(ScenePhysicsData d, int idx)
        {
            var emptyPairs = d.LayerCollisions.Where(p => p.Enabled && (p.ColliderCountA == 0 || p.ColliderCountB == 0)).ToList();
            if (emptyPairs.Count == 0) return;

            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            USGUIUtilities.DrawColoredLabel("Bloat:" + emptyPairs.Count, Color.cyan, 60);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.ScenePath))
                USGUIUtilities.DrawAssetButton(d.ScenePath, 300f, 18f);
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var pair in emptyPairs.Take(15))
                {
                    var nameA = LayerMask.LayerToName(pair.LayerA);
                    var nameB = LayerMask.LayerToName(pair.LayerB);
                    GUILayout.Label("  " + (string.IsNullOrEmpty(nameA) ? "Layer" + pair.LayerA : nameA) +
                        " <-> " + (string.IsNullOrEmpty(nameB) ? "Layer" + pair.LayerB : nameB) +
                        " (col:" + pair.ColliderCountA + " / " + pair.ColliderCountB + ")", EditorStyles.miniLabel);
                }
                if (emptyPairs.Count > 15)
                    GUILayout.Label("  ... and " + (emptyPairs.Count - 15) + " more", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawMaterialsRow(ScenePhysicsData d, int idx)
        {
            var missingMaterial = d.Colliders.Where(c => !c.HasPhysicsMaterial && !c.IsTrigger).ToList();
            if (missingMaterial.Count == 0) return;

            var isExpanded = _expandedRow == idx;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(new GUIContent(isExpanded ? "v" : ">", "Expand/collapse"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedRow = isExpanded ? -1 : idx;
            USGUIUtilities.DrawColoredLabel("NoMat:" + missingMaterial.Count, Color.yellow, 65);
            EditorGUILayout.LabelField(idx.ToString(), GUILayout.Width(30));
            if (!string.IsNullOrEmpty(d.ScenePath))
                USGUIUtilities.DrawAssetButton(d.ScenePath, 300f, 18f);
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var col in missingMaterial.Take(15))
                    GUILayout.Label("  " + col.ObjectPath + " (" + col.ColliderType + ")", EditorStyles.miniLabel);
                if (missingMaterial.Count > 15)
                    GUILayout.Label("  ... and " + (missingMaterial.Count - 15) + " more", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Physics Analysis:");
            foreach (var d in GetFilteredResults())
            {
                sb.AppendLine("Scene: " + d.ScenePath + " | RB:" + d.RigidbodyCount + " Col:" + d.ColliderCount + " Triggers:" + d.TriggerCount);
                foreach (var rb in d.Rigidbodies)
                    sb.AppendLine("  RB: " + rb.ObjectPath + " kinematic:" + rb.IsKinematic + " gravity:" + rb.UseGravity);
                foreach (var col in d.Colliders)
                    sb.AppendLine("  Col: " + col.ObjectPath + " " + col.ColliderType + " trigger:" + col.IsTrigger + " material:" + col.HasPhysicsMaterial);
            }
            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Physics Analysis", Application.dataPath, "physics_analysis.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder();
            sb.AppendLine("ScenePath,RigidbodyCount,ColliderCount,TriggerCount");
            foreach (var d in GetFilteredResults())
                sb.AppendLine(USExportUtilities.EscapeCsvField(d.ScenePath) + "," +
                    d.RigidbodyCount + "," + d.ColliderCount + "," + d.TriggerCount);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private List<ScenePhysicsData> GetFilteredResults()
        {
            EnsureCache();
            return _cachedResults;
        }

        private void EnsureCache()
        {
            var src = _category?.LastResults;
            if (!_cacheDirty && _cachedResults != null && ReferenceEquals(src, _lastSourceResults)) return;
            _lastSourceResults = src;
            var raw = src ?? new List<ScenePhysicsData>();
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
