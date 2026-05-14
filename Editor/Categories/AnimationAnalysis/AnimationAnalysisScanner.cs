using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityScanner.Core.Issues;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Categories.AnimationAnalysis
{
    public static class AnimationAnalysisScanner
    {
        public static IEnumerator ScanAll(
            AnimationAnalysisSettings settings,
            List<AnimatorData> animators,
            List<AnimationClipData> clips,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            yield return ScanAnimators(settings, animators, issueSink, yieldInterval);
            yield return ScanClips(settings, clips, issueSink, yieldInterval);

            if (settings.DetectDuplicateClips)
                DetectDuplicateClips(clips);

            GC.Collect();
        }

        private static IEnumerator ScanAnimators(
            AnimationAnalysisSettings settings,
            List<AnimatorData> animators,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            var animatorGuids = AssetDatabase.FindAssets("t:RuntimeAnimatorController");
            var total = animatorGuids.Length;

            for (var i = 0; i < animatorGuids.Length; i++)
            {
                if (i % 30 == 0)
                    issueSink.ReportProgress((float)i / total * 0.6f, "Scanning animator controllers...");

                if (yieldInterval > 0 && i > 0 && i % yieldInterval == 0)
                {
                    System.GC.Collect();
                    yield return 0.05f;
                    System.GC.Collect();
                }

                var controllerPath = AssetDatabase.GUIDToAssetPath(animatorGuids[i]);
                if (!string.IsNullOrEmpty(settings.PathFilter) && !controllerPath.Contains(settings.PathFilter))
                    continue;

                var controller = AssetDatabase.LoadMainAssetAtPath(controllerPath) as RuntimeAnimatorController;
                if (controller == null) continue;

                var data = AnalyzeController(controller, controllerPath, settings);
                animators.Add(data);
            }
        }

        private static AnimatorData AnalyzeController(
            RuntimeAnimatorController controller, string path, AnimationAnalysisSettings settings)
        {
            var data = new AnimatorData
            {
                Path = path,
                Name = Path.GetFileName(path),
                Controller = controller
            };

            var ac = controller as UnityEditor.Animations.AnimatorController;
            if (ac == null)
            {
                data.StateCount = 0;
                data.TransitionCount = 0;
                return data;
            }

            data.LayerCount = ac.layers.Length;

            var allStates = new List<UnityEditor.Animations.AnimatorState>();
            var allTransitions = new List<(string src, string dst)>();
            var anyStateTransitions = new List<UnityEditor.Animations.AnimatorStateTransition>();
            var entryStates = new HashSet<string>();

            foreach (var layer in ac.layers)
            {
                var sm = layer.stateMachine;
                if (sm == null) continue;

                CollectStatesAndTransitions(sm, allStates, allTransitions, anyStateTransitions, entryStates);
            }

            data.StateCount = allStates.Count;
            data.TransitionCount = allTransitions.Count + anyStateTransitions.Count;
            data.AnyStateTransitionCount = anyStateTransitions.Count;

            if (settings.DetectMissingReferences)
            {
                foreach (var state in allStates)
                {
                    if (state.motion == null && !state.name.EndsWith("(Placeholder)"))
                    {
                        if (state.name != "Empty" && state.name != "empty")
                            data.MissingReferences.Add("State '" + state.name + "' has no motion/clip assigned.");
                    }
                }

                foreach (var layer in ac.layers)
                {
                    if (layer.avatarMask != null) continue;
                }
            }

            if (settings.DetectUnreachableStates)
            {
                var reachable = BuildReachabilityMap(allStates, allTransitions, entryStates);
                foreach (var state in allStates)
                {
                    if (!reachable.Contains(state.name) && !IsSpecialState(state.name))
                        data.UnreachableStates.Add(state.name);
                }
            }

            if (data.MissingReferences.Count > 0)
                data.TrySetWarningLevel(3);
            if (data.UnreachableStates.Count > 0)
                data.TrySetWarningLevel(2);
            if (data.TransitionCount > settings.MaxTransitionCount || data.AnyStateTransitionCount > settings.AnyStateTransitionThreshold)
                data.TrySetWarningLevel(2);
            if (data.StateCount > settings.StateMachineComplexityThreshold)
                data.TrySetWarningLevel(1);

            if (settings.DetectParameterMismatches)
            {
                DetectParameterMismatches(ac, data);
            }

            return data;
        }

        private static void CollectStatesAndTransitions(
            UnityEditor.Animations.AnimatorStateMachine sm,
            List<UnityEditor.Animations.AnimatorState> allStates,
            List<(string src, string dst)> allTransitions,
            List<UnityEditor.Animations.AnimatorStateTransition> anyStateTransitions,
            HashSet<string> entryStates)
        {
            if (sm == null) return;

            foreach (var state in sm.states)
            {
                if (state.state != null)
                    allStates.Add(state.state);
            }

            if (sm.entryTransitions != null)
            {
                foreach (var et in sm.entryTransitions)
                {
                    if (et.destinationState != null)
                        entryStates.Add(et.destinationState.name);
                }
            }

            if (sm.defaultState != null)
                entryStates.Add(sm.defaultState.name);

            if (sm.anyStateTransitions != null)
            {
                foreach (var t in sm.anyStateTransitions)
                {
                    anyStateTransitions.Add(t);
                    if (t.destinationState != null)
                        allTransitions.Add(("*", t.destinationState.name));
                }
            }

            foreach (var state in sm.states)
            {
                if (state.state == null) continue;
                if (state.state.transitions != null)
                {
                    foreach (var t in state.state.transitions)
                    {
                        if (t.destinationState != null)
                            allTransitions.Add((state.state.name, t.destinationState.name));
                    }
                }
            }

            if (sm.stateMachines != null)
            {
                foreach (var subSm in sm.stateMachines)
                {
                    CollectStatesAndTransitions(subSm.stateMachine, allStates, allTransitions, anyStateTransitions, entryStates);
                }
            }
        }

        private static HashSet<string> BuildReachabilityMap(
            List<UnityEditor.Animations.AnimatorState> allStates,
            List<(string src, string dst)> allTransitions,
            HashSet<string> entryStates)
        {
            var adjacency = new Dictionary<string, HashSet<string>>();
            foreach (var state in allStates)
            {
                if (!adjacency.ContainsKey(state.name))
                    adjacency[state.name] = new HashSet<string>();
            }

            foreach (var t in allTransitions)
            {
                if (t.src == "*")
                {
                    foreach (var state in allStates)
                    {
                        if (adjacency.ContainsKey(state.name))
                            adjacency[state.name].Add(t.dst);
                    }
                }
                else
                {
                    if (adjacency.ContainsKey(t.src))
                        adjacency[t.src].Add(t.dst);
                }
            }

            var reachable = new HashSet<string>();
            var queue = new Queue<string>(entryStates);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (reachable.Contains(current)) continue;
                reachable.Add(current);

                if (adjacency.ContainsKey(current))
                {
                    foreach (var next in adjacency[current])
                    {
                        if (!reachable.Contains(next))
                            queue.Enqueue(next);
                    }
                }
            }

            return reachable;
        }

        private static bool IsSpecialState(string name)
        {
            return name == "Entry" || name == "Exit" || name == "Any State";
        }

        private static void DetectParameterMismatches(
            UnityEditor.Animations.AnimatorController ac, AnimatorData data)
        {
            var controllerParams = new HashSet<string>();
            foreach (var p in ac.parameters)
                controllerParams.Add(p.name);

            if (controllerParams.Count == 0) return;

            var assetPath = AssetDatabase.GetAssetPath(ac);
            if (string.IsNullOrEmpty(assetPath)) return;

            var scripts = AssetDatabase.FindAssets("t:MonoScript", new[] { Path.GetDirectoryName(assetPath) });
            if (scripts.Length == 0) return;

            var animatorSetPattern = new Regex(
                @"(?:animator|_animator|Animator)\s*\.\s*(?:Set(?:Float|Bool|Integer|Int|Trigger)|Get(?:Float|Bool|Integer|Int))\s*\(\s*""(\w+)""",
                RegexOptions.Compiled);

            foreach (var guid in scripts.Take(50))
            {
                var scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!scriptPath.EndsWith(".cs")) continue;

                try
                {
                    var content = File.ReadAllText(scriptPath);
                    var matches = animatorSetPattern.Matches(content);
                    foreach (Match match in matches)
                    {
                        if (match.Success && match.Groups.Count > 1)
                        {
                            var paramName = match.Groups[1].Value;
                            if (!controllerParams.Contains(paramName))
                            {
                                data.ParameterMismatches.Add(
                                    "Script '" + Path.GetFileName(scriptPath) + "' references parameter '" + paramName + "' not found in controller.");
                            }
                        }
                    }
                }
                catch { }
            }

            if (data.ParameterMismatches.Count > 0)
                data.TrySetWarningLevel(1);
        }

        private static IEnumerator ScanClips(
            AnimationAnalysisSettings settings,
            List<AnimationClipData> clips,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            var clipGuids = AssetDatabase.FindAssets("t:AnimationClip");
            var total = clipGuids.Length;

            for (var i = 0; i < clipGuids.Length; i++)
            {
                if (i % 100 == 0)
                    issueSink.ReportProgress(0.6f + (float)i / total * 0.3f, "Scanning animation clips...");

                if (yieldInterval > 0 && i > 0 && i % yieldInterval == 0)
                {
                    System.GC.Collect();
                    yield return 0.05f;
                    System.GC.Collect();
                }

                var clipPath = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
                if (!string.IsNullOrEmpty(settings.PathFilter) && !clipPath.Contains(settings.PathFilter))
                    continue;

                var clip = AssetDatabase.LoadMainAssetAtPath(clipPath) as AnimationClip;
                if (clip == null) continue;

                var data = CreateClipData(clip, clipPath, settings);
                clips.Add(data);
            }
        }

        private static AnimationClipData CreateClipData(
            AnimationClip clip, string path, AnimationAnalysisSettings settings)
        {
            long fileBytes = 0;
            try { if (File.Exists(path)) fileBytes = new FileInfo(path).Length; } catch { }

            var bindings = AnimationUtility.GetCurveBindings(clip);
            var curveCount = bindings.Length;
            var totalKeyframes = 0;

            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null)
                    totalKeyframes += curve.keys.Length;
            }

            var density = clip.length > 0.01f ? (int)(totalKeyframes / clip.length) : totalKeyframes;

            var data = new AnimationClipData
            {
                Path = path,
                Name = Path.GetFileName(path),
                Clip = clip,
                CurveCount = curveCount,
                TotalKeyframes = totalKeyframes,
                Duration = clip.length,
                KeyframeDensity = density,
                FileSizeBytes = fileBytes
            };

            if (settings.DetectExpensiveCurves)
            {
                if (density > settings.CurveKeyframeDensityThreshold)
                    data.TrySetWarningLevel(1);
                if (curveCount > settings.CurveCountThreshold)
                    data.TrySetWarningLevel(1);
            }

            return data;
        }

        private static void DetectDuplicateClips(List<AnimationClipData> clips)
        {
            var bySize = new Dictionary<long, List<AnimationClipData>>();
            foreach (var clip in clips)
            {
                if (clip.FileSizeBytes <= 0) continue;
                if (!bySize.TryGetValue(clip.FileSizeBytes, out var list))
                {
                    list = new List<AnimationClipData>();
                    bySize[clip.FileSizeBytes] = list;
                }
                list.Add(clip);
            }

            foreach (var group in bySize.Values)
            {
                if (group.Count < 2) continue;
                var key = group[0].FileSizeBytes.ToString();
                foreach (var c in group)
                {
                    c.IsDuplicate = true;
                    c.DuplicateGroupKey = key;
                    c.DuplicatePaths = group.Where(g => g != c).Select(g => g.Path).ToList();
                    c.TrySetWarningLevel(1);
                }
            }
        }
    }
}
