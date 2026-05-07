using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.Dependencies
{
    public class DependenciesFixProvider : IUnityScannerFixProvider
    {
        public bool CanFix(UnityScannerIssue issue)
        {
            return issue?.CategoryId == "dependencies"
                   && issue.FixId == "delete"
                   && !string.IsNullOrEmpty(issue.AssetPath);
        }

        public UnityScannerFixPreview Preview(UnityScannerIssue issue, UnityScannerScanContext context)
        {
            return new UnityScannerFixPreview
            {
                Description = $"Delete unreferenced asset: {issue.AssetPath}",
                IsSafe = false,
                AffectedAssets = new List<string> { issue.AssetPath }
            };
        }

        public IEnumerator Apply(UnityScannerIssue issue, UnityScannerScanContext context)
        {
            if (string.IsNullOrEmpty(issue.AssetPath)) yield break;

            var deleted = AssetDatabase.DeleteAsset(issue.AssetPath);
            if (deleted)
                Debug.Log($"[US] Deleted unreferenced asset: {issue.AssetPath}");
            else
                Debug.LogWarning($"[US] Failed to delete: {issue.AssetPath}");

            yield return null;
        }

        public static int BackupAssets(List<DependenciesAssetData> assets, string backupDirectory)
        {
            var backedUpCount = 0;

            foreach (var asset in assets)
            {
                try
                {
                    var destPath = Path.Combine(backupDirectory, asset.Path);
                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    File.Copy(asset.Path, destPath, true);

                    var metaPath = asset.Path + ".meta";
                    if (File.Exists(metaPath))
                    {
                        var destMetaPath = destPath + ".meta";
                        File.Copy(metaPath, destMetaPath, true);
                    }

                    backedUpCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to back up {asset.Path}: {e.Message}");
                }
            }

            return backedUpCount;
        }

        public static List<string> DeleteAssets(List<DependenciesAssetData> assets)
        {
            var deletedPaths = new List<string>();

            foreach (var asset in assets)
            {
                var deleted = AssetDatabase.DeleteAsset(asset.Path);
                if (deleted)
                    deletedPaths.Add(asset.Path);
            }

            if (deletedPaths.Count > 0)
                AssetDatabase.Refresh();

            return deletedPaths;
        }
    }
}
