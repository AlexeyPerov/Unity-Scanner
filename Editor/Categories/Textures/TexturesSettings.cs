using System.Collections.Generic;
using UnityScanner.Core.Categories;
using UnityEditor;

namespace UnityScanner.Categories.Textures
{
    public class TexturesSettings : UnityScannerCategorySettings
    {
        public bool MipMapsAreErrors = true;
        public bool ReadableAreErrors = false;
        public bool SizeHigher4KAreErrors = true;
        public bool NoOverridenCompressionAsErrors = true;
        public bool WarnAtlasTexturesDoubleCompression = true;
        public bool TryUseReflectionForAddressablesDetection = false;
        public int GarbageCollectStep = 100000;
        public int DebugLimit = 0;
        public string PathFilter = "";

        public List<TextureImporterFormat> RecommendedFormatsAndroid = new()
        {
            TextureImporterFormat.ASTC_6x6,
            TextureImporterFormat.ASTC_8x8,
            TextureImporterFormat.ASTC_10x10,
            TextureImporterFormat.ASTC_12x12,
            TextureImporterFormat.ETC2_RGBA8Crunched
        };

        public List<TextureImporterFormat> RecommendedFormatsiOS = new()
        {
            TextureImporterFormat.ASTC_6x6,
            TextureImporterFormat.ASTC_8x8,
            TextureImporterFormat.ASTC_10x10,
            TextureImporterFormat.ASTC_12x12,
            TextureImporterFormat.ETC2_RGBA8Crunched
        };
    }
}
