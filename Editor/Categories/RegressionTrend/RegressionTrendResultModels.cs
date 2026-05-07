using System;
using System.Collections.Generic;

namespace UnityScanner.Categories.RegressionTrend
{
    [Serializable]
    public class BaselineSnapshot
    {
        public string Timestamp;
        public string UnityVersion;
        public string PlatformProfile;
        public List<CategoryBaseline> Categories = new List<CategoryBaseline>();
    }

    [Serializable]
    public class CategoryBaseline
    {
        public string CategoryId;
        public string DisplayName;
        public int TotalIssues;
        public int Errors;
        public int Warnings;
        public int Infos;
        public int Verboses;
        public double ScanDurationMs;
    }

    public class CategoryComparison
    {
        public string CategoryId;
        public string DisplayName;
        public int BaselineErrors;
        public int BaselineWarnings;
        public int BaselineInfos;
        public int BaselineVerboses;
        public int CurrentErrors;
        public int CurrentWarnings;
        public int CurrentInfos;
        public int CurrentVerboses;
        public int ErrorDelta => CurrentErrors - BaselineErrors;
        public int WarningDelta => CurrentWarnings - BaselineWarnings;
        public int InfoDelta => CurrentInfos - BaselineInfos;
        public int TotalDelta => ErrorDelta + WarningDelta + InfoDelta;
        public bool HasRegression => ErrorDelta > 0 || WarningDelta > 0;
        public bool HasImprovement => ErrorDelta < 0 || WarningDelta < 0;
        public bool IsNew => BaselineErrors == 0 && BaselineWarnings == 0 && BaselineInfos == 0 && (CurrentErrors > 0 || CurrentWarnings > 0 || CurrentInfos > 0);
    }
}
