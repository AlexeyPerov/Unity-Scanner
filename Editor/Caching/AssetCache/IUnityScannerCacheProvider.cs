using System;

namespace UnityScanner.Caching
{
    public interface IUnityScannerCacheProvider
    {
        USCacheStatus Status { get; }
        USAssetCacheData Data { get; }

        USCacheStatus Load(string cachePath);
        USCacheStatus Validate(USCacheValidationContext context);
        USCacheStatus Save(string cachePath);
        void Invalidate();
        void Clear();
    }
}
