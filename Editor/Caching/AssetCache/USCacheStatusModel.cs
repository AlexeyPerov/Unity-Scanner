using System;
using System.Collections.Generic;

namespace UnityScanner.Caching
{
    public class USCacheStatusModel
    {
        public USCacheStatus CurrentStatus = USCacheStatus.Disabled;
        public USCacheStatus LastOperationStatus = USCacheStatus.Disabled;
        public string LastError;
        public readonly List<USCacheFallbackRecord> FallbackHistory = new List<USCacheFallbackRecord>();
        public bool IsFallbackMode => CurrentStatus != USCacheStatus.LoadSucceeded;

        public void RecordStatus(USCacheStatus status, string error = null)
        {
            LastOperationStatus = status;
            CurrentStatus = status;
            LastError = error;

            if (status == USCacheStatus.LoadFailed
                || status == USCacheStatus.ValidationFailed
                || status == USCacheStatus.WriteFailed)
            {
                FallbackHistory.Add(new USCacheFallbackRecord
                {
                    Status = status,
                    Error = error,
                    Timestamp = DateTime.Now
                });
            }
        }

        public void Reset()
        {
            CurrentStatus = USCacheStatus.Disabled;
            LastOperationStatus = USCacheStatus.Disabled;
            LastError = null;
        }
    }
}
