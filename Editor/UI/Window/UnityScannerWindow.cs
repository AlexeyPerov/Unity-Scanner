using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityScanner.Categories.Addressables;
using UnityScanner.Categories.AnimationAnalysis;
using UnityScanner.Categories.AudioAnalysis;
using UnityScanner.Categories.BuildPlatformReadiness;
using UnityScanner.Categories.Dependencies;
using UnityScanner.Categories.FontTextAnalysis;
using UnityScanner.Categories.Materials;
using UnityScanner.Categories.MissingReferences;
using UnityScanner.Categories.ParticleSystemAnalysis;
using UnityScanner.Categories.RegressionTrend;
using UnityScanner.Categories.ScenePrefabHealth;
using UnityScanner.Categories.ShaderAnalysis;
using UnityScanner.Categories.UICanvasAnalysis;
using UnityScanner.Categories.LightingAnalysis;
using UnityScanner.Categories.LODAnalysis;
using UnityScanner.Categories.PhysicsAnalysis;
using UnityScanner.Categories.AsmDefAudit;
using UnityScanner.Categories.Sprite2DAnalysis;
using UnityScanner.Categories.ProjectHealth;
using UnityScanner.Categories.TerrainAnalysis;
using UnityScanner.Categories.Textures;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Export;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Progress;
using UnityScanner.Core.Results;
using UnityScanner.Core.Settings;
using UnityScanner.Caching;
using UnityScanner.UI.Controls;

namespace UnityScanner.UI.Window
{
    public enum TabState
    {
        NotRun,
        Running,
        DoneNoIssues,
        DoneHasIssues,
        Failed,
        Skipped
    }

    public enum MainTab
    {
        Setup,
        Dependencies,
        MissingReferences,
        Materials,
        Textures,
        ShaderAnalysis,
        TerrainAnalysis,
        FontTextAnalysis,
        AudioAnalysis,
        AnimationAnalysis,
        ScenePrefabHealth,
        BuildPlatformReadiness,
        ParticleAnalysis,
        UICanvasAnalysis,
        LightingAnalysis,
        LODAnalysis,
        PhysicsAnalysis,
        AsmDefAudit,
        Sprite2DAnalysis,
        ProjectHealth,
        Addressables,
        RegressionTrend,
        Summary,
        Settings,
        Help
    }

    public class UnityScannerWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Unity Scanner";
        private const string BuildLayoutFolderPref = "US_BuildLayoutFolder";
        private const string PlatformProfilePref = "US_PlatformProfile";
        private const string EnabledCategoriesPref = "US_EnabledCategories";

        [MenuItem(MenuPath)]
        public static void Launch()
        {
            GetWindow<UnityScannerWindow>("Unity Scanner");
        }

        private UnityScannerCategoryRegistry _registry;
        private UnityScannerOrchestrator _orchestrator;
        private UnityScannerSettings _settings;

        private readonly Dictionary<string, IUnityScannerTabDrawer> _tabDrawers = new();
        private readonly Dictionary<string, TabState> _tabStates = new();
        private readonly Dictionary<string, UnityScannerResult> _tabResults = new();

        private MainTab _currentTab = MainTab.Setup;
        private Vector2 _mainScroll;

        private UnityScannerAggregateResult _lastAggregate;
        private UnityScannerProgressInfo _currentProgress;
        private bool _isRunning;
        private int _runningCategoryIndex;
        private int _runningCategoryTotal;

        private string _buildLayoutPathDisplay = "";
        private string _buildLayoutFolderCache = "Library";
        private Vector2 _settingsScroll;
        private Vector2 _summaryScroll;
        private Vector2 _helpScroll;
        private bool _categoryInfoFoldout;

        private int _cachedAssetCount = -1;
        private int _cachedEnabledCount = -1;
        private int _cachedTotalIssues = -1;

        private static readonly GUIContent BrowseButtonContent = new GUIContent("Browse...", "Browse for a BuildLayout.txt file");
        private static readonly GUIContent ClearButtonContent = new GUIContent("Clear", "Clear the build layout path");
        private static readonly GUIContent SelectAllContent = new GUIContent("Select All", "Select all categories for scanning");
        private static readonly GUIContent DeselectAllContent = new GUIContent("Deselect All", "Deselect all categories");
        private static readonly GUIContent ViewButtonContent = new GUIContent("View", "Open this category tab");
        private static readonly GUIContent RunAllContent = new GUIContent("Run All Selected", "Run scan for all selected categories");
        private static readonly GUIContent RunFailedContent = new GUIContent("Run Failed", "Re-run only categories that failed last time");
        private static readonly GUIContent RunSkippedContent = new GUIContent("Run Skipped", "Re-run only categories that were skipped last time");
        private static readonly GUIContent CancelScanContent = new GUIContent("Cancel", "Cancel the running scan");
        private static readonly GUIContent ViewSummaryContent = new GUIContent("View Summary", "Open the Summary tab");
        private static readonly GUIContent PrevTabContent = new GUIContent("<", "Previous tab");
        private static readonly GUIContent NextTabContent = new GUIContent(">", "Next tab");

        private static readonly Color DoneOkColor = new Color(0.6f, 0.9f, 0.6f);
        private static readonly Color FailedStateColor = new Color(1f, 0.5f, 0.5f);
        private static readonly Color VerboseColor = new Color(0.7f, 0.7f, 0.7f);

        private readonly (string name, MainTab tab, string description)[] _allTabs =
        {
            ("Setup", MainTab.Setup, ""),
            ("---", MainTab.Setup, ""),
            ("Dependencies", MainTab.Dependencies, "Find unreferenced assets in your project."),
            ("Missing Refs", MainTab.MissingReferences, "Detect missing scripts, broken GUID references, invalid layers."),
            ("Textures Compression", MainTab.Textures, "Check texture compression, dimensions, atlas usage, duplicates."),
            ("Sprites Packing", MainTab.Sprite2DAnalysis, "Detect low atlas packing efficiency, unpacked sprites, polygon vertex excess, uneven sprite sheet cells, full-rect waste, duplicate sprite content."),
            ("Shaders", MainTab.ShaderAnalysis, "Detect shader variant explosion, error/fallback shaders, expensive keywords, duplicate keyword profiles."),
            ("Materials and Renderers", MainTab.Materials, "Analyze materials for shader issues, null textures, batch compatibility."),
            ("Font and Text", MainTab.FontTextAnalysis, "Analyze TMP/Unity fonts for atlas sizes, fallback chains, dynamic growth risk."),
            ("Particles", MainTab.ParticleAnalysis, "Detect excessive emission rates, collision overhead, trail overdraw, sub-emitter chains, missing LOD, texture oversize."),
            ("UI Canvas", MainTab.UICanvasAnalysis, "Detect unused shader channels, nested redundancy, unnecessary raycast targets, Text/TMP mix, layout nesting, vertex count, atlas waste."),
            ("Scenes and Prefabs", MainTab.ScenePrefabHealth, "Detect deep nesting, override explosion, hierarchy hotspots, broken references, inactive anti-patterns."),
            ("Audio", MainTab.AudioAnalysis, "Check import settings, oversized clips, duplicates, missing mixer groups."),
            ("Animation", MainTab.AnimationAnalysis, "Check animator controllers for unreachable states, missing clips, complexity, duplicate animation clips."),
            ("Physics", MainTab.PhysicsAnalysis, "Detect excessive rigidbodies, static colliders on moving parents, non-kinematic triggers, unnecessary interpolation, complex mesh colliders, missing physics materials, layer matrix bloat."),
            ("Terrain", MainTab.TerrainAnalysis, "Check terrain layers, control map budgets, tree/detail density, expensive settings."),
            ("Lighting", MainTab.LightingAnalysis, "Detect realtime light budget, shadow overhead on mobile, lightmap sizing, mode inconsistency, missing probes, emissive GI, pipeline mismatches."),
            ("LOD", MainTab.LODAnalysis, "Detect missing LOD levels, null renderers, renderer count mismatch, complex last LOD, material mismatch across levels, close transitions, missing cross-fade, unnecessary LOD groups."),
            ("AsmDef", MainTab.AsmDefAudit, "Detect circular references, editor references in runtime assemblies, orphan auto-referenced assemblies, platform filter issues, duplicate assembly names, invalid version defines."),
            ("Project Health", MainTab.ProjectHealth, "Detect empty folders, orphaned .meta files, broken/corrupted assets, empty scenes, excessive folder nesting, and oversized directories."),
            ("Platform and Build", MainTab.BuildPlatformReadiness, "Check import policies, platform compatibility, startup budgets, stripping risks, profile conformance."),
            ("Build Layout", MainTab.Addressables, "Analyze BuildLayout.txt for bundle dependency issues, duplicates, and comparison."),
            ("---", MainTab.Setup, ""),
            ("Trends", MainTab.RegressionTrend, "Track scan results over time, detect regressions between runs, compare issue counts across categories."),
            ("Summary", MainTab.Summary, ""),
            ("Settings", MainTab.Settings, ""),
            ("Help", MainTab.Help, "")
        };

        private Dictionary<MainTab, (string name, string description)> _tabInfoMap;

        private void BuildTabInfoMap()
        {
            _tabInfoMap = new Dictionary<MainTab, (string name, string description)>();
            foreach (var (name, tab, description) in _allTabs)
            {
                if (name == "---") continue;
                if (!_tabInfoMap.ContainsKey(tab))
                    _tabInfoMap[tab] = (name, description);
            }
        }

        private string GetTabName(MainTab tab)
        {
            if (_tabInfoMap != null && _tabInfoMap.TryGetValue(tab, out var info))
                return info.name;
            return tab.ToString();
        }

        private string GetTabDescription(MainTab tab)
        {
            if (_tabInfoMap != null && _tabInfoMap.TryGetValue(tab, out var info))
                return info.description;
            return "";
        }

        private readonly MainTab[] _categoryTabs =
        {
            MainTab.Dependencies,
            MainTab.MissingReferences,
            MainTab.Materials,
            MainTab.Textures,
            MainTab.ShaderAnalysis,
            MainTab.TerrainAnalysis,
            MainTab.FontTextAnalysis,
            MainTab.AudioAnalysis,
            MainTab.AnimationAnalysis,
            MainTab.ScenePrefabHealth,
            MainTab.BuildPlatformReadiness,
            MainTab.ParticleAnalysis,
            MainTab.UICanvasAnalysis,
            MainTab.LightingAnalysis,
            MainTab.LODAnalysis,
            MainTab.PhysicsAnalysis,
            MainTab.AsmDefAudit,
            MainTab.ProjectHealth,
            MainTab.Sprite2DAnalysis,
            MainTab.Addressables,
            MainTab.RegressionTrend
        };

        private readonly USPaginationSettings _summaryPagination = new() { PageToShow = 0, PageSize = 20 };
        private int _summarySeverityFilter;
        private readonly HashSet<string> _summarySelectedCategories = new();
        private Vector2 _summaryCatScroll;
        private string _summaryPathFilter = "";
        private string _summaryTextFilter = "";
        private List<UnityScannerIssue> _filteredSummaryIssues;

        private void OnEnable()
        {
            Initialize();
        }

        private void Initialize()
        {
            _settings = ScriptableObject.CreateInstance<UnityScannerSettings>();
            _registry = new UnityScannerCategoryRegistry();
            _orchestrator = new UnityScannerOrchestrator(_registry);
            _orchestrator.OnProgress += progress => { _currentProgress = progress; };
            _orchestrator.OnCategoryStarted += (index, total, name) =>
            {
                _runningCategoryIndex = index;
                _runningCategoryTotal = total;
            };

            RegisterCategories();
            LoadEnabledCategories();
            BuildTabDrawers();
            BuildTabInfoMap();
            InitTabStates();

            _buildLayoutFolderCache = EditorPrefs.GetString(BuildLayoutFolderPref, "Library");

            _cachedAssetCount = -1;
            _cachedEnabledCount = -1;
            _cachedTotalIssues = -1;

            var savedProfile = EditorPrefs.GetString(PlatformProfilePref, PlatformProfilePresets.Mobile);
            _settings.SetPlatformProfile(savedProfile);
        }

        private void RegisterCategories()
        {
            var deps = new DependenciesCategory();
            _registry.RegisterCategory(deps);

            var missingRefs = new MissingReferencesCategory();
            _registry.RegisterCategory(missingRefs);

            var materials = new MaterialsCategory();
            _registry.RegisterCategory(materials);

            var textures = new TexturesCategory();
            _registry.RegisterCategory(textures);

            var shaderAnalysis = new ShaderAnalysisCategory();
            _registry.RegisterCategory(shaderAnalysis);

            var terrainAnalysis = new TerrainAnalysisCategory();
            _registry.RegisterCategory(terrainAnalysis);

            var fontTextAnalysis = new FontTextAnalysisCategory();
            _registry.RegisterCategory(fontTextAnalysis);

            var audioAnalysis = new AudioAnalysisCategory();
            _registry.RegisterCategory(audioAnalysis);

            var animationAnalysis = new AnimationAnalysisCategory();
            _registry.RegisterCategory(animationAnalysis);

            var scenePrefabHealth = new ScenePrefabHealthCategory();
            _registry.RegisterCategory(scenePrefabHealth);

            var buildPlatformReadiness = new BuildPlatformReadinessCategory();
            _registry.RegisterCategory(buildPlatformReadiness);

            var particleAnalysis = new ParticleSystemAnalysisCategory();
            _registry.RegisterCategory(particleAnalysis);

            var uiCanvasAnalysis = new UICanvasAnalysisCategory();
            _registry.RegisterCategory(uiCanvasAnalysis);

            var lightingAnalysis = new LightingAnalysisCategory();
            _registry.RegisterCategory(lightingAnalysis);

            var lodAnalysis = new LODAnalysisCategory();
            _registry.RegisterCategory(lodAnalysis);

            var physicsAnalysis = new PhysicsAnalysisCategory();
            _registry.RegisterCategory(physicsAnalysis);

            var asmDefAudit = new AsmDefAuditCategory();
            _registry.RegisterCategory(asmDefAudit);

            var projectHealth = new ProjectHealthCategory();
            _registry.RegisterCategory(projectHealth);

            var sprite2D = new Sprite2DAnalysisCategory();
            _registry.RegisterCategory(sprite2D);

            var addressables = new AddressablesCategory();
            _registry.RegisterCategory(addressables);

            var regressionTrend = new RegressionTrendCategory();
            _registry.RegisterCategory(regressionTrend);

            _registry.RegisterFixProvider("dependencies", new DependenciesFixProvider());
            _registry.RegisterFixProvider("materials", new MaterialsFixProvider());
        }

        private void BuildTabDrawers()
        {
            var dependenciesDrawer = new DependenciesTabDrawer();
            dependenciesDrawer.Bind(_registry.GetCategory("dependencies") as DependenciesCategory);
            dependenciesDrawer.OnScanRequested = () => StartScanSingle("dependencies");
            _tabDrawers["dependencies"] = dependenciesDrawer;

            var missingRefsDrawer = new MissingReferencesTabDrawer();
            missingRefsDrawer.Bind(_registry.GetCategory("missing_references") as MissingReferencesCategory);
            missingRefsDrawer.OnScanRequested = () => StartScanSingle("missing_references");
            _tabDrawers["missing_references"] = missingRefsDrawer;

            var materialsDrawer = new MaterialsTabDrawer();
            materialsDrawer.Bind(_registry.GetCategory("materials") as MaterialsCategory);
            materialsDrawer.OnScanRequested = () => StartScanSingle("materials");
            _tabDrawers["materials"] = materialsDrawer;

            var texturesDrawer = new TexturesTabDrawer();
            texturesDrawer.Bind(_registry.GetCategory("textures") as TexturesCategory);
            texturesDrawer.OnScanRequested = () => StartScanSingle("textures");
            _tabDrawers["textures"] = texturesDrawer;

            var shaderDrawer = new ShaderAnalysisTabDrawer();
            shaderDrawer.Bind(_registry.GetCategory("shader_analysis") as ShaderAnalysisCategory);
            shaderDrawer.OnScanRequested = () => StartScanSingle("shader_analysis");
            _tabDrawers["shader_analysis"] = shaderDrawer;

            var terrainDrawer = new TerrainAnalysisTabDrawer();
            terrainDrawer.Bind(_registry.GetCategory("terrain_analysis") as TerrainAnalysisCategory);
            terrainDrawer.OnScanRequested = () => StartScanSingle("terrain_analysis");
            _tabDrawers["terrain_analysis"] = terrainDrawer;

            var fontTextDrawer = new FontTextAnalysisTabDrawer();
            fontTextDrawer.Bind(_registry.GetCategory("font_text_analysis") as FontTextAnalysisCategory);
            fontTextDrawer.OnScanRequested = () => StartScanSingle("font_text_analysis");
            _tabDrawers["font_text_analysis"] = fontTextDrawer;

            var audioDrawer = new AudioAnalysisTabDrawer();
            audioDrawer.Bind(_registry.GetCategory("audio_analysis") as AudioAnalysisCategory);
            audioDrawer.OnScanRequested = () => StartScanSingle("audio_analysis");
            _tabDrawers["audio_analysis"] = audioDrawer;

            var animationDrawer = new AnimationAnalysisTabDrawer();
            animationDrawer.Bind(_registry.GetCategory("animation_analysis") as AnimationAnalysisCategory);
            animationDrawer.OnScanRequested = () => StartScanSingle("animation_analysis");
            _tabDrawers["animation_analysis"] = animationDrawer;

            var scenePrefabDrawer = new ScenePrefabHealthTabDrawer();
            scenePrefabDrawer.Bind(_registry.GetCategory("scene_prefab_health") as ScenePrefabHealthCategory);
            scenePrefabDrawer.OnScanRequested = () => StartScanSingle("scene_prefab_health");
            _tabDrawers["scene_prefab_health"] = scenePrefabDrawer;

            var buildPlatformDrawer = new BuildPlatformReadinessTabDrawer();
            buildPlatformDrawer.Bind(_registry.GetCategory("build_platform_readiness") as BuildPlatformReadinessCategory);
            buildPlatformDrawer.OnScanRequested = () => StartScanSingle("build_platform_readiness");
            _tabDrawers["build_platform_readiness"] = buildPlatformDrawer;

            var particleDrawer = new ParticleSystemAnalysisTabDrawer();
            particleDrawer.Bind(_registry.GetCategory("particle_analysis") as ParticleSystemAnalysisCategory);
            particleDrawer.OnScanRequested = () => StartScanSingle("particle_analysis");
            _tabDrawers["particle_analysis"] = particleDrawer;

            var uiCanvasDrawer = new UICanvasAnalysisTabDrawer();
            uiCanvasDrawer.Bind(_registry.GetCategory("ui_canvas_analysis") as UICanvasAnalysisCategory);
            uiCanvasDrawer.OnScanRequested = () => StartScanSingle("ui_canvas_analysis");
            _tabDrawers["ui_canvas_analysis"] = uiCanvasDrawer;

            var lightingDrawer = new LightingAnalysisTabDrawer();
            lightingDrawer.Bind(_registry.GetCategory("lighting_analysis") as LightingAnalysisCategory);
            lightingDrawer.OnScanRequested = () => StartScanSingle("lighting_analysis");
            _tabDrawers["lighting_analysis"] = lightingDrawer;

            var lodDrawer = new LODAnalysisTabDrawer();
            lodDrawer.Bind(_registry.GetCategory("lod_analysis") as LODAnalysisCategory);
            lodDrawer.OnScanRequested = () => StartScanSingle("lod_analysis");
            _tabDrawers["lod_analysis"] = lodDrawer;

            var physicsDrawer = new PhysicsAnalysisTabDrawer();
            physicsDrawer.Bind(_registry.GetCategory("physics_analysis") as PhysicsAnalysisCategory);
            physicsDrawer.OnScanRequested = () => StartScanSingle("physics_analysis");
            _tabDrawers["physics_analysis"] = physicsDrawer;

            var asmDefDrawer = new AsmDefAuditTabDrawer();
            asmDefDrawer.Bind(_registry.GetCategory("asmdef_audit") as AsmDefAuditCategory);
            asmDefDrawer.OnScanRequested = () => StartScanSingle("asmdef_audit");
            _tabDrawers["asmdef_audit"] = asmDefDrawer;

            var projectHealthDrawer = new ProjectHealthTabDrawer();
            projectHealthDrawer.Bind(_registry.GetCategory("project_health") as ProjectHealthCategory);
            projectHealthDrawer.OnScanRequested = () => StartScanSingle("project_health");
            _tabDrawers["project_health"] = projectHealthDrawer;

            var sprite2DDrawer = new Sprite2DAnalysisTabDrawer();
            sprite2DDrawer.Bind(_registry.GetCategory("sprite_2d_analysis") as Sprite2DAnalysisCategory);
            sprite2DDrawer.OnScanRequested = () => StartScanSingle("sprite_2d_analysis");
            _tabDrawers["sprite_2d_analysis"] = sprite2DDrawer;

            var addressablesDrawer = new AddressablesTabDrawer();
            addressablesDrawer.Bind(_registry.GetCategory("addressables") as AddressablesCategory);
            _tabDrawers["addressables"] = addressablesDrawer;

             var regressionTrendDrawer = new RegressionTrendTabDrawer();
             regressionTrendDrawer.Bind(_registry.GetCategory("regression_trend") as RegressionTrendCategory);
             regressionTrendDrawer.OnScanRequested = () => StartScanSingle("regression_trend");
             regressionTrendDrawer.HasResultsToCompare = () => _tabResults.Any(kv => kv.Key != "regression_trend");
             regressionTrendDrawer.GetPlatformProfile = () => _settings?.ActivePlatformProfileId ?? "";
             _tabDrawers["regression_trend"] = regressionTrendDrawer;
        }

        private void InitTabStates()
        {
            foreach (var cat in _registry.Categories)
                _tabStates[cat.Id] = TabState.NotRun;
        }

        private void LoadEnabledCategories()
        {
            var saved = EditorPrefs.GetString(EnabledCategoriesPref, "");
            if (string.IsNullOrEmpty(saved)) return;

            var enabledSet = new HashSet<string>(saved.Split(','), StringComparer.OrdinalIgnoreCase);
            foreach (var cat in _registry.Categories)
                cat.Settings.Enabled = enabledSet.Contains(cat.Id);
        }

        private void SaveEnabledCategories()
        {
            _cachedEnabledCount = -1;
            var enabled = _registry.Categories
                .Where(c => c.Settings.Enabled)
                .Select(c => c.Id);
            EditorPrefs.SetString(EnabledCategoriesPref, string.Join(",", enabled));
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawStatusBar();
            DrawContent();
        }

        #region Toolbar

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var currentDisplayName = GetCurrentTabDisplayName();
            var stateColor = GetTabStateColor(_currentTab);
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = stateColor;
            if (GUILayout.Button(new GUIContent(currentDisplayName, "Click to select tab"), EditorStyles.toolbarDropDown, GUILayout.Width(160)))
            {
                var menu = new GenericMenu();
                foreach (var (name, tab, _) in _allTabs)
                {
                    if (name == "---")
                    {
                        menu.AddSeparator("");
                        continue;
                    }
                    var badge = GetTabBadge(tab);
                    var label = string.IsNullOrEmpty(badge) ? name : $"{name} {badge}";
                    var isCurrent = _currentTab == tab;
                    menu.AddItem(new GUIContent(label), isCurrent, () =>
                    {
                        _currentTab = tab;
                        Repaint();
                    });
                }
                menu.ShowAsContext();
            }
            GUI.backgroundColor = prevBg;

            GUILayout.Space(4);

            var currentIdx = FindTabIndex(_currentTab);
            EditorGUI.BeginDisabledGroup(currentIdx < 0);
            if (GUILayout.Button(PrevTabContent, EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                var prevIdx = currentIdx;
                do { prevIdx = (prevIdx - 1 + _allTabs.Length) % _allTabs.Length; }
                while (_allTabs[prevIdx].name == "---");
                _currentTab = _allTabs[prevIdx].tab;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(currentIdx < 0);
            if (GUILayout.Button(NextTabContent, EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                var nextIdx = currentIdx;
                do { nextIdx = (nextIdx + 1) % _allTabs.Length; }
                while (_allTabs[nextIdx].name == "---");
                _currentTab = _allTabs[nextIdx].tab;
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            var categoryId = TabToCategoryId(_currentTab);
            if (categoryId != null && !_isRunning)
            {
                var stateText = GetTabStateText(_currentTab);
                if (!string.IsNullOrEmpty(stateText))
                {
                    var stateLabelColor = GetTabStateColor(_currentTab);
                    GUI.color = stateLabelColor;
                    GUILayout.Label(stateText, EditorStyles.miniLabel, GUILayout.Width(100));
                    GUI.color = Color.white;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private string GetCurrentTabDisplayName()
        {
            return GetTabName(_currentTab);
        }

        private int FindTabIndex(MainTab tab)
        {
            for (var i = 0; i < _allTabs.Length; i++)
                if (_allTabs[i].tab == tab && _allTabs[i].name != "---") return i;
            return -1;
        }

        private string GetTabStateText(MainTab tab)
        {
            var categoryId = TabToCategoryId(tab);
            if (categoryId == null) return "";
            if (!_tabStates.TryGetValue(categoryId, out var state)) return "";
            return state switch
            {
                TabState.Running => "Running...",
                TabState.DoneNoIssues => "Done - OK",
                TabState.DoneHasIssues => "Done - Issues",
                TabState.Failed => "Failed",
                TabState.Skipped => "Skipped",
                _ => ""
            };
        }

        private Color GetTabStateColor(MainTab tab)
        {
            var categoryId = TabToCategoryId(tab);
            if (categoryId == null) return Color.white;

            if (!_tabStates.TryGetValue(categoryId, out var state))
                return Color.white;

            return state switch
            {
                TabState.Running => Color.cyan,
                TabState.DoneNoIssues => DoneOkColor,
                TabState.DoneHasIssues => Color.yellow,
                TabState.Failed => FailedStateColor,
                TabState.Skipped => Color.gray,
                _ => Color.white
            };
        }

        private string GetTabBadge(MainTab tab)
        {
            var categoryId = TabToCategoryId(tab);
            if (categoryId == null) return "";

            if (!_tabStates.TryGetValue(categoryId, out var state))
                return "";

            switch (state)
            {
                case TabState.Running:
                    return "[...]";
                case TabState.DoneHasIssues:
                    if (_tabResults.TryGetValue(categoryId, out var result))
                        return $"[{result.Issues.Count}]";
                    return "[!]";
                case TabState.Failed:
                    return "[X]";
                case TabState.Skipped:
                    return "[-]";
                default:
                    return "";
            }
        }

        #endregion

        #region Status Bar

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            if (_isRunning)
            {
                var categoryLabel = _runningCategoryTotal > 1
                    ? $"({_runningCategoryIndex}/{_runningCategoryTotal}) "
                    : "";

                if (_currentProgress != null)
                {
                    var barRect = GUILayoutUtility.GetRect(200, 18, GUILayout.Width(200));
                    EditorGUI.ProgressBar(barRect, _currentProgress.Progress,
                        $"{categoryLabel}{_currentProgress.Message ?? "Scanning..."}");
                }
                else
                {
                    GUILayout.Label($"{categoryLabel}Scanning...", EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(CancelScanContent, EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    _orchestrator.Cancel();
                }
            }
            else
            {
                var categoryId = TabToCategoryId(_currentTab);

                if (categoryId == null || !_tabResults.ContainsKey(categoryId))
                {
                    GUILayout.Label("Run Scanning in 'Setup' section", EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                if (categoryId != null && categoryId != "regression_trend" && !_isRunning)
                {
                    var category = _registry.GetCategory(categoryId);
                    if (category != null)
                    {
                        var hasResult = _tabResults.ContainsKey(categoryId);
                        var btnLabel = hasResult ? $"Refresh {category.DisplayName}" : $"Scan {category.DisplayName}";
                        if (GUILayout.Button(new GUIContent(btnLabel, hasResult ? "Re-run this category scan" : "Run this category scan"),
                            GUILayout.Width(200), GUILayout.Height(22)))
                            StartScanSingle(categoryId);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Content

        private void DrawContent()
        {
            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

            switch (_currentTab)
            {
                case MainTab.Setup: DrawSetupTab(); break;
                case MainTab.Summary: DrawSummaryTab(); break;
                case MainTab.Settings: DrawSettingsTab(); break;
                case MainTab.Help: DrawHelpTab(); break;
                default: DrawCategoryTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Setup Tab

        private void DrawSetupTab()
        {
            GUILayout.Space(10);
            GUILayout.Label("Unity Scanner Setup", EditorStyles.boldLabel);

            if (_cachedAssetCount < 0)
                _cachedAssetCount = AssetDatabase.GetAllAssetPaths().Count(p => p.StartsWith("Assets/"));
            var assetCount = _cachedAssetCount;
            if (assetCount > 10000)
                EditorGUILayout.HelpBox($"Project has {assetCount:N0} assets. Scanning may take several minutes.", MessageType.Warning);

            GUILayout.Space(5);

            USGUIUtilities.HorizontalLine();
            DrawBuildLayoutSection();
            USGUIUtilities.HorizontalLine();
            DrawCategorySelectionSection();
            USGUIUtilities.HorizontalLine();
            DrawRunActionsSection();
            USGUIUtilities.HorizontalLine();
            DrawScopeSummary();
        }

        private void DrawBuildLayoutSection()
        {
            GUILayout.Label("Build Layout", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Path:", GUILayout.Width(50));
            GUILayout.TextField(string.IsNullOrEmpty(_settings.BuildLayoutPath) ? "(none)" : _settings.BuildLayoutPath,
                GUILayout.ExpandWidth(true));

            if (GUILayout.Button(BrowseButtonContent, GUILayout.Width(80)))
            {
                var path = EditorUtility.OpenFilePanelWithFilters("Select BuildLayout.txt",
                    _buildLayoutFolderCache, new[] { "Text Files (*.txt)", "txt" });
                if (!string.IsNullOrEmpty(path))
                {
                    _settings.BuildLayoutPath = path;
                    _buildLayoutPathDisplay = path;
                    _buildLayoutFolderCache = System.IO.Path.GetDirectoryName(path);
                    EditorPrefs.SetString(BuildLayoutFolderPref, _buildLayoutFolderCache);
                }
            }

            if (GUILayout.Button(ClearButtonContent, GUILayout.Width(50)))
            {
                _settings.BuildLayoutPath = "";
                _buildLayoutPathDisplay = "";
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Label(
                "Optional. Used by Materials, Textures, and Build Layout categories for bundle-aware analysis.",
                EditorStyles.miniLabel);
        }

        private void DrawCategorySelectionSection()
        {
            GUILayout.Label("Categories", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(SelectAllContent, GUILayout.Width(80)))
            {
                foreach (var cat in _registry.Categories)
                    cat.Settings.Enabled = true;
                SaveEnabledCategories();
            }

            if (GUILayout.Button(DeselectAllContent, GUILayout.Width(80)))
            {
                foreach (var cat in _registry.Categories)
                    cat.Settings.Enabled = false;
                SaveEnabledCategories();
            }

            EditorGUILayout.EndHorizontal();

            foreach (var (name, tab, _) in _allTabs)
            {
                if (name == "---") continue;

                var categoryId = TabToCategoryId(tab);
                if (categoryId == null) continue;
                if (categoryId == "regression_trend") continue;

                var cat = _registry.GetCategory(categoryId);
                if (cat == null) continue;

                EditorGUILayout.BeginHorizontal();
                var wasEnabled = cat.Settings.Enabled;
                cat.Settings.Enabled = EditorGUILayout.ToggleLeft($"  {name}", cat.Settings.Enabled,
                    GUILayout.Width(200));
                if (cat.Settings.Enabled != wasEnabled) SaveEnabledCategories();
                GUILayout.Label($"[{cat.Id}]", EditorStyles.miniLabel, GUILayout.Width(120));

                if (_tabStates.TryGetValue(cat.Id, out var state))
                {
                    var stateLabel = state switch
                    {
                        TabState.NotRun => "Not Run",
                        TabState.Running => "Running...",
                        TabState.DoneNoIssues => "Done - OK",
                        TabState.DoneHasIssues => "Done - Issues",
                        TabState.Failed => "Failed",
                        TabState.Skipped => "Skipped",
                        _ => ""
                    };
                    var stateColor = state switch
                    {
                        TabState.DoneNoIssues => Color.green,
                        TabState.DoneHasIssues => Color.yellow,
                        TabState.Failed => Color.red,
                        TabState.Running => Color.cyan,
                        TabState.Skipped => Color.gray,
                        _ => Color.white
                    };
                    USGUIUtilities.DrawColoredLabel(stateLabel, stateColor, 150);
                }
                else
                {
                    GUILayout.Label("", GUILayout.Width(150));
                }

                var hasResult = _tabResults.ContainsKey(cat.Id);
                EditorGUI.BeginDisabledGroup(!hasResult);
                if (GUILayout.Button(ViewButtonContent, EditorStyles.miniButton, GUILayout.Width(50)))
                    _currentTab = tab;
                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawRunActionsSection()
        {
            GUILayout.Label("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(_isRunning);

            if (GUILayout.Button(RunAllContent, GUILayout.Width(150), GUILayout.Height(30)))
                StartScanAll();

            if (GUILayout.Button(RunFailedContent, GUILayout.Width(100), GUILayout.Height(30)))
                StartScanFailed();

            if (GUILayout.Button(RunSkippedContent, GUILayout.Width(100), GUILayout.Height(30)))
                StartScanSkipped();

            EditorGUI.EndDisabledGroup();

            if (_isRunning && GUILayout.Button(CancelScanContent, GUILayout.Width(80), GUILayout.Height(30)))
                _orchestrator.Cancel();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawScopeSummary()
        {
            GUILayout.Label("Scope Summary", EditorStyles.boldLabel);

            if (_cachedEnabledCount < 0)
                _cachedEnabledCount = _registry.Categories.Count(c => c.Settings.Enabled);
            GUILayout.Label($"Enabled categories: {_cachedEnabledCount}/{_registry.Categories.Count}", EditorStyles.miniLabel);

            if (_lastAggregate != null)
            {
                if (_cachedTotalIssues < 0)
                    _cachedTotalIssues = _lastAggregate.Results.Sum(r => r.Issues.Count);
                GUILayout.Label(
                    $"Last scan: {_lastAggregate.Results.Count} categories completed, {_cachedTotalIssues} total issues, {_lastAggregate.TotalDurationMs:F0}ms",
                    EditorStyles.miniLabel);
            }

            if (GUILayout.Button(ViewSummaryContent, GUILayout.Width(150)))
                _currentTab = MainTab.Summary;
        }

        #endregion

        #region Category Tab

        private void DrawCategoryTab()
        {
            var categoryId = TabToCategoryId(_currentTab);
            if (categoryId == null) return;

            if (_tabStates.TryGetValue(categoryId, out var state) && state == TabState.Running)
            {
                return;
            }

            if (_tabDrawers.TryGetValue(categoryId, out var drawer))
            {
                var result = _tabResults.GetValueOrDefault(categoryId);
                
                DrawCategoryInfoFoldout(_currentTab);

                if (result is { Succeeded: false })
                {
                    USGUIUtilities.DrawColoredLabel($"Failed. {result.ErrorMessage}", Color.red);
                }
                
                drawer.DrawTopBar(result);
                drawer.DrawHeader(result);
                drawer.DrawIssues(result);
             
                return;
            }

            DrawGenericCategoryTab(categoryId);
        }

        private static readonly HashSet<MainTab> InfoExcludedTabs = new HashSet<MainTab>
        {
            MainTab.Setup, MainTab.RegressionTrend, MainTab.Summary, MainTab.Help, MainTab.MissingReferences
        };

        private void DrawCategoryInfoFoldout(MainTab tab)
        {
            if (InfoExcludedTabs.Contains(tab)) return;

            var desc = GetTabDescription(tab);
            if (string.IsNullOrEmpty(desc)) return;

            _categoryInfoFoldout = EditorGUILayout.Foldout(_categoryInfoFoldout, "Info");
            if (!_categoryInfoFoldout) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(desc, MessageType.Info);
            EditorGUI.indentLevel--;
        }

        private void DrawGenericCategoryTab(string categoryId)
        {
            if (_tabStates.TryGetValue(categoryId, out var state) && state == TabState.Running)
            {
                return;
            }

            var category = _registry.GetCategory(categoryId);
            if (category == null)
            {
                GUILayout.Label($"Unknown category: {categoryId}");
                return;
            }

            GUILayout.Label(category.DisplayName, EditorStyles.boldLabel);
            GUILayout.Space(5);

            DrawCategoryInfoFoldout(CategoryIdToTab(categoryId));

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(_isRunning);
            if (GUILayout.Button(new GUIContent($"Scan {category.DisplayName}", "Run scan for this category only"), GUILayout.Width(200), GUILayout.Height(25)))
                StartScanSingle(categoryId);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            if (_tabResults.TryGetValue(categoryId, out var result))
            {
                if (!result.Succeeded)
                {
                    USGUIUtilities.DrawColoredLabel($"Error: {result.ErrorMessage}", Color.red);
                    return;
                }

                if (result.Skipped)
                {
                    USGUIUtilities.DrawColoredLabel(result.SkipReason ?? "Skipped", Color.gray);
                    return;
                }

                GUILayout.Label($"Issues: {result.Issues.Count}  |  Time: {result.ScanDurationMs:F0}ms",
                    EditorStyles.miniLabel);
                GUILayout.Space(5);

                foreach (var issue in result.Issues)
                {
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    var sevColor = issue.Severity switch
                    {
                        UnityScannerIssueSeverity.Error => Color.red,
                        UnityScannerIssueSeverity.Warning => Color.yellow,
                        UnityScannerIssueSeverity.Verbose => VerboseColor,
                        _ => Color.cyan
                    };
                    USGUIUtilities.DrawColoredLabel($"[{issue.Severity}]", sevColor, 70);
                    GUILayout.Label(issue.Description, EditorStyles.wordWrappedLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("No results yet. Click Scan to run this category.", EditorStyles.miniLabel);
            }
        }

        #endregion

        private int _summarySortMode;
        private int _summaryExpandedIssue = -1;
        private bool _summaryOverviewFoldout = true;

        private void DrawSummaryTab()
        {
            GUILayout.Label("Summary", EditorStyles.boldLabel);
            GUILayout.Space(5);

            if (_lastAggregate == null || _lastAggregate.Results.Count == 0)
            {
                GUILayout.Label("No scan results yet. Run a scan from the Setup tab.", EditorStyles.wordWrappedLabel);
                return;
            }

            DrawSummaryOverview();
            USGUIUtilities.HorizontalLine();
            DrawSummaryFilters();
            USGUIUtilities.HorizontalLine();
            DrawSummaryActions();
            DrawSummaryIssues();
        }

        private void DrawSummaryOverview()
        {
            _summaryOverviewFoldout = EditorGUILayout.Foldout(_summaryOverviewFoldout, "Overview");
            if (!_summaryOverviewFoldout) return;

            EditorGUI.indentLevel++;

            var allIssues = GetAllIssues();
            var errors = allIssues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
            var warnings = allIssues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);
            var infos = allIssues.Count(i => i.Severity == UnityScannerIssueSeverity.Info);
            var verboses = allIssues.Count(i => i.Severity == UnityScannerIssueSeverity.Verbose);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            USGUIUtilities.DrawColoredLabel($"Errors: {errors}", Color.red, 100);
            USGUIUtilities.DrawColoredLabel($"Warnings: {warnings}", Color.yellow, 100);
            USGUIUtilities.DrawColoredLabel($"Info: {infos}", Color.cyan, 100);
            USGUIUtilities.DrawColoredLabel($"Verbose: {verboses}", new Color(0.7f, 0.7f, 0.7f), 100);
            GUILayout.Label($"Total: {allIssues.Count}", EditorStyles.miniLabel, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Label(
                $"Categories scanned: {_lastAggregate.Results.Count}  |  Total time: {_lastAggregate.TotalDurationMs:F0}ms",
                EditorStyles.miniLabel);

            DrawSummaryCategoryBreakdown();

            EditorGUI.indentLevel--;
        }

        private void DrawSummaryCategoryBreakdown()
        {
            GUILayout.Label("By Category:", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Category", EditorStyles.boldLabel, GUILayout.Width(160));
            GUILayout.Label("Errors", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("Warns", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("Info", EditorStyles.boldLabel, GUILayout.Width(35));
            GUILayout.Label("Verbose", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("Total", EditorStyles.boldLabel, GUILayout.Width(50));
            GUILayout.Label("Time", EditorStyles.boldLabel, GUILayout.Width(70));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            foreach (var result in _lastAggregate.Results.OrderBy(r => r.DisplayName))
            {
                var catErrors = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
                var catWarnings = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);
                var catInfos = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Info);
                var catVerboses = result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Verbose);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(result.ShortDisplayName, GUILayout.Width(160));

                var errColor = catErrors > 0 ? Color.red : Color.gray;
                USGUIUtilities.DrawColoredLabel(catErrors.ToString(), errColor, 50);

                var warnColor = catWarnings > 0 ? Color.yellow : Color.gray;
                USGUIUtilities.DrawColoredLabel(catWarnings.ToString(), warnColor, 50);

                var infoColor = catInfos > 0 ? Color.cyan : Color.gray;
                USGUIUtilities.DrawColoredLabel(catInfos.ToString(), infoColor, 40);

                var verboseColor = catVerboses > 0 ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.5f, 0.5f, 0.5f, 1f);
                USGUIUtilities.DrawColoredLabel(catVerboses.ToString(), verboseColor, 40);

                GUILayout.Label(result.Issues.Count.ToString(), GUILayout.Width(50));
                GUILayout.Label($"{result.ScanDurationMs:F0}ms", EditorStyles.miniLabel, GUILayout.Width(70));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSummaryFilters()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Severity:", GUILayout.Width(55));
            var sevLabel = _summarySeverityFilter switch
            {
                1 => "Errors",
                2 => "Errors+Warn",
                3 => "All+Verbose",
                _ => "Non-Verbose"
            };
            if (GUILayout.Button(new GUIContent(sevLabel, "Cycle severity filter for the issues list"), GUILayout.Width(100)))
            {
                _summarySeverityFilter = _summarySeverityFilter >= 3 ? 0 : _summarySeverityFilter + 1;
                InvalidateSummaryFilter();
            }

            GUILayout.Label("Category:", GUILayout.Width(55));

            var allCats = _registry.Categories;
            var prevColor = GUI.color;

            var allSelected = _summarySelectedCategories.Count == 0 ||
                             allCats.All(c => _summarySelectedCategories.Contains(c.Id));
            GUI.color = allSelected ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("All", "Select all categories in summary filter"), GUILayout.Width(40)))
            {
                _summarySelectedCategories.Clear();
                foreach (var cat in allCats)
                    _summarySelectedCategories.Add(cat.Id);
                InvalidateSummaryFilter();
            }
            GUI.color = prevColor;

            var catScrollRect = GUILayoutUtility.GetRect(0f, 35f, GUILayout.ExpandWidth(true));
            var catContentWidth = allCats.Count * 98;
            _summaryCatScroll = GUI.BeginScrollView(
                catScrollRect,
                _summaryCatScroll,
                new Rect(0f, -3f, catContentWidth, 22f),
                true, false);

            var catX = 0f;
            foreach (var cat in allCats)
            {
                var isSelected = _summarySelectedCategories.Contains(cat.Id);
                GUI.color = isSelected ? Color.yellow : Color.white;
                var btnRect = new Rect(catX, 0f, 140f, 20f);
                if (GUI.Button(btnRect, new GUIContent(cat.DisplayName, "Toggle this category in summary filter"), EditorStyles.miniButton))
                {
                    if (isSelected)
                        _summarySelectedCategories.Remove(cat.Id);
                    else
                        _summarySelectedCategories.Add(cat.Id);
                    InvalidateSummaryFilter();
                }
                catX += 140f;
            }
            GUI.color = prevColor;

            GUI.EndScrollView();
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Path:", GUILayout.Width(35));
            var newPathFilter = GUILayout.TextField(_summaryPathFilter, GUILayout.Width(150));
            if (newPathFilter != _summaryPathFilter)
            {
                _summaryPathFilter = newPathFilter;
                InvalidateSummaryFilter();
            }

            GUILayout.Label("Text:", GUILayout.Width(35));
            var newTextFilter = GUILayout.TextField(_summaryTextFilter, GUILayout.Width(150));
            if (newTextFilter != _summaryTextFilter)
            {
                _summaryTextFilter = newTextFilter;
                InvalidateSummaryFilter();
            }

            if (GUILayout.Button(new GUIContent("Clear", "Reset all summary filters"), GUILayout.Width(50)))
            {
                _summarySeverityFilter = 0;
                _summarySelectedCategories.Clear();
                _summaryPathFilter = "";
                _summaryTextFilter = "";
                InvalidateSummaryFilter();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            var sortLabel = _summarySortMode switch
            {
                0 => "Severity Desc",
                1 => "Category",
                2 => "Category Desc",
                3 => "Asset Path",
                _ => "Severity Desc"
            };
            if (GUILayout.Button(new GUIContent($"Sort: {sortLabel}", "Change sort order for issues list"), GUILayout.Width(130)))
            {
                _summarySortMode = _summarySortMode >= 3 ? 0 : _summarySortMode + 1;
                InvalidateSummaryFilter();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummaryActions()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("Copy CSV to Clipboard", "Copy filtered issues as CSV to clipboard"), GUILayout.Width(160)))
                CopySummaryCsv();

            if (GUILayout.Button(new GUIContent("Export CSV...", "Export filtered issues to a CSV file"), GUILayout.Width(100)))
                ExportSummaryCsv();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummaryIssues()
        {
            var filtered = GetFilteredSummaryIssues();

            GUILayout.Label($"Issues: {filtered.Count}");
            USGUIPaginationUtilities.DrawPagesWidget(filtered.Count, _summaryPagination);

            _summaryScroll = EditorGUILayout.BeginScrollView(_summaryScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filtered.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _summaryPagination))
                    continue;

                var issue = filtered[i];
                DrawSummaryIssueRow(i, issue);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSummaryIssueRow(int index, UnityScannerIssue issue)
        {
            var isExpanded = _summaryExpandedIssue == index;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var sevColor = issue.Severity switch
            {
                UnityScannerIssueSeverity.Error => Color.red,
                UnityScannerIssueSeverity.Warning => Color.yellow,
                _ => Color.cyan
            };

            var toggleChar = isExpanded ? "v" : ">";
            if (GUILayout.Button(new GUIContent(toggleChar, "Toggle visibility of this severity level"), EditorStyles.miniButton, GUILayout.Width(18)))
                _summaryExpandedIssue = isExpanded ? -1 : index;

            USGUIUtilities.DrawColoredLabel($"[{issue.Severity}]", sevColor, 70);

            var category = _registry.GetCategory(issue.CategoryId);
            GUILayout.Label(category?.DisplayName ?? issue.CategoryId, EditorStyles.miniLabel, GUILayout.Width(100));

            var shortDesc = issue.Description;
            if (shortDesc != null && shortDesc.Length > 120)
                shortDesc = shortDesc.Substring(0, 120) + "...";
            GUILayout.Label(shortDesc, EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();

            DrawSummaryIssueActions(issue);

            EditorGUILayout.EndHorizontal();

            if (isExpanded)
                DrawSummaryIssueDetail(issue);
        }

        private void DrawSummaryIssueActions(UnityScannerIssue issue)
        {
            if (!string.IsNullOrEmpty(issue.AssetPath))
            {
                if (GUILayout.Button(new GUIContent("Ping", "Ping this asset in the Project window"), GUILayout.Width(40)))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(issue.AssetPath));

                if (GUILayout.Button(new GUIContent("Open", "Open this asset (scene, script, etc.)"), GUILayout.Width(40)))
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(issue.AssetPath);
                    if (obj != null)
                        AssetDatabase.OpenAsset(obj);
                }
            }

            var fixProvider = _registry.GetFixProvider(issue.CategoryId);
            if (fixProvider != null && fixProvider.CanFix(issue))
            {
                if (GUILayout.Button(new GUIContent("Fix", "Apply the recommended fix for this issue"), GUILayout.Width(35)))
                {
                    var preview = fixProvider.Preview(issue, BuildScanContext());
                    if (preview != null)
                    {
                        var apply = EditorUtility.DisplayDialog("Apply Fix",
                            $"{preview.Description}\n\nApply this fix?", "Apply", "Cancel");
                        if (apply)
                            fixProvider.Apply(issue, BuildScanContext());
                    }
                }
            }
        }

        private void DrawSummaryIssueDetail(UnityScannerIssue issue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!string.IsNullOrEmpty(issue.IssueCode))
                GUILayout.Label($"Code: {issue.IssueCode}", EditorStyles.miniLabel);

            GUILayout.Label("Description:", EditorStyles.miniLabel);
            GUILayout.Label(issue.Description, EditorStyles.wordWrappedLabel);

            if (!string.IsNullOrEmpty(issue.AssetPath))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Asset:", EditorStyles.miniLabel, GUILayout.Width(50));
                if (GUILayout.Button(new GUIContent(issue.AssetPath, "Select this asset in the Project window"), EditorStyles.miniLabel))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(issue.AssetPath));
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(issue.Guid))
                GUILayout.Label($"GUID: {issue.Guid}", EditorStyles.miniLabel);

            if (issue.Metadata != null && issue.Metadata.Count > 0)
            {
                GUILayout.Label("Metadata:", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                foreach (var kv in issue.Metadata)
                    GUILayout.Label($"{kv.Key}: {kv.Value}", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            if (issue.TargetObject != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Target:", EditorStyles.miniLabel, GUILayout.Width(50));
                EditorGUILayout.ObjectField(issue.TargetObject, typeof(UnityEngine.Object), true);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void CopySummaryCsv()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Severity,Category,Code,AssetPath,Description");

            var issues = GetFilteredSummaryIssues();
            foreach (var issue in issues)
            {
                var cat = _registry.GetCategory(issue.CategoryId);
                sb.AppendLine(
                    $"{issue.Severity},{USExportUtilities.EscapeCsvField(cat?.DisplayName ?? issue.CategoryId)},{USExportUtilities.EscapeCsvField(issue.IssueCode)},{USExportUtilities.EscapeCsvField(issue.AssetPath)},{USExportUtilities.EscapeCsvField(issue.Description)}");
            }

            USExportUtilities.CopyToClipboard(sb);
        }

        private void ExportSummaryCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Summary CSV", "", "UnityScanner_Summary.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Severity,Category,Code,AssetPath,Guid,Description");

            var issues = GetFilteredSummaryIssues();
            foreach (var issue in issues)
            {
                var cat = _registry.GetCategory(issue.CategoryId);
                sb.AppendLine(
                    $"{issue.Severity},{USExportUtilities.EscapeCsvField(cat?.DisplayName ?? issue.CategoryId)},{USExportUtilities.EscapeCsvField(issue.IssueCode)},{USExportUtilities.EscapeCsvField(issue.AssetPath)},{USExportUtilities.EscapeCsvField(issue.Guid)},{USExportUtilities.EscapeCsvField(issue.Description)}");
            }

            System.IO.File.WriteAllText(path, sb.ToString());
            EditorUtility.RevealInFinder(path);
        }

        private List<UnityScannerIssue> GetAllIssues()
        {
            var all = new List<UnityScannerIssue>();
            if (_lastAggregate == null) return all;
            foreach (var result in _lastAggregate.Results)
                all.AddRange(result.Issues);
            return all;
        }

        private void InvalidateSummaryFilter()
        {
            _filteredSummaryIssues = null;
        }

        private List<UnityScannerIssue> GetFilteredSummaryIssues()
        {
            if (_filteredSummaryIssues != null) return _filteredSummaryIssues;

            var issues = GetAllIssues();

            if (_summarySeverityFilter > 0)
            {
                issues = _summarySeverityFilter switch
                {
                    1 => issues.Where(i => i.Severity == UnityScannerIssueSeverity.Error).ToList(),
                    2 => issues.Where(i => i.Severity >= UnityScannerIssueSeverity.Warning).ToList(),
                    3 => issues,
                    _ => issues.Where(i => i.Severity >= UnityScannerIssueSeverity.Info).ToList()
                };
            }

            var allCatIds = new HashSet<string>(_registry.Categories.Select(c => c.Id));
            var isAllSelected = _summarySelectedCategories.Count > 0 &&
                               allCatIds.All(id => _summarySelectedCategories.Contains(id));
            if (_summarySelectedCategories.Count > 0 && !isAllSelected)
            {
                issues = issues.Where(i => _summarySelectedCategories.Contains(i.CategoryId)).ToList();
            }

            if (!string.IsNullOrEmpty(_summaryPathFilter))
            {
                var lowered = _summaryPathFilter.ToLowerInvariant();
                issues = issues.Where(i =>
                    i.AssetPath != null && i.AssetPath.ToLowerInvariant().Contains(lowered)
                ).ToList();
            }

            if (!string.IsNullOrEmpty(_summaryTextFilter))
            {
                var lowered = _summaryTextFilter.ToLowerInvariant();
                issues = issues.Where(i =>
                    (i.Description?.ToLowerInvariant().Contains(lowered) ?? false) ||
                    (i.AssetPath?.ToLowerInvariant().Contains(lowered) ?? false) ||
                    (i.IssueCode?.ToLowerInvariant().Contains(lowered) ?? false)
                ).ToList();
            }

            issues = _summarySortMode switch
            {
                0 => issues.OrderByDescending(i => i.Severity).ThenBy(i => i.CategoryId).ToList(),
                1 => issues.OrderBy(i => i.CategoryId).ThenByDescending(i => i.Severity).ToList(),
                2 => issues.OrderByDescending(i => i.CategoryId).ThenByDescending(i => i.Severity).ToList(),
                3 => issues.OrderBy(i => i.AssetPath).ThenByDescending(i => i.Severity).ToList(),
                _ => issues.OrderByDescending(i => i.Severity).ToList()
            };

            _filteredSummaryIssues = issues;
            _summaryExpandedIssue = -1;
            return _filteredSummaryIssues;
        }

        #region Settings Tab

        private void DrawSettingsTab()
        {
            _settingsScroll = EditorGUILayout.BeginScrollView(_settingsScroll);

            GUILayout.Label("Settings", EditorStyles.boldLabel);
            GUILayout.Space(5);

            DrawPlatformProfileSection();
            USGUIUtilities.HorizontalLine();
            DrawCacheSettings();
            USGUIUtilities.HorizontalLine();
            DrawPerformanceSettings();
            USGUIUtilities.HorizontalLine();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Reset to Defaults", "Reset all settings to default values"), GUILayout.Width(150)))
            {
                _settings.CacheEnabled = false;
                _settings.BinaryCacheEnabled = false;
                _settings.BuildLayoutPath = "";
                _buildLayoutPathDisplay = "";
                _settings.YieldAssetThreshold = 5000;
                _settings.YieldIntervalDivisor = 10;
                _settings.SetPlatformProfile(PlatformProfilePresets.Mobile);
                EditorPrefs.SetString(PlatformProfilePref, PlatformProfilePresets.Mobile);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPlatformProfileSection()
        {
            GUILayout.Label("Platform Profile", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Active Profile:", GUILayout.Width(90));

            var profileIds = UnityScanner.Core.Settings.PlatformProfilePresets.GetPresetIds();
            var profileNames = UnityScanner.Core.Settings.PlatformProfilePresets.GetPresetDisplayNames();
            var currentIndex = System.Array.IndexOf(profileIds, _settings.ActivePlatformProfileId);
            if (currentIndex < 0) currentIndex = 0;

            var newIndex = EditorGUILayout.Popup(currentIndex, profileNames, GUILayout.Width(120));
            if (newIndex != currentIndex)
            {
                _settings.SetPlatformProfile(profileIds[newIndex]);
                EditorPrefs.SetString(PlatformProfilePref, profileIds[newIndex]);
            }

            GUILayout.Label(_settings.ActivePlatformProfile.Description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();

            var profile = _settings.ActivePlatformProfile;
            EditorGUI.indentLevel++;
            GUILayout.Label($"Max Texture: {profile.MaxTextureSize}px | Shader Variants: {profile.ShaderVariantThreshold} | " +
                            $"Shader Keywords: {profile.ShaderKeywordThreshold} | Shader Passes: {profile.ShaderPassThreshold}",
                EditorStyles.miniLabel);
            GUILayout.Label($"Particle Emission: {profile.MaxParticleEmissionRate} | Particle Trail Lifetime: {profile.MaxParticleTrailLifetime:F1}s | " +
                            $"Particle Modules: {profile.MaxParticleSystemModules} | Canvas Vertices: {profile.MaxCanvasVertexCount} | Canvas Nesting: {profile.MaxCanvasNestingDepth}",
                EditorStyles.miniLabel);
            GUILayout.Label($"Realtime Lights: {profile.MaxRealtimeLightsPerScene} | Lightmap Size: {profile.MaxLightmapSize} | " +
                            $"Reflection Probes: {profile.MaxReflectionProbeCount} (size {profile.MaxReflectionProbeSize}) | LOD Levels: {profile.MinLODLevels} | LOD Transition: {profile.MaxLODScreenTransitionHeight:F2}",
                EditorStyles.miniLabel);
            GUILayout.Label($"Rigidbodies: {profile.MaxRigidbodyCount} | Mesh Collider Triangles: {profile.MaxMeshColliderTriangles} | " +
                            $"Sprite Atlas Waste: {profile.MaxSpriteAtlasUnusedRatio:P0}",
                EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        private void DrawCacheSettings()
        {
            GUILayout.Label("Cache", EditorStyles.boldLabel);

            _settings.CacheEnabled = EditorGUILayout.Toggle(
                new GUIContent("Cache Enabled", "Enable caching of scan results to speed up repeated scans. Cached data is stored on disk."),
                _settings.CacheEnabled);
            EditorGUI.BeginDisabledGroup(!_settings.CacheEnabled);
            _settings.BinaryCacheEnabled =
                EditorGUILayout.Toggle(
                    new GUIContent("Binary Cache", "Use binary format for cache files instead of JSON. Faster but not human-readable."),
                    _settings.BinaryCacheEnabled);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawPerformanceSettings()
        {
            GUILayout.Label("Performance", EditorStyles.boldLabel);

            _settings.YieldAssetThreshold = EditorGUILayout.IntField(
                new GUIContent("Yield Asset Threshold",
                    "Minimum number of assets before scanners start yielding. Lower values = more frequent yields = slower but smoother UI."),
                _settings.YieldAssetThreshold);
            if (_settings.YieldAssetThreshold < 100)
                _settings.YieldAssetThreshold = 100;

            _settings.YieldIntervalDivisor = EditorGUILayout.IntField(
                new GUIContent("Yield Interval Divisor",
                    "Controls how often scanners yield during scanning. Yield every (totalAssets / divisor) iterations. Higher values = less frequent yields."),
                _settings.YieldIntervalDivisor);
            if (_settings.YieldIntervalDivisor < 1)
                _settings.YieldIntervalDivisor = 1;

            EditorGUILayout.HelpBox(
                $"Scanners will yield every ~{(_settings.YieldIntervalDivisor > 0 ? _settings.YieldAssetThreshold / _settings.YieldIntervalDivisor : 0)} assets when above {_settings.YieldAssetThreshold} total assets.",
                MessageType.Info);
        }

        #endregion

        #region Help Tab

        private void DrawHelpTab()
        {
            _helpScroll = EditorGUILayout.BeginScrollView(_helpScroll);

            GUILayout.Space(10);
            GUILayout.Label("Unity Scanner", EditorStyles.boldLabel);
            GUILayout.Label(
                "A unified Unity Editor tool that combines multiple asset analysis categories into one window.",
                EditorStyles.wordWrappedLabel);

            GUILayout.Space(8);
            USGUIUtilities.HorizontalLine();

            GUILayout.Label("Categories", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            foreach (var (name, tab, description) in _allTabs)
            {
                if (name == "---") continue;
                if (string.IsNullOrEmpty(description)) continue;
                GUILayout.Label($"{name} — {description}", EditorStyles.wordWrappedLabel);
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(8);
            USGUIUtilities.HorizontalLine();

            GUILayout.Label("How to Use", EditorStyles.boldLabel);
            GUILayout.Label("1. Open Setup tab and enable desired categories.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("2. Optionally load a BuildLayout.txt for bundle-aware analysis.",
                EditorStyles.wordWrappedLabel);
            GUILayout.Label("3. Click 'Run All Selected' to scan.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("4. Switch to category tabs or Summary tab to review results.",
                EditorStyles.wordWrappedLabel);

            GUILayout.Space(8);
            USGUIUtilities.HorizontalLine();

            GUILayout.Label("Tab Status Indicators", EditorStyles.boldLabel);
            USGUIUtilities.DrawColoredLabel("  White — Not run yet", Color.white);
            USGUIUtilities.DrawColoredLabel("  Cyan — Currently running", Color.cyan);
            USGUIUtilities.DrawColoredLabel("  Green — Done, no issues", Color.green);
            USGUIUtilities.DrawColoredLabel("  Yellow — Done, has issues (count shown)", Color.yellow);
            USGUIUtilities.DrawColoredLabel("  Red — Failed", Color.red);
            USGUIUtilities.DrawColoredLabel("  Gray — Skipped", Color.gray);

            GUILayout.Space(10);

            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Scan Execution

        private readonly Stack<IEnumerator> _coroutineStack = new();

        private void StartScanAll()
        {
            if (_isRunning) return;
            _isRunning = true;

            foreach (var cat in _registry.Categories)
            {
                if (cat.Id == "regression_trend") continue;
                if (cat.Settings.Enabled)
                {
                    _tabStates[cat.Id] = TabState.Running;
                }
                else if (!_tabResults.ContainsKey(cat.Id))
                {
                    _tabStates[cat.Id] = TabState.Skipped;
                }
            }

            _filteredSummaryIssues = null;
            _runningCategoryIndex = 0;
            _runningCategoryTotal = _registry.Categories.Count(c => c.Settings.Enabled && c.Id != "regression_trend");

            var context = BuildScanContext();
            _coroutineStack.Clear();
            _coroutineStack.Push(RunAllCoroutine(context));
            EditorApplication.update += TickCoroutine;
        }

        private void StartScanSingle(string categoryId)
        {
            if (_isRunning) return;
            _isRunning = true;
            _tabStates[categoryId] = TabState.Running;
            _filteredSummaryIssues = null;
            _runningCategoryIndex = 1;
            _runningCategoryTotal = 1;

            var context = BuildScanContext();
            _coroutineStack.Clear();
            _coroutineStack.Push(RunSingleCoroutine(categoryId, context));
            EditorApplication.update += TickCoroutine;
        }

        private void StartScanFailed()
        {
            if (_isRunning) return;

            var failedIds = _tabStates
                .Where(kv => kv.Value == TabState.Failed && kv.Key != "regression_trend")
                .Select(kv => kv.Key)
                .ToList();

            if (failedIds.Count == 0) return;

            _isRunning = true;
            foreach (var id in failedIds)
                _tabStates[id] = TabState.Running;
            _filteredSummaryIssues = null;
            _runningCategoryIndex = 0;
            _runningCategoryTotal = failedIds.Count;

            var context = BuildScanContext();
            _coroutineStack.Clear();
            _coroutineStack.Push(RunSelectedCoroutine(failedIds, context));
            EditorApplication.update += TickCoroutine;
        }

        private void StartScanSkipped()
        {
            if (_isRunning) return;

            var skippedIds = _tabStates
                .Where(kv => kv.Value == TabState.Skipped && kv.Key != "regression_trend")
                .Select(kv => kv.Key)
                .ToList();

            if (skippedIds.Count == 0) return;

            _isRunning = true;
            foreach (var id in skippedIds)
                _tabStates[id] = TabState.Running;
            _filteredSummaryIssues = null;
            _runningCategoryIndex = 0;
            _runningCategoryTotal = skippedIds.Count;

            var context = BuildScanContext();
            _coroutineStack.Clear();
            _coroutineStack.Push(RunSelectedCoroutine(skippedIds, context));
            EditorApplication.update += TickCoroutine;
        }

        private void TickCoroutine()
        {
            if (_coroutineStack.Count == 0)
            {
                EditorApplication.update -= TickCoroutine;
                return;
            }

            var current = _coroutineStack.Peek();
            bool moved;
            try
            {
                moved = current.MoveNext();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _coroutineStack.Clear();
                _isRunning = false;
                _currentProgress = null;
                EditorApplication.update -= TickCoroutine;
                Repaint();
                return;
            }

            if (!moved)
            {
                _coroutineStack.Pop();
            }
            else if (current.Current is IEnumerator nested)
            {
                _coroutineStack.Push(nested);
            }

            if (moved)
                Repaint();
        }

        private UnityScannerScanContext BuildScanContext()
        {
            UnityScannerAggregateResult previous = null;
            if (_lastAggregate != null && _lastAggregate.Results.Count > 0)
                previous = _lastAggregate;
            else
            {
                previous = new UnityScannerAggregateResult();
                foreach (var kv in _tabResults)
                    previous.Results.Add(kv.Value);
            }

            return new UnityScannerScanContext
            {
                Settings = _settings,
                PreviousResults = previous
            };
        }

        private IEnumerator RunAllCoroutine(UnityScannerScanContext context)
        {
            yield return _orchestrator.RunAll(context, aggregate =>
            {
                _lastAggregate = aggregate;
                _cachedTotalIssues = -1;
                ProcessResults(aggregate);
                _isRunning = false;
                _currentProgress = null;
                _runningCategoryIndex = 0;
                _runningCategoryTotal = 0;
                Repaint();
            });
        }

        private IEnumerator RunSingleCoroutine(string categoryId, UnityScannerScanContext context)
        {
            yield return _orchestrator.RunCategory(categoryId, context, result =>
            {
                if (result != null)
                {
                    _tabResults[categoryId] = result;
                    UpdateTabState(categoryId, result);
                    UpdateAggregate();
                }

                _isRunning = false;
                _currentProgress = null;
                Repaint();
            });
        }

        private IEnumerator RunSelectedCoroutine(List<string> categoryIds, UnityScannerScanContext context)
        {
            foreach (var categoryId in categoryIds)
            {
                if (_orchestrator == null) break;

                var category = _registry.GetCategory(categoryId);
                if (category == null || !category.Settings.Enabled) continue;

                var aggregate = new UnityScannerAggregateResult();
                yield return _orchestrator.RunCategory(categoryId, context, result =>
                {
                    if (result != null)
                    {
                        _tabResults[categoryId] = result;
                        UpdateTabState(categoryId, result);
                    }
                });
            }

            UpdateAggregate();
            _isRunning = false;
            _currentProgress = null;
            Repaint();
        }

        private void ProcessResults(UnityScannerAggregateResult aggregate)
        {
            foreach (var result in aggregate.Results)
            {
                DetectSkippedState(result);
                _tabResults[result.CategoryId] = result;
                UpdateTabState(result.CategoryId, result);
            }

            foreach (var cat in _registry.Categories)
            {
                if (cat.Id == "regression_trend") continue;
                if (!_tabResults.ContainsKey(cat.Id) && !cat.Settings.Enabled)
                    _tabStates[cat.Id] = TabState.Skipped;
            }
        }

        private void DetectSkippedState(UnityScannerResult result)
        {
            if (result.CategoryId != "addressables") return;
            if (!result.Succeeded || result.Issues.Count > 0) return;

            var cat = _registry.GetCategory("addressables") as AddressablesCategory;
            if (cat != null && cat.LastResult == null)
            {
                result.Skipped = true;
                result.SkipReason = "BuildLayout.txt path not configured";
            }
        }

        private void UpdateTabState(string categoryId, UnityScannerResult result)
        {
            if (result.Skipped)
                _tabStates[categoryId] = TabState.Skipped;
            else if (!result.Succeeded)
                _tabStates[categoryId] = TabState.Failed;
            else if (result.Issues.Any(i => i.Severity >= UnityScannerIssueSeverity.Warning))
                _tabStates[categoryId] = TabState.DoneHasIssues;
            else
                _tabStates[categoryId] = TabState.DoneNoIssues;
        }

        private void UpdateAggregate()
        {
            _cachedTotalIssues = -1;
            _lastAggregate = new UnityScannerAggregateResult();
            foreach (var kvp in _tabResults)
                _lastAggregate.Results.Add(kvp.Value);
            _filteredSummaryIssues = null;
        }

        #endregion

        #region Helpers

        private string TabToCategoryId(MainTab tab)
        {
            return tab switch
            {
                MainTab.Dependencies => "dependencies",
                MainTab.MissingReferences => "missing_references",
                MainTab.Materials => "materials",
                MainTab.Textures => "textures",
                MainTab.ShaderAnalysis => "shader_analysis",
                MainTab.TerrainAnalysis => "terrain_analysis",
                MainTab.FontTextAnalysis => "font_text_analysis",
                MainTab.AudioAnalysis => "audio_analysis",
                MainTab.AnimationAnalysis => "animation_analysis",
                MainTab.ScenePrefabHealth => "scene_prefab_health",
                MainTab.BuildPlatformReadiness => "build_platform_readiness",
                MainTab.ParticleAnalysis => "particle_analysis",
                MainTab.UICanvasAnalysis => "ui_canvas_analysis",
                MainTab.LightingAnalysis => "lighting_analysis",
                MainTab.LODAnalysis => "lod_analysis",
                MainTab.PhysicsAnalysis => "physics_analysis",
                MainTab.AsmDefAudit => "asmdef_audit",
                MainTab.ProjectHealth => "project_health",
                MainTab.Sprite2DAnalysis => "sprite_2d_analysis",
                MainTab.Addressables => "addressables",
                MainTab.RegressionTrend => "regression_trend",
                _ => null
            };
        }

        private MainTab CategoryIdToTab(string categoryId)
        {
            return categoryId switch
            {
                "dependencies" => MainTab.Dependencies,
                "missing_references" => MainTab.MissingReferences,
                "materials" => MainTab.Materials,
                "textures" => MainTab.Textures,
                "shader_analysis" => MainTab.ShaderAnalysis,
                "terrain_analysis" => MainTab.TerrainAnalysis,
                "font_text_analysis" => MainTab.FontTextAnalysis,
                "audio_analysis" => MainTab.AudioAnalysis,
                "animation_analysis" => MainTab.AnimationAnalysis,
                "scene_prefab_health" => MainTab.ScenePrefabHealth,
                "build_platform_readiness" => MainTab.BuildPlatformReadiness,
                "particle_analysis" => MainTab.ParticleAnalysis,
                "ui_canvas_analysis" => MainTab.UICanvasAnalysis,
                "lighting_analysis" => MainTab.LightingAnalysis,
                "lod_analysis" => MainTab.LODAnalysis,
                "physics_analysis" => MainTab.PhysicsAnalysis,
                "asmdef_audit" => MainTab.AsmDefAudit,
                "project_health" => MainTab.ProjectHealth,
                "sprite_2d_analysis" => MainTab.Sprite2DAnalysis,
                "addressables" => MainTab.Addressables,
                "regression_trend" => MainTab.RegressionTrend,
                _ => MainTab.Setup
            };
        }

        public void SwitchToCategoryTab(string categoryId, string[] selectedPaths = null, UnityEngine.Object[] selectedObjects = null)
        {
            EnsureInitialized();

            if (selectedPaths != null && categoryId == "dependencies")
            {
                var context = new UnityScannerScanContext
                {
                    Settings = _settings,
                    SelectedAssetPaths = selectedPaths
                };
                StartScanSingle(categoryId);
            }

            _currentTab = CategoryIdToTab(categoryId);
            Repaint();
        }

        private void EnsureInitialized()
        {
            if (_registry == null || _registry.Categories.Count == 0)
                Initialize();
        }

        #endregion
    }
}
