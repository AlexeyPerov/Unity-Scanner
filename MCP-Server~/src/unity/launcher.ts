import { spawn } from "child_process";
import { mkdtempSync, existsSync, readFileSync, unlinkSync, rmSync } from "fs";
import { join } from "path";
import { tmpdir } from "os";
import type { MCPConfig } from "../types.js";
import { getTimeout } from "../config.js";

interface LaunchOptions {
  config: MCPConfig;
  method: string;
  extraArgs?: string[];
  timeoutOverride?: number;
}

export function launchUnity<T>(opts: LaunchOptions): Promise<T> {
  return new Promise((resolve, reject) => {
    const tmpDir = mkdtempSync(join(tmpdir(), "us-mcp-"));
    const outputPath = join(tmpDir, "result.json");
    const logPath = join(tmpDir, "unity.log");

    const args = [
      "-batchmode",
      "-nographics",
      "-quit",
      "-projectPath",
      opts.config.projectPath,
      "-executeMethod",
      `UnityScanner.MCP.UnityScannerMCPEntry.${opts.method}`,
      "-usOutput",
      outputPath,
      "-logFile",
      logPath,
      ...(opts.extraArgs ?? []),
    ];

    const timeout =
      opts.timeoutOverride ?? getTimeout(opts.config, opts.method);
    const timeoutMs = timeout * 1000;

    const proc = spawn(opts.config.unityPath, args, {
      stdio: "ignore",
      detached: false,
    });

    let settled = false;

    const timer = setTimeout(() => {
      if (settled) return;
      settled = true;
      try {
        proc.kill("SIGKILL");
      } catch {}
      cleanup(tmpDir);
      reject(new Error(`Unity process timed out after ${timeout}s`));
    }, timeoutMs);

    proc.on("close", (code) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);

      if (!existsSync(outputPath)) {
        const logContent = existsSync(logPath)
          ? readFileSync(logPath, "utf-8").slice(-2000)
          : "(no log file)";
        cleanup(tmpDir);
        reject(
          new Error(
            `Unity exited with code ${code} but no output file was created.\nUnity log (tail):\n${logContent}`
          )
        );
        return;
      }

      try {
        const raw = readFileSync(outputPath, "utf-8");
        cleanup(tmpDir);
        resolve(JSON.parse(raw) as T);
      } catch (err) {
        cleanup(tmpDir);
        reject(new Error(`Failed to parse Unity output: ${err}`));
      }
    });

    proc.on("error", (err) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      cleanup(tmpDir);
      reject(new Error(`Failed to launch Unity: ${err.message}`));
    });
  });
}

function cleanup(tmpDir: string): void {
  try {
    rmSync(tmpDir, { recursive: true, force: true });
  } catch {}
}
