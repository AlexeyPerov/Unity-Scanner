using System.Collections.Generic;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.UICanvasAnalysis
{
    public class CanvasData : USItemDataBase
    {
        public string AssetPath;
        public string ScenePath;
        public string CanvasName;
        public int VertexCount;
        public int ChildCount;
        public int RaycastTargetCount;
        public int UnnecessaryRaycastCount;
        public int LayoutNestingDepth;
        public List<string> LayoutTypes = new List<string>();
        public int LegacyTextCount;
        public int TmpTextCount;
        public int UnpackedSpriteCount;
        public string EnabledChannels;
        public string UsedChannels;
        public bool IsNestedRedundant;
        public string ParentCanvasPath;
        public string RenderMode;
        public List<RaycastTargetInfo> UnnecessaryRaycasts = new List<RaycastTargetInfo>();
        public bool Foldout;
    }

    public class RaycastTargetInfo
    {
        public string ObjectPath;
        public string ComponentType;
        public bool HasEventHandler;
    }
}
