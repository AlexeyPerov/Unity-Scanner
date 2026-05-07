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

namespace UnityScanner.Categories.Textures
{
    public class TexturesTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "textures";
        public System.Action OnScanRequested;

        private const int PageSize = 50;

        private enum OutputFilterType { Textures, Atlases }
        private enum TextureListSortAxis { Warnings, Path, Metric }

        private OutputFilterType _typeFilter;
        private string _pathFilter = "";

        private TextureListSortAxis _atlasSortAxis = TextureListSortAxis.Warnings;
        private bool _atlasSortDescending;
        private bool _atlasWarningsOnly;
        private int? _atlasPage = 0;
        private Vector2 _atlasPagesScroll;
        private Vector2 _atlasScroll;

        private TextureListSortAxis _textureSortAxis = TextureListSortAxis.Warnings;
        private bool _textureSortDescending;
        private bool _textureWarningsOnly;
        private int? _texturePage = 0;
        private Vector2 _texturePagesScroll;
        private Vector2 _textureScroll;

        private bool _analysisSettingsFoldout;

        private TexturesCategory _category;

        public void Bind(TexturesCategory category)
        {
            _category = category;
        }

        public void DrawHeader(UnityScannerResult result)
        {
            DrawAnalysisSettings();
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category == null) return;
            if (_category.LastAtlases == null && _category.LastTextures == null) return;

            var atlases = _category.LastAtlases ?? new List<AtlasData>();
            var textures = _category.LastTextures ?? new List<TextureData>();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_category.OutputDescription ?? "");
            EditorGUILayout.EndHorizontal();

            USGUIUtilities.HorizontalLine();

            EditorGUILayout.BeginHorizontal();
            var prevColor = GUI.color;
            var prevAlignment = GUI.skin.button.alignment;
            GUI.skin.button.alignment = TextAnchor.MiddleLeft;

            GUI.color = _typeFilter == OutputFilterType.Textures ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent($"[{textures.Count}] Textures (non-atlas)", "Switch to textures view"), GUILayout.Width(200f)))
                _typeFilter = OutputFilterType.Textures;

            GUI.color = _typeFilter == OutputFilterType.Atlases ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent($"[{atlases.Count}] Atlases", "Switch to atlases view"), GUILayout.Width(200f)))
                _typeFilter = OutputFilterType.Atlases;

            GUI.skin.button.alignment = prevAlignment;
            GUI.color = prevColor;
            EditorGUILayout.EndHorizontal();

            USGUIUtilities.HorizontalLine();

            EditorGUILayout.BeginHorizontal();
            var textFieldStyle = EditorStyles.textField;
            var prevTextFieldAlignment = textFieldStyle.alignment;
            textFieldStyle.alignment = TextAnchor.MiddleCenter;
            _pathFilter = EditorGUILayout.TextField("Path Contains:", _pathFilter, GUILayout.Width(400f));
            textFieldStyle.alignment = prevTextFieldAlignment;
            EditorGUILayout.EndHorizontal();

            USGUIUtilities.HorizontalLine();

            switch (_typeFilter)
            {
                case OutputFilterType.Atlases:
                    DrawAtlases(atlases);
                    break;
                case OutputFilterType.Textures:
                    DrawTextures(textures);
                    break;
            }
        }

        public void DrawTopBar(UnityScannerResult result)
        {
        }

        private void DrawAnalysisSettings()
        {
            if (_category == null) return;
            var settings = (TexturesSettings)_category.Settings;

            _analysisSettingsFoldout = EditorGUILayout.Foldout(_analysisSettingsFoldout,
                new GUIContent("Analysis Settings", "Toggle which conditions raise warnings."));
            if (!_analysisSettingsFoldout) return;

            USGUIUtilities.HorizontalLine();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent($"No platform overriden compression as error: {settings.NoOverridenCompressionAsErrors}", "Toggle this condition as error/warning in next scan")))
                settings.NoOverridenCompressionAsErrors = !settings.NoOverridenCompressionAsErrors;
            if (GUILayout.Button(new GUIContent($"MipmapEnabled as error: {settings.MipMapsAreErrors}", "Toggle this condition as error/warning in next scan")))
                settings.MipMapsAreErrors = !settings.MipMapsAreErrors;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent($"(Non Atlas) IsReadable as error: {settings.ReadableAreErrors}", "Toggle this condition as error/warning in next scan")))
                settings.ReadableAreErrors = !settings.ReadableAreErrors;
            if (GUILayout.Button(new GUIContent($"(Non Atlas) Height/Width > 4k as error: {settings.SizeHigher4KAreErrors}", "Toggle this condition as error/warning in next scan")))
                settings.SizeHigher4KAreErrors = !settings.SizeHigher4KAreErrors;
            EditorGUILayout.EndHorizontal();

            settings.WarnAtlasTexturesDoubleCompression = EditorGUILayout.ToggleLeft(
                "Warn Atlas Texture Double Compression", settings.WarnAtlasTexturesDoubleCompression);
            settings.TryUseReflectionForAddressablesDetection = EditorGUILayout.ToggleLeft(
                "Try Detect Addressables", settings.TryUseReflectionForAddressablesDetection);

            settings.GarbageCollectStep = EditorGUILayout.IntField("GC once in (iterations):", settings.GarbageCollectStep);
            settings.DebugLimit = EditorGUILayout.IntField("Assets Debug Limit:", settings.DebugLimit);

            USGUIUtilities.HorizontalLine();
            DrawRecommendedFormats("Android", ref settings.RecommendedFormatsAndroid);
            USGUIUtilities.HorizontalLine();
            DrawRecommendedFormats("iOS", ref settings.RecommendedFormatsiOS);
        }

        private void DrawRecommendedFormats(string description, ref List<TextureImporterFormat> recommendedFormats)
        {
            GUILayout.Label($"Recommended Formats ({description})");
            var count = recommendedFormats.Count;

            GUILayout.BeginHorizontal();
            if (count > 0 && GUILayout.Button(new GUIContent("Remove", "Remove the last recommended format entry"))) count--;
            if (GUILayout.Button(new GUIContent("Add", "Add a new recommended format entry"))) count++;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (count != recommendedFormats.Count)
            {
                var newList = new List<TextureImporterFormat>(count);
                for (var i = 0; i < count; i++)
                    newList.Add(i < recommendedFormats.Count ? recommendedFormats[i] : TextureImporterFormat.ASTC_6x6);
                recommendedFormats = newList;
            }

            for (var i = 0; i < recommendedFormats.Count; i++)
                recommendedFormats[i] = (TextureImporterFormat)EditorGUILayout.EnumPopup($"[{i}] Format: {recommendedFormats[i]}", recommendedFormats[i]);
        }

        private void DrawAtlases(List<AtlasData> atlases)
        {
            if (atlases.Count == 0)
            {
                EditorGUILayout.LabelField("No atlases found");
                return;
            }

            EditorGUILayout.BeginHorizontal();
            var prevColor = GUI.color;

            GUI.color = _atlasSortAxis == TextureListSortAxis.Warnings ? Color.yellow : Color.white;
            var orderType = _atlasSortAxis == TextureListSortAxis.Warnings && _atlasSortDescending ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by warnings {orderType}", "Sort entries by this criteria"), GUILayout.Width(150f)))
                SortAtlases(atlases, TextureListSortAxis.Warnings);

            GUI.color = _atlasSortAxis == TextureListSortAxis.Path ? Color.yellow : Color.white;
            orderType = _atlasSortAxis == TextureListSortAxis.Path && _atlasSortDescending ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by path {orderType}", "Sort entries by this criteria"), GUILayout.Width(150f)))
                SortAtlases(atlases, TextureListSortAxis.Path);

            GUI.color = _atlasSortAxis == TextureListSortAxis.Metric ? Color.yellow : Color.white;
            orderType = _atlasSortAxis == TextureListSortAxis.Metric && _atlasSortDescending ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by sprites count {orderType}", "Sort entries by this criteria"), GUILayout.Width(200f)))
                SortAtlases(atlases, TextureListSortAxis.Metric);

            GUI.color = _atlasWarningsOnly ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("Warnings Level 2+ Only", "Toggle: show only entries with warnings level 2 or higher"), GUILayout.Width(250f)))
                _atlasWarningsOnly = !_atlasWarningsOnly;

            GUI.color = prevColor;
            EditorGUILayout.EndHorizontal();

            var filtered = GetFilteredAtlases(atlases);
            DrawPagesWidget(filtered.Count, ref _atlasPage, ref _atlasPagesScroll);
            USGUIUtilities.HorizontalLine();

            _atlasScroll = GUILayout.BeginScrollView(_atlasScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filtered.Count; i++)
            {
                if (_atlasPage.HasValue)
                {
                    var page = _atlasPage.Value;
                    if (i < page * PageSize || i >= (page + 1) * PageSize) continue;
                }

                var asset = filtered[i];
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent(asset.Foldout ? ">Minimize" : ">Expand", "Toggle detailed view for this asset"), GUILayout.Width(70)))
                    asset.Foldout = !asset.Foldout;

                USGUIUtilities.DrawColoredLabelByWarning(i.ToString(), asset.WarningLevel, 40);
                EditorGUILayout.LabelField(asset.TypeName, GUILayout.Width(150f));
                EditorGUILayout.LabelField($"Warning: {asset.WarningLevel}", GUILayout.Width(70f));
                USGUIUtilities.DrawAssetButton(asset.Path);
                EditorGUILayout.LabelField("Sprites: " + asset.SpritesCount, GUILayout.Width(100f));

                if (!string.IsNullOrEmpty(asset.Bundle))
                    EditorGUILayout.LabelField($"[{asset.Bundle}]", GUILayout.Width(250));

                foreach (var importSettings in asset.ImportSettings)
                    EditorGUILayout.LabelField(importSettings.Key + " : " + importSettings.Value.Description, GUILayout.Width(235));

                EditorGUILayout.EndHorizontal();

                if (asset.Foldout)
                {
                    GUILayout.Space(3);
                    EditorGUILayout.LabelField($"Atlas Path: {asset.Path}. Self file size: {asset.ReadableSize}");

                    foreach (var packable in asset.Packables)
                    {
                        var isFolder = !Path.HasExtension(packable.Key);
                        EditorGUILayout.LabelField($"Packable {(isFolder ? "(folder)" : "")}: {packable.Key}");
                        foreach (var textureData in packable.Content)
                            DrawTextureRow(textureData);
                    }

                    USGUIUtilities.HorizontalLine();

                    if (asset.CustomWarnings != null)
                    {
                        EditorGUILayout.LabelField($"Warnings [{asset.CustomWarnings.Count}]:");
                        foreach (var w in asset.CustomWarnings)
                            EditorGUILayout.LabelField(w);
                        USGUIUtilities.HorizontalLine();
                    }
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawTextures(List<TextureData> textures)
        {
            if (textures.Count == 0)
            {
                EditorGUILayout.LabelField("No textures found");
                return;
            }

            EditorGUILayout.BeginHorizontal();
            var prevColor = GUI.color;

            GUI.color = _textureSortAxis == TextureListSortAxis.Warnings ? Color.yellow : Color.white;
            var orderType = _textureSortAxis == TextureListSortAxis.Warnings && _textureSortDescending ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by warnings {orderType}", "Sort entries by this criteria"), GUILayout.Width(150f)))
                SortTextures(textures, TextureListSortAxis.Warnings);

            GUI.color = _textureSortAxis == TextureListSortAxis.Path ? Color.yellow : Color.white;
            orderType = _textureSortAxis == TextureListSortAxis.Path && _textureSortDescending ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by path {orderType}", "Sort entries by this criteria"), GUILayout.Width(150f)))
                SortTextures(textures, TextureListSortAxis.Path);

            GUI.color = _textureSortAxis == TextureListSortAxis.Metric ? Color.yellow : Color.white;
            orderType = _textureSortAxis == TextureListSortAxis.Metric && _textureSortDescending ? "Z-A" : "A-Z";
            if (GUILayout.Button(new GUIContent($"Sort by size {orderType}", "Sort entries by this criteria"), GUILayout.Width(150f)))
                SortTextures(textures, TextureListSortAxis.Metric);

            GUI.color = _textureWarningsOnly ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("Warnings Level 2+ Only", "Toggle: show only entries with warnings level 2 or higher"), GUILayout.Width(250f)))
                _textureWarningsOnly = !_textureWarningsOnly;

            GUI.color = prevColor;
            EditorGUILayout.EndHorizontal();

            var filtered = GetFilteredTextures(textures);
            DrawPagesWidget(filtered.Count, ref _texturePage, ref _texturePagesScroll);
            USGUIUtilities.HorizontalLine();

            _textureScroll = GUILayout.BeginScrollView(_textureScroll);
            EditorGUILayout.BeginVertical();

            for (var i = 0; i < filtered.Count; i++)
            {
                if (_texturePage.HasValue)
                {
                    var page = _texturePage.Value;
                    if (i < page * PageSize || i >= (page + 1) * PageSize) continue;
                }
                DrawTextureRow(filtered[i]);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawTextureRow(TextureData asset)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(asset.Foldout ? "Minimize" : "Expand", "Toggle detailed view for this asset"), GUILayout.Width(70)))
                asset.Foldout = !asset.Foldout;

            USGUIUtilities.DrawColoredLabelByWarning(asset.Name, asset.WarningLevel, 200);
            EditorGUILayout.LabelField(asset.ReadableSize, GUILayout.Width(70f));

            if (asset.Info != null)
            {
                EditorGUILayout.LabelField($"{asset.Info.Width}x{asset.Info.Height}", GUILayout.Width(80));
                var prevColor = GUI.color;
                GUI.color = asset.Info.IsPot ? Color.green : Color.gray;
                EditorGUILayout.LabelField(asset.Info.IsPot ? "POT" : "Non-POT", GUILayout.Width(60));
                GUI.color = asset.Info.IsMultipleOfFour ? Color.green : Color.gray;
                EditorGUILayout.LabelField(asset.Info.IsMultipleOfFour ? "Mul4" : "Non-Mul4", GUILayout.Width(60));
                GUI.color = prevColor;
            }

            if (!string.IsNullOrEmpty(asset.Bundle))
                EditorGUILayout.LabelField($"[{asset.Bundle}]", GUILayout.Width(250));

            foreach (var settings in asset.ImportSettings)
                EditorGUILayout.LabelField(settings.Key + " : " + settings.Value.Description, GUILayout.Width(235));

            EditorGUILayout.EndHorizontal();

            if (!asset.Foldout) return;

            GUILayout.Space(3);
            EditorGUILayout.LabelField($"Path: {asset.Path}");
            USGUIUtilities.HorizontalLine();

            if (asset.CustomWarnings != null)
            {
                EditorGUILayout.LabelField($"Warnings [{asset.CustomWarnings.Count}]:");
                foreach (var w in asset.CustomWarnings)
                    EditorGUILayout.LabelField(w);
                USGUIUtilities.HorizontalLine();
            }
        }

        private void DrawPagesWidget(int assetsCount, ref int? pageToShow, ref Vector2 scroll)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.BeginHorizontal();

            var pagesCount = assetsCount / PageSize + (assetsCount % PageSize > 0 ? 1 : 0);
            var showAllButton = assetsCount <= 150;
            if (!showAllButton && !pageToShow.HasValue && pagesCount > 0)
                pageToShow = 0;

            var prevColor = GUI.color;
            if (showAllButton)
            {
                GUI.color = !pageToShow.HasValue ? Color.yellow : Color.white;
                if (GUILayout.Button(new GUIContent("All", "Show all entries on one page"), GUILayout.Width(30f)))
                    pageToShow = null;
                GUI.color = prevColor;
            }

            for (var i = 0; i < pagesCount; i++)
            {
                prevColor = GUI.color;
                GUI.color = pageToShow == i ? Color.yellow : Color.white;
                if (GUILayout.Button(new GUIContent((i + 1).ToString(), $"Go to page {i + 1}"), GUILayout.Width(30f)))
                    pageToShow = i;
                GUI.color = prevColor;
            }

            if (pageToShow.HasValue && pageToShow > pagesCount - 1)
                pageToShow = pagesCount - 1;
            if (pageToShow.HasValue && pagesCount == 0)
                pageToShow = null;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private void SortAtlases(List<AtlasData> list, TextureListSortAxis axis)
        {
            if (_atlasSortAxis == axis)
                _atlasSortDescending = !_atlasSortDescending;
            else
            {
                _atlasSortAxis = axis;
                _atlasSortDescending = false;
            }

            switch (axis)
            {
                case TextureListSortAxis.Warnings:
                    list.Sort(_atlasSortDescending
                        ? (a, b) => b.WarningLevel.CompareTo(a.WarningLevel)
                        : (a, b) => a.WarningLevel.CompareTo(b.WarningLevel));
                    break;
                case TextureListSortAxis.Path:
                    list.Sort(_atlasSortDescending
                        ? (a, b) => string.Compare(b.Path, a.Path, StringComparison.Ordinal)
                        : (a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));
                    break;
                case TextureListSortAxis.Metric:
                    list.Sort(_atlasSortDescending
                        ? (a, b) => b.SpritesCount.CompareTo(a.SpritesCount)
                        : (a, b) => a.SpritesCount.CompareTo(b.SpritesCount));
                    break;
            }
        }

        private void SortTextures(List<TextureData> list, TextureListSortAxis axis)
        {
            if (_textureSortAxis == axis)
                _textureSortDescending = !_textureSortDescending;
            else
            {
                _textureSortAxis = axis;
                _textureSortDescending = false;
            }

            switch (axis)
            {
                case TextureListSortAxis.Warnings:
                    list.Sort(_textureSortDescending
                        ? (a, b) => b.WarningLevel.CompareTo(a.WarningLevel)
                        : (a, b) => a.WarningLevel.CompareTo(b.WarningLevel));
                    break;
                case TextureListSortAxis.Path:
                    list.Sort(_textureSortDescending
                        ? (a, b) => string.Compare(b.Path, a.Path, StringComparison.Ordinal)
                        : (a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));
                    break;
                case TextureListSortAxis.Metric:
                    list.Sort(_textureSortDescending
                        ? (a, b) => b.BytesSize.CompareTo(a.BytesSize)
                        : (a, b) => a.BytesSize.CompareTo(b.BytesSize));
                    break;
            }
        }

        private List<AtlasData> GetFilteredAtlases(List<AtlasData> atlases)
        {
            var filtered = atlases.AsEnumerable();
            if (_atlasWarningsOnly) filtered = filtered.Where(x => x.WarningLevel > 1);
            if (!string.IsNullOrEmpty(_pathFilter)) filtered = filtered.Where(x => x.Path.Contains(_pathFilter));
            return filtered.ToList();
        }

        private List<TextureData> GetFilteredTextures(List<TextureData> textures)
        {
            var filtered = textures.AsEnumerable();
            if (_textureWarningsOnly) filtered = filtered.Where(x => x.WarningLevel > 1);
            if (!string.IsNullOrEmpty(_pathFilter)) filtered = filtered.Where(x => x.Path.Contains(_pathFilter));
            return filtered.ToList();
        }
    }
}
