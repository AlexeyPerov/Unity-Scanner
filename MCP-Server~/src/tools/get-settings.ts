import { launchUnity } from "../unity/launcher.js";
import { parseSettings } from "../unity/result-parser.js";
import type { MCPConfig, SettingsResult } from "../types.js";

export async function getSettings(config: MCPConfig) {
  const raw = await launchUnity<SettingsResult>({
    config,
    method: "MCP_GetSettings",
  });
  return parseSettings(raw);
}
