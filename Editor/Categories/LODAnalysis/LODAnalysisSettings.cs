using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.LODAnalysis
{
    public class LODAnalysisSettings : UnityScannerCategorySettings
    {
        public bool CheckMissingLevels = true;
        public bool CheckNullRenderers = true;
        public bool CheckRendererCountMismatch = true;
        public bool CheckLastLevelComplex = true;
        public bool CheckMaterialMismatch = true;
        public bool CheckTransitionTooClose = true;
        public bool CheckNoCrossfade = true;
        public bool CheckUnnecessary = true;
        public string PathFilter = "";
    }
}
