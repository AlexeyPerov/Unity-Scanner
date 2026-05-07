using System.Collections.Generic;
using UnityScanner.UI.Controls;
using UnityEngine;

namespace UnityScanner.Categories.AnimationAnalysis
{
    public class AnimatorData : USItemDataBase
    {
        public string Path;
        public string Name;
        public RuntimeAnimatorController Controller;
        public int StateCount;
        public int TransitionCount;
        public int AnyStateTransitionCount;
        public int LayerCount;
        public List<string> MissingReferences = new List<string>();
        public List<string> UnreachableStates = new List<string>();
        public List<string> ParameterMismatches = new List<string>();
        public bool HasMissingController;
        public bool HasMissingAvatar;
        public bool Foldout;
    }

    public class AnimationClipData : USItemDataBase
    {
        public string Path;
        public string Name;
        public AnimationClip Clip;
        public int CurveCount;
        public int TotalKeyframes;
        public float Duration;
        public int KeyframeDensity;
        public long FileSizeBytes;
        public bool IsDuplicate;
        public string DuplicateGroupKey;
        public List<string> DuplicatePaths = new List<string>();
        public bool Foldout;
    }
}
