using System.Collections.Generic;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.TerrainAnalysis
{
    public static class TerrainAnalysisIssueMapper
    {
        public const string CodeColliderMismatch = "collider_mismatch";
        public const string CodeMissingLayer = "missing_layer";
        public const string CodeControlMapOverBudget = "control_map_over_budget";
        public const string CodeTextureOverBudget = "texture_over_budget";
        public const string CodeTreeDensityOverBudget = "tree_density_over_budget";
        public const string CodeDetailDensityOverBudget = "detail_density_over_budget";
        public const string CodeExpensiveSettings = "expensive_settings";

        public static List<UnityScannerIssue> MapIssues(
            List<TerrainDataInfo> terrains,
            TerrainAnalysisSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();

            var controlBudgetMB = profile?.MaxTerrainControlMapMemoryMB ?? settings.ControlMapMemoryBudgetMB;
            var textureBudget = profile?.MaxTerrainTextureSize ?? settings.MaxTerrainTextureSize;
            var treeBudget = profile?.MaxTreeDensity ?? settings.TreeDensityThreshold;
            var detailBudget = profile?.MaxDetailDensity ?? settings.DetailDensityThreshold;

            foreach (var terrain in terrains)
            {
                if (settings.DetectColliderMismatches && terrain.HasColliderMismatch)
                {
                    issues.Add(MakeIssue(CodeColliderMismatch,
                        $"Terrain collider data mismatch at '{terrain.Name}'.",
                        UnityScannerIssueSeverity.Error, terrain.Path));
                }

                if (settings.DetectMissingLayers && terrain.MissingLayerCount > 0)
                {
                    issues.Add(MakeIssue(CodeMissingLayer,
                        $"Terrain '{terrain.Name}' has {terrain.MissingLayerCount} missing layer(s): {string.Join(", ", terrain.MissingLayerNames)}.",
                        UnityScannerIssueSeverity.Error, terrain.Path,
                        "missing_count", terrain.MissingLayerCount));
                }

                if (settings.DetectTextureBudgetOverages)
                {
                    var controlMB = terrain.ControlMapMemoryBytes / (1024.0 * 1024.0);
                    if (controlMB > controlBudgetMB)
                    {
                        issues.Add(MakeIssue(CodeControlMapOverBudget,
                            $"Terrain '{terrain.Name}' control map memory ({controlMB:F1} MB) exceeds budget ({controlBudgetMB} MB).",
                            UnityScannerIssueSeverity.Warning, terrain.Path,
                            "actual_mb", controlMB.ToString("F1"),
                            "budget_mb", controlBudgetMB.ToString()));
                    }

                    foreach (var texSize in terrain.AlphamapTextureSizes)
                    {
                        if (texSize > textureBudget)
                        {
                            issues.Add(MakeIssue(CodeTextureOverBudget,
                                $"Terrain '{terrain.Name}' alphamap texture size ({texSize}) exceeds budget ({textureBudget}).",
                                UnityScannerIssueSeverity.Warning, terrain.Path,
                                "actual_size", texSize,
                                "budget_size", textureBudget));
                            break;
                        }
                    }
                }

                if (settings.DetectDensityOverages)
                {
                    if (terrain.TreeCount > treeBudget)
                    {
                        issues.Add(MakeIssue(CodeTreeDensityOverBudget,
                            $"Terrain '{terrain.Name}' tree count ({terrain.TreeCount}) exceeds budget ({treeBudget}).",
                            UnityScannerIssueSeverity.Info, terrain.Path,
                            "actual_count", terrain.TreeCount,
                            "budget", treeBudget));
                    }

                    if (terrain.DetailCount > detailBudget)
                    {
                        issues.Add(MakeIssue(CodeDetailDensityOverBudget,
                            $"Terrain '{terrain.Name}' detail count ({terrain.DetailCount}) exceeds budget ({detailBudget}).",
                            UnityScannerIssueSeverity.Info, terrain.Path,
                            "actual_count", terrain.DetailCount,
                            "budget", detailBudget));
                    }
                }

                if (settings.DetectExpensiveSettings && terrain.HasExpensiveSettings)
                {
                    issues.Add(MakeIssue(CodeExpensiveSettings,
                        $"Terrain '{terrain.Name}' uses expensive settings for selected platform profile.",
                        UnityScannerIssueSeverity.Info, terrain.Path));
                }
            }

            return issues;
        }

        private static UnityScannerIssue MakeIssue(
            string code, string description, UnityScannerIssueSeverity severity,
            string assetPath, params object[] metadataPairs)
        {
            var issue = new UnityScannerIssue
            {
                CategoryId = "terrain_analysis",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };

            if (metadataPairs != null)
            {
                for (var i = 0; i + 1 < metadataPairs.Length; i += 2)
                {
                    var key = metadataPairs[i]?.ToString();
                    if (key != null)
                        issue.Metadata[key] = metadataPairs[i + 1];
                }
            }

            return issue;
        }
    }
}
