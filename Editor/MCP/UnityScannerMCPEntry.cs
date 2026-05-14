using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityScanner.Batch;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Settings;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.MCP
{
    public static class UnityScannerMCPEntry
    {
        private const string ArgOutput = "-usOutput";
        private const string ArgCategory = "-usCategory";
        private const string ArgCategories = "-usCategories";
        private const string ArgProfile = "-usPlatformProfile";
        private const string ArgBaseline = "-usBaseline";

        public static void MCP_RunAll()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var outputPath = GetArg(args, ArgOutput);
                var profile = GetArg(args, ArgProfile);

                if (string.IsNullOrEmpty(outputPath))
                {
                    Debug.LogError("[MCP] Missing -usOutput argument");
                    EditorApplication.Exit(UnityScannerBatch.ExitInvalidArguments);
                    return;
                }

                var options = new BatchOptions { PlatformProfile = profile };
                var result = UnityScannerBatch.RunAll(options);
                var json = UnityScannerMCPResultFormatter.FormatScanResult(result);
                File.WriteAllText(outputPath, json);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MCP] " + ex.Message);
                WriteError(GetArg(Environment.GetCommandLineArgs(), ArgOutput), ex.Message);
                EditorApplication.Exit(UnityScannerBatch.ExitRuntimeFailure);
            }
        }

        public static void MCP_RunCategory()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var outputPath = GetArg(args, ArgOutput);
                var category = GetArg(args, ArgCategory);
                var profile = GetArg(args, ArgProfile);

                if (string.IsNullOrEmpty(outputPath))
                {
                    Debug.LogError("[MCP] Missing -usOutput argument");
                    EditorApplication.Exit(UnityScannerBatch.ExitInvalidArguments);
                    return;
                }

                if (string.IsNullOrEmpty(category))
                {
                    WriteError(outputPath, "Missing -usCategory argument");
                    EditorApplication.Exit(UnityScannerBatch.ExitInvalidArguments);
                    return;
                }

                var options = new BatchOptions { PlatformProfile = profile };
                var result = UnityScannerBatch.RunSelectedCategories(new[] { category }, options);
                var json = UnityScannerMCPResultFormatter.FormatScanResult(result);
                File.WriteAllText(outputPath, json);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MCP] " + ex.Message);
                WriteError(GetArg(Environment.GetCommandLineArgs(), ArgOutput), ex.Message);
                EditorApplication.Exit(UnityScannerBatch.ExitRuntimeFailure);
            }
        }

        public static void MCP_RunSelected()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var outputPath = GetArg(args, ArgOutput);
                var categoriesStr = GetArg(args, ArgCategories);
                var profile = GetArg(args, ArgProfile);

                if (string.IsNullOrEmpty(outputPath))
                {
                    Debug.LogError("[MCP] Missing -usOutput argument");
                    EditorApplication.Exit(UnityScannerBatch.ExitInvalidArguments);
                    return;
                }

                if (string.IsNullOrEmpty(categoriesStr))
                {
                    WriteError(outputPath, "Missing -usCategories argument");
                    EditorApplication.Exit(UnityScannerBatch.ExitInvalidArguments);
                    return;
                }

                var categoryIds = categoriesStr.Split(',').Select(s => s.Trim()).ToArray();
                var options = new BatchOptions
                {
                    Categories = categoryIds,
                    PlatformProfile = profile
                };
                var result = UnityScannerBatch.RunSelectedCategories(categoryIds, options);
                var json = UnityScannerMCPResultFormatter.FormatScanResult(result);
                File.WriteAllText(outputPath, json);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MCP] " + ex.Message);
                WriteError(GetArg(Environment.GetCommandLineArgs(), ArgOutput), ex.Message);
                EditorApplication.Exit(UnityScannerBatch.ExitRuntimeFailure);
            }
        }

        public static void MCP_ListCategories()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var outputPath = GetArg(args, ArgOutput);

                if (string.IsNullOrEmpty(outputPath))
                {
                    Debug.LogError("[MCP] Missing -usOutput argument");
                    EditorApplication.Exit(UnityScannerBatch.ExitInvalidArguments);
                    return;
                }

                var registry = CreateRegistry();
                var json = UnityScannerMCPResultFormatter.FormatCategoryList(registry.Categories.ToList());
                File.WriteAllText(outputPath, json);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MCP] " + ex.Message);
                WriteError(GetArg(Environment.GetCommandLineArgs(), ArgOutput), ex.Message);
                EditorApplication.Exit(UnityScannerBatch.ExitRuntimeFailure);
            }
        }

        public static void MCP_GetSettings()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var outputPath = GetArg(args, ArgOutput);

                if (string.IsNullOrEmpty(outputPath))
                {
                    Debug.LogError("[MCP] Missing -usOutput argument");
                    EditorApplication.Exit(UnityScannerBatch.ExitInvalidArguments);
                    return;
                }

                var settings = ScriptableObject.CreateInstance<UnityScannerSettings>();
                var json = UnityScannerMCPResultFormatter.FormatSettings(settings);
                File.WriteAllText(outputPath, json);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MCP] " + ex.Message);
                WriteError(GetArg(Environment.GetCommandLineArgs(), ArgOutput), ex.Message);
                EditorApplication.Exit(UnityScannerBatch.ExitRuntimeFailure);
            }
        }

        public static void MCP_SetProfile()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var outputPath = GetArg(args, ArgOutput);
                var profile = GetArg(args, ArgProfile);

                if (string.IsNullOrEmpty(outputPath))
                {
                    Debug.LogError("[MCP] Missing -usOutput argument");
                    EditorApplication.Exit(UnityScannerBatch.ExitInvalidArguments);
                    return;
                }

                if (string.IsNullOrEmpty(profile))
                {
                    WriteError(outputPath, "Missing -usPlatformProfile argument");
                    EditorApplication.Exit(UnityScannerBatch.ExitInvalidArguments);
                    return;
                }

                var settings = ScriptableObject.CreateInstance<UnityScannerSettings>();
                settings.SetPlatformProfile(profile);
                var json = UnityScannerMCPResultFormatter.FormatProfileSet(profile);
                File.WriteAllText(outputPath, json);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MCP] " + ex.Message);
                WriteError(GetArg(Environment.GetCommandLineArgs(), ArgOutput), ex.Message);
                EditorApplication.Exit(UnityScannerBatch.ExitRuntimeFailure);
            }
        }

        public static void MCP_RegressionCheck()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var outputPath = GetArg(args, ArgOutput);
                var baselinePath = GetArg(args, ArgBaseline);
                var profile = GetArg(args, ArgProfile);

                if (string.IsNullOrEmpty(outputPath))
                {
                    Debug.LogError("[MCP] Missing -usOutput argument");
                    EditorApplication.Exit(UnityScannerBatch.ExitInvalidArguments);
                    return;
                }

                if (string.IsNullOrEmpty(baselinePath) || !File.Exists(baselinePath))
                {
                    WriteError(outputPath, "Missing or invalid -usBaseline path");
                    EditorApplication.Exit(UnityScannerBatch.ExitRequiredInputMissing);
                    return;
                }

                var options = new BatchOptions
                {
                    PlatformProfile = profile,
                    BaselinePath = baselinePath
                };
                var result = UnityScannerBatch.RunRegressionTrend(options);
                var textSummary = UnityScannerMCPResultFormatter.BuildTextSummary(result);
                var json = UnityScannerMCPResultFormatter.FormatRegression(result, textSummary);
                File.WriteAllText(outputPath, json);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MCP] " + ex.Message);
                WriteError(GetArg(Environment.GetCommandLineArgs(), ArgOutput), ex.Message);
                EditorApplication.Exit(UnityScannerBatch.ExitRuntimeFailure);
            }
        }

        public static void MCP_BaselineCreate()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var outputPath = GetArg(args, ArgOutput);
                var profile = GetArg(args, ArgProfile);

                if (string.IsNullOrEmpty(outputPath))
                {
                    Debug.LogError("[MCP] Missing -usOutput argument");
                    EditorApplication.Exit(UnityScannerBatch.ExitInvalidArguments);
                    return;
                }

                var options = new BatchOptions { PlatformProfile = profile };
                var result = UnityScannerBatch.RunAll(options);
                var json = UnityScannerMCPResultFormatter.FormatBaseline(result, outputPath);
                File.WriteAllText(outputPath, json);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[MCP] " + ex.Message);
                WriteError(GetArg(Environment.GetCommandLineArgs(), ArgOutput), ex.Message);
                EditorApplication.Exit(UnityScannerBatch.ExitRuntimeFailure);
            }
        }

        private static UnityScannerCategoryRegistry CreateRegistry()
        {
            var registry = new UnityScannerCategoryRegistry();
            registry.RegisterCategory(new UnityScanner.Categories.Dependencies.DependenciesCategory());
            registry.RegisterCategory(new UnityScanner.Categories.MissingReferences.MissingReferencesCategory());
            registry.RegisterCategory(new UnityScanner.Categories.Materials.MaterialsCategory());
            registry.RegisterCategory(new UnityScanner.Categories.Textures.TexturesCategory());
            registry.RegisterCategory(new UnityScanner.Categories.ShaderAnalysis.ShaderAnalysisCategory());
            registry.RegisterCategory(new UnityScanner.Categories.TerrainAnalysis.TerrainAnalysisCategory());
            registry.RegisterCategory(new UnityScanner.Categories.FontTextAnalysis.FontTextAnalysisCategory());
            registry.RegisterCategory(new UnityScanner.Categories.AudioAnalysis.AudioAnalysisCategory());
            registry.RegisterCategory(new UnityScanner.Categories.AnimationAnalysis.AnimationAnalysisCategory());
            registry.RegisterCategory(new UnityScanner.Categories.ScenePrefabHealth.ScenePrefabHealthCategory());
            registry.RegisterCategory(new UnityScanner.Categories.BuildPlatformReadiness.BuildPlatformReadinessCategory());
            registry.RegisterCategory(new UnityScanner.Categories.ParticleSystemAnalysis.ParticleSystemAnalysisCategory());
            registry.RegisterCategory(new UnityScanner.Categories.UICanvasAnalysis.UICanvasAnalysisCategory());
            registry.RegisterCategory(new UnityScanner.Categories.LightingAnalysis.LightingAnalysisCategory());
            registry.RegisterCategory(new UnityScanner.Categories.LODAnalysis.LODAnalysisCategory());
            registry.RegisterCategory(new UnityScanner.Categories.PhysicsAnalysis.PhysicsAnalysisCategory());
            registry.RegisterCategory(new UnityScanner.Categories.AsmDefAudit.AsmDefAuditCategory());
            registry.RegisterCategory(new UnityScanner.Categories.ProjectHealth.ProjectHealthCategory());
            registry.RegisterCategory(new UnityScanner.Categories.Sprite2DAnalysis.Sprite2DAnalysisCategory());
            registry.RegisterCategory(new UnityScanner.Categories.Addressables.AddressablesCategory());
            return registry;
        }

        private static string GetArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == name && i + 1 < args.Length)
                    return args[i + 1];
            }
            return null;
        }

        private static void WriteError(string outputPath, string message)
        {
            if (!string.IsNullOrEmpty(outputPath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllText(outputPath, UnityScannerMCPResultFormatter.FormatError(message));
                }
                catch { }
            }
        }
    }
}