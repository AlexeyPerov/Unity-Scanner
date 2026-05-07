using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Caching
{
    public class USCacheInvalidationService
    {
        public bool ShouldInvalidate(USAssetCacheData cacheData, USCacheValidationContext context)
        {
            if (context == null || cacheData?.Header == null)
                return true;

            if (context.ForceRebuild)
                return true;

            if (cacheData.Header.SchemaVersion != USCacheHeader.CurrentSchemaVersion)
                return true;

            if (!string.IsNullOrEmpty(context.UnityVersion)
                && cacheData.Header.UnityVersion != context.UnityVersion)
                return true;

            if (!string.IsNullOrEmpty(context.ProjectId)
                && cacheData.Header.ProjectId != context.ProjectId)
                return true;

            if (!string.IsNullOrEmpty(context.BuildLayoutPath)
                && (cacheData.BuildLayoutPath != context.BuildLayoutPath
                    || cacheData.BuildLayoutTimestamp != context.BuildLayoutTimestamp))
                return true;

            return false;
        }

        public bool ShouldInvalidateEntry(USAssetCacheEntry entry, string currentPath, long currentImportMarker)
        {
            if (entry == null)
                return true;

            if (entry.Path != currentPath)
                return true;

            if (entry.ImportMarker != currentImportMarker)
                return true;

            return false;
        }

        public List<string> FindInvalidEntries(USAssetCacheData cacheData)
        {
            var invalid = new List<string>();

            if (cacheData?.EntriesByGuid == null)
                return invalid;

            foreach (var kvp in cacheData.EntriesByGuid)
            {
                var guid = kvp.Key;
                var entry = kvp.Value;

                var currentPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(currentPath))
                {
                    invalid.Add(guid);
                    continue;
                }

                var currentMarker = GetMetaTimestamp(currentPath);

                if (ShouldInvalidateEntry(entry, currentPath, currentMarker))
                    invalid.Add(guid);
            }

            return invalid;
        }

        private static long GetMetaTimestamp(string assetPath)
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
