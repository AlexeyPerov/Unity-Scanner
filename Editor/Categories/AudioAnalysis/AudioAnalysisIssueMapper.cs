using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.AudioAnalysis
{
    public static class AudioAnalysisIssueMapper
    {
        public const string CodeImportMismatch = "import_mismatch";
        public const string CodeStartupOversized = "startup_oversized";
        public const string CodeDuplicateClip = "duplicate_clip";
        public const string CodeMissingMixerGroup = "missing_mixer_group";
        public const string CodeChannelSampleRateIssue = "channel_sample_rate_issue";

        public static List<UnityScannerIssue> MapIssues(
            List<AudioClipData> clips,
            AudioAnalysisSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();
            var maxClipMB = profile != null ? profile.MaxAudioClipSizeMB : settings.MaxClipSizeMB;

            foreach (var clip in clips)
            {
                if (settings.DetectImportMismatches)
                {
                    var sizeMB = clip.FileSizeBytes / (1024.0 * 1024.0);
                    string recommendedLoadType;
                    if (clip.Duration > 30f) recommendedLoadType = "Streaming";
                    else if (clip.Duration > 5f) recommendedLoadType = "CompressedInMemory";
                    else recommendedLoadType = "DecompressOnLoad";

                    if (clip.LoadType != recommendedLoadType && clip.Duration > 0)
                    {
                        issues.Add(MakeIssue(
                            "import_mismatch",
                            "Clip '" + clip.Name + "' (" + clip.Duration.ToString("F1") + "s, " + sizeMB.ToString("F1") + "MB) uses '" + clip.LoadType + "' but '" + recommendedLoadType + "' is recommended for this duration.",
                            UnityScannerIssueSeverity.Warning, clip.Path));
                    }
                }

                if (settings.DetectStartupOversized && clip.FileSizeBytes > maxClipMB * 1024L * 1024L)
                {
                    var sizeMB = clip.FileSizeBytes / (1024.0 * 1024.0);
                    issues.Add(MakeIssue(
                        "startup_oversized",
                        "Clip '" + clip.Name + "' size (" + sizeMB.ToString("F1") + " MB) exceeds budget (" + maxClipMB + " MB).",
                        UnityScannerIssueSeverity.Warning, clip.Path));
                }

                if (settings.DetectDuplicates && clip.IsDuplicate && clip.DuplicatePaths.Count > 0)
                {
                    var sizeMB = clip.FileSizeBytes / (1024.0 * 1024.0);
                    issues.Add(MakeIssue(
                        "duplicate_clip",
                        "Duplicate audio payload (" + sizeMB.ToString("F1") + " MB): '" + clip.Name + "' matches " + clip.DuplicatePaths.Count + " other clip(s).",
                        UnityScannerIssueSeverity.Warning, clip.Path));
                }

                if (settings.DetectMissingMixerGroups && string.IsNullOrEmpty(clip.MixerGroup))
                {
                    issues.Add(MakeIssue(
                        "missing_mixer_group",
                        "Clip '" + clip.Name + "' has no mixer group assignment.",
                        UnityScannerIssueSeverity.Info, clip.Path));
                }

                if (settings.DetectChannelSampleRateIssues)
                {
                    if (clip.Channels > 2)
                    {
                        issues.Add(MakeIssue(
                            "channel_sample_rate_issue",
                            "Clip '" + clip.Name + "' has " + clip.Channels + " channels (stereo or mono recommended).",
                            UnityScannerIssueSeverity.Info, clip.Path));
                    }
                }
            }

            return issues;
        }

        private static UnityScannerIssue MakeIssue(
            string code, string description, UnityScannerIssueSeverity severity,
            string assetPath)
        {
            return new UnityScannerIssue
            {
                CategoryId = "audio_analysis",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };
        }
    }
}
