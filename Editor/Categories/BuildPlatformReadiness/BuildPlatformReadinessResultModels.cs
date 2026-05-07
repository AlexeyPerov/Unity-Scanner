using System.Collections.Generic;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.BuildPlatformReadiness
{
    public class ImportPolicyViolation : USItemDataBase
    {
        public string AssetPath;
        public string AssetName;
        public string AssetType;
        public string ViolationType;
        public string CurrentValue;
        public string ExpectedValue;
        public string Description;
        public string RecommendedFix;
        public bool Foldout;
    }

    public class PlatformIncompatibility : USItemDataBase
    {
        public string AssetPath;
        public string AssetName;
        public string SettingName;
        public string CurrentValue;
        public string RequiredValue;
        public string Description;
        public string RecommendedFix;
        public bool Foldout;
    }

    public class StrippingRisk : USItemDataBase
    {
        public string ScriptPath;
        public string ScriptName;
        public string RiskType;
        public string Description;
        public string RecommendedFix;
        public List<string> Evidence = new List<string>();
        public bool Foldout;
    }

    public class StartupBudgetStatus : USItemDataBase
    {
        public string Category;
        public long CurrentBytes;
        public long BudgetBytes;
        public float PercentUsed;
        public string Description;
        public string Explanation;
        public string Tooltip;
        public string RecommendedFix;
        public bool Foldout;
    }
}
