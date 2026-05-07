namespace UnityScanner.Caching
{
    public class USNullCacheProvider : IUnityScannerCacheProvider
    {
        public USCacheStatus Status => USCacheStatus.Disabled;
        public USAssetCacheData Data => null;

        public USCacheStatus Load(string cachePath)
        {
            return USCacheStatus.Disabled;
        }

        public USCacheStatus Validate(USCacheValidationContext context)
        {
            return USCacheStatus.Disabled;
        }

        public USCacheStatus Save(string cachePath)
        {
            return USCacheStatus.Disabled;
        }

        public void Invalidate()
        {
        }

        public void Clear()
        {
        }
    }
}
