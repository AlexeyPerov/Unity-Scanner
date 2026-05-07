namespace UnityScanner.Caching
{
    public class USCacheValidationContext
    {
        public string UnityVersion;
        public string ProjectId;
        public string BuildLayoutPath;
        public long BuildLayoutTimestamp;
        public bool ForceRebuild;
    }
}
