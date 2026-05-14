using System;
using System.Collections;
using UnityEditor;

namespace UnityScanner.Core.Categories
{
    public static class USCoroutineHelper
    {
        public static int ComputeYieldInterval(int totalItems, int threshold, int divisor)
        {
            if (totalItems < threshold || threshold <= 0) return 0;
            if (divisor <= 0) return 0;
            return Math.Max(1, totalItems / divisor);
        }

        public static IEnumerator YieldIfNecessary(int index, int yieldInterval)
        {
            if (yieldInterval <= 0) yield break;
            if (index > 0 && index % yieldInterval == 0)
            {
                GC.Collect();
                yield return 0.05f;
                GC.Collect();
            }
        }
    }
}