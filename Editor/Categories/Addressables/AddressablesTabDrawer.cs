using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Results;
using UnityScanner.UI.Controls;
using BuildLayoutProvider = UnityScanner.Categories.Addressables.USAddressablesBuildLayoutProvider;
using BundleLayoutComparisonService = UnityScanner.Categories.Addressables.USAddressablesBundleComparisonService;
using BundleUtilities = UnityScanner.Categories.Addressables.USAddressablesBundleUtilities;
using DependencyFormatter = UnityScanner.Categories.Addressables.USAddressablesDependencyFormatter;

namespace UnityScanner.Categories.Addressables
{
    public class AddressablesTabDrawer : IUnityScannerTabDrawer
    {
        private enum SubTab
        {
            Groups,
            Size,
            Assets,
            Duplicates,
            Labels,
            Comparison,
            Settings
        }

        private const string ComparisonFolderKey = "US_Addressables_LastComparisonFolder";

        private AddressablesCategory _category;
        private USAddressablesSettings Settings => _category?.Settings as USAddressablesSettings ?? new USAddressablesSettings();

        private SubTab _currentTab = SubTab.Groups;
        private SubTab _previousTab;

        private Vector2 _mainScroll;

        private readonly (string name, SubTab tab)[] _toolbarTabs =
        {
            ("Groups", SubTab.Groups),
            ("Size", SubTab.Size),
            ("Assets", SubTab.Assets),
            ("Duplicates", SubTab.Duplicates),
            ("Labels", SubTab.Labels),
            ("Comparison", SubTab.Comparison),
            ("Settings", SubTab.Settings)
        };

        private readonly SubTab[] _analysisTabs =
        {
            SubTab.Groups, SubTab.Size, SubTab.Assets,
            SubTab.Duplicates, SubTab.Labels, SubTab.Comparison
        };

        private string _groupsFilter;
        private Vector2 _groupsScroll;
        private readonly USPaginationSettings _groupsPagination = new() { PageToShow = 0, PageSize = 20 };
        private int _groupsSortType;
        private bool _groupsNeedInitialSort = true;
        private BuildLayoutProvider _lastSortedLayout;
        private BuildLayoutProvider.Group _selectedGroup;
        private Vector2 _selectedGroupScroll;
        private readonly Dictionary<BuildLayoutProvider.Archive, USAddressablesArchiveUiState> _archiveUIStates = new();
        private readonly Dictionary<BuildLayoutProvider.Archive, bool> _bundleRecommendationsFoldouts = new();
        private readonly Dictionary<BuildLayoutProvider.Archive, bool> _bundleDetailsFoldouts = new();
        private readonly Dictionary<BuildLayoutProvider.Archive, bool> _bundleDepsFoldouts = new();
        private readonly Dictionary<BuildLayoutProvider.Archive, bool> _bundleExpandedDepsFoldouts = new();

        private List<BuildLayoutProvider.Archive> _sizeBundles;
        private string _sizeFilter;
        private Vector2 _sizeScroll;
        private readonly USPaginationSettings _sizePagination = new() { PageToShow = 0, PageSize = 20 };
        private BuildLayoutProvider.Archive _selectedSizeBundle;
        private Vector2 _selectedSizeBundleScroll;
        private readonly USPaginationSettings _sizeAssetPagination = new() { PageToShow = 0, PageSize = 20 };
        private bool _sizeStatsFoldout;
        private int _sizeShowType;
        private int _sizeSortType;
        private readonly Dictionary<BuildLayoutProvider.Archive, USAddressablesArchiveUiState> _sizeArchiveUIStates = new();
        private readonly Dictionary<BuildLayoutProvider.Asset, USAddressablesAssetUiState> _sizeAssetUIStates = new();

        private string _assetsFilterTemp;
        private string _assetsFilter;
        private List<BuildLayoutProvider.Asset> _assetsToShow;
        private Vector2 _assetsScroll;
        private readonly USPaginationSettings _assetsPagination = new() { PageToShow = 0, PageSize = 20 };
        private BuildLayoutProvider.Asset _selectedAsset;
        private Vector2 _selectedAssetScroll;
        private Vector2 _usedByBundlesScroll;

        private List<USAddressablesDuplicateEntry> _duplicatesList;
        private string _duplicatesFilter;
        private Vector2 _duplicatesScroll;
        private readonly USPaginationSettings _duplicatesPagination = new() { PageToShow = 0, PageSize = 20 };
        private int _duplicatesSortType;
        private int _selectedDuplicateIndex = -1;
        private Vector2 _selectedDuplicateScroll;
        private readonly Dictionary<BuildLayoutProvider.Asset, USAddressablesAssetUiState> _dupAssetUIStates = new();

        private string _labelsFilter;
        private Vector2 _labelsScroll;
        private readonly USPaginationSettings _labelsPagination = new() { PageToShow = 0, PageSize = 20 };
        private int _labelsSortType;
        private KeyValuePair<string, BuildLayoutProvider.LabelInfo>? _selectedLabel;
        private Vector2 _selectedLabelScroll;
        private readonly USPaginationSettings _labelAssetPagination = new() { PageToShow = 0, PageSize = 20 };

        private string _comparisonFilter;
        private Vector2 _comparisonScroll;
        private readonly USPaginationSettings _comparisonPagination = new() { PageToShow = 0, PageSize = 20 };
        private BundleLayoutComparisonService.BundleComparisonEntry _selectedComparisonEntry;
        private Vector2 _selectedComparisonEntryScroll;
        private bool _comparisonStatsFoldout;
        private int _comparisonShowType;
        private int _comparisonShowDiffType;
        private string _lastComparisonFolder;

        private Vector2 _settingsScroll;
        private string _newRemotePattern = "";
        private string _newStartupPattern = "";

        public string CategoryId => "addressables";

        public void Bind(AddressablesCategory category)
        {
            _category = category;
        }

        public void DrawHeader(UnityScannerResult result)
        {
            USGUIUtilities.MonochromeMode = Settings.MonochromeWarnings;
            DrawToolbar();

            if (_category.LastResult?.Layout != null && !IsNonAnalysisTab())
                DrawBuildSummaryHeader(_category.LastResult.Layout);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

            switch (_currentTab)
            {
                case SubTab.Groups: DrawGroups(); break;
                case SubTab.Size: DrawSize(); break;
                case SubTab.Assets: DrawAssets(); break;
                case SubTab.Duplicates: DrawDuplicates(); break;
                case SubTab.Labels: DrawLabels(); break;
                case SubTab.Comparison: DrawComparison(); break;
                case SubTab.Settings: DrawSettings(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        public void DrawTopBar(UnityScannerResult result)
        {
        }

        private bool IsNonAnalysisTab()
        {
            return _currentTab == SubTab.Settings;
        }

        private bool CanAnalyze()
        {
            return _category?.LastResult?.Layout != null;
        }

        private BuildLayoutProvider Layout => _category?.LastResult?.Layout;

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var prevTab = _currentTab;
            var canAnalyze = CanAnalyze();

            for (var i = 0; i < _toolbarTabs.Length; i++)
            {
                var (name, tab) = _toolbarTabs[i];
                var isAnalysis = Array.IndexOf(_analysisTabs, tab) >= 0;

                if (isAnalysis)
                    EditorGUI.BeginDisabledGroup(!canAnalyze);

                var isActive = _currentTab == tab;
                var prevBg = GUI.backgroundColor;
                if (isActive) GUI.backgroundColor = new Color(0.6f, 0.8f, 0.6f, 1f);

                if (GUILayout.Button(new GUIContent(name, $"Switch to {name} tab"), EditorStyles.toolbarButton, GUILayout.MinWidth(50f)))
                    _currentTab = tab;

                GUI.backgroundColor = prevBg;

                if (isAnalysis)
                    EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndHorizontal();

            if (!canAnalyze && Array.IndexOf(_analysisTabs, _currentTab) >= 0)
                _currentTab = SubTab.Settings;

            if (prevTab != _currentTab)
            {
                _previousTab = prevTab;
                OnTabSelected(_currentTab);
            }
        }

        private void OnTabSelected(SubTab tab)
        {
            switch (tab)
            {
                case SubTab.Groups:
                    if (Layout != null)
                        SortGroups();
                    break;
                case SubTab.Size:
                    InitSizeBundles();
                    break;
                case SubTab.Assets:
                    ResetAssetsFilter();
                    break;
                case SubTab.Duplicates:
                    RebuildDuplicates();
                    break;
                case SubTab.Labels:
                    _selectedLabel = null;
                    break;
                case SubTab.Comparison:
                    _lastComparisonFolder = EditorPrefs.GetString(ComparisonFolderKey, "Library");
                    break;
            }
        }

        #region Build Summary Header

        private void DrawBuildSummaryHeader(BuildLayoutProvider layout)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label(
                new GUIContent($" {layout.Name}", EditorGUIUtility.FindTexture("BuildSettings.Editor")),
                GUILayout.Height(20f));
            GUILayout.Label($"Total: {EditorUtility.FormatBytes(layout.TotalSize)}", EditorStyles.miniLabel,
                GUILayout.Height(20f));
            GUILayout.Label($"Remote: {EditorUtility.FormatBytes(layout.TotalRemoteSize)}", EditorStyles.miniLabel,
                GUILayout.Height(20f));
            GUILayout.Label($"Groups: {layout.Groups.Count}", EditorStyles.miniLabel, GUILayout.Height(20f));
            GUILayout.Label($"Bundles: {layout.Bundles.Count}", EditorStyles.miniLabel, GUILayout.Height(20f));

            var warningCount = layout.Groups.Sum(g => g.Archives.Sum(b => b.Recommendations.Count));
            GUILayout.Label($"Warnings: {warningCount}", EditorStyles.miniLabel, GUILayout.Height(20f));

            GUILayout.Space(8);

            DrawGateBadges(layout);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGateBadges(BuildLayoutProvider layout)
        {
            var result = _category.LastResult;
            if (result == null) return;

            foreach (var gate in result.GateResults)
            {
                DrawGateBadge(gate.Name, gate.FormattedActual, gate.Pass);
            }
        }

        private static void DrawGateBadge(string label, string value, bool pass)
        {
            var tag = pass ? "PASS" : "FAIL";
            var color = pass ? Color.green : Color.red;
            USGUIUtilities.DrawColoredLabel($"{label}: {value} [{tag}]", color);
        }

        #endregion

        #region Groups

        private void DrawGroups()
        {
            if (Layout == null) return;

            if (_groupsNeedInitialSort || _lastSortedLayout != Layout)
            {
                SortGroups();
                _groupsNeedInitialSort = false;
                _lastSortedLayout = Layout;
            }

            if (_selectedGroup != null)
            {
                DrawGroupDetail(_selectedGroup);
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter Groups:");
            _groupsFilter = GUILayout.TextField(_groupsFilter, GUILayout.Width(550));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var filterLowered = _groupsFilter?.ToLowerInvariant() ?? string.Empty;
            var filtered = !string.IsNullOrEmpty(_groupsFilter)
                ? Layout.Groups.Where(x => x.Name.ToLowerInvariant().Contains(filterLowered)).ToList()
                : Layout.Groups;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Groups found: " + Layout.Groups.Count + ". " +
                            (filtered.Count != Layout.Groups.Count ? $"Showing: {filtered.Count}" : string.Empty));

            var sortLabel = _groupsSortType switch
            {
                0 => "Warnings Desc",
                1 => "Warnings Asc",
                2 => "Name A-Z",
                3 => "Name Z-A",
                _ => "Warnings Desc"
            };
            if (GUILayout.Button(new GUIContent("Sort: " + sortLabel, "Sort groups"), GUILayout.Width(130)))
            {
                _groupsSortType = _groupsSortType >= 3 ? 0 : _groupsSortType + 1;
                if (Layout != null) SortGroups();
            }
            GUILayout.EndHorizontal();

            USGUIPaginationUtilities.DrawPagesWidget(filtered.Count, _groupsPagination);

            _groupsScroll = EditorGUILayout.BeginScrollView(_groupsScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filtered.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _groupsPagination))
                    continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                var group = filtered[i];

                GUILayout.Label($"{i + 1}.", GUILayout.Width(30));
                USGUIUtilities.DrawColoredLabelByWarning(group.Name, group.TopWarning, 350);
                GUILayout.Label(EditorUtility.FormatBytes(group.Size), EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label($"{group.Archives.Count} bundles", EditorStyles.miniLabel, GUILayout.Width(80));

                if (group.TopWarning > 0)
                {
                    var severityTag = GetSeverityTag(group.TopWarning);
                    USGUIUtilities.DrawColoredLabelByWarning(severityTag, group.TopWarning);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Details >>>", "Show detailed information"), GUILayout.Width(100)))
                {
                    _selectedGroupScroll = Vector2.zero;
                    _selectedGroup = group;
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void SortGroups()
        {
            if (Layout?.Groups == null) return;
            Layout.Groups = _groupsSortType switch
            {
                0 => Layout.Groups.OrderByDescending(x => x.TopWarning).ToList(),
                1 => Layout.Groups.OrderBy(x => x.TopWarning).ToList(),
                2 => Layout.Groups.OrderBy(x => x.Name, StringComparer.Ordinal).ToList(),
                3 => Layout.Groups.OrderByDescending(x => x.Name, StringComparer.Ordinal).ToList(),
                _ => Layout.Groups.OrderByDescending(x => x.TopWarning).ToList()
            };
        }

        private void DrawGroupDetail(BuildLayoutProvider.Group group)
        {
            _selectedGroupScroll = EditorGUILayout.BeginScrollView(_selectedGroupScroll);

            DrawBreadcrumb("Back", group.Name, () => { _selectedGroup = null; });

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                $"Size: {EditorUtility.FormatBytes(group.Size)}  |  Bundles: {group.Archives.Count}  |  Startup: {BundleUtilities.IsBundleStartup(group.Name)}",
                EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (group.Archives.Count == 0)
            {
                USGUIUtilities.DrawColoredLabel("Group is empty", Color.red);
            }
            else
            {
                for (var i = 0; i < group.Archives.Count; i++)
                {
                    var bundle = group.Archives[i];
                    var currentBuiltIn = !BundleUtilities.IsBundleRemote(bundle.Name);

                    if (group.Archives.Count == 1 && !_archiveUIStates.ContainsKey(bundle))
                        _bundleDetailsFoldouts[bundle] = true;

                    var detailsOpen = GetFoldout(_bundleDetailsFoldouts, bundle);
                    var newDetailsOpen = EditorGUILayout.Foldout(detailsOpen,
                        $"{bundle.Name}  [{EditorUtility.FormatBytes(bundle.Size)}]  {(currentBuiltIn ? "[built-in]" : "[remote]")}  {GetSeverityTag(bundle.TopWarning)}");
                    SetFoldout(_bundleDetailsFoldouts, bundle, newDetailsOpen);

                    if (!newDetailsOpen)
                        continue;

                    EditorGUI.indentLevel++;

                    GUILayout.Label(
                        $"Size: {EditorUtility.FormatBytes(bundle.Size)}  |  Compression: {bundle.Compression}  |  Assets: {bundle.ExplicitAssets.Count}/{bundle.AllAssets.Count}  |  Built-In by Unity: {bundle.IsBuiltin}  |  Startup: {BundleUtilities.IsBundleStartup(bundle.Name)}");

                    if (bundle.Recommendations.Count > 0)
                    {
                        var recOpen = GetFoldout(_bundleRecommendationsFoldouts, bundle);
                        var newRecOpen = EditorGUILayout.Foldout(recOpen, $"Warnings ({bundle.Recommendations.Count})");
                        SetFoldout(_bundleRecommendationsFoldouts, bundle, newRecOpen);

                        if (newRecOpen)
                        {
                            EditorGUI.indentLevel++;
                            var warnings = bundle.Recommendations
                                .Where(x => x.WarningLevel >= Settings.MinWarningLevelToShow).ToList();
                            for (var w = 0; w < warnings.Count; w++)
                            {
                                EditorGUILayout.BeginHorizontal();
                                USGUIUtilities.DrawColoredLabelByWarning($"  {w + 1}. ", warnings[w].WarningLevel);
                                USGUIUtilities.DrawColoredLabelByWarning(warnings[w].Message,
                                    warnings[w].WarningLevel);
                                GUILayout.FlexibleSpace();
                                EditorGUILayout.EndHorizontal();
                                GUILayout.Space(2);
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    var directDeps = bundle.BundleDependenciesInfos;
                    if (directDeps.Count > 0)
                    {
                        var depsOpen = GetFoldout(_bundleDepsFoldouts, bundle);
                        var newDepsOpen = EditorGUILayout.Foldout(depsOpen,
                            $"Direct Dependencies ({directDeps.Count})");
                        SetFoldout(_bundleDepsFoldouts, bundle, newDepsOpen);

                        if (newDepsOpen)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var dep in directDeps.OrderByDescending(x =>
                                             BundleUtilities.IsBundleRemote(x.DependentBundle.Name))
                                         .ThenByDescending(x => x.DependentBundle.Size))
                            {
                                DrawBundleDependencyInfo(dep, currentBuiltIn);
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    var expandedDeps = bundle.ExpandedBundleDependenciesInfos;
                    if (expandedDeps.Count > 0)
                    {
                        var expOpen = GetFoldout(_bundleExpandedDepsFoldouts, bundle);
                        var newExpOpen = EditorGUILayout.Foldout(expOpen,
                            $"Expanded Dependencies ({expandedDeps.Count})");
                        SetFoldout(_bundleExpandedDepsFoldouts, bundle, newExpOpen);

                        if (newExpOpen)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var dep in expandedDeps.OrderByDescending(x =>
                                             BundleUtilities.IsBundleRemote(
                                                 x.BundleFromExpandedDependencies.Name))
                                         .ThenByDescending(x => x.BundleFromExpandedDependencies.Size))
                            {
                                DrawBundleExpandedDependencyInfo(dep, currentBuiltIn);
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    var bundleUIState = GetArchiveUIState(_archiveUIStates, bundle);
                    if (bundle.ReferencedByBundlesDirectly.Count > 0)
                    {
                        bundleUIState.ReferencedByBundlesDirectlyFoldout = EditorGUILayout.Foldout(
                            bundleUIState.ReferencedByBundlesDirectlyFoldout,
                            $"Referenced By Bundles Directly ({bundle.ReferencedByBundlesDirectly.Count})");

                        if (bundleUIState.ReferencedByBundlesDirectlyFoldout)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var bundleDependency in bundle.ReferencedByBundlesDirectly
                                         .OrderByDescending(x => BundleUtilities.IsBundleRemote(x.Name))
                                         .ThenByDescending(x => x.Size))
                            {
                                DrawBundleDependencyLine(bundleDependency, currentBuiltIn);
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    if (bundle.ReferencedByBundlesExpanded.Count > 0)
                    {
                        bundleUIState.ReferencedByBundlesExpandedFoldout = EditorGUILayout.Foldout(
                            bundleUIState.ReferencedByBundlesExpandedFoldout,
                            $"Referenced By Bundles Expanded ({bundle.ReferencedByBundlesExpanded.Count})");

                        if (bundleUIState.ReferencedByBundlesExpandedFoldout)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var bundleDependency in bundle.ReferencedByBundlesExpanded
                                         .OrderByDescending(x => BundleUtilities.IsBundleRemote(x.Name))
                                         .ThenByDescending(x => x.Size))
                            {
                                DrawBundleDependencyLine(bundleDependency, currentBuiltIn);
                            }

                            EditorGUI.indentLevel--;
                        }
                    }

                    if (bundle.ReferencedByGroups.Count > 0)
                    {
                        USGUIUtilities.DrawColoredLabel("Referenced By Groups:", Color.magenta);
                        EditorGUI.indentLevel++;
                        foreach (var groupDependency in bundle.ReferencedByGroups.OrderByDescending(x => x.Size))
                            DrawGroupDependencyLine(groupDependency);
                        EditorGUI.indentLevel--;
                    }

                    EditorGUI.indentLevel--;
                    USGUIUtilities.HorizontalLine();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBundleDependencyInfo(BuildLayoutProvider.BundleDependencyInfo info, bool currentBuiltIn)
        {
            var bundleDependency = info.DependentBundle;
            var isRemoteDep = currentBuiltIn && BundleUtilities.IsBundleRemote(bundleDependency.Name);
            var warning = isRemoteDep ? 3 : bundleDependency.TopWarning;

            USGUIUtilities.DrawColoredLabelByWarning(
                FormatDependencyLine(bundleDependency), warning);

            if (info.AssetsCrossReferences.Count > 0)
            {
                info.Foldout = EditorGUILayout.Foldout(info.Foldout, "  reasons:");

                if (info.Foldout)
                {
                    EditorGUI.indentLevel++;
                    foreach (var (key, value) in info.AssetsCrossReferences)
                    {
                        if (value.Count == 0) continue;
                        DrawAssetButton(key.Name);
                        USGUIUtilities.DrawColoredLabel(" uses:", Color.yellow);

                        foreach (var asset in value)
                        {
                            EditorGUILayout.BeginHorizontal();
                            USGUIUtilities.DrawColoredLabel(" - ", Color.yellow);
                            DrawAssetButton(asset.Name);
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                        }
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawBundleExpandedDependencyInfo(BuildLayoutProvider.BundleExpandedDependencyInfo info,
            bool currentBuiltIn)
        {
            var bundleDependency = info.BundleFromExpandedDependencies;
            var isRemoteDep = currentBuiltIn && BundleUtilities.IsBundleRemote(bundleDependency.Name);
            var warning = isRemoteDep ? 3 : bundleDependency.TopWarning;

            USGUIUtilities.DrawColoredLabelByWarning(
                FormatDependencyLine(bundleDependency), warning);

            info.Foldout = EditorGUILayout.Foldout(info.Foldout, "  reasons:");

            if (info.Foldout)
            {
                EditorGUI.indentLevel++;
                foreach (var reasonBundle in info.BundlesFromDirectDependencies)
                    USGUIUtilities.DrawColoredLabel($"- referenced by: {reasonBundle.Name}", Color.yellow);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawBundleDependencyLine(BuildLayoutProvider.Archive bundle, bool currentBuiltIn)
        {
            var isRemoteDep = currentBuiltIn && BundleUtilities.IsBundleRemote(bundle.Name);
            var warning = isRemoteDep ? 3 : bundle.TopWarning;
            USGUIUtilities.DrawColoredLabelByWarning(FormatDependencyLine(bundle), warning);
        }

        private void DrawGroupDependencyLine(BuildLayoutProvider.Group group)
        {
            USGUIUtilities.DrawColoredLabelByWarning(FormatGroupDependencyLine(group), group.TopWarning);
        }

        private static string FormatDependencyLine(BuildLayoutProvider.Archive bundle)
        {
            var typeTag = DependencyFormatter.GetDependencyTypeTag(bundle.Name);
            var size = DependencyFormatter.GetDependencySizeString(bundle.Size);
            var deps = DependencyFormatter.GetDependencyCountString(bundle);
            return $"- {bundle.Name} {size} {typeTag} {deps}";
        }

        private static string FormatGroupDependencyLine(BuildLayoutProvider.Group group)
        {
            var typeTag = DependencyFormatter.GetDependencyTypeTag(group.Name);
            var size = DependencyFormatter.GetDependencySizeString(group.Size);
            return $"- {group.Name} {size} {typeTag}";
        }

        #endregion

        #region Size

        private void InitSizeBundles()
        {
            if (Layout == null)
            {
                _sizeBundles = new List<BuildLayoutProvider.Archive>();
                return;
            }

            _sizeBundles = Layout.Bundles.Values.OrderByDescending(x => x.Size).ToList();
        }

        private void DrawSize()
        {
            if (Layout == null) return;

            _sizeStatsFoldout = EditorGUILayout.Foldout(_sizeStatsFoldout, "Total Stats");
            if (_sizeStatsFoldout)
            {
                EditorGUI.indentLevel++;
                GUILayout.Label(
                    $"Total Size: {EditorUtility.FormatBytes(Layout.Bundles.Values.Sum(x => x.Size))}  |  Built-In: {EditorUtility.FormatBytes(Layout.TotalBuiltInSize)}  |  Remote: {EditorUtility.FormatBytes(Layout.TotalRemoteSize)}");
                EditorGUI.indentLevel--;
            }

            if (_selectedSizeBundle != null)
            {
                DrawSizeBundleDetail(_selectedSizeBundle);
                return;
            }

            if (_sizeBundles == null)
                InitSizeBundles();

            DrawSizeFilterSection();

            var filterLowered = _sizeFilter?.ToLowerInvariant() ?? string.Empty;
            var filtered = !string.IsNullOrEmpty(_sizeFilter)
                ? _sizeBundles.Where(x => x.Name.ToLowerInvariant().Contains(filterLowered)).ToList()
                : _sizeBundles;

            if (_sizeShowType == 1)
                filtered = filtered.Where(x => !BundleUtilities.IsBundleRemote(x.Name)).ToList();
            else if (_sizeShowType == 2)
                filtered = filtered.Where(x => BundleUtilities.IsBundleRemote(x.Name)).ToList();

            GUILayout.Label("Bundles found: " + _sizeBundles.Count + ". " +
                            (filtered.Count != _sizeBundles.Count ? $"Showing: {filtered.Count}" : string.Empty));

            USGUIPaginationUtilities.DrawPagesWidget(filtered.Count, _sizePagination);

            _sizeScroll = EditorGUILayout.BeginScrollView(_sizeScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filtered.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _sizePagination))
                    continue;

                var entry = filtered[i];
                var isRemote = BundleUtilities.IsBundleRemote(entry.Name);

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                USGUIUtilities.DrawColoredLabel(entry.Name, Color.white);
                GUILayout.Label(EditorUtility.FormatBytes(entry.Size), EditorStyles.miniLabel, GUILayout.Width(80));
                USGUIUtilities.DrawColoredLabel(isRemote ? "[remote]" : "[built-in]",
                    isRemote ? Color.cyan : Color.gray);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Details >>>", "Show detailed information"), GUILayout.Width(100)))
                {
                    _selectedSizeBundleScroll = Vector2.zero;
                    _selectedSizeBundle = entry;
                    _sizeAssetPagination.PageToShow = 0;
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSizeFilterSection()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter Bundles:");
            _sizeFilter = GUILayout.TextField(_sizeFilter, GUILayout.Width(550));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            var typePostfix = _sizeShowType switch
            {
                1 => "Built-in Only",
                2 => "Remote Only",
                _ => "All"
            };

            if (GUILayout.Button(new GUIContent($"Type: {typePostfix}", "Filter by bundle type")))
                _sizeShowType = _sizeShowType switch { 0 => 1, 1 => 2, _ => 0 };

            var sortPostfix = _sizeSortType == 1 ? "Asc" : "Desc";
            if (GUILayout.Button(new GUIContent($"Sorted by Size: {sortPostfix}", "Sort entries by size")))
            {
                if (_sizeSortType == 1)
                {
                    _sizeSortType = 0;
                    _sizeBundles = _sizeBundles.OrderByDescending(x => x.Size).ToList();
                }
                else
                {
                    _sizeSortType = 1;
                    _sizeBundles = _sizeBundles.OrderBy(x => x.Size).ToList();
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawSizeBundleDetail(BuildLayoutProvider.Archive entry)
        {
            _selectedSizeBundleScroll = EditorGUILayout.BeginScrollView(_selectedSizeBundleScroll);

            DrawBreadcrumb("Back", entry.Name, () => { _selectedSizeBundle = null; });

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                $"Size: {EditorUtility.FormatBytes(entry.Size)}  |  Assets: {entry.ExplicitAssets.Count}/{entry.AllAssets.Count}",
                EditorStyles.boldLabel);

            var sortedTitle = _sizeAssetPagination.SortingOption switch
            {
                1 => "Size Desc",
                2 => "Size Asc",
                3 => "Refs Desc",
                4 => "Refs Asc",
                _ => "Unsorted"
            };

            if (GUILayout.Button(new GUIContent($"Sort: {sortedTitle}", "Change sort order"), GUILayout.Width(100)))
            {
                if (_sizeAssetPagination.SortingOption == 1)
                {
                    _sizeAssetPagination.SortingOption = 2;
                    entry.AllAssets = entry.AllAssets.OrderBy(x => x.Size).ToList();
                }
                else if (_sizeAssetPagination.SortingOption == 2)
                {
                    _sizeAssetPagination.SortingOption = 3;
                    entry.AllAssets = entry.AllAssets.OrderByDescending(x => x.ExternalReferences.Count).ToList();
                }
                else if (_sizeAssetPagination.SortingOption == 3)
                {
                    _sizeAssetPagination.SortingOption = 4;
                    entry.AllAssets = entry.AllAssets.OrderBy(x => x.ExternalReferences.Count).ToList();
                }
                else
                {
                    _sizeAssetPagination.SortingOption = 1;
                    entry.AllAssets = entry.AllAssets.OrderByDescending(x => x.Size).ToList();
                }
            }

            GUILayout.Label("Search:");
            var bundleUIState = GetArchiveUIState(_sizeArchiveUIStates, entry);
            bundleUIState.SearchFilter = GUILayout.TextField(bundleUIState.SearchFilter, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            var searchLowered = bundleUIState.SearchFilter?.ToLowerInvariant() ?? string.Empty;

            GUILayout.Label($"Assets ({entry.AllAssets.Count}):");
            USGUIPaginationUtilities.DrawPagesWidget(entry.AllAssets.Count, _sizeAssetPagination);

            for (var i = 0; i < entry.AllAssets.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _sizeAssetPagination))
                    continue;

                var asset = entry.AllAssets[i];

                if (!string.IsNullOrEmpty(searchLowered))
                {
                    var assetFit = asset.Name.ToLowerInvariant().Contains(searchLowered);
                    var refsFit = asset.ExternalReferences.Any(x =>
                        x.Name.ToLowerInvariant().Contains(searchLowered) ||
                        (x.IncludedInBundle != null &&
                         x.IncludedInBundle.Name.ToLowerInvariant().Contains(searchLowered)));
                    if (!assetFit && !refsFit) continue;
                }

                USGUIUtilities.HorizontalLine();

                EditorGUILayout.BeginHorizontal();
                USGUIUtilities.DrawColoredLabel(
                    $"{asset.Name}  [{EditorUtility.FormatBytes(asset.Size)}]  Explicit:{entry.ExplicitAssets.Contains(asset)}",
                    Color.white);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (asset.IncludedByAsset != null)
                    USGUIUtilities.DrawColoredLabel($"  Included By: {asset.IncludedByAsset.Name}", Color.gray);

                DrawSizeReferencedByBundles(asset);
                DrawSizeExternalReferences(asset);
            }

            GUILayout.FlexibleSpace();
            USGUIUtilities.HorizontalLine();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSizeReferencedByBundles(BuildLayoutProvider.Asset asset)
        {
            var totalRefs = asset.IncludedByBundle.Count(x => x != asset.IncludedInBundle);
            if (totalRefs <= 0) return;

            var assetUIState = GetAssetUIState(_sizeAssetUIStates, asset);
            assetUIState.ReferencedByBundlesFoldout = EditorGUILayout.Foldout(
                assetUIState.ReferencedByBundlesFoldout,
                $"Referenced by Bundles ({totalRefs})");

            if (assetUIState.ReferencedByBundlesFoldout)
            {
                EditorGUI.indentLevel++;
                foreach (var archive in asset.IncludedByBundle.Where(x => x != asset.IncludedInBundle))
                    USGUIUtilities.DrawColoredLabelByWarning(
                        $">>> {archive.Name}  [{EditorUtility.FormatBytes(archive.Size)}]", archive.TopWarning);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawSizeExternalReferences(BuildLayoutProvider.Asset asset)
        {
            if (asset.ExternalReferences.Count <= 0) return;

            var assetUIState = GetAssetUIState(_sizeAssetUIStates, asset);
            assetUIState.ExternalRefsFoldout = EditorGUILayout.Foldout(assetUIState.ExternalRefsFoldout,
                $"External References ({asset.ExternalReferences.Count})" +
                (assetUIState.ShowExternalReferencesToRemoteOnly ? " [remote only]" : ""));

            if (!assetUIState.ExternalRefsFoldout) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(assetUIState.ShowExternalReferencesToRemoteOnly
                    ? new GUIContent("Showing: Remote Only", "Toggle between showing all references or remote only")
                    : new GUIContent("Showing: All", "Toggle between showing all references or remote only"), GUILayout.Width(150)))
                assetUIState.ShowExternalReferencesToRemoteOnly = !assetUIState.ShowExternalReferencesToRemoteOnly;

            GUILayout.Label("Filter:");
            assetUIState.SearchFilter = GUILayout.TextField(assetUIState.SearchFilter, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            var filteredRefs = new List<(BuildLayoutProvider.Asset reference, bool isRemote, int warning)>();

            foreach (var reference in asset.ExternalReferences)
            {
                var isRemote = reference.IncludedInBundle != null &&
                               BundleUtilities.IsBundleRemote(reference.IncludedInBundle.Name);

                if (assetUIState.ShowExternalReferencesToRemoteOnly && !isRemote) continue;

                if (!string.IsNullOrEmpty(assetUIState.SearchFilter))
                {
                    var loweredFilter = assetUIState.SearchFilter.ToLowerInvariant();
                    if (!reference.Name.ToLowerInvariant().Contains(loweredFilter) &&
                        (reference.IncludedInBundle == null ||
                         !reference.IncludedInBundle.Name.ToLowerInvariant().Contains(loweredFilter)))
                        continue;
                }

                var bundleWarning = reference.IncludedInBundle?.TopWarning ?? 0;
                var warning = Mathf.Max(bundleWarning, reference.TopWarning);
                filteredRefs.Add((reference, isRemote, warning));
            }

            if (filteredRefs.Count > 0)
            {
                var scrollHeight = Mathf.Min(filteredRefs.Count * 44 + 20, 300);
                assetUIState.ExternalRefsScroll =
                    EditorGUILayout.BeginScrollView(assetUIState.ExternalRefsScroll, GUILayout.Height(scrollHeight));
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                for (var r = 0; r < filteredRefs.Count; r++)
                {
                    var (reference, isRemote, warning) = filteredRefs[r];
                    EditorGUILayout.BeginHorizontal();
                    USGUIUtilities.DrawColoredLabelByWarning($"  {r + 1}. ", warning);
                    USGUIUtilities.DrawColoredLabelByWarning(
                        $" {reference.Name}  [{(isRemote ? "remote" : "built-in")}]", warning);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    if (reference.IncludedInBundle != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        USGUIUtilities.DrawColoredLabelByWarning(
                            $"     in: {reference.IncludedInBundle.Name}  Dir:{reference.IncludedInBundle.BundleDependencies.Count} Exp:{reference.IncludedInBundle.ExpandedBundleDependencies.Count}",
                            warning);
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                    }

                    GUILayout.Space(2);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndScrollView();
            }

            EditorGUI.indentLevel--;
        }

        #endregion

        #region Assets

        private void ResetAssetsFilter()
        {
            if (Layout == null)
            {
                _assetsToShow = null;
                return;
            }

            _assetsToShow = Layout.AssetsByPath.Values
                .OrderByDescending(a => a.UsedByBundles.Count)
                .ThenByDescending(a => a.Size)
                .ToList();
            _assetsFilter = string.Empty;
        }

        private void DrawAssets()
        {
            if (Layout == null) return;

            if (_selectedAsset != null)
            {
                DrawAssetDetail(_selectedAsset);
                return;
            }

            if (_assetsToShow == null)
                ResetAssetsFilter();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Filter Assets", "Filter assets by name or path")))
            {
                _assetsFilter = _assetsFilterTemp;
                if (string.IsNullOrEmpty(_assetsFilter))
                {
                    ResetAssetsFilter();
                }
                else
                {
                    var filterLowered = _assetsFilter.ToLowerInvariant();
                    _assetsToShow = Layout.AssetsByPath
                        .Where(p => p.Key.ToLowerInvariant().Contains(filterLowered))
                        .Select(p => p.Value)
                        .ToList();
                }
            }

            if (!string.IsNullOrEmpty(_assetsFilter))
            {
                if (GUILayout.Button(new GUIContent("Reset Filter", "Reset the asset filter")))
                    ResetAssetsFilter();
            }

            _assetsFilterTemp = GUILayout.TextField(_assetsFilterTemp, GUILayout.Width(550));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var allAssets = Layout.AssetsByPath;
            GUILayout.Label($"Assets found: {allAssets.Count}. " +
                            (_assetsToShow.Count != allAssets.Count
                                ? $"Showing: {_assetsToShow.Count}"
                                : string.Empty));

            USGUIPaginationUtilities.DrawPagesWidget(_assetsToShow.Count, _assetsPagination);

            _assetsScroll = EditorGUILayout.BeginScrollView(_assetsScroll);
            EditorGUILayout.BeginVertical();

            var i = 0;
            foreach (var asset in _assetsToShow)
            {
                if (asset == null) continue;
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _assetsPagination))
                {
                    i++;
                    continue;
                }

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(Path.GetFileName(asset.Name));
                if (asset.IncludedInBundle != null)
                    USGUIUtilities.DrawColoredLabel($"in: {asset.IncludedInBundle.Name}", Color.gray);
                GUILayout.Label(EditorUtility.FormatBytes(asset.Size), EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Details >>>", "Show detailed information"), GUILayout.Width(100)))
                {
                    _selectedAssetScroll = Vector2.zero;
                    _selectedAsset = asset;
                }

                EditorGUILayout.EndHorizontal();
                i++;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetDetail(BuildLayoutProvider.Asset asset)
        {
            _selectedAssetScroll = EditorGUILayout.BeginScrollView(_selectedAssetScroll);

            DrawBreadcrumb("Back", Path.GetFileName(asset.Name), () => { _selectedAsset = null; });

            var includedInBuiltIn = asset.IncludedInBundle != null &&
                                    !BundleUtilities.IsBundleRemote(asset.IncludedInBundle.Name);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                $"Size: {EditorUtility.FormatBytes(asset.Size)}  |  Built-In: {includedInBuiltIn}  |  GUID: {asset.Guid}",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Label($"Path: {asset.Name}");

            if (asset.IncludedByAsset != null)
                GUILayout.Label($"Included By Asset: {asset.IncludedByAsset.Name}");

            if (asset.IncludedInBundle != null)
            {
                GUILayout.Label($"Included In Bundle: {asset.IncludedInBundle.Name}");

                if (asset.IncludedInBundle.ReferencedByGroups.Count > 0)
                {
                    GUILayout.Label("Bundle Referenced By Groups:");
                    EditorGUI.indentLevel++;
                    foreach (var referencedByGroup in asset.IncludedInBundle.ReferencedByGroups)
                        DrawGroupDependencyLine(referencedByGroup);
                    EditorGUI.indentLevel--;
                }
            }

            USGUIUtilities.HorizontalLine();

            if (asset.InternalReferences.Count > 0)
            {
                GUILayout.Label($"Internal References ({asset.InternalReferences.Count}):");
                EditorGUI.indentLevel++;
                foreach (var internalRef in asset.InternalReferences)
                    GUILayout.Label(internalRef.Name);
                EditorGUI.indentLevel--;
            }

            if (asset.ExternalReferences.Count > 0)
            {
                USGUIUtilities.HorizontalLine();
                GUILayout.Label($"External References ({asset.ExternalReferences.Count}):");
                EditorGUI.indentLevel++;
                foreach (var extRef in asset.ExternalReferences)
                    GUILayout.Label(extRef.Name);
                EditorGUI.indentLevel--;
            }

            if (asset.IncludedByBundle.Count > 0)
            {
                USGUIUtilities.HorizontalLine();
                GUILayout.Label($"Referenced (Included) By Bundles [{asset.IncludedByBundle.Count}]:");
                EditorGUI.indentLevel++;
                foreach (var bundleDep in asset.IncludedByBundle
                             .OrderByDescending(x => BundleUtilities.IsBundleRemote(x.Name))
                             .ThenByDescending(x => x.Size))
                    DrawBundleDependencyLine(bundleDep, includedInBuiltIn);
                EditorGUI.indentLevel--;
            }

            if (asset.UsedByBundles.Count > 0)
            {
                USGUIUtilities.HorizontalLine();
                GUILayout.Label($"Used By Bundles [{asset.UsedByBundles.Count}]:");
                var scrollHeight = Mathf.Min(asset.UsedByBundles.Count * 22 + 10, 300);
                _usedByBundlesScroll =
                    EditorGUILayout.BeginScrollView(_usedByBundlesScroll, GUILayout.Height(scrollHeight));
                EditorGUI.indentLevel++;
                foreach (var bundleDep in asset.UsedByBundles
                             .OrderByDescending(x => BundleUtilities.IsBundleRemote(x.Name))
                             .ThenByDescending(x => x.Size))
                    DrawBundleDependencyLine(bundleDep, includedInBuiltIn);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndScrollView();
            }

            USGUIUtilities.HorizontalLine();
            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Duplicates

        private void RebuildDuplicates()
        {
            _duplicatesList = _category?.LastResult?.Duplicates ?? new List<USAddressablesDuplicateEntry>();
            _selectedDuplicateIndex = -1;
            _duplicatesSortType = 0;
        }

        private void DrawDuplicates()
        {
            if (Layout == null) return;

            if (_duplicatesList == null)
                RebuildDuplicates();

            if (_duplicatesList.Count == 0)
            {
                USGUIUtilities.DrawLabelAtCenterHorizontally("No duplicate assets found", Color.green);
                return;
            }

            if (_selectedDuplicateIndex >= 0 && _selectedDuplicateIndex < _duplicatesList.Count)
            {
                DrawDuplicateDetail(_duplicatesList[_selectedDuplicateIndex]);
                return;
            }

            var totalWasted = _duplicatesList.Sum(x => x.WastedSize);
            USGUIUtilities.DrawColoredLabel(
                $"Total wasted by duplicates: {EditorUtility.FormatBytes(totalWasted)} across {_duplicatesList.Count} assets",
                Color.yellow);

            DrawDuplicatesFilterSection();

            var filterLowered = _duplicatesFilter?.ToLowerInvariant() ?? string.Empty;
            var filtered = !string.IsNullOrEmpty(_duplicatesFilter)
                ? _duplicatesList.Where(x => x.AssetPath.ToLowerInvariant().Contains(filterLowered)).ToList()
                : _duplicatesList;

            GUILayout.Label($"Duplicates: {_duplicatesList.Count}. " +
                            (filtered.Count != _duplicatesList.Count ? $"Showing: {filtered.Count}" : string.Empty));

            USGUIPaginationUtilities.DrawPagesWidget(filtered.Count, _duplicatesPagination);

            _duplicatesScroll = EditorGUILayout.BeginScrollView(_duplicatesScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filtered.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _duplicatesPagination))
                    continue;

                var entry = filtered[i];

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                USGUIUtilities.DrawColoredLabel(Path.GetFileName(entry.AssetPath), Color.white);
                GUILayout.Label($"{entry.Bundles.Count} bundles", EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label(EditorUtility.FormatBytes(entry.WastedSize), EditorStyles.miniLabel,
                    GUILayout.Width(80));

                var reasonTag = GetReasonTag(entry.Reason);
                var reasonColor = GetReasonColor(entry.Reason);
                if (!string.IsNullOrEmpty(reasonTag))
                    USGUIUtilities.DrawColoredLabel(reasonTag, reasonColor);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Details >>>", "Show detailed information"), GUILayout.Width(100)))
                {
                    _selectedDuplicateIndex = i;
                    _selectedDuplicateScroll = Vector2.zero;
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawDuplicatesFilterSection()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter Duplicates:");
            _duplicatesFilter = GUILayout.TextField(_duplicatesFilter, GUILayout.Width(550));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var sortLabel = _duplicatesSortType switch
            {
                0 => "Wasted Size: Desc",
                1 => "Wasted Size: Asc",
                2 => "Count: Desc",
                3 => "Count: Asc",
                _ => "Unsorted"
            };

            if (GUILayout.Button(new GUIContent($"Sort: {sortLabel}", "Change sort order")))
            {
                _duplicatesSortType = _duplicatesSortType switch
                {
                    0 => 1,
                    1 => 2,
                    2 => 3,
                    _ => 0
                };

                _duplicatesList = _duplicatesSortType switch
                {
                    0 => _duplicatesList.OrderByDescending(x => x.WastedSize).ToList(),
                    1 => _duplicatesList.OrderBy(x => x.WastedSize).ToList(),
                    2 => _duplicatesList.OrderByDescending(x => x.Bundles.Count).ToList(),
                    3 => _duplicatesList.OrderBy(x => x.Bundles.Count).ToList(),
                    _ => _duplicatesList
                };
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawDuplicateDetail(USAddressablesDuplicateEntry entry)
        {
            _selectedDuplicateScroll = EditorGUILayout.BeginScrollView(_selectedDuplicateScroll);

            DrawBreadcrumb("Back", Path.GetFileName(entry.AssetPath), () => { _selectedDuplicateIndex = -1; });

            DrawAssetButton(entry.AssetPath);

            EditorGUILayout.BeginHorizontal();
            USGUIUtilities.DrawColoredLabel(
                $"Size: {EditorUtility.FormatBytes(entry.Asset.Size)}  |  Duplicates: {entry.Bundles.Count}  |  Wasted: {EditorUtility.FormatBytes(entry.WastedSize)}",
                Color.yellow);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            USGUIUtilities.DrawColoredLabel($"Reason: {GetReasonTag(entry.Reason)}",
                GetReasonColor(entry.Reason));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Label("Contained in bundles:");
            EditorGUI.indentLevel++;

            for (var i = 0; i < entry.Bundles.Count; i++)
            {
                var bundle = entry.Bundles[i];
                var isRemote = BundleUtilities.IsBundleRemote(bundle.Name);
                var isBuiltin = bundle.IsBuiltin;
                var typeLabel = isRemote ? "[REMOTE]" : isBuiltin ? "[BUILTIN]" : "[BUILT-IN]";
                var isExplicit = bundle.ExplicitAssets.Contains(entry.Asset);
                var includeTag = isExplicit ? "[Explicit]" : "[Pulled-in]";

                EditorGUILayout.BeginHorizontal();
                USGUIUtilities.DrawColoredLabel(
                    $"  {i + 1}. {bundle.Name}  {typeLabel} {includeTag}  [{EditorUtility.FormatBytes(bundle.Size)}]",
                    isRemote ? Color.cyan : isBuiltin ? Color.gray : Color.white);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                var groups = bundle.ReferencedByGroups.Select(g => g.Name);
                EditorGUI.indentLevel++;
                USGUIUtilities.DrawColoredLabel($"Groups: {string.Join(", ", groups)}", Color.gray);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;

            USGUIUtilities.HorizontalLine();

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            USGUIUtilities.DrawColoredLabel("Suggested Fix:", Color.white);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            USGUIUtilities.DrawColoredLabel($"  {entry.SuggestedFix}", new Color(0.7f, 0.85f, 1f));

            if (entry.Asset.ExternalReferences.Count > 0)
            {
                USGUIUtilities.HorizontalLine();

                var assetUIState = GetAssetUIState(_dupAssetUIStates, entry.Asset);
                assetUIState.ExternalRefsFoldout = EditorGUILayout.Foldout(assetUIState.ExternalRefsFoldout,
                    $"External References ({entry.Asset.ExternalReferences.Count})");

                if (assetUIState.ExternalRefsFoldout)
                {
                    EditorGUI.indentLevel++;
                    foreach (var extRef in entry.Asset.ExternalReferences)
                        USGUIUtilities.DrawColoredLabel(
                            $"-> {extRef.Name} (in {(extRef.IncludedInBundle != null ? extRef.IncludedInBundle.Name : "Unknown")})",
                            Color.white);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Labels

        private void DrawLabels()
        {
            if (Layout == null) return;

            if (Layout.Labels.Count == 0)
            {
                USGUIUtilities.DrawLabelAtCenterHorizontally("No labels found in this build layout", Color.yellow);
                return;
            }

            if (_selectedLabel.HasValue)
            {
                DrawLabelDetail(_selectedLabel.Value);
                return;
            }

            DrawLabelList();
        }

        private void DrawLabelList()
        {
            var sortedLabels = GetSortedLabels();

            USGUIUtilities.DrawColoredLabel(
                $"Labels: {Layout.Labels.Count}  |  Labeled assets: {Layout.Labels.Values.Sum(l => l.Assets.Count)}  |  Total labeled size: {EditorUtility.FormatBytes(Layout.Labels.Values.Sum(l => l.TotalSize))}",
                Color.white);

            DrawLabelsFilterSection();

            var filterLowered = _labelsFilter?.ToLowerInvariant() ?? string.Empty;
            var filtered = !string.IsNullOrEmpty(_labelsFilter)
                ? sortedLabels.Where(x => x.Key.ToLowerInvariant().Contains(filterLowered)).ToList()
                : sortedLabels;

            GUILayout.Label($"Labels: {sortedLabels.Count}. " +
                            (filtered.Count != sortedLabels.Count ? $"Showing: {filtered.Count}" : string.Empty));

            USGUIPaginationUtilities.DrawPagesWidget(filtered.Count, _labelsPagination);

            _labelsScroll = EditorGUILayout.BeginScrollView(_labelsScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filtered.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _labelsPagination))
                    continue;

                var kvp = filtered[i];

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                USGUIUtilities.DrawColoredLabel(kvp.Key, Color.white);
                GUILayout.Label($"{kvp.Value.Assets.Count} assets", EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label($"{kvp.Value.Bundles.Count} bundles", EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label(EditorUtility.FormatBytes(kvp.Value.TotalSize), EditorStyles.miniLabel,
                    GUILayout.Width(80));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Details >>>", "Show detailed information"), GUILayout.Width(100)))
                {
                    _selectedLabelScroll = Vector2.zero;
                    _selectedLabel = kvp;
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawLabelDetail(KeyValuePair<string, BuildLayoutProvider.LabelInfo> kvp)
        {
            var info = kvp.Value;
            _selectedLabelScroll = EditorGUILayout.BeginScrollView(_selectedLabelScroll);

            if (GUILayout.Button(new GUIContent("< Back", "Go back to the list view"), EditorStyles.miniButton, GUILayout.Width(100)))
                _selectedLabel = null;

            GUILayout.Label($"Label: {kvp.Key}", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            USGUIUtilities.DrawColoredLabel(
                $"Assets: {info.Assets.Count}  |  Bundles: {info.Bundles.Count}  |  Total Size: {EditorUtility.FormatBytes(info.TotalSize)}",
                Color.white);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            USGUIUtilities.HorizontalLine();

            GUILayout.Label("Bundles containing assets with this label:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var sortedBundles = info.Bundles.OrderByDescending(b => b.Size).ToList();
            for (var i = 0; i < sortedBundles.Count; i++)
            {
                var bundle = sortedBundles[i];
                var isRemote = BundleUtilities.IsBundleRemote(bundle.Name);
                var typeLabel = isRemote ? "[remote]" : bundle.IsBuiltin ? "[builtin]" : "[built-in]";
                var assetsInBundle = info.Assets.Count(a => a.IncludedInBundle == bundle);

                EditorGUILayout.BeginHorizontal();
                USGUIUtilities.DrawColoredLabel(
                    $"  {i + 1}. {bundle.Name}  {typeLabel}  [{EditorUtility.FormatBytes(bundle.Size)}]  Assets with label: {assetsInBundle}",
                    Color.white);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            USGUIUtilities.HorizontalLine();

            GUILayout.Label($"Assets with this label ({info.Assets.Count}):", EditorStyles.boldLabel);
            USGUIPaginationUtilities.DrawPagesWidget(info.Assets.Count, _labelAssetPagination);

            for (var i = 0; i < info.Assets.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _labelAssetPagination))
                    continue;

                var asset = info.Assets[i];
                var bundleName = asset.IncludedInBundle != null ? asset.IncludedInBundle.Name : "Unknown";
                var isRemote = asset.IncludedInBundle != null &&
                               BundleUtilities.IsBundleRemote(asset.IncludedInBundle.Name);

                EditorGUILayout.BeginHorizontal();
                USGUIUtilities.DrawColoredLabel(
                    $"  {i + 1}. {Path.GetFileName(asset.Name)}  [{EditorUtility.FormatBytes(asset.Size)}] in: {bundleName}  {(isRemote ? "[remote]" : "[built-in]")}",
                    Color.white);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndScrollView();
        }

        private List<KeyValuePair<string, BuildLayoutProvider.LabelInfo>> GetSortedLabels()
        {
            if (Layout == null) return new List<KeyValuePair<string, BuildLayoutProvider.LabelInfo>>();
            var labels = Layout.Labels.ToList();
            return _labelsSortType switch
            {
                1 => labels.OrderBy(x => x.Value.Assets.Count).ToList(),
                2 => labels.OrderByDescending(x => x.Value.Assets.Count).ToList(),
                3 => labels.OrderBy(x => x.Value.TotalSize).ToList(),
                4 => labels.OrderByDescending(x => x.Value.TotalSize).ToList(),
                5 => labels.OrderBy(x => x.Value.Bundles.Count).ToList(),
                6 => labels.OrderByDescending(x => x.Value.Bundles.Count).ToList(),
                _ => labels.OrderBy(x => x.Key).ToList()
            };
        }

        private void DrawLabelsFilterSection()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter Labels:");
            _labelsFilter = GUILayout.TextField(_labelsFilter, GUILayout.Width(550));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var sortLabel = _labelsSortType switch
            {
                0 => "Name: Asc",
                1 => "Assets: Asc",
                2 => "Assets: Desc",
                3 => "Size: Asc",
                4 => "Size: Desc",
                5 => "Bundles: Asc",
                6 => "Bundles: Desc",
                _ => "Unsorted"
            };

            if (GUILayout.Button(new GUIContent($"Sort: {sortLabel}", "Change sort order")))
                _labelsSortType = _labelsSortType >= 6 ? 0 : _labelsSortType + 1;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        #endregion

        #region Comparison

        private void DrawComparison()
        {
            var comparisonService = _category?.LastResult?.ComparisonService;
            if (comparisonService == null) return;

            if (comparisonService.OriginalLayout == null)
            {
                USGUIUtilities.DrawLabelAtCenterHorizontally("Please load BuildLayout in Setup tab", Color.white);
                return;
            }

            if (comparisonService.AlternativeLayout == null)
            {
                USGUIUtilities.DrawLabelAtCenterHorizontally(
                    "Load an alternative BuildLayout to compare with the one loaded in Setup tab", Color.white);

                USGUIUtilities.DrawAtCenterHorizontally(() =>
                {
                    if (GUILayout.Button(new GUIContent("Load BuildLayout.txt", "Load an Addressables Build Layout file for analysis")))
                        OpenComparisonFileDialog(comparisonService);
                }, Color.white);

                return;
            }

            if (comparisonService.ComparisonResult == null)
            {
                USGUIUtilities.DrawLabelAtCenterHorizontally(
                    "Error performing comparison. Please re-upload BuildLayout", Color.red);
                return;
            }

            DrawComparisonActions(comparisonService);

            if (comparisonService.ComparisonResult.Count == 0)
            {
                USGUIUtilities.DrawLabelAtCenterHorizontally(
                    "No bundles with similar names found so nothing to compare", Color.yellow);
                return;
            }

            DrawComparisonSummary(comparisonService);

            if (_selectedComparisonEntry != null)
            {
                DrawComparisonEntry(_selectedComparisonEntry);
                return;
            }

            DrawComparisonFilterSection();

            var filterLowered = _comparisonFilter?.ToLowerInvariant() ?? string.Empty;
            var filtered = !string.IsNullOrEmpty(_comparisonFilter)
                ? comparisonService.ComparisonResult.Where(x =>
                    x.OriginalBundle.Name.ToLowerInvariant().Contains(filterLowered)).ToList()
                : comparisonService.ComparisonResult;

            if (_comparisonShowType == 1)
                filtered = filtered.Where(x => !BundleUtilities.IsBundleRemote(x.OriginalBundle.Name)).ToList();
            else if (_comparisonShowType == 2)
                filtered = filtered.Where(x => BundleUtilities.IsBundleRemote(x.OriginalBundle.Name)).ToList();

            if (_comparisonShowDiffType == 1)
                filtered = filtered.Where(x => x.SizeDiffModule != 0 && x.OriginalLarger).ToList();
            else if (_comparisonShowDiffType == 2)
                filtered = filtered.Where(x => x.SizeDiffModule != 0 && !x.OriginalLarger).ToList();

            GUILayout.Label("Bundles matched: " + comparisonService.ComparisonResult.Count + ". " +
                            (filtered.Count != comparisonService.ComparisonResult.Count
                                ? $"Showing: {filtered.Count}"
                                : string.Empty));

            USGUIPaginationUtilities.DrawPagesWidget(filtered.Count, _comparisonPagination);

            _comparisonScroll = EditorGUILayout.BeginScrollView(_comparisonScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filtered.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _comparisonPagination))
                    continue;

                var entry = filtered[i];
                var sign = string.Empty;
                var color = Color.white;

                if (entry.SizeDiffModule != 0)
                {
                    sign = entry.OriginalLarger ? "-" : "+";
                    color = entry.OriginalLarger ? Color.cyan : Color.yellow;
                }

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                USGUIUtilities.DrawColoredLabel(entry.OriginalBundle.Name, color);
                GUILayout.Label($"{sign}{EditorUtility.FormatBytes(entry.SizeDiffModule)}", EditorStyles.miniLabel,
                    GUILayout.Width(90));
                GUILayout.Label($"Orig: {EditorUtility.FormatBytes(entry.OriginalBundle.Size)}", EditorStyles.miniLabel,
                    GUILayout.Width(110));
                GUILayout.Label($"Alt: {EditorUtility.FormatBytes(entry.AlternativeBundle.Size)}", EditorStyles.miniLabel,
                    GUILayout.Width(110));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Details >>>", "Show detailed information"), GUILayout.Width(100)))
                {
                    _selectedComparisonEntryScroll = Vector2.zero;
                    _selectedComparisonEntry = entry;
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawComparisonActions(BundleLayoutComparisonService service)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Comparing: {service.OriginalLayout.Name} vs {service.AlternativeLayout.Name}",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Swap A/B", "Swap comparison sides A and B"), EditorStyles.miniButton, GUILayout.Width(70)))
                service.SwapLayouts();

            if (GUILayout.Button(new GUIContent("Change Alternative", "Select a different build layout for comparison"), EditorStyles.miniButton, GUILayout.Width(210)))
                OpenComparisonFileDialog(service);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawComparisonSummary(BundleLayoutComparisonService service)
        {
            _comparisonStatsFoldout = EditorGUILayout.Foldout(_comparisonStatsFoldout, "Summary");
            if (!_comparisonStatsFoldout) return;

            EditorGUI.indentLevel++;

            GUILayout.Label(
                $"Orig Total: {EditorUtility.FormatBytes(service.OriginalLayout.TotalSize)}  |  Alt Total: {EditorUtility.FormatBytes(service.AlternativeLayout.TotalSize)}  |  Diff: {(service.AlternativeLayout.TotalSize >= service.OriginalLayout.TotalSize ? "+" : "-")}{EditorUtility.FormatBytes(Math.Abs(service.AlternativeLayout.TotalSize - service.OriginalLayout.TotalSize))}");

            var origBundles = service.OriginalLayout.Bundles.Keys.ToHashSet();
            var altBundles = service.AlternativeLayout.Bundles.Keys.ToHashSet();
            var added = altBundles.Except(origBundles).ToList();
            var removed = origBundles.Except(altBundles).ToList();

            if (added.Count > 0)
                USGUIUtilities.DrawColoredLabel(
                    $"Added bundles ({added.Count}): {string.Join(", ", added.Take(10))}{(added.Count > 10 ? "..." : "")}",
                    Color.yellow);
            if (removed.Count > 0)
                USGUIUtilities.DrawColoredLabel(
                    $"Removed bundles ({removed.Count}): {string.Join(", ", removed.Take(10))}{(removed.Count > 10 ? "..." : "")}",
                    Color.cyan);

            var topGrowth = service.ComparisonResult
                .Where(x => !x.OriginalLarger && x.SizeDiffModule > 0)
                .OrderByDescending(x => x.SizeDiffModule)
                .Take(5)
                .ToList();

            if (topGrowth.Count > 0)
            {
                GUILayout.Label("Top growth contributors:");
                EditorGUI.indentLevel++;
                foreach (var entry in topGrowth)
                    USGUIUtilities.DrawColoredLabel(
                        $"+{EditorUtility.FormatBytes(entry.SizeDiffModule)}  {entry.OriginalBundle.Name}",
                        Color.yellow);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        private void DrawComparisonFilterSection()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter Bundles:");
            _comparisonFilter = GUILayout.TextField(_comparisonFilter, GUILayout.Width(550));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var typePostfix = _comparisonShowType switch
            {
                1 => "Built-in Only",
                2 => "Remote Only",
                _ => "All"
            };

            if (GUILayout.Button(new GUIContent($"Type: {typePostfix}", "Filter by bundle type")))
                _comparisonShowType = _comparisonShowType switch { 0 => 1, 1 => 2, _ => 0 };

            var diffPostfix = _comparisonShowDiffType switch
            {
                1 => "Orig Larger Only",
                2 => "Alt Larger Only",
                _ => "All"
            };

            if (GUILayout.Button(new GUIContent($"Diff: {diffPostfix}", "Filter by diff type (added, removed, changed, unchanged)")))
                _comparisonShowDiffType = _comparisonShowDiffType switch { 0 => 1, 1 => 2, _ => 0 };

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawComparisonEntry(BundleLayoutComparisonService.BundleComparisonEntry entry)
        {
            _selectedComparisonEntryScroll = EditorGUILayout.BeginScrollView(_selectedComparisonEntryScroll);

            DrawBreadcrumb("Back", entry.OriginalBundle.Name, () => { _selectedComparisonEntry = null; });

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                $"Orig: {EditorUtility.FormatBytes(entry.OriginalBundle.Size)}  |  Alt: {EditorUtility.FormatBytes(entry.AlternativeBundle.Size)}  |  Diff: {(entry.OriginalLarger ? "-" : "+")}{EditorUtility.FormatBytes(entry.SizeDiffModule)}",
                EditorStyles.boldLabel);

            if (GUILayout.Button(new GUIContent("Sort by Size Desc", "Sort by size in descending order"), GUILayout.Width(120)))
                entry.OriginalBundle.AllAssets =
                    entry.OriginalBundle.AllAssets.OrderByDescending(x => x.Size).ToList();

            GUILayout.EndHorizontal();

            var hasChanges = false;

            for (var i = 0; i < entry.OriginalBundle.AllAssets.Count; i++)
            {
                var originalAsset = entry.OriginalBundle.AllAssets[i];
                var alternativeAsset =
                    entry.AlternativeBundle.AllAssets.FirstOrDefault(x => x.Name == originalAsset.Name);

                if (alternativeAsset != null)
                {
                    if (originalAsset.Size != alternativeAsset.Size)
                    {
                        hasChanges = true;
                        var sizeDiff = (long)Mathf.Abs(alternativeAsset.Size - originalAsset.Size);
                        var sign = originalAsset.Size > alternativeAsset.Size ? "-" : "+";
                        var color = originalAsset.Size > alternativeAsset.Size ? Color.cyan : Color.yellow;

                        EditorGUILayout.BeginHorizontal();
                        USGUIUtilities.DrawColoredLabel(
                            $"{originalAsset.Name}  {sign}{EditorUtility.FormatBytes(sizeDiff)} ({sign}{Mathf.Round((float)sizeDiff / originalAsset.Size * 100f)}%)  Orig:{EditorUtility.FormatBytes(originalAsset.Size)}  Alt:{EditorUtility.FormatBytes(alternativeAsset.Size)}",
                            color);
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    hasChanges = true;
                    USGUIUtilities.DrawColoredLabel(
                        $"? {originalAsset.Name} [{EditorUtility.FormatBytes(originalAsset.Size)}] not in alternative",
                        Color.cyan);
                }
            }

            for (var i = 0; i < entry.AlternativeBundle.AllAssets.Count; i++)
            {
                var alternativeAsset = entry.AlternativeBundle.AllAssets[i];
                var originalAsset =
                    entry.OriginalBundle.AllAssets.FirstOrDefault(x => x.Name == alternativeAsset.Name);

                if (originalAsset == null)
                {
                    hasChanges = true;
                    USGUIUtilities.DrawColoredLabel(
                        $"+ {alternativeAsset.Name} [{EditorUtility.FormatBytes(alternativeAsset.Size)}] new in alternative",
                        Color.yellow);
                }
            }

            if (!hasChanges)
            {
                GUILayout.Space(10);
                USGUIUtilities.DrawLabelAtCenterHorizontally(
                    "No asset differences between Original and Alternative bundles", Color.green);
                GUILayout.Label(
                    $"Both bundles contain {entry.OriginalBundle.AllAssets.Count} identical assets.",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        private void OpenComparisonFileDialog(BundleLayoutComparisonService service)
        {
            var folder = EditorPrefs.GetString(ComparisonFolderKey, "Library");
            var path = EditorUtility.OpenFilePanelWithFilters("Open BuildLayout.txt", folder,
                new[] { "Text Files (*.txt)", "txt" });
            if (string.IsNullOrEmpty(path))
                return;

            _lastComparisonFolder = Path.GetDirectoryName(path);
            EditorPrefs.SetString(ComparisonFolderKey, _lastComparisonFolder);
            service.LoadAlternativeBuildLayout(path, Settings.RemoteDependencyStartupWarningThresholdBytes);
        }

        #endregion

        #region Settings

        private void DrawSettings()
        {
            _settingsScroll = EditorGUILayout.BeginScrollView(_settingsScroll);
            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 220;

            Settings.MinWarningLevelToShow = EditorGUILayout.IntField(
                new GUIContent("Min Warnings Level"), Settings.MinWarningLevelToShow);

            Settings.ShowRelatedBundlesSection = EditorGUILayout.Toggle(
                new GUIContent("Show Related Bundles"), Settings.ShowRelatedBundlesSection);

            var thresholdBytes = EditorGUILayout.LongField(
                new GUIContent("Startup Warn Threshold"), Settings.RemoteDependencyStartupWarningThresholdBytes);
            if (thresholdBytes >= 0)
                Settings.RemoteDependencyStartupWarningThresholdBytes = thresholdBytes;

            Settings.MonochromeWarnings = EditorGUILayout.Toggle(
                new GUIContent("Monochrome Warnings"), Settings.MonochromeWarnings);

            USGUIUtilities.HorizontalLine();

            GUILayout.Label("Quality Gates", EditorStyles.boldLabel);
            GUILayout.Label("Set thresholds to 0 to disable a gate.", EditorStyles.miniLabel);

            var gateTotalSize = EditorGUILayout.LongField(
                new GUIContent("Max Total size (bytes)"), Settings.GateMaxTotalSizeBytes);
            Settings.GateMaxTotalSizeBytes = gateTotalSize >= 0 ? gateTotalSize : 0;

            var gateDupWasted = EditorGUILayout.LongField(
                new GUIContent("Max Duplicate Waste (bytes)"), Settings.GateMaxDuplicateWastedBytes);
            Settings.GateMaxDuplicateWastedBytes = gateDupWasted >= 0 ? gateDupWasted : 0;

            var gateStartupRemote = EditorGUILayout.LongField(
                new GUIContent("Max Startup Remote Deps (bytes)"), Settings.GateMaxStartupRemoteDepsBytes);
            Settings.GateMaxStartupRemoteDepsBytes = gateStartupRemote >= 0 ? gateStartupRemote : 0;

            EditorGUIUtility.labelWidth = prevLabelWidth;

            USGUIUtilities.HorizontalLine();

            DrawPatternList("Remote Bundle Patterns", Settings.RemoteBundlePatterns, ref _newRemotePattern);

            USGUIUtilities.HorizontalLine();

            DrawPatternList("Startup Bundle Patterns", Settings.StartupBundlePatterns, ref _newStartupPattern);

            USGUIUtilities.HorizontalLine();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Reload Settings From Disk", "Reload addressables settings from disk"), GUILayout.Width(200)))
            {
                var loaded = USAddressablesSettingsData.Reload();
                Settings.LoadFromSettings(loaded);
            }

            if (GUILayout.Button(new GUIContent("Save Settings", "Save addressables settings to disk"), GUILayout.Width(200)))
            {
                var target = USAddressablesSettingsData.Load();
                Settings.SaveToSettings(target);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        private static void DrawPatternList(string title, List<string> patterns, ref string newPattern)
        {
            GUILayout.Label(title, EditorStyles.boldLabel);

            for (var i = 0; i < patterns.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"  {patterns[i]}");
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("X", "Remove this ignore pattern"), GUILayout.Width(20)))
                {
                    patterns.RemoveAt(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("  Add:", GUILayout.Width(50));
            newPattern = GUILayout.TextField(newPattern);
            if (GUILayout.Button(new GUIContent("+", "Add a new ignore pattern"), GUILayout.Width(20)))
            {
                var trimmed = newPattern.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !patterns.Contains(trimmed))
                    patterns.Add(trimmed);
                newPattern = "";
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Shared Helpers

        private static void DrawBreadcrumb(string parent, string current, Action onBack)
        {
            if (GUILayout.Button(new GUIContent($"< {parent}", "Navigate to parent group/bundle"), EditorStyles.miniButton, GUILayout.Width(100)))
                onBack();
            GUILayout.Label(current);
        }

        private static void DrawAssetButton(string assetPath, float minWidth = 300f, float height = 18f)
        {
            var selectedObjectType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var selectedObjectContent = EditorGUIUtility.ObjectContent(null, selectedObjectType);
            selectedObjectContent.text = Path.GetFileName(assetPath);

            selectedObjectContent.tooltip = "Click to select this asset in the Project window";

            var alignment = GUI.skin.button.alignment;
            GUI.skin.button.alignment = TextAnchor.MiddleLeft;

            if (GUILayout.Button(selectedObjectContent, GUILayout.MinWidth(minWidth), GUILayout.Height(height)))
                Selection.objects = new[] { AssetDatabase.LoadMainAssetAtPath(assetPath) };

            GUI.skin.button.alignment = alignment;
        }

        private static string GetSeverityTag(int warningLevel)
        {
            if (warningLevel >= 4) return "[CRITICAL]";
            if (warningLevel == 3) return "[HIGH]";
            if (warningLevel == 2) return "[MEDIUM]";
            if (warningLevel == 1) return "[LOW]";
            return "";
        }

        private static string GetReasonTag(DuplicateReason reason)
        {
            return reason switch
            {
                DuplicateReason.ExplicitInclude => "[Explicit Include]",
                DuplicateReason.DependencyPullIn => "[Dependency Pull-in]",
                DuplicateReason.Mixed => "[Mixed]",
                _ => ""
            };
        }

        private static Color GetReasonColor(DuplicateReason reason)
        {
            return reason switch
            {
                DuplicateReason.ExplicitInclude => Color.yellow,
                DuplicateReason.DependencyPullIn => Color.cyan,
                DuplicateReason.Mixed => new Color(1f, 0.6f, 0.2f),
                _ => Color.white
            };
        }

        private bool GetFoldout(Dictionary<BuildLayoutProvider.Archive, bool> dict, BuildLayoutProvider.Archive key)
        {
            dict.TryGetValue(key, out var val);
            return val;
        }

        private void SetFoldout(Dictionary<BuildLayoutProvider.Archive, bool> dict, BuildLayoutProvider.Archive key,
            bool val)
        {
            dict[key] = val;
        }

        private static USAddressablesArchiveUiState GetArchiveUIState(Dictionary<BuildLayoutProvider.Archive, USAddressablesArchiveUiState> dict,
            BuildLayoutProvider.Archive archive)
        {
            if (!dict.TryGetValue(archive, out var uiState))
            {
                uiState = new USAddressablesArchiveUiState();
                dict[archive] = uiState;
            }

            return uiState;
        }

        private static USAddressablesAssetUiState GetAssetUIState(Dictionary<BuildLayoutProvider.Asset, USAddressablesAssetUiState> dict,
            BuildLayoutProvider.Asset asset)
        {
            if (!dict.TryGetValue(asset, out var uiState))
            {
                uiState = new USAddressablesAssetUiState();
                dict[asset] = uiState;
            }

            return uiState;
        }

        private class USAddressablesArchiveUiState
        {
            public bool ReferencedByBundlesDirectlyFoldout;
            public bool ReferencedByBundlesExpandedFoldout;
            public string SearchFilter = string.Empty;
        }

        private class USAddressablesAssetUiState
        {
            public bool ExternalRefsFoldout;
            public bool ReferencedByBundlesFoldout;
            public string SearchFilter = string.Empty;
            public bool ShowExternalReferencesToRemoteOnly;
            public Vector2 ExternalRefsScroll;
        }

        #endregion
    }
}
