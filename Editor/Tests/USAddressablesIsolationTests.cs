using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityScanner.Batch;
using UnityScanner.Categories.Addressables;

namespace UnityScanner.Tests
{
    public class USAddressablesIsolationTests
    {
        [Test]
        public void UnityScannerAsmdef_ContainsNoExternalAsmdefReferences()
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            Assert.IsNotNull(projectRoot);

            var asmdefPath = Path.Combine(projectRoot, "Assets", "Editor", "UnityScanner", "UnityScanner.asmdef");
            Assert.IsTrue(File.Exists(asmdefPath));

            var asmdefText = File.ReadAllText(asmdefPath);
            StringAssert.Contains("\"references\": []", asmdefText);
        }

        [Test]
        public void AddressablesIssueMapper_MapsRecommendationsDuplicatesAndFailedGates()
        {
            const string buildLayout = "Unity Version: 2022.3.0f1\n" +
                                       "com.unity.addressables: 1.21.19\n" +
                                       "Group Main (Total Size: 2B)\n" +
                                       "\tArchive remote_main (Size: 2B, Compression: LZ4, Asset Bundle Object Size: 0B)\n" +
                                       "\t\tBundle Dependencies:\n" +
                                       "\t\tExpanded Bundle Dependencies:\n" +
                                       "\t\tExplicit Assets\n" +
                                       "\t\t\tAssets/Test.prefab (Size: 2B, Addressable Name: test)\n";

            var parser = USAddressablesBuildLayoutParser.Parse("Task18Test", buildLayout);
            var layout = new USAddressablesBuildLayoutProvider(parser);
            var group = layout.Groups.First();
            var bundle = group.Archives.First();
            var asset = layout.AssetsByPath.Values.First();

            bundle.Recommendations.Add(new USAddressablesRecommendationMessage(4,
                $"Bundle {bundle.Name} of group {group.Name} has CIRCULAR dependencies with [dep]"));

            var result = new AddressablesScanResult
            {
                Layout = layout,
                Duplicates =
                {
                    new USAddressablesDuplicateEntry
                    {
                        AssetPath = asset.Name,
                        Asset = asset,
                        Bundles = { bundle, bundle },
                        WastedSize = 2,
                        Reason = DuplicateReason.DependencyPullIn,
                        SuggestedFix = "Use shared bundle."
                    }
                },
                GateResults =
                {
                    new GateResult
                    {
                        Name = "Size",
                        Pass = false,
                        FormattedActual = "2 B"
                    }
                }
            };

            var issues = AddressablesIssueMapper.MapIssues(result);
            Assert.IsTrue(issues.Any(issue => issue.IssueCode == AddressablesIssueMapper.CodeCircularDependency));
            Assert.IsTrue(issues.Any(issue => issue.IssueCode == AddressablesIssueMapper.CodeDuplicateAsset));
            Assert.IsTrue(issues.Any(issue => issue.IssueCode == AddressablesIssueMapper.CodeGateFailed));
        }

        [Test]
        public void BatchApi_DoesNotExposeLegacyTextureHunterBatchOperations()
        {
            var methodNames = typeof(UnityScannerBatch).GetMethods()
                .Select(method => method.Name.ToLowerInvariant())
                .ToList();

            Assert.IsFalse(methodNames.Any(name => name.Contains("texturehunter")));
        }
    }
}
