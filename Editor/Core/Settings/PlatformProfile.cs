using System;
using System.Collections.Generic;

namespace UnityScanner.Core.Settings
{
    [Serializable]
    public class PlatformProfile
    {
        public string Id;
        public string DisplayName;
        public string Description;

        public int MaxTextureSize = 2048;
        public int MaxAtlasSize = 4096;

        public int ShaderVariantThreshold = 256;
        public int ShaderPassThreshold = 8;
        public int ShaderKeywordThreshold = 64;

        public int MaxTerrainControlMapMemoryMB = 64;
        public int MaxTerrainTextureSize = 2048;
        public int MaxTreeDensity = 500;
        public int MaxDetailDensity = 1000;

        public int MaxTmpAtlasSize = 2048;
        public int MaxFallbackChainDepth = 3;

        public int MaxAudioClipSizeMB = 10;
        public int MaxStartupAudioTotalMB = 50;

        public int MaxSceneObjectCount = 5000;
        public int MaxPrefabNestingDepth = 5;
        public int MaxPrefabOverrideCount = 50;

        public long StartupSceneBudgetKB = 51200;

        public bool RequireETC2Compression = false;
        public bool RequireASTCCompression = false;
        public int MaxMeshVertexCount = 65535;
        public int MaxMeshBoneWeightCount = 4;
        public bool RequireStaticBatching = false;
        public bool RequireGPUInstancing = false;

        public int MaxParticleEmissionRate = 500;
        public float MaxParticleTrailLifetime = 5.0f;
        public int MaxParticleSystemModules = 10;

        public int MaxCanvasVertexCount = 10000;
        public int MaxCanvasNestingDepth = 5;

        public int MaxRealtimeLightsPerScene = 8;
        public int MaxLightmapSize = 1024;
        public int MaxReflectionProbeSize = 256;
        public int MaxReflectionProbeCount = 4;

        public int MinLODLevels = 2;
        public float MaxLODScreenTransitionHeight = 0.5f;

        public int MaxRigidbodyCount = 500;
        public int MaxMeshColliderTriangles = 256;

        public float MaxSpriteAtlasUnusedRatio = 0.3f;

        public bool Validate()
        {
            if (string.IsNullOrEmpty(Id) || string.IsNullOrEmpty(DisplayName))
                return false;
            if (MaxTextureSize <= 0 || ShaderVariantThreshold <= 0)
                return false;
            if (MaxParticleEmissionRate < 0 || MaxParticleSystemModules < 0)
                return false;
            if (MaxCanvasVertexCount < 0 || MaxCanvasNestingDepth < 0)
                return false;
            if (MaxRealtimeLightsPerScene < 0 || MaxLightmapSize < 0)
                return false;
            if (MinLODLevels < 0 || MaxLODScreenTransitionHeight < 0f)
                return false;
            if (MaxRigidbodyCount < 0 || MaxMeshColliderTriangles < 0)
                return false;
            if (MaxSpriteAtlasUnusedRatio < 0f || MaxSpriteAtlasUnusedRatio > 1f)
                return false;
            return true;
        }

        public PlatformProfile Clone()
        {
            return (PlatformProfile)MemberwiseClone();
        }
    }
}
