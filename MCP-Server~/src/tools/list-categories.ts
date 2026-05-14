import { launchUnity } from "../unity/launcher.js";
import { parseCategoryList } from "../unity/result-parser.js";
import type { MCPConfig, CategoryListResult } from "../types.js";

export async function listCategories(config: MCPConfig) {
  const raw = await launchUnity<CategoryListResult>({
    config,
    method: "MCP_ListCategories",
  });
  return parseCategoryList(raw);
}
