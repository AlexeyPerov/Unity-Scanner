using System.Collections.Generic;
using UnityScanner.UI.Controls;
using UnityEngine;

namespace UnityScanner.Categories.FontTextAnalysis
{
    public class FontAssetData : USItemDataBase
    {
        public string Path;
        public string Name;
        public Object FontAsset;
        public int AtlasWidth;
        public int AtlasHeight;
        public bool IsDynamic;
        public int FallbackChainDepth;
        public List<string> FallbackChainNames = new List<string>();
        public int GlyphCount;
        public bool IsTmpFont;
        public bool Foldout;
    }

    public class FontFeatureSet
    {
        public string NormalizedFallbackKey;
        public List<FontAssetData> Fonts = new List<FontAssetData>();
    }
}
