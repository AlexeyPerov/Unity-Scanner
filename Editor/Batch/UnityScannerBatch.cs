using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting;
using UnityScanner.Categories.Addressables;
using UnityScanner.Categories.AnimationAnalysis;
using UnityScanner.Categories.AudioAnalysis;
using UnityScanner.Categories.BuildPlatformReadiness;
using UnityScanner.Categories.Dependencies;
using UnityScanner.Categories.FontTextAnalysis;
using UnityScanner.Categories.Materials;
using UnityScanner.Categories.MissingReferences;
using UnityScanner.Categories.ParticleSystemAnalysis;
using UnityScanner.Categories.RegressionTrend;
using UnityScanner.Categories.ScenePrefabHealth;
using UnityScanner.Categories.ShaderAnalysis;
using UnityScanner.Categories.UICanvasAnalysis;
using UnityScanner.Categories.LightingAnalysis;
using UnityScanner.Categories.LODAnalysis;
using UnityScanner.Categories.PhysicsAnalysis;
using UnityScanner.Categories.AsmDefAudit;
using UnityScanner.Categories.Sprite2DAnalysis;
using UnityScanner.Categories.ProjectHealth;
using UnityScanner.Categories.TerrainAnalysis;
using UnityScanner.Categories.Textures;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Export;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Results;
using UnityScanner.Core.Settings;

namespace UnityScanner.Batch
{
    [Preserve]
    public static class UnityScannerBatch
    {
        public const int ExitSuccess = 0;
        public const int ExitIssuesAboveThreshold = 1;
        public const int ExitInvalidArguments = 2;
        public const int ExitRequiredInputMissing = 3;
        public const int ExitRuntimeFailure = 4;
        public const int ExitCacheFailure = 5;

        [Preserve]
        public static BatchResult RunAll(BatchOptions options = null)
        {
            options ??= new BatchOptions();
            var registry = CreateRegistry();
            var context = BuildContext(options);
            var orchestrator = new UnityScannerOrchestrator(registry);
            var sink = new BatchIssueSink();
            var results = new List<UnityScannerResult>();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var category in registry.Categories)
            {
                if (!category.Settings.Enabled) continue;

                context.PreviousResults = new UnityScannerAggregateResult();
                foreach (var r in results)
                    context.PreviousResults.Results.Add(r);

                var result = RunCategorySync(category, context, sink);
                results.Add(result);
            }

            stopwatch.Stop();

            return BuildBatchResult(results, options, stopwatch.Elapsed.TotalMilliseconds);
        }

        [Preserve]
        public static BatchResult RunDependencies(BatchOptions options = null)
        {
            return RunSingleCategory("dependencies", options);
        }

        [Preserve]
        public static BatchResult RunMissingReferences(BatchOptions options = null)
        {
            return RunSingleCategory("missing_references", options);
        }

        [Preserve]
        public static BatchResult RunMaterials(BatchOptions options = null)
        {
            return RunSingleCategory("materials", options);
        }

        [Preserve]
        public static BatchResult RunTextures(BatchOptions options = null)
        {
            return RunSingleCategory("textures", options);
        }

        [Preserve]
        public static BatchResult RunAddressables(BatchOptions options = null)
        {
            return RunSingleCategory("addressables", options);
        }

        [Preserve]
        public static BatchResult RunShaderAnalysis(BatchOptions options = null)
        {
            return RunSingleCategory("shader_analysis", options);
        }

        [Preserve]
        public static BatchResult RunTerrainAnalysis(BatchOptions options = null)
        {
            return RunSingleCategory("terrain_analysis", options);
        }

        [Preserve]
        public static BatchResult RunFontTextAnalysis(BatchOptions options = null)
        {
            return RunSingleCategory("font_text_analysis", options);
        }

        [Preserve]
        public static BatchResult RunAudioAnalysis(BatchOptions options = null)
        {
            return RunSingleCategory("audio_analysis", options);
        }

        [Preserve]
        public static BatchResult RunAnimationAnalysis(BatchOptions options = null)
        {
            return RunSingleCategory("animation_analysis", options);
        }

        [Preserve]
        public static BatchResult RunScenePrefabHealth(BatchOptions options = null)
        {
            return RunSingleCategory("scene_prefab_health", options);
        }
        
        [Preserve]
        public static BatchResult RunBuildPlatformReadiness(BatchOptions options = null)
        {
            return RunSingleCategory("build_platform_readiness", options);
        }

        [Preserve]
        public static BatchResult RunParticleAnalysis(BatchOptions options = null)
        {
            return RunSingleCategory("particle_analysis", options);
        }

        [Preserve]
        public static BatchResult RunUICanvasAnalysis(BatchOptions options = null)
        {
            return RunSingleCategory("ui_canvas_analysis", options);
        }

        [Preserve]
        public static BatchResult RunLightingAnalysis(BatchOptions options = null)
        {
            return RunSingleCategory("lighting_analysis", options);
        }

        [Preserve]
        public static BatchResult RunLODAnalysis(BatchOptions options = null)
        {
            return RunSingleCategory("lod_analysis", options);
        }

        [Preserve]
        public static BatchResult RunPhysicsAnalysis(BatchOptions options = null)
        {
            return RunSingleCategory("physics_analysis", options);
        }

        [Preserve]
        public static BatchResult RunAsmDefAudit(BatchOptions options = null)
        {
            return RunSingleCategory("asmdef_audit", options);
        }

        [Preserve]
        public static BatchResult RunProjectHealth(BatchOptions options = null)
        {
            return RunSingleCategory("project_health", options);
        }

        [Preserve]
        public static BatchResult RunSprite2DAnalysis(BatchOptions options = null)
        {
            return RunSingleCategory("sprite_2d_analysis", options);
        }

        [Preserve]
        public static BatchResult RunRegressionTrend(BatchOptions options = null)
        {
            return RunSingleCategory("regression_trend", options);
        }

        [Preserve]
        public static BatchResult RunSelectedCategories(string[] categoryIds, BatchOptions options = null)
        {
            options ??= new BatchOptions();
            var registry = CreateRegistry();
            ApplyOptions(registry, options);
            var context = BuildContext(options);
            var results = new List<UnityScannerResult>();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var catId in categoryIds)
            {
                var category = registry.GetCategory(catId);
                if (category == null || !category.Settings.Enabled) continue;

                context.PreviousResults = new UnityScannerAggregateResult();
                foreach (var r in results)
                    context.PreviousResults.Results.Add(r);

                var result = RunCategorySync(category, context, new BatchIssueSink());
                results.Add(result);
            }

            stopwatch.Stop();
            return BuildBatchResult(results, options, stopwatch.Elapsed.TotalMilliseconds);
        }

        [Preserve]
        public static BatchResult RebuildCache(BatchOptions options = null)
        {
            options ??= new BatchOptions();
            try
            {
                var cachePath = options.CachePath ?? "Library/UnityScanner/UnityScannerAssetCache.bin";
                if (File.Exists(cachePath))
                    File.Delete(cachePath);

                return new BatchResult
                {
                    ExitCode = ExitSuccess,
                    Message = "Cache cleared successfully."
                };
            }
            catch (Exception ex)
            {
                return new BatchResult
                {
                    ExitCode = ExitCacheFailure,
                    Message = $"Cache rebuild failed: {ex.Message}"
                };
            }
        }

        [Preserve]
        public static BatchResult ValidateCache(BatchOptions options = null)
        {
            options ??= new BatchOptions();
            var cachePath = options.CachePath ?? "Library/UnityScanner/UnityScannerAssetCache.bin";

            if (!File.Exists(cachePath))
            {
                return new BatchResult
                {
                    ExitCode = ExitSuccess,
                    Message = "No cache file found. Cache is clean."
                };
            }

            try
            {
                return new BatchResult
                {
                    ExitCode = ExitSuccess,
                    Message = $"Cache file exists at {cachePath} ({new FileInfo(cachePath).Length} bytes)."
                };
            }
            catch (Exception ex)
            {
                return new BatchResult
                {
                    ExitCode = ExitCacheFailure,
                    Message = $"Cache validation failed: {ex.Message}"
                };
            }
        }

        [Preserve]
        public static BatchResult BackupUnreferencedAssets(BatchOptions options = null)
        {
            options ??= new BatchOptions();
            options.DryRun = true;
            var result = RunDependencies(options);

            var unreferenced = result.Issues
                .Where(i => i.IssueCode == "unreferenced_cleanup_candidate" || i.IssueCode == "unreferenced_asset")
                .ToList();

            if (!options.DryRun)
            {
                var backupDir = options.OutputPath ?? "Library/UnityScanner/Backup";
                Directory.CreateDirectory(backupDir);

                foreach (var issue in unreferenced)
                {
                    if (string.IsNullOrEmpty(issue.AssetPath)) continue;
                    try
                    {
                        var src = issue.AssetPath;
                        var dst = Path.Combine(backupDir, Path.GetFileName(issue.AssetPath));
                        if (File.Exists(src))
                            File.Copy(src, dst, true);
                    }
                    catch { }
                }
            }

            result.ExitCode = ExitSuccess;
            result.Message = options.DryRun
                ? $"[DRY RUN] Would backup {unreferenced.Count} unreferenced assets."
                : $"Backed up {unreferenced.Count} unreferenced assets to {options.OutputPath}.";
            return result;
        }

        [Preserve]
        public static BatchResult DeleteUnreferencedAssets(BatchOptions options = null)
        {
            options ??= new BatchOptions();

            if (!options.ConfirmDestructive)
            {
                return new BatchResult
                {
                    ExitCode = ExitInvalidArguments,
                    Message = "Destructive operation requires explicit confirmation (set ConfirmDestructive = true)."
                };
            }

            var scanResult = RunDependencies(options);
            var unreferenced = scanResult.Issues
                .Where(i => i.IssueCode == "unreferenced_cleanup_candidate" || i.IssueCode == "unreferenced_asset")
                .ToList();

            if (options.DryRun)
            {
                scanResult.Message = $"[DRY RUN] Would delete {unreferenced.Count} unreferenced assets.";
                return scanResult;
            }

            var deleted = 0;
            foreach (var issue in unreferenced)
            {
                if (string.IsNullOrEmpty(issue.AssetPath)) continue;
                try
                {
                    if (AssetDatabase.MoveAssetToTrash(issue.AssetPath))
                        deleted++;
                }
                catch { }
            }

            AssetDatabase.Refresh();
            scanResult.ExitCode = ExitSuccess;
            scanResult.Message = $"Deleted {deleted}/{unreferenced.Count} unreferenced assets.";
            return scanResult;
        }

        [Preserve]
        public static BatchResult RunMaterialFixes(BatchOptions options = null)
        {
            options ??= new BatchOptions();
            var registry = CreateRegistry();
            ApplyOptions(registry, options);
            var context = BuildContext(options);
            var category = registry.GetCategory("materials") as MaterialsCategory;
            if (category == null)
                return new BatchResult { ExitCode = ExitRuntimeFailure, Message = "Materials category not found." };

            var sink = new BatchIssueSink();
            var scanResult = RunCategorySync(category, context, sink);

            var fixProvider = registry.GetFixProvider("materials") as MaterialsFixProvider;
            if (fixProvider == null)
                return new BatchResult { ExitCode = ExitRuntimeFailure, Message = "Materials fix provider not found." };

            var fixableIssues = scanResult.Issues.Where(i => fixProvider.CanFix(i)).ToList();

            if (options.DryRun)
            {
                return new BatchResult
                {
                    ExitCode = ExitSuccess,
                    Issues = fixableIssues,
                    Message = $"[DRY RUN] {fixableIssues.Count} material issues can be fixed.",
                    TotalDurationMs = scanResult.ScanDurationMs
                };
            }

            var fixedCount = 0;
            foreach (var issue in fixableIssues)
            {
                try
                {
                    var e = fixProvider.Apply(issue, context);
                    while (e.MoveNext()) { }
                    fixedCount++;
                }
                catch { }
            }

            return new BatchResult
            {
                ExitCode = ExitSuccess,
                Message = $"Fixed {fixedCount}/{fixableIssues.Count} material issues.",
                TotalDurationMs = scanResult.ScanDurationMs
            };
        }

        [Preserve]
        public static BatchResult ParseAndRun(string[] args)
        {
            var options = ParseArguments(args);
            if (options == null)
                return new BatchResult { ExitCode = ExitInvalidArguments, Message = "Invalid arguments." };

            var categories = options.Categories ?? new[] { "dependencies", "missing_references", "materials", "textures", "addressables" };
            return RunSelectedCategories(categories, options);
        }

        [Preserve]
        public static void WriteOutput(BatchResult result, BatchOptions options)
        {
            options ??= new BatchOptions();
            var format = options.OutputFormat ?? "json";
            var output = format switch
            {
                "csv" => FormatCsv(result),
                "log" => FormatLog(result),
                _ => FormatJson(result)
            };

            if (!string.IsNullOrEmpty(options.OutputPath))
            {
                var dir = Path.GetDirectoryName(options.OutputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(options.OutputPath, output);
            }
            else
            {
                Debug.Log(output);
            }
        }

        [Preserve]
        public static int ComputeExitCode(BatchResult result, string failOnSeverity = "error")
        {
            if (result.ExitCode != ExitSuccess && result.ExitCode != ExitIssuesAboveThreshold)
                return result.ExitCode;

            var threshold = failOnSeverity?.ToLowerInvariant() switch
            {
                "never" => (UnityScannerIssueSeverity?)null,
                "verbose" => UnityScannerIssueSeverity.Verbose,
                "info" => UnityScannerIssueSeverity.Info,
                "warn" => UnityScannerIssueSeverity.Warning,
                "error" => UnityScannerIssueSeverity.Error,
                _ => UnityScannerIssueSeverity.Error
            };

            if (threshold == null) return ExitSuccess;

            var hasAboveThreshold = result.Issues.Any(i => i.Severity >= threshold);
            return hasAboveThreshold ? ExitIssuesAboveThreshold : ExitSuccess;
        }

        #region Internal

        [Preserve]
        private static UnityScannerCategoryRegistry CreateRegistry()
        {
            var registry = new UnityScannerCategoryRegistry();
            registry.RegisterCategory(new DependenciesCategory());
            registry.RegisterCategory(new MissingReferencesCategory());
            registry.RegisterCategory(new MaterialsCategory());
            registry.RegisterCategory(new TexturesCategory());
            registry.RegisterCategory(new ShaderAnalysisCategory());
            registry.RegisterCategory(new TerrainAnalysisCategory());
            registry.RegisterCategory(new FontTextAnalysisCategory());
            registry.RegisterCategory(new AudioAnalysisCategory());
            registry.RegisterCategory(new AnimationAnalysisCategory());
            registry.RegisterCategory(new ScenePrefabHealthCategory());
            registry.RegisterCategory(new BuildPlatformReadinessCategory());
            registry.RegisterCategory(new ParticleSystemAnalysisCategory());
            registry.RegisterCategory(new UICanvasAnalysisCategory());
            registry.RegisterCategory(new LightingAnalysisCategory());
            registry.RegisterCategory(new LODAnalysisCategory());
            registry.RegisterCategory(new PhysicsAnalysisCategory());
            registry.RegisterCategory(new AsmDefAuditCategory());
            registry.RegisterCategory(new Sprite2DAnalysisCategory());
            registry.RegisterCategory(new AddressablesCategory());
            registry.RegisterCategory(new RegressionTrendCategory());
            registry.RegisterFixProvider("dependencies", new DependenciesFixProvider());
            registry.RegisterFixProvider("materials", new MaterialsFixProvider());
            return registry;
        }

        private static void ApplyOptions(UnityScannerCategoryRegistry registry, BatchOptions options)
        {
            if (options.Categories != null)
            {
                var enabledSet = new HashSet<string>(options.Categories);
                foreach (var cat in registry.Categories)
                    cat.Settings.Enabled = enabledSet.Contains(cat.Id);
            }

            if (options.ShaderVariantThreshold.HasValue || options.ShaderPassThreshold.HasValue ||
                options.ShaderKeywordThreshold.HasValue)
            {
                var shader = registry.GetCategory("shader_analysis")?.Settings as ShaderAnalysisSettings;
                if (shader != null)
                {
                    if (options.ShaderVariantThreshold.HasValue)
                        shader.VariantThreshold = options.ShaderVariantThreshold.Value;
                    if (options.ShaderPassThreshold.HasValue)
                        shader.PassThreshold = options.ShaderPassThreshold.Value;
                    if (options.ShaderKeywordThreshold.HasValue)
                        shader.KeywordThreshold = options.ShaderKeywordThreshold.Value;
                }
            }

            if (options.BaselinePath != null || options.RegressionThreshold.HasValue)
            {
                var regression = registry.GetCategory("regression_trend")?.Settings as RegressionTrendSettings;
                if (regression != null)
                {
                    if (options.BaselinePath != null)
                        regression.BaselinePath = options.BaselinePath;
                    if (options.RegressionThreshold.HasValue)
                    {
                        regression.RegressionWarningThreshold = options.RegressionThreshold.Value;
                        regression.RegressionErrorThreshold = options.RegressionThreshold.Value;
                    }
                }
            }

            if (options.ParticleEmissionThreshold.HasValue || options.ParticleMaxModules.HasValue ||
                options.ParticleMaxTrailLifetime.HasValue || options.ParticleCheckCollision.HasValue ||
                options.ParticleCheckOverdraw.HasValue)
            {
                var particle = registry.GetCategory("particle_analysis")?.Settings as ParticleSystemAnalysisSettings;
                if (particle != null)
                {
                    if (options.ParticleCheckCollision.HasValue)
                        particle.CheckCollision = options.ParticleCheckCollision.Value;
                    if (options.ParticleCheckOverdraw.HasValue)
                        particle.CheckOverdraw = options.ParticleCheckOverdraw.Value;
                }
            }

            if (options.UICheckRaycastTargets.HasValue || options.UICheckTextTmpMix.HasValue)
            {
                var ui = registry.GetCategory("ui_canvas_analysis")?.Settings as UICanvasAnalysisSettings;
                if (ui != null)
                {
                    if (options.UICheckRaycastTargets.HasValue)
                        ui.CheckRaycastTargets = options.UICheckRaycastTargets.Value;
                    if (options.UICheckTextTmpMix.HasValue)
                        ui.CheckTextTmpMix = options.UICheckTextTmpMix.Value;
                }
            }

            if (options.LightingCheckEmissiveGI.HasValue || options.LightingCheckMixedConsistency.HasValue)
            {
                var lighting = registry.GetCategory("lighting_analysis")?.Settings as LightingAnalysisSettings;
                if (lighting != null)
                {
                    if (options.LightingCheckEmissiveGI.HasValue)
                        lighting.CheckEmissiveNoGI = options.LightingCheckEmissiveGI.Value;
                    if (options.LightingCheckMixedConsistency.HasValue)
                        lighting.CheckModeInconsistent = options.LightingCheckMixedConsistency.Value;
                }
            }

            if (options.LODCheckMissingLevels.HasValue || options.LODCheckMaterialMismatch.HasValue)
            {
                var lod = registry.GetCategory("lod_analysis")?.Settings as LODAnalysisSettings;
                if (lod != null)
                {
                    if (options.LODCheckMissingLevels.HasValue)
                        lod.CheckMissingLevels = options.LODCheckMissingLevels.Value;
                    if (options.LODCheckMaterialMismatch.HasValue)
                        lod.CheckMaterialMismatch = options.LODCheckMaterialMismatch.Value;
                }
            }

            if (options.PhysicsCheckLayerMatrix.HasValue || options.PhysicsCheckMaterials.HasValue)
            {
                var physics = registry.GetCategory("physics_analysis")?.Settings as PhysicsAnalysisSettings;
                if (physics != null)
                {
                    if (options.PhysicsCheckLayerMatrix.HasValue)
                        physics.CheckLayerMatrixBloat = options.PhysicsCheckLayerMatrix.Value;
                    if (options.PhysicsCheckMaterials.HasValue)
                        physics.CheckMissingMaterial = options.PhysicsCheckMaterials.Value;
                }
            }

            if (options.AsmDefCheckCircular.HasValue || options.AsmDefCheckEditorLeakage.HasValue ||
                options.AsmDefCheckPlatformFilters.HasValue || options.AsmDefCheckDuplicates.HasValue)
            {
                var asmdef = registry.GetCategory("asmdef_audit")?.Settings as AsmDefAuditSettings;
                if (asmdef != null)
                {
                    if (options.AsmDefCheckCircular.HasValue)
                        asmdef.CheckCircularReferences = options.AsmDefCheckCircular.Value;
                    if (options.AsmDefCheckEditorLeakage.HasValue)
                        asmdef.CheckEditorInRuntime = options.AsmDefCheckEditorLeakage.Value;
                    if (options.AsmDefCheckPlatformFilters.HasValue)
                        asmdef.CheckPlatformFilterBroad = options.AsmDefCheckPlatformFilters.Value;
                    if (options.AsmDefCheckDuplicates.HasValue)
                        asmdef.CheckDuplicateName = options.AsmDefCheckDuplicates.Value;
                }
            }

            if (options.SpriteCheckPacking.HasValue || options.SpriteCheckDuplicates.HasValue || options.SpriteCheckPolygonVertices.HasValue)
            {
                var sprite = registry.GetCategory("sprite_2d_analysis")?.Settings as Sprite2DAnalysisSettings;
                if (sprite != null)
                {
                    if (options.SpriteCheckPacking.HasValue)
                        sprite.CheckNotPacked = options.SpriteCheckPacking.Value;
                    if (options.SpriteCheckDuplicates.HasValue)
                        sprite.CheckDuplicateContent = options.SpriteCheckDuplicates.Value;
                    if (options.SpriteCheckPolygonVertices.HasValue)
                        sprite.CheckPolygonVerticesExcessive = options.SpriteCheckPolygonVertices.Value;
                }
            }
        }

        private static UnityScannerScanContext BuildContext(BatchOptions options)
        {
            var settings = ScriptableObject.CreateInstance<UnityScannerSettings>();
            if (!string.IsNullOrEmpty(options.BuildLayoutPath))
                settings.BuildLayoutPath = options.BuildLayoutPath;
            if (!string.IsNullOrEmpty(options.CachePath))
                settings.CachePath = options.CachePath;
            settings.CacheEnabled = options.UseCache;
            settings.BinaryCacheEnabled = options.UseCache;

            if (!string.IsNullOrEmpty(options.PlatformProfile))
                settings.SetPlatformProfile(options.PlatformProfile);

            return new UnityScannerScanContext
            {
                Settings = settings
            };
        }

        private static UnityScannerResult RunCategorySync(IUnityScannerCategory category, UnityScannerScanContext context, IUnityScannerIssueSink sink)
        {
            var result = new UnityScannerResult
            {
                CategoryId = category.Id,
                DisplayName = category.DisplayName,
                ShortDisplayName = category.ShortDisplayName,
                Capabilities = category.Capabilities
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var enumerator = category.Scan(context, sink);
                if (enumerator != null)
                {
                    while (enumerator.MoveNext()) { }
                }
            }
            catch (Exception ex)
            {
                result.Succeeded = false;
                result.ErrorMessage = ex.Message;
            }

            sw.Stop();

            if (sink is BatchIssueSink batchSink)
                result.Issues = batchSink.Issues.ToList();

            result.ScanDurationMs = sw.Elapsed.TotalMilliseconds;
            return result;
        }

        private static BatchResult RunSingleCategory(string categoryId, BatchOptions options)
        {
            options ??= new BatchOptions();
            if (options.Categories == null)
                options.Categories = new[] { categoryId };
            return RunSelectedCategories(new[] { categoryId }, options);
        }

        private static BatchResult BuildBatchResult(List<UnityScannerResult> results, BatchOptions options, double totalMs)
        {
            var allIssues = results.SelectMany(r => r.Issues).ToList();
            var failed = results.Any(r => !r.Succeeded);

            return new BatchResult
            {
                ExitCode = failed ? ExitRuntimeFailure : ExitSuccess,
                Results = results,
                Issues = allIssues,
                TotalDurationMs = totalMs,
                ToolVersion = "1.0.0",
                UnityVersion = Application.unityVersion,
                Timestamp = DateTime.UtcNow.ToString("o"),
                ProjectName = Application.productName,
                Message = failed
                    ? $"Scan completed with failures. {results.Count(r => !r.Succeeded)} categories failed."
                    : $"Scan completed. {allIssues.Count} issues across {results.Count} categories."
            };
        }

        #endregion

        #region Argument Parsing

        private static BatchOptions ParseArguments(string[] args)
        {
            if (args == null || args.Length == 0) return new BatchOptions();

            var options = new BatchOptions();

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-usCategories":
                        if (i + 1 < args.Length)
                            options.Categories = args[++i].Split(',');
                        break;
                    case "-usOutput":
                        if (i + 1 < args.Length) options.OutputPath = args[++i];
                        break;
                    case "-usOutputFormat":
                        if (i + 1 < args.Length) options.OutputFormat = args[++i];
                        break;
                    case "-usBuildLayout":
                        if (i + 1 < args.Length) options.BuildLayoutPath = args[++i];
                        break;
                    case "-usSettings":
                        if (i + 1 < args.Length) options.SettingsPath = args[++i];
                        break;
                    case "-usUseCache":
                        if (i + 1 < args.Length) options.UseCache = args[++i].ToLowerInvariant() == "true";
                        break;
                    case "-usRebuildCache":
                        options.RebuildCache = true;
                        break;
                    case "-usFailOnSeverity":
                        if (i + 1 < args.Length) options.FailOnSeverity = args[++i];
                        break;
                    case "-usDryRun":
                        options.DryRun = true;
                        break;
                    case "-usIncludePaths":
                        if (i + 1 < args.Length) options.IncludePaths = args[++i].Split(';');
                        break;
                    case "-usIgnorePaths":
                        if (i + 1 < args.Length) options.IgnorePaths = args[++i].Split(';');
                        break;
                    case "-usPlatformProfile":
                        if (i + 1 < args.Length) options.PlatformProfile = args[++i];
                        break;
                    case "-usShaderVariantThreshold":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var svt))
                            options.ShaderVariantThreshold = svt;
                        break;
                    case "-usShaderPassThreshold":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var spt))
                            options.ShaderPassThreshold = spt;
                        break;
                    case "-usShaderKeywordThreshold":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int skt))
                            options.ShaderKeywordThreshold = skt;
                        break;
                    case "-usShaderProfile":
                        if (i + 1 < args.Length) options.ShaderProfile = args[++i];
                        break;
                    case "-usStrictPolicy":
                        if (i + 1 < args.Length) options.StrictPolicy = args[++i].ToLowerInvariant() == "true";
                        break;
                    case "-usBaselinePath":
                        if (i + 1 < args.Length) options.BaselinePath = args[++i];
                        break;
                    case "-usRegressionThreshold":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var rt))
                            options.RegressionThreshold = rt;
                        break;
                    case "-usParticleEmissionThreshold":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var pet))
                            options.ParticleEmissionThreshold = pet;
                        break;
                    case "-usParticleMaxModules":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var pmm))
                            options.ParticleMaxModules = pmm;
                        break;
                    case "-usParticleMaxTrailLifetime":
                        if (i + 1 < args.Length && float.TryParse(args[++i], out var pmt))
                            options.ParticleMaxTrailLifetime = pmt;
                        break;
                    case "-usParticleCheckCollision":
                        if (i + 1 < args.Length) options.ParticleCheckCollision = args[++i].ToLowerInvariant() == "true";
                        break;
                    case "-usParticleCheckOverdraw":
                        if (i + 1 < args.Length) options.ParticleCheckOverdraw = args[++i].ToLowerInvariant() == "true";
                        break;
                    case "-usUICheckRaycastTargets":
                        if (i + 1 < args.Length) options.UICheckRaycastTargets = args[++i].ToLowerInvariant() == "true";
                        break;
                    case "-usUICheckTextTmpMix":
                        if (i + 1 < args.Length) options.UICheckTextTmpMix = args[++i].ToLowerInvariant() == "true";
                        break;
                    case "-usUIMaxVertexCount":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var uivc))
                            options.UIMaxVertexCount = uivc;
                        break;
                    case "-usUIMaxNestingDepth":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var uind))
                            options.UIMaxNestingDepth = uind;
                        break;
                    case "-usLightingCheckEmissiveGI":
                        if (i + 1 < args.Length) options.LightingCheckEmissiveGI = args[++i].ToLowerInvariant() == "true";
                        break;
                    case "-usLightingCheckMixedConsistency":
                        if (i + 1 < args.Length) options.LightingCheckMixedConsistency = args[++i].ToLowerInvariant() == "true";
                        break;
                    case "-usLightingMaxRealtimeLights":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var lmrl))
                            options.LightingMaxRealtimeLights = lmrl;
                        break;
                    case "-usLightingMaxLightmapSize":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var lmls))
                            options.LightingMaxLightmapSize = lmls;
                        break;
                    case "-usLightingMaxReflectionProbes":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var lmrp))
                            options.LightingMaxReflectionProbes = lmrp;
                        break;
                }
            }

            return options;
        }

        #endregion

        #region Output Formatting

        private static string FormatJson(BatchResult result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"toolVersion\": \"{result.ToolVersion}\",");
            sb.AppendLine($"  \"unityVersion\": \"{result.UnityVersion}\",");
            sb.AppendLine($"  \"timestamp\": \"{result.Timestamp}\",");
            sb.AppendLine($"  \"project\": \"{result.ProjectName}\",");
            sb.AppendLine($"  \"exitCode\": {result.ExitCode},");
            sb.AppendLine($"  \"message\": \"{EscapeJson(result.Message)}\",");
            sb.AppendLine($"  \"totalDurationMs\": {result.TotalDurationMs:F1},");
            sb.AppendLine($"  \"totalIssues\": {result.Issues.Count},");
            sb.AppendLine($"  \"errors\": {result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error)},");
            sb.AppendLine($"  \"warnings\": {result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning)},");
            sb.AppendLine($"  \"info\": {result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Info)},");
            sb.AppendLine($"  \"verbose\": {result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Verbose)},");
            sb.AppendLine("  \"categories\": [");

            if (result.Results != null)
            {
                for (var i = 0; i < result.Results.Count; i++)
                {
                    var r = result.Results[i];
                    var comma = i < result.Results.Count - 1 ? "," : "";
                    sb.AppendLine($"    {{\"id\": \"{r.CategoryId}\", \"name\": \"{r.DisplayName}\", \"issues\": {r.Issues.Count}, \"durationMs\": {r.ScanDurationMs:F1}, \"succeeded\": {r.Succeeded.ToString().ToLowerInvariant()}}}{comma}");
                }
            }

            sb.AppendLine("  ],");
            sb.AppendLine("  \"issues\": [");

            for (var i = 0; i < result.Issues.Count; i++)
            {
                var issue = result.Issues[i];
                var comma = i < result.Issues.Count - 1 ? "," : "";
                sb.AppendLine($"    {{\"severity\": \"{issue.Severity}\", \"category\": \"{issue.CategoryId}\", \"code\": \"{EscapeJson(issue.IssueCode)}\", \"assetPath\": \"{EscapeJson(issue.AssetPath)}\", \"description\": \"{EscapeJson(issue.Description)}\"}}{comma}");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string FormatCsv(BatchResult result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Severity,Category,Code,AssetPath,Guid,Description");

            foreach (var issue in result.Issues)
            {
                sb.AppendLine($"{issue.Severity},{USExportUtilities.EscapeCsvField(issue.CategoryId)},{USExportUtilities.EscapeCsvField(issue.IssueCode)},{USExportUtilities.EscapeCsvField(issue.AssetPath)},{USExportUtilities.EscapeCsvField(issue.Guid)},{USExportUtilities.EscapeCsvField(issue.Description)}");
            }

            return sb.ToString();
        }

        private static string FormatLog(BatchResult result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"UnityScanner v{result.ToolVersion} | Unity {result.UnityVersion} | {result.Timestamp}");
            sb.AppendLine(result.Message);
            sb.AppendLine($"Duration: {result.TotalDurationMs:F0}ms");
            sb.AppendLine($"Errors: {result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error)} | Warnings: {result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning)} | Info: {result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Info)} | Verbose: {result.Issues.Count(i => i.Severity == UnityScannerIssueSeverity.Verbose)}");
            sb.AppendLine(new string('-', 80));

            foreach (var issue in result.Issues)
            {
                sb.AppendLine($"[{issue.Severity}] [{issue.CategoryId}] {issue.IssueCode}: {issue.Description}");
                if (!string.IsNullOrEmpty(issue.AssetPath))
                    sb.AppendLine($"  Asset: {issue.AssetPath}");
            }

            return sb.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        #endregion
    }

    public class BatchOptions
    {
        public string[] Categories;
        public string OutputPath;
        public string OutputFormat = "json";
        public string BuildLayoutPath;
        public string SettingsPath;
        public bool UseCache;
        public bool RebuildCache;
        public string FailOnSeverity = "error";
        public bool DryRun;
        public bool ConfirmDestructive;
        public string CachePath;
        public string[] IncludePaths;
        public string[] IgnorePaths;

        public string PlatformProfile;
        public int? ShaderVariantThreshold;
        public int? ShaderPassThreshold;
        public int? ShaderKeywordThreshold;
        public string ShaderProfile;
        public bool? StrictPolicy;
        public string BaselinePath;
        public int? RegressionThreshold;

        public int? ParticleEmissionThreshold;
        public int? ParticleMaxModules;
        public float? ParticleMaxTrailLifetime;
        public bool? ParticleCheckCollision;
        public bool? ParticleCheckOverdraw;

        public bool? UICheckRaycastTargets;
        public bool? UICheckTextTmpMix;
        public int? UIMaxVertexCount;
        public int? UIMaxNestingDepth;

        public bool? LightingCheckEmissiveGI;
        public bool? LightingCheckMixedConsistency;
        public int? LightingMaxRealtimeLights;
        public int? LightingMaxLightmapSize;
        public int? LightingMaxReflectionProbes;

        public int? LODMinLevels;
        public float? LODMaxScreenTransition;
        public bool? LODCheckMaterialMismatch;
        public bool? LODCheckMissingLevels;

        public int? PhysicsMaxRigidbodyCount;
        public int? PhysicsMaxMeshColliderTriangles;
        public bool? PhysicsCheckLayerMatrix;
        public bool? PhysicsCheckMaterials;

        public bool? AsmDefCheckCircular;
        public bool? AsmDefCheckEditorLeakage;
        public bool? AsmDefCheckPlatformFilters;
        public bool? AsmDefCheckDuplicates;

        public float? SpriteMaxAtlasUnusedRatio;
        public bool? SpriteCheckPacking;
        public bool? SpriteCheckDuplicates;
        public bool? SpriteCheckPolygonVertices;
    }

    public class BatchResult
    {
        public int ExitCode;
        public string Message;
        public List<UnityScannerResult> Results = new();
        public List<UnityScannerIssue> Issues = new();
        public double TotalDurationMs;
        public string ToolVersion;
        public string UnityVersion;
        public string Timestamp;
        public string ProjectName;
    }

    public class BatchIssueSink : IUnityScannerIssueSink
    {
        private readonly List<UnityScannerIssue> _issues = new();
        public IReadOnlyList<UnityScannerIssue> Issues => _issues;

        public void Add(UnityScannerIssue issue) => _issues.Add(issue);
        public void AddRange(IEnumerable<UnityScannerIssue> issues) => _issues.AddRange(issues);
        public void ReportProgress(float progress, string message) { }
        public void MarkSkipped(string reason = null) { }
    }
}
