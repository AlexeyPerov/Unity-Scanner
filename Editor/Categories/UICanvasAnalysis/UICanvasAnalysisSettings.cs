using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.UICanvasAnalysis
{
    public class UICanvasAnalysisSettings : UnityScannerCategorySettings
    {
        public bool CheckUnusedShaderChannels = true;
        public bool CheckNestedRedundancy = true;
        public bool CheckRaycastTargets = true;
        public bool CheckTextTmpMix = true;
        public bool CheckLayoutNesting = true;
        public bool CheckVertexCount = true;
        public bool CheckAtlasWaste = true;
        public bool ScanPrefabs = false;
        public string PathFilter = "";
    }
}
