using System;
using System.Collections.Generic;
using UnityScanner.Core.Progress;

namespace UnityScanner.Core.Issues
{
    public class UnityScannerIssueSink : IUnityScannerIssueSink
    {
        private readonly List<UnityScannerIssue> _issues = new List<UnityScannerIssue>();

        public IReadOnlyList<UnityScannerIssue> Issues => _issues;
        public bool WasSkipped { get; private set; }
        public string SkipReason { get; private set; }
        public event Action<UnityScannerProgressInfo> OnProgressUpdated;

        public void Add(UnityScannerIssue issue)
        {
            _issues.Add(issue);
        }

        public void AddRange(IEnumerable<UnityScannerIssue> issues)
        {
            _issues.AddRange(issues);
        }

        public void ReportProgress(float progress, string message)
        {
            OnProgressUpdated?.Invoke(new UnityScannerProgressInfo
            {
                Progress = progress,
                Message = message
            });
        }

        public void MarkSkipped(string reason = null)
        {
            WasSkipped = true;
            SkipReason = reason;
        }
    }
}
