using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace UnityScanner.Categories.Addressables
{
    [Serializable]
    public class USAddressablesSettingsData
    {
        private const string SettingsFileName = "UnityScannerAddressablesSettings.json";
        private static USAddressablesSettingsData _cached;

        public int MinWarningLevelToShow;
        public bool ShowRelatedBundlesSection;
        public long RemoteDependencyStartupWarningThresholdBytes = 3100000L;
        public bool MonochromeWarnings;

        public long GateMaxTotalSizeBytes;
        public long GateMaxDuplicateWastedBytes;
        public long GateMaxStartupRemoteDepsBytes;

        public List<string> RemoteBundlePatterns = new() { "remote" };
        public List<string> StartupBundlePatterns = new();

        public static USAddressablesSettingsData Load()
        {
            if (_cached != null)
                return _cached;

            var settingsPath = GetSettingsFilePath();
            if (File.Exists(settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(settingsPath);
                    _cached = JsonUtility.FromJson<USAddressablesSettingsData>(json);
                    if (_cached != null)
                        return _cached;
                }
                catch
                {
                    // ignored - use defaults
                }
            }

            _cached = new USAddressablesSettingsData();
            return _cached;
        }

        public static USAddressablesSettingsData Reload()
        {
            _cached = null;
            return Load();
        }

        public void Save()
        {
            var json = JsonUtility.ToJson(this, true);
            var settingsPath = GetSettingsFilePath();
            var settingsDir = Path.GetDirectoryName(settingsPath);
            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);

            File.WriteAllText(settingsPath, json);
            _cached = this;
            AssetDatabase.Refresh();
        }

        private static string GetSettingsFilePath()
        {
            return Path.Combine("ProjectSettings", SettingsFileName);
        }
    }

    public static class USAddressablesBundleUtilities
    {
        private static USAddressablesSettingsData _config;
        private static List<string> _startupPatternsFormatted;
        private static int _startupPatternsHash;

        public static bool IsBundleRemote(string name)
        {
            var config = GetConfig();
            var lowered = name.ToLowerInvariant();
            foreach (var pattern in config.RemoteBundlePatterns)
            {
                if (lowered.Contains(pattern.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        public static bool IsBundleStartup(string name)
        {
            if (!IsBundleRemote(name))
                return false;

            var config = GetConfig();
            if (config.StartupBundlePatterns.Count == 0)
                return false;

            EnsureStartupPatternsFormatted(config);
            var formattedName = name.ToLowerInvariant().Replace(" ", string.Empty);
            return _startupPatternsFormatted.Any(pattern => formattedName.StartsWith(pattern));
        }

        public static void InvalidateConfigCache()
        {
            _config = null;
            _startupPatternsFormatted = null;
            _startupPatternsHash = 0;
        }

        private static USAddressablesSettingsData GetConfig()
        {
            if (_config == null)
                _config = USAddressablesSettingsData.Load();

            return _config;
        }

        private static void EnsureStartupPatternsFormatted(USAddressablesSettingsData config)
        {
            var hash = config.StartupBundlePatterns.GetHashCode();
            if (_startupPatternsFormatted != null && _startupPatternsHash == hash)
                return;

            _startupPatternsFormatted = new List<string>();
            foreach (var pattern in config.StartupBundlePatterns)
                _startupPatternsFormatted.Add(pattern.ToLowerInvariant());

            _startupPatternsHash = hash;
        }
    }

    public class USAddressablesRecommendationMessage
    {
        public USAddressablesRecommendationMessage(int warningLevel, string message)
        {
            WarningLevel = warningLevel;
            Message = message;
        }

        public int WarningLevel { get; }
        public string Message { get; }
    }

    public class USAddressablesRecommendation
    {
        public USAddressablesRecommendation(string target)
        {
            Target = target;
        }

        public string Target { get; }
        public List<USAddressablesRecommendationMessage> Messages { get; } = new();
        public int MaxWarningLevel { get; private set; }

        public USAddressablesRecommendationMessage AddMessage(int level, string message)
        {
            var existing = Messages.FirstOrDefault(item => item.Message == message);
            if (existing != null)
                return existing;

            var created = new USAddressablesRecommendationMessage(level, message);
            Messages.Add(created);
            MaxWarningLevel = Messages.Max(item => item.WarningLevel);
            return created;
        }
    }

    public class USAddressablesRecommendationsSummary
    {
        private List<USAddressablesRecommendation> Recommendations { get; } = new();

        public USAddressablesRecommendationMessage AddRecommendation(string target, string message, int level)
        {
            var recommendation = Recommendations.FirstOrDefault(item => item.Target == target);
            if (recommendation == null)
            {
                recommendation = new USAddressablesRecommendation(target);
                Recommendations.Add(recommendation);
            }

            return recommendation.AddMessage(level, message);
        }
    }

    [Serializable]
    public class USAddressablesBuildLayoutParser
    {
        public string Name { get; private set; }
        public string unityVersion;
        public string addressablesVersion;
        public List<Group> groups = new();
        public List<Archive> builtinBundles = new();

        public USAddressablesBuildLayoutParser(string name)
        {
            Name = name;
        }

        [Serializable]
        public class Group
        {
            public string name;
            public long size;
            public List<Archive> bundles = new();
        }

        [Serializable]
        public class Archive
        {
            public string name;
            public long size;
            public string compression;
            public long assetBundleObjectSize;
            public List<string> bundleDependencies = new();
            public List<string> expandedBundleDependencies = new();
            public List<ExplicitAsset> explicitAssets = new();
            public List<FileEntry> files = new();
        }

        [Serializable]
        public class ExplicitAsset
        {
            public string name;
            public long size;
            public long sizeFromObjects;
            public long sizeFromStreamedData;
            public string address;
            public List<string> externalReferences = new();
            public List<string> internalReferences = new();
            public List<string> labels = new();
        }

        [Serializable]
        public class FileEntry
        {
            public string name;
            public int monoScriptCount;
            public long monoScriptSize;
            public List<CabEntry> cabs = new();
            public List<ExplicitAsset> assets = new();
        }

        [Serializable]
        public class CabEntry
        {
            public string name;
            public long size;
        }

        public static USAddressablesBuildLayoutParser Load(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var text = File.ReadAllText(path);
            return Parse(name, text);
        }

        public static USAddressablesBuildLayoutParser Parse(string name, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException($"argument '{nameof(text)}' must not be empty.");

            var layout = new USAddressablesBuildLayoutParser(name);
            var lines = new List<string>();
            foreach (var line in text.Split(new[] { '\n' }))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }

            for (var index = 0; index < lines.Count; ++index)
            {
                var line = lines[index];
                if (line.StartsWith("Unity Version:", StringComparison.Ordinal))
                {
                    layout.unityVersion = line.Substring("Unity Version:".Length).Trim();
                    continue;
                }

                if (line.StartsWith("com.unity.addressables:", StringComparison.Ordinal))
                {
                    layout.addressablesVersion = line.Substring("com.unity.addressables:".Length).Trim();
                    continue;
                }

                if (line.StartsWith("BuiltIn Bundles", StringComparison.Ordinal))
                {
                    layout.builtinBundles.AddRange(ReadBuiltInBundles(ref index));
                    continue;
                }

                if (line.StartsWith("Group ", StringComparison.Ordinal))
                {
                    var group = ReadGroup(ref index);
                    layout.groups.Add(group);
                }
            }

            layout.groups.Sort((a, b) => a.name.CompareTo(b.name));
            return layout;

            Group ReadGroup(ref int index)
            {
                var group = new Group();
                var groupLine = lines[index];
                var groupIndent = GetIndent(groupLine);

                var groupName = groupLine.Substring(groupLine.IndexOf("Group ", StringComparison.Ordinal) + "Group ".Length);
                group.name = RemoveAttributes(groupName).Trim();

                foreach (var attribute in ReadAttributes(groupLine))
                {
                    if (attribute.Key == "Total Size")
                        group.size = ParseSize(attribute.Value);
                }

                var loopguard = 0;
                index++;
                for (; index < lines.Count; ++index)
                {
                loop:
                    if (++loopguard > 30000)
                        break;
                    if (lines.Count <= index)
                        break;

                    var currentLine = lines[index];
                    var lineIndent = GetIndent(currentLine);
                    if (lineIndent <= groupIndent)
                    {
                        index--;
                        return group;
                    }

                    if (currentLine.StartsWith("\tSchemas"))
                    {
                        SkipSchemas(ref index);
                        goto loop;
                    }

                    if (currentLine.StartsWith("\tArchive"))
                    {
                        var archive = ReadArchive(ref index);
                        group.bundles.Add(archive);
                        goto loop;
                    }
                }

                return group;
            }

            List<Archive> ReadBuiltInBundles(ref int index)
            {
                var result = new List<Archive>();
                var loopguard = 0;
                index++;
                for (; index < lines.Count; ++index)
                {
                loop:
                    if (++loopguard > 30000)
                        break;
                    if (lines.Count <= index)
                        break;

                    var line = lines[index];
                    var lineIndent = GetIndent(line);
                    if (lineIndent <= 0)
                    {
                        index--;
                        return result;
                    }

                    if (line.StartsWith("\tArchive"))
                    {
                        result.Add(ReadArchive(ref index));
                        goto loop;
                    }
                }

                return result;
            }

            void SkipSchemas(ref int index)
            {
                var schemasLevel = GetIndent(lines[index]);
                for (index++; index < lines.Count; ++index)
                {
                    if (GetIndent(lines[index]) <= schemasLevel)
                        break;
                }
            }

            Archive ReadArchive(ref int index)
            {
                var archive = new Archive();
                var archiveLine = lines[index];
                var archiveIndent = GetIndent(archiveLine);

                var archiveName = archiveLine.Substring(archiveLine.IndexOf("Archive", StringComparison.Ordinal) + "Archive".Length);
                archive.name = RemoveAttributes(archiveName).Trim();

                foreach (var attribute in ReadAttributes(archiveLine))
                {
                    if (attribute.Key == "Size")
                        archive.size = ParseSize(attribute.Value);
                    if (attribute.Key == "Compression")
                        archive.compression = attribute.Value;
                    if (attribute.Key == "Asset Bundle Object Size")
                        archive.assetBundleObjectSize = ParseSize(attribute.Value);
                }

                var loopguard = 0;
                for (index++; index < lines.Count - 1; ++index)
                {
                    if (++loopguard > 30000)
                        break;
                    if (GetIndent(lines[index]) <= archiveIndent)
                        break;

                    var trimmed = lines[index].Trim();
                    if (trimmed.StartsWith("Bundle Dependencies:", StringComparison.OrdinalIgnoreCase))
                    {
                        archive.bundleDependencies.AddRange(ReadCommaSeparatedStrings(ref index));
                        continue;
                    }

                    if (trimmed.StartsWith("Expanded Bundle Dependencies:", StringComparison.OrdinalIgnoreCase))
                    {
                        archive.expandedBundleDependencies.AddRange(ReadCommaSeparatedStrings(ref index));
                        continue;
                    }

                    if (trimmed.StartsWith("Explicit Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        archive.explicitAssets.AddRange(ReadExplicitAssets(ref index));
                        continue;
                    }

                    if (trimmed.StartsWith("Files:", StringComparison.OrdinalIgnoreCase))
                        archive.files.AddRange(ReadFiles(ref index));
                }

                return archive;
            }

            List<FileEntry> ReadFiles(ref int index)
            {
                var files = new List<FileEntry>();
                var filesIndent = GetIndent(lines[index]);
                index++;
                for (; index < lines.Count - 1; ++index)
                {
                    var line = lines[index];
                    var lineIndent = GetIndent(line);
                    if (lineIndent <= filesIndent)
                    {
                        index--;
                        break;
                    }

                    var file = ReadFileEntry(ref index);
                    if (file != null)
                        files.Add(file);
                }

                return files;
            }

            FileEntry ReadFileEntry(ref int index)
            {
                var file = new FileEntry();
                var fileLine = lines[index];
                var fileIndent = GetIndent(fileLine);
                index++;

                file.name = RemoveAttributes(fileLine).Trim();
                foreach (var attribute in ReadAttributes(fileLine))
                {
                    if (attribute.Key == "MonoScripts")
                        int.TryParse(attribute.Value, out file.monoScriptCount);
                    if (attribute.Key == "MonoScript Size")
                        file.monoScriptSize = ParseSize(attribute.Value);
                }

                for (; index < lines.Count - 1; ++index)
                {
                    var line = lines[index];
                    var lineIndent = GetIndent(line);
                    if (lineIndent <= fileIndent)
                    {
                        index--;
                        break;
                    }

                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("CAB-", StringComparison.Ordinal))
                    {
                        var cab = new CabEntry { name = RemoveAttributes(trimmedLine).Trim() };
                        foreach (var attribute in ReadAttributes(trimmedLine))
                        {
                            if (attribute.Key == "Size")
                                cab.size = ParseSize(attribute.Value);
                        }

                        file.cabs.Add(cab);
                        continue;
                    }

                    if (trimmedLine.StartsWith("Data From Other Assets"))
                    {
                        for (index++; index < lines.Count - 1; ++index)
                        {
                            if (GetIndent(lines[index]) <= lineIndent)
                            {
                                index--;
                                break;
                            }

                            var asset = ReadExplicitAsset(ref index);
                            if (asset != null)
                                file.assets.Add(asset);
                        }
                    }
                }

                return file;
            }

            List<ExplicitAsset> ReadExplicitAssets(ref int index)
            {
                var assets = new List<ExplicitAsset>();
                var explicitAssetsIndent = GetIndent(lines[index]);
                index++;
                for (; index < lines.Count - 1; ++index)
                {
                    var line = lines[index];
                    var lineIndent = GetIndent(line);
                    if (lineIndent <= explicitAssetsIndent)
                    {
                        index--;
                        break;
                    }

                    var asset = ReadExplicitAsset(ref index);
                    if (asset != null)
                        assets.Add(asset);
                }

                return assets;
            }

            ExplicitAsset ReadExplicitAsset(ref int index)
            {
                var result = new ExplicitAsset();
                var assetLine = lines[index++];
                var assetIndent = GetIndent(assetLine);
                result.name = RemoveAttributes(assetLine).Trim();

                foreach (var attribute in ReadAttributes(assetLine))
                {
                    if (attribute.Key == "Total Size" || attribute.Key == "Size")
                        result.size = ParseSize(attribute.Value);
                    if (attribute.Key == "Addressable Name")
                        result.address = attribute.Value;
                    if (attribute.Key == "Size from Objects")
                        result.sizeFromObjects = ParseSize(attribute.Value);
                    if (attribute.Key == "Size from Streamed Data")
                        result.sizeFromStreamedData = ParseSize(attribute.Value);
                    if (attribute.Key == "Labels" && !string.IsNullOrEmpty(attribute.Value))
                    {
                        foreach (var label in attribute.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var trimmed = label.Trim();
                            if (!string.IsNullOrEmpty(trimmed))
                                result.labels.Add(trimmed);
                        }
                    }
                }

                for (; index < lines.Count - 1; ++index)
                {
                    var line = lines[index];
                    var lineIndent = GetIndent(line);
                    if (lineIndent <= assetIndent)
                    {
                        index--;
                        return result;
                    }

                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("External References:", StringComparison.OrdinalIgnoreCase))
                        result.externalReferences.AddRange(ReadCommaSeparatedStrings(ref index));
                    if (trimmed.StartsWith("Internal References:", StringComparison.OrdinalIgnoreCase))
                        result.internalReferences.AddRange(ReadCommaSeparatedStrings(ref index));
                }

                index--;
                return result;
            }

            List<string> ReadCommaSeparatedStrings(ref int index)
            {
                var text = lines[index];
                var separatorIndex = text.IndexOf(':');
                if (separatorIndex == -1)
                    return new List<string>();

                text = text.Substring(separatorIndex + 1).Trim();
                var values = new List<string>();
                foreach (var entry in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    values.Add(entry.Trim());
                values.Sort();
                return values;
            }

            string RemoveAttributes(string line)
            {
                var last = line.LastIndexOf(')');
                var first = last;
                var count = 1;
                for (var position = last - 1; position >= 0; --position)
                {
                    if (line[position] == ')')
                        count++;
                    if (line[position] == '(')
                        count--;
                    if (count != 0)
                        continue;
                    first = position;
                    break;
                }

                if (first != last && first > 0)
                    line = line.Substring(0, first);

                return line;
            }

            Dictionary<string, string> ReadAttributes(string line)
            {
                var last = line.LastIndexOf(')');
                var first = last;
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var count = 1;

                for (var position = last - 1; position >= 0; --position)
                {
                    if (line[position] == ')')
                        count++;
                    if (line[position] == '(')
                        count--;
                    if (count != 0)
                        continue;
                    first = position + 1;
                    break;
                }

                if (first != last)
                {
                    line = line.Substring(first, last - first);
                    foreach (var entry in line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (string.IsNullOrEmpty(entry.Trim()))
                            continue;
                        var pair = entry.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        if (pair.Length == 2)
                            result[pair[0].Trim()] = pair[1].Trim();
                        else if (pair.Length >= 1)
                            result[pair[0].Trim()] = pair[0].Trim();
                    }
                }

                return result;
            }

            long ParseSize(string size)
            {
                if (size.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
                    return (long)(float.Parse(size[..^2], CultureInfo.InvariantCulture) * 1024 * 1024 * 1024);
                if (size.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
                    return (long)(float.Parse(size[..^2], CultureInfo.InvariantCulture) * 1024 * 1024);
                if (size.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
                    return (long)(float.Parse(size[..^2], CultureInfo.InvariantCulture) * 1024);
                if (size.EndsWith("B", StringComparison.OrdinalIgnoreCase))
                    return long.Parse(size[..^1], CultureInfo.InvariantCulture);
                return -1;
            }

            int GetIndent(string value)
            {
                var count = 0;
                for (var index = 0; index < value.Length; ++index)
                {
                    if (value[index] != '\t')
                        break;
                    count++;
                }

                return count;
            }
        }
    }

    public class USAddressablesBuildLayoutProvider
    {
        private readonly USAddressablesBuildLayoutParser _layoutParser;

        public List<Group> Groups = new();
        public readonly Dictionary<string, Archive> Bundles = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, Asset> AssetsByGuid = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, Asset> AssetsByPath = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, LabelInfo> Labels = new(StringComparer.OrdinalIgnoreCase);

        public long TotalSize { get; }
        public long TotalBuiltInSize { get; }
        public long TotalRemoteSize { get; }

        public string Name => _layoutParser.Name;

        public class Group
        {
            public string Name = string.Empty;
            public long Size;
            public readonly List<Archive> Archives = new();
            public int TopWarning;
        }

        public class Archive
        {
            public bool IsBuiltin;
            public string Name = string.Empty;
            public long Size;
            public string Compression = string.Empty;
            public long AssetBundleObjectSize;

            public readonly List<Archive> BundleDependencies = new();
            public readonly List<BundleDependencyInfo> BundleDependenciesInfos = new();
            public readonly List<Archive> ExpandedBundleDependencies = new();
            public readonly List<BundleExpandedDependencyInfo> ExpandedBundleDependenciesInfos = new();
            public List<Archive> AllBundleDependencies = new();

            public readonly List<Asset> ExplicitAssets = new();
            public List<Asset> AllAssets = new();

            public USAddressablesBuildLayoutParser.Archive BaseObject;
            public readonly List<Group> ReferencedByGroups = new();
            public readonly List<Archive> ReferencedByBundlesDirectly = new();
            public readonly List<Archive> ReferencedByBundlesExpanded = new();

            public HashSet<USAddressablesRecommendationMessage> Recommendations { get; } = new();
            public int TopWarning { get; private set; }

            public void TrySetWarningLevel(int level)
            {
                if (level > TopWarning)
                    TopWarning = level;
            }
        }

        public class Asset
        {
            public string Guid { get; set; }
            public string Name { get; set; }
            public long Size { get; set; }
            public long SizeFromObjects { get; set; }
            public long SizeFromStreamedData { get; set; }
            public string Address { get; set; }
            public List<Asset> ExternalReferences { get; } = new();
            public List<Asset> InternalReferences { get; } = new();
            public USAddressablesBuildLayoutParser.ExplicitAsset BaseObject { get; set; }
            public List<Archive> IncludedByBundle { get; } = new();
            public HashSet<Archive> UsedByBundles { get; } = new();
            public List<string> Labels { get; set; } = new();

            public bool IsEmbedded { get; set; }
            public Asset IncludedByAsset { get; set; }
            public Archive IncludedInBundle { get; set; }
            public int TopWarning { get; set; }
        }

        public class LabelInfo
        {
            public string Name;
            public readonly List<Asset> Assets = new();
            public readonly HashSet<Archive> Bundles = new();
            public long TotalSize;
        }

        public class BundleDependencyInfo
        {
            public Archive DependentBundle { get; set; }
            public Dictionary<Asset, List<Asset>> AssetsCrossReferences { get; } = new();
            public bool Foldout { get; set; }
        }

        public class BundleExpandedDependencyInfo
        {
            public Archive BundleFromExpandedDependencies { get; set; }
            public HashSet<Archive> BundlesFromDirectDependencies { get; } = new();
            public bool Foldout { get; set; }
        }

        public USAddressablesBuildLayoutProvider(USAddressablesBuildLayoutParser parser)
        {
            _layoutParser = parser;
            CollectBuiltInBundles(parser);
            CollectAllBundles(parser);
            ResolveBundleDependencies();
            CollectAllAssets();
            FillGroups(parser);
            CollectAllBundleDependenciesUnionLists();
            CollectDependencyReasons();
            FillUsedByBundlesField();
            BuildLabelIndex();

            foreach (var pair in Bundles)
            {
                TotalSize += pair.Value.Size;
                var name = pair.Value.Name.ToLowerInvariant();
                if (USAddressablesBundleUtilities.IsBundleRemote(name))
                    TotalRemoteSize += pair.Value.Size;
                else
                    TotalBuiltInSize += pair.Value.Size;
            }
        }

        public Asset FindAssetByPath(string path)
        {
            return AssetsByPath.GetValueOrDefault(path);
        }

        private Asset FindAssetByUid(string uid)
        {
            return AssetsByGuid.GetValueOrDefault(uid);
        }

        private Archive FindBundle(string bundleName)
        {
            return Bundles.GetValueOrDefault(bundleName);
        }

        private static string BuildUid(string bundleName, string assetName)
        {
            return $"{bundleName}###{assetName}";
        }

        private void BuildLabelIndex()
        {
            foreach (var asset in AssetsByPath.Values)
            {
                if (asset.Labels == null || asset.Labels.Count == 0)
                    continue;

                foreach (var label in asset.Labels)
                {
                    if (!Labels.TryGetValue(label, out var info))
                    {
                        info = new LabelInfo { Name = label };
                        Labels[label] = info;
                    }

                    info.Assets.Add(asset);
                    info.TotalSize += asset.Size;
                    if (asset.IncludedInBundle != null)
                        info.Bundles.Add(asset.IncludedInBundle);
                }
            }
        }

        private void FillUsedByBundlesField()
        {
            foreach (var bundle in Bundles.Values)
            {
                foreach (var asset in bundle.AllAssets)
                {
                    asset.UsedByBundles.Add(bundle);
                    foreach (var reference in asset.ExternalReferences)
                        reference.UsedByBundles.Add(bundle);
                }
            }
        }

        private void CollectAllBundleDependenciesUnionLists()
        {
            foreach (var group in Groups)
            {
                foreach (var archive in group.Archives)
                    archive.AllBundleDependencies = archive.BundleDependencies.Union(archive.ExpandedBundleDependencies).ToList();
            }
        }

        private void CollectDependencyReasons()
        {
            foreach (var group in Groups)
            {
                foreach (var archive in group.Archives)
                {
                    foreach (var bundleDependency in archive.BundleDependencies)
                    {
                        var info = new BundleDependencyInfo { DependentBundle = bundleDependency };
                        archive.BundleDependenciesInfos.Add(info);
                        foreach (var asset in archive.AllAssets)
                        {
                            var refs = asset.ExternalReferences.Where(reference => reference.IncludedInBundle == bundleDependency).ToList();
                            info.AssetsCrossReferences[asset] = refs;
                        }
                    }

                    foreach (var expandedDependency in archive.ExpandedBundleDependencies)
                    {
                        var info = new BundleExpandedDependencyInfo { BundleFromExpandedDependencies = expandedDependency };
                        archive.ExpandedBundleDependenciesInfos.Add(info);
                        foreach (var directDependency in archive.BundleDependencies)
                        {
                            if (directDependency.ExpandedBundleDependencies.Contains(expandedDependency) ||
                                directDependency.BundleDependencies.Contains(expandedDependency))
                            {
                                info.BundlesFromDirectDependencies.Add(directDependency);
                            }
                        }
                    }
                }
            }
        }

        private void CollectBuiltInBundles(USAddressablesBuildLayoutParser parser)
        {
            foreach (var baseBundle in parser.builtinBundles)
            {
                if (FindBundle(baseBundle.name) != null)
                    continue;
                var bundle = new Archive
                {
                    BaseObject = baseBundle,
                    Name = baseBundle.name,
                    Size = baseBundle.size,
                    Compression = baseBundle.compression.ToUpperInvariant(),
                    AssetBundleObjectSize = baseBundle.assetBundleObjectSize,
                    IsBuiltin = true
                };
                Bundles.Add(bundle.Name, bundle);
            }
        }

        private void CollectAllBundles(USAddressablesBuildLayoutParser parser)
        {
            foreach (var baseGroup in parser.groups)
            {
                foreach (var baseBundle in baseGroup.bundles)
                {
                    if (FindBundle(baseBundle.name) != null)
                        continue;
                    var bundle = new Archive
                    {
                        BaseObject = baseBundle,
                        Name = baseBundle.name,
                        Size = baseBundle.size,
                        Compression = baseBundle.compression.ToUpperInvariant(),
                        AssetBundleObjectSize = baseBundle.assetBundleObjectSize
                    };
                    Bundles.Add(bundle.Name, bundle);
                }
            }
        }

        private void ResolveBundleDependencies()
        {
            foreach (var bundle in Bundles.Values)
            {
                foreach (var dependentBundleName in bundle.BaseObject.bundleDependencies)
                {
                    var dependency = FindBundle(dependentBundleName);
                    if (dependency == null)
                    {
                        Debug.LogError($"Cannot resolve bundle dependency to '{dependentBundleName}' in bundle '{bundle.Name}'.");
                        continue;
                    }

                    bundle.BundleDependencies.Add(dependency);
                    if (!bundle.BaseObject.expandedBundleDependencies.Contains(dependentBundleName))
                        dependency.ReferencedByBundlesDirectly.Add(bundle);
                }

                foreach (var expandedDependencyName in bundle.BaseObject.expandedBundleDependencies)
                {
                    var expanded = FindBundle(expandedDependencyName);
                    if (expanded == null)
                    {
                        Debug.LogError($"Cannot resolve bundle dependency to '{expandedDependencyName}' in bundle '{bundle.Name}'.");
                        continue;
                    }

                    bundle.ExpandedBundleDependencies.Add(expanded);
                    if (!bundle.BaseObject.bundleDependencies.Contains(expandedDependencyName))
                        expanded.ReferencedByBundlesExpanded.Add(bundle);
                }
            }
        }

        private void CollectAllAssets()
        {
            foreach (var bundle in Bundles.Values)
            {
                foreach (var baseAsset in bundle.BaseObject.explicitAssets)
                {
                    var asset = FindAssetByPath(baseAsset.name);
                    if (asset == null)
                    {
                        asset = new Asset
                        {
                            BaseObject = baseAsset,
                            Guid = BuildUid(bundle.Name, baseAsset.name),
                            Name = baseAsset.name,
                            Size = baseAsset.size,
                            SizeFromObjects = baseAsset.sizeFromObjects,
                            SizeFromStreamedData = baseAsset.sizeFromStreamedData,
                            Address = baseAsset.address,
                            IncludedInBundle = bundle,
                            Labels = baseAsset.labels ?? new List<string>()
                        };
                        AssetsByGuid.Add(asset.Guid, asset);
                        AssetsByPath.Add(asset.Name, asset);
                    }

                    bundle.ExplicitAssets.Add(asset);
                    bundle.AllAssets.Add(asset);
                    asset.IncludedByBundle.Add(bundle);

                    foreach (var internalReferenceName in baseAsset.internalReferences)
                    {
                        USAddressablesBuildLayoutParser.ExplicitAsset internalBaseAsset = null;
                        foreach (var file in bundle.BaseObject.files)
                        {
                            foreach (var fileAsset in file.assets)
                            {
                                if (!string.Equals(fileAsset.name, internalReferenceName, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                internalBaseAsset = fileAsset;
                                break;
                            }
                        }

                        if (internalBaseAsset == null)
                        {
                            Debug.LogError($"Could not find '{internalReferenceName}'.");
                            continue;
                        }

                        var internalAsset = FindAssetByUid(BuildUid(bundle.Name, internalReferenceName));
                        if (internalAsset == null)
                        {
                            internalAsset = new Asset
                            {
                                BaseObject = internalBaseAsset,
                                Guid = BuildUid(bundle.Name, internalReferenceName),
                                Name = internalReferenceName,
                                Size = internalBaseAsset.size,
                                SizeFromObjects = internalBaseAsset.sizeFromObjects,
                                SizeFromStreamedData = internalBaseAsset.sizeFromStreamedData,
                                IsEmbedded = true,
                                IncludedByAsset = asset,
                                IncludedInBundle = bundle
                            };
                            internalAsset.IncludedByBundle.Add(bundle);
                            AssetsByGuid.Add(internalAsset.Guid, internalAsset);
                            if (!AssetsByPath.ContainsKey(internalAsset.Name))
                                AssetsByPath.Add(internalAsset.Name, internalAsset);
                            bundle.AllAssets.Add(internalAsset);
                        }

                        asset.InternalReferences.Add(internalAsset);
                    }
                }
            }

            foreach (var asset in AssetsByGuid.Values)
            {
                foreach (var baseReference in asset.BaseObject.externalReferences)
                {
                    var reference = FindAssetByPath(baseReference);
                    if (reference != null)
                        asset.ExternalReferences.Add(reference);
                }
            }
        }

        private void FillGroups(USAddressablesBuildLayoutParser parser)
        {
            foreach (var baseGroup in parser.groups)
            {
                var group = new Group
                {
                    Name = baseGroup.name,
                    Size = baseGroup.size
                };
                Groups.Add(group);
                foreach (var baseBundle in baseGroup.bundles)
                {
                    var bundle = FindBundle(baseBundle.name);
                    if (bundle == null)
                        continue;
                    bundle.ReferencedByGroups.Add(group);
                    group.Archives.Add(bundle);
                }
            }
        }
    }

    public static class USAddressablesDependencyFormatter
    {
        public static string GetDependencyTypeTag(string bundleName)
        {
            var isRemote = USAddressablesBundleUtilities.IsBundleRemote(bundleName);
            var type = isRemote ? "[remote]" : "[built-in]";
            if (USAddressablesBundleUtilities.IsBundleStartup(bundleName))
                type += "[startup]";
            return type;
        }

        public static string GetDependencySizeString(long size)
        {
            return "[" + EditorUtility.FormatBytes(size) + "]";
        }

        public static string GetDependencyCountString(USAddressablesBuildLayoutProvider.Archive archive)
        {
            return $"[DirDeps:{archive.BundleDependencies.Count} ExpDeps:{archive.ExpandedBundleDependencies.Count}]";
        }
    }

    public class USAddressablesBundleComparisonService
    {
        public USAddressablesBuildLayoutProvider OriginalLayout { get; private set; }
        public USAddressablesBuildLayoutProvider AlternativeLayout { get; private set; }
        public List<BundleComparisonEntry> ComparisonResult { get; private set; }

        public void SetOriginalLayout(USAddressablesBuildLayoutProvider layout)
        {
            OriginalLayout = layout;
            ComparisonResult = null;
        }

        public void SwapLayouts()
        {
            (AlternativeLayout, OriginalLayout) = (OriginalLayout, AlternativeLayout);
            ComparisonResult = null;
            PerformComparison();
        }

        public void LoadAlternativeBuildLayout(string path, long remoteDependencyThresholdBytes)
        {
            try
            {
                AlternativeLayout = new USAddressablesBuildLayoutProvider(USAddressablesBuildLayoutParser.Load(path));
                USAddressablesAnalysis.PerformGroupsAnalysis(AlternativeLayout, null, remoteDependencyThresholdBytes);
                PerformComparison();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Error",
                    $"UnityScanner cannot load BuildLayout file '{path}'.\n\nError:\n{exception.Message}",
                    "OK");
            }
        }

        private void PerformComparison()
        {
            if (OriginalLayout == null || AlternativeLayout == null)
                return;

            ComparisonResult = new List<BundleComparisonEntry>();
            var unchangedEntries = new List<BundleComparisonEntry>();

            foreach (var pair in OriginalLayout.Bundles)
            {
                if (!AlternativeLayout.Bundles.TryGetValue(pair.Key, out var alternativeArchive))
                    continue;
                var entry = new BundleComparisonEntry(pair.Value, alternativeArchive);
                if (entry.SizeDiffModule != 0)
                    ComparisonResult.Add(entry);
                else
                    unchangedEntries.Add(entry);
            }

            ComparisonResult = ComparisonResult.OrderByDescending(entry => entry.SizeDiffModule).ToList();
            ComparisonResult.AddRange(unchangedEntries);
        }

        public class BundleComparisonEntry
        {
            public BundleComparisonEntry(USAddressablesBuildLayoutProvider.Archive originalBundle, USAddressablesBuildLayoutProvider.Archive alternativeBundle)
            {
                OriginalBundle = originalBundle;
                AlternativeBundle = alternativeBundle;
                SizeDiffModule = (long)Mathf.Abs(AlternativeBundle.Size - OriginalBundle.Size);
                OriginalLarger = OriginalBundle.Size > AlternativeBundle.Size;
            }

            public USAddressablesBuildLayoutProvider.Archive OriginalBundle { get; }
            public USAddressablesBuildLayoutProvider.Archive AlternativeBundle { get; }
            public long SizeDiffModule { get; }
            public bool OriginalLarger { get; }
        }
    }
}
