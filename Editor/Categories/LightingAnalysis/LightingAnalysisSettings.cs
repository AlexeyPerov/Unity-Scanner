using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.LightingAnalysis
{
    public class LightingAnalysisSettings : UnityScannerCategorySettings
    {
        public bool CheckRealtimeExceeded = true;
        public bool CheckShadowsOnMobile = true;
        public bool CheckLightmapOversized = true;
        public bool CheckModeInconsistent = true;
        public bool CheckBakedSetToRealtime = true;
        public bool CheckProbeMissing = true;
        public bool CheckReflectionProbeExceeded = true;
        public bool CheckEmissiveNoGI = true;
        public bool CheckPipelineMismatch = true;
        public string PathFilter = "";
    }
}
