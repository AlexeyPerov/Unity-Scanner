import { launchUnity } from "../unity/launcher.js";
import { parseScanResult } from "../unity/result-parser.js";
import type { MCPConfig, ScanResult } from "../types.js";

export async function runAll(config: MCPConfig) {
  const raw = await launchUnity<ScanResult>({
    config,
    method: "MCP_RunAll",
  });
  return parseScanResult(raw);
}
