using System.Collections.Generic;

namespace UnityScanner.Caching
{
    public class USAssetCacheData
    {
        public USCacheHeader Header = new USCacheHeader();
        public readonly Dictionary<string, USAssetCacheEntry> EntriesByGuid = new Dictionary<string, USAssetCacheEntry>();
        public readonly Dictionary<string, string> PathToGuid = new Dictionary<string, string>();
        public string BuildLayoutPath;
        public long BuildLayoutTimestamp;
    }
}
