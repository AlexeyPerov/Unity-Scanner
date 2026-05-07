using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Categories.Addressables
{
    public static class USAddressablesAnalysis
    {
        public static void PerformGroupsAnalysis(
            USAddressablesBuildLayoutProvider layout,
            USAddressablesRecommendationsSummary summary,
            long remoteDependencyStartupWarningThresholdBytes)
        {
            foreach (var group in layout.Groups)
            {
                foreach (var bundle in group.Archives)
                {
                    var allDeps = bundle.AllBundleDependencies;
                    var circularDeps = allDeps.Where(item => item.BundleDependencies.Contains(bundle)).ToList();
                    if (circularDeps.Count <= 0)
                        continue;

                    var circularInfo = circularDeps.Aggregate(string.Empty,
                        (current, circularDep) => current + $"[{circularDep.Name}]");
                    var msg = summary?.AddRecommendation(group.Name,
                        $"Bundle {bundle.Name} of group {group.Name} has CIRCULAR dependencies with {circularInfo}",
                        4);
                    if (msg != null)
                        bundle.Recommendations.Add(msg);
                    bundle.TrySetWarningLevel(4);
                }
            }

            foreach (var group in layout.Groups)
            {
                foreach (var bundle in group.Archives)
                {
                    if (bundle.AllAssets.Count == 0)
                    {
                        var msg = summary?.AddRecommendation(group.Name,
                            $"Bundle {bundle.Name} of group {group.Name} contains no assets", 4);
                        if (msg != null)
                            bundle.Recommendations.Add(msg);
                        bundle.TrySetWarningLevel(4);
                    }
                    else
                    {
                        foreach (var asset in bundle.AllAssets)
                        {
                            if (!asset.Name.Contains("builtin"))
                                continue;

                            var msg = summary?.AddRecommendation(group.Name,
                                $"Bundle {bundle.Name} of group {group.Name} contains builtin asset {asset.Name}. This might cause a duplicate with Unity builtin assets",
                                1);
                            if (msg != null)
                                bundle.Recommendations.Add(msg);
                            bundle.TrySetWarningLevel(1);
                        }
                    }

                    var isCurrentRemote = USAddressablesBundleUtilities.IsBundleRemote(bundle.Name);
                    foreach (var bundleDependency in bundle.BundleDependencies)
                    {
                        var isDependencyRemote = USAddressablesBundleUtilities.IsBundleRemote(bundleDependency.Name);
                        if (!isCurrentRemote && isDependencyRemote)
                        {
                            var msg = summary?.AddRecommendation(group.Name,
                                $"Built-In bundle {bundle.Name} of group {group.Name} directly (!) references remote bundle {bundleDependency.Name}",
                                5);
                            if (msg != null)
                                bundle.Recommendations.Add(msg);
                            bundle.TrySetWarningLevel(5);
                        }
                    }

                    foreach (var expandedBundleDependency in bundle.ExpandedBundleDependencies)
                    {
                        var isDependencyRemote = USAddressablesBundleUtilities.IsBundleRemote(expandedBundleDependency.Name);
                        if (!isCurrentRemote && isDependencyRemote)
                        {
                            var msg = summary?.AddRecommendation(group.Name,
                                $"Built-In bundle {bundle.Name} of group {group.Name} references remote bundle {expandedBundleDependency.Name}",
                                4);
                            if (msg != null)
                                bundle.Recommendations.Add(msg);
                            bundle.TrySetWarningLevel(4);
                        }
                    }

                    if (!isCurrentRemote)
                        continue;

                    var isCurrentStartup = USAddressablesBundleUtilities.IsBundleStartup(bundle.Name);
                    if (!isCurrentStartup)
                        continue;

                    var referencedRemoteSize = 0L;
                    var referencedBundlesList = string.Empty;
                    foreach (var bundleDependency in bundle.AllBundleDependencies.OrderByDescending(item => item.Size))
                    {
                        var isDependencyRemote = USAddressablesBundleUtilities.IsBundleRemote(bundleDependency.Name);
                        var isDependencyStartup = USAddressablesBundleUtilities.IsBundleStartup(bundleDependency.Name);
                        if (!isDependencyRemote || isDependencyStartup)
                            continue;

                        referencedRemoteSize += bundleDependency.Size;
                        referencedBundlesList += bundleDependency.Name + "[" +
                                                 EditorUtility.FormatBytes(bundleDependency.Size) +
                                                 "]; ";
                        bundle.TrySetWarningLevel(1);
                    }

                    if (referencedRemoteSize <= 0)
                        continue;

                    var warningLevel = referencedRemoteSize >= remoteDependencyStartupWarningThresholdBytes ? 3 : 1;
                    var recommendation = summary?.AddRecommendation(group.Name,
                        $"Startup remote bundle {bundle.Name} of group {group.Name} references remote (non-startup) bundles with total size of {EditorUtility.FormatBytes(referencedRemoteSize)}. Bundles: {referencedBundlesList}",
                        warningLevel);
                    if (recommendation != null)
                        bundle.Recommendations.Add(recommendation);
                    bundle.TrySetWarningLevel(warningLevel);
                }

                group.TopWarning = group.Archives.Count > 0 ? group.Archives.Max(item => item.TopWarning) : 3;
            }

            foreach (var pair in layout.AssetsByPath)
            {
                if (pair.Value.IncludedByBundle.Count(item => item != pair.Value.IncludedInBundle) <= 0)
                    continue;

                pair.Value.TopWarning = Mathf.Max(
                    pair.Value.IncludedByBundle.Where(item => item != pair.Value.IncludedInBundle)
                        .Max(item => item.TopWarning), pair.Value.TopWarning);
            }
        }
    }
}
