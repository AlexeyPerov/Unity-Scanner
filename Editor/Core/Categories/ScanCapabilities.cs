using System;

namespace UnityScanner.Core.Categories
{
    [Flags]
    public enum ScanCapabilities
    {
        None = 0,
        ScanAll = 1,
        ScanSelected = 2,
        ScanFiltered = 4,
        Export = 8,
        Fix = 16,
        Progress = 32
    }
}
