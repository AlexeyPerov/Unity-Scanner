using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.ShaderAnalysis
{
    public class ShaderAnalysisSettings : UnityScannerCategorySettings
    {
        public int VariantThreshold = 256;
        public int PassThreshold = 8;
        public int KeywordThreshold = 64;
        public bool DetectErrorShaders = true;
        public bool DetectFallbackShaders = true;
        public bool DetectDuplicateKeywords = true;
        public bool DetectPlatformMismatches = true;
        public bool DetectExpensiveFeatures = true;
        public string PathFilter = "";
    }
}
