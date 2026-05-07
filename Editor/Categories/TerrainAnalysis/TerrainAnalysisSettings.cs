using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.TerrainAnalysis
{
    public class TerrainAnalysisSettings : UnityScannerCategorySettings
    {
        public int ControlMapMemoryBudgetMB = 64;
        public int MaxTerrainTextureSize = 2048;
        public int TreeDensityThreshold = 500;
        public int DetailDensityThreshold = 1000;
        public bool DetectMissingLayers = true;
        public bool DetectColliderMismatches = true;
        public bool DetectTextureBudgetOverages = true;
        public bool DetectDensityOverages = true;
        public bool DetectExpensiveSettings = true;
        public string PathFilter = "";
    }
}
