using UnityScanner.Core.Categories;

namespace UnityScanner.Categories.AnimationAnalysis
{
    public class AnimationAnalysisSettings : UnityScannerCategorySettings
    {
        public int StateMachineComplexityThreshold = 50;
        public int AnyStateTransitionThreshold = 5;
        public int CurveKeyframeDensityThreshold = 100;
        public int CurveCountThreshold = 20;
        public int MaxTransitionCount = 30;
        public bool DetectMissingReferences = true;
        public bool DetectUnreachableStates = true;
        public bool DetectComplexityOverThreshold = true;
        public bool DetectAnyStateOveruse = true;
        public bool DetectDuplicateClips = true;
        public bool DetectExpensiveCurves = true;
        public bool DetectParameterMismatches = true;
        public string PathFilter = "";
    }
}
