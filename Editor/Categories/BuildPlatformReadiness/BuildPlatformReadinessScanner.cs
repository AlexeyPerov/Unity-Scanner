using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Categories.BuildPlatformReadiness
{
    public static class BuildPlatformReadinessScanner
    {
        public static void ScanAll(
            BuildPlatformReadinessSettings settings,
            PlatformProfile profile,
            List<ImportPolicyViolation> violations,
            List<PlatformIncompatibility> incompatibilities,
            List<StrippingRisk> strippingRisks,
            List<StartupBudgetStatus> budgetStatuses,
            IUnityScannerIssueSink issueSink)
        {
            if (settings.CheckImportPolicies)
                ScanImportPolicies(profile, violations, issueSink);

            if (settings.CheckPlatformCompatibility)
                ScanPlatformCompatibility(profile, incompatibilities, issueSink);

            if (settings.CheckStrippingRisk)
                ScanStrippingRisks(strippingRisks, issueSink);

            if (settings.CheckStartupBudget)
                ScanStartupBudget(profile, budgetStatuses, issueSink);

            GC.Collect();
        }

        private static void ScanImportPolicies(
            PlatformProfile profile,
            List<ImportPolicyViolation> violations,
            IUnityScannerIssueSink issueSink)
        {
            if (profile == null) return;

            issueSink.ReportProgress(0f, "Checking texture import policies...");
            var textureGuids = AssetDatabase.FindAssets("t:Texture2D");
            var total = textureGuids.Length;

            for (var i = 0; i < textureGuids.Length; i++)
            {
                if (i % 200 == 0)
                    issueSink.ReportProgress((float)i / total * 0.3f, "Checking texture imports...");

                var path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                var tex = AssetDatabase.LoadMainAssetAtPath(path) as Texture2D;
                if (tex == null) continue;

                if (tex.width > profile.MaxTextureSize || tex.height > profile.MaxTextureSize)
                {
                    var maxSize = Math.Max(tex.width, tex.height);
                    violations.Add(new ImportPolicyViolation
                    {
                        AssetPath = path,
                        AssetName = Path.GetFileName(path),
                        AssetType = "Texture",
                        ViolationType = "oversized_texture",
                        CurrentValue = maxSize.ToString(),
                        ExpectedValue = profile.MaxTextureSize.ToString(),
                        Description = "Texture '" + Path.GetFileName(path) + "' is " + maxSize + "px (max: " + profile.MaxTextureSize + ").",
                        RecommendedFix = "Resize texture to " + profile.MaxTextureSize + "px or less, or enable 'Max Size' override in the import settings for " + profile.DisplayName + "."
                    });
                }

                if (profile.RequireETC2Compression || profile.RequireASTCCompression)
                {
                    var platform = profile.Id;
                    var settings2d = platform == PlatformProfilePresets.Mobile
                        ? importer.GetPlatformTextureSettings("Android")
                        : null;

                    if (settings2d != null && settings2d.overridden && !settings2d.format.ToString().Contains("ETC") && !settings2d.format.ToString().Contains("ASTC"))
                    {
                        violations.Add(new ImportPolicyViolation
                        {
                            AssetPath = path,
                            AssetName = Path.GetFileName(path),
                            AssetType = "Texture",
                            ViolationType = "compression_mismatch",
                            CurrentValue = settings2d.format.ToString(),
                            ExpectedValue = "ETC2/ASTC",
                            Description = "Texture '" + Path.GetFileName(path) + "' uses " + settings2d.format + " instead of ETC2/ASTC for mobile.",
                            RecommendedFix = "In the texture import inspector, override the Android platform format to ETC2 or ASTC for smaller build size and hardware acceleration."
                        });
                    }
                }
            }

            issueSink.ReportProgress(0.3f, "Checking audio import policies...");
            var audioGuids = AssetDatabase.FindAssets("t:AudioClip");
            for (var i = 0; i < audioGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(audioGuids[i]);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                var settings3d = importer.defaultSampleSettings;
                long sizeBytes = 0;
                try { if (File.Exists(path)) sizeBytes = new FileInfo(path).Length; } catch { }
                var sizeMB = sizeBytes / (1024f * 1024f);

                if (sizeMB > profile.MaxAudioClipSizeMB)
                {
                    violations.Add(new ImportPolicyViolation
                    {
                        AssetPath = path,
                        AssetName = Path.GetFileName(path),
                        AssetType = "Audio",
                        ViolationType = "oversized_clip",
                        CurrentValue = sizeMB.ToString("F1") + "MB",
                        ExpectedValue = profile.MaxAudioClipSizeMB + "MB",
                        Description = "Audio clip '" + Path.GetFileName(path) + "' is " + sizeMB.ToString("F1") + "MB (max: " + profile.MaxAudioClipSizeMB + "MB).",
                        RecommendedFix = "Reduce clip length, increase compression, or convert to a smaller format (Vorbis/ADPCM). For long clips, use Streaming load type."
                    });
                }

                if (profile.Id == PlatformProfilePresets.Mobile && settings3d.loadType == AudioClipLoadType.DecompressOnLoad && sizeMB > 2)
                {
                    violations.Add(new ImportPolicyViolation
                    {
                        AssetPath = path,
                        AssetName = Path.GetFileName(path),
                        AssetType = "Audio",
                        ViolationType = "audio_load_mismatch",
                        CurrentValue = "DecompressOnLoad",
                        ExpectedValue = "Streaming",
                        Description = "Audio clip '" + Path.GetFileName(path) + "' (" + sizeMB.ToString("F1") + "MB) should use Streaming on mobile, not DecompressOnLoad.",
                        RecommendedFix = "Switch the clip's Load Type to 'Streaming' in the audio import settings to avoid loading the entire clip into RAM at startup."
                    });
                }
            }

            foreach (var v in violations)
            {
                if (v.ViolationType == "oversized_texture" || v.ViolationType == "oversized_clip")
                    v.TrySetWarningLevel(2);
                else
                    v.TrySetWarningLevel(1);
            }
        }

        private static void ScanPlatformCompatibility(
            PlatformProfile profile,
            List<PlatformIncompatibility> incompatibilities,
            IUnityScannerIssueSink issueSink)
        {
            if (profile == null) return;

            issueSink.ReportProgress(0.4f, "Checking mesh compatibility...");
            var meshGuids = AssetDatabase.FindAssets("t:Mesh");
            foreach (var guid in meshGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadMainAssetAtPath(path) as Mesh;
                if (mesh == null) continue;

                if (mesh.vertexCount > profile.MaxMeshVertexCount)
                {
                    incompatibilities.Add(new PlatformIncompatibility
                    {
                        AssetPath = path,
                        AssetName = Path.GetFileName(path),
                        SettingName = "VertexCount",
                        CurrentValue = mesh.vertexCount.ToString(),
                        RequiredValue = profile.MaxMeshVertexCount.ToString(),
                        Description = "Mesh '" + Path.GetFileName(path) + "' has " + mesh.vertexCount + " vertices (max: " + profile.MaxMeshVertexCount + ")."
                    });
                }
            }

            foreach (var inc in incompatibilities)
                inc.TrySetWarningLevel(2);
        }

        private static void ScanStrippingRisks(
            List<StrippingRisk> risks,
            IUnityScannerIssueSink issueSink)
        {
            issueSink.ReportProgress(0.7f, "Checking stripping risks...");
            var scriptGuids = AssetDatabase.FindAssets("t:MonoScript");
            var reflectionPattern = new Regex(
                @"(?:System\.Reflection|GetMethod|GetField|GetProperty|GetTypeInfo|Assembly\.Load|Activator\.CreateInstance|Type\.GetType)",
                RegexOptions.Compiled);
            var serializeFieldPattern = new Regex(
                @"(?:SerializeField|SerializeReference|FormerlySerializedAs)",
                RegexOptions.Compiled);
            var addressablePattern = new Regex(
                @"(?:Addressables\.LoadAssetAsync|Addressables\.InstantiateAsync|AssetReference)",
                RegexOptions.Compiled);

            foreach (var guid in scriptGuids)
            {
                var scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!scriptPath.EndsWith(".cs")) continue;
                if (scriptPath.Contains("/Editor/")) continue;
                if (scriptPath.StartsWith("Packages/")) continue;

                try
                {
                    var content = File.ReadAllText(scriptPath);
                    if (content.Length > 500000) continue;

                    if (reflectionPattern.IsMatch(content))
                    {
                        risks.Add(new StrippingRisk
                        {
                            ScriptPath = scriptPath,
                            ScriptName = Path.GetFileName(scriptPath),
                            RiskType = "reflection",
                            Description = "Script uses System.Reflection which may be stripped in IL2CPP builds.",
                            RecommendedFix = "Add a link.xml file to preserve types accessed via reflection, or use [Preserve] / [assembly: Preserve] attributes."
                        });
                    }

                    if (addressablePattern.IsMatch(content))
                    {
                        risks.Add(new StrippingRisk
                        {
                            ScriptPath = scriptPath,
                            ScriptName = Path.GetFileName(scriptPath),
                            RiskType = "addressables",
                            Description = "Script uses Addressables runtime loading which may require preserved type references.",
                            RecommendedFix = "Ensure types loaded via Addressables are referenced in a link.xml file or marked with [Preserve] to prevent IL2CPP stripping."
                        });
                    }
                }
                catch { }
            }

            foreach (var risk in risks)
                risk.TrySetWarningLevel(1);
        }

        private static void ScanStartupBudget(
            PlatformProfile profile,
            List<StartupBudgetStatus> statuses,
            IUnityScannerIssueSink issueSink)
        {
            if (profile == null) return;

            issueSink.ReportProgress(0.9f, "Checking startup budget...");
            var budgetKB = profile.StartupSceneBudgetKB;

            var totalSceneSizeKB = 0L;
            var buildScenes = new List<string>();
            for (var i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                if (!EditorBuildSettings.scenes[i].enabled) continue;
                var scenePath = EditorBuildSettings.scenes[i].path;
                buildScenes.Add(scenePath);
                try
                {
                    if (File.Exists(scenePath))
                        totalSceneSizeKB += new FileInfo(scenePath).Length / 1024;
                }
                catch { }
            }

            var totalAudioKB = 0L;
            foreach (var scene in buildScenes)
            {
                var deps = AssetDatabase.GetDependencies(scene, true);
                foreach (var dep in deps)
                {
                    if (!dep.EndsWith(".wav") && !dep.EndsWith(".mp3") && !dep.EndsWith(".ogg") && !dep.EndsWith(".aiff")) continue;
                    try
                    {
                        if (File.Exists(dep))
                            totalAudioKB += new FileInfo(dep).Length / 1024;
                    }
                    catch { }
                }
            }

            var percentUsed = budgetKB > 0 ? (float)totalSceneSizeKB / budgetKB * 100f : 0f;
            statuses.Add(new StartupBudgetStatus
            {
                Category = "Startup Scenes",
                CurrentBytes = totalSceneSizeKB * 1024,
                BudgetBytes = budgetKB * 1024,
                PercentUsed = percentUsed,
                Description = "Total startup scene size: " + (totalSceneSizeKB / 1024f).ToString("F1") + "MB / " + (budgetKB / 1024f).ToString("F1") + "MB (" + percentUsed.ToString("F0") + "%)",
                Explanation = "All enabled Build Scenes are loaded at startup. Exceeding the budget increases first-frame time and memory pressure on low-end devices. Consider splitting large bootstrap scenes or using additive loading.",
                Tooltip = "Combined file size of all enabled scenes in Build Settings vs. the startup budget defined by the active platform profile."
            });

            if (percentUsed > 100f)
                statuses[statuses.Count - 1].TrySetWarningLevel(3);
            else if (percentUsed > 80f)
                statuses[statuses.Count - 1].TrySetWarningLevel(2);
            else if (percentUsed > 60f)
                statuses[statuses.Count - 1].TrySetWarningLevel(1);

            var audioBudgetKB = profile.MaxStartupAudioTotalMB * 1024L;
            var audioPercent = audioBudgetKB > 0 ? (float)totalAudioKB / audioBudgetKB * 100f : 0f;
            statuses.Add(new StartupBudgetStatus
            {
                Category = "Startup Audio",
                CurrentBytes = totalAudioKB * 1024,
                BudgetBytes = audioBudgetKB * 1024,
                PercentUsed = audioPercent,
                Description = "Startup audio: " + (totalAudioKB / 1024f).ToString("F1") + "MB / " + (audioBudgetKB / 1024f).ToString("F1") + "MB (" + audioPercent.ToString("F0") + "%)",
                Explanation = "Audio clips referenced by startup scenes count toward the budget. Decompress-on-load clips consume RAM immediately. Use Streaming or load on demand to reduce startup impact.",
                Tooltip = "Combined size of audio files referenced by enabled Build Scenes vs. the audio budget defined by the active platform profile."
            });

            if (audioPercent > 100f)
                statuses[statuses.Count - 1].TrySetWarningLevel(3);
            else if (audioPercent > 80f)
                statuses[statuses.Count - 1].TrySetWarningLevel(2);
            else if (audioPercent > 60f)
                statuses[statuses.Count - 1].TrySetWarningLevel(1);
        }
    }
}
