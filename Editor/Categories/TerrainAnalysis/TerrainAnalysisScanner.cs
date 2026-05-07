using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Categories.TerrainAnalysis
{
    public static class TerrainAnalysisScanner
    {
        public static void ScanAll(
            TerrainAnalysisSettings settings,
            PlatformProfile profile,
            List<TerrainDataInfo> terrains,
            IUnityScannerIssueSink issueSink)
        {
            var terrainGuids = AssetDatabase.FindAssets("t:Terrain");
            var total = terrainGuids.Length;

            for (var i = 0; i < terrainGuids.Length; i++)
            {
                if (i % 50 == 0)
                {
                    GC.Collect();
                    issueSink.ReportProgress((float)i / total, "Scanning terrains...");
                }

                var terrainPath = AssetDatabase.GUIDToAssetPath(terrainGuids[i]);

                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    !terrainPath.Contains(settings.PathFilter))
                    continue;

                var terrain = AssetDatabase.LoadMainAssetAtPath(terrainPath) as Terrain;
                if (terrain == null || terrain.terrainData == null)
                    continue;

                var info = CreateTerrainInfo(terrain, terrainPath, settings, profile);
                terrains.Add(info);
            }

            GC.Collect();
        }

        private static TerrainDataInfo CreateTerrainInfo(
            Terrain terrain, string path, TerrainAnalysisSettings settings, PlatformProfile profile)
        {
            var td = terrain.terrainData;
            var info = new TerrainDataInfo
            {
                Path = path,
                Name = Path.GetFileName(path),
                Terrain = terrain,
                TerrainData = td,
                TreeCount = td.treeInstanceCount,
                HeightmapResolution = td.heightmapResolution,
                ControlMapResolution = td.alphamapResolution
            };

            info.LayerCount = td.terrainLayers?.Length ?? 0;

            var missingLayers = 0;
            if (td.terrainLayers != null)
            {
                for (var i = 0; i < td.terrainLayers.Length; i++)
                {
                    if (td.terrainLayers[i] == null)
                    {
                        missingLayers++;
                        info.MissingLayerNames.Add($"Layer {i}");
                    }
                    else
                    {
                        var layer = td.terrainLayers[i];
                        if (layer.diffuseTexture == null)
                        {
                            missingLayers++;
                            info.MissingLayerNames.Add($"Layer {i}: missing diffuse");
                        }
                    }
                }
            }
            info.MissingLayerCount = missingLayers;

            if (info.MissingLayerCount > 0)
                info.TrySetWarningLevel(3);

            info.AlphamapTextureCount = td.alphamapTextureCount;
            long controlMapBytes = 0;
            for (var i = 0; i < td.alphamapTextureCount; i++)
            {
                var alphamapTextures = td.alphamapTextures;
                if (alphamapTextures != null && i < alphamapTextures.Length)
                {
                    var tex = alphamapTextures[i];
                    if (tex != null)
                    {
                        info.AlphamapTextureSizes.Add(Mathf.Max(tex.width, tex.height));
                        controlMapBytes += tex.width * tex.height * 4;
                    }
                }
            }
            info.ControlMapMemoryBytes = controlMapBytes;

            var detailBudget = profile?.MaxDetailDensity ?? settings.DetailDensityThreshold;
            var treeBudget = profile?.MaxTreeDensity ?? settings.TreeDensityThreshold;
            var textureBudget = profile?.MaxTerrainTextureSize ?? settings.MaxTerrainTextureSize;
            var controlBudgetMB = profile?.MaxTerrainControlMapMemoryMB ?? settings.ControlMapMemoryBudgetMB;

            if (info.TreeCount > treeBudget)
                info.TrySetWarningLevel(2);
            if (info.ControlMapMemoryBytes > controlBudgetMB * 1024L * 1024L)
                info.TrySetWarningLevel(2);

            foreach (var texSize in info.AlphamapTextureSizes)
            {
                if (texSize > textureBudget)
                    info.TrySetWarningLevel(2);
            }

            if (settings.DetectColliderMismatches)
            {
                var collider = terrain.GetComponent<TerrainCollider>();
                if (collider != null && collider.terrainData != td)
                    info.HasColliderMismatch = true;
                if (info.HasColliderMismatch)
                    info.TrySetWarningLevel(3);
            }

            if (settings.DetectExpensiveSettings && profile != null)
            {
                if (td.heightmapResolution > 513 && profile.Id == PlatformProfilePresets.Mobile)
                    info.HasExpensiveSettings = true;
                if (td.alphamapResolution > 512 && profile.Id == PlatformProfilePresets.Mobile)
                    info.HasExpensiveSettings = true;
                if (info.HasExpensiveSettings)
                    info.TrySetWarningLevel(1);
            }

            return info;
        }
    }
}
