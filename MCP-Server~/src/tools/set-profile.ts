import { launchUnity } from "../unity/launcher.js";
import { parseProfileSet } from "../unity/result-parser.js";
import type { MCPConfig } from "../types.js";

export async function setProfile(
  config: MCPConfig,
  profile: string
) {
  await launchUnity<{ success: boolean; profile: string }>({
    config,
    method: "MCP_SetProfile",
    extraArgs: ["-usPlatformProfile", profile],
  });
  return parseProfileSet(profile);
}
