using System.Collections.Generic;
using UnityEngine;

namespace UnityScanner.Core.Issues
{
    public class UnityScannerIssue
    {
        public string Id;
        public string CategoryId;
        public string IssueCode;
        public string Description;
        public UnityScannerIssueSeverity Severity = UnityScannerIssueSeverity.Info;
        public string AssetPath;
        public string Guid;
        public Object TargetObject;
        public string FixId;
        public Dictionary<string, object> Metadata = new Dictionary<string, object>();
    }
}
