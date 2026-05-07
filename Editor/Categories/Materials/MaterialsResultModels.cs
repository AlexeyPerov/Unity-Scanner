using System;
using System.Collections.Generic;
using System.IO;
using UnityScanner.UI.Controls;
using UnityScanner.Utilities.Addressables;
using UnityScanner.Utilities.BuildLayout;
using UnityScanner.Core.Export;

namespace UnityScanner.Categories.Materials
{
    public class MaterialPropertyData
    {
        public string Name;
        public string Type;
        public string Value;
        public string ReadableSize;
        public List<string> UsedByMaterialPaths;
    }

    public class RendererComponentData : USItemDataBase
    {
        public string Path;
        public string GameObjectName => System.IO.Path.GetFileName(Path);
        public string ChildName;
        public bool Foldout;
        public string Bundle;
        public int MaterialSlotsCount;
        public int WarningsCount => CustomWarnings?.Count ?? 0;

        public RendererComponentData(string path, string childName)
        {
            Path = path;
            ChildName = childName;
        }
    }

    public class MaterialAssetData : USItemDataBase
    {
        private readonly bool _tryUseReflectionForAddressablesDetection;
        private bool _isAddressableCalculated;
        private bool _isAddressable;

        public string Path;
        public string Name => System.IO.Path.GetFileName(Path);
        public Type Type;
        public string TypeName;
        public long BytesSize;
        public string ReadableSize;
        public bool Foldout;
        public bool PropertiesFoldout;
        public bool TextureReferencesFoldout;
        public Dictionary<string, bool> TextureUsedByMaterialsFoldout = new Dictionary<string, bool>();
        public string Bundle;
        public bool InResources;
        public bool IsAddressable
        {
            get
            {
                if (!_isAddressableCalculated)
                {
                    _isAddressable = USAddressablesReflection.IsAssetAddressable(Path, _tryUseReflectionForAddressablesDetection);
                    _isAddressableCalculated = true;
                }
                return _isAddressable;
            }
        }
        public string Fingerprint;
        public List<string> DuplicatePaths = new List<string>();
        public string ShaderName;
        public int RenderQueue;
        public int? ShaderDefaultRenderQueue;
        public bool HasRenderQueueOverride => ShaderDefaultRenderQueue.HasValue && RenderQueue != ShaderDefaultRenderQueue.Value;
        public List<string> EnabledKeywords;
        public List<MaterialPropertyData> Properties;
        public List<string> ReferencedTexturePaths = new List<string>();
        public bool IsMissingShader;
        public bool IsBuiltinShader;
        public List<string> ReferencedByPaths = new List<string>();
        public bool IsVariant;
        public string ParentMaterialPath;
        public int VariantChainDepth;
        public int? VariantOverrideCount;
        public bool ParentLinkBroken;
        public List<string> VariantChildrenPaths = new List<string>();
        public bool? SupportsGpuInstancing;
        public bool GpuInstancingEnabled;
        public bool? SrpBatcherCompatible;

        public MaterialAssetData(string path, Type type, string typeName, long bytesSize,
            string readableSize, bool tryUseReflectionForAddressablesDetection)
        {
            Path = path;
            Type = type;
            TypeName = typeName;
            BytesSize = bytesSize;
            ReadableSize = readableSize;
            _tryUseReflectionForAddressablesDetection = tryUseReflectionForAddressablesDetection;
            InResources = path.Contains("/Resources/");
        }

        public void AddDuplicatePath(string path)
        {
            DuplicatePaths ??= new List<string>();
            DuplicatePaths.Add(path);
        }

        public void AddReferencedByPath(string path)
        {
            ReferencedByPaths ??= new List<string>();
            ReferencedByPaths.Add(path);
        }

        public void AddVariantChildPath(string path)
        {
            VariantChildrenPaths ??= new List<string>();
            VariantChildrenPaths.Add(path);
        }

        public void SetReferencedTexturePaths(IEnumerable<string> paths)
        {
            ReferencedTexturePaths = new List<string>(new HashSet<string>(paths));
            ReferencedTexturePaths.Sort();
        }
    }
}
