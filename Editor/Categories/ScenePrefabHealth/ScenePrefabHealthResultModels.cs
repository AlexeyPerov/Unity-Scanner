using System.Collections.Generic;
using UnityScanner.UI.Controls;
using UnityEngine;

namespace UnityScanner.Categories.ScenePrefabHealth
{
    public class SceneData : USItemDataBase
    {
        public string Path;
        public string Name;
        public int RootCount;
        public int TotalObjectCount;
        public int TotalComponentCount;
        public int InactiveObjectCount;
        public int InactiveRendererCount;
        public bool IsBootstrapScene;
        public long FileSizeBytes;
        public List<string> BrokenReferences = new List<string>();
        public List<string> HotspotPaths = new List<string>();
        public List<InactiveObjectInfo> ExpensiveInactiveObjects = new List<InactiveObjectInfo>();
        public bool Foldout;
    }

    public class PrefabData : USItemDataBase
    {
        public string Path;
        public string Name;
        public int NestingDepth;
        public int OverrideCount;
        public int ComponentCount;
        public int ChildCount;
        public long FileSizeBytes;
        public List<string> OverrideDetails = new List<string>();
        public bool Foldout;
    }

    public class InactiveObjectInfo
    {
        public string ObjectPath;
        public string ComponentType;
        public string Description;
    }
}
