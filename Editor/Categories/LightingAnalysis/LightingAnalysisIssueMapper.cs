using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.LightingAnalysis
{
    public static class LightingAnalysisIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(
            List<SceneLightingData> results,
            LightingAnalysisSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();
            if (profile == null) return issues;

            foreach (var data in results)
            {
                if (settings.CheckRealtimeExceeded && data.RealtimeLightCount > profile.MaxRealtimeLightsPerScene)
                {
                    issues.Add(MakeIssue("lighting_realtime_exceeded",
                        "Scene has " + data.RealtimeLightCount + " realtime lights (max: " + profile.MaxRealtimeLightsPerScene + ").",
                        UnityScannerIssueSeverity.Warning, data.ScenePath,
                        "RealtimeLightCount", data.RealtimeLightCount,
                        "ThresholdCount", profile.MaxRealtimeLightsPerScene));
                }

                if (settings.CheckShadowsOnMobile && profile.Id == PlatformProfilePresets.Mobile)
                {
                    var shadowLights = data.Lights.Where(l =>
                        l.ShadowsEnabled && l.LightMode == "Realtime").ToList();
                    foreach (var sl in shadowLights)
                    {
                        issues.Add(MakeIssue("lighting_shadows_on_mobile",
                            "Realtime light '" + sl.ObjectPath + "' has shadows enabled on mobile profile.",
                            UnityScannerIssueSeverity.Warning, data.ScenePath,
                            "LightPath", sl.ObjectPath));
                    }
                }

                if (settings.CheckLightmapOversized && data.LightmapSize > profile.MaxLightmapSize)
                {
                    issues.Add(MakeIssue("lighting_lightmap_oversized",
                        "Lightmap size " + data.LightmapSize + " exceeds threshold " + profile.MaxLightmapSize + ".",
                        UnityScannerIssueSeverity.Warning, data.ScenePath,
                        "LightmapSize", data.LightmapSize,
                        "ThresholdSize", profile.MaxLightmapSize));
                }

                if (settings.CheckModeInconsistent &&
                    (data.MixedLightCount > 0 && (data.RealtimeLightCount > 0 || data.BakedLightCount > 0)) ||
                    (data.RealtimeLightCount > 0 && data.BakedLightCount > 0 && data.MixedLightCount == 0))
                {
                    issues.Add(MakeIssue("lighting_mode_inconsistent",
                        "Inconsistent lighting modes: " + data.MixedLightCount + " Mixed, " + data.RealtimeLightCount + " Realtime, " + data.BakedLightCount + " Baked.",
                        UnityScannerIssueSeverity.Info, data.ScenePath,
                        "MixedCount", data.MixedLightCount,
                        "RealtimeCount", data.RealtimeLightCount,
                        "BakedCount", data.BakedLightCount));
                }

                if (settings.CheckBakedSetToRealtime)
                {
                    var staticRealtimeLights = data.Lights.Where(l =>
                        l.LightMode == "Realtime").ToList();
                    foreach (var light in staticRealtimeLights)
                    {
                        if (data.RealtimeLightCount <= profile.MaxRealtimeLightsPerScene)
                            continue;
                        issues.Add(MakeIssue("lighting_baked_set_to_realtime",
                            "Light '" + light.ObjectPath + "' is set to Realtime but may be intended for baking.",
                            UnityScannerIssueSeverity.Warning, data.ScenePath,
                            "LightPath", light.ObjectPath,
                            "CurrentMode", light.LightMode));
                    }
                }

                if (settings.CheckProbeMissing &&
                    (data.RealtimeLightCount > 0 || data.MixedLightCount > 0) &&
                    !data.HasLightProbes)
                {
                    issues.Add(MakeIssue("lighting_probe_missing",
                        "Scene has " + (data.RealtimeLightCount + data.MixedLightCount) + " realtime/mixed lights but no light probes.",
                        UnityScannerIssueSeverity.Warning, data.ScenePath));
                }

                if (settings.CheckReflectionProbeExceeded)
                {
                    if (data.ReflectionProbeCount > profile.MaxReflectionProbeCount)
                    {
                        issues.Add(MakeIssue("lighting_reflection_probe_exceeded",
                            "Reflection probe count " + data.ReflectionProbeCount + " exceeds threshold " + profile.MaxReflectionProbeCount + ".",
                            UnityScannerIssueSeverity.Warning, data.ScenePath,
                            "ProbeCount", data.ReflectionProbeCount,
                            "MaxResolution", data.MaxReflectionProbeResolution,
                            "ThresholdCount", profile.MaxReflectionProbeCount,
                            "ThresholdResolution", profile.MaxReflectionProbeSize));
                    }
                    if (data.MaxReflectionProbeResolution > profile.MaxReflectionProbeSize)
                    {
                        issues.Add(MakeIssue("lighting_reflection_probe_exceeded",
                            "Reflection probe resolution " + data.MaxReflectionProbeResolution + " exceeds threshold " + profile.MaxReflectionProbeSize + ".",
                            UnityScannerIssueSeverity.Warning, data.ScenePath,
                            "ProbeCount", data.ReflectionProbeCount,
                            "MaxResolution", data.MaxReflectionProbeResolution,
                            "ThresholdCount", profile.MaxReflectionProbeCount,
                            "ThresholdResolution", profile.MaxReflectionProbeSize));
                    }
                }

                if (settings.CheckEmissiveNoGI)
                {
                    foreach (var em in data.EmissiveMaterials)
                    {
                        issues.Add(MakeIssue("lighting_emissive_no_gi",
                            "Emissive material '" + em.MaterialName + "' does not contribute to GI. Flags: " + em.GlobalIlluminationFlags,
                            UnityScannerIssueSeverity.Info, data.ScenePath,
                            "MaterialPath", em.MaterialPath,
                            "CurrentFlags", em.GlobalIlluminationFlags));
                    }
                }

                if (settings.CheckPipelineMismatch && data.ActivePipeline != "Built-in")
                {
                    var bakedLights = data.Lights.Where(l => l.LightMode == "Baked").ToList();
                    if (bakedLights.Count > 0 && data.LightmapCount == 0)
                    {
                        issues.Add(MakeIssue("lighting_pipeline_mismatch",
                            "Scene has " + bakedLights.Count + " baked lights but no lightmaps with " + data.ActivePipeline + " pipeline.",
                            UnityScannerIssueSeverity.Warning, data.ScenePath,
                            "ActivePipeline", data.ActivePipeline,
                            "IssueDescription", "Baked lights without lightmaps in scriptable render pipeline"));
                    }
                }
            }

            return issues;
        }

        private static UnityScannerIssue MakeIssue(
            string code, string description, UnityScannerIssueSeverity severity,
            string assetPath, params object[] metadataPairs)
        {
            var issue = new UnityScannerIssue
            {
                CategoryId = "lighting_analysis",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };

            for (var i = 0; i + 1 < metadataPairs.Length; i += 2)
            {
                if (metadataPairs[i] is string key)
                    issue.Metadata[key] = metadataPairs[i + 1];
            }

            return issue;
        }
    }
}
