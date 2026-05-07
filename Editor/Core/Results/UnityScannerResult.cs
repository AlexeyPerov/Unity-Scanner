using System.Collections.Generic;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;

namespace UnityScanner.Core.Results
{
    public class UnityScannerResult
    {
        public string CategoryId;
        public string DisplayName;
        public string ShortDisplayName;
        public List<UnityScannerIssue> Issues = new List<UnityScannerIssue>();
        public ScanCapabilities Capabilities;
        public double ScanDurationMs;
        public bool Succeeded = true;
        public string ErrorMessage;
        public bool Skipped;
        public string SkipReason;
    }
}
