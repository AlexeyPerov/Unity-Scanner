using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Caching
{
    public class USCacheRebuildService
    {
        private readonly USCacheInvalidationService _invalidationService;

        public USCacheRebuildService(USCacheInvalidationService invalidationService)
        {
            _invalidationService = invalidationService;
        }

        public USAssetCacheData RebuildFull(string buildLayoutPath = null)
        {
            var data = new USAssetCacheData();
            var now = DateTime.UtcNow.Ticks;

            data.Header = new USCacheHeader
            {
                SchemaVersion = USCacheHeader.CurrentSchemaVersion,
                ToolVersion = "1.0.0",
                UnityVersion = Application.unityVersion,
                ProjectId = GetProjectId(),
                CreatedTimestamp = now,
                LastModifiedTimestamp = now
            };

            data.BuildLayoutPath = buildLayoutPath ?? string.Empty;
            data.BuildLayoutTimestamp = !string.IsNullOrEmpty(buildLayoutPath) && File.Exists(buildLayoutPath)
                ? new FileInfo(buildLayoutPath).LastWriteTimeUtc.Ticks
                : 0;

            var assetPaths = AssetDatabase.GetAllAssetPaths();

            var guidToEntry = new Dictionary<string, USAssetCacheEntry>();
            var guidToDependencies = new Dictionary<string, HashSet<string>>();

            foreach (var assetPath in assetPaths)
            {
                if (assetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (type == null || type == typeof(DefaultAsset))
                    continue;

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset == null)
                    continue;

                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long _))
                    continue;

                var entry = new USAssetCacheEntry
                {
                    Guid = guid,
                    Path = assetPath,
                    TypeName = type.FullName ?? type.Name,
                    FileSize = GetAssetSize(assetPath),
                    ImportMarker = GetMetaTimestamp(assetPath),
                    IsAddressable = false,
                    BundleName = string.Empty
                };

                guidToEntry[guid] = entry;
                data.PathToGuid[assetPath] = guid;

                var deps = new HashSet<string>();
                var dependencies = AssetDatabase.GetDependencies(assetPath, false);
                foreach (var depPath in dependencies)
                {
                    if (depPath == assetPath) continue;

                    var depAsset = AssetDatabase.LoadMainAssetAtPath(depPath);
                    if (depAsset == null) continue;

                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(depAsset, out var depGuid, out long _))
                        deps.Add(depGuid);
                }

                guidToDependencies[guid] = deps;
            }

            foreach (var kvp in guidToDependencies)
            {
                if (!guidToEntry.TryGetValue(kvp.Key, out var entry)) continue;
                entry.DirectDependencies = kvp.Value;

                foreach (var depGuid in kvp.Value)
                {
                    if (guidToEntry.TryGetValue(depGuid, out var depEntry))
                        depEntry.ReverseDependencies.Add(kvp.Key);
                }
            }

            foreach (var kvp in guidToEntry)
                data.EntriesByGuid[kvp.Key] = kvp.Value;

            return data;
        }

        public List<string> DetectInvalidEntries(USAssetCacheData data)
        {
            var invalidGuids = new List<string>();

            foreach (var kvp in data.EntriesByGuid)
            {
                var entry = kvp.Value;

                var currentPath = AssetDatabase.GUIDToAssetPath(kvp.Key);
                if (string.IsNullOrEmpty(currentPath))
                {
                    invalidGuids.Add(kvp.Key);
                    continue;
                }

                if (currentPath != entry.Path)
                {
                    invalidGuids.Add(kvp.Key);
                    continue;
                }

                var currentMarker = GetMetaTimestamp(currentPath);
                if (currentMarker != entry.ImportMarker)
                {
                    invalidGuids.Add(kvp.Key);
                }
            }

            return invalidGuids;
        }

        public void RebuildEntries(USAssetCacheData data, IEnumerable<string> guids)
        {
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    data.EntriesByGuid.Remove(guid);
                    continue;
                }

                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type == null)
                {
                    data.EntriesByGuid.Remove(guid);
                    continue;
                }

                var entry = new USAssetCacheEntry
                {
                    Guid = guid,
                    Path = path,
                    TypeName = type.FullName ?? type.Name,
                    FileSize = GetAssetSize(path),
                    ImportMarker = GetMetaTimestamp(path),
                    IsAddressable = false,
                    BundleName = string.Empty,
                    DirectDependencies = new HashSet<string>(),
                    ReverseDependencies = new HashSet<string>()
                };

                var dependencies = AssetDatabase.GetDependencies(path, false);
                foreach (var depPath in dependencies)
                {
                    if (depPath == path) continue;
                    var depAsset = AssetDatabase.LoadMainAssetAtPath(depPath);
                    if (depAsset == null) continue;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(depAsset, out var depGuid, out long _))
                        entry.DirectDependencies.Add(depGuid);
                }

                data.EntriesByGuid[guid] = entry;
                data.PathToGuid[path] = guid;
            }
        }

        private static long GetAssetSize(string assetPath)
        {
            try
            {
                if (!File.Exists(assetPath))
                    return 0;

                var info = new FileInfo(assetPath);
                return info.Length;
            }
            catch
            {
                return 0;
            }
        }

        private static string GetProjectId()
        {
            var projectPath = Directory.GetParent(Application.dataPath)?.FullName;
            return projectPath != null ? projectPath.GetHashCode().ToString("x") : string.Empty;
        }

        internal static long GetMetaTimestamp(string assetPath)
        {
            try
            {
                var metaPath = assetPath + ".meta";
                if (!File.Exists(metaPath))
                    return 0;
                return new FileInfo(metaPath).LastWriteTimeUtc.Ticks;
            }
            catch
            {
                return 0;
            }
        }
    }
}
