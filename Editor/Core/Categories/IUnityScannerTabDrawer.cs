using UnityScanner.Core.Issues;
using UnityScanner.Core.Results;

namespace UnityScanner.Core.Categories
{
    public interface IUnityScannerTabDrawer
    {
        string CategoryId { get; }
        void DrawHeader(UnityScannerResult result);
        void DrawIssues(UnityScannerResult result);
        void DrawTopBar(UnityScannerResult result);
    }
}
