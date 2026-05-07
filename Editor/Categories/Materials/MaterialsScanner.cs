using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Export;
using UnityScanner.Core.Issues;
using UnityScanner.Utilities.Addressables;
using UnityScanner.Utilities.AssetDatabase;
using UnityScanner.Utilities.BuildLayout;
using Object = UnityEngine.Object;

namespace UnityScanner.Categories.Materials
{
    public static class MaterialsScanner
    {
        private static readonly HashSet<string> BuiltinShaderNames = new HashSet<string>
        {
            "Standard", "Standard (Specular setup)", "Standard (Roughness setup)",
            "Unlit/Color", "Unlit/Texture", "Unlit/Transparent", "Unlit/Transparent Cutout",
            "Particles/Standard Unlit", "Legacy Shaders/Diffuse", "Legacy Shaders/Specular",
            "Legacy Shaders/Bumped Diffuse", "Legacy Shaders/Bumped Specular",
            "Mobile/Diffuse", "Mobile/Unlit (Supports Lightmap)", "Mobile/VertexLit",
            "Mobile/VertexLit-OnlyDirectionalLights", "Mobile/Particles/Alpha Blended",
            "Mobile/Particles/Additive"
        };

        public static List<RendererComponentData> ScanRenderers(
            MaterialsSettings settings,
            USLiteBuildLayoutProvider buildLayout,
            IUnityScannerIssueSink issueSink)
        {
            USAddressablesReflection.ClearCache();
            var renderers = new List<RendererComponentData>();
            var assetPaths = AssetDatabase.GetAllAssetPaths();
            var total = assetPaths.Length;
            var count = 0;

            foreach (var assetPath in assetPaths)
            {
                count++;
                if (settings.GarbageCollectStep > 0 && count % settings.GarbageCollectStep == 0)
                {
                    GC.Collect();
                    issueSink.ReportProgress((float)count / total, "Scanning renderers");
                }

                if (settings.DebugLimit > 0 && renderers.Count >= settings.DebugLimit) break;

                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (type != typeof(GameObject)) continue;

                AnalyzeRendererAsset(assetPath, settings, buildLayout, renderers);
            }

            renderers.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel));
            return renderers;
        }

        public static List<MaterialAssetData> ScanMaterials(
            MaterialsSettings settings,
            USLiteBuildLayoutProvider buildLayout,
            List<RendererComponentData> renderers,
            IUnityScannerIssueSink issueSink)
        {
            USAddressablesReflection.ClearCache();
            var materials = new List<MaterialAssetData>();
            var materialToRendererPaths = BuildMaterialToRendererPaths(renderers);
            var assetPaths = AssetDatabase.GetAllAssetPaths();
            var total = assetPaths.Length;
            var count = 0;

            foreach (var assetPath in assetPaths)
            {
                count++;
                if (settings.GarbageCollectStep > 0 && count % settings.GarbageCollectStep == 0)
                {
                    GC.Collect();
                    issueSink.ReportProgress((float)count / total, "Scanning materials");
                }

                if (settings.DebugLimit > 0 && materials.Count >= settings.DebugLimit) break;

                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (type != typeof(Material)) continue;

                var matData = CreateMaterialData(assetPath, settings, buildLayout);
                FindMaterialWarnings(matData, settings);
                materials.Add(matData);
            }

            GC.Collect();

            if (settings.DuplicateMaterialsAreErrors)
                DetectDuplicateMaterials(materials);

            ApplyReferencedByPaths(materials, materialToRendererPaths);

            if (settings.UnusedMaterialsAreErrors)
                DetectUnusedMaterials(materials);

            AnalyzeMaterialVariantsAndPerformance(materials, settings);
            BuildMaterialTextureCrossReference(materials);

            materials.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel));
            return materials;
        }

        private static void AnalyzeRendererAsset(string assetPath, MaterialsSettings settings,
            USLiteBuildLayoutProvider buildLayout, List<RendererComponentData> renderers)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (go == null) return;

            var components = go.GetComponentsInChildren<Renderer>(true);

            foreach (var renderer in components)
            {
                var childName = CommonUtilities.GetFullName(renderer.transform);
                var bundleName = buildLayout?.GetBundleNameByAssetPath(assetPath) ?? string.Empty;
                var rowData = new RendererComponentData(assetPath, childName) { Bundle = bundleName };

                var sharedMaterials = renderer.sharedMaterials;

                if (sharedMaterials == null || sharedMaterials.Length == 0)
                {
                    if (settings.NullMaterialsAreErrors)
                    {
                        rowData.AddCustomWarning(MaterialsWarningMessages.NullMaterial);
                        rowData.TrySetWarningLevel(1);
                    }
                    rowData.MaterialSlotsCount = 0;
                    renderers.Add(rowData);
                    continue;
                }

                rowData.MaterialSlotsCount = sharedMaterials.Length;

                foreach (var mat in sharedMaterials)
                {
                    if (mat == null)
                    {
                        if (settings.NullMaterialsAreErrors)
                        {
                            rowData.AddCustomWarning(MaterialsWarningMessages.NullMaterialSlot);
                            rowData.TrySetWarningLevel(1);
                        }
                        continue;
                    }

                    var materialPath = AssetDatabase.GetAssetPath(mat);
                    if (materialPath.Contains("unity_builtin"))
                    {
                        if (settings.DefaultMaterialsAreErrors)
                        {
                            rowData.AddCustomWarning(MaterialsWarningMessages.UnityBuiltinMaterialAt(childName));
                            rowData.TrySetWarningLevel(2);
                        }
                    }
                }

                renderers.Add(rowData);
            }
        }

        private static MaterialAssetData CreateMaterialData(string path, MaterialsSettings settings,
            USLiteBuildLayoutProvider buildLayout)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            var typeName = USAssetTypeUtilities.GetReadableTypeName(type);
            var bytesSize = GetAssetSizeSafe(path);
            var readableSize = USExportUtilities.GetReadableSize(bytesSize);
            var bundleName = buildLayout?.GetBundleNameByAssetPath(path) ?? string.Empty;

            var data = new MaterialAssetData(path, type, typeName, bytesSize, readableSize,
                settings.TryUseReflectionForAddressablesDetection)
            {
                Bundle = bundleName
            };

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                var shader = material.shader;
                data.ShaderName = shader != null ? shader.name : "Unknown";
                data.RenderQueue = material.renderQueue;
                data.GpuInstancingEnabled = material.enableInstancing;
                data.EnabledKeywords = new List<string>(material.shaderKeywords);

                if (shader != null)
                    data.ShaderDefaultRenderQueue = shader.renderQueue;

                PopulateMaterialProperties(data, material);
                data.Fingerprint = ComputeMaterialFingerprint(material);
            }

            return data;
        }

        private static void FindMaterialWarnings(MaterialAssetData data, MaterialsSettings settings)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(data.Path);

            if (material == null)
            {
                data.AddCustomWarning(MaterialsWarningMessages.UnableToLoad);
                data.TrySetWarningLevel(2);
                return;
            }

            var shader = material.shader;

            if (shader == null)
            {
                data.AddCustomWarning(MaterialsWarningMessages.ShaderIsNull);
                data.TrySetWarningLevel(2);
                data.IsMissingShader = true;
            }
            else if (shader.name == "Hidden/InternalErrorShader")
            {
                data.AddCustomWarning(MaterialsWarningMessages.ShaderInternalErrorShader);
                data.TrySetWarningLevel(2);
                data.IsMissingShader = true;
            }
            else if (IsBuiltinShaderPublic(shader.name) && settings.BuiltinShadersAreErrors)
            {
                data.AddCustomWarning(MaterialsWarningMessages.BuiltInShaderLine(shader.name));
                data.TrySetWarningLevel(1);
                data.IsBuiltinShader = true;
            }

            if (data.HasRenderQueueOverride)
            {
                data.AddCustomWarning(MaterialsWarningMessages.RenderQueueOverrideLine(data.RenderQueue, data.ShaderDefaultRenderQueue));
                data.TrySetWarningLevel(1);
            }

            var shaderProps = shader != null ? ShaderUtil.GetPropertyCount(shader) : 0;
            for (var i = 0; i < shaderProps; i++)
            {
                var propType = ShaderUtil.GetPropertyType(shader, i);
                if (propType != ShaderUtil.ShaderPropertyType.TexEnv) continue;

                var propName = ShaderUtil.GetPropertyName(shader, i);
                var texture = material.GetTexture(propName);
                var texturePath = texture != null ? AssetDatabase.GetAssetPath(texture) : null;

                if (texture == null)
                {
                    if (settings.NullTexturesAreErrors)
                    {
                        data.AddCustomWarning(MaterialsWarningMessages.TextureIsNullAt(propName));
                        data.TrySetWarningLevel(1);
                    }
                }
                else if (texturePath != null && texturePath.Contains("unity_builtin"))
                {
                    if (settings.DefaultTexturesAreErrors)
                    {
                        data.AddCustomWarning(MaterialsWarningMessages.UnityBuiltinTextureAt(propName));
                        data.TrySetWarningLevel(2);
                    }
                }
            }
        }

        private static void PopulateMaterialProperties(MaterialAssetData data, Material material)
        {
            var shader = material.shader;
            if (shader == null) return;

            data.Properties = new List<MaterialPropertyData>();
            var propCount = ShaderUtil.GetPropertyCount(shader);

            for (var i = 0; i < propCount; i++)
            {
                var propName = ShaderUtil.GetPropertyName(shader, i);
                var propType = ShaderUtil.GetPropertyType(shader, i);
                var typeStr = propType.ToString();
                string value = null;
                string readableSize = null;

                switch (propType)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        value = material.GetColor(propName).ToString();
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        value = material.GetVector(propName).ToString();
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        value = material.GetFloat(propName).ToString();
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        var tex = material.GetTexture(propName);
                        if (tex != null)
                        {
                            var texPath = AssetDatabase.GetAssetPath(tex);
                            value = texPath;
                            var texType = AssetDatabase.GetMainAssetTypeAtPath(texPath);
                            if (texType == typeof(Texture2D))
                                readableSize = USExportUtilities.GetReadableSize(GetAssetSizeSafe(texPath));
                        }
                        else
                        {
                            value = "null";
                        }
                        break;
                }

                data.Properties.Add(new MaterialPropertyData
                {
                    Name = propName,
                    Type = typeStr,
                    Value = value,
                    ReadableSize = readableSize
                });
            }
        }

        private static string ComputeMaterialFingerprint(Material material)
        {
            var sb = new StringBuilder();
            var shader = material.shader;
            sb.Append("shader:").Append(shader != null ? shader.name : "null").Append(';');
            sb.Append("queue:").Append(material.renderQueue).Append(';');

            var keywords = material.shaderKeywords;
            Array.Sort(keywords);
            sb.Append("keywords:").Append(string.Join(",", keywords)).Append(';');

            if (shader != null)
            {
                var propCount = ShaderUtil.GetPropertyCount(shader);
                for (var i = 0; i < propCount; i++)
                {
                    var propName = ShaderUtil.GetPropertyName(shader, i);
                    var propType = ShaderUtil.GetPropertyType(shader, i);

                    if (propType == ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        var tex = material.GetTexture(propName);
                        if (tex != null)
                        {
                            sb.Append("tex:").Append(propName).Append('=')
                                .Append(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tex))).Append(';');
                        }
                    }
                    else if (propType == ShaderUtil.ShaderPropertyType.Color)
                        sb.Append("col:").Append(propName).Append('=').Append(material.GetColor(propName).ToString()).Append(';');
                    else if (propType == ShaderUtil.ShaderPropertyType.Vector)
                        sb.Append("vec:").Append(propName).Append('=').Append(material.GetVector(propName).ToString()).Append(';');
                    else
                        sb.Append("flt:").Append(propName).Append('=').Append(material.GetFloat(propName).ToString()).Append(';');
                }
            }

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void DetectDuplicateMaterials(List<MaterialAssetData> materials)
        {
            var groups = new Dictionary<string, List<MaterialAssetData>>();
            foreach (var mat in materials)
            {
                if (string.IsNullOrEmpty(mat.Fingerprint)) continue;
                if (!groups.TryGetValue(mat.Fingerprint, out var list))
                {
                    list = new List<MaterialAssetData>();
                    groups[mat.Fingerprint] = list;
                }
                list.Add(mat);
            }

            foreach (var kvp in groups)
            {
                if (kvp.Value.Count <= 1) continue;
                foreach (var mat in kvp.Value)
                {
                    var others = kvp.Value.Where(m => m != mat).ToList();
                    var otherNames = string.Join(", ", others.Select(o => o.Name));
                    mat.AddCustomWarning(MaterialsWarningMessages.DuplicateOfLine(others.Count, otherNames));
                    mat.TrySetWarningLevel(1);
                    mat.DuplicatePaths = others.Select(o => o.Path).ToList();
                }
            }
        }

        private static void ApplyReferencedByPaths(List<MaterialAssetData> materials,
            Dictionary<string, List<string>> materialToRendererPaths)
        {
            foreach (var mat in materials)
            {
                if (materialToRendererPaths.TryGetValue(mat.Path, out var paths))
                    mat.ReferencedByPaths = new List<string>(paths);
                else
                    mat.ReferencedByPaths = new List<string>();
            }
        }

        private static void DetectUnusedMaterials(List<MaterialAssetData> materials)
        {
            foreach (var mat in materials)
            {
                if (mat.ReferencedByPaths.Count == 0 && !mat.InResources && !mat.IsAddressable)
                {
                    mat.AddCustomWarning(MaterialsWarningMessages.NotReferencedUnused);
                    mat.TrySetWarningLevel(1);
                }
            }
        }

        private static void AnalyzeMaterialVariantsAndPerformance(List<MaterialAssetData> materials,
            MaterialsSettings settings)
        {
            var pathToData = materials.ToDictionary(m => m.Path);

            foreach (var mat in materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(mat.Path);
                if (material == null) continue;

                if (TryGetIsMaterialVariant(material, out var isVariant))
                {
                    mat.IsVariant = isVariant;

                    if (isVariant)
                    {
                        if (TryGetParentMaterial(material, out var parentPath, out var parentLinkBroken, out var parentMaterial))
                        {
                            mat.ParentMaterialPath = parentPath;
                            mat.ParentLinkBroken = parentLinkBroken;

                            if (parentLinkBroken && settings.VariantChainsAreErrors)
                            {
                                mat.AddCustomWarning(MaterialsWarningMessages.VariantParentInvalid);
                                mat.TrySetWarningLevel(2);
                            }

                            mat.VariantChainDepth = ComputeVariantChainDepth(material);
                            if (mat.VariantChainDepth > settings.VariantDeepChainThreshold && settings.VariantChainsAreErrors)
                            {
                                mat.AddCustomWarning(MaterialsWarningMessages.VariantChainDepthLine(
                                    mat.VariantChainDepth, settings.VariantDeepChainThreshold));
                                mat.TrySetWarningLevel(1);
                            }

                            if (parentMaterial != null)
                            {
                                mat.VariantOverrideCount = ComputeVariantOverrideCount(material, parentMaterial);
                                if (mat.VariantOverrideCount > settings.VariantHeavyOverridesThreshold
                                    && settings.VariantHeavyOverridesAreErrors)
                                {
                                    mat.AddCustomWarning(MaterialsWarningMessages.HeavyVariantOverridesLine(
                                        mat.VariantOverrideCount, settings.VariantHeavyOverridesThreshold));
                                    mat.TrySetWarningLevel(1);
                                }
                            }
                        }
                    }
                }

                var shader = material.shader;
                if (shader != null)
                {
                    mat.SupportsGpuInstancing = TryGetGpuInstancingSupport(shader);
                    if (mat.SupportsGpuInstancing == true && !mat.GpuInstancingEnabled && settings.InstancingDisabledAreErrors)
                    {
                        mat.AddCustomWarning(MaterialsWarningMessages.GpuInstancingOff);
                        mat.TrySetWarningLevel(1);
                    }

                    mat.SrpBatcherCompatible = TryGetSrpBatcherCompatibility(shader);
                    if (mat.SrpBatcherCompatible == false && settings.SrpBatcherIncompatibleAreErrors)
                    {
                        mat.AddCustomWarning(MaterialsWarningMessages.ShaderNotSrpBatcherLine(shader.name));
                        mat.TrySetWarningLevel(1);
                    }
                }
            }

            BuildVariantHierarchyMetadata(pathToData);
        }

        private static void BuildVariantHierarchyMetadata(Dictionary<string, MaterialAssetData> pathToData)
        {
            foreach (var kvp in pathToData)
            {
                var mat = kvp.Value;
                if (string.IsNullOrEmpty(mat.ParentMaterialPath)) continue;

                if (pathToData.TryGetValue(mat.ParentMaterialPath, out var parent))
                    parent.AddVariantChildPath(mat.Path);
            }
        }

        private static void BuildMaterialTextureCrossReference(List<MaterialAssetData> materials)
        {
            var textureToMaterials = new Dictionary<string, List<string>>();

            foreach (var mat in materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(mat.Path);
                if (material == null || material.shader == null) continue;

                var texPaths = new List<string>();
                var propCount = ShaderUtil.GetPropertyCount(material.shader);

                for (var i = 0; i < propCount; i++)
                {
                    if (ShaderUtil.GetPropertyType(material.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv) continue;
                    var propName = ShaderUtil.GetPropertyName(material.shader, i);
                    var tex = material.GetTexture(propName);
                    if (tex == null) continue;

                    var texPath = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(texPath)) continue;

                    texPaths.Add(texPath);

                    if (!textureToMaterials.TryGetValue(texPath, out var matList))
                    {
                        matList = new List<string>();
                        textureToMaterials[texPath] = matList;
                    }
                    if (!matList.Contains(mat.Path))
                        matList.Add(mat.Path);
                }

                mat.SetReferencedTexturePaths(texPaths);
            }

            foreach (var mat in materials)
            {
                if (mat.Properties == null) continue;
                foreach (var prop in mat.Properties)
                {
                    if (prop.Type != "TexEnv" || string.IsNullOrEmpty(prop.Value) || prop.Value == "null") continue;
                    if (textureToMaterials.TryGetValue(prop.Value, out var matList))
                        prop.UsedByMaterialPaths = matList;
                }
            }
        }

        private static Dictionary<string, List<string>> BuildMaterialToRendererPaths(List<RendererComponentData> renderers)
        {
            var map = new Dictionary<string, List<string>>();
            foreach (var row in renderers)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(row.Path);
                if (go == null) continue;

                var components = go.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in components)
                {
                    if (renderer.transform.name != row.ChildName) continue;
                    var shared = renderer.sharedMaterials;
                    if (shared == null) continue;

                    foreach (var mat in shared)
                    {
                        if (mat == null) continue;
                        var matPath = AssetDatabase.GetAssetPath(mat);
                        if (string.IsNullOrEmpty(matPath) || matPath.Contains("unity_builtin")) continue;

                        if (!map.TryGetValue(matPath, out var list))
                        {
                            list = new List<string>();
                            map[matPath] = list;
                        }
                        if (!list.Contains(row.Path))
                            list.Add(row.Path);
                    }
                }
            }
            return map;
        }

        private static bool TryGetIsMaterialVariant(Material m, out bool isVariant)
        {
            isVariant = false;
            try
            {
                var prop = typeof(Material).GetProperty("isVariant",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    isVariant = (bool)prop.GetValue(m);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryGetParentMaterial(Material m, out string parentPath,
            out bool parentLinkBroken, out Material parentMaterial)
        {
            parentPath = null;
            parentLinkBroken = false;
            parentMaterial = null;

            try
            {
                var parentProp = typeof(Material).GetProperty("parent",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (parentProp != null)
                {
                    var parent = parentProp.GetValue(m) as Material;
                    if (parent == null)
                    {
                        var so = new SerializedObject(m);
                        var parentRef = so.FindProperty("m_Parent");
                        if (parentRef != null && parentRef.objectReferenceValue != null)
                        {
                            parentMaterial = parentRef.objectReferenceValue as Material;
                            parentPath = AssetDatabase.GetAssetPath(parentMaterial);
                            parentLinkBroken = false;
                            return true;
                        }
                        return false;
                    }
                    parentMaterial = parent;
                    parentPath = AssetDatabase.GetAssetPath(parent);
                    parentLinkBroken = string.IsNullOrEmpty(parentPath);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static int ComputeVariantChainDepth(Material m)
        {
            var depth = 0;
            var visited = new HashSet<Material>();
            var current = m;

            while (current != null && depth < 64)
            {
                if (!visited.Add(current)) break;

                if (!TryGetIsMaterialVariant(current, out var isVariant) || !isVariant) break;

                if (!TryGetParentMaterial(current, out _, out _, out var parent)) break;

                current = parent;
                depth++;
            }

            return depth;
        }

        private static int ComputeVariantOverrideCount(Material child, Material parent)
        {
            if (child.shader != parent.shader) return 999;

            var count = 0;
            var shader = child.shader;
            var propCount = ShaderUtil.GetPropertyCount(shader);

            if (child.renderQueue != parent.renderQueue) count++;
            if (child.enableInstancing != parent.enableInstancing) count++;

            var childKeywords = new HashSet<string>(child.shaderKeywords);
            var parentKeywords = new HashSet<string>(parent.shaderKeywords);
            if (!childKeywords.SetEquals(parentKeywords)) count++;

            for (var i = 0; i < propCount; i++)
            {
                var propName = ShaderUtil.GetPropertyName(shader, i);
                var propType = ShaderUtil.GetPropertyType(shader, i);

                if (propType == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    var tc = child.GetTexture(propName);
                    var tp = parent.GetTexture(propName);
                    if (TexturesDifferByAssetPath(tc, tp)) count++;
                }
                else
                {
                    if (PropertyValuesDiffer(propName, propType, child, parent)) count++;
                }
            }

            return count;
        }

        private static bool PropertyValuesDiffer(string name, ShaderUtil.ShaderPropertyType propType,
            Material child, Material parent)
        {
            switch (propType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    return child.GetColor(name) != parent.GetColor(name);
                case ShaderUtil.ShaderPropertyType.Vector:
                    return child.GetVector(name) != parent.GetVector(name);
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    return Math.Abs(child.GetFloat(name) - parent.GetFloat(name)) > 0.0001f;
                default:
                    return false;
            }
        }

        private static bool TexturesDifferByAssetPath(Texture tc, Texture tp)
        {
            if (tc == null && tp == null) return false;
            if (tc == null || tp == null) return true;
            return AssetDatabase.GetAssetPath(tc) != AssetDatabase.GetAssetPath(tp);
        }

        private static bool? TryGetGpuInstancingSupport(Shader shader)
        {
            try
            {
                var method = typeof(ShaderUtil).GetMethod("HasInstancing",
                    BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                    return (bool)method.Invoke(null, new object[] { shader });
            }
            catch { }
            return null;
        }

        private static bool? TryGetSrpBatcherCompatibility(Shader shader)
        {
            try
            {
                var method = typeof(ShaderUtil).GetMethod("IsShaderSrpBatcherCompatible",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    ?? typeof(ShaderUtil).GetMethod("IsSRPBatcherShaderCompatible",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (method != null)
                    return (bool)method.Invoke(null, new object[] { shader });
            }
            catch { }
            return null;
        }

        public static bool IsBuiltinShaderPublic(string shaderName) => BuiltinShaderNames.Contains(shaderName);

        private static long GetAssetSizeSafe(string assetPath)
        {
            try
            {
                if (!File.Exists(assetPath)) return 0;
                return new FileInfo(assetPath).Length;
            }
            catch { return 0; }
        }

        private static class CommonUtilities
        {
            public static string GetFullName(Transform transform)
            {
                var sb = new StringBuilder();
                var current = transform;
                while (current != null)
                {
                    if (sb.Length > 0) sb.Insert(0, "/");
                    sb.Insert(0, current.name);
                    current = current.parent;
                }
                return sb.ToString();
            }
        }
    }
}
