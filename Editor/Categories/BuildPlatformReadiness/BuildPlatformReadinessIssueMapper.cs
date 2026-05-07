using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEditor;

namespace UnityScanner.Categories.BuildPlatformReadiness
{
    public static class BuildPlatformReadinessIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(
            List<ImportPolicyViolation> violations,
            List<PlatformIncompatibility> incompatibilities,
            List<StrippingRisk> strippingRisks,
            List<StartupBudgetStatus> budgetStatuses,
            BuildPlatformReadinessSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();
            var profileName = profile?.DisplayName ?? "Unknown";

            if (settings.CheckImportPolicies)
            {
                foreach (var v in violations)
                {
                    var severity = UnityScannerIssueSeverity.Warning;
                    if (v.ViolationType == "compression_mismatch" || v.ViolationType == "audio_load_mismatch")
                        severity = UnityScannerIssueSeverity.Info;

                    issues.Add(MakeIssue("import_policy_violation",
                        "[" + profileName + "] " + v.Description,
                        severity, v.AssetPath));
                }
            }

            if (settings.CheckPlatformCompatibility)
            {
                foreach (var inc in incompatibilities)
                {
                    issues.Add(MakeIssue("platform_incompatibility",
                        "[" + profileName + "] " + inc.Description,
                        UnityScannerIssueSeverity.Warning, inc.AssetPath));
                }
            }

            if (settings.CheckStrippingRisk)
            {
                foreach (var risk in strippingRisks)
                {
                    issues.Add(MakeIssue("stripping_risk",
                        "Stripping risk in '" + risk.ScriptName + "': " + risk.Description,
                        UnityScannerIssueSeverity.Warning, risk.ScriptPath));
                }
            }

            if (settings.CheckStartupBudget)
            {
                foreach (var budget in budgetStatuses)
                {
                    if (budget.PercentUsed > 100f)
                    {
                        issues.Add(MakeIssue("startup_budget_exceeded",
                            "[" + profileName + "] " + budget.Category + " budget exceeded. " + budget.Description,
                            UnityScannerIssueSeverity.Error, ""));
                    }
                    else if (budget.PercentUsed > 80f)
                    {
                        issues.Add(MakeIssue("startup_budget_warning",
                            "[" + profileName + "] " + budget.Category + " budget near limit. " + budget.Description,
                            UnityScannerIssueSeverity.Warning, ""));
                    }
                }
            }

            if (settings.CheckProfileConformance && profile != null)
            {
                if (profile.Id == PlatformProfilePresets.Mobile)
                {
                    if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == ScriptingImplementation.IL2CPP)
                    {
                        issues.Add(MakeIssue("profile_conformance",
                            "[" + profileName + "] IL2CPP backend detected — verify managed stripping level is appropriate.",
                            UnityScannerIssueSeverity.Info, ""));
                    }
                }
            }

            return issues;
        }

        private static UnityScannerIssue MakeIssue(
            string code, string description, UnityScannerIssueSeverity severity, string assetPath)
        {
            return new UnityScannerIssue
            {
                CategoryId = "build_platform_readiness",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };
        }
    }
}
