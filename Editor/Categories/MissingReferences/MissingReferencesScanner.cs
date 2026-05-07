using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityScanner.Core.Issues;
using UnityScanner.Utilities.AssetDatabase;
using UnityScanner.Utilities.Serialization;
using UnityScanner.Utilities.RegexPatterns;
using Object = UnityEngine.Object;

namespace UnityScanner.Categories.MissingReferences
{
    public static class MissingReferencesScanner
    {
        public static List<MissingRefAssetData> ScanAllAssets(
            MissingReferencesSettings settings,
            IUnityScannerIssueSink issueSink)
        {
            var result = new List<MissingRefAssetData>();
            var allGuids = new HashSet<string>();
            var allFileIDs = new HashSet<long>();

            var assetPaths = AssetDatabase.GetAllAssetPaths().ToList();
            HashSet<int> validLayers = null;
            if (settings.EnableInvalidLayerScan)
                validLayers = LoadValidLayers();

            var regexFileAndGuid = new Regex(@"fileID: \d+, guid: [a-f0-9]" + "{" + "32" + "}");
            var regexFileID = new Regex(@"{fileID: \d+}");
            var regexTypeStart = new Regex(@"^[a-zA-Z0-9_ ]+:");

            var totalAssets = assetPaths.Count;
            var progressInterval = Math.Max(1, totalAssets / 100);

            for (var assetIndex = 0; assetIndex < totalAssets; assetIndex++)
            {
                if (assetIndex % 20000 == 0)
                {
                    issueSink.ReportProgress((float)assetIndex / totalAssets, "Scanning for missing references");
                }

                var assetPath = assetPaths[assetIndex];
                var assetObject = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

                if (assetObject == null) continue;

                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(assetObject, out var guidStr, out long fileId))
                    continue;

                allGuids.Add(guidStr);
                allFileIDs.Add(fileId);

                if (!USPathFilterUtilities.IsIncludedInAnalysis(assetPath, DefaultIgnorePatterns)) continue;
                if (!IsValidType(assetPath, AssetDatabase.GetMainAssetTypeAtPath(assetPath))) continue;

                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (!CanAnalyzeType(type)) continue;

                var lines = USYamlUtilities.TryReadAllLines(assetPath);
                if (lines.Length == 0) continue;

                var refsData = new AssetReferencesData();
                var isScene = type == typeof(SceneAsset);

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];

                    if (USYamlUtilities.IsSystemReference(line, USYamlUtilities.KeyWordsToIgnore)) continue;
                    if (isScene && USYamlUtilities.IsSystemReference(line, USYamlUtilities.KeyWordsToIgnoreInSceneAsset)) continue;

                    if (line.Contains("guid:"))
                    {
                        var matches = regexFileAndGuid.Matches(line);
                        foreach (Match match in matches)
                        {
                            var matchText = match.Value;
                            var lastSpace = matchText.LastIndexOf(' ');
                            var externalGuid = matchText.Substring(lastSpace + 1);

                            var colonIdx = matchText.IndexOf(':');
                            var localFileIdStr = matchText.Substring(colonIdx + 1, lastSpace - colonIdx - 1).Trim();
                            long.TryParse(localFileIdStr, out var localFileID);

                            var guidValid = !externalGuid.StartsWith("0000000000");
                            var localIdValid = localFileID > 0;

                            if (!guidValid && !localIdValid) continue;

                            var referenceData = new ExternalReferenceRegistry(localIdValid, guidValid, localFileID, externalGuid, i);

                            if (guidValid)
                            {
                                var guidPath = AssetDatabase.GUIDToAssetPath(externalGuid);
                                referenceData.GuidExistsInAssets = allGuids.Contains(externalGuid) || !string.IsNullOrEmpty(guidPath);

                                if (!referenceData.GuidExistsInAssets)
                                    RecordGuidPlaceData(i, lines, referenceData);
                                else
                                    referenceData.Sample.Add(line);
                            }
                            else
                            {
                                referenceData.Sample.Add(line);
                            }

                            FindFieldType(regexTypeStart, i, lines, referenceData);
                            refsData.ExternalReferences.Add(referenceData);
                        }
                    }
                    else if (line.Contains("fileID:"))
                    {
                        var localMatches = regexFileID.Matches(line);
                        foreach (Match match in localMatches)
                        {
                            var idStr = match.Value;
                            var digitsOnly = idStr.Replace("{fileID: ", "").Replace("}", "").Trim();

                            if (digitsOnly == "0")
                            {
                                refsData.EmptyFileIDs.Add(new EmptyLocalFileIDRegistry(i));
                            }
                            else if (long.TryParse(digitsOnly, out var localId))
                            {
                                refsData.LocalReferences.Add(new LocalReferenceRegistry(localId, i));
                            }
                        }
                    }
                }

                foreach (var registry in refsData.LocalReferences)
                {
                    var usages = 0;
                    for (var j = 0; j < lines.Length; j++)
                    {
                        if (j == registry.Line) continue;
                        if (lines[j].Contains(registry.IdStr)) usages++;
                    }
                    registry.LocalUsagesCount = usages;
                }

                if (settings.EnableMissingMethodScan || settings.EnableTypeMismatchScan)
                    ScanUnityEventReferences(lines, refsData, settings.EnableMissingMethodScan, settings.EnableTypeMismatchScan);

                if (settings.EnableMissingScriptScan)
                    ScanMissingScripts(lines, refsData);

                if (settings.EnableInvalidLayerScan && validLayers != null)
                    ScanInvalidLayers(lines, refsData, validLayers);

                if (settings.EnableDuplicateComponentScan && type == typeof(GameObject))
                    ScanDuplicateComponents(assetPath, refsData);

                var typeName = USAssetTypeUtilities.GetReadableTypeName(type);
                result.Add(new MissingRefAssetData(assetPath, type, typeName, guidStr, refsData));
            }

            ResolveReferences(result, allGuids, allFileIDs);
            return result;
        }

        private static void ResolveReferences(List<MissingRefAssetData> assets, HashSet<string> allGuids, HashSet<long> allFileIDs)
        {
            foreach (var asset in assets)
            {
                foreach (var registry in asset.RefsData.LocalReferences)
                    registry.ExistsInAssets = allFileIDs.Contains(registry.Id);

                foreach (var registry in asset.RefsData.ExternalReferences)
                {
                    if (registry.FileIDValid)
                    {
                        registry.FileIDExistsInAssets = allFileIDs.Contains(registry.FileID) ||
                            asset.RefsData.LocalReferences.Any(l => l.Id == registry.FileID);
                    }
                }

                asset.RefsData.CalculateCounters();

                foreach (var extRef in asset.RefsData.ExternalReferences)
                {
                    if (!string.IsNullOrEmpty(extRef.FieldType) && extRef.WarningLevel > 0)
                        asset.MissingFieldTypes.Add(extRef.FieldType);
                }
            }
        }

        private static void FindFieldType(Regex regexTypeStart, int index, string[] lines, ExternalReferenceRegistry referenceData)
        {
            for (var j = index - 1; j >= 0; j--)
            {
                var line = lines[j];
                if (line.StartsWith("  ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal)) continue;
                var match = regexTypeStart.Match(line);
                if (match.Success)
                {
                    referenceData.FieldType = line.Trim();
                    return;
                }
            }
        }

        private static void RecordGuidPlaceData(int index, string[] lines, ExternalReferenceRegistry referenceData)
        {
            for (var j = index - 1; j >= 0; j--)
            {
                var line = lines[j];
                if (line.Contains("m_Name:") || line.Contains("m_TagString:"))
                    continue;

                if (line.StartsWith("---", StringComparison.Ordinal) || line.StartsWith("  ", StringComparison.Ordinal))
                    continue;

                referenceData.HolderName = line.Trim().TrimEnd(':');
                break;
            }

            for (var j = Math.Max(0, index - 1); j <= Math.Min(lines.Length - 1, index + 2); j++)
                referenceData.Sample.Add(lines[j]);
        }

        private static void ScanUnityEventReferences(string[] lines, AssetReferencesData refsData, bool checkMissingMethods, bool checkTypeMismatches)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!line.Contains("m_TargetAssemblyTypeName:")) continue;

                var typeNameMatch = USSharedRegex.UnityEventTargetType.Match(line);
                if (!typeNameMatch.Success) continue;

                var typeName = typeNameMatch.Groups[1].Value;

                string methodName = null;
                for (var j = i + 1; j < Math.Min(lines.Length, i + 10); j++)
                {
                    var methodMatch = USSharedRegex.UnityEventMethodName.Match(lines[j]);
                    if (methodMatch.Success)
                    {
                        methodName = methodMatch.Groups[1].Value;
                        break;
                    }
                }

                if (checkMissingMethods && !string.IsNullOrEmpty(methodName))
                {
                    var resolvedType = ResolveType(typeName);
                    if (resolvedType != null)
                    {
                        var method = resolvedType.GetMethod(methodName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        if (method == null)
                            refsData.MissingMethods.Add(new MissingMethodEntry(typeName, methodName, i));
                    }
                }

                if (checkTypeMismatches)
                {
                    for (var j = i + 1; j < Math.Min(lines.Length, i + 15); j++)
                    {
                        var argTypeMatch = USSharedRegex.UnityEventArgType.Match(lines[j]);
                        if (!argTypeMatch.Success) continue;

                        var argTypeName = argTypeMatch.Groups[1].Value;
                        var resolvedArgType = ResolveType(argTypeName);
                        if (resolvedArgType == null)
                            refsData.TypeMismatches.Add(new TypeMismatchEntry(argTypeName, j));

                        break;
                    }
                }
            }
        }

        private static Type ResolveType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName, false);
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }

        private static void ScanMissingScripts(string[] lines, AssetReferencesData refsData)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!line.Contains("m_Script:")) continue;

                var match = USSharedRegex.ScriptGuid.Match(line);
                if (!match.Success) continue;

                var guid = match.Groups[1].Value;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    refsData.MissingScripts.Add(new MissingScriptEntry(guid, i));
            }
        }

        private static void ScanInvalidLayers(string[] lines, AssetReferencesData refsData, HashSet<int> validLayers)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var match = USSharedRegex.LayerIndex.Match(lines[i]);
                if (!match.Success) continue;

                var layerIndex = int.Parse(match.Groups[1].Value);
                if (!validLayers.Contains(layerIndex))
                    refsData.InvalidLayers.Add(new InvalidLayerEntry(layerIndex, i));
            }
        }

        private static void ScanDuplicateComponents(string assetPath, AssetReferencesData refsData)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (go == null) return;

            var transforms = go.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                var components = t.GetComponents<Component>();
                var typeCounts = new Dictionary<Type, int>();

                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    var compType = comp.GetType();
                    typeCounts.TryGetValue(compType, out var count);
                    typeCounts[compType] = count + 1;
                }

                foreach (var kvp in typeCounts)
                {
                    if (kvp.Value > 1)
                        refsData.DuplicateComponents.Add(new DuplicateComponentEntry(
                            kvp.Key.Name, kvp.Value, t.gameObject.name));
                }
            }
        }

        private static HashSet<int> LoadValidLayers()
        {
            var validLayers = new HashSet<int>();
            for (var i = 0; i < 32; i++)
            {
                if (!string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                    validLayers.Add(i);
            }
            return validLayers;
        }

        private static bool IsValidType(string path, Type type)
        {
            if (type != null) return type != typeof(DefaultAsset);
            Debug.LogWarning($"Invalid asset type found at {path}");
            return false;
        }

        private static bool CanAnalyzeType(Type type)
        {
            if (type == null) return false;
            return type == typeof(GameObject) || type == typeof(SceneAsset)
                || DerivesFromOrEqual(type, typeof(ScriptableObject));
        }

        private static bool DerivesFromOrEqual(Type a, Type b)
        {
            return b == a || b.IsAssignableFrom(a);
        }

        private static readonly List<string> DefaultIgnorePatterns = new List<string>
        {
            @"ProjectSettings/",
            @"Packages/",
            @"\.asmdef$",
            @"link\.xml$",
            @"\.csv$",
            @"\.md$",
            @"\.json$",
            @"\.xml$",
            @"\.txt$"
        };
    }
}
