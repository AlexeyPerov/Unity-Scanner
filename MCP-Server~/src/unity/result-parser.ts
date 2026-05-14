import type {
  ScanResult,
  CategoryListResult,
  SettingsResult,
  BaselineResult,
  RegressionResult,
} from "../types.js";

export interface ParsedScanResult {
  text: string;
  structured: ScanResult;
}

export function parseScanResult(raw: ScanResult): ParsedScanResult {
  return {
    text: raw.textSummary ?? formatScanText(raw),
    structured: raw,
  };
}

export function parseCategoryList(
  raw: CategoryListResult
): { text: string; structured: CategoryListResult } {
  const lines = raw.categories.map(
    (c) => `  ${c.id.padEnd(30)} ${c.enabled ? "enabled" : "disabled"}  ${c.name}`
  );
  return {
    text: `Available categories (${raw.categories.length}):\n${lines.join("\n")}`,
    structured: raw,
  };
}

export function parseSettings(
  raw: SettingsResult
): { text: string; structured: SettingsResult } {
  const lines = [
    `Active profile: ${raw.activePlatformProfile}`,
    `Available profiles: ${raw.profiles.join(", ")}`,
    "",
    "Category settings:",
    ...raw.categories.map(
      (c) =>
        `  ${c.id.padEnd(30)} ${c.enabled ? "enabled" : "disabled"}`
    ),
  ];
  return { text: lines.join("\n"), structured: raw };
}

export function parseProfileSet(
  profileId: string
): { text: string; structured: { success: boolean; profile: string } } {
  return {
    text: `Platform profile set to "${profileId}".`,
    structured: { success: true, profile: profileId },
  };
}

export function parseBaseline(
  raw: BaselineResult
): { text: string; structured: BaselineResult } {
  return {
    text: `Baseline created at: ${raw.baselinePath}\nTotal issues: ${raw.totalIssues}\nCategories scanned: ${raw.categories}`,
    structured: raw,
  };
}

export function parseRegression(
  raw: RegressionResult
): { text: string; structured: RegressionResult } {
  return {
    text: raw.textSummary ?? `Regression check complete. Issues: ${raw.totalIssues}`,
    structured: raw,
  };
}

export function parseError(raw: {
  success: boolean;
  error: string;
}): { text: string } {
  return { text: `Error: ${raw.error}` };
}

function formatScanText(raw: ScanResult): string {
  const s = raw.summary;
  const lines = [
    `Scan complete: ${s.totalIssues} issues (${s.errors} errors, ${s.warnings} warnings, ${s.info} info)`,
    `Duration: ${raw.totalDurationMs.toFixed(0)}ms`,
    `Categories scanned: ${s.categoriesScanned}`,
    "",
  ];

  for (const cat of raw.categories ?? []) {
    if (!cat.succeeded) {
      lines.push(`[${cat.name}] FAILED: ${cat.error}`);
      continue;
    }
    if (cat.issueCount === 0) continue;
    lines.push(`[${cat.name}] ${cat.issueCount} issues:`);
    for (const issue of cat.issues.slice(0, 5)) {
      lines.push(`  - ${issue.severity}: ${issue.description}`);
    }
    if (cat.issues.length > 5) {
      lines.push(`  ... and ${cat.issues.length - 5} more`);
    }
  }

  return lines.join("\n");
}
