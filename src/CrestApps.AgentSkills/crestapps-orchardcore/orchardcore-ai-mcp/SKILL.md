---
name: orchardcore-ai-mcp
description: Overview skill for Model Context Protocol (MCP) support in Orchard Core through the CrestApps.OrchardCore.AI.Mcp module. Covers choosing between the MCP client features (CrestApps.OrchardCore.AI.Mcp for remote SSE servers, CrestApps.OrchardCore.AI.Mcp.Stdio for local process servers) and the MCP Server feature (CrestApps.OrchardCore.AI.Mcp.Server with opt-in tool exposure), the FTP and SFTP resource modules, the Model Context Protocol admin screens (MCP Hosts, Prompts, Resources), permissions, the McpConnection, McpPrompt and McpResource recipe steps, deployment steps, and a combined setup recipe. Use this skill when requests mention Orchard Core MCP in general, configuring MCP integration, picking the right MCP feature, or an end-to-end MCP setup, then switch to orchardcore-ai-mcp-client, orchardcore-ai-mcp-server or orchardcore-ai-mcp-resources for details.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core Model Context Protocol (MCP)

The [Model Context Protocol](https://modelcontextprotocol.io/introduction) is an open standard for connecting LLM applications to tools and data. The `CrestApps.OrchardCore.AI.Mcp` package lets Orchard Core act as an MCP **client** (use tools hosted elsewhere) and as an MCP **server** (offer Orchard Core tools, prompts, and resources to other AI apps).

## Pick the right feature

| Goal | Feature ID | Skill |
|---|---|---|
| Call tools on a remote MCP server over HTTP | `CrestApps.OrchardCore.AI.Mcp` | `orchardcore-ai-mcp-client` |
| Run a local MCP server process (for example Docker) | `CrestApps.OrchardCore.AI.Mcp.Stdio` | `orchardcore-ai-mcp-client` |
| Let IDEs and AI apps call Orchard Core tools, prompts, resources | `CrestApps.OrchardCore.AI.Mcp.Server` | `orchardcore-ai-mcp-server` |
| Expose FTP/FTPS files as MCP resources | `CrestApps.OrchardCore.AI.Mcp.Resources.Ftp` | `orchardcore-ai-mcp-resources` |
| Expose SFTP files as MCP resources | `CrestApps.OrchardCore.AI.Mcp.Resources.Sftp` | `orchardcore-ai-mcp-resources` |

All MCP features depend on `CrestApps.OrchardCore.AI`. The client feature also depends on `CrestApps.OrchardCore.Resources`, and the stdio and resource features depend on the client and server features respectively. Install the packages in the web/startup project.

## Guidelines

- Client and server are independent. Enable only the side you need.
- The server exposes **no tools by default**. Opt tools and tool instances in on the **MCP Server** card under **Settings → Artificial Intelligence**, or with `McpServerOptions.Tools` / `ExposeAllTools`.
- Use `OpenId` authentication for the server in production; `None` is for local development only.
- Credentials on client connections and FTP/SFTP resources are encrypted at rest and blanked in deployment exports.
- Tool calls, from either direction, still go through Orchard Core permission checks (`AccessAITool_{name}` or `AccessAnyAITool`).

## Admin screens

Under **Artificial Intelligence → Model Context Protocol**:

| Screen | Feature | Permission |
|---|---|---|
| **MCP Hosts** | MCP Client | `ManageMcpConnections` |
| **Prompts** | MCP Server | `ManageMcpPrompts` |
| **Resources** | MCP Server | `ManageMcpResources` |

The **MCP Server** card under **Settings → Artificial Intelligence** needs `ManageMcpServerSettings`. Calling the server in `OpenId` mode needs `AccessMcpServer` when `RequireAccessPermission` is `true`. All three lists render through Orchard Core's `AdminList` shape (`McpConnections`, `McpPrompts`, `McpResources`); see `orchardcore-admin-lists`.

## Recipe and deployment steps

| Step | Root array | Notes |
|---|---|---|
| `McpConnection` | `Connections` | `Source` (`sse` or `stdio`) is required for a new connection |
| `McpPrompt` | `Prompts` | `Prompt.Name` is the key; messages use role `user` or `assistant` |
| `McpResource` | `Resources` | `Source` is the resource type; `Resource.Uri` may be the path portion |

With `OrchardCore.Deployment` enabled, matching **Connection**, **Prompt**, and **Resource** deployment steps appear under **Artificial Intelligence** in the deployment-plan editor.

## Combined setup

Connect to a remote MCP server and expose Orchard Core's own content search to MCP clients:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Agent",
        "CrestApps.OrchardCore.AI.Mcp",
        "CrestApps.OrchardCore.AI.Mcp.Server"
      ],
      "disable": []
    },
    {
      "name": "McpConnection",
      "Connections": [
        {
          "Source": "sse",
          "DisplayText": "Remote tools server",
          "Properties": {
            "SseMcpConnectionMetadata": {
              "Endpoint": "https://mcp-tools.example.com/",
              "AuthenticationType": "Anonymous"
            }
          }
        }
      ]
    }
  ]
}
```

Then allow the tools for MCP clients in `appsettings.json` (or on the **MCP Server** card):

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "McpServer": {
          "AuthenticationType": "OpenId",
          "RequireAccessPermission": true,
          "Tools": [ "searchForContentItems", "getContentItemById" ]
        }
      }
    }
  }
}
```

An AI provider connection and a chat deployment are also needed before profiles can use the remote tools; see `orchardcore-ai-connections` and `orchardcore-ai-providers`. More end-to-end samples are in [references/ai-mcp-examples.md](references/ai-mcp-examples.md).

## Upgrading from 2.x

- The MCP Server no longer exposes every tool. Opt tools in, or set `ExposeAllTools: true`, after upgrading.
- `McpServerOptions` is refreshed live through `IOptionsMonitor<T>`; code reading it through `IOptions<T>` must switch.
- The MCP lists use Orchard Core's `AdminList` shape; whole-row template overrides only apply to the List layout.

## Discover MCP servers

- [Docker Hub MCP images](https://hub.docker.com/search?q=mcp)
- [MCP.so](https://mcp.so/)
- [Glama.ai MCP servers](https://glama.ai/mcp/servers)
- [MCPServers.org](https://mcpservers.org/)
