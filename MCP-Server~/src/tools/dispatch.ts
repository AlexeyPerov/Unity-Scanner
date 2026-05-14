import type { MCPConfig } from "../types.js";
import { runAll } from "./run-all.js";
import { runCategory } from "./run-category.js";
import { runSelected } from "./run-selected.js";
import { listCategories } from "./list-categories.js";
import { getSettings } from "./get-settings.js";
import { setProfile } from "./set-profile.js";
import { regressionCheck } from "./regression-check.js";
import { baselineCreate } from "./baseline-create.js";

export async function dispatch(
  toolName: string,
  args: Record<string, unknown>,
  config: MCPConfig
): Promise<{ text: string; structured?: unknown }> {
  switch (toolName) {
    case "unity_scanner_run_all":
      return await runAll(config);

    case "unity_scanner_run_category": {
      const category = args.category as string;
      if (!category) throw new Error("Missing 'category' argument");
      return await runCategory(config, category);
    }

    case "unity_scanner_run_selected": {
      const categories = args.categories as string[];
      if (!categories || categories.length === 0)
        throw new Error("Missing 'categories' argument");
      return await runSelected(config, categories);
    }

    case "unity_scanner_list_categories":
      return await listCategories(config);

    case "unity_scanner_get_settings":
      return await getSettings(config);

    case "unity_scanner_set_profile": {
      const profile = args.profile as string;
      if (!profile) throw new Error("Missing 'profile' argument");
      return await setProfile(config, profile);
    }

    case "unity_scanner_regression_check": {
      const baselinePath = args.baseline_path as string;
      if (!baselinePath)
        throw new Error("Missing 'baseline_path' argument");
      return await regressionCheck(config, baselinePath);
    }

    case "unity_scanner_baseline_create":
      return await baselineCreate(config);

    default:
      throw new Error(`Unknown tool: ${toolName}`);
  }
}
