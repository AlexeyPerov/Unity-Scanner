using System.Collections.Generic;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.MissingReferences
{
    public static class MissingReferencesIssueMapper
    {
        public const string CodeMissingFileIDAndGuid = "missing_fileid_and_guid";
        public const string CodeMissingGuid = "missing_guid";
        public const string CodeMissingFileID = "missing_fileid";
        public const string CodeMissingLocalFileID = "missing_local_fileid";
        public const string CodeEmptyLocalRef = "empty_local_ref";
        public const string CodeMissingMethod = "missing_method";
        public const string CodeTypeMismatch = "type_mismatch";
        public const string CodeMissingScript = "missing_script";
        public const string CodeDuplicateComponent = "duplicate_component";
        public const string CodeInvalidLayer = "invalid_layer";

        public static List<UnityScannerIssue> MapToIssues(List<MissingRefAssetData> assets)
        {
            var issues = new List<UnityScannerIssue>();

            foreach (var asset in assets)
            {
                var refs = asset.RefsData;

                foreach (var extRef in refs.ExternalReferences)
                {
                    if (extRef.FileIDValid && extRef.GuidValid && !extRef.FileIDExistsInAssets && !extRef.GuidExistsInAssets)
                    {
                        issues.Add(MakeIssue(asset, CodeMissingFileIDAndGuid,
                            $"Missing both FileID ({extRef.FileID}) and GUID ({extRef.Guid}) at line {extRef.Line}",
                            UnityScannerIssueSeverity.Error, extRef.Line));
                    }
                    else if (extRef.GuidValid && !extRef.GuidExistsInAssets)
                    {
                        issues.Add(MakeIssue(asset, CodeMissingGuid,
                            $"Missing GUID ({extRef.Guid}) at line {extRef.Line}",
                            UnityScannerIssueSeverity.Warning, extRef.Line));
                    }
                    else if (extRef.FileIDValid && !extRef.FileIDExistsInAssets)
                    {
                        issues.Add(MakeIssue(asset, CodeMissingFileID,
                            $"Missing FileID ({extRef.FileID}) at line {extRef.Line}",
                            UnityScannerIssueSeverity.Info, extRef.Line));
                    }
                }

                foreach (var localRef in refs.LocalReferences)
                {
                    if (localRef.IdValid && localRef.LocalUsagesCount == 0 && !localRef.ExistsInAssets)
                    {
                        issues.Add(MakeIssue(asset, CodeMissingLocalFileID,
                            $"Missing local FileID ({localRef.Id}) at line {localRef.Line}",
                            UnityScannerIssueSeverity.Verbose, localRef.Line));
                    }
                }

                foreach (var empty in refs.EmptyFileIDs)
                {
                    issues.Add(MakeIssue(asset, CodeEmptyLocalRef,
                        $"Empty local fileID reference at line {empty.Line}",
                        UnityScannerIssueSeverity.Verbose, empty.Line));
                }

                foreach (var method in refs.MissingMethods)
                {
                    issues.Add(MakeIssue(asset, CodeMissingMethod,
                        $"Missing method {method.MethodName} on {method.ClassName} at line {method.Line}",
                        UnityScannerIssueSeverity.Info, method.Line));
                }

                foreach (var mismatch in refs.TypeMismatches)
                {
                    issues.Add(MakeIssue(asset, CodeTypeMismatch,
                        $"Type mismatch: unresolvable type {mismatch.TypeName} at line {mismatch.Line}",
                        UnityScannerIssueSeverity.Info, mismatch.Line));
                }

                foreach (var script in refs.MissingScripts)
                {
                    issues.Add(MakeIssue(asset, CodeMissingScript,
                        $"Missing script GUID {script.ScriptGuid} at line {script.Line}",
                        UnityScannerIssueSeverity.Error, script.Line));
                }

                foreach (var dup in refs.DuplicateComponents)
                {
                    issues.Add(MakeIssue(asset, CodeDuplicateComponent,
                        $"Duplicate component {dup.ComponentType} ({dup.Count}x) on '{dup.GameObjectName}'",
                        UnityScannerIssueSeverity.Info));
                }

                foreach (var layer in refs.InvalidLayers)
                {
                    issues.Add(MakeIssue(asset, CodeInvalidLayer,
                        $"Invalid layer index {layer.LayerIndex} at line {layer.Line}",
                        UnityScannerIssueSeverity.Warning, layer.Line));
                }
            }

            return issues;
        }

        private static UnityScannerIssue MakeIssue(
            MissingRefAssetData asset, string code, string description,
            UnityScannerIssueSeverity severity, int? line = null)
        {
            var issue = new UnityScannerIssue
            {
                CategoryId = "missing_references",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = asset.Path,
                Guid = asset.Guid
            };

            if (line.HasValue)
                issue.Metadata["line"] = line.Value;

            return issue;
        }
    }
}
