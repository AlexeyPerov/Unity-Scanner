using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.AnimationAnalysis
{
    public static class AnimationAnalysisIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(
            List<AnimatorData> animators,
            List<AnimationClipData> clips,
            AnimationAnalysisSettings settings)
        {
            var issues = new List<UnityScannerIssue>();

            foreach (var animator in animators)
            {
                if (settings.DetectMissingReferences && animator.HasMissingController)
                {
                    issues.Add(MakeIssue("missing_controller",
                        "Animator '" + animator.Name + "' references missing controller.",
                        UnityScannerIssueSeverity.Error, animator.Path));
                }

                if (settings.DetectMissingReferences && animator.HasMissingAvatar)
                {
                    issues.Add(MakeIssue("missing_avatar",
                        "Animator '" + animator.Name + "' references missing avatar.",
                        UnityScannerIssueSeverity.Error, animator.Path));
                }

                if (settings.DetectMissingReferences)
                {
                    foreach (var missing in animator.MissingReferences)
                    {
                        issues.Add(MakeIssue("missing_clip",
                            "Animator '" + animator.Name + "': " + missing,
                            UnityScannerIssueSeverity.Error, animator.Path));
                    }
                }

                if (settings.DetectComplexityOverThreshold && animator.StateCount > settings.StateMachineComplexityThreshold)
                {
                    issues.Add(MakeIssue("complexity_over_threshold",
                        "Animator '" + animator.Name + "' has " + animator.StateCount + " states (threshold: " + settings.StateMachineComplexityThreshold + "). Transitions: " + animator.TransitionCount + ".",
                        UnityScannerIssueSeverity.Warning, animator.Path));
                }

                if (settings.DetectAnyStateOveruse && animator.AnyStateTransitionCount > settings.AnyStateTransitionThreshold)
                {
                    issues.Add(MakeIssue("anystate_overuse",
                        "Animator '" + animator.Name + "' has " + animator.AnyStateTransitionCount + " AnyState transitions (threshold: " + settings.AnyStateTransitionThreshold + ").",
                        UnityScannerIssueSeverity.Warning, animator.Path));
                }

                if (settings.DetectUnreachableStates)
                {
                    foreach (var state in animator.UnreachableStates)
                    {
                        issues.Add(MakeIssue("unreachable_state",
                            "Animator '" + animator.Name + "' has unreachable state: '" + state + "'.",
                            UnityScannerIssueSeverity.Warning, animator.Path));
                    }
                }

                if (settings.DetectParameterMismatches)
                {
                    foreach (var mismatch in animator.ParameterMismatches)
                    {
                        issues.Add(MakeIssue("parameter_mismatch",
                            "Animator '" + animator.Name + "': " + mismatch,
                            UnityScannerIssueSeverity.Info, animator.Path));
                    }
                }
            }

            foreach (var clip in clips)
            {
                if (settings.DetectExpensiveCurves)
                {
                    if (clip.KeyframeDensity > settings.CurveKeyframeDensityThreshold)
                    {
                        issues.Add(MakeIssue("expensive_curves_density",
                            "Clip '" + clip.Name + "' has high keyframe density (" + clip.KeyframeDensity + " kf/s, threshold: " + settings.CurveKeyframeDensityThreshold + ").",
                            UnityScannerIssueSeverity.Info, clip.Path));
                    }

                    if (clip.CurveCount > settings.CurveCountThreshold)
                    {
                        issues.Add(MakeIssue("expensive_curves_count",
                            "Clip '" + clip.Name + "' has " + clip.CurveCount + " curves (threshold: " + settings.CurveCountThreshold + ").",
                            UnityScannerIssueSeverity.Info, clip.Path));
                    }
                }

                if (settings.DetectDuplicateClips && clip.IsDuplicate && clip.DuplicatePaths.Count > 0)
                {
                    issues.Add(MakeIssue("duplicate_clip",
                        "Duplicate animation clip '" + clip.Name + "' matches " + clip.DuplicatePaths.Count + " other clip(s).",
                        UnityScannerIssueSeverity.Info, clip.Path));
                }
            }

            return issues;
        }

        private static UnityScannerIssue MakeIssue(
            string code, string description, UnityScannerIssueSeverity severity, string assetPath)
        {
            return new UnityScannerIssue
            {
                CategoryId = "animation_analysis",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };
        }
    }
}
