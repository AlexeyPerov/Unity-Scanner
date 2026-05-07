using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.Sprite2DAnalysis
{
    public class Sprite2DAnalysisCategory : IUnityScannerCategory
    {
        public string Id => "sprite_2d_analysis";
        public string DisplayName => "Sprite & 2D Analysis";
        public string ShortDisplayName => "Sprites";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly Sprite2DAnalysisSettings _settings = new Sprite2DAnalysisSettings();

        public List<SpriteAtlasData> LastAtlasResults { get; private set; }
        public List<SpriteEntry> LastSpriteResults { get; private set; }
        public List<DuplicateGroup> LastDuplicateResults { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var profile = context?.Settings?.ActivePlatformProfile;

            issueSink.ReportProgress(0f, "Scanning sprites and atlases...");
            yield return null;

            var atlasResults = new List<SpriteAtlasData>();
            var spriteResults = new List<SpriteEntry>();
            var duplicateResults = new List<DuplicateGroup>();

            Sprite2DAnalysisScanner.ScanAll(_settings, profile, atlasResults, spriteResults, duplicateResults, issueSink);

            issueSink.ReportProgress(0.95f, "Mapping issues...");
            yield return null;

            var issues = Sprite2DAnalysisIssueMapper.MapIssues(atlasResults, spriteResults, duplicateResults, _settings, profile);
            issueSink.AddRange(issues);

            LastAtlasResults = atlasResults;
            LastSpriteResults = spriteResults;
            LastDuplicateResults = duplicateResults;

            var errors = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
            var warns = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);

            OutputDescription = "Atlases: " + atlasResults.Count + " | Sprites: " + spriteResults.Count +
                " | Duplicates: " + duplicateResults.Count + ". Issues: " + (errors + warns) + ".";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
