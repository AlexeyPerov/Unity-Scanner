using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.AnimationAnalysis
{
    public class AnimationAnalysisCategory : IUnityScannerCategory
    {
        public string Id => "animation_analysis";
        public string DisplayName => "Animations";
        public string ShortDisplayName => DisplayName;
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.ScanFiltered |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly AnimationAnalysisSettings _settings = new AnimationAnalysisSettings();

        public List<AnimatorData> LastAnimators { get; private set; }
        public List<AnimationClipData> LastClips { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var settings = _settings;

            issueSink.ReportProgress(0f, "Scanning animation assets...");
            yield return null;

            var animators = new List<AnimatorData>();
            var clips = new List<AnimationClipData>();

            AnimationAnalysisScanner.ScanAll(settings, animators, clips, issueSink);

            issueSink.ReportProgress(0.9f, "Mapping issues...");
            yield return null;

            var issues = AnimationAnalysisIssueMapper.MapIssues(animators, clips, settings);
            issueSink.AddRange(issues);

            LastAnimators = animators;
            LastClips = clips;

            var errorCount = animators.Count(a => a.WarningLevel >= 3);
            var warnCount = animators.Count(a => a.WarningLevel >= 1 && a.WarningLevel < 3);

            OutputDescription = "Controllers: " + animators.Count + ". Clips: " + clips.Count + ". Errors: " + errorCount + ". Warnings: " + warnCount + ".";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
