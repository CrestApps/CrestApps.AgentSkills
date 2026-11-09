---
name: orchardcore-ai-webmcp
description: Skill for the CrestApps Orchard Core WebMCP module (CrestApps.OrchardCore.AI.WebMcp), a thin layer over CrestApps.Core.AI.WebMcp that gives AI agents in the browser tools through the standard document.modelContext API. Covers site tools selected on the WebMCP settings card (WebMcpSettings, ExposeAllTools, Tools, EnableOnAdminPages, EnableOnFrontEndPages, EnablePageTools), the CrestApps AI WebMcp configuration section and PageToolOrigins, the UseWebMcpTools and ManageWebMcpSettings permissions, the api/ai/webmcp endpoints, the WebMcpPart content part with per-content-item tools, the built-in page tools, the AI chat as a WebMCP agent with WebMcpProfileSettings AllowPageTools and AllowConsequentialPageTools, custom registerTool page tools, IWebMcpClientConfigurationHandler, and recipes. Use this skill when requests mention WebMCP, document.modelContext, browser AI agents using Orchard Core tools, page tools, letting the admin AI chat fill in a form, or tools on a content item's page.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core WebMCP

[WebMCP](https://webmachinelearning.github.io/webmcp/) is a proposed W3C Community Group standard that lets a web page give tools to an AI agent in the browser (the browser's own assistant, an extension, or a chat on the page). The page registers each tool with `document.modelContext.registerTool()`; an agent lists them with `getTools()` and runs one with `executeTool()`. The tool runs in the page with the signed-in user's session.

The `CrestApps.OrchardCore.AI.WebMcp` module is a thin Orchard Core layer over CrestApps.Core's `CrestApps.Core.AI.WebMcp` package and `@crestapps/webmcp` scripts. It uses only the standard API and adds the W3C reference polyfill for browsers without native WebMCP.

## Guidelines

- One feature, ID `CrestApps.OrchardCore.AI.WebMcp` (category **Artificial Intelligence - MCP**), depending on `CrestApps.OrchardCore.AI`. Install the `CrestApps.OrchardCore.AI.WebMcp` package in the web/startup project. Do not look for sub-features:
  - the AI chat bridge switches on by itself with **AI Chat** (`[RequireFeatures]` on `CrestApps.OrchardCore.AI.Chat`);
  - the **WebMCP tools** content part switches on with Orchard Core's **Contents** feature;
  - page tools are a setting (`EnablePageTools`).
- **Nothing is offered by default.** Tools are selected on the **WebMCP** card under **Settings → Artificial Intelligence**, or with `ExposeAllTools`. Front-end pages are off by default.
- Only signed-in users with **Use WebMCP tools in the browser** (`UseWebMcpTools`) get the scripts; anonymous visitors never do. Changing the card needs **Manage the WebMCP settings** (`ManageWebMcpSettings`). Administrators hold both by default.
- Every tool also needs the user's **Access AI tool** permission (`AccessAnyAITool` or the per-tool permission), checked on the server on every list and call. A browser agent can never do more than the user.
- The tool catalog is the same one the MCP Server reads: selectable tools registered with `AddCoreAITool<T>().Selectable()` plus configured tool instances. System and hidden tools are never offered. WebMCP adds no tools of its own to that catalog.
- WebMCP needs a secure context (HTTPS, or `localhost` while developing).

## WebMCP versus the MCP Server

| | MCP Server (`orchardcore-ai-mcp-server`) | WebMCP |
|---|---|---|
| Caller | A remote MCP client | An agent in the user's browser |
| Transport | Streamable HTTP at `/mcp` | `document.modelContext` plus same-origin endpoints |
| Authentication | API key, OpenID Connect or none | The user's cookie, plus the antiforgery token on calls |
| Tool selection | **MCP Server** card | **WebMCP** card |
| Per-user tool permission | No | Yes |

## Site tools

A full page (MVC view or Razor page) gets the scripts when the user is signed in, the page kind is turned on (admin pages by default, front-end pages only with **Offer tools on front-end pages**), and the user has `UseWebMcpTools`. The page then loads, in order: the polyfill (`CrestAppsWebMcpPolyfill`), `webmcp.js` (`CrestAppsWebMcp`), the page tools (`CrestAppsWebMcpPageTools`, unless turned off), and scripts added by `IWebMcpClientConfigurationHandler`.

| Endpoint | Purpose | Responses |
|---|---|---|
| `GET /api/ai/webmcp/tools` | Lists the tools the user may call | `401` not signed in, `403` without `UseWebMcpTools` |
| `POST /api/ai/webmcp/tools/{name}/invoke` | Calls a tool with the JSON body as arguments; returns `{ "content": [...], "isError" }` | `400` without the `RequestVerificationToken` header, a non-object body or a body over 256 KB; `404` when the tool is not listed for the user |

A tool that throws answers `isError: true` with a generic message; details go to the log. A hidden or forbidden tool always answers `404`, so the endpoint does not reveal which tools exist. The module maps Core's endpoints with `MapWebMcpTools()` and replaces Core's access check with `PermissionWebMcpAccessEvaluator`.

Saving the card releases the tenant (Core reads `IOptionsMonitor<WebMcpOptions>`). While **Expose all tools** is on, the card hides the tool lists.

### Configuration

The `CrestApps:AI:WebMcp` shell section overrides the stored settings: `ExposeAllTools`, `Tools`, `EnableOnAdminPages`, `EnableOnFrontEndPages`, `EnablePageTools`, and `PageToolOrigins` (origins of cross-origin frames whose shared tools the AI chat may use; empty by default). `CrestApps:AI:WebMcp:Chat:ToolCallTimeout` (default `00:01:00`) bounds how long the chat waits for the browser to run a page tool.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "AI": {
        "WebMcp": {
          "EnableOnFrontEndPages": false,
          "Tools": [ "listTimeZones" ],
          "Chat": { "ToolCallTimeout": "00:00:30" }
        }
      }
    }
  }
}
```

## Content item tools

The **WebMCP tools** part (`WebMcpPart`, property `Tools`) lets each content item pick the tools its own page exposes.

1. Attach **WebMCP tools** to a content type.
2. On each item, pick the tools. The editor lists only the selectable tools the editor may use; tools another editor picked stay selected.
3. On the item's **Detail** display, signed-in users with `UseWebMcpTools` who may **view** the item get those tools, in addition to the site tools.

| Endpoint | Purpose |
|---|---|
| `GET /api/ai/webmcp/content-items/{contentItemId}/tools` | Lists the item's tools the user may call |
| `POST /api/ai/webmcp/content-items/{contentItemId}/tools/{name}/invoke` | Calls one, with the antiforgery token |

Every list and call checks the user, `UseWebMcpTools`, that the published item has the part with that tool, that the user may view the item, and **Access AI tool**. An item's tools are added even when front-end pages are off on the card.

## Page tools

With **Add page tools** on (default), CrestApps.Core's page tools are registered for any agent:

| Tool | Read-only | What it does |
|---|---|---|
| `page_get_info` | Yes | Title, address, description, first 20 `h1`/`h2` headings, whether it is an admin page |
| `page_list_links` | Yes | Up to 200 same-origin navigation links, optional `filter` |
| `page_navigate` | No | Opens a same-origin address only |
| `page_list_form_fields` | Yes | Up to 200 form fields; hidden, password, file, button, disabled and antiforgery fields are left out |
| `page_set_form_field` | No | Sets one field and raises `input` and `change`; never submits the form |

## The AI chat as a WebMCP agent

With **AI Chat** enabled, the chat reads every tool `document.modelContext.getTools()` lists (not only CrestApps ones), sends them to the AI chat hub with `SetPageTools`, and the hub runs a call in the tab through `ExecutePageTool` (SignalR client results). No extra connection or sticky session is needed.

- The hub accepts at most 64 tools, names matching `^[A-Za-z0-9_.-]{1,128}$`, descriptions up to 2,000 characters and object schemas up to 16 KB.
- Tools are offered only to profiles with **Allow page tools** (`WebMcpProfileSettings.AllowPageTools`) on the profile's **Capabilities** tab. Tools annotated `consequentialHint` also need **Allow consequential page tools** (`AllowConsequentialPageTools`).
- A name with `.` is offered to the model with `_` (`store.add_to_cart` becomes `store_add_to_cart`). Results of tools annotated `untrustedContentHint` reach the model as data.
- A cross-origin frame shares tools with `exposedTo`, the page must allow it with `<iframe allow="tools">`, and the origin must be listed in `PageToolOrigins`.

## Registering your own tools

Register page tools with the standard API from a script resource that depends on `CrestAppsWebMcpPolyfill` (or `CrestAppsWebMcp` when it reads `window.crestAppsWebMcp.config`). Prefix the name with your module and use the `readOnlyHint`, `consequentialHint` and `untrustedContentHint` annotations.

```javascript
const registration = new AbortController();

document.modelContext.registerTool({
    name: 'contoso.find_store',
    title: 'Find a store',
    description: 'Finds the Contoso store nearest to a postal code.',
    inputSchema: {
        type: 'object',
        properties: { postalCode: { type: 'string' } },
        required: ['postalCode'],
    },
    annotations: { readOnlyHint: true },
    execute: async ({ postalCode }) => findStore(postalCode),
}, { signal: registration.signal });
```

To expose an existing server AI tool, do not write a page tool: register it with `.Selectable()` and select it on the card or on an item's **WebMCP tools** part.

### Adding scripts and settings to every WebMCP page

`IWebMcpClientConfigurationHandler.ConfigureAsync(WebMcpClientConfigurationContext)` runs after the user and page qualified. `context.AddScript(...)` loads a resource after `webmcp.js`; `context.Settings` is written to the `crestapps-webmcp-config` JSON block and read as `window.crestAppsWebMcp.config`. `context.IsAdminPage` and `context.HttpContext` are available.

```csharp
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;

internal sealed class ContosoWebMcpHandler : IWebMcpClientConfigurationHandler
{
    public Task ConfigureAsync(WebMcpClientConfigurationContext context)
    {
        context.AddScript("ContosoWebMcpTools");
        context.Settings["storesUrl"] = $"{context.HttpContext.Request.PathBase}/stores";

        return Task.CompletedTask;
    }
}

// In a startup marked [RequireFeatures("CrestApps.OrchardCore.AI.WebMcp")]:
services.AddScoped<IWebMcpClientConfigurationHandler, ContosoWebMcpHandler>();
```

## Recipes

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [ "CrestApps.OrchardCore.AI.WebMcp" ],
      "disable": []
    },
    {
      "name": "Settings",
      "WebMcpSettings": {
        "EnableOnAdminPages": true,
        "EnableOnFrontEndPages": false,
        "EnablePageTools": true,
        "ExposeAllTools": false,
        "Tools": [ "listAIProfiles" ]
      }
    },
    {
      "name": "AIProfile",
      "Profiles": [
        {
          "Name": "admin-assistant",
          "Settings": {
            "WebMcpProfileSettings": {
              "AllowPageTools": true,
              "AllowConsequentialPageTools": false
            }
          }
        }
      ]
    }
  ]
}
```

Per-item tools: attach `WebMcpPart` with the `ContentDefinition` step and set `"WebMcpPart": { "Tools": [ "checkStock" ] }` on the item in the `Content` step. See [references/webmcp-examples.md](references/webmcp-examples.md).

## Security checklist

- Give browser agents only tools you would let the user run; prefer read-only tools.
- Page tools never submit forms and never leave the site; the user reviews and saves.
- **Allow page tools** lets the model run any tool the page exposes, including third-party scripts' tools. Enable it only on profiles used on pages whose scripts you trust.
- A recipe can set item tools an editor could not pick; **Access AI tool** is still checked on every call.

## Related skills

- `orchardcore-ai-mcp` and `orchardcore-ai-mcp-server` for remote MCP clients.
- `orchardcore-ai-tools` for registering selectable AI tools; `orchardcore-ai-tool-instances` for tool instances.
- `orchardcore-ai-chat` for AI profiles and the chat widget.
- `orchardcore-nlweb` for the `ask` tool, which can be selected on the WebMCP card.
