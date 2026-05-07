using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityScanner.Categories.ProjectHealth
{
    public static class ProjectHealthScanner
    {
        public static void ScanAll(
            ProjectHealthSettings settings,
            List<ProjectHealthEntry> results,
            IUnityScannerIssueSink issueSink)
        {
            var assetRoot = Path.GetFullPath("Assets");

            if (settings.CheckEmptyFolders || settings.CheckLargeFolders || settings.CheckDeepNesting)
            {
                ScanFolders(settings, assetRoot, results, issueSink);
            }

            if (settings.CheckOrphanedMeta)
            {
                ScanOrphanedMeta(settings, assetRoot, results, issueSink);
            }

            if (settings.CheckBrokenAssets)
            {
                ScanBrokenAssets(settings, results, issueSink);
            }

            if (settings.CheckEmptyScenes)
            {
                ScanEmptyScenes(settings, results, issueSink);
            }
        }

        private static void ScanFolders(
            ProjectHealthSettings settings, string assetRoot,
            List<ProjectHealthEntry> results, IUnityScannerIssueSink issueSink)
        {
            var dirs = new List<string>();
            try
            {
                dirs = Directory.EnumerateDirectories(assetRoot, "*", SearchOption.AllDirectories).ToList();
            }
            catch { return; }

            var total = dirs.Count;
            for (var i = 0; i < dirs.Count; i++)
            {
                if (i % 100 == 0)
                    issueSink.ReportProgress((float)i / total * 0.4f, "Scanning folders...");

                var dir = dirs[i];
                var relativePath = MakeRelative(dir);

                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    relativePath.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (settings.CheckEmptyFolders)
                {
                    var hasFiles = false;
                    var hasOnlyMeta = true;
                    try
                    {
                        var files = Directory.EnumerateFiles(dir);
                        foreach (var f in files)
                        {
                            if (!f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                            {
                                hasFiles = true;
                                hasOnlyMeta = false;
                                break;
                            }
                        }
                    }
                    catch { continue; }

                    if (!hasFiles)
                    {
                        var hasSubDirs = false;
                        try
                        {
                            hasSubDirs = Directory.EnumerateDirectories(dir).Any();
                        }
                        catch { }

                        if (!hasSubDirs)
                        {
                            var entry = new ProjectHealthEntry
                            {
                                Path = relativePath,
                                Name = Path.GetFileName(dir),
                                IssueType = hasOnlyMeta ? ProjectHealthIssueType.MetaOnlyFolder : ProjectHealthIssueType.EmptyFolder,
                                Detail = hasOnlyMeta
                                    ? "Folder contains only .meta files with no actual assets"
                                    : "Empty folder with no files or subdirectories"
                            };
                            entry.TrySetWarningLevel(1);
                            results.Add(entry);
                            continue;
                        }
                    }
                }

                if (settings.CheckDeepNesting)
                {
                    var depth = CountPathDepth(relativePath);
                    if (depth > settings.MaxFolderNestingDepth)
                    {
                        var entry = new ProjectHealthEntry
                        {
                            Path = relativePath,
                            Name = Path.GetFileName(dir),
                            IssueType = ProjectHealthIssueType.DeepNesting,
                            Detail = "Folder nesting depth " + depth + " exceeds threshold " + settings.MaxFolderNestingDepth
                        };
                        entry.TrySetWarningLevel(1);
                        results.Add(entry);
                    }
                }

                if (settings.CheckLargeFolders)
                {
                    var fileCount = 0;
                    try
                    {
                        fileCount = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly).Length;
                    }
                    catch { }

                    if (fileCount > settings.MaxFilesPerFolder)
                    {
                        var entry = new ProjectHealthEntry
                        {
                            Path = relativePath,
                            Name = Path.GetFileName(dir),
                            IssueType = ProjectHealthIssueType.LargeFolder,
                            Detail = fileCount + " files in single folder (threshold: " + settings.MaxFilesPerFolder + ")"
                        };
                        entry.TrySetWarningLevel(1);
                        results.Add(entry);
                    }
                }
            }
        }

        private static void ScanOrphanedMeta(
            ProjectHealthSettings settings, string assetRoot,
            List<ProjectHealthEntry> results, IUnityScannerIssueSink issueSink)
        {
            var metaFiles = new List<string>();
            try
            {
                metaFiles = Directory.EnumerateFiles(assetRoot, "*.meta", SearchOption.AllDirectories).ToList();
            }
            catch { return; }

            var total = metaFiles.Count;
            for (var i = 0; i < metaFiles.Count; i++)
            {
                if (i % 200 == 0)
                    issueSink.ReportProgress(0.4f + (float)i / total * 0.2f, "Scanning orphaned .meta files...");

                var metaPath = metaFiles[i];
                var relativePath = MakeRelative(metaPath);

                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    relativePath.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var assetPath = metaPath.Substring(0, metaPath.Length - 5);
                if (File.Exists(assetPath) || Directory.Exists(assetPath))
                    continue;

                var entry = new ProjectHealthEntry
                {
                    Path = relativePath,
                    Name = Path.GetFileName(metaPath),
                    IssueType = ProjectHealthIssueType.OrphanedMeta,
                    FileSizeBytes = new FileInfo(metaPath).Length,
                    Detail = "Orphaned .meta file — no corresponding asset or folder exists"
                };
                entry.TrySetWarningLevel(2);
                results.Add(entry);
            }
        }

        private static void ScanBrokenAssets(
            ProjectHealthSettings settings,
            List<ProjectHealthEntry> results,
            IUnityScannerIssueSink issueSink)
        {
            var assetPaths = AssetDatabase.GetAllAssetPaths();
            var total = assetPaths.Length;

            for (var i = 0; i < assetPaths.Length; i++)
            {
                if (i % 500 == 0)
                    issueSink.ReportProgress(0.6f + (float)i / total * 0.25f, "Scanning for broken assets...");

                var assetPath = assetPaths[i];

                if (!assetPath.StartsWith("Assets/"))
                    continue;

                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    assetPath.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (assetPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Directory.Exists(assetPath))
                    continue;

                if (!File.Exists(assetPath))
                    continue;

                try
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    if (obj == null)
                    {
                        var entry = new ProjectHealthEntry
                        {
                            Path = assetPath,
                            Name = Path.GetFileName(assetPath),
                            IssueType = ProjectHealthIssueType.BrokenAsset,
                            FileSizeBytes = new FileInfo(Path.GetFullPath(assetPath)).Length,
                            Detail = "Asset could not be loaded — possibly corrupted or missing importer"
                        };
                        entry.TrySetWarningLevel(3);
                        results.Add(entry);
                    }
                }
                catch (Exception ex)
                {
                    var entry = new ProjectHealthEntry
                    {
                        Path = assetPath,
                        Name = Path.GetFileName(assetPath),
                        IssueType = ProjectHealthIssueType.BrokenAsset,
                        FileSizeBytes = new FileInfo(Path.GetFullPath(assetPath)).Length,
                        Detail = "Asset threw exception on load: " + ex.Message
                    };
                    entry.TrySetWarningLevel(3);
                    results.Add(entry);
                }
            }
        }

        private static void ScanEmptyScenes(
            ProjectHealthSettings settings,
            List<ProjectHealthEntry> results,
            IUnityScannerIssueSink issueSink)
        {
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var total = sceneGuids.Length;

            for (var i = 0; i < sceneGuids.Length; i++)
            {
                if (i % 5 == 0)
                    issueSink.ReportProgress(0.85f + (float)i / total * 0.15f, "Scanning for empty scenes...");

                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (scenePath.StartsWith("Packages/") || scenePath.StartsWith("Library/"))
                    continue;

                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    scenePath.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Scene scene;
                try
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }
                catch { continue; }

                try
                {
                    if (scene.rootCount == 0)
                    {
                        var entry = new ProjectHealthEntry
                        {
                            Path = scenePath,
                            Name = Path.GetFileName(scenePath),
                            IssueType = ProjectHealthIssueType.EmptyScene,
                            FileSizeBytes = new FileInfo(Path.GetFullPath(scenePath)).Length,
                            Detail = "Scene has zero root objects — effectively empty"
                        };
                        entry.TrySetWarningLevel(1);
                        results.Add(entry);
                    }
                }
                finally
                {
                    if (SceneManager.sceneCount > 1)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static string MakeRelative(string fullPath)
        {
            var projectPath = Path.GetFullPath(".");
            if (fullPath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(projectPath.Length + 1).Replace('\\', '/');
            return fullPath.Replace('\\', '/');
        }

        private static int CountPathDepth(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return 0;
            var count = 0;
            foreach (var c in relativePath)
                if (c == '/' || c == '\\') count++;
            return count;
        }
    }
}
