import type { Tool } from "@modelcontextprotocol/sdk/types.js";

export const tools: Tool[] = [
  {
    name: "unity_scanner_run_all",
    description:
      "Run all enabled UnityScanner categories and return a full project health report. This is the most comprehensive scan.",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
  {
    name: "unity_scanner_run_category",
    description:
      "Run a specific UnityScanner category by ID. Use unity_scanner_list_categories to discover available IDs.",
    inputSchema: {
      type: "object",
      properties: {
        category: {
          type: "string",
          description:
            "Category ID to scan (e.g. 'dependencies', 'shader_analysis', 'project_health')",
        },
      },
      required: ["category"],
    },
  },
  {
    name: "unity_scanner_run_selected",
    description:
      "Run multiple selected UnityScanner categories by their IDs.",
    inputSchema: {
      type: "object",
      properties: {
        categories: {
          type: "array",
          items: { type: "string" },
          description:
            "Array of category IDs to scan (e.g. ['dependencies', 'materials', 'project_health'])",
        },
      },
      required: ["categories"],
    },
  },
  {
    name: "unity_scanner_list_categories",
    description:
      "List all available UnityScanner categories with their IDs and enabled status.",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
  {
    name: "unity_scanner_get_settings",
    description:
      "Get current UnityScanner settings including active platform profile and category enable/disable states.",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
  {
    name: "unity_scanner_set_profile",
    description:
      "Set the platform profile for threshold comparisons. Profiles adjust severity thresholds for mobile, console, or desktop targets.",
    inputSchema: {
      type: "object",
      properties: {
        profile: {
          type: "string",
          enum: ["mobile", "console", "desktop"],
          description: "Platform profile to activate",
        },
      },
      required: ["profile"],
    },
  },
  {
    name: "unity_scanner_regression_check",
    description:
      "Compare current scan results against a previously saved baseline to detect regressions (new issues).",
    inputSchema: {
      type: "object",
      properties: {
        baseline_path: {
          type: "string",
          description: "Path to the baseline JSON file to compare against",
        },
      },
      required: ["baseline_path"],
    },
  },
  {
    name: "unity_scanner_baseline_create",
    description:
      "Run a full scan and save results as a baseline snapshot for future regression comparisons.",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
];
