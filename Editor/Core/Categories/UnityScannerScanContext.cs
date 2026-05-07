using UnityScanner.Caching;
using UnityScanner.Core.Results;
using UnityScanner.Core.Settings;

namespace UnityScanner.Core.Categories
{
    public class UnityScannerScanContext
    {
        public UnityScannerSettings Settings;
        public USCacheService CacheService;
        public string[] SelectedAssetPaths;
        public string[] FilterPaths;
        public UnityScannerAggregateResult PreviousResults;
    }
}
