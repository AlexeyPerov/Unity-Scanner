using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.ScenePrefabHealth
{
    public class ScenePrefabHealthCategory : IUnityScannerCategory
    {
        public string Id => "scene_prefab_health";
        public string DisplayName => "Scenes and Prefabs Health";
        public string ShortDisplayName => "Scenes and Prefabs";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.ScanFiltered |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly ScenePrefabHealthSettings _settings = new ScenePrefabHealthSettings();

        public List<SceneData> LastScenes { get; private set; }
        public List<PrefabData> LastPrefabs { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var settings = _settings;
            var profile = context?.Settings?.ActivePlatformProfile;

            issueSink.ReportProgress(0f, "Scanning scenes and prefabs...");
            yield return null;

            var scenes = new List<SceneData>();
            var prefabs = new List<PrefabData>();

            ScenePrefabHealthScanner.ScanAll(settings, profile, scenes, prefabs, issueSink);

            issueSink.ReportProgress(0.95f, "Mapping issues...");
            yield return null;

            var issues = ScenePrefabHealthIssueMapper.MapIssues(scenes, prefabs, settings, profile);
            issueSink.AddRange(issues);

            LastScenes = scenes;
            LastPrefabs = prefabs;

            var errorCount = scenes.Count(s => s.WarningLevel >= 3) + prefabs.Count(p => p.WarningLevel >= 3);
            var warnCount = scenes.Count(s => s.WarningLevel >= 1 && s.WarningLevel < 3) +
                           prefabs.Count(p => p.WarningLevel >= 1 && p.WarningLevel < 3);

            OutputDescription = "Scenes: " + scenes.Count + ". Prefabs: " + prefabs.Count + ". Errors: " + errorCount + ". Warnings: " + warnCount + ".";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }
    }
}
