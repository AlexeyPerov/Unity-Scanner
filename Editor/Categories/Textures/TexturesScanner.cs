using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityScanner.Core.Export;
using UnityScanner.Core.Issues;
using UnityScanner.UI.Controls;
using UnityScanner.Utilities.Addressables;
using UnityScanner.Utilities.AssetDatabase;
using UnityScanner.Utilities.BuildLayout;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace UnityScanner.Categories.Textures
{
    public static class TexturesScanner
    {
        public const string WarningDuplicateInAddressables =
            "Possible duplicate in build: this texture is addressable and in atlas";

        public const string WarningDuplicateInResources =
            "Possible duplicate in build: this texture is in Resources and in atlas";

        public const string DuplicateInAtlas = "Duplicate in atlas: ";

        public const string AtlasContainsTextureThatExistsInAnotherAtlas =
            "Contains texture {0} that exists in another atlas";

        public const string DimensionsFallbackIssue =
            "Texture is neither POT nor multiple of 4: possible compression issue";

        public static (List<AtlasData> atlases, List<TextureData> textures) ScanAll(
            TexturesSettings settings,
            USLiteBuildLayoutProvider buildLayout,
            IUnityScannerIssueSink issueSink)
        {
            USAddressablesReflection.ClearCache();

            var atlases = new List<AtlasData>();
            var textures = new List<TextureData>();

            var assetPaths = AssetDatabase.GetAllAssetPaths();
            var total = assetPaths.Length;
            var count = 0;

            for (var i = 0; i < assetPaths.Length; i++)
            {
                count++;

                if (settings.GarbageCollectStep > 0 && count % settings.GarbageCollectStep == 0)
                {
                    GC.Collect();
                    issueSink.ReportProgress((float)count / total, "Scanning for atlases");
                }

                var assetPath = assetPaths[i];
                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);

                if (type != typeof(SpriteAtlas))
                    continue;

                atlases.Add(CreateAtlasData(assetPath, buildLayout, settings));

                if (settings.DebugLimit > 0 && atlases.Count > settings.DebugLimit)
                    break;
            }

            for (var i = 0; i < assetPaths.Length; i++)
            {
                count++;

                if (settings.GarbageCollectStep > 0 && count % settings.GarbageCollectStep == 0)
                {
                    GC.Collect();
                    issueSink.ReportProgress((float)count / total, "Scanning for textures");
                }

                var assetPath = assetPaths[i];
                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);

                if (type == null)
                    continue;

                if (type != typeof(Texture) && type != typeof(Texture2D))
                    continue;

                var textureData = CreateTextureData(assetPath, buildLayout, settings);
                var atlasFound = TryProcessAsAtlasTexture(textureData, atlases, settings);

                if (!atlasFound)
                {
                    ProcessAsNonAtlasTexture(textureData, settings);
                    textures.Add(textureData);

                    if (settings.DebugLimit > 0 && textures.Count > settings.DebugLimit)
                        break;
                }
            }

            GC.Collect();

            PostProcessAtlases(atlases, buildLayout);

            atlases.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel));
            textures.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel));

            return (atlases, textures);
        }

        private static string GetBundleName(USLiteBuildLayoutProvider buildLayout, string assetPath)
        {
            return buildLayout != null
                ? buildLayout.GetBundleNameByAssetPath(assetPath)
                : string.Empty;
        }

        private static AtlasData CreateAtlasData(
            string path,
            USLiteBuildLayoutProvider buildLayout,
            TexturesSettings settings)
        {
            var fileInfo = new FileInfo(path);
            var bytesSize = fileInfo.Length;

            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            var typeName = USAssetTypeUtilities.GetReadableTypeName(type);
            var readableSize = USExportUtilities.GetReadableSize(bytesSize);
            var bundle = GetBundleName(buildLayout, path);

            var atlas = EditorGUIUtility.Load(path) as SpriteAtlas;
            var packables = atlas.GetPackables();

            var defaultsAssets = packables.OfType<DefaultAsset>();
            var folders = defaultsAssets.Select(AssetDatabase.GetAssetPath).ToList();

            var packablesDictionary = folders.ToDictionary(folder => folder, _ => new List<TextureData>());

            var directTextures = packables.OfType<Texture2D>();

            foreach (var directTexture in directTextures)
            {
                var textureName = AssetDatabase.GetAssetPath(directTexture);
                if (!packablesDictionary.ContainsKey(textureName))
                {
                    packablesDictionary.Add(textureName, new List<TextureData>());
                }
                else
                {
                    Debug.LogWarning($"Texture name [{textureName}]" +
                                     $" is presented in the atlas [{path}] twice");
                }
            }

            var atlasData = new AtlasData(atlas, path, type, typeName, readableSize,
                packablesDictionary, bundle);

            ProcessSpriteAtlasTexture(atlasData, atlas, settings);

            return atlasData;
        }

        private static TextureData CreateTextureData(
            string path,
            USLiteBuildLayoutProvider buildLayout,
            TexturesSettings settings)
        {
            var fileInfo = new FileInfo(path);
            var bytesSize = fileInfo.Length;

            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            var typeName = USAssetTypeUtilities.GetReadableTypeName(type);
            var readableSize = USExportUtilities.GetReadableSize(bytesSize);
            var bundle = GetBundleName(buildLayout, path);

            return new TextureData(path, type, typeName, bytesSize, readableSize,
                settings.TryUseReflectionForAddressablesDetection, bundle);
        }

        private static bool TryProcessAsAtlasTexture(
            TextureData textureData,
            List<AtlasData> atlases,
            TexturesSettings settings)
        {
            var atlasFound = false;
            AtlasData atlasCandidate = null;
            PackableData packableCandidate = null;

            foreach (var atlas in atlases)
            {
                foreach (var packable in atlas.Packables)
                {
                    var isFolder = !Path.HasExtension(packable.Key);

                    bool isAddedDirectly;
                    bool isAddedViaFolder;

                    if (isFolder)
                    {
                        isAddedDirectly = false;

                        var endsAsOnNix = packable.Key.EndsWith("/");
                        var endsAsOnWindows = packable.Key.EndsWith("\\");

                        if (endsAsOnNix || endsAsOnWindows)
                        {
                            isAddedViaFolder = textureData.Path.Contains(packable.Key);
                        }
                        else
                        {
                            isAddedViaFolder = textureData.Path.Contains(packable.Key + "/") ||
                                               textureData.Path.Contains(packable.Key + "\\");
                        }
                    }
                    else
                    {
                        isAddedViaFolder = false;
                        isAddedDirectly = textureData.Path == packable.Key;
                    }

                    if (isAddedDirectly || isAddedViaFolder)
                    {
                        atlasFound = true;

                        if (atlasCandidate != null)
                        {
                            textureData.AddCustomWarning(
                                $"This texture's links to atlases ({atlas.Name}, {atlasCandidate.Name}) are ambiguous. " +
                                "While Unity probably handles it in a deterministic way we still mark is as a warning because it may be error-prone for users.");
                            textureData.TrySetWarningLevel(2);

                            atlas.AddCustomWarning(
                                $"Atlas has ambiguous packables with atlas {atlasCandidate.Name} and its packable {packableCandidate.Key}");
                            atlas.TrySetWarningLevel(2);

                            atlasCandidate.AddCustomWarning(
                                $"Atlas has ambiguous packables with atlas {atlas.Name} and its packable {packable.Key}");
                            atlasCandidate.TrySetWarningLevel(2);

                            if (packableCandidate.Key.Length > packable.Key.Length)
                            {
                                continue;
                            }
                        }

                        atlasCandidate = atlas;
                        packableCandidate = packable;
                    }
                }
            }

            if (atlasCandidate != null && packableCandidate != null)
            {
                ApplyTextureToAtlas(textureData, atlasCandidate, packableCandidate, settings);
            }

            return atlasFound;
        }

        private static void ApplyTextureToAtlas(
            TextureData textureData,
            AtlasData atlas,
            PackableData packable,
            TexturesSettings settings)
        {
            if (textureData.IsAddressable)
            {
                textureData.AddCustomWarning(WarningDuplicateInAddressables);
                textureData.TrySetWarningLevel(1);
                atlas.TrySetWarningLevel(1);
            }

            if (textureData.Atlas != null)
            {
                textureData.AddCustomWarning(DuplicateInAtlas + textureData.Atlas.Name);
                textureData.TrySetWarningLevel(3);

                textureData.Atlas.TrySetWarningLevel(2);
                textureData.Atlas.AddCustomWarning(
                    string.Format(AtlasContainsTextureThatExistsInAnotherAtlas,
                        textureData.Name));
            }

            textureData.Atlas = atlas;

            if (textureData.InResources)
            {
                textureData.AddCustomWarning(WarningDuplicateInResources);
                textureData.TrySetWarningLevel(2);
            }

            if (settings.WarnAtlasTexturesDoubleCompression)
            {
                ProcessAtlasTextureCompression(textureData, atlas);
            }

            packable.Content.Add(textureData);
        }

        private static void ProcessAtlasTextureCompression(TextureData textureData, AtlasData atlas)
        {
            var importer = textureData.Importer;

            if (importer == null)
                return;

            textureData.ImportSettings["iOS"] = new TexturePlatformImportSettings(importer, "iOS");
            textureData.ImportSettings["Android"] = new TexturePlatformImportSettings(importer, "Android");
            textureData.ImportSettings["Default"] = new TexturePlatformImportSettings(importer, "Default");

            CheckAtlasTextureCompression(textureData, atlas, "Android");
            CheckAtlasTextureCompression(textureData, atlas, "iOS");
        }

        private static void CheckAtlasTextureCompression(
            TextureData textureData,
            AtlasData atlas,
            string platform)
        {
            if (!textureData.ImportSettings.TryGetValue(platform, out var textureSettings) ||
                !atlas.ImportSettings.TryGetValue(platform, out var atlasSettings))
            {
                return;
            }

            if (!TextureUtilities.IsCompressedTextureFormat(textureSettings.FormatActual) ||
                !TextureUtilities.IsCompressedTextureFormat(atlasSettings.FormatActual))
            {
                return;
            }

            textureData.TrySetWarningLevel(1);
            atlas.TrySetWarningLevel(1);
            textureData.AddCustomWarning(
                $"{platform}: source texture and atlas are both compressed; this may cause double-compression artifacts");
        }

        private static void ProcessAsNonAtlasTexture(TextureData textureData, TexturesSettings settings)
        {
            var info = textureData.Info;

            if (info == null)
            {
                textureData.TrySetWarningLevel(2);
                textureData.AddCustomWarning("Unable to load texture info");
                return;
            }

            if (!info.IsPot && !info.IsMultipleOfFour)
            {
                textureData.TrySetWarningLevel(1);
                textureData.AddCustomWarning(DimensionsFallbackIssue);
            }

            if (settings.SizeHigher4KAreErrors && (info.Width > 4096 || info.Height > 4096))
            {
                textureData.TrySetWarningLevel(2);
                textureData.AddCustomWarning("Size over 4096");
            }

            var importer = textureData.Importer;

            if (importer == null)
            {
                textureData.TrySetWarningLevel(2);
                textureData.AddCustomWarning("Unable to load an importer");
                return;
            }

            if (settings.MipMapsAreErrors && importer.mipmapEnabled)
            {
                textureData.TrySetWarningLevel(2);
                textureData.AddCustomWarning("Mipmap is enabled. Is it intended?");
            }

            if (settings.ReadableAreErrors && importer.isReadable)
            {
                textureData.TrySetWarningLevel(2);
                textureData.AddCustomWarning("Texture is readable. Is it intended?");
            }

            var iOSSettings = new TexturePlatformImportSettings(importer, "iOS");
            var androidSettings = new TexturePlatformImportSettings(importer, "Android");

            if (settings.NoOverridenCompressionAsErrors)
            {
                if (iOSSettings.IsUsingDefaultSettings || androidSettings.IsUsingDefaultSettings)
                {
                    textureData.TrySetWarningLevel(2);
                    textureData.AddCustomWarning("Texture uses Automatic compression. Is it intended?");
                }
            }

            textureData.ImportSettings["iOS"] = iOSSettings;
            textureData.ImportSettings["Android"] = androidSettings;

            if (!iOSSettings.IsUsingDefaultSettings &&
                !settings.RecommendedFormatsiOS.Contains(iOSSettings.FormatActual))
            {
                textureData.TrySetWarningLevel(2);
                textureData.AddCustomWarning("iOS: does not use recommended compression");
            }

            if (!androidSettings.IsUsingDefaultSettings &&
                !settings.RecommendedFormatsAndroid.Contains(androidSettings.FormatActual))
            {
                textureData.TrySetWarningLevel(2);
                textureData.AddCustomWarning("Android: does not use recommended compression");
            }

            if (!androidSettings.IsUsingDefaultSettings &&
                !CheckCompressionQuality(androidSettings.FormatActual,
                    androidSettings.Settings.compressionQuality, 30, 50))
            {
                textureData.TrySetWarningLevel(2);
                textureData.AddCustomWarning("Does not use recommended quality");
            }

            if (!iOSSettings.IsUsingDefaultSettings &&
                !CheckCompressionQuality(iOSSettings.FormatActual,
                    iOSSettings.Settings.compressionQuality, 30, 50))
            {
                textureData.TrySetWarningLevel(2);
                textureData.AddCustomWarning("Does not use recommended quality");
            }

            textureData.ImportSettings["Default"] =
                new TexturePlatformImportSettings(importer, "Default");

            foreach (var importSetting in textureData.ImportSettings)
            {
                if (importSetting.Value.ActualFormatAsLoweredString.Contains("crunch") && !info.IsMultipleOfFour)
                {
                    textureData.TrySetWarningLevel(2);
                    textureData.AddCustomWarning(
                        $"{importSetting.Key}: only multiple of 4 textures can use crunch compression");
                }

                if (importSetting.Value.ActualFormatAsLoweredString.Contains("pvrtc") && !info.IsPot)
                {
                    textureData.TrySetWarningLevel(2);
                    textureData.AddCustomWarning(
                        $"{importSetting.Key}: only POT textures can use PVRTC format");
                }
            }
        }

        private static void PostProcessAtlases(
            List<AtlasData> atlases,
            USLiteBuildLayoutProvider buildLayout)
        {
            foreach (var atlas in atlases)
            {
                atlas.UpdateSpritesCount();

                if (atlas.Packables.Count == 0)
                {
                    atlas.TrySetWarningLevel(2);
                    atlas.AddCustomWarning("Packables list is empty");
                }
                else if (atlas.SpritesCount == 0)
                {
                    atlas.TrySetWarningLevel(1);
                    atlas.AddCustomWarning(
                        "Unable to detect sprites. Might be an issue with packables or this tool could not find sprites within subfolders." +
                        "We mark it as a warning because we suggest that this atlas settings might be confusing for users.");
                }
                else if (buildLayout != null)
                {
                    var bundles = new HashSet<string>();

                    if (!string.IsNullOrEmpty(atlas.Bundle))
                        bundles.Add(atlas.Bundle);

                    foreach (var packable in atlas.Packables)
                    {
                        foreach (var textureData in packable.Content)
                        {
                            if (!string.IsNullOrEmpty(textureData.Bundle))
                                bundles.Add(textureData.Bundle);
                        }
                    }

                    if (bundles.Count > 1)
                    {
                        atlas.TrySetWarningLevel(2);
                        atlas.AddCustomWarning("Atlas and/or its textures reside in different bundles");
                    }
                }
            }
        }

        private static bool CheckCompressionQuality(
            TextureImporterFormat formatActual,
            int currentQuality,
            int etc2TargetQuality,
            int astcTargetQuality)
        {
            if (formatActual == TextureImporterFormat.ETC2_RGBA8Crunched)
            {
                if (currentQuality != etc2TargetQuality)
                {
                    return false;
                }
            }
            else if (TextureUtilities.IsAnyAstc(formatActual))
            {
                if (currentQuality != astcTargetQuality)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ProcessSpriteAtlasTexture(
            AtlasData atlasData,
            SpriteAtlas atlas,
            TexturesSettings settings)
        {
            var iOSAutomatic = false;
            var androidAutomatic = false;

            var textureSettings = atlas.GetTextureSettings();

            var defaultPlatformSettings = atlas.GetPlatformSettings("DefaultTexturePlatform");

            if (defaultPlatformSettings == null)
            {
                atlasData.TrySetWarningLevel(2);
                atlasData.AddCustomWarning("Unable to retrieve default importer settings");
                return;
            }

            var defaultSettings =
                new AtlasPlatformImportSettings(defaultPlatformSettings, true, defaultPlatformSettings.format);
            atlasData.ImportSettings["Default"] = defaultSettings;

            var androidPlatformSettings = atlas.GetPlatformSettings("Android");

            if (androidPlatformSettings != null)
            {
                var androidSettings =
                    new AtlasPlatformImportSettings(androidPlatformSettings, false, defaultPlatformSettings.format);
                androidAutomatic = androidSettings.IsUsingDefaultSettings;
                atlasData.ImportSettings["Android"] = androidSettings;

                if (!settings.RecommendedFormatsAndroid.Contains(androidSettings.FormatActual))
                {
                    atlasData.TrySetWarningLevel(2);
                    atlasData.AddCustomWarning("Does not use recommended compression");
                }

                var formatActual = androidSettings.FormatActual;
                var quality = androidSettings.Settings.compressionQuality;

                if (!CheckCompressionQuality(formatActual, quality, 0, 50))
                {
                    atlasData.TrySetWarningLevel(2);
                    atlasData.AddCustomWarning("Does not use recommended quality");
                }
            }

            var iOSPlatformSettings = atlas.GetPlatformSettings("iPhone");

            if (iOSPlatformSettings != null)
            {
                var iOSSettings =
                    new AtlasPlatformImportSettings(iOSPlatformSettings, false, defaultPlatformSettings.format);
                iOSAutomatic = iOSSettings.IsUsingDefaultSettings;
                atlasData.ImportSettings["iOS"] = iOSSettings;

                if (!settings.RecommendedFormatsiOS.Contains(iOSSettings.FormatActual))
                {
                    atlasData.TrySetWarningLevel(2);
                    atlasData.AddCustomWarning("Does not use recommended compression");
                }

                var formatActual = iOSSettings.FormatActual;
                var quality = iOSSettings.Settings.compressionQuality;

                if (!CheckCompressionQuality(formatActual, quality, 0, 50))
                {
                    atlasData.TrySetWarningLevel(2);
                    atlasData.AddCustomWarning("Does not use recommended quality");
                }
            }

            if (settings.MipMapsAreErrors && textureSettings.generateMipMaps)
            {
                atlasData.TrySetWarningLevel(2);
                atlasData.AddCustomWarning("Mipmap is enabled. Is it intended?");
            }

            if (settings.NoOverridenCompressionAsErrors)
            {
                if (iOSAutomatic || androidAutomatic)
                {
                    atlasData.TrySetWarningLevel(2);
                    atlasData.AddCustomWarning("Atlas uses Automatic compression. Is it intended?");
                }
            }
        }

        public static class TextureUtilities
        {
            public static bool IsPowerOfTwo(int x)
            {
                return x != 0 && (x & (x - 1)) == 0;
            }

            public static bool IsAnyAstc(TextureImporterFormat format)
            {
                return format is TextureImporterFormat.ASTC_4x4 or TextureImporterFormat.ASTC_5x5
                    or TextureImporterFormat.ASTC_6x6 or TextureImporterFormat.ASTC_8x8
                    or TextureImporterFormat.ASTC_10x10 or TextureImporterFormat.ASTC_12x12;
            }

            public static bool ShouldApplyAstcMinimum(TextureImporterFormat current, TextureImporterFormat minFormat)
            {
                if (!TryGetAstcIndex(current, out var currentIndex) ||
                    !TryGetAstcIndex(minFormat, out var minIndex))
                    return false;
                return currentIndex < minIndex;
            }

            private static bool TryGetAstcIndex(TextureImporterFormat format, out int index)
            {
                switch (format)
                {
                    case TextureImporterFormat.ASTC_4x4:
                        index = 0;
                        return true;
                    case TextureImporterFormat.ASTC_5x5:
                        index = 1;
                        return true;
                    case TextureImporterFormat.ASTC_6x6:
                        index = 2;
                        return true;
                    case TextureImporterFormat.ASTC_8x8:
                        index = 3;
                        return true;
                    case TextureImporterFormat.ASTC_10x10:
                        index = 4;
                        return true;
                    case TextureImporterFormat.ASTC_12x12:
                        index = 5;
                        return true;
                    default:
                        index = -1;
                        return false;
                }
            }

            public static bool IsCompressedTextureFormat(TextureImporterFormat format)
            {
                if (format == TextureImporterFormat.Automatic)
                    return false;

                if (IsAnyAstc(format))
                    return true;

                var formatName = format.ToString().ToLowerInvariant();
                return formatName.Contains("crunch")
                       || formatName.Contains("etc")
                       || formatName.Contains("eac")
                       || formatName.Contains("dxt")
                       || formatName.Contains("bc")
                       || formatName.Contains("pvrtc")
                       || formatName.Contains("atc");
            }
        }
    }
}
