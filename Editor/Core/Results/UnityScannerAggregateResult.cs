using System.Collections.Generic;

namespace UnityScanner.Core.Results
{
    public class UnityScannerAggregateResult
    {
        public List<UnityScannerResult> Results = new List<UnityScannerResult>();
        public double TotalDurationMs;
        public bool Cancelled;
    }
}
