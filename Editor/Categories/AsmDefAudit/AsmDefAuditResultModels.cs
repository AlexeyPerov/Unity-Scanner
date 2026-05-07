using System.Collections.Generic;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.AsmDefAudit
{
    public class AsmDefData : USItemDataBase
    {
        public string AssemblyPath;
        public string AssemblyName;
        public string RootNamespace;
        public List<string> References = new List<string>();
        public List<string> IncludePlatforms = new List<string>();
        public List<string> ExcludePlatforms = new List<string>();
        public bool AutoReferenced;
        public bool AnyPlatform = true;
        public List<VersionDefineData> VersionDefines = new List<VersionDefineData>();
        public bool IsEditorOnly;
        public bool Foldout;
    }

    public class VersionDefineData
    {
        public string Package;
        public string Expression;
        public string Symbol;
    }
}
