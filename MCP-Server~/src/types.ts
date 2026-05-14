export interface MCPConfig {
  unityPath: string;
  projectPath: string;
  timeouts?: {
    default?: number;
    runAll?: number;
    runCategory?: number;
    runSelected?: number;
    listCategories?: number;
    getSettings?: number;
    setProfile?: number;
    regressionCheck?: number;
    baselineCreate?: number;
  };
}

export interface ScanResult {
  success: boolean;
  exitCode: number;
  message: string;
  totalDurationMs: number;
  summary: {
    totalIssues: number;
    errors: number;
    warnings: number;
    info: number;
    categoriesScanned: number;
  };
  categories: CategoryResult[];
  textSummary: string;
}

export interface CategoryResult {
  id: string;
  name: string;
  issueCount: number;
  durationMs: number;
  succeeded: boolean;
  error?: string;
  issues: IssueResult[];
}

export interface IssueResult {
  severity: string;
  code: string;
  assetPath: string;
  description: string;
}

export interface CategoryInfo {
  id: string;
  name: string;
  enabled: boolean;
}

export interface CategoryListResult {
  categories: CategoryInfo[];
}

export interface SettingsResult {
  activePlatformProfile: string;
  profiles: string[];
  categories: { id: string; enabled: boolean }[];
}

export interface BaselineResult {
  success: boolean;
  baselinePath: string;
  totalIssues: number;
  categories: number;
}

export interface RegressionResult {
  success: boolean;
  exitCode: number;
  totalIssues: number;
  textSummary: string;
}
