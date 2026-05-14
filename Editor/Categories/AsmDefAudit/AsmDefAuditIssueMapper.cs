using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.AsmDefAudit
{
    public static class AsmDefAuditIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(
            List<AsmDefData> results,
            AsmDefAuditSettings settings)
        {
            var issues = new List<UnityScannerIssue>();
            var nameMap = BuildNameMap(results);

            if (settings.CheckDuplicateName)
            {
                foreach (var group in nameMap)
                {
                    if (group.Value.Count > 1)
                    {
                        var paths = string.Join(", ", group.Value.Select(d => d.AssemblyPath));
                        var msg = "Duplicate assembly name '" + group.Key + "' found in " + group.Value.Count + " files. Each assembly must have a unique name. Rename one of the assemblies or consolidate them.";
                        foreach (var d in group.Value)
                            d.AddError(msg);
                        issues.Add(MakeIssue("asmdef_duplicate_name",
                            "Duplicate assembly name '" + group.Key + "' in " + group.Value.Count + " files.",
                            UnityScannerIssueSeverity.Error, group.Value[0].AssemblyPath,
                            "AssemblyPaths", paths,
                            "DuplicateName", group.Key));
                    }
                }
            }

            foreach (var data in results)
            {
                if (settings.CheckCircularReferences)
                {
                    var cycle = FindCycle(data, results);
                    if (cycle != null)
                    {
                        data.AddError("Circular reference detected: " + cycle + ". Circular dependencies prevent the compiler from determining build order. Remove one of the references to break the cycle.");
                        issues.Add(MakeIssue("asmdef_circular_reference",
                            "Circular reference: " + cycle,
                            UnityScannerIssueSeverity.Error, data.AssemblyPath,
                            "CyclePath", cycle));
                    }
                }

                if (settings.CheckEditorInRuntime && !data.IsEditorOnly)
                {
                    var editorRefs = data.References.Where(r => r.Contains("UnityEditor") || r.Contains(".Editor")).ToList();
                    if (editorRefs.Count > 0 && !data.IncludePlatforms.Contains("Editor"))
                    {
                        foreach (var edRef in editorRefs)
                        {
                            data.AddWarning("Runtime assembly references editor assembly '" + edRef + "'. This will cause compilation errors in builds. Add 'Editor' to includePlatforms or move the reference to an editor-only assembly.");
                            issues.Add(MakeIssue("asmdef_editor_in_runtime",
                                "Runtime assembly '" + data.AssemblyName + "' references '" + edRef + "'.",
                                UnityScannerIssueSeverity.Warning, data.AssemblyPath,
                                "AssemblyName", data.AssemblyName,
                                "EditorReference", edRef));
                        }
                    }
                }

                if (settings.CheckAutoReferencedOrphan && !data.AutoReferenced)
                {
                    var isReferenced = results.Any(other =>
                        other.AssemblyName != data.AssemblyName &&
                        other.References.Contains(data.AssemblyName));
                    if (!isReferenced)
                    {
                        data.AddInfo("Assembly has autoReferenced=false but no other assembly references it. Either set autoReferenced=true or add an explicit reference from a dependent assembly.");
                        issues.Add(MakeIssue("asmdef_auto_referenced_orphan",
                            "Assembly '" + data.AssemblyName + "' has autoReferenced=false but is not referenced by any other assembly.",
                            UnityScannerIssueSeverity.Info, data.AssemblyPath,
                            "AssemblyName", data.AssemblyName));
                    }
                }

                if (settings.CheckPlatformFilterBroad && data.IncludePlatforms.Count == 0 && data.ExcludePlatforms.Count == 0 && data.AnyPlatform)
                {
                    data.AddInfo("Assembly compiles for all platforms (no platform filters). Consider restricting to relevant platforms to reduce build time and size. Use includePlatforms or excludePlatforms in the .asmdef file.");
                    issues.Add(MakeIssue("asmdef_platform_filter_broad",
                        "Assembly '" + data.AssemblyName + "' compiles for all platforms (no platform filters).",
                        UnityScannerIssueSeverity.Info, data.AssemblyPath,
                            "AssemblyName", data.AssemblyName));
                }

                if (settings.CheckPlatformFilterContradict && data.IncludePlatforms.Count > 0 && data.ExcludePlatforms.Count > 0)
                {
                    data.AddWarning("Assembly has both includePlatforms and excludePlatforms set simultaneously. This is contradictory — use only one. includePlatforms specifies which platforms to compile for; excludePlatforms specifies which to skip.");
                    issues.Add(MakeIssue("asmdef_platform_filter_contradict",
                        "Assembly '" + data.AssemblyName + "' has both includePlatforms and excludePlatforms set.",
                        UnityScannerIssueSeverity.Warning, data.AssemblyPath,
                        "AssemblyName", data.AssemblyName,
                        "IncludePlatforms", string.Join(", ", data.IncludePlatforms),
                        "ExcludePlatforms", string.Join(", ", data.ExcludePlatforms)));
                }

                if (settings.CheckVersionDefineInvalid)
                {
                    foreach (var vd in data.VersionDefines)
                    {
                        if (!string.IsNullOrEmpty(vd.Package) && vd.Package.StartsWith("com."))
                        {
                            data.AddInfo("Version define references package '" + vd.Package + "'. Ensure the package is installed and the version constraint is valid. Invalid version defines silently fail and the symbol will not be defined.");
                            issues.Add(MakeIssue("asmdef_version_define_invalid",
                                "Assembly '" + data.AssemblyName + "' references package '" + vd.Package + "' in version defines.",
                                UnityScannerIssueSeverity.Info, data.AssemblyPath,
                                "AssemblyName", data.AssemblyName,
                                "PackageName", vd.Package,
                                "Symbol", vd.Symbol));
                        }
                    }
                }
            }

            return issues;
        }

        private static Dictionary<string, List<AsmDefData>> BuildNameMap(List<AsmDefData> results)
        {
            var map = new Dictionary<string, List<AsmDefData>>();
            foreach (var data in results)
            {
                if (!map.ContainsKey(data.AssemblyName))
                    map[data.AssemblyName] = new List<AsmDefData>();
                map[data.AssemblyName].Add(data);
            }
            return map;
        }

        private static string FindCycle(AsmDefData start, List<AsmDefData> all)
        {
            var nameMap = all.ToDictionary(d => d.AssemblyName, d => d);
            var visited = new HashSet<string>();
            var path = new List<string>();

            if (DFS(start.AssemblyName, start.AssemblyName, nameMap, visited, path))
                return string.Join(" -> ", path) + " -> " + start.AssemblyName;

            return null;
        }

        private static bool DFS(string current, string start, Dictionary<string, AsmDefData> nameMap,
            HashSet<string> visited, List<string> path)
        {
            if (visited.Contains(current)) return false;
            visited.Add(current);
            path.Add(current);

            if (!nameMap.TryGetValue(current, out var data)) { path.RemoveAt(path.Count - 1); return false; }

            foreach (var ref_ in data.References)
            {
                var refName = ref_.Replace("GUID:", "").Trim();
                if (refName == start && path.Count > 1)
                    return true;
                if (DFS(refName, start, nameMap, visited, path))
                    return true;
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        private static UnityScannerIssue MakeIssue(
            string code, string description, UnityScannerIssueSeverity severity,
            string assetPath, params object[] metadataPairs)
        {
            var issue = new UnityScannerIssue
            {
                CategoryId = "asmdef_audit",
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
