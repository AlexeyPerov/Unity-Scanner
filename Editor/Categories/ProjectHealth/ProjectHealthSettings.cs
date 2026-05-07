using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.ProjectHealth
{
    public class ProjectHealthSettings : UnityScannerCategorySettings
    {
        public bool CheckEmptyFolders = true;
        public bool CheckOrphanedMeta = true;
        public bool CheckBrokenAssets = true;
        public bool CheckEmptyScenes = true;
        public bool CheckDeepNesting = true;
        public bool CheckLargeFolders = true;
        public int MaxFolderNestingDepth = 8;
        public int MaxFilesPerFolder = 200;
        public string PathFilter = "";
    }
}
