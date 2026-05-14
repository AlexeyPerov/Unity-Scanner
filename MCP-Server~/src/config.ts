import { readFileSync, existsSync } from "fs";
import { join, dirname } from "path";
import type { MCPConfig } from "./types.js";

const CONFIG_FILENAME = "mcp-config.json";

function searchConfigPaths(): string[] {
  const candidates: string[] = [];

  const cwd = process.cwd();
  candidates.push(join(cwd, CONFIG_FILENAME));
  candidates.push(join(cwd, "unityscanner-mcp.json"));

  const homeDir =
    process.env.HOME || process.env.USERPROFILE || process.env.HOMEPATH || "";
  if (homeDir) {
    candidates.push(join(homeDir, ".unityscanner", CONFIG_FILENAME));
  }

  const scriptDir = dirname(new URL(import.meta.url).pathname);
  candidates.push(join(scriptDir, "..", CONFIG_FILENAME));

  return candidates;
}

export function loadConfig(): MCPConfig {
  const configPath = process.env.UNITY_SCANNER_CONFIG;
  const paths = configPath ? [configPath] : searchConfigPaths();

  for (const p of paths) {
    if (existsSync(p)) {
      const raw = readFileSync(p, "utf-8");
      const config = JSON.parse(raw) as MCPConfig;
      validateConfig(config, p);
      return config;
    }
  }

  throw new Error(
    `No MCP config found. Searched:\n${paths.map((p) => "  " + p).join("\n")}\n\nCreate mcp-config.json with unityPath and projectPath fields.`
  );
}

function validateConfig(config: MCPConfig, path: string): void {
  if (!config.unityPath) {
    throw new Error(`Missing "unityPath" in ${path}`);
  }
  if (!config.projectPath) {
    throw new Error(`Missing "projectPath" in ${path}`);
  }
}

export function getTimeout(config: MCPConfig, tool: string): number {
  const timeouts = config.timeouts ?? {};
  const specific = (
    timeouts as Record<string, number | undefined>
  )[tool];
  return specific ?? timeouts.default ?? 120;
}
