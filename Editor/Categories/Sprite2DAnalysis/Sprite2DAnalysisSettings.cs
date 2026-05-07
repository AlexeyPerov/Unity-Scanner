using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.Sprite2DAnalysis
{
    public class Sprite2DAnalysisSettings : UnityScannerCategorySettings
    {
        public bool CheckAtlasEfficiency = true;
        public bool CheckNotPacked = true;
        public bool CheckAtlasPlatformInconsistent = true;
        public bool CheckPolygonVerticesExcessive = true;
        public bool CheckSheetUnevenCells = true;
        public bool CheckFullRectUnnecessary = true;
        public bool CheckDuplicateContent = true;
        public string PathFilter = "";
    }
}
