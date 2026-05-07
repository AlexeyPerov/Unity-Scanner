using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Categories;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;

namespace UnityScanner.Categories.ParticleSystemAnalysis
{
    public class ParticleSystemAnalysisCategory : IUnityScannerCategory
    {
        public string Id => "particle_analysis";
        public string DisplayName => "Particle System Analysis";
        public string ShortDisplayName => "Particles";
        public UnityScannerCategorySettings Settings => _settings;
        public ScanCapabilities Capabilities => ScanCapabilities.ScanAll |
                                                ScanCapabilities.Export |
                                                ScanCapabilities.Progress;

        private readonly ParticleSystemAnalysisSettings _settings = new ParticleSystemAnalysisSettings();

        public List<ParticleSystemData> LastResults { get; private set; }
        public string OutputDescription { get; private set; }

        public IEnumerator Scan(UnityScannerScanContext context, IUnityScannerIssueSink issueSink)
        {
            var profile = context?.Settings?.ActivePlatformProfile;

            issueSink.ReportProgress(0f, "Scanning particle systems...");
            yield return null;

            var results = new List<ParticleSystemData>();

            ParticleSystemAnalysisScanner.ScanAll(_settings, profile, results, issueSink);

            foreach (var data in results)
            {
                if (data.SubEmitterCount > 0)
                {
                    var ps = FindParticleSystem(data);
                    if (ps != null)
                        data.SubEmitterChainDepth = ParticleSystemAnalysisScanner.ComputeSubEmitterChainDepth(ps);
                }
            }

            issueSink.ReportProgress(0.95f, "Mapping issues...");
            yield return null;

            var issues = ParticleSystemAnalysisIssueMapper.MapIssues(results, _settings, profile);
            issueSink.AddRange(issues);

            LastResults = results;

            var total = results.Count;
            var errors = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Error);
            var warns = issues.Count(i => i.Severity == UnityScannerIssueSeverity.Warning);

            OutputDescription = "Particle systems: " + total + ". Issues: " + (errors + warns) + " (" + errors + " errors, " + warns + " warnings).";

            issueSink.ReportProgress(1f, "Done");
            yield return null;
        }

        private static UnityEngine.ParticleSystem FindParticleSystem(ParticleSystemData data)
        {
            if (string.IsNullOrEmpty(data.AssetPath)) return null;

            var go = UnityEditor.AssetDatabase.LoadMainAssetAtPath(data.AssetPath) as UnityEngine.GameObject;
            if (go == null) return null;

            return go.GetComponentInChildren<UnityEngine.ParticleSystem>();
        }
    }
}
