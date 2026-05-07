namespace UnityScanner.Caching
{
    public enum USCacheStatus
    {
        Disabled,
        NotLoaded,
        LoadSucceeded,
        LoadFailed,
        ValidationFailed,
        WriteSucceeded,
        WriteFailed,
        Invalidated
    }
}
