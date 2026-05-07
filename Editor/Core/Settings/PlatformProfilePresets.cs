using System.Collections.Generic;

namespace UnityScanner.Core.Settings
{
    public static class PlatformProfilePresets
    {
        public const string Mobile = "mobile";
        public const string Console = "console";
        public const string Desktop = "desktop";

        private static readonly Dictionary<string, PlatformProfile> Presets = new Dictionary<string, PlatformProfile>
        {
            {
                Mobile, new PlatformProfile
                {
                    Id = Mobile,
                    DisplayName = "Mobile",
                    Description = "Optimized for mobile platforms (iOS, Android). Lower texture sizes, stricter shader variant limits, reduced memory budgets.",

                    MaxTextureSize = 2048,
                    MaxAtlasSize = 2048,
                    ShaderVariantThreshold = 128,
                    ShaderPassThreshold = 4,
                    ShaderKeywordThreshold = 32,
                    MaxTerrainControlMapMemoryMB = 32,
                    MaxTerrainTextureSize = 1024,
                    MaxTreeDensity = 200,
                    MaxDetailDensity = 500,
                    MaxTmpAtlasSize = 1024,
                    MaxFallbackChainDepth = 2,
                    MaxAudioClipSizeMB = 5,
                    MaxStartupAudioTotalMB = 20,
                    MaxSceneObjectCount = 2000,
                    MaxPrefabNestingDepth = 3,
                    MaxPrefabOverrideCount = 30,
                    StartupSceneBudgetKB = 20480,
                    MaxParticleEmissionRate = 200,
                    MaxParticleTrailLifetime = 3.0f,
                    MaxParticleSystemModules = 6,
                    MaxCanvasVertexCount = 3000,
                    MaxCanvasNestingDepth = 3,
                    MaxRealtimeLightsPerScene = 4,
                    MaxLightmapSize = 512,
                    MaxReflectionProbeSize = 128,
                    MaxReflectionProbeCount = 2,
                    MinLODLevels = 2,
                    MaxLODScreenTransitionHeight = 0.3f,
                    MaxRigidbodyCount = 100,
                    MaxMeshColliderTriangles = 64,
                    MaxSpriteAtlasUnusedRatio = 0.2f
                }
            },
            {
                Console, new PlatformProfile
                {
                    Id = Console,
                    DisplayName = "Console",
                    Description = "Balanced for console platforms (PS, Xbox, Switch). Moderate limits with higher quality targets.",

                    MaxTextureSize = 4096,
                    MaxAtlasSize = 4096,
                    ShaderVariantThreshold = 512,
                    ShaderPassThreshold = 8,
                    ShaderKeywordThreshold = 96,
                    MaxTerrainControlMapMemoryMB = 128,
                    MaxTerrainTextureSize = 2048,
                    MaxTreeDensity = 1000,
                    MaxDetailDensity = 2000,
                    MaxTmpAtlasSize = 2048,
                    MaxFallbackChainDepth = 3,
                    MaxAudioClipSizeMB = 15,
                    MaxStartupAudioTotalMB = 80,
                    MaxSceneObjectCount = 10000,
                    MaxPrefabNestingDepth = 5,
                    MaxPrefabOverrideCount = 50,
                    StartupSceneBudgetKB = 102400,
                    MaxParticleEmissionRate = 800,
                    MaxParticleTrailLifetime = 8.0f,
                    MaxParticleSystemModules = 12,
                    MaxCanvasVertexCount = 15000,
                    MaxCanvasNestingDepth = 6,
                    MaxRealtimeLightsPerScene = 16,
                    MaxLightmapSize = 2048,
                    MaxReflectionProbeSize = 512,
                    MaxReflectionProbeCount = 8,
                    MinLODLevels = 3,
                    MaxLODScreenTransitionHeight = 0.5f,
                    MaxRigidbodyCount = 800,
                    MaxMeshColliderTriangles = 256,
                    MaxSpriteAtlasUnusedRatio = 0.4f
                }
            },
            {
                Desktop, new PlatformProfile
                {
                    Id = Desktop,
                    DisplayName = "Desktop",
                    Description = "Relaxed limits for desktop platforms (Windows, macOS, Linux). Higher budgets and fewer restrictions.",

                    MaxTextureSize = 8192,
                    MaxAtlasSize = 8192,
                    ShaderVariantThreshold = 1024,
                    ShaderPassThreshold = 16,
                    ShaderKeywordThreshold = 128,
                    MaxTerrainControlMapMemoryMB = 256,
                    MaxTerrainTextureSize = 4096,
                    MaxTreeDensity = 2000,
                    MaxDetailDensity = 4000,
                    MaxTmpAtlasSize = 4096,
                    MaxFallbackChainDepth = 5,
                    MaxAudioClipSizeMB = 30,
                    MaxStartupAudioTotalMB = 150,
                    MaxSceneObjectCount = 20000,
                    MaxPrefabNestingDepth = 8,
                    MaxPrefabOverrideCount = 100,
                    StartupSceneBudgetKB = 204800,
                    MaxParticleEmissionRate = 1500,
                    MaxParticleTrailLifetime = 10.0f,
                    MaxParticleSystemModules = 15,
                    MaxCanvasVertexCount = 30000,
                    MaxCanvasNestingDepth = 8,
                    MaxRealtimeLightsPerScene = 32,
                    MaxLightmapSize = 4096,
                    MaxReflectionProbeSize = 1024,
                    MaxReflectionProbeCount = 16,
                    MinLODLevels = 3,
                    MaxLODScreenTransitionHeight = 0.5f,
                    MaxRigidbodyCount = 2000,
                    MaxMeshColliderTriangles = 512,
                    MaxSpriteAtlasUnusedRatio = 0.5f
                }
            }
        };

        public static PlatformProfile GetPreset(string id)
        {
            if (id != null && Presets.TryGetValue(id, out var preset))
                return preset.Clone();
            return GetDesktop();
        }

        public static PlatformProfile GetMobile() => Presets[Mobile].Clone();
        public static PlatformProfile GetConsole() => Presets[Console].Clone();
        public static PlatformProfile GetDesktop() => Presets[Desktop].Clone();

        public static List<PlatformProfile> GetAllPresets()
        {
            var result = new List<PlatformProfile>();
            foreach (var preset in Presets.Values)
                result.Add(preset.Clone());
            return result;
        }

        public static string[] GetPresetIds()
        {
            return new[] { Mobile, Console, Desktop };
        }

        public static string[] GetPresetDisplayNames()
        {
            return new[] { "Mobile", "Console", "Desktop" };
        }
    }
}
