import { launchUnity } from "../unity/launcher.js";
import { parseScanResult } from "../unity/result-parser.js";
import type { MCPConfig, ScanResult } from "../types.js";

export async function runSelected(
  config: MCPConfig,
  categories: string[]
) {
  const raw = await launchUnity<ScanResult>({
    config,
    method: "MCP_RunSelected",
    extraArgs: ["-usCategories", categories.join(",")],
  });
  return parseScanResult(raw);
}
