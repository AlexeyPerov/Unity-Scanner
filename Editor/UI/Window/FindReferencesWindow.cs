using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityScanner.UI.Controls;

namespace UnityScanner.Windows
{
    public class FindReferencesWindow : EditorWindow
    {
        private static class PrefsKeys
        {
            private const string Prefix = "US.FindRefs.";
            public const string ScanForAssetReferences = Prefix + "ScanForAssetReferences";
            public const string DetectAddressables = Prefix + "DetectAddressables";
            public const string ScanTerrainData = Prefix + "ScanTerrainData";
        }

        private static class RefsMapBuilder
        {
            private static readonly Regex GuidRegex = new Regex(
                @"m_AssetGUID:\s*([0-9a-fA-F]{32})",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

            public static void Build(bool scanAssetRefs, bool scanTerrain, bool binarySerialization,
                out Dictionary<string, List<string>> reverseDeps)
            {
                var assetPaths = AssetDatabase.GetAllAssetPaths().ToList();
                reverseDeps = assetPaths.ToDictionary(p => p, _ => new List<string>());

                var total = assetPaths.Count;
                var interval = Math.Max(1, total / 100);

                for (var i = 0; i < total; i++)
                {
                    if (i % interval == 0)
                        EditorUtility.DisplayProgressBar("US Find References",
                            "Building dependency map", (float)i / total);

                    var deps = scanAssetRefs
                        ? GetAllDependencies(assetPaths[i], binarySerialization, false)
                        : AssetDatabase.GetDependencies(assetPaths[i], false);

                    foreach (var dep in deps)
                    {
                        if (reverseDeps.TryGetValue(dep, out var list) && dep != assetPaths[i])
                            list.Add(assetPaths[i]);
                    }
                }

                if (scanTerrain)
                    ScanTerrainDataReferences(reverseDeps);
            }

            private static string[] GetAllDependencies(string assetPath, bool binarySerialization, bool recursive = true)
            {
                var regular = AssetDatabase.GetDependencies(assetPath, recursive);
                if (!CanContainAssetReferences(assetPath))
                    return regular;

                if (binarySerialization)
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (obj == null) return regular;

                    HashSet<string> result = null;
                    var so = new SerializedObject(obj);
                    var it = so.GetIterator();

                    while (it.NextVisible(true))
                    {
                        if (it.propertyType != SerializedPropertyType.Generic ||
                            !it.type.Contains("AssetReference"))
                            continue;

                        var guidProp = it.FindPropertyRelative("m_AssetGUID");
                        if (guidProp == null || string.IsNullOrEmpty(guidProp.stringValue))
                            continue;

                        var refPath = AssetDatabase.GUIDToAssetPath(guidProp.stringValue);
                        if (!string.IsNullOrEmpty(refPath))
                        {
                            result ??= regular.ToHashSet();
                            result.Add(refPath);
                        }
                    }

                    return result != null ? result.ToArray() : regular;
                }

                if (!File.Exists(assetPath))
                    return regular;

                var content = File.ReadAllText(assetPath);
                if (!content.Contains("m_AssetGUID"))
                    return regular;

                HashSet<string> set = null;
                foreach (Match match in GuidRegex.Matches(content))
                {
                    if (match == null || match.Groups.Count <= 1) continue;
                    var guid = match.Groups[1].Value;
                    if (string.IsNullOrEmpty(guid)) continue;

                    var refPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(refPath))
                    {
                        set ??= regular.ToHashSet();
                        set.Add(refPath);
                    }
                }

                return set != null ? set.ToArray() : regular;
            }

            private static bool CanContainAssetReferences(string assetPath)
            {
                var ext = Path.GetExtension(assetPath).ToLowerInvariant();
                return ext == ".asset" || ext == ".prefab" || ext == ".unity";
            }

            private static void ScanTerrainDataReferences(Dictionary<string, List<string>> reverseDeps)
            {
                var terrainGuids = AssetDatabase.FindAssets("t:TerrainData");
                if (terrainGuids.Length == 0) return;

                var guidToPath = new Dictionary<string, string>();
                foreach (var guid in terrainGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                        guidToPath[guid] = path;
                }

                if (guidToPath.Count == 0) return;

                var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".prefab", ".unity" };
                var candidates = AssetDatabase.GetAllAssetPaths()
                    .Where(p => exts.Contains(Path.GetExtension(p)))
                    .ToList();

                foreach (var candidate in candidates)
                {
                    if (!File.Exists(candidate)) continue;

                    string content = null;
                    foreach (var kvp in guidToPath)
                    {
                        content ??= File.ReadAllText(candidate);
                        if (!content.Contains(kvp.Key)) continue;

                        if (reverseDeps.TryGetValue(kvp.Value, out var list) &&
                            !list.Contains(candidate))
                            list.Add(candidate);
                    }
                }
            }
        }

        private static class AddressablesDetector
        {
            private static readonly Dictionary<string, bool> Cache = new Dictionary<string, bool>();
            private static bool _initialized;
            private static bool _available;
            private static bool _warningLogged;
            private static PropertyInfo _settingsProperty;
            private static MethodInfo _findEntryMethod;
            private static int _findEntryParamCount;
            private static readonly object[] OneArg = new object[1];
            private static readonly object[] TwoArgs = new object[2];

            public static void ClearCache() => Cache.Clear();

            public static bool IsAddressable(string assetPath)
            {
                if (string.IsNullOrEmpty(assetPath)) return false;

                try
                {
                    var guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (string.IsNullOrEmpty(guid)) return false;

                    if (Cache.TryGetValue(guid, out var cached)) return cached;

                    EnsureInit();
                    if (!_available) return false;

                    var settings = _settingsProperty.GetValue(null, null);
                    if (settings == null) return false;

                    object entry;
                    if (_findEntryParamCount == 1)
                    {
                        OneArg[0] = guid;
                        entry = _findEntryMethod.Invoke(settings, OneArg);
                        OneArg[0] = null;
                    }
                    else
                    {
                        TwoArgs[0] = guid;
                        TwoArgs[1] = true;
                        entry = _findEntryMethod.Invoke(settings, TwoArgs);
                        TwoArgs[0] = null;
                        TwoArgs[1] = null;
                    }

                    var result = entry != null;
                    Cache[guid] = result;
                    return result;
                }
                catch (Exception e)
                {
                    LogWarning($"checking {assetPath}", e);
                    return false;
                }
            }

            private static void EnsureInit()
            {
                if (_initialized) return;
                _initialized = true;

                try
                {
                    Type defaultObjType = null;
                    Type settingsType = null;

                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        defaultObjType ??= asm.GetType(
                            "UnityEditor.AddressableAssets.Settings.AddressableAssetSettingsDefaultObject", false);
                        defaultObjType ??= asm.GetType(
                            "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject", false);

                        settingsType ??= asm.GetType(
                            "UnityEditor.AddressableAssets.Settings.AddressableAssetSettings", false);
                        settingsType ??= asm.GetType(
                            "UnityEditor.AddressableAssets.AddressableAssetSettings", false);

                        if (defaultObjType != null && settingsType != null) break;
                    }

                    if (defaultObjType == null || settingsType == null) return;

                    _settingsProperty = defaultObjType.GetProperty("Settings",
                        BindingFlags.Public | BindingFlags.Static);
                    _findEntryMethod =
                        settingsType.GetMethod("FindAssetEntry", new[] { typeof(string) }) ??
                        settingsType.GetMethod("FindAssetEntry", new[] { typeof(string), typeof(bool) });
                    _findEntryParamCount = _findEntryMethod?.GetParameters().Length ?? 0;

                    _available = _settingsProperty != null && _findEntryMethod != null;
                }
                catch (Exception e)
                {
                    _available = false;
                    LogWarning("initializing reflection", e);
                }
            }

            private static void LogWarning(string ctx, Exception e)
            {
                if (_warningLogged) return;
                _warningLogged = true;
                UnityEngine.Debug.LogWarning($"[US FindRefs] Addressables reflection failed while {ctx}: {e}");
            }
        }

        private Dictionary<string, List<string>> _cachedReverseDeps;
        private bool _cachedScanAssetRefs;
        private bool _cachedBinarySerialization;

        private Dictionary<UnityEngine.Object, List<string>> _lastResults;
        private UnityEngine.Object[] _selectedObjects;
        private List<string> _selectedAssetPaths = new List<string>();
        private List<string> _missingAssetPaths = new List<string>();
        private bool _hasProjectChangesSinceLastRun;
        private readonly Dictionary<string, bool> _foldoutByPath = new Dictionary<string, bool>();

        private bool _analysisSettingsFoldout;
        private string _searchFilter = string.Empty;
        private ListFilterMode _listFilterMode;
        private ResultsSortMode _resultsSortMode = ResultsSortMode.RefsAsc;
        private DepsSortMode _dependenciesSortMode = DepsSortMode.PathAsc;

        private enum ListFilterMode { All, WithDependenciesOnly, ZeroDependenciesOnly }
        private enum DepsSortMode { PathAsc, PathDesc, TypeAsc, TypeDesc }
        private enum ResultsSortMode { RefsAsc, RefsDesc, PathAsc, PathDesc }

        private class SelectedAssetEntry
        {
            public string SelectedPath;
            public List<string> Dependencies;
            public bool IsAddressable;
            public bool IsInResources;
            public bool HasWarning => IsAddressable || IsInResources;
        }

        private Vector2 _scrollPos = Vector2.zero;

        private bool ScanAssetRefs => EditorPrefs.GetBool(PrefsKeys.ScanForAssetReferences, false);
        private bool DetectAddressables => EditorPrefs.GetBool(PrefsKeys.DetectAddressables, false);
        private bool ScanTerrain => EditorPrefs.GetBool(PrefsKeys.ScanTerrainData, false);

        public static FindReferencesWindow OpenWithSelection()
        {
            var window = GetWindow<FindReferencesWindow>("US Find References");
            window.CaptureSelectionPaths();
            window.RefreshAnalysis();
            return window;
        }

        private void CaptureSelectionPaths()
        {
            _selectedAssetPaths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .ToList();
        }

        private void RefreshAnalysis()
        {
            Show();

            _selectedObjects = ResolveSelectedObjects(out _missingAssetPaths);
            _hasProjectChangesSinceLastRun = false;

            var scanAssetRefs = ScanAssetRefs;
            var binarySerialization = EditorSettings.serializationMode != SerializationMode.ForceText;

            var rebuild = _cachedReverseDeps == null
                          || _cachedScanAssetRefs != scanAssetRefs
                          || _cachedBinarySerialization != binarySerialization;

            if (rebuild)
            {
                var sw = Stopwatch.StartNew();
                RefsMapBuilder.Build(scanAssetRefs, ScanTerrain, binarySerialization,
                    out _cachedReverseDeps);
                _cachedScanAssetRefs = scanAssetRefs;
                _cachedBinarySerialization = binarySerialization;
                sw.Stop();
                UnityEngine.Debug.Log($"[US FindRefs] Map built in {sw.Elapsed.TotalSeconds:F2}s");
            }

            EditorUtility.ClearProgressBar();

            var sw2 = Stopwatch.StartNew();
            _lastResults = LookupReferences(_selectedObjects, _cachedReverseDeps);
            sw2.Stop();

            EditorUtility.DisplayProgressBar("US Find References", "Preparing", 1f);
            EditorUtility.UnloadUnusedAssetsImmediate();
            EditorUtility.ClearProgressBar();

            InitializeFoldouts();

            UnityEngine.Debug.Log($"[US FindRefs] Lookup done in {sw2.Elapsed.TotalSeconds:F2}s");
        }

        private static Dictionary<UnityEngine.Object, List<string>> LookupReferences(
            UnityEngine.Object[] objects, IReadOnlyDictionary<string, List<string>> map)
        {
            var results = new Dictionary<UnityEngine.Object, List<string>>();
            foreach (var obj in objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (map.TryGetValue(path, out var deps))
                    results[obj] = deps;
                else
                    results[obj] = new List<string>();
            }

            return results;
        }

        private UnityEngine.Object[] ResolveSelectedObjects(out List<string> missingPaths)
        {
            missingPaths = new List<string>();
            var resolved = new List<UnityEngine.Object>(_selectedAssetPaths.Count);

            foreach (var path in _selectedAssetPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) { missingPaths.Add(path); continue; }
                resolved.Add(asset);
            }

            return resolved.ToArray();
        }

        private void InitializeFoldouts()
        {
            if (_selectedObjects == null) return;

            var valid = new HashSet<string>();
            for (var i = 0; i < _selectedObjects.Length; i++)
            {
                var path = AssetDatabase.GetAssetPath(_selectedObjects[i]);
                if (string.IsNullOrEmpty(path)) continue;
                valid.Add(path);
                if (!_foldoutByPath.ContainsKey(path))
                    _foldoutByPath[path] = _selectedObjects.Length < 7 || i == 0;
            }

            foreach (var key in _foldoutByPath.Keys.Where(k => !valid.Contains(k)).ToList())
                _foldoutByPath.Remove(key);
        }

        private void Clear()
        {
            _selectedObjects = null;
            _lastResults = null;
            _cachedReverseDeps = null;
            _selectedAssetPaths.Clear();
            _missingAssetPaths.Clear();
            _hasProjectChangesSinceLastRun = false;
            _foldoutByPath.Clear();
            EditorUtility.UnloadUnusedAssetsImmediate();
        }

        private void OnGUI()
        {
            if (_lastResults == null)
            {
                EditorGUILayout.HelpBox(
                    "Select assets in the Project browser, then use [US] Find References In Project to start analysis.",
                    MessageType.Info);
                return;
            }

            if (_selectedObjects == null) { Clear(); return; }

            GUILayout.BeginVertical();
            DrawStateWarnings();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var prev = GUI.color;
            GUI.color = Color.yellow;
            if (GUILayout.Button(new GUIContent("Re-run", "Rebuild references for current selection"), GUILayout.Width(100f)))
                RefreshAnalysis();
            GUI.color = prev;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            DrawAnalysisSettings();
            USGUIUtilities.HorizontalLine();

            var entries = SortEntries(BuildEntries()).ToList();
            DrawHeaderToolbar(entries);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            foreach (var e in entries)
                if (PassesFilters(e)) DrawAssetEntry(e);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawStateWarnings()
        {
            if (_hasProjectChangesSinceLastRun)
                EditorGUILayout.HelpBox("Project changed since last analysis. Press Re-run to refresh.",
                    MessageType.Warning);
            if (_missingAssetPaths.Count > 0)
                EditorGUILayout.HelpBox(
                    $"{_missingAssetPaths.Count} selected asset(s) are missing or were removed.",
                    MessageType.Warning);
        }

        private void DrawAnalysisSettings()
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
                EditorGUILayout.HelpBox(
                    "Set EditorSettings.serializationMode to ForceText for more reliable scanning.",
                    MessageType.Error);

            _analysisSettingsFoldout = EditorGUILayout.Foldout(_analysisSettingsFoldout,
                new GUIContent("Analysis Settings", "Changes apply on next run."));
            if (!_analysisSettingsFoldout) return;

            USGUIUtilities.HorizontalLine();

            var scanRefs = EditorGUILayout.ToggleLeft("Scan Addressables AssetReferences", ScanAssetRefs);
            if (scanRefs != ScanAssetRefs)
                EditorPrefs.SetBool(PrefsKeys.ScanForAssetReferences, scanRefs);

            var detectAddr = EditorGUILayout.ToggleLeft("Detect Addressables", DetectAddressables);
            if (detectAddr != DetectAddressables)
                EditorPrefs.SetBool(PrefsKeys.DetectAddressables, detectAddr);

            var scanTerrain = EditorGUILayout.ToggleLeft("Scan Terrain References", ScanTerrain);
            if (scanTerrain != ScanTerrain)
                EditorPrefs.SetBool(PrefsKeys.ScanTerrainData, scanTerrain);

            EditorGUILayout.HelpBox("Settings are saved immediately. Press Re-run to apply.", MessageType.Info);
        }

        private void DrawHeaderToolbar(List<SelectedAssetEntry> entries)
        {
            var withDeps = entries.Count(e => e.Dependencies.Count > 0);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Selected: {entries.Count}");
            GUILayout.Space(6f);
            GUILayout.Label($"With dependencies: {withDeps}");
            GUILayout.Space(6f);
            GUILayout.Label($"Zero dependencies: {entries.Count - withDeps}");
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(50f));
            _listFilterMode = (ListFilterMode)EditorGUILayout.EnumPopup(_listFilterMode, GUILayout.Width(140f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(5f);
            EditorGUILayout.LabelField("Search:", GUILayout.Width(55f));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, GUILayout.Width(150f));
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Sort deps:", GUILayout.Width(65f));
            _dependenciesSortMode = (DepsSortMode)EditorGUILayout.EnumPopup(_dependenciesSortMode, GUILayout.Width(100f));
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Sort results:", GUILayout.Width(75f));
            _resultsSortMode = (ResultsSortMode)EditorGUILayout.EnumPopup(_resultsSortMode, GUILayout.Width(90f));
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!_foldoutByPath.Values.Any(x => !x)))
                if (GUILayout.Button(new GUIContent("Expand All", "Expand all entries"), GUILayout.Width(80f))) SetAllFoldouts(true);
            using (new EditorGUI.DisabledScope(!_foldoutByPath.Values.Any(x => x)))
                if (GUILayout.Button(new GUIContent("Collapse All", "Collapse all entries"), GUILayout.Width(80f))) SetAllFoldouts(false);

            EditorGUILayout.EndHorizontal();
        }

        private List<SelectedAssetEntry> BuildEntries()
        {
            var detectAddr = DetectAddressables;
            var entries = new List<SelectedAssetEntry>(_selectedObjects.Length);

            foreach (var obj in _selectedObjects)
            {
                if (obj == null) continue;
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (!_lastResults.TryGetValue(obj, out var deps))
                    deps = new List<string>();

                var pathNorm = path.Replace("\\", "/");
                entries.Add(new SelectedAssetEntry
                {
                    SelectedPath = path,
                    Dependencies = deps,
                    IsAddressable = detectAddr && AddressablesDetector.IsAddressable(path),
                    IsInResources = pathNorm.Contains("/Resources/")
                });
            }

            return entries;
        }

        private IEnumerable<SelectedAssetEntry> SortEntries(IEnumerable<SelectedAssetEntry> entries)
        {
            return _resultsSortMode switch
            {
                ResultsSortMode.RefsDesc => entries.OrderByDescending(e => e.Dependencies.Count)
                    .ThenBy(e => e.SelectedPath, StringComparer.Ordinal),
                ResultsSortMode.PathAsc => entries.OrderBy(e => e.SelectedPath, StringComparer.Ordinal),
                ResultsSortMode.PathDesc => entries.OrderByDescending(e => e.SelectedPath, StringComparer.Ordinal),
                _ => entries.OrderBy(e => e.Dependencies.Count)
                    .ThenBy(e => e.SelectedPath, StringComparer.Ordinal)
            };
        }

        private bool PassesFilters(SelectedAssetEntry entry)
        {
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                var match = entry.SelectedPath.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!match)
                    match = entry.Dependencies.Any(d =>
                        d.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!match) return false;
            }

            return _listFilterMode switch
            {
                ListFilterMode.WithDependenciesOnly => entry.Dependencies.Count > 0,
                ListFilterMode.ZeroDependenciesOnly => entry.Dependencies.Count == 0,
                _ => true
            };
        }

        private void DrawAssetEntry(SelectedAssetEntry entry)
        {
            USGUIUtilities.HorizontalLine();

            if (!_foldoutByPath.TryGetValue(entry.SelectedPath, out var foldout))
            {
                foldout = false;
                _foldoutByPath[entry.SelectedPath] = false;
            }

            EditorGUILayout.BeginHorizontal();

            var prevColor = GUI.color;
            if (entry.Dependencies.Count == 0)
                GUI.color = Color.yellow;

            var name = Path.GetFileNameWithoutExtension(entry.SelectedPath);
            var header = $"{name} is used by [{entry.Dependencies.Count}] " +
                         (entry.Dependencies.Count == 1 ? "asset" : "assets");

            if (entry.Dependencies.Count == 0)
            {
                GUILayout.Space(14f);
                EditorGUILayout.LabelField(header);
            }
            else
            {
                foldout = EditorGUILayout.Foldout(foldout, header, true);
            }

            _foldoutByPath[entry.SelectedPath] = foldout;
            GUILayout.FlexibleSpace();

            GUI.color = entry.IsAddressable ? Color.cyan : prevColor;
            if (entry.IsAddressable) GUILayout.Label("[Addressable]");
            GUI.color = entry.IsInResources ? Color.cyan : prevColor;
            if (entry.IsInResources) GUILayout.Label("[Resources]");
            GUI.color = prevColor;

            USGUIUtilities.DrawAssetButtonWithFixedWidth(entry.SelectedPath, 300f, 18f);
            EditorGUILayout.EndHorizontal();

            if (!foldout || entry.Dependencies.Count == 0) return;

            GUILayout.Space(5f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            if (GUILayout.Button(new GUIContent("Export to Clipboard", "Copy dependency paths to clipboard"), GUILayout.Width(120)))
            {
                EditorGUIUtility.systemCopyBuffer = string.Join(Environment.NewLine, entry.Dependencies);
                UnityEngine.Debug.Log($"[US FindRefs] Copied {entry.Dependencies.Count} deps for {entry.SelectedPath}");
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            foreach (var depPath in SortDeps(entry.Dependencies))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                USGUIUtilities.DrawAssetButtonWithFixedWidth(depPath, 300f, 18f);
                GUILayout.Space(10f);
                GUILayout.Label(depPath);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private IEnumerable<string> SortDeps(IEnumerable<string> deps)
        {
            return _dependenciesSortMode switch
            {
                DepsSortMode.PathDesc => deps.OrderByDescending(x => x, StringComparer.Ordinal),
                DepsSortMode.TypeAsc => deps.OrderBy(GetTypeName).ThenBy(x => x, StringComparer.Ordinal),
                DepsSortMode.TypeDesc => deps.OrderByDescending(GetTypeName).ThenBy(x => x, StringComparer.Ordinal),
                _ => deps.OrderBy(x => x, StringComparer.Ordinal)
            };
        }

        private static string GetTypeName(string path)
        {
            var t = AssetDatabase.GetMainAssetTypeAtPath(path);
            return t != null ? t.Name : "Unknown";
        }

        private void SetAllFoldouts(bool value)
        {
            foreach (var key in _foldoutByPath.Keys.ToList())
                _foldoutByPath[key] = value;
        }

        private void OnDestroy() => Clear();
    }
}
