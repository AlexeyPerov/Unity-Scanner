using System.Collections.Generic;

namespace UnityScanner.Core.Issues
{
    public interface IUnityScannerIssueSink
    {
        void Add(UnityScannerIssue issue);
        void AddRange(IEnumerable<UnityScannerIssue> issues);
        void ReportProgress(float progress, string message);
        void MarkSkipped(string reason = null);
    }
}
