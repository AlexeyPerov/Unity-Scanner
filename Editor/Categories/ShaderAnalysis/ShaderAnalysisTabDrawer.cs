using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Export;
using UnityScanner.Core.Results;
using UnityScanner.UI.Controls;

namespace UnityScanner.Categories.ShaderAnalysis
{
    public class ShaderAnalysisTabDrawer : IUnityScannerTabDrawer
    {
        public string CategoryId => "shader_analysis";

        private const int PageSize = 50;

        private ShaderAnalysisCategory _category;
        public System.Action OnScanRequested;

        private readonly USPaginationSettings _pagination = new() { PageToShow = 0, PageSize = PageSize };
        private Vector2 _listScroll;

        private int _subTab;
        private string _pathFilter = "";
        private bool _warningsOnly;
        private int _sortMode;
        private string _keywordFilter = "";

        private List<ShaderData> _cachedShaders;
        private List<MaterialData> _cachedMaterials;
        private bool _cacheDirty = true;

        private bool _settingsFoldout;
        private int _expandedShader = -1;
        private int _expandedMaterial = -1;

        public void Bind(ShaderAnalysisCategory category)
        {
            _category = category;
        }

        public void DrawHeader(UnityScannerResult result)
        {
            if (_category == null) return;

            if (!string.IsNullOrEmpty(_category.OutputDescription))
                GUILayout.Label(_category.OutputDescription, EditorStyles.miniLabel);
        }

        public void DrawIssues(UnityScannerResult result)
        {
            if (_category?.LastShaders == null && _category?.LastMaterials == null) return;

            DrawSubTabs();
            USGUIUtilities.HorizontalLine();
            DrawFilters();
            USGUIUtilities.HorizontalLine();
            DrawExportButtons();
            USGUIUtilities.HorizontalLine();
            USGUIPaginationUtilities.DrawPagesWidget(GetFilteredCount(), _pagination);
            USGUIUtilities.HorizontalLine();
            DrawList();
        }

        public void DrawTopBar(UnityScannerResult result)
        {
            if (_category == null) return;

            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Analysis Settings");
            if (!_settingsFoldout) return;

            EditorGUI.indentLevel++;
            var settings = _category.Settings as ShaderAnalysisSettings;
            if (settings == null) { EditorGUI.indentLevel--; return; }

            EditorGUILayout.HelpBox("Changes apply on the next analysis launch.", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            settings.VariantThreshold = EditorGUILayout.IntField("Variant Threshold", settings.VariantThreshold);
            settings.PassThreshold = EditorGUILayout.IntField("Pass Threshold", settings.PassThreshold);
            settings.KeywordThreshold = EditorGUILayout.IntField("Keyword Threshold", settings.KeywordThreshold);
            settings.DetectErrorShaders = EditorGUILayout.ToggleLeft("Detect Error Shaders", settings.DetectErrorShaders);
            settings.DetectFallbackShaders = EditorGUILayout.ToggleLeft("Detect Fallback Shaders", settings.DetectFallbackShaders);
            settings.DetectDuplicateKeywords = EditorGUILayout.ToggleLeft("Detect Duplicate Keywords", settings.DetectDuplicateKeywords);
            settings.DetectPlatformMismatches = EditorGUILayout.ToggleLeft("Detect Platform Mismatches", settings.DetectPlatformMismatches);
            settings.DetectExpensiveFeatures = EditorGUILayout.ToggleLeft("Detect Expensive Features", settings.DetectExpensiveFeatures);
            if (EditorGUI.EndChangeCheck())
                InvalidateCache();

            EditorGUI.indentLevel--;
        }

        private void DrawSubTabs()
        {
            var shaders = GetFilteredShaders();
            var materials = GetFilteredMaterials();

            EditorGUILayout.BeginHorizontal();

            var prevColor = GUI.color;
            GUI.color = _subTab == 0 ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent($"Shaders ({shaders.Count})", "Switch to shaders view"), EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                _subTab = 0;
                InvalidateCache();
            }

            GUI.color = _subTab == 1 ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent($"Materials ({materials.Count})", "Switch to materials view"), EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                _subTab = 1;
                InvalidateCache();
            }

            GUI.color = prevColor;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Path:", GUILayout.Width(40));
            var newPath = GUILayout.TextField(_pathFilter, GUILayout.Width(250));
            if (newPath != _pathFilter)
            {
                _pathFilter = newPath;
                InvalidateCache();
            }

            GUILayout.Label("Keyword:", GUILayout.Width(55));
            var newKw = GUILayout.TextField(_keywordFilter, GUILayout.Width(150));
            if (newKw != _keywordFilter)
            {
                _keywordFilter = newKw;
                InvalidateCache();
            }

            EditorGUI.BeginChangeCheck();
            _warningsOnly = EditorGUILayout.ToggleLeft("Warnings", _warningsOnly, GUILayout.Width(140));
            if (EditorGUI.EndChangeCheck())
                InvalidateCache();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Sort:", GUILayout.Width(40));
            var sortLabel = _sortMode switch
            {
                0 => "Warnings Desc",
                1 => "Warnings Asc",
                2 => "Path A-Z",
                3 => "Path Z-A",
                4 => "Variants Desc",
                _ => "Warnings Desc"
            };
            if (GUILayout.Button(new GUIContent(sortLabel, "Sort entries by this criteria"), GUILayout.Width(130)))
            {
                _sortMode = _sortMode >= 4 ? 0 : _sortMode + 1;
                InvalidateCache();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawExportButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("Copy to Clipboard", "Copy filtered data to clipboard"), GUILayout.Width(140)))
                ExportToClipboard();

            if (GUILayout.Button(new GUIContent("Export CSV...", "Export filtered data to a CSV file"), GUILayout.Width(100)))
                ExportToCsv();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private int GetFilteredCount()
        {
            return _subTab == 0 ? GetFilteredShaders().Count : GetFilteredMaterials().Count;
        }

        private void DrawList()
        {
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            EditorGUILayout.BeginVertical();

            if (_subTab == 0)
                DrawShaderList();
            else
                DrawMaterialList();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawShaderList()
        {
            var shaders = GetFilteredShaders();

            for (var i = 0; i < shaders.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination))
                    continue;

                DrawShaderRow(shaders[i], i);
            }
        }

        private void DrawShaderRow(ShaderData shader, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var isExpanded = _expandedShader == index;
            var toggleChar = isExpanded ? "v" : ">";
            if (GUILayout.Button(new GUIContent(toggleChar, "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedShader = isExpanded ? -1 : index;

            var sevColor = shader.WarningLevel switch
            {
                >= 3 => Color.red,
                2 => Color.yellow,
                1 => Color.cyan,
                _ => Color.white
            };
            USGUIUtilities.DrawColoredLabel($"[{shader.WarningLevel}]", sevColor, 30);

            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            USGUIUtilities.DrawAssetButton(shader.Path, 250f, 18f);
            GUILayout.Label($"V:{shader.VariantCount}", EditorStyles.miniLabel, GUILayout.Width(70));
            GUILayout.Label($"K:{shader.KeywordCount}", EditorStyles.miniLabel, GUILayout.Width(50));
            GUILayout.Label($"P:{shader.PassCount}", EditorStyles.miniLabel, GUILayout.Width(50));
            GUILayout.Label(shader.RenderPipeline, EditorStyles.miniLabel, GUILayout.Width(60));
            GUILayout.Label($"Mats:{shader.ReferencingMaterials.Count}", EditorStyles.miniLabel, GUILayout.Width(50));

            EditorGUILayout.EndHorizontal();

            if (isExpanded)
                DrawShaderDetail(shader);
        }

        private void DrawShaderDetail(ShaderData shader)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label($"Shader: {shader.Name}", EditorStyles.miniLabel);
            GUILayout.Label($"Path: {shader.Path}", EditorStyles.miniLabel);
            GUILayout.Label($"Render Pipeline: {shader.RenderPipeline}", EditorStyles.miniLabel);
            GUILayout.Label($"Estimated Variants: {shader.VariantCount}", EditorStyles.miniLabel);
            GUILayout.Label($"Keywords: {shader.KeywordCount}", EditorStyles.miniLabel);
            GUILayout.Label($"Passes: {shader.PassCount}", EditorStyles.miniLabel);

            if (shader.IsErrorShader)
                USGUIUtilities.DrawColoredLabel("ERROR SHADER", Color.red, 100);
            if (shader.IsFallbackShader)
            {
                var fallbackName = ShaderAnalysisScanner.GetShaderFallbackName(shader.Shader, shader.Path);
                GUILayout.Label($"Fallback: {(string.IsNullOrEmpty(fallbackName) ? "Yes" : fallbackName)}", EditorStyles.miniLabel);
            }

            if (shader.Keywords.Count > 0)
            {
                GUILayout.Space(3);
                GUILayout.Label("Keywords:", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                var line = "";
                for (var i = 0; i < shader.Keywords.Count; i++)
                {
                    line += shader.Keywords[i];
                    if (i < shader.Keywords.Count - 1) line += ", ";
                    if (line.Length > 80 || i == shader.Keywords.Count - 1)
                    {
                        GUILayout.Label(line, EditorStyles.miniLabel);
                        line = "";
                    }
                }
                EditorGUI.indentLevel--;
            }

            if (shader.ReferencingMaterials.Count > 0)
            {
                GUILayout.Space(3);
                GUILayout.Label($"Referencing Materials ({shader.ReferencingMaterials.Count}):", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                foreach (var mat in shader.ReferencingMaterials.Take(10))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(16);
                    USGUIUtilities.DrawAssetButton(mat.Path, 250f, 16f);
                    GUILayout.Label(mat.IsUsingErrorShader ? "[ERROR]" : "", EditorStyles.miniLabel, GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();
                }
                if (shader.ReferencingMaterials.Count > 10)
                    GUILayout.Label($"... and {shader.ReferencingMaterials.Count - 10} more", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            if (shader.CustomWarnings != null && shader.CustomWarnings.Count > 0)
            {
                GUILayout.Space(3);
                var prev = GUI.color;
                GUI.color = Color.yellow;
                GUILayout.Label($"Warnings ({shader.CustomWarnings.Count}):", EditorStyles.boldLabel);
                GUI.color = prev;
                EditorGUI.indentLevel++;
                foreach (var w in shader.CustomWarnings)
                    USGUIUtilities.DrawColoredLabel("• " + w, Color.yellow);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void DrawMaterialList()
        {
            var materials = GetFilteredMaterials();

            for (var i = 0; i < materials.Count; i++)
            {
                if (!USGUIPaginationUtilities.ShouldDrawItem(i, _pagination))
                    continue;

                DrawMaterialRow(materials[i], i);
            }
        }

        private void DrawMaterialRow(MaterialData mat, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            var isExpanded = _expandedMaterial == index;
            var toggleChar = isExpanded ? "v" : ">";
            if (GUILayout.Button(new GUIContent(toggleChar, "Expand or collapse details"), EditorStyles.miniButton, GUILayout.Width(18)))
                _expandedMaterial = isExpanded ? -1 : index;

            var sevColor = mat.WarningLevel switch
            {
                >= 3 => Color.red,
                2 => Color.yellow,
                1 => Color.cyan,
                _ => Color.white
            };
            USGUIUtilities.DrawColoredLabel($"[{mat.WarningLevel}]", sevColor, 30);

            EditorGUILayout.LabelField(index.ToString(), GUILayout.Width(30));
            USGUIUtilities.DrawAssetButton(mat.Path, 250f, 18f);

            if (mat.Shader != null)
                GUILayout.Label(mat.Shader.Name, EditorStyles.miniLabel, GUILayout.Width(150));
            else
                GUILayout.Label("(unknown shader)", EditorStyles.miniLabel, GUILayout.Width(150));

            GUILayout.Label($"KWs:{mat.ShaderKeywords.Count}", EditorStyles.miniLabel, GUILayout.Width(50));

            EditorGUILayout.EndHorizontal();

            if (isExpanded)
                DrawMaterialDetail(mat);
        }

        private void DrawMaterialDetail(MaterialData mat)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label($"Material: {mat.Name}", EditorStyles.miniLabel);
            GUILayout.Label($"Path: {mat.Path}", EditorStyles.miniLabel);

            if (mat.Shader != null)
            {
                GUILayout.Label($"Shader: {mat.Shader.Name}", EditorStyles.miniLabel);
                GUILayout.Label($"Pipeline: {mat.Shader.RenderPipeline}", EditorStyles.miniLabel);
            }

            if (mat.IsUsingErrorShader)
                USGUIUtilities.DrawColoredLabel("USES ERROR SHADER", Color.red, 120);

            if (mat.ShaderKeywords.Count > 0)
            {
                GUILayout.Space(3);
                GUILayout.Label($"Enabled Keywords ({mat.ShaderKeywords.Count}):", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                GUILayout.Label(string.Join(", ", mat.ShaderKeywords), EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
            }

            if (mat.CustomWarnings != null && mat.CustomWarnings.Count > 0)
            {
                GUILayout.Space(3);
                var prev = GUI.color;
                GUI.color = Color.yellow;
                GUILayout.Label($"Warnings ({mat.CustomWarnings.Count}):", EditorStyles.boldLabel);
                GUI.color = prev;
                EditorGUI.indentLevel++;
                foreach (var w in mat.CustomWarnings)
                    USGUIUtilities.DrawColoredLabel("• " + w, Color.yellow);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        private void ExportToClipboard()
        {
            var sb = new StringBuilder();

            if (_subTab == 0)
            {
                var shaders = GetFilteredShaders();
                sb.AppendLine($"Shader Analysis [{shaders.Count} shaders]:");
                foreach (var s in shaders)
                    sb.AppendLine($"[{s.WarningLevel}] {s.Name} | V:{s.VariantCount} K:{s.KeywordCount} P:{s.PassCount} | {s.Path}");
            }
            else
            {
                var materials = GetFilteredMaterials();
                sb.AppendLine($"Shader Analysis [{materials.Count} materials]:");
                foreach (var m in materials)
                    sb.AppendLine($"[{m.WarningLevel}] {m.Name} | Shader:{m.Shader?.Name ?? "unknown"} | {m.Path}");
            }

            USExportUtilities.CopyToClipboard(sb);
            Debug.Log($"[US] Exported to clipboard.");
        }

        private void ExportToCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export Shader Analysis", Application.dataPath,
                "shader_analysis_export.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder();

            if (_subTab == 0)
            {
                sb.AppendLine("WarningLevel,Name,Path,Variants,Keywords,Passes,Pipeline,ErrorShader,Fallback,MaterialCount");
                foreach (var s in GetFilteredShaders())
                {
                    sb.AppendLine($"{s.WarningLevel},{USExportUtilities.EscapeCsvField(s.Name)}," +
                                  $"{USExportUtilities.EscapeCsvField(s.Path)},{s.VariantCount}," +
                                  $"{s.KeywordCount},{s.PassCount},{s.RenderPipeline}," +
                                  $"{s.IsErrorShader},{s.IsFallbackShader},{s.ReferencingMaterials.Count}");
                }
            }
            else
            {
                sb.AppendLine("WarningLevel,Name,Path,Shader,Keywords,ErrorShader");
                foreach (var m in GetFilteredMaterials())
                {
                    sb.AppendLine($"{m.WarningLevel},{USExportUtilities.EscapeCsvField(m.Name)}," +
                                  $"{USExportUtilities.EscapeCsvField(m.Path)}," +
                                  $"{USExportUtilities.EscapeCsvField(m.Shader?.Name ?? "unknown")}," +
                                  $"{m.ShaderKeywords.Count},{m.IsUsingErrorShader}");
                }
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(path);
        }

        private void InvalidateCache()
        {
            _cacheDirty = true;
        }

        private List<ShaderData> GetFilteredShaders()
        {
            if (!_cacheDirty && _cachedShaders != null)
                return _cachedShaders;

            var shaders = _category?.LastShaders;
            if (shaders == null)
            {
                _cachedShaders = new List<ShaderData>();
                _cacheDirty = false;
                return _cachedShaders;
            }

            var filtered = shaders.AsEnumerable();

            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(s => s.Path.IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               s.Name.IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!string.IsNullOrEmpty(_keywordFilter))
                filtered = filtered.Where(s => s.Keywords.Any(k => k.IndexOf(_keywordFilter, StringComparison.OrdinalIgnoreCase) >= 0));

            if (_warningsOnly)
                filtered = filtered.Where(s => s.WarningLevel > 0);

            var sorted = filtered.ToList();
            ApplyShaderSorting(sorted);

            _cachedShaders = sorted;
            _cacheDirty = false;
            return _cachedShaders;
        }

        private List<MaterialData> GetFilteredMaterials()
        {
            if (!_cacheDirty && _cachedMaterials != null)
                return _cachedMaterials;

            var materials = _category?.LastMaterials;
            if (materials == null)
            {
                _cachedMaterials = new List<MaterialData>();
                _cacheDirty = false;
                return _cachedMaterials;
            }

            var filtered = materials.AsEnumerable();

            if (!string.IsNullOrEmpty(_pathFilter))
                filtered = filtered.Where(m => m.Path.IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               m.Name.IndexOf(_pathFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!string.IsNullOrEmpty(_keywordFilter))
                filtered = filtered.Where(m => m.ShaderKeywords.Any(k => k.IndexOf(_keywordFilter, StringComparison.OrdinalIgnoreCase) >= 0));

            if (_warningsOnly)
                filtered = filtered.Where(m => m.WarningLevel > 0);

            var sorted = filtered.ToList();
            ApplyMaterialSorting(sorted);

            _cachedMaterials = sorted;
            _cacheDirty = false;
            return _cachedMaterials;
        }

        private void ApplyShaderSorting(List<ShaderData> shaders)
        {
            switch (_sortMode)
            {
                case 0:
                    shaders.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel));
                    break;
                case 1:
                    shaders.Sort((a, b) => a.WarningLevel.CompareTo(b.WarningLevel));
                    break;
                case 2:
                    shaders.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));
                    break;
                case 3:
                    shaders.Sort((a, b) => string.Compare(b.Path, a.Path, StringComparison.Ordinal));
                    break;
                case 4:
                    shaders.Sort((a, b) => b.VariantCount.CompareTo(a.VariantCount));
                    break;
            }
        }

        private void ApplyMaterialSorting(List<MaterialData> materials)
        {
            switch (_sortMode)
            {
                case 0:
                    materials.Sort((a, b) => b.WarningLevel.CompareTo(a.WarningLevel));
                    break;
                case 1:
                    materials.Sort((a, b) => a.WarningLevel.CompareTo(b.WarningLevel));
                    break;
                case 2:
                    materials.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));
                    break;
                case 3:
                    materials.Sort((a, b) => string.Compare(b.Path, a.Path, StringComparison.Ordinal));
                    break;
                case 4:
                    materials.Sort((a, b) =>
                    {
                        var ak = a.ShaderKeywords?.Count ?? 0;
                        var bk = b.ShaderKeywords?.Count ?? 0;
                        return bk.CompareTo(ak);
                    });
                    break;
            }
        }
    }
}
