using System.Collections;
using UnityScanner.Core.Issues;

namespace UnityScanner.Core.Categories
{
    public interface IUnityScannerCategory
    {
        string Id { get; }
        string DisplayName { get; }
        string ShortDisplayName { get; }
        UnityScannerCategorySettings Settings { get; }
        ScanCapabilities Capabilities { get; }
        IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink);
    }
}
