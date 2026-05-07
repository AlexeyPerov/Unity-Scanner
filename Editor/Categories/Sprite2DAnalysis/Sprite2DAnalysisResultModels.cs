using System.Collections.Generic;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.Sprite2DAnalysis
{
    public class SpriteAtlasData : USItemDataBase
    {
        public string AssetPath;
        public int SpriteCount;
        public long AtlasPixelArea;
        public long UsedPixelArea;
        public float UnusedRatio;
        public List<SpriteEntry> Sprites = new List<SpriteEntry>();
        public bool Foldout;
    }

    public class SpriteEntry
    {
        public string AssetPath;
        public string SpriteName;
        public int Width;
        public int Height;
        public long PixelArea;
        public string MeshType;
        public int PolygonVertexCount;
        public bool IsInAtlas;
        public string AtlasPath;
    }

    public class DuplicateGroup
    {
        public string ContentHash;
        public long ContentSizeBytes;
        public List<string> AssetPaths = new List<string>();
    }
}
