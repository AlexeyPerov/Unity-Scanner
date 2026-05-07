using System;
using System.Collections.Generic;
using System.IO;

namespace UnityScanner.Utilities.BuildLayout
{
    public class USLiteBuildLayoutProvider
    {
        private readonly Dictionary<string, string> _assetPathToBundle = new(StringComparer.OrdinalIgnoreCase);

        private USLiteBuildLayoutProvider()
        {
        }

        public static USLiteBuildLayoutProvider Load(string path)
        {
            var text = File.ReadAllText(path);
            var provider = new USLiteBuildLayoutProvider();

            var lines = new List<string>();
            foreach (var line in text.Split('\n'))
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);

            for (var n = 0; n < lines.Count; ++n)
            {
                var line = lines[n];

                if (line.StartsWith("BuiltIn Bundles", StringComparison.Ordinal))
                {
                    ReadBundles(ref n, provider, lines);
                    continue;
                }

                if (line.StartsWith("Group ", StringComparison.Ordinal))
                    ReadBundles(ref n, provider, lines);
            }

            return provider;
        }

        public string GetBundleNameByAssetPath(string assetPath)
        {
            return _assetPathToBundle.TryGetValue(assetPath, out var bundleName) ? bundleName : string.Empty;
        }

        private static void ReadBundles(ref int index, USLiteBuildLayoutProvider provider, List<string> lines)
        {
            var containerIndent = GetIndent(lines[index]);
            index++;

            for (; index < lines.Count; ++index)
            {
                var l = lines[index];
                var lineIndent = GetIndent(l);
                if (lineIndent <= containerIndent)
                {
                    index--;
                    return;
                }

                if (!l.StartsWith("\tArchive")) continue;

                var bundleName = ExtractName(l);
                var bundleIndent = lineIndent;

                for (index++; index < lines.Count; ++index)
                {
                    if (GetIndent(lines[index]) <= bundleIndent)
                        break;

                    var trimmed = lines[index].TrimStart();
                    if (trimmed.StartsWith("Explicit Assets", StringComparison.OrdinalIgnoreCase))
                        ReadExplicitAssets(ref index, provider, bundleName, lines);
                }

                index--;
            }
        }

        private static void ReadExplicitAssets(ref int index, USLiteBuildLayoutProvider provider,
            string bundleName, List<string> lines)
        {
            var assetsIndent = GetIndent(lines[index]);
            index++;

            for (; index < lines.Count; ++index)
            {
                var l = lines[index];
                var lineIndent = GetIndent(l);
                if (lineIndent <= assetsIndent)
                {
                    index--;
                    return;
                }

                var assetName = ExtractName(l);
                if (!string.IsNullOrEmpty(assetName))
                    provider._assetPathToBundle[assetName] = bundleName;

                var assetIndent = lineIndent;
                for (index++; index < lines.Count; ++index)
                    if (GetIndent(lines[index]) <= assetIndent)
                        break;

                index--;
            }
        }

        private static string ExtractName(string line)
        {
            var lastParen = line.LastIndexOf(')');
            var firstParen = lastParen;

            var count = 1;
            for (var n = lastParen - 1; n >= 0; --n)
            {
                if (line[n] == ')') count++;
                if (line[n] == '(') count--;
                if (count == 0)
                {
                    firstParen = n;
                    break;
                }
            }

            if (firstParen != lastParen && firstParen > 0)
                line = line.Substring(0, firstParen);

            return line.Trim();
        }

        private static int GetIndent(string s)
        {
            var count = 0;
            foreach (var t in s)
                if (t == '\t') count++;
                else break;

            return count;
        }
    }
}
