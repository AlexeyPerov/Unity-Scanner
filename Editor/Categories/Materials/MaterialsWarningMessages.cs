namespace UnityScanner.Categories.Materials
{
    public static class MaterialsWarningMessages
    {
        public const string NullMaterial = "Null material";
        public const string NullMaterialSlot = "Null material slot";
        public const string UnityBuiltinMaterialPrefix = "unity_builtin material at ";
        public const string UnableToLoad = "Unable to load";
        public const string TextureNullPrefix = "Texture is null at ";
        public const string UnityBuiltinTexturePrefix = "unity_builtin texture at ";
        public const string ShaderIsNull = "Shader is null";
        public const string ShaderInternalErrorShader = "Shader is missing (InternalErrorShader)";
        public const string BuiltInShaderPrefix = "Built-in shader: ";
        public const string RenderQueueOverridePrefix = "Render queue override: ";
        public const string DuplicateOfPrefix = "Duplicate of ";
        public const string NotReferencedUnused = "Not referenced by any renderer, not in Resources, not Addressable";
        public const string NotReferencedPrefix = "Not referenced by any renderer";
        public const string VariantParentInvalid = "Material variant: parent is missing or invalid";
        public const string GpuInstancingOff = "GPU instancing is disabled but shader supports it";
        public const string TokenSrpBatcher = "is not SRP Batcher compatible";
        public const string TokenVariantChainDepth = "Variant chain depth";
        public const string TokenExceedsThreshold = "exceeds threshold";
        public const string TokenHeavyVariantOverrides = "Heavy variant overrides";
        public const string HeavyVariantOverridesPrefix = "Heavy variant overrides: ";
        public const string TokenGpuInstancingDisabled = "GPU instancing is disabled";

        public static string UnityBuiltinMaterialAt(string fullTransformName) =>
            UnityBuiltinMaterialPrefix + fullTransformName;

        public static string TextureIsNullAt(string propertyName) =>
            TextureNullPrefix + propertyName;

        public static string UnityBuiltinTextureAt(string propertyName) =>
            UnityBuiltinTexturePrefix + propertyName;

        public static string BuiltInShaderLine(string shaderName) =>
            BuiltInShaderPrefix + shaderName;

        public static string RenderQueueOverrideLine(int current, int? shaderDefault) =>
            $"{RenderQueueOverridePrefix}{current} (shader default: {shaderDefault})";

        public static string DuplicateOfLine(int otherCount, string otherNamesJoined) =>
            $"{DuplicateOfPrefix}{otherCount} material(s): {otherNamesJoined}";

        public static string VariantChainDepthLine(int chainDepth, int threshold) =>
            $"{TokenVariantChainDepth} {chainDepth} {TokenExceedsThreshold} {threshold}";

        public static string HeavyVariantOverridesLine(int? overrideCount, int threshold) =>
            $"{HeavyVariantOverridesPrefix}{overrideCount} (threshold {threshold})";

        public static string ShaderNotSrpBatcherLine(string shaderName) =>
            $"Shader \"{shaderName}\" {TokenSrpBatcher}";
    }
}
