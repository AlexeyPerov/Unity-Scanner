using System.Collections.Generic;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.LightingAnalysis
{
    public class SceneLightingData : USItemDataBase
    {
        public string ScenePath;
        public int RealtimeLightCount;
        public int MixedLightCount;
        public int BakedLightCount;
        public int TotalLightCount;
        public int ReflectionProbeCount;
        public int MaxReflectionProbeResolution;
        public bool HasLightProbes;
        public int LightmapSize;
        public int LightmapCount;
        public List<LightInfo> Lights = new List<LightInfo>();
        public List<EmissiveMaterialInfo> EmissiveMaterials = new List<EmissiveMaterialInfo>();
        public string ActivePipeline;
        public bool Foldout;
    }

    public class LightInfo
    {
        public string ObjectPath;
        public string LightType;
        public string LightMode;
        public bool ShadowsEnabled;
        public float Range;
        public float Intensity;
        public string Color;
    }

    public class EmissiveMaterialInfo
    {
        public string MaterialPath;
        public string MaterialName;
        public string GlobalIlluminationFlags;
    }
}
