using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.ScenePrefabHealth
{
    public static class ScenePrefabHealthIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(
            List<SceneData> scenes,
            List<PrefabData> prefabs,
            ScenePrefabHealthSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();
            var maxObjects = profile?.MaxSceneObjectCount ?? settings.MaxSceneObjectCount;
            var maxNesting = profile?.MaxPrefabNestingDepth ?? settings.MaxPrefabNestingDepth;
            var maxOverrides = profile?.MaxPrefabOverrideCount ?? settings.MaxPrefabOverrideCount;

            foreach (var scene in scenes)
            {
                if (settings.DetectBrokenReferences)
                {
                    foreach (var br in scene.BrokenReferences)
                    {
                        scene.AddError("Scene '" + scene.Name + "': " + br);
                        issues.Add(MakeIssue("broken_reference",
                            "Scene '" + scene.Name + "': " + br,
                            UnityScannerIssueSeverity.Error, scene.Path));
                    }
                }

                if (settings.DetectHighRiskBootstrap && scene.IsBootstrapScene && scene.TotalObjectCount > maxObjects / 2)
                {
                    scene.AddWarning("Bootstrap scene '" + scene.Name + "' has " + scene.TotalObjectCount + " objects (budget: " + (maxObjects / 2) + ").");
                    issues.Add(MakeIssue("high_risk_bootstrap",
                        "Bootstrap scene '" + scene.Name + "' has " + scene.TotalObjectCount + " objects (budget: " + (maxObjects / 2) + ").",
                        UnityScannerIssueSeverity.Warning, scene.Path));
                }

                if (settings.DetectHierarchyHotspots && scene.TotalObjectCount > maxObjects)
                {
                    scene.AddWarning("Scene '" + scene.Name + "' has " + scene.TotalObjectCount + " objects (budget: " + maxObjects + ").");
                    issues.Add(MakeIssue("scene_object_count",
                        "Scene '" + scene.Name + "' has " + scene.TotalObjectCount + " objects (budget: " + maxObjects + ").",
                        UnityScannerIssueSeverity.Warning, scene.Path));
                }

                if (settings.DetectHierarchyHotspots)
                {
                    foreach (var hotspot in scene.HotspotPaths)
                    {
                        scene.AddWarning("Scene '" + scene.Name + "' hotspot: " + hotspot);
                        issues.Add(MakeIssue("component_hotspot",
                            "Scene '" + scene.Name + "' hotspot: " + hotspot,
                            UnityScannerIssueSeverity.Warning, scene.Path));
                    }
                }

                if (settings.DetectInactiveAntiPatterns && scene.InactiveRendererCount > 0)
                {
                    issues.Add(MakeIssue("inactive_expensive",
                        "Scene '" + scene.Name + "' has " + scene.InactiveRendererCount + " inactive objects with renderers.",
                        UnityScannerIssueSeverity.Verbose, scene.Path));
                }

                if (settings.DetectInactiveAntiPatterns && scene.InactiveObjectCount > settings.MaxInactiveObjectThreshold)
                {
                    issues.Add(MakeIssue("inactive_heavy",
                        "Scene '" + scene.Name + "' has " + scene.InactiveObjectCount + " inactive objects (threshold: " + settings.MaxInactiveObjectThreshold + ").",
                        UnityScannerIssueSeverity.Verbose, scene.Path));
                }
            }

            foreach (var prefab in prefabs)
            {
                if (settings.DetectDeepNesting && prefab.NestingDepth > maxNesting)
                {
                    prefab.AddWarning("Prefab '" + prefab.Name + "' has nesting depth " + prefab.NestingDepth + " (max: " + maxNesting + ").");
                    issues.Add(MakeIssue("deep_nesting",
                        "Prefab '" + prefab.Name + "' has nesting depth " + prefab.NestingDepth + " (max: " + maxNesting + ").",
                        UnityScannerIssueSeverity.Warning, prefab.Path));
                }

                if (settings.DetectOverrideExplosion && prefab.OverrideCount > maxOverrides)
                {
                    prefab.AddWarning("Prefab '" + prefab.Name + "' has " + prefab.OverrideCount + " overrides (max: " + maxOverrides + ").");
                    issues.Add(MakeIssue("override_explosion",
                        "Prefab '" + prefab.Name + "' has " + prefab.OverrideCount + " overrides (max: " + maxOverrides + ").",
                        UnityScannerIssueSeverity.Warning, prefab.Path));
                }
            }

            return issues;
        }

        private static UnityScannerIssue MakeIssue(
            string code, string description, UnityScannerIssueSeverity severity, string assetPath)
        {
            return new UnityScannerIssue
            {
                CategoryId = "scene_prefab_health",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };
        }
    }
}
