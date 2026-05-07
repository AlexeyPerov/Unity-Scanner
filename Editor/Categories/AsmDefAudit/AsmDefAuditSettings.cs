using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.AsmDefAudit
{
    public class AsmDefAuditSettings : UnityScannerCategorySettings
    {
        public bool CheckCircularReferences = true;
        public bool CheckEditorInRuntime = true;
        public bool CheckAutoReferencedOrphan = true;
        public bool CheckPlatformFilterBroad = true;
        public bool CheckPlatformFilterContradict = true;
        public bool CheckDuplicateName = true;
        public bool CheckVersionDefineInvalid = true;
        public string PathFilter = "";
    }
}
