#!/usr/bin/env node

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { loadConfig } from "./config.js";
import { tools } from "./tools/registry.js";
import { dispatch } from "./tools/dispatch.js";

async function main() {
  let config;
  try {
    config = loadConfig();
  } catch (err) {
    console.error(
      `Failed to load config: ${err instanceof Error ? err.message : err}`
    );
    process.exit(1);
  }

  const server = new McpServer({
    name: "unityscanner-mcp",
    version: "1.0.0",
  });

  for (const tool of tools) {
    server.tool(
      tool.name,
      tool.description ?? "",
      tool.inputSchema as Record<string, unknown>,
      async (args: Record<string, unknown>) => {
        try {
          const result = await dispatch(tool.name, args, config);
          return {
            content: [
              { type: "text" as const, text: result.text },
              ...(result.structured
                ? [
                    {
                      type: "text" as const,
                      text: "```json\n" +
                        JSON.stringify(result.structured, null, 2) +
                        "\n```",
                    },
                  ]
                : []),
            ],
          };
        } catch (err) {
          const message =
            err instanceof Error ? err.message : String(err);
          return {
            content: [{ type: "text" as const, text: `Error: ${message}` }],
            isError: true,
          };
        }
      }
    );
  }

  const transport = new StdioServerTransport();
  await server.connect(transport);

  console.error("UnityScanner MCP server running on stdio");
}

main().catch((err) => {
  console.error(`Fatal: ${err instanceof Error ? err.message : err}`);
  process.exit(1);
});
