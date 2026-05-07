using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.RegressionTrend
{
    public class RegressionTrendSettings : UnityScannerCategorySettings
    {
        public int RegressionWarningThreshold = 1;
        public int RegressionErrorThreshold = 1;
        public string BaselinePath = "";
    }
}
