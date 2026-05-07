using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.Addressables
{
    public class AddressablesCategory : IUnityScannerCategory
    {
        public string Id => "addressables";
        public string DisplayName => "Build Layout";
        public string ShortDisplayName => DisplayName;
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.Export;

        private readonly USAddressablesSettings _settings = new();

        public AddressablesScanResult LastResult { get; private set; }
        public string LastBuildLayoutPath { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var buildLayoutPath = context?.Settings?.BuildLayoutPath;
            if (string.IsNullOrEmpty(buildLayoutPath))
                buildLayoutPath = _settings.BuildLayoutPath;

            if (string.IsNullOrEmpty(buildLayoutPath))
            {
                LastResult = null;
                issueSink.MarkSkipped("No Build Layout file loaded. Load a BuildLayout.txt to enable this analysis.");
                yield break;
            }

            LastBuildLayoutPath = buildLayoutPath;
            LastResult = AddressablesScanner.Scan(buildLayoutPath, _settings, issueSink);

            yield return null;

            if (LastResult != null)
            {
                var issues = AddressablesIssueMapper.MapIssues(LastResult);
                issueSink.AddRange(issues);
            }

            yield return null;
        }

        public void LoadBuildLayout(string path, IUnityScannerIssueSink issueSink)
        {
            _settings.BuildLayoutPath = path;
            LastBuildLayoutPath = path;
            LastResult = AddressablesScanner.Scan(path, _settings, issueSink);

            if (LastResult != null)
            {
                var issues = AddressablesIssueMapper.MapIssues(LastResult);
                issueSink.AddRange(issues);
            }
        }

        public void Reset()
        {
            LastResult = null;
            LastBuildLayoutPath = null;
            _settings.BuildLayoutPath = null;
        }
    }

    public class USAddressablesSettings : UnityScannerCategorySettings
    {
        public string BuildLayoutPath;
        public int MinWarningLevelToShow;
        public bool ShowRelatedBundlesSection;
        public long RemoteDependencyStartupWarningThresholdBytes = 3100000L;
        public bool MonochromeWarnings;

        public long GateMaxTotalSizeBytes;
        public long GateMaxDuplicateWastedBytes;
        public long GateMaxStartupRemoteDepsBytes;

        public List<string> RemoteBundlePatterns = new() { "remote" };
        public List<string> StartupBundlePatterns = new();

        public void SaveToSettings(USAddressablesSettingsData target)
        {
            target.MinWarningLevelToShow = MinWarningLevelToShow;
            target.ShowRelatedBundlesSection = ShowRelatedBundlesSection;
            target.RemoteDependencyStartupWarningThresholdBytes = RemoteDependencyStartupWarningThresholdBytes;
            target.MonochromeWarnings = MonochromeWarnings;
            target.GateMaxTotalSizeBytes = GateMaxTotalSizeBytes;
            target.GateMaxDuplicateWastedBytes = GateMaxDuplicateWastedBytes;
            target.GateMaxStartupRemoteDepsBytes = GateMaxStartupRemoteDepsBytes;
            target.RemoteBundlePatterns = RemoteBundlePatterns;
            target.StartupBundlePatterns = StartupBundlePatterns;
            target.Save();
            USAddressablesBundleUtilities.InvalidateConfigCache();
        }

        public void LoadFromSettings(USAddressablesSettingsData source)
        {
            MinWarningLevelToShow = source.MinWarningLevelToShow;
            ShowRelatedBundlesSection = source.ShowRelatedBundlesSection;
            RemoteDependencyStartupWarningThresholdBytes = source.RemoteDependencyStartupWarningThresholdBytes;
            MonochromeWarnings = source.MonochromeWarnings;
            GateMaxTotalSizeBytes = source.GateMaxTotalSizeBytes;
            GateMaxDuplicateWastedBytes = source.GateMaxDuplicateWastedBytes;
            GateMaxStartupRemoteDepsBytes = source.GateMaxStartupRemoteDepsBytes;
            RemoteBundlePatterns = source.RemoteBundlePatterns;
            StartupBundlePatterns = source.StartupBundlePatterns;
            USAddressablesBundleUtilities.InvalidateConfigCache();
        }
    }
}
