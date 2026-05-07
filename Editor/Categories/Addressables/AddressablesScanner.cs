using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.Addressables
{
    public static class AddressablesScanner
    {
        public static AddressablesScanResult Scan(
            string buildLayoutPath,
            USAddressablesSettings settings,
            IUnityScannerIssueSink issueSink)
        {
            if (string.IsNullOrEmpty(buildLayoutPath) || !File.Exists(buildLayoutPath))
            {
                issueSink.ReportProgress(1f, "BuildLayout file not found");
                return null;
            }

            issueSink.ReportProgress(0.1f, "Parsing BuildLayout...");

            USAddressablesBuildLayoutProvider layout;
            try
            {
                var parsed = USAddressablesBuildLayoutParser.Load(buildLayoutPath);
                layout = new USAddressablesBuildLayoutProvider(parsed);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }

            issueSink.ReportProgress(0.4f, "Analyzing groups and dependencies...");

            var summary = new USAddressablesRecommendationsSummary();
            var threshold = settings.RemoteDependencyStartupWarningThresholdBytes;
            USAddressablesAnalysis.PerformGroupsAnalysis(layout, summary, threshold);

            issueSink.ReportProgress(0.6f, "Detecting duplicates...");

            var duplicates = BuildDuplicates(layout);

            issueSink.ReportProgress(0.8f, "Computing metrics...");

            var totalWasted = duplicates.Sum(x => x.WastedSize);
            var maxStartupRemote = ComputeMaxStartupRemoteDeps(layout);

            var gateResults = ComputeGates(layout, settings, totalWasted, maxStartupRemote);

            var comparisonService = new USAddressablesBundleComparisonService();
            comparisonService.SetOriginalLayout(layout);

            var result = new AddressablesScanResult
            {
                Layout = layout,
                ComparisonService = comparisonService,
                Summary = summary,
                TotalWastedByDuplicates = totalWasted,
                DuplicateAssetCount = duplicates.Count,
                MaxStartupRemoteDepsSize = maxStartupRemote,
                Duplicates = duplicates,
                GateResults = gateResults
            };

            issueSink.ReportProgress(1f, "Analysis complete");

            return result;
        }

        private static List<USAddressablesDuplicateEntry> BuildDuplicates(USAddressablesBuildLayoutProvider layout)
        {
            var duplicates = new List<USAddressablesDuplicateEntry>();
            var seenPaths = new HashSet<string>();

            foreach (var asset in layout.AssetsByPath.Values)
            {
                if (asset.IncludedByBundle.Count < 2)
                    continue;

                var wastedSize = asset.Size * (asset.IncludedByBundle.Count - 1);
                var reason = DetermineReason(asset);
                var fix = BuildSuggestedFix(asset, reason);

                duplicates.Add(new USAddressablesDuplicateEntry
                {
                    AssetPath = asset.Name,
                    Asset = asset,
                    Bundles = asset.IncludedByBundle.ToList(),
                    WastedSize = wastedSize,
                    Reason = reason,
                    SuggestedFix = fix
                });

                seenPaths.Add(asset.Name);
            }

            var byName = new Dictionary<string, List<USAddressablesBuildLayoutProvider.Asset>>();
            foreach (var asset in layout.AssetsByGuid.Values)
            {
                if (!byName.TryGetValue(asset.Name, out var list))
                {
                    list = new List<USAddressablesBuildLayoutProvider.Asset>();
                    byName.Add(asset.Name, list);
                }

                list.Add(asset);
            }

            foreach (var pair in byName)
            {
                if (pair.Value.Count < 2)
                    continue;

                if (seenPaths.Contains(pair.Key))
                    continue;

                var bundles = new List<USAddressablesBuildLayoutProvider.Archive>();
                long wastedSize = 0;

                foreach (var asset in pair.Value)
                {
                    if (asset.IncludedInBundle != null && !bundles.Contains(asset.IncludedInBundle))
                    {
                        bundles.Add(asset.IncludedInBundle);
                        wastedSize += asset.Size;
                    }
                }

                if (bundles.Count < 2)
                    continue;

                var representative = pair.Value.First();
                var fix = BuildGroupFixForGuidDuplicates(pair.Value, bundles);

                duplicates.Add(new USAddressablesDuplicateEntry
                {
                    AssetPath = pair.Key,
                    Asset = representative,
                    Bundles = bundles,
                    WastedSize = wastedSize,
                    Reason = DuplicateReason.DependencyPullIn,
                    SuggestedFix = fix
                });
            }

            return duplicates.OrderByDescending(x => x.WastedSize).ToList();
        }

        private static DuplicateReason DetermineReason(USAddressablesBuildLayoutProvider.Asset asset)
        {
            var explicitCount = 0;
            var pulledInCount = 0;

            foreach (var bundle in asset.IncludedByBundle)
            {
                if (bundle.ExplicitAssets.Contains(asset))
                    explicitCount++;
                else
                    pulledInCount++;
            }

            if (explicitCount > 0 && pulledInCount > 0)
                return DuplicateReason.Mixed;
            if (explicitCount > 1)
                return DuplicateReason.ExplicitInclude;
            return DuplicateReason.DependencyPullIn;
        }

        private static string BuildSuggestedFix(USAddressablesBuildLayoutProvider.Asset asset, DuplicateReason reason)
        {
            switch (reason)
            {
                case DuplicateReason.ExplicitInclude:
                {
                    var groups = asset.IncludedByBundle
                        .SelectMany(b => b.ReferencedByGroups)
                        .Select(g => g.Name)
                        .Distinct()
                        .ToList();
                    return
                        $"Move asset to a shared bundle or assign it to a single group. Currently referenced by groups: {string.Join(", ", groups)}. Consider creating a shared group for common assets.";
                }
                case DuplicateReason.DependencyPullIn:
                {
                    var parentAssets = asset.IncludedByBundle
                        .SelectMany(b => b.ExplicitAssets)
                        .Where(a => a.InternalReferences.Contains(asset) || a.ExternalReferences.Contains(asset))
                        .Select(a => Path.GetFileName(a.Name))
                        .Distinct()
                        .ToList();
                    return
                        $"Asset is pulled into multiple bundles as a dependency of: {string.Join(", ", parentAssets)}. Consider bundling these parent assets together, or move this dependency to a shared bundle.";
                }
                case DuplicateReason.Mixed:
                {
                    var groups = asset.IncludedByBundle
                        .SelectMany(b => b.ReferencedByGroups)
                        .Select(g => g.Name)
                        .Distinct()
                        .ToList();
                    return
                        $"Asset is both explicitly included and pulled as a dependency. Remove explicit duplicate includes and consider a shared bundle. Groups: {string.Join(", ", groups)}.";
                }
                default:
                    return "";
            }
        }

        private static string BuildGroupFixForGuidDuplicates(List<USAddressablesBuildLayoutProvider.Asset> assets,
            List<USAddressablesBuildLayoutProvider.Archive> bundles)
        {
            var groups = bundles
                .SelectMany(b => b.ReferencedByGroups)
                .Select(g => g.Name)
                .Distinct()
                .ToList();
            return
                $"Same-named asset appears in {bundles.Count} bundles across groups: {string.Join(", ", groups)}. Check if these are truly different assets or if the same asset is assigned to multiple Addressable groups. Consolidate into a single group if possible.";
        }

        private static long ComputeMaxStartupRemoteDeps(USAddressablesBuildLayoutProvider layout)
        {
            long maxRemoteSize = 0;
            foreach (var group in layout.Groups)
            {
                foreach (var bundle in group.Archives)
                {
                    if (!USAddressablesBundleUtilities.IsBundleRemote(bundle.Name) ||
                        !USAddressablesBundleUtilities.IsBundleStartup(bundle.Name))
                        continue;

                    long remoteSize = 0;
                    foreach (var dep in bundle.AllBundleDependencies)
                    {
                        if (USAddressablesBundleUtilities.IsBundleRemote(dep.Name) &&
                            !USAddressablesBundleUtilities.IsBundleStartup(dep.Name))
                            remoteSize += dep.Size;
                    }

                    if (remoteSize > maxRemoteSize)
                        maxRemoteSize = remoteSize;
                }
            }

            return maxRemoteSize;
        }

        private static List<GateResult> ComputeGates(USAddressablesBuildLayoutProvider layout, USAddressablesSettings settings,
            long totalWasted, long maxStartupRemote)
        {
            var gates = new List<GateResult>();

            if (settings.GateMaxTotalSizeBytes > 0)
            {
                var pass = layout.TotalSize <= settings.GateMaxTotalSizeBytes;
                gates.Add(new GateResult
                {
                    Name = "Size",
                    ActualValue = layout.TotalSize,
                    Threshold = settings.GateMaxTotalSizeBytes,
                    Pass = pass,
                    FormattedActual = EditorUtility.FormatBytes(layout.TotalSize)
                });
            }

            if (settings.GateMaxDuplicateWastedBytes > 0)
            {
                var pass = totalWasted <= settings.GateMaxDuplicateWastedBytes;
                gates.Add(new GateResult
                {
                    Name = "Duplicates",
                    ActualValue = totalWasted,
                    Threshold = settings.GateMaxDuplicateWastedBytes,
                    Pass = pass,
                    FormattedActual = EditorUtility.FormatBytes(totalWasted)
                });
            }

            if (settings.GateMaxStartupRemoteDepsBytes > 0)
            {
                var pass = maxStartupRemote <= settings.GateMaxStartupRemoteDepsBytes;
                gates.Add(new GateResult
                {
                    Name = "Startup Remote",
                    ActualValue = maxStartupRemote,
                    Threshold = settings.GateMaxStartupRemoteDepsBytes,
                    Pass = pass,
                    FormattedActual = EditorUtility.FormatBytes(maxStartupRemote)
                });
            }

            return gates;
        }
    }
}
