namespace UnityScanner.Core.Categories
{
    public interface IUnityScannerCacheContributor
    {
        string CategoryId { get; }
        void InvalidateCache(string cachePath);
        bool ValidateCache(string cachePath);
    }
}
