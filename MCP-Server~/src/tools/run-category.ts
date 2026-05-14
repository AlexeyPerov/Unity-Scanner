import { launchUnity } from "../unity/launcher.js";
import { parseScanResult } from "../unity/result-parser.js";
import type { MCPConfig, ScanResult } from "../types.js";

export async function runCategory(config: MCPConfig, categoryId: string) {
  const raw = await launchUnity<ScanResult>({
    config,
    method: "MCP_RunCategory",
    extraArgs: ["-usCategory", categoryId],
  });
  return parseScanResult(raw);
}
