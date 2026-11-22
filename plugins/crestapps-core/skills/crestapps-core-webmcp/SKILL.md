---
name: crestapps-core-webmcp
description: Skill for WebMCP in CrestApps.Core 2.0, the proposed W3C standard that lets a page give tools to browser AI agents through document.modelContext. Covers AddWebMcp and AddCoreAIWebMcp, MapWebMcpTools endpoints, WebMcpOptions, IWebMcpAccessEvaluator, the webmcp.js, webmcp-page-tools.js and webmcp-polyfill.js scripts with the crestapps-webmcp-config block, the page tools (page_get_info, page_read_content, page_list_links, page_navigate, page_list_form_fields, page_set_form_field with previousValue), the AI chat as a WebMCP agent with AddPageTools, AllowPageTools and AllowConsequentialPageTools, consequentialHint tools that wait for approval in AG-UI runs, untrustedContentHint, page tool failure messages, function-name mapping, pageToolOrigins, SetPageTools and ExecutePageTool, and the WebMCP Playground. Use it when exposing AI tools to browser agents, letting the chat drive the page a user has open, or troubleshooting page tools that never reach the model.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core WebMCP

[WebMCP](https://webmachinelearning.github.io/webmcp/) lets a page register tools with `document.modelContext.registerTool()` (name, description, JSON schema, annotations and a JavaScript `execute`). An agent lists them with `getTools()` and runs one with `executeTool()`. CrestApps uses only that standard API, in two directions:

| Part | What it does |
|---|---|
| Your AI tools as WebMCP tools | `webmcp.js` registers the AI tools and tool instances you select; calling one posts to your app, which runs it as the signed-in user |
| Page tools | `webmcp-page-tools.js` registers tools that read the page and what it shows (text, tables, form values), list its links, open another page of the site, and read and fill forms |
| The AI chat as a WebMCP agent | The AI Profile chat discovers and runs every WebMCP tool the tab exposes (yours, your own scripts', third-party scripts', shared frames'), only for profiles that allow it |

## Guidelines

- Nothing is exposed by default. Select tools in `WebMcpOptions`, render the scripts only for signed-in users, and allow page tools per profile.
- The server re-checks the user, `IWebMcpAccessEvaluator`, `WebMcpOptions` and `IAIToolAccessEvaluator` on every list and call; the browser list only decides what gets registered.
- WebMCP needs a secure context (HTTPS, or `localhost`). Load `webmcp-polyfill.js` first so browsers without native WebMCP get `document.modelContext`.
- A profile that allows page tools lets the model run any tool the page exposes, including a third-party script's. Allow it only where you trust every script on the page, and keep `AllowConsequentialPageTools` off unless needed.
- Page tools reach AI Profile chat sessions (`AIChatHubCore`) only, not Chat Interactions.

## Registration

```csharp
using CrestApps.Core.AI.WebMcp;
using CrestApps.Core.AI.WebMcp.Endpoints;
using CrestApps.Core.AI.WebMcp.Models;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddWebMcp(webMcp => webMcp
            .AddPageTools<AIChatHub>())));

builder.Services.Configure<WebMcpOptions>(options =>
{
    options.Tools = ["calculator"];
});

var app = builder.Build();

app.MapHub<AIChatHub>("/hubs/ai-chat");
app.MapWebMcpTools();
```

Without the suite builder: `services.AddCoreAIWebMcp().AddCoreAIWebMcpPageTools<AIChatHub>();`.

- `AddCoreAIWebMcp()` registers `WebMcpOptions`, antiforgery, `IWebMcpToolService` and the default `IWebMcpAccessEvaluator` (all `TryAdd`).
- `AddCoreAIWebMcpPageTools<THub>()` registers `WebMcpPageToolOptions`, the in-memory `IWebMcpPageToolRegistry`, `IWebMcpPageToolClient` (`HubWebMcpPageToolClient<THub>`) and the page-tool `IToolRegistryProvider`.
- Client results need `MaximumParallelInvocationsPerClient` above 1; `ConfigureCrestAppsChatHubOptions<THub>()` sets 8.
- The NuGet package is `CrestApps.Core.AI.WebMcp`. The scripts come from the `CrestApps.AI.WebMcp.Resources` Razor class library (served under `_content/CrestApps.AI.WebMcp.Resources/scripts/`; it is a project reference, not a NuGet package) or from the `@crestapps/webmcp` npm package (`dist/webmcp.js`, `webmcp-page-tools.js`, `webmcp-polyfill.js`, plus `.min.js` builds of the first two).

## Choosing Tools and Users

| `WebMcpOptions` | Default | Meaning |
|---|---|---|
| `ExposeAllTools` | `false` | Every selectable tool and tool instance; system and hidden tools are never given |
| `Tools` | empty | Names given when `ExposeAllTools` is `false`, case-insensitive |

WebMCP adds no tools of its own: the source is the same tool catalog (`AddCoreAITool<T>()`) and tool instances. `DefaultWebMcpAccessEvaluator` allows every signed-in user; replace it to require a role or permission:

```csharp
using System.Security.Claims;
using CrestApps.Core.AI.WebMcp.Services;

public sealed class AdministratorsOnlyWebMcpAccessEvaluator : IWebMcpAccessEvaluator
{
    public Task<bool> CanUseAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => Task.FromResult(user?.IsInRole("Administrator") == true);
}
```

```csharp
builder.Services.AddSingleton<IWebMcpAccessEvaluator, AdministratorsOnlyWebMcpAccessEvaluator>();
```

## Endpoints

`app.MapWebMcpTools(prefix)` maps under `WebMcpConstants.DefaultRoutePrefix` (`api/ai/webmcp`) and returns the `RouteGroupBuilder`, so you can add rate limiting.

| Route | Result | Failures |
|---|---|---|
| `GET {prefix}/tools` | `{ "tools": [{ "name", "title", "description", "inputSchema" }] }` | `401` anonymous, `403` evaluator refused |
| `POST {prefix}/tools/{name}/invoke` | Runs the tool in an `AIInvocationScope` with the JSON object body; answers `{ "content": [{ "type": "text", "text" }], "isError" }` | `401`, `403`, `400` for a missing antiforgery header token, a non-object body or a body over 256 KB (`WebMcpEndpoints.MaxRequestBodyLength`), `404` for any tool not listed for the user |

A tool that throws answers `isError: true` with a generic message; the exception is only logged.

## Page Setup

Render the configuration block and the scripts for signed-in users, before the chat scripts. The block's id is `WebMcpConstants.ConfigurationElementId` (`crestapps-webmcp-config`):

```cshtml
@using System.Text.Json
@using CrestApps.Core.AI.WebMcp
@using Microsoft.AspNetCore.Antiforgery
@inject IAntiforgery Antiforgery

@if (User.Identity?.IsAuthenticated == true)
{
    var tokens = Antiforgery.GetAndStoreTokens(Context);
    var configuration = new Dictionary<string, object>
    {
        ["toolsUrl"] = Url.Content($"~/{WebMcpConstants.DefaultRoutePrefix}/tools"),
        ["invokeUrlTemplate"] = Url.Content($"~/{WebMcpConstants.DefaultRoutePrefix}/tools/") + "{name}/invoke",
        ["antiforgeryHeaderName"] = tokens.HeaderName,
        ["antiforgeryToken"] = tokens.RequestToken,
    };

    <script type="application/json" id="@WebMcpConstants.ConfigurationElementId">@Html.Raw(JsonSerializer.Serialize(configuration))</script>
    <script src="~/_content/CrestApps.AI.WebMcp.Resources/scripts/webmcp-polyfill.js"></script>
    <script src="~/_content/CrestApps.AI.WebMcp.Resources/scripts/webmcp.js"></script>
    <script src="~/_content/CrestApps.AI.WebMcp.Resources/scripts/webmcp-page-tools.js"></script>
}
```

| Config key | Meaning |
|---|---|
| `toolsUrl`, `invokeUrlTemplate` | Where `webmcp.js` lists and calls your AI tools (`{name}` is replaced). Omit to register no AI tools |
| `antiforgeryHeaderName`, `antiforgeryToken` | Sent with every call |
| `isAdmin` | Reported by `page_get_info` |
| `pageToolOrigins` | Cross-origin frame origins whose shared tools the chat may use (`getTools({ fromOrigins })`) |

`webmcp.js` marks each AI tool `untrustedContentHint`, never sets `exposedTo`, and rejects `execute` when the tool fails. It publishes `window.crestAppsWebMcp` (`config`, `ready` promise of registered names, `isSupported()`, `connectChat(connection)`) and raises `crestapps-webmcp-ready` on `document`. Register your own tools with the plain standard API; the chat finds them like any other. See [webmcp-examples.md](references/webmcp-examples.md).

Built-in page tools:

| Tool | Read-only | What it does |
|---|---|---|
| `page_get_info` | Yes | Title, address, description, the first 20 `h1`/`h2` headings, and `isAdmin` from the config |
| `page_read_content` | Yes | What `main` (or `[role="main"]`, else the body) shows: text one line per block, cut to 8,000 characters, plus up to 10 tables (caption, headers, first 50 rows, cells cut to 300 characters) marked `[table N]`. Form fields show their values next to their labels (chosen option, `[x]`/`[ ]`, values cut to 500 characters); hidden, disabled, password, file and button fields and the antiforgery token show nothing. Menus, scripts, hidden parts, the chat and `data-webmcp-ignore` elements are skipped; `truncated` says whether something was cut |
| `page_list_links` | Yes | Up to 200 same-origin links in `nav`, `header`, `aside`, `[role="navigation"]`; optional `filter` |
| `page_navigate` | No | Opens a path starting with `/` or a full same-origin address. It first sends a `HEAD` request: a bare relative path such as `ArticlesAdmin`, a 404 or 410, other origins and script addresses are refused with a message telling the agent to call `page_list_links` (a 405 or a redirect is opened as asked). It answers first and opens the page 400 ms later (`navigateDelayMs`) |
| `page_list_form_fields` | Yes | Up to 200 fields (name, id, label, type, value cut to 2,000 characters, required, choices); skips hidden, password, file, button and disabled fields and the antiforgery token |
| `page_set_form_field` | No | Sets one listed field by name, id or label, raises `input` and `change`, never submits; answers the field as it is now and its `previousValue` so the agent can undo the change |

## The AI Chat as a WebMCP Agent

1. Before each message, `ai-chat.js` reads `getTools()` and, when the list changed, calls the hub's `SetPageTools(WebMcpPageToolDescriptor[])`. It leaves out the AI tools `webmcp.js` registered, because the server already has those.
2. `IWebMcpPageToolRegistry` keeps the tools per connection after `WebMcpPageToolValidator` checks them: at most 64, names matching `^[A-Za-z0-9_.-]{1,128}$`, descriptions up to 2,000 characters, object schemas up to 16 KB. `ClearPageTools()` and a closed connection forget them.
3. `AIChatResponseHandler` puts the connection id in `AICompletionContextKeys.ChatConnectionId`.
4. The page-tool registry provider adds a `WebMcpPageToolFunction` per tool when the session's profile has `WebMcpProfileSettings.AllowPageTools`. A tool with `consequentialHint` is offered in an AG-UI run (the chat UI's default, see `crestapps-core-ag-ui`) as a tool that requires approval, so each call waits for the user's **Allow** or **Deny**; with `AllowConsequentialPageTools` it runs without asking. Other chat runs offer it only when `AllowConsequentialPageTools` is on.
5. A call invokes the client method `ExecutePageTool` (`WebMcpConstants.ExecutePageToolMethod`) with SignalR client results and returns `{ text, isError }` (`WebMcpPageToolResult`) within `WebMcpPageToolOptions.ToolCallTimeout` (60 s). For a page tool that `webmcp-page-tools.js` registered in the same window, the tab runs the tool's own `execute` (found with `CrestAppsWebMcpPageTools.getRegisteredTool(modelContext, name)`), so the model reads the tool's real failure message (for example a 404 from `page_navigate`) instead of WebMCP's generic "Tool execution failed". Every other tool (third-party, frames, a tool registered first under a page tool's name) runs through `executeTool()`.

In an AG-UI run on the chat hub, `page_navigate` ends the turn: the turn is saved with `ChatTurnToolActivity.ContinuesOnNextPage`, and the chat on the opened page continues it with that page's tools (`forwardedProps.continueRun`), up to five pages in a row. See `crestapps-core-ag-ui`.

```csharp
using CrestApps.Core.AI.Models;

profile.AlterSettings<WebMcpProfileSettings>(settings =>
{
    settings.AllowPageTools = true;
    settings.AllowConsequentialPageTools = false;
});
```

- **Names**: providers reject `.` in function names, so `WebMcpPageToolValidator.ToFunctionName` maps characters other than letters, digits, `_` and `-` to `_` and hashes names over 64 characters. `store.add_to_cart` is offered as `store_add_to_cart`; the tab still runs `store.add_to_cart`.
- **Annotations**: `readOnlyHint` is kept (`WebMcpPageTool.ReadOnly`); `untrustedContentHint` results are prefixed with `WebMcpPageToolFunction.UntrustedContentNotice` so the model treats them as data.
- **Frames**: a cross-origin frame shares tools with `exposedTo`, the page allows it with `<iframe allow="tools">`, and its origin must be in `pageToolOrigins`.
- **Web farms**: the registry is in memory; a connection and the completions of its messages run on the same server, so no shared store is needed.

## Sample Hosts

The MVC host renders the scripts in `Views/Shared/_WebMcp.cshtml` and the Blazor host in `Components/App.razor`. Both add a **WebMCP** card to the settings page (stored with `SiteSettingsConfigureStoredOptions<WebMcpOptions>`), **Allow page tools** and **Allow consequential page tools** to the profile editor, and a **WebMCP Playground** page (`/webmcp/playground`): a fake store whose `wwwroot/js/webmcp-playground.js` registers `store.search_products`, `store.get_order`, `store.read_reviews` (untrusted), `store.add_to_cart` and `store.place_order` (consequential) with the standard API only.

## Troubleshooting

- **Chat never calls page tools**: the profile lacks `AllowPageTools`, `AddPageTools<THub>()` names a different hub than the one mapped, the page loads the chat before `webmcp.js`, or the chat is a Chat Interaction.
- **Consequential tool missing**: the run is not an AG-UI run (`protocol: 'legacy'`, a hub without `RunAgent`); turn on `AllowConsequentialPageTools`, or use the default AG-UI chat so the tool waits for approval.
- **Model keeps guessing addresses**: `page_navigate` refuses bare relative paths and pages that answer 404/410; make sure the navigation links are in `nav`, `header`, `aside` or `[role="navigation"]` so `page_list_links` finds them.
- **`400` on invoke**: the antiforgery header is missing or stale; render the config block per request.
- **`404` on invoke**: the tool is not in `WebMcpOptions`, is a system or hidden tool, or `IAIToolAccessEvaluator` denies it for the user.
- **No `document.modelContext`**: load `webmcp-polyfill.js` first and serve over HTTPS.
