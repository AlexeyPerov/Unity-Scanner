using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.AudioAnalysis
{
    public class AudioAnalysisCategory : IUnityScannerCategory
    {
        public string Id => "audio_analysis";
        public string DisplayName => "Audio";
        public string ShortDisplayName => DisplayName;
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.ScanFiltered |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly AudioAnalysisSettings _settings = new AudioAnalysisSettings();

        public List<AudioClipData> LastClips { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var settings = _settings;
            var profile = context?.Settings?.ActivePlatformProfile;

            issueSink.ReportProgress(0f, "Scanning audio clips...");
            yield return null;

            var clips = new List<AudioClipData>();
            AudioAnalysisScanner.ScanAll(settings, profile, clips, issueSink);

            issueSink.ReportProgress(0.9f, "Mapping issues...");
            yield return null;

            var issues = AudioAnalysisIssueMapper.MapIssues(clips, settings, profile);
            issueSink.AddRange(issues);

            LastClips = clips;

            var totalMB = clips.Sum(c => c.FileSizeBytes) / (1024.0 * 1024.0);
            OutputDescription = $"Clips: {clips.Count}. Total size: {totalMB:F1} MB.";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
