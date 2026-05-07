using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.UICanvasAnalysis
{
    public static class UICanvasAnalysisIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(
            List<CanvasData> results,
            UICanvasAnalysisSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();

            var maxVertexCount = profile?.MaxCanvasVertexCount ?? 10000;
            var maxNestingDepth = profile?.MaxCanvasNestingDepth ?? 5;

            foreach (var data in results)
            {
                if (settings.CheckUnusedShaderChannels && !string.IsNullOrEmpty(data.EnabledChannels) &&
                    (data.UsedChannels == "None" || string.IsNullOrEmpty(data.UsedChannels)))
                {
                    issues.Add(MakeIssue("canvas_unused_shader_channels",
                        "Canvas '" + data.CanvasName + "' has additional shader channels enabled (" + data.EnabledChannels + ") but no child material uses them.",
                        UnityScannerIssueSeverity.Info, data.ScenePath,
                        "EnabledChannels", data.EnabledChannels,
                        "UsedChannels", data.UsedChannels ?? "None"));
                }

                if (settings.CheckNestedRedundancy && data.IsNestedRedundant)
                {
                    issues.Add(MakeIssue("canvas_nested_redundancy",
                        "Nested Canvas '" + data.CanvasName + "' is redundant — same render mode/sorting as parent '" + data.ParentCanvasPath + "'.",
                        UnityScannerIssueSeverity.Info, data.ScenePath,
                        "ParentCanvasPath", data.ParentCanvasPath ?? "",
                        "RenderMode", data.RenderMode ?? ""));
                }

                if (settings.CheckRaycastTargets)
                {
                    foreach (var rt in data.UnnecessaryRaycasts)
                    {
                        issues.Add(MakeIssue("ui_unnecessary_raycast_target",
                            "Non-interactive " + rt.ComponentType + " '" + rt.ObjectPath + "' has raycast target enabled but no event handler.",
                            UnityScannerIssueSeverity.Info, data.ScenePath,
                            "ComponentType", rt.ComponentType,
                            "HasEventHandler", rt.HasEventHandler));
                    }
                }

                if (settings.CheckLayoutNesting && data.LayoutNestingDepth > maxNestingDepth)
                {
                    issues.Add(MakeIssue("ui_layout_nesting_depth",
                        "Layout nesting depth " + data.LayoutNestingDepth + " in canvas '" + data.CanvasName + "' exceeds threshold " + maxNestingDepth + ".",
                        UnityScannerIssueSeverity.Warning, data.ScenePath,
                        "NestingDepth", data.LayoutNestingDepth,
                        "ThresholdDepth", maxNestingDepth,
                        "LayoutTypes", string.Join(", ", data.LayoutTypes)));
                }

                if (settings.CheckVertexCount && data.VertexCount > maxVertexCount)
                {
                    issues.Add(MakeIssue("canvas_vertex_count_exceeded",
                        "Canvas '" + data.CanvasName + "' vertex count " + data.VertexCount + " exceeds threshold " + maxVertexCount + ".",
                        UnityScannerIssueSeverity.Warning, data.ScenePath,
                        "VertexCount", data.VertexCount,
                        "ThresholdVertices", maxVertexCount));
                }

                if (settings.CheckTextTmpMix && data.LegacyTextCount > 0 && data.TmpTextCount > 0)
                {
                    issues.Add(MakeIssue("ui_text_tmp_mix",
                        "Canvas '" + data.CanvasName + "' mixes legacy Text (" + data.LegacyTextCount + ") and TMP (" + data.TmpTextCount + ") components.",
                        UnityScannerIssueSeverity.Info, data.ScenePath,
                        "LegacyTextCount", data.LegacyTextCount,
                        "TmpTextCount", data.TmpTextCount));
                }

                if (settings.CheckAtlasWaste && data.UnpackedSpriteCount > 0)
                {
                    issues.Add(MakeIssue("ui_atlas_waste",
                        "Canvas '" + data.CanvasName + "' has " + data.UnpackedSpriteCount + " sprites not packed into a shared atlas.",
                        UnityScannerIssueSeverity.Info, data.ScenePath,
                        "UnpackedSpriteCount", data.UnpackedSpriteCount));
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
                CategoryId = "ui_canvas_analysis",
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
    }
}
