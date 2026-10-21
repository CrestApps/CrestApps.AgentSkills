---
name: orchardcore-ai-mcp-server
description: Skill for the CrestApps Orchard Core MCP Server feature (CrestApps.OrchardCore.AI.Mcp.Server). Covers the Streamable HTTP endpoint at /mcp, OpenId, ApiKey and None authentication, opt-in tool exposure on the MCP Server settings card (McpServerSettings loaded into McpServerOptions with ExposeAllTools, Tools, ExposeAllAgents and Agents), exposing tool instances such as documentation search to MCP clients, MCP prompts and resources with the McpPrompt and McpResource recipe steps, built-in resource types, custom resource types with AddCoreAIMcpResourceType and McpResourceTypeHandlerBase, the AccessMcpServer, ManageMcpServerSettings, ManageMcpPrompts and ManageMcpResources permissions, live IOptionsMonitor settings, and upgrading from releases that exposed every tool. Use this skill when requests mention Orchard Core MCP Server, exposing Orchard Core tools to Claude, VS Code or Copilot, McpServerOptions, ExposeAllTools, MCP prompts or resources, or tools that do not appear for MCP clients.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core MCP Server

The MCP Server feature exposes Orchard Core AI tools, tool instances, prompts, and resources to external MCP clients (IDEs, desktop agents, other AI apps) over the MCP **Streamable HTTP** transport.

## Guidelines

- Feature ID `CrestApps.OrchardCore.AI.Mcp.Server`, from the `CrestApps.OrchardCore.AI.Mcp` package. It depends only on `CrestApps.OrchardCore.AI`. Install the package in the web/startup project.
- **Tool exposure is opt-in.** Nothing is listed or callable until you select tools and tool instances on the **MCP Server** card, list them in `McpServerOptions.Tools`, or set `ExposeAllTools` to `true`. Prompts and resources are not affected by this switch.
- System tools and hidden tools are never exposed, even with `ExposeAllTools`. Only *selectable* tools (registered with `.Selectable()`) can be exposed.
- Agent profiles are gated separately through `ExposeAllAgents` and `Agents` (configuration only).
- Exposed tools still run Orchard Core permission checks for the calling identity.
- Use `OpenId` in production. `None` is for local development only.
- The settings are read through `IOptionsMonitor<McpServerOptions>`, so saving the card takes effect without a tenant reload.

## Enable the feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.AI",
        "CrestApps.OrchardCore.AI.Mcp.Server"
      ],
      "disable": []
    }
  ]
}
```

Enable `CrestApps.OrchardCore.AI.Agent` and other tool-providing features only when you intend to expose their tools, and enable `CrestApps.OrchardCore.AI.ToolInstances` to expose tool instances.

## Endpoint

`routes.MapMcp("mcp")` maps one Streamable HTTP endpoint, relative to the tenant URL prefix, protected by the `McpServerPolicy` authorization policy (schemes `McpApiKey` and `Api`).

| Method | Purpose |
|---|---|
| `POST /mcp` | Send a JSON-RPC message. The reply is a JSON payload or a `text/event-stream`. |
| `GET /mcp` | Open the server-to-client event stream for an established session. |
| `DELETE /mcp` | End the session. |

The deprecated HTTP+SSE transport (`GET /mcp/sse` plus `POST /mcp/message`) is not mapped. Clients that only speak that transport cannot connect.

## Tool exposure

Configure exposure under **Settings → Artificial Intelligence**, on the **MCP Server** card (permission `ManageMcpServerSettings`, granted to Administrator by default):

- **Authentication type**, **API key**, and **Require access permission**.
- **Expose all tools** - exposes every selectable tool and every tool instance, and hides the selectors.
- **Tools** and **Tool instances** selectors - list only what the current admin user may access (same selector as the AI Profile editor). Only the checked entries are exposed.

The allow-list is enforced by both the list and the call handlers, so a tool that is not exposed can neither be discovered nor invoked. Each `Tools` entry matches a code-registered tool name, a tool instance's function name, or a tool instance's technical name (case-insensitive).

### Configuration override

The card is stored as `McpServerSettings` site settings and projected into `McpServerOptions`. The shell configuration section `OrchardCore:CrestApps:AI:McpServer` is bound afterwards and overrides the stored values (the older `OrchardCore:CrestApps:McpServer` section is still read first for backward compatibility).

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "McpServer": {
          "AuthenticationType": "OpenId",
          "RequireAccessPermission": true,
          "ExposeAllTools": false,
          "Tools": [ "searchForContentItems", "orchardcore-docs" ],
          "ExposeAllAgents": false,
          "Agents": [ "research-agent" ]
        }
      }
    }
  }
}
```

| Option | Default | Description |
|---|---|---|
| `AuthenticationType` | `OpenId` | `OpenId`, `ApiKey`, or `None`. |
| `ApiKey` | `null` | Key required in `ApiKey` mode. |
| `RequireAccessPermission` | `true` | In `OpenId` mode, also require the `AccessMcpServer` permission. |
| `ExposeAllTools` | `false` | Expose every selectable tool and tool instance; `Tools` is ignored. |
| `Tools` | `[]` | Allow-list of tool and tool instance names. |
| `ExposeAllAgents` | `false` | Expose every Agent profile as a callable tool. Not on the admin card. |
| `Agents` | `[]` | Allow-list of Agent profile `Name`s. Not on the admin card. |

An exposed agent runs its whole profile (system message, tools, data sources) for any client that passes authentication. Its internal tools are neither filtered by nor exposed through `Tools`.

### Example - expose documentation search only

1. Enable `CrestApps.OrchardCore.AI.ToolInstances`.
2. Under **Artificial Intelligence → Tool Instances**, add one **Documentation search (sitemap)** instance per public site (for example `orchardcore-docs` with base URL `https://docs.orchardcore.net`). See `orchardcore-ai-tool-instances`.
3. Enable the MCP Server and configure authentication.
4. On the **MCP Server** card, leave **Expose all tools** off and tick only those instances under **Tool instances**.

MCP clients now discover exactly those search tools while every content or management tool stays private.

## Authentication

| Mode | How clients authenticate |
|---|---|
| `OpenId` | Bearer token from the Orchard Core OpenID server (`Api` scheme). Requires `AccessMcpServer` when `RequireAccessPermission` is `true`. |
| `ApiKey` | `Authorization: Bearer <key>`, `Authorization: ApiKey <key>`, or the raw key. |
| `None` | No credentials. Development only. |

Prefer setting `ApiKey` through user secrets or environment variables (`OrchardCore__CrestApps__AI__McpServer__ApiKey`) rather than the admin card in production.

## Client configuration

```json
{
  "mcpServers": {
    "orchard-core": {
      "transport": {
        "type": "http",
        "url": "https://your-orchard-site.com/mcp",
        "headers": {
          "Authorization": "Bearer <token-or-api-key>"
        }
      }
    }
  }
}
```

Send `Accept: application/json, text/event-stream` on `POST /mcp`.

## MCP prompts

Managed under **Artificial Intelligence → Model Context Protocol → Prompts** (permission `ManageMcpPrompts`). A prompt has a **Title**, unique **Name**, optional **Description**, optional **Arguments** (name, title, description, required) referenced in content as `{{argName}}`, and one or more **Messages** with role `user` or `assistant`. Clients call `ListPrompts` and `GetPrompt`.

```json
{
  "steps": [
    {
      "name": "McpPrompt",
      "Prompts": [
        {
          "Prompt": {
            "Name": "summarize",
            "Title": "Summarize",
            "Description": "Summarizes supplied text.",
            "Arguments": [
              { "Name": "text", "Description": "The text to summarize.", "Required": true }
            ]
          },
          "Messages": [
            { "Role": "user", "Content": "Summarize the following text in three bullet points: {{text}}" }
          ]
        }
      ]
    }
  ]
}
```

## MCP resources

Managed under **Artificial Intelligence → Model Context Protocol → Resources** (permission `ManageMcpResources`). **Add Resource** lists the registered resource types. The editor takes **Display Text**, **Name**, **Path**, **Description**, **MIME Type**, and type-specific fields. The URI is built as `{source}://{itemId}/{path}`, and a path with `{variable}` placeholders becomes a templated resource listed by `ListResourceTemplates`.

| Type | Variables | Registered when |
|---|---|---|
| `file` | `{providerName}`, `{fileName}` | Always |
| `media` | `{path}` | `OrchardCore.Media` is enabled |
| `content-item` | `{contentItemId}`, `{contentItemVersionId}` | `OrchardCore.ContentManagement` is enabled |
| `content-type` | `{contentType}` | `OrchardCore.ContentManagement` is enabled |
| `recipe-schema` | none | `CrestApps.OrchardCore.Recipes` is enabled |
| `recipe-step-schema` | `{stepName}` | `CrestApps.OrchardCore.Recipes` is enabled |
| `recipe` | `{recipeName}` | `CrestApps.OrchardCore.Recipes` is enabled |
| `ftp`, `sftp` | `{path}` | See `orchardcore-ai-mcp-resources` |

```json
{
  "steps": [
    {
      "name": "McpResource",
      "Resources": [
        {
          "Source": "recipe-step-schema",
          "DisplayText": "Recipe step schema",
          "Resource": {
            "Uri": "{stepName}",
            "Name": "recipe-step-schema",
            "Description": "JSON schema for one recipe step.",
            "MimeType": "application/json"
          }
        }
      ]
    }
  ]
}
```

`Resource.Uri` accepts either the path portion (expanded to `{source}://{itemId}/{path}` on import) or a full URI that already uses the source scheme. The resource title is always taken from `DisplayText`.

## Custom resource type

```csharp
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;
using OrchardCore.Modules;

[RequireFeatures("CrestApps.OrchardCore.AI.Mcp.Server")]
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAIMcpResourceType<DatabaseResourceTypeHandler>("database", entry =>
        {
            entry.DisplayName = S["Database"];
            entry.Description = S["Query data from databases."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("table") { Description = S["The database table name."] },
                new McpResourceVariable("id") { Description = S["The row ID to fetch."] },
            ];
        });
    }
}

public sealed class DatabaseResourceTypeHandler : McpResourceTypeHandlerBase
{
    public DatabaseResourceTypeHandler()
        : base("database")
    {
    }

    protected override Task<ReadResourceResult> GetResultAsync(
        McpResource resource,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        variables.TryGetValue("table", out var table);
        variables.TryGetValue("id", out var id);

        // Query your data source here and return its contents.
        return Task.FromResult(CreateErrorResult(resource.Resource.Uri, "Not implemented."));
    }
}
```

The base class also offers `SanitizePath` (rejects traversal sequences) and `IsTextMimeType`. Add an `McpResource` display driver gated on `entry.Source` when the type needs its own fields.

## Deployment

With `OrchardCore.Deployment` enabled, the MCP **Prompt** and **Resource** export steps appear under the **Artificial Intelligence** category of the deployment-plan editor.

## Admin lists

The Prompts and Resources pages use Orchard Core's `AdminList` shape (list names `McpPrompts` and `McpResources`), so they follow the site's List or Grid layout. See `orchardcore-admin-lists` to add columns with `IAdminListColumnsProvider`.

## Upgrading from 2.x

- Releases before opt-in exposure listed every registered tool. After upgrading, MCP clients see **no tools** until you opt them in on the **MCP Server** card, populate `Tools`, or set `ExposeAllTools: true`.
- `ExposeAllTools` never exposes system or hidden tools.
- Saving the MCP Server settings no longer releases the tenant. Custom code that read `McpServerOptions` through `IOptions<T>` keeps the first value forever; switch to `IOptionsMonitor<McpServerOptions>.CurrentValue` (or `IOptionsSnapshot<T>` in a scoped service).
- Move configuration from `OrchardCore:CrestApps:McpServer` to `OrchardCore:CrestApps:AI:McpServer`.

## Troubleshooting

| Symptom | Check |
|---|---|
| Client connects but lists no tools | Tools are opt-in. Select them on the **MCP Server** card or set `ExposeAllTools`. |
| A tool is missing even with `ExposeAllTools` | It is a system or hidden tool, or its providing feature is disabled. |
| Tool listed but the call fails or is skipped | The calling identity lacks `AccessAITool_{name}` or `AccessAnyAITool`. |
| 401 or 403 | Authentication type and credentials do not match, or `AccessMcpServer` is missing in `OpenId` mode. |
| Legacy SSE client cannot connect | Point it at `/mcp` with a Streamable HTTP transport. |
| `appsettings.json` value ignored | Use `OrchardCore:CrestApps:AI:McpServer`; configuration changes need an app restart. |
