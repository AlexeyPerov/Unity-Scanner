using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.BuildPlatformReadiness
{
    public class BuildPlatformReadinessSettings : UnityScannerCategorySettings
    {
        public bool CheckImportPolicies = true;
        public bool CheckPlatformCompatibility = true;
        public bool CheckStartupBudget = true;
        public bool CheckStrippingRisk = true;
        public bool CheckProfileConformance = true;
        public bool CrossReferenceOtherCategories = true;
        public string PathFilter = "";
    }
}
