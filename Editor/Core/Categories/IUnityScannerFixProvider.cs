using System.Collections;
using UnityScanner.Core.Issues;

namespace UnityScanner.Core.Categories
{
    public interface IUnityScannerFixProvider
    {
        bool CanFix(UnityScannerIssue issue);
        UnityScannerFixPreview Preview(UnityScannerIssue issue, UnityScannerScanContext context);
        IEnumerator Apply(UnityScannerIssue issue, UnityScannerScanContext context);
    }
}
