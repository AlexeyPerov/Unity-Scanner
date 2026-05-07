using System.Collections.Generic;

namespace UnityScanner.Caching
{
    public class USAssetCacheEntry
    {
        public string Guid;
        public string Path;
        public string TypeName;
        public long FileSize;
        public long ImportMarker;
        public bool IsAddressable;
        public string BundleName;
        public HashSet<string> DirectDependencies = new HashSet<string>();
        public HashSet<string> ReverseDependencies = new HashSet<string>();
    }
}
