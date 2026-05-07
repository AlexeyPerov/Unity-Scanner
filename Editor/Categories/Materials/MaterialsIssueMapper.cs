using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.Materials
{
    public static class MaterialsIssueMapper
    {
        public const string CodeNullMaterial = "null_material";
        public const string CodeNullMaterialSlot = "null_material_slot";
        public const string CodeBuiltinMaterial = "builtin_material";
        public const string CodeUnableToLoad = "unable_to_load";
        public const string CodeNullTexture = "null_texture";
        public const string CodeBuiltinTexture = "builtin_texture";
        public const string CodeShaderNull = "shader_null";
        public const string CodeShaderInternalError = "shader_internal_error";
        public const string CodeBuiltinShader = "builtin_shader";
        public const string CodeRenderQueueOverride = "render_queue_override";
        public const string CodeDuplicateMaterial = "duplicate_material";
        public const string CodeUnusedMaterial = "unused_material";
        public const string CodeVariantParentInvalid = "variant_parent_invalid";
        public const string CodeVariantDeepChain = "variant_deep_chain";
        public const string CodeVariantHeavyOverrides = "variant_heavy_overrides";
        public const string CodeGpuInstancingOff = "gpu_instancing_off";
        public const string CodeSrpBatcherIncompatible = "srp_batcher_incompatible";

        public static List<UnityScannerIssue> MapRendererIssues(
            List<RendererComponentData> renderers,
            MaterialsSettings settings)
        {
            var issues = new List<UnityScannerIssue>();

            foreach (var row in renderers)
            {
                if (row.CustomWarnings == null) continue;

                foreach (var warning in row.CustomWarnings)
                {
                    var issue = MapRendererWarning(warning, row, settings);
                    if (issue != null)
                        issues.Add(issue);
                }
            }

            return issues;
        }

        public static List<UnityScannerIssue> MapMaterialIssues(
            List<MaterialAssetData> materials,
            MaterialsSettings settings)
        {
            var issues = new List<UnityScannerIssue>();

            foreach (var mat in materials)
            {
                if (mat.CustomWarnings == null) continue;

                foreach (var warning in mat.CustomWarnings)
                {
                    var issue = MapMaterialWarning(warning, mat, settings);
                    if (issue != null)
                        issues.Add(issue);
                }
            }

            return issues;
        }

        private static UnityScannerIssue MapRendererWarning(
            string warning,
            RendererComponentData row,
            MaterialsSettings settings)
        {
            if (warning == MaterialsWarningMessages.NullMaterial)
                return MakeIssue("materials", CodeNullMaterial, warning,
                    UnityScannerIssueSeverity.Warning, row.Path);

            if (warning == MaterialsWarningMessages.NullMaterialSlot)
                return MakeIssue("materials", CodeNullMaterialSlot, warning,
                    UnityScannerIssueSeverity.Warning, row.Path);

            if (warning.StartsWith(MaterialsWarningMessages.UnityBuiltinMaterialPrefix))
                return MakeIssue("materials", CodeBuiltinMaterial, warning,
                    UnityScannerIssueSeverity.Warning, row.Path);

            return null;
        }

        private static UnityScannerIssue MapMaterialWarning(
            string warning,
            MaterialAssetData mat,
            MaterialsSettings settings)
        {
            if (warning == MaterialsWarningMessages.UnableToLoad)
                return MakeIssue("materials", CodeUnableToLoad, warning,
                    UnityScannerIssueSeverity.Error, mat.Path);

            if (warning == MaterialsWarningMessages.ShaderIsNull)
                return MakeIssue("materials", CodeShaderNull, warning,
                    UnityScannerIssueSeverity.Error, mat.Path);

            if (warning == MaterialsWarningMessages.ShaderInternalErrorShader)
                return MakeIssue("materials", CodeShaderInternalError, warning,
                    UnityScannerIssueSeverity.Error, mat.Path);

            if (warning.StartsWith(MaterialsWarningMessages.BuiltInShaderPrefix))
                return MakeIssue("materials", CodeBuiltinShader, warning,
                    UnityScannerIssueSeverity.Warning, mat.Path);

            if (warning.StartsWith(MaterialsWarningMessages.RenderQueueOverridePrefix))
                return MakeIssue("materials", CodeRenderQueueOverride, warning,
                    UnityScannerIssueSeverity.Warning, mat.Path);

            if (warning.StartsWith(MaterialsWarningMessages.TextureNullPrefix))
                return MakeIssue("materials", CodeNullTexture, warning,
                    UnityScannerIssueSeverity.Info, mat.Path);

            if (warning.StartsWith(MaterialsWarningMessages.UnityBuiltinTexturePrefix))
                return MakeIssue("materials", CodeBuiltinTexture, warning,
                    UnityScannerIssueSeverity.Warning, mat.Path);

            if (warning.StartsWith(MaterialsWarningMessages.DuplicateOfPrefix))
                return MakeIssue("materials", CodeDuplicateMaterial, warning,
                    UnityScannerIssueSeverity.Warning, mat.Path);

            if (warning == MaterialsWarningMessages.NotReferencedUnused)
                return MakeIssue("materials", CodeUnusedMaterial, warning,
                    UnityScannerIssueSeverity.Warning, mat.Path);

            if (warning == MaterialsWarningMessages.VariantParentInvalid)
                return MakeIssue("materials", CodeVariantParentInvalid, warning,
                    UnityScannerIssueSeverity.Error, mat.Path);

            if (warning.Contains(MaterialsWarningMessages.TokenVariantChainDepth) &&
                warning.Contains(MaterialsWarningMessages.TokenExceedsThreshold))
                return MakeIssue("materials", CodeVariantDeepChain, warning,
                    UnityScannerIssueSeverity.Warning, mat.Path);

            if (warning.StartsWith(MaterialsWarningMessages.HeavyVariantOverridesPrefix))
                return MakeIssue("materials", CodeVariantHeavyOverrides, warning,
                    UnityScannerIssueSeverity.Warning, mat.Path);

            if (warning.Contains(MaterialsWarningMessages.TokenGpuInstancingDisabled))
                return MakeIssue("materials", CodeGpuInstancingOff, warning,
                    UnityScannerIssueSeverity.Warning, mat.Path);

            if (warning.Contains(MaterialsWarningMessages.TokenSrpBatcher))
                return MakeIssue("materials", CodeSrpBatcherIncompatible, warning,
                    UnityScannerIssueSeverity.Warning, mat.Path);

            return null;
        }

        private static UnityScannerIssue MakeIssue(
            string categoryId, string code, string description,
            UnityScannerIssueSeverity severity, string assetPath)
        {
            return new UnityScannerIssue
            {
                CategoryId = categoryId,
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };
        }
    }
}
