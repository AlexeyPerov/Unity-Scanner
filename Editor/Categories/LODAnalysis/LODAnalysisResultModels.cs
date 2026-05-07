using System.Collections.Generic;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.LODAnalysis
{
    public class LODGroupData : USItemDataBase
    {
        public string AssetPath;
        public string ScenePath;
        public int LODLevelCount;
        public bool HasCullLOD;
        public bool AnimateCrossFading;
        public int FadeMode;
        public bool IsUIElement;
        public bool IsSmallObject;
        public string ObjectName;
        public List<LODLevelData> Levels = new List<LODLevelData>();
        public bool Foldout;
    }

    public class LODLevelData
    {
        public int LevelIndex;
        public float ScreenTransitionHeight;
        public int RendererCount;
        public int TriangleCount;
        public List<string> MaterialNames = new List<string>();
        public bool HasNullRenderers;
        public int NullRendererCount;
    }
}
