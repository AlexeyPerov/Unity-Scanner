using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.ScenePrefabHealth
{
    public class ScenePrefabHealthSettings : UnityScannerCategorySettings
    {
        public int MaxPrefabNestingDepth = 5;
        public int MaxPrefabOverrideCount = 50;
        public int MaxSceneObjectCount = 5000;
        public int MaxComponentCountPerObject = 20;
        public int MaxInactiveObjectThreshold = 100;
        public bool DetectDeepNesting = true;
        public bool DetectOverrideExplosion = true;
        public bool DetectHierarchyHotspots = true;
        public bool DetectBrokenReferences = true;
        public bool DetectInactiveAntiPatterns = true;
        public bool DetectHighRiskBootstrap = true;
        public string PathFilter = "";
    }
}
