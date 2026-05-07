using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.ParticleSystemAnalysis
{
    public static class ParticleSystemAnalysisIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(
            List<ParticleSystemData> results,
            ParticleSystemAnalysisSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();
            if (profile == null) return issues;

            foreach (var data in results)
            {
                if (settings.CheckEmission && data.EmissionRate > profile.MaxParticleEmissionRate)
                {
                    issues.Add(MakeIssue("particle_emission_excessive",
                        "Particle system emission rate " + data.EmissionRate + " exceeds threshold " + profile.MaxParticleEmissionRate +
                        (data.IsBurst ? " (burst)" : "") + ".",
                        UnityScannerIssueSeverity.Warning, data.AssetPath,
                        "EmissionRate", data.EmissionRate,
                        "ThresholdRate", profile.MaxParticleEmissionRate,
                        "IsBurst", data.IsBurst));
                }

                if (settings.CheckCollision && data.CollisionEnabled && !data.CollisionSendMessages)
                {
                    issues.Add(MakeIssue("particle_collision_unnecessary",
                        "Collision module enabled but does not send collision messages. Consider disabling for CPU savings.",
                        UnityScannerIssueSeverity.Info, data.AssetPath,
                        "CollisionEnabled", data.CollisionEnabled,
                        "SendMessages", data.CollisionSendMessages));
                }

                if (settings.CheckOverdraw && data.TrailEnabled && data.TrailLifetime > profile.MaxParticleTrailLifetime)
                {
                    issues.Add(MakeIssue("particle_trail_overdraw",
                        "Trail lifetime " + data.TrailLifetime.ToString("F1") + "s exceeds threshold " + profile.MaxParticleTrailLifetime.ToString("F1") + "s.",
                        UnityScannerIssueSeverity.Warning, data.AssetPath,
                        "TrailLifetime", data.TrailLifetime,
                        "ThresholdLifetime", profile.MaxParticleTrailLifetime));
                }

                if (settings.CheckSubEmitters && data.SubEmitterChainDepth > 2)
                {
                    issues.Add(MakeIssue("particle_subemitter_chain",
                        "Sub-emitter chain depth " + data.SubEmitterChainDepth + " exceeds 2 levels.",
                        UnityScannerIssueSeverity.Warning, data.AssetPath,
                        "ChainDepth", data.SubEmitterChainDepth,
                        "SubEmitterCount", data.SubEmitterCount));
                }

                if (settings.CheckLOD && !string.IsNullOrEmpty(data.ScenePath) && !data.HasLOD)
                {
                    issues.Add(MakeIssue("particle_no_lod",
                        "Particle system in scene has no LOD mechanism. Will simulate at full cost regardless of distance.",
                        UnityScannerIssueSeverity.Info, data.AssetPath,
                        "ScenePath", data.ScenePath));
                }

                if (settings.CheckSimulationMismatch && data.SimulationSpace == "World" && data.MaxParticles > 1000)
                {
                    issues.Add(MakeIssue("particle_cpu_simulation_mismatch",
                        "World simulation space with " + data.MaxParticles + " max particles. GPU simulation would be more efficient.",
                        UnityScannerIssueSeverity.Info, data.AssetPath,
                        "SimulationSpace", data.SimulationSpace,
                        "MaxParticles", data.MaxParticles));
                }

                if (settings.CheckTextures && data.MainTextureSize > profile.MaxTextureSize)
                {
                    issues.Add(MakeIssue("particle_texture_oversized",
                        "Particle texture " + data.MainTextureSize + "px exceeds max " + profile.MaxTextureSize + "px.",
                        UnityScannerIssueSeverity.Warning, data.AssetPath,
                        "TexturePath", data.MainTexturePath ?? "",
                        "TextureSize", data.MainTextureSize,
                        "MaxSize", profile.MaxTextureSize));
                }

                if (settings.CheckModuleCount && data.ActiveModuleCount > profile.MaxParticleSystemModules)
                {
                    issues.Add(MakeIssue("particle_module_count_excessive",
                        "Active module count " + data.ActiveModuleCount + " exceeds threshold " + profile.MaxParticleSystemModules + ".",
                        UnityScannerIssueSeverity.Info, data.AssetPath,
                        "ModuleCount", data.ActiveModuleCount,
                        "ThresholdModules", profile.MaxParticleSystemModules));
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
                CategoryId = "particle_analysis",
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
