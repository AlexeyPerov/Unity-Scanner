using System.Collections.Generic;
using UnityScanner.UI.Controls;
using UnityEngine;

namespace UnityScanner.Categories.TerrainAnalysis
{
    public class TerrainDataInfo : USItemDataBase
    {
        public string Path;
        public string Name;
        public Terrain Terrain;
        public TerrainData TerrainData;
        public int LayerCount;
        public int MissingLayerCount;
        public List<string> MissingLayerNames = new List<string>();
        public int SplatPrototypeCount;
        public int MissingSplatCount;
        public int TreeCount;
        public int DetailCount;
        public int ControlMapResolution;
        public int HeightmapResolution;
        public long ControlMapMemoryBytes;
        public int AlphamapTextureCount;
        public List<int> AlphamapTextureSizes = new List<int>();
        public bool HasColliderMismatch;
        public bool HasExpensiveSettings;
        public bool Foldout;
    }
}
