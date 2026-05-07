using System.Collections.Generic;

namespace UnityScanner.Categories.Addressables
{
    public class AddressablesScanResult
    {
        public USAddressablesBuildLayoutProvider Layout { get; set; }
        public USAddressablesBundleComparisonService ComparisonService { get; set; }
        public USAddressablesRecommendationsSummary Summary { get; set; }

        public long TotalWastedByDuplicates { get; set; }
        public int DuplicateAssetCount { get; set; }
        public long MaxStartupRemoteDepsSize { get; set; }

        public List<USAddressablesDuplicateEntry> Duplicates { get; set; } = new();
        public List<GateResult> GateResults { get; set; } = new();
    }

    public class USAddressablesDuplicateEntry
    {
        public string AssetPath { get; set; }
        public USAddressablesBuildLayoutProvider.Asset Asset { get; set; }
        public List<USAddressablesBuildLayoutProvider.Archive> Bundles { get; set; } = new();
        public long WastedSize { get; set; }
        public DuplicateReason Reason { get; set; }
        public string SuggestedFix { get; set; }
    }

    public enum DuplicateReason
    {
        ExplicitInclude,
        DependencyPullIn,
        Mixed
    }

    public class GateResult
    {
        public string Name { get; set; }
        public long ActualValue { get; set; }
        public long Threshold { get; set; }
        public bool Pass { get; set; }
        public string FormattedActual { get; set; }
    }
}
