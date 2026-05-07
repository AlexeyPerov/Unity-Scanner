using System;
using System.Collections.Generic;
using System.IO;
using UnityScanner.Core.Export;
using UnityScanner.Utilities.AssetDatabase;
using UnityScanner.Utilities.Addressables;

namespace UnityScanner.Categories.Dependencies
{
    public class DependenciesAssetData
    {
        public string Path;
        public string ShortPath;
        public Type Type;
        public string TypeName;
        public long BytesSize;
        public string ReadableSize;
        public bool IsAddressable;
        public int ReferencesCount;
        public List<string> ReferencedByPaths = new List<string>();
        public string FalsePositiveWarning;
        public bool ValidType => Type != null;
        public bool Foldout;
        public bool ShowReferencedByAssets;
        public bool Selected;
        public bool IsEligibleForDeletion => ReferencesCount == 0 && !IsAddressable;

        public static DependenciesAssetData Create(
            string path,
            Type type,
            int referencesCount,
            List<string> referencedByPaths,
            string falsePositiveWarning,
            bool tryUseReflectionForAddressablesDetection)
        {
            var typeName = USAssetTypeUtilities.GetReadableTypeName(type);

            long bytesSize = 0;
            if (File.Exists(path))
            {
                try { bytesSize = new FileInfo(path).Length; }
                catch { }
            }

            var readableSize = USExportUtilities.GetReadableSize(bytesSize);
            var isAddressable = USAddressablesReflection.IsAssetAddressable(path, tryUseReflectionForAddressablesDetection);

            var shortPath = path.Length > 35
                ? "..." + path.Substring(path.Length - 32)
                : path;

            return new DependenciesAssetData
            {
                Path = path,
                ShortPath = shortPath,
                Type = type,
                TypeName = typeName,
                BytesSize = bytesSize,
                ReadableSize = readableSize,
                IsAddressable = isAddressable,
                ReferencesCount = referencesCount,
                ReferencedByPaths = referencedByPaths != null ? new List<string>(referencedByPaths) : new List<string>(),
                FalsePositiveWarning = falsePositiveWarning
            };
        }
    }
}
