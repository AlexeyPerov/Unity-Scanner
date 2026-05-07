using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.Materials
{
    public class MaterialsSettings : UnityScannerCategorySettings
    {
        public bool DefaultMaterialsAreErrors = true;
        public bool NullMaterialsAreErrors = false;
        public bool DefaultTexturesAreErrors = true;
        public bool NullTexturesAreErrors = false;
        public bool DuplicateMaterialsAreErrors = true;
        public bool UnusedMaterialsAreErrors = true;
        public bool BuiltinShadersAreErrors = true;
        public bool VariantChainsAreErrors = true;
        public bool VariantHeavyOverridesAreErrors = true;
        public int VariantDeepChainThreshold = 3;
        public int VariantHeavyOverridesThreshold = 8;
        public bool InstancingDisabledAreErrors = true;
        public bool SrpBatcherIncompatibleAreErrors = true;
        public bool TryUseReflectionForAddressablesDetection = false;
        public int GarbageCollectStep = 100000;
        public int DebugLimit = 0;
        public string PathFilter = "";
    }
}
