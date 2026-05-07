using System;
using System.Collections.Generic;
using UnityScanner.UI.Controls;
using UnityScanner.Utilities.Addressables;
using UnityScanner.Utilities.BuildLayout;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace UnityScanner.Categories.Textures
{
    public class TextureInfo
    {
        public int Width;
        public int Height;
        public bool IsPot;
        public bool IsMultipleOfFour;

        public TextureInfo(int width, int height, bool isPot, bool isMultipleOfFour)
        {
            Width = width;
            Height = height;
            IsPot = isPot;
            IsMultipleOfFour = isMultipleOfFour;
        }
    }

    public class TexturePlatformImportSettings
    {
        public TextureImporterPlatformSettings Settings;
        public TextureImporterFormat FormatSet;
        public TextureImporterFormat FormatActual;
        public int CompressionQuality;
        public string Description;
        public string ActualFormatAsLoweredString;
        public bool IsUsingDefaultSettings;
        public bool IsDefaultPlatform;

        public TexturePlatformImportSettings(TextureImporter importer, string platform)
        {
            IsDefaultPlatform = platform == "Default";
            Settings = IsDefaultPlatform
                ? importer.GetDefaultPlatformTextureSettings()
                : importer.GetPlatformTextureSettings(platform);

            FormatSet = Settings.format;
            CompressionQuality = Settings.compressionQuality;
            IsUsingDefaultSettings = FormatSet == TextureImporterFormat.Automatic;

            if (IsUsingDefaultSettings)
            {
                FormatActual = importer.GetAutomaticFormat(platform);
                Description = FormatActual == TextureImporterFormat.Automatic
                    ? "Automatic"
                    : "Automatic -> " + FormatActual;
            }
            else
            {
                FormatActual = FormatSet;
                Description = FormatActual.ToString();
            }

            Description += $"[Q{CompressionQuality}]";
            ActualFormatAsLoweredString = FormatActual.ToString().ToLowerInvariant();
        }
    }

    public class AtlasPlatformImportSettings
    {
        public TextureImporterPlatformSettings Settings;
        public TextureImporterFormat FormatSet;
        public TextureImporterFormat FormatActual;
        public int CompressionQuality;
        public string Description;
        public bool IsDefaultPlatform;
        public bool IsUsingDefaultSettings;

        public AtlasPlatformImportSettings(
            TextureImporterPlatformSettings settings,
            bool isDefault,
            TextureImporterFormat defaultFormat)
        {
            Settings = settings;
            FormatSet = Settings.format;
            CompressionQuality = Settings.compressionQuality;
            IsDefaultPlatform = isDefault;
            IsUsingDefaultSettings = !Settings.overridden;

            if (!isDefault && IsUsingDefaultSettings)
            {
                FormatActual = defaultFormat;
                Description = FormatActual == TextureImporterFormat.Automatic
                    ? "Automatic"
                    : "Automatic -> " + FormatActual;
            }
            else
            {
                FormatActual = FormatSet;
                Description = FormatActual.ToString();
            }

            Description += $"[Q{CompressionQuality}]";
        }
    }

    public class PackableData
    {
        public string Key;
        public List<TextureData> Content;

        public PackableData(string key, List<TextureData> content)
        {
            Key = key;
            Content = content;
        }
    }

    public class AtlasData : USItemDataBase
    {
        public string Path;
        public string Name => System.IO.Path.GetFileName(Path);
        public Type Type;
        public string TypeName;
        public string ReadableSize;
        public List<PackableData> Packables;
        public bool Foldout;
        public Dictionary<string, AtlasPlatformImportSettings> ImportSettings = new Dictionary<string, AtlasPlatformImportSettings>();
        public int SpritesCount;
        public SpriteAtlas Atlas;
        public string Bundle;

        public AtlasData(SpriteAtlas atlas, string path, Type type, string typeName,
            string readableSize, Dictionary<string, List<TextureData>> packablesDictionary,
            string bundleName)
        {
            Atlas = atlas;
            Path = path;
            Type = type;
            TypeName = typeName;
            ReadableSize = readableSize;
            Bundle = bundleName;
            Packables = new List<PackableData>();
            foreach (var pair in packablesDictionary)
                Packables.Add(new PackableData(pair.Key, pair.Value));
        }

        public void UpdateSpritesCount()
        {
            SpritesCount = 0;
            foreach (var packable in Packables)
                SpritesCount += packable.Content.Count;
        }
    }

    public class TextureData : USItemDataBase
    {
        public string Path;
        public string Name => System.IO.Path.GetFileName(Path);
        public Type Type;
        public string TypeName;
        public long BytesSize;
        public string ReadableSize;
        public bool Foldout;
        public string Bundle;
        public bool InResources;

        private bool _isAddressableCalculated;
        private bool _isAddressable;
        private readonly bool _tryUseReflectionForAddressablesDetection;

        public bool IsAddressable
        {
            get
            {
                if (!_isAddressableCalculated)
                {
                    _isAddressable = USAddressablesReflection.IsAssetAddressable(Path, _tryUseReflectionForAddressablesDetection);
                    _isAddressableCalculated = true;
                }
                return _isAddressable;
            }
        }

        public Dictionary<string, TexturePlatformImportSettings> ImportSettings = new Dictionary<string, TexturePlatformImportSettings>();
        public AtlasData Atlas;

        private TextureImporter _importer;
        private bool _importerLoaded;
        private TextureInfo _info;
        private Texture _texture;
        private bool _textureLoaded;

        public TextureImporter Importer
        {
            get
            {
                if (_importerLoaded) return _importer;
                _importerLoaded = true;
                _importer = AssetImporter.GetAtPath(Path) as TextureImporter;
                return _importer;
            }
        }

        public TextureInfo Info
        {
            get
            {
                if (_info != null) return _info;
                var texture = Texture;
                if (texture == null) return null;

                var width = texture.width;
                var height = texture.height;
                var isPot = TexturesScanner.TextureUtilities.IsPowerOfTwo(width) && TexturesScanner.TextureUtilities.IsPowerOfTwo(height);
                var isMultipleOfFour = width % 4 == 0 && height % 4 == 0;
                _info = new TextureInfo(width, height, isPot, isMultipleOfFour);
                _texture = null;
                return _info;
            }
        }

        private Texture Texture
        {
            get
            {
                if (_textureLoaded) return _texture;
                _textureLoaded = true;
                _texture = EditorGUIUtility.Load(Path) as Texture;
                return _texture;
            }
        }

        public TextureData(string path, Type type, string typeName, long bytesSize,
            string readableSize, bool tryUseReflectionForAddressablesDetection, string bundleName)
        {
            Path = path;
            Type = type;
            TypeName = typeName;
            BytesSize = bytesSize;
            ReadableSize = readableSize;
            _tryUseReflectionForAddressablesDetection = tryUseReflectionForAddressablesDetection;
            InResources = path.Contains("/Resources/");
            Bundle = bundleName;
        }
    }
}
