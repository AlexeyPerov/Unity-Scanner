using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.AudioAnalysis
{
    public class AudioAnalysisSettings : UnityScannerCategorySettings
    {
        public int MaxClipSizeMB = 10;
        public int MaxStartupAudioTotalMB = 50;
        public bool DetectImportMismatches = true;
        public bool DetectStartupOversized = true;
        public bool DetectDuplicates = true;
        public bool DetectMissingMixerGroups = true;
        public bool DetectChannelSampleRateIssues = true;
        public string PathFilter = "";
    }
}
