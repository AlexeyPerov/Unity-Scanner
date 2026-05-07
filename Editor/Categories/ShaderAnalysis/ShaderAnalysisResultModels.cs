using System.Collections.Generic;
using UnityScanner.UI.Controls;
using UnityEngine;

namespace UnityScanner.Categories.ShaderAnalysis
{
    public class ShaderData : USItemDataBase
    {
        public string Path;
        public string Name;
        public Shader Shader;
        public int VariantCount;
        public int PassCount;
        public int KeywordCount;
        public List<string> Keywords = new List<string>();
        public List<string> PassNames = new List<string>();
        public bool IsErrorShader;
        public bool IsFallbackShader;
        public string RenderPipeline;
        public List<MaterialData> ReferencingMaterials = new List<MaterialData>();
        public bool Foldout;
    }

    public class MaterialData : USItemDataBase
    {
        public string Path;
        public string Name;
        public Material Material;
        public ShaderData Shader;
        public List<string> EnabledKeywords = new List<string>();
        public List<string> ShaderKeywords = new List<string>();
        public bool IsUsingErrorShader;
        public bool Foldout;
    }

    public class ShaderFeatureSet
    {
        public string NormalizedKeywordsKey;
        public List<MaterialData> Materials = new List<MaterialData>();
    }
}
