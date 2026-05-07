using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEngine;

namespace UnityScanner.Categories.LODAnalysis
{
    public static class LODAnalysisIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(
            List<LODGroupData> results,
            LODAnalysisSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();
            if (profile == null) return issues;

            foreach (var data in results)
            {
                if (settings.CheckMissingLevels && data.LODLevelCount < profile.MinLODLevels)
                {
                    issues.Add(MakeIssue("lod_missing_levels",
                        "LOD group has " + data.LODLevelCount + " levels, minimum is " + profile.MinLODLevels + ".",
                        UnityScannerIssueSeverity.Warning, data.AssetPath,
                        "LevelCount", data.LODLevelCount,
                        "MinLevels", profile.MinLODLevels));
                }

                if (settings.CheckNullRenderers)
                {
                    foreach (var level in data.Levels)
                    {
                        if (level.HasNullRenderers)
                        {
                            issues.Add(MakeIssue("lod_null_renderers",
                                "LOD level " + level.LevelIndex + " has " + level.NullRendererCount + " null renderer(s).",
                                UnityScannerIssueSeverity.Error, data.AssetPath,
                                "LODLevel", level.LevelIndex,
                                "NullRendererCount", level.NullRendererCount));
                        }
                    }
                }

                if (settings.CheckRendererCountMismatch && data.Levels.Count > 1)
                {
                    var lod0Count = data.Levels[0].RendererCount;
                    foreach (var level in data.Levels.Skip(1))
                    {
                        if (level.RendererCount != lod0Count && level.ScreenTransitionHeight > 0f)
                        {
                            issues.Add(MakeIssue("lod_renderer_count_mismatch",
                                "LOD level " + level.LevelIndex + " has " + level.RendererCount +
                                " renderers vs LOD0's " + lod0Count + ".",
                                UnityScannerIssueSeverity.Warning, data.AssetPath,
                                "LODLevel", level.LevelIndex,
                                "LOD0RendererCount", lod0Count,
                                "CurrentRendererCount", level.RendererCount));
                        }
                    }
                }

                if (settings.CheckLastLevelComplex && data.Levels.Count > 1)
                {
                    var lastVisible = data.Levels.LastOrDefault(l => l.ScreenTransitionHeight > 0f);
                    if (lastVisible != null && lastVisible.TriangleCount > 0)
                    {
                        var lod0Tris = data.Levels[0].TriangleCount;
                        if (lod0Tris > 0 && lastVisible.TriangleCount > lod0Tris * 0.5f)
                        {
                            issues.Add(MakeIssue("lod_last_level_complex",
                                "Last visible LOD level " + lastVisible.LevelIndex + " has " +
                                lastVisible.TriangleCount + " triangles (LOD0: " + lod0Tris + ").",
                                UnityScannerIssueSeverity.Warning, data.AssetPath,
                                "TriangleCount", lastVisible.TriangleCount,
                                "LOD0TriangleCount", lod0Tris));
                        }
                    }
                }

                if (settings.CheckMaterialMismatch && data.Levels.Count > 1)
                {
                    var lod0Materials = data.Levels[0].MaterialNames;
                    foreach (var level in data.Levels.Skip(1))
                    {
                        if (level.ScreenTransitionHeight <= 0f) continue;
                        if (level.MaterialNames.Count > 0 && !lod0Materials.SetEquals(level.MaterialNames))
                        {
                            issues.Add(MakeIssue("lod_material_mismatch",
                                "LOD level " + level.LevelIndex + " uses different materials than LOD0.",
                                UnityScannerIssueSeverity.Warning, data.AssetPath,
                                "LODLevel", level.LevelIndex,
                                "LOD0MaterialCount", lod0Materials.Count,
                                "CurrentMaterialCount", level.MaterialNames.Count));
                        }
                    }
                }

                if (settings.CheckTransitionTooClose && data.Levels.Count > 1)
                {
                    for (var i = 0; i < data.Levels.Count - 1; i++)
                    {
                        var a = data.Levels[i];
                        var b = data.Levels[i + 1];
                        if (a.ScreenTransitionHeight <= 0f || b.ScreenTransitionHeight <= 0f) continue;

                        var diff = a.ScreenTransitionHeight - b.ScreenTransitionHeight;
                        if (diff > 0f && diff < 0.05f)
                        {
                            issues.Add(MakeIssue("lod_transition_too_close",
                                "LOD levels " + a.LevelIndex + " and " + b.LevelIndex +
                                " transition heights too close: " + a.ScreenTransitionHeight.ToString("F3") +
                                " vs " + b.ScreenTransitionHeight.ToString("F3") + ".",
                                UnityScannerIssueSeverity.Info, data.AssetPath,
                                "LevelA", a.LevelIndex,
                                "LevelB", b.LevelIndex,
                                "HeightA", a.ScreenTransitionHeight,
                                "HeightB", b.ScreenTransitionHeight));
                        }
                    }
                }

                if (settings.CheckNoCrossfade && !data.AnimateCrossFading && data.LODLevelCount > 2)
                {
                    issues.Add(MakeIssue("lod_no_crossfade",
                        "LOD group with " + data.LODLevelCount + " levels does not use cross-fade. FadeMode: " + (LODFadeMode)data.FadeMode + ".",
                        UnityScannerIssueSeverity.Info, data.AssetPath,
                        "FadeMode", data.FadeMode));
                }

                if (settings.CheckUnnecessary)
                {
                    if (data.IsUIElement)
                    {
                        issues.Add(MakeIssue("lod_unnecessary",
                            "LOD group on UI element '" + data.ObjectName + "'. UI elements do not benefit from LOD.",
                            UnityScannerIssueSeverity.Verbose, data.AssetPath,
                            "Reason", "UI element"));
                    }
                    else if (data.IsSmallObject)
                    {
                        issues.Add(MakeIssue("lod_unnecessary",
                            "LOD group on small object '" + data.ObjectName + "' (scale < 0.5). Unlikely to benefit from LOD.",
                            UnityScannerIssueSeverity.Verbose, data.AssetPath,
                            "Reason", "Small static object"));
                    }
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
                CategoryId = "lod_analysis",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };

            for (var i = 0; i + 1 < metadataPairs.Length; i += 2)
            {
                if (metadataPairs[i] is string key)
                    issue.Metadata[key] = metadataPairs[i + 1];
            }

            return issue;
        }

        private static bool SetEquals(this List<string> a, List<string> b)
        {
            var setA = new HashSet<string>(a);
            var setB = new HashSet<string>(b);
            return setA.SetEquals(setB);
        }
    }
}
