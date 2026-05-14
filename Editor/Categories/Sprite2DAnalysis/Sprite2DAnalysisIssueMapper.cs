using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.Sprite2DAnalysis
{
    public static class Sprite2DAnalysisIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(
            List<SpriteAtlasData> atlasResults,
            List<SpriteEntry> spriteResults,
            List<DuplicateGroup> duplicateResults,
            Sprite2DAnalysisSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();

            var threshold = profile?.MaxSpriteAtlasUnusedRatio ?? 0.3f;

            if (settings.CheckAtlasEfficiency)
            {
                foreach (var atlas in atlasResults)
                {
                    if (atlas.AtlasPixelArea > 0 && atlas.UnusedRatio > threshold)
                    {
                        atlas.AddWarning("Atlas '" + atlas.AssetPath + "' has " + (atlas.UnusedRatio * 100).ToString("F1") + "% unused space (threshold: " + (threshold * 100).ToString("F1") + "%). " +
                            "Total atlas area: " + atlas.AtlasPixelArea.ToString("N0") + " px, used: " + atlas.UsedPixelArea.ToString("N0") + " px. " +
                            "Excessive wasted space increases runtime memory and texture upload cost. " +
                            "Consider adding more sprites to this atlas, consolidating small atlases, or adjusting sprite padding.");
                        issues.Add(MakeIssue("sprite_atlas_low_efficiency",
                            "Atlas '" + atlas.AssetPath + "' has " + (atlas.UnusedRatio * 100).ToString("F1") + "% unused space (threshold: " + (threshold * 100).ToString("F1") + "%). " +
                            "Total atlas area: " + atlas.AtlasPixelArea.ToString("N0") + " px, used: " + atlas.UsedPixelArea.ToString("N0") + " px. " +
                            "Excessive wasted space increases runtime memory and texture upload cost. " +
                            "Consider adding more sprites to this atlas, consolidating small atlases, or adjusting sprite padding.",
                            UnityScannerIssueSeverity.Warning, atlas.AssetPath,
                            "AtlasPixelArea", atlas.AtlasPixelArea,
                            "UsedPixelArea", atlas.UsedPixelArea,
                            "UnusedRatio", atlas.UnusedRatio,
                            "ThresholdRatio", threshold));
                    }
                }
            }

            if (settings.CheckNotPacked)
            {
                var looseSprites = spriteResults.Where(s => !s.IsInAtlas).ToList();
                foreach (var sprite in looseSprites)
                {
                    if (sprite.PixelArea > settings.MinNotPackedSpriteSize * settings.MinNotPackedSpriteSize)
                    {
                        issues.Add(MakeIssue("sprite_not_packed",
                            "Sprite '" + sprite.SpriteName + "' (" + sprite.Width + "x" + sprite.Height + ") is not included in any Sprite Atlas. " +
                            "Unpacked sprites are loaded individually at runtime, causing a separate draw call per sprite and higher memory overhead. " +
                            "Add this sprite to a Sprite Atlas to enable batching and reduce draw calls.",
                            UnityScannerIssueSeverity.Info, sprite.AssetPath,
                            "SpriteName", sprite.SpriteName,
                            "TextureSize", sprite.Width + "x" + sprite.Height));
                    }
                }
            }

            if (settings.CheckPolygonVerticesExcessive)
            {
                foreach (var sprite in spriteResults)
                {
                    if (sprite.PolygonVertexCount > settings.MaxPolygonVertexCount)
                    {
                        issues.Add(MakeIssue("sprite_polygon_vertices_excessive",
                            "Sprite '" + sprite.SpriteName + "' has " + sprite.PolygonVertexCount + " polygon vertices (threshold: " + settings.MaxPolygonVertexCount + "). " +
                            "High vertex counts increase CPU time for collision detection and physics simulation when used with PolygonCollider2D. " +
                            "Consider simplifying the sprite outline or reducing the detail level in Sprite Editor.",
                            UnityScannerIssueSeverity.Info, sprite.AssetPath,
                            "VertexCount", sprite.PolygonVertexCount,
                            "SpriteName", sprite.SpriteName));
                    }
                }
            }

            if (settings.CheckSheetUnevenCells)
            {
                var pathGroups = spriteResults
                    .Where(s => !string.IsNullOrEmpty(s.AssetPath))
                    .GroupBy(s => s.AssetPath)
                    .Where(g => g.Count() > 1);

                foreach (var group in pathGroups)
                {
                    var sizes = group.Select(s => s.Width + "x" + s.Height).Distinct().ToList();
                    if (sizes.Count > 1)
                    {
                        issues.Add(MakeIssue("sprite_sheet_uneven_cells",
                            "Sprite sheet '" + group.Key + "' has " + sizes.Count + " different sprite sizes across " + group.Count() + " sprites. " +
                            "Uneven cell sizes prevent the sprite sheet from being packed efficiently into an atlas, wasting GPU memory. " +
                            "Sizes found: " + string.Join(", ", sizes) + ". " +
                            "Standardize sprite dimensions within each sheet for optimal atlas packing.",
                            UnityScannerIssueSeverity.Warning, group.Key,
                            "SpriteCount", group.Count(),
                            "SizeVariants", sizes.Count));
                    }
                }
            }

            if (settings.CheckFullRectUnnecessary)
            {
                foreach (var sprite in spriteResults)
                {
                    if (sprite.MeshType == "FullRect" && sprite.PixelArea > settings.MinFullRectSpriteSize * settings.MinFullRectSpriteSize)
                    {
                        issues.Add(MakeIssue("sprite_full_rect_unnecessary",
                            "Sprite '" + sprite.SpriteName + "' uses FullRect mesh type. Tight mesh can reduce wasted pixel space, especially for non-rectangular sprites. " +
                            "FullRect includes the entire bounding box, while Tight trims to the sprite outline. " +
                            "Switch to Tight mesh in Sprite Editor unless FullRect is required for 9-slicing or specific UI layouts.",
                            UnityScannerIssueSeverity.Info, sprite.AssetPath,
                            "SpriteName", sprite.SpriteName,
                            "CurrentMeshType", sprite.MeshType,
                            "EstimatedWaste", "FullRect vs Tight"));
                    }
                }
            }

            if (settings.CheckDuplicateContent)
            {
                foreach (var dup in duplicateResults)
                {
                    issues.Add(MakeIssue("sprite_duplicate_content",
                        "Duplicate sprite content found in " + dup.AssetPaths.Count + " files (" + (dup.ContentSizeBytes / 1024) + " KB each, total waste: " + ((dup.AssetPaths.Count - 1) * dup.ContentSizeBytes / 1024) + " KB). " +
                        "Duplicate textures waste build size and runtime memory. " +
                        "Files: " + string.Join(", ", dup.AssetPaths) + ". " +
                        "Consolidate by keeping a single copy and updating all references.",
                        UnityScannerIssueSeverity.Warning, dup.AssetPaths.FirstOrDefault() ?? "",
                        "AssetPaths", string.Join("; ", dup.AssetPaths),
                        "ContentSizeBytes", dup.ContentSizeBytes));
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
                CategoryId = "sprite_2d_analysis",
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
