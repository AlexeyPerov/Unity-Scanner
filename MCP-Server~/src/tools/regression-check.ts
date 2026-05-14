import { launchUnity } from "../unity/launcher.js";
import { parseRegression } from "../unity/result-parser.js";
import type { MCPConfig, RegressionResult } from "../types.js";

export async function regressionCheck(
  config: MCPConfig,
  baselinePath: string
) {
  const raw = await launchUnity<RegressionResult>({
    config,
    method: "MCP_RegressionCheck",
    extraArgs: ["-usBaseline", baselinePath],
  });
  return parseRegression(raw);
}
