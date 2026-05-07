using System.Collections.Generic;
using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.MissingReferences
{
    public class MissingReferencesSettings : UnityScannerCategorySettings
    {
        public bool FindUnreferencedOnly = true;
        public bool EnableMissingMethodScan = true;
        public bool EnableTypeMismatchScan = true;
        public bool EnableMissingScriptScan = true;
        public bool EnableDuplicateComponentScan = true;
        public bool EnableInvalidLayerScan = true;
        public string PathFilter = "";
        public HashSet<string> FieldTypesToShow = new HashSet<string>();

        public bool ShowMissingFileIDAndGuid = true;
        public bool ShowMissingGuid = true;
        public bool ShowMissingFileID = false;
        public bool ShowMissingLocalFileID = false;
        public bool ShowEmptyLocalRefs = false;
        public bool ShowFileIDIssues = false;
        public bool ShowMissingMethods = true;
        public bool ShowTypeMismatches = true;
        public bool ShowMissingScripts = true;
        public bool ShowDuplicateComponents = true;
        public bool ShowInvalidLayers = true;
    }
}
