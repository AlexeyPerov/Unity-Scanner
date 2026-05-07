namespace UnityScanner.Caching
{
    public class USCacheHeader
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string ToolVersion = "";
        public string UnityVersion = "";
        public string ProjectId = "";
        public long CreatedTimestamp;
        public long LastModifiedTimestamp;
    }
}
