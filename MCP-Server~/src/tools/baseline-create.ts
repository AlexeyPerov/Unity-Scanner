import { launchUnity } from "../unity/launcher.js";
import { parseBaseline } from "../unity/result-parser.js";
import type { MCPConfig, BaselineResult } from "../types.js";

export async function baselineCreate(config: MCPConfig) {
  const raw = await launchUnity<BaselineResult>({
    config,
    method: "MCP_BaselineCreate",
  });
  return parseBaseline(raw);
}
