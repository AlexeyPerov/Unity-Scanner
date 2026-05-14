using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Categories.AsmDefAudit
{
    public static class AsmDefAuditScanner
    {
        public static IEnumerator ScanAll(
            AsmDefAuditSettings settings,
            List<AsmDefData> results,
            IUnityScannerIssueSink issueSink,
            int yieldInterval)
        {
            issueSink.ReportProgress(0f, "Finding assembly definitions...");

            var asmdefPaths = FindAllAsmDefFiles(settings, issueSink);
            var total = asmdefPaths.Count;

            for (var i = 0; i < total; i++)
            {
                if (yieldInterval > 0 && i > 0 && i % yieldInterval == 0)
                {
                    System.GC.Collect();
                    yield return 0.05f;
                    System.GC.Collect();
                }

                if (i % 50 == 0)
                    issueSink.ReportProgress((float)i / total * 0.8f, "Parsing assembly definitions...");

                var path = asmdefPaths[i];
                var data = ParseAsmDef(path);
                if (data != null)
                    results.Add(data);
            }
        }

        private static List<string> FindAllAsmDefFiles(AsmDefAuditSettings settings, IUnityScannerIssueSink issueSink)
        {
            var results = new List<string>();
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(settings.PathFilter) &&
                    path.IndexOf(settings.PathFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (path.StartsWith("Packages/"))
                    continue;
                results.Add(path);
            }
            return results;
        }

        private static AsmDefData ParseAsmDef(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                var data = new AsmDefData { AssemblyPath = path };

                data.AssemblyName = ExtractStringField(json, "name");
                data.RootNamespace = ExtractStringField(json, "rootNamespace");
                data.AutoReferenced = ExtractBoolField(json, "autoReferenced", true);
                data.AnyPlatform = ExtractBoolField(json, "anyPlatform", true);

                data.References = ExtractStringArray(json, "references");
                data.IncludePlatforms = ExtractStringArray(json, "includePlatforms");
                data.ExcludePlatforms = ExtractStringArray(json, "excludePlatforms");

                data.IsEditorOnly = data.IncludePlatforms.Contains("Editor") ||
                    (data.IncludePlatforms.Count == 0 && data.ExcludePlatforms.Count > 0 && !data.AnyPlatform) ||
                    (!data.AnyPlatform && data.ExcludePlatforms.Count == 0 && data.IncludePlatforms.Count == 0);

                ParseVersionDefines(json, data);

                return data;
            }
            catch
            {
                return null;
            }
        }

        private static void ParseVersionDefines(string json, AsmDefData data)
        {
            var vdIndex = json.IndexOf("\"versionDefines\"");
            if (vdIndex < 0) return;

            var start = json.IndexOf('[', vdIndex);
            if (start < 0) return;

            var depth = 0;
            var end = start;
            for (var i = start; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0) { end = i; break; }
                }
            }

            var vdContent = json.Substring(start, end - start + 1);
            var entries = SplitObjects(vdContent);
            foreach (var entry in entries)
            {
                data.VersionDefines.Add(new VersionDefineData
                {
                    Package = ExtractStringField(entry, "name"),
                    Expression = ExtractStringField(entry, "expression"),
                    Symbol = ExtractStringField(entry, "define")
                });
            }
        }

        private static string ExtractStringField(string json, string fieldName)
        {
            var search = "\"" + fieldName + "\"";
            var idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return "";

            var colonIdx = json.IndexOf(':', idx + search.Length);
            if (colonIdx < 0) return "";

            var startQuote = json.IndexOf('"', colonIdx + 1);
            if (startQuote < 0) return "";

            var endQuote = json.IndexOf('"', startQuote + 1);
            if (endQuote < 0) return "";

            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }

        private static bool ExtractBoolField(string json, string fieldName, bool defaultVal)
        {
            var search = "\"" + fieldName + "\"";
            var idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return defaultVal;

            var colonIdx = json.IndexOf(':', idx + search.Length);
            if (colonIdx < 0) return defaultVal;

            var remainder = json.Substring(colonIdx + 1).TrimStart();
            if (remainder.StartsWith("true")) return true;
            if (remainder.StartsWith("false")) return false;
            return defaultVal;
        }

        private static List<string> ExtractStringArray(string json, string fieldName)
        {
            var result = new List<string>();
            var search = "\"" + fieldName + "\"";
            var idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return result;

            var start = json.IndexOf('[', idx + search.Length);
            if (start < 0) return result;

            var depth = 0;
            var end = start;
            for (var i = start; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0) { end = i; break; }
                }
            }

            var content = json.Substring(start + 1, end - start - 1);
            foreach (var part in content.Split(','))
            {
                var trimmed = part.Trim().Trim('"');
                if (!string.IsNullOrEmpty(trimmed))
                    result.Add(trimmed);
            }

            return result;
        }

        private static List<string> SplitObjects(string jsonArray)
        {
            var result = new List<string>();
            var depth = 0;
            var objStart = -1;

            for (var i = 0; i < jsonArray.Length; i++)
            {
                if (jsonArray[i] == '{')
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (jsonArray[i] == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        result.Add(jsonArray.Substring(objStart, i - objStart + 1));
                        objStart = -1;
                    }
                }
            }

            return result;
        }
    }
}
