using System;
using System.Collections.Generic;

namespace UnityScanner.Categories.Materials
{
    public static class CustomWarningTooltips
    {
        private static readonly (Func<string, bool> Match, string Tip)[] Rules = BuildRules();

        private static (Func<string, bool> M, string T)[] BuildRules() => new[]
        {
            (Eq(MaterialsWarningMessages.NullMaterial),
                "This renderer's sharedMaterials list is empty. Rendering may be missing, pink, or use pipeline defaults until a material is assigned."),
            (Eq(MaterialsWarningMessages.NullMaterialSlot),
                "At least one entry in sharedMaterials is null. Submesh or slot mapping may be out of date; fix slots or the mesh's material list."),
            (StartsWith(MaterialsWarningMessages.UnityBuiltinMaterialPrefix),
                "A Unity built-in / internal default material is assigned on a slot. Use a project material for predictable and portable results."),
            (Eq(MaterialsWarningMessages.UnableToLoad),
                "The material asset could not be loaded. It may be missing, broken, or blocked by a script/import error."),
            (StartsWith(MaterialsWarningMessages.TextureNullPrefix),
                "The shader's texture property has no texture assigned. Assign a texture or clear unused slots if the shader still expects a binding."),
            (StartsWith(MaterialsWarningMessages.UnityBuiltinTexturePrefix),
                "This texture property points to a built-in/embedded Unity resource. Use a project texture for stable packaging and art direction."),
            (Eq(MaterialsWarningMessages.ShaderIsNull),
                "The material's shader is missing. The asset cannot render correctly until a valid shader is assigned."),
            (Eq(MaterialsWarningMessages.ShaderInternalErrorShader),
                "Unity is using the InternalErrorShader placeholder because the real shader is missing, broken, or not compiled for this SRP."),
            (StartsWith(MaterialsWarningMessages.BuiltInShaderPrefix),
                "The material uses a Unity built-in shader. If your project standard is custom/URP/HDRP shaders, replace or upgrade as needed."),
            (StartsWith(MaterialsWarningMessages.RenderQueueOverridePrefix),
                "Render queue is set differently from the shader's default, which can change transparency sorting and when the draw happens."),
            (StartsWith(MaterialsWarningMessages.DuplicateOfPrefix),
                "This material's serialized fingerprint matches other materials. Merging duplicates can cut asset count, variants, and maintenance."),
            (Eq(MaterialsWarningMessages.NotReferencedUnused),
                "No renderer in the scan references this material, and it is not in Resources/ nor marked Addressable, so it may be dead or only loaded indirectly."),
            (Eq(MaterialsWarningMessages.VariantParentInvalid),
                "This Material Variant (or an equivalent parent link) has no resolvable parent, so inherited values may be wrong or the setup is broken."),
            (w => w.Contains(MaterialsWarningMessages.TokenVariantChainDepth, StringComparison.Ordinal) &&
                  w.Contains(MaterialsWarningMessages.TokenExceedsThreshold, StringComparison.Ordinal),
                "The parent-to-parent chain is longer than the configured threshold, which is harder to author and reason about than shallow variants."),
            (StartsWith(MaterialsWarningMessages.HeavyVariantOverridesPrefix),
                "The variant changes many things versus its parent, so a standalone material or a new parent may be simpler than a deep override list."),
            (Eq(MaterialsWarningMessages.GpuInstancingOff),
                "The shader can batch instances, but the material has GPU instancing off, so you may be missing draw-call savings on repeated meshes."),
            (w => w.Contains(MaterialsWarningMessages.TokenSrpBatcher, StringComparison.Ordinal),
                "The shader is reported as not compatible with the SRP Batcher; you may not get the same per-frame CPU batching benefits as SRP Batcher-friendly shaders.")
        };

        private static Func<string, bool> Eq(string s) => w => w == s;
        private static Func<string, bool> StartsWith(string p) => w => w.StartsWith(p, StringComparison.Ordinal);

        public static string GetTooltipOrEmpty(string warning)
        {
            if (string.IsNullOrEmpty(warning))
                return string.Empty;
            foreach (var (m, t) in Rules)
            {
                if (m(warning))
                    return t;
            }
            return string.Empty;
        }
    }
}
