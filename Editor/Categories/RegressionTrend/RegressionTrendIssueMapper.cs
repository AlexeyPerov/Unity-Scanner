using System;
using System.Collections.Generic;
using UnityScanner.Core.Issues;

namespace UnityScanner.Categories.RegressionTrend
{
    public static class RegressionTrendIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(List<CategoryComparison> comparisons, RegressionTrendSettings settings)
        {
            var issues = new List<UnityScannerIssue>();

            foreach (var comp in comparisons)
            {
                if (comp.HasRegression)
                {
                    if (comp.ErrorDelta > 0 && comp.ErrorDelta >= settings.RegressionErrorThreshold)
                    {
                        issues.Add(new UnityScannerIssue
                        {
                            CategoryId = "regression_trend",
                            IssueCode = "error_regression",
                            Description = $"{comp.DisplayName}: errors increased by {comp.ErrorDelta} (was {comp.BaselineErrors}, now {comp.CurrentErrors}).",
                            Severity = UnityScannerIssueSeverity.Warning,
                            Metadata = new Dictionary<string, object>
                            {
                                { "categoryId", comp.CategoryId },
                                { "metric", "errors" },
                                { "baseline", comp.BaselineErrors.ToString() },
                                { "current", comp.CurrentErrors.ToString() },
                                { "delta", comp.ErrorDelta.ToString() }
                            }
                        });
                    }

                    if (comp.WarningDelta > 0 && comp.WarningDelta >= settings.RegressionWarningThreshold)
                    {
                        issues.Add(new UnityScannerIssue
                        {
                            CategoryId = "regression_trend",
                            IssueCode = "warning_regression",
                            Description = $"{comp.DisplayName}: warnings increased by {comp.WarningDelta} (was {comp.BaselineWarnings}, now {comp.CurrentWarnings}).",
                            Severity = UnityScannerIssueSeverity.Warning,
                            Metadata = new Dictionary<string, object>
                            {
                                { "categoryId", comp.CategoryId },
                                { "metric", "warnings" },
                                { "baseline", comp.BaselineWarnings.ToString() },
                                { "current", comp.CurrentWarnings.ToString() },
                                { "delta", comp.WarningDelta.ToString() }
                            }
                        });
                    }
                }

                if (comp.IsNew && (comp.CurrentErrors > 0 || comp.CurrentWarnings > 0))
                {
                    issues.Add(new UnityScannerIssue
                    {
                        CategoryId = "regression_trend",
                        IssueCode = "new_category_issues",
                        Description = $"{comp.DisplayName}: {comp.CurrentErrors} errors, {comp.CurrentWarnings} warnings detected (no baseline data).",
                        Severity = UnityScannerIssueSeverity.Info,
                        Metadata = new Dictionary<string, object>
                        {
                            { "categoryId", comp.CategoryId },
                            { "errors", comp.CurrentErrors.ToString() },
                            { "warnings", comp.CurrentWarnings.ToString() }
                        }
                    });
                }

                if (comp.HasImprovement)
                {
                    var improvedErrors = comp.ErrorDelta < 0 ? $"errors decreased by {Math.Abs(comp.ErrorDelta)}" : "";
                    var improvedWarnings = comp.WarningDelta < 0 ? $"warnings decreased by {Math.Abs(comp.WarningDelta)}" : "";
                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(improvedErrors)) parts.Add(improvedErrors);
                    if (!string.IsNullOrEmpty(improvedWarnings)) parts.Add(improvedWarnings);

                    if (parts.Count > 0)
                    {
                        issues.Add(new UnityScannerIssue
                        {
                            CategoryId = "regression_trend",
                            IssueCode = "improvement",
                            Description = $"{comp.DisplayName}: {string.Join(", ", parts)}.",
                            Severity = UnityScannerIssueSeverity.Info,
                            Metadata = new Dictionary<string, object>
                            {
                                { "categoryId", comp.CategoryId },
                                { "errorDelta", comp.ErrorDelta.ToString() },
                                { "warningDelta", comp.WarningDelta.ToString() }
                            }
                        });
                    }
                }
            }

            return issues;
        }
    }
}
