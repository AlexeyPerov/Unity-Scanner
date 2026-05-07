using System.Collections.Generic;
using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.Dependencies
{
    public class DependenciesSettings : UnityScannerCategorySettings
    {
        public bool FindUnreferencedOnly = true;
        public bool ScanForAssetReferences = false;
        public bool TryUseReflectionForAddressablesDetection = false;
        public bool ScanForTerrainDataReferences = false;
        public bool ShowAddressables = false;
        public bool ShowUnreferencedOnly = false;
        public bool ShowPotentialFalsePositivesOnly = false;
        public int SortType = 2;
        public string PathFilter = "";
        public string TypeFilter = "";
        public List<string> CustomIgnorePatterns = new List<string>();
    }
}
