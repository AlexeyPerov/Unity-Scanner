using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.FontTextAnalysis
{
    public class FontTextAnalysisSettings : UnityScannerCategorySettings
    {
        public int MaxAtlasSize = 2048;
        public int MaxFallbackChainDepth = 3;
        public bool DetectAtlasGrowth = true;
        public bool DetectOversizedAtlases = true;
        public bool DetectDeepFallbackChains = true;
        public bool DetectDuplicateFallbackChains = true;
        public bool DetectMissingFontAssignments = true;
        public string PathFilter = "";
    }
}
