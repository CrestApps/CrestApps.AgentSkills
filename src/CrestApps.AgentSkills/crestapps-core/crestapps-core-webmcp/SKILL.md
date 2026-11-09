---
name: crestapps-core-webmcp
description: Skill for WebMCP in CrestApps.Core 2.0, the proposed W3C standard that lets a page give tools to browser AI agents through document.modelContext. Covers CrestApps.Core.AI.WebMcp with AddWebMcp and AddCoreAIWebMcp, MapWebMcpTools list and invoke endpoints, WebMcpOptions ExposeAllTools and Tools, IWebMcpAccessEvaluator and IAIToolAccessEvaluator checks, the CrestApps.AI.WebMcp.Resources scripts and the @crestapps/webmcp npm package (webmcp.js, webmcp-page-tools.js, webmcp-polyfill.js) with the crestapps-webmcp-config block, built-in page tools, the AI chat as a WebMCP agent with AddPageTools, WebMcpProfileSettings AllowPageTools and AllowConsequentialPageTools, consequentialHint and untrustedContentHint, function-name mapping, pageToolOrigins for cross-origin frames, SetPageTools and ExecutePageTool client results, and the sample WebMCP Playground. Use it when exposing AI tools to browser agents, letting the chat drive the page a user has open, or troubleshooting page tools that never reach the model.
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
| Page tools | `webmcp-page-tools.js` registers tools that read the page, list its links, open another page of the site, and read and fill forms |
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

Built-in page tools: `page_get_info`, `page_list_links` (same-origin navigation links, optional `filter`), `page_navigate` (same-origin only), `page_list_form_fields` (skips hidden, password, file, button and disabled fields and the antiforgery token) and `page_set_form_field` (raises `input` and `change`, never submits).

## The AI Chat as a WebMCP Agent

1. Before each message, `ai-chat.js` reads `getTools()` and, when the list changed, calls the hub's `SetPageTools(WebMcpPageToolDescriptor[])`. It leaves out the AI tools `webmcp.js` registered, because the server already has those.
2. `IWebMcpPageToolRegistry` keeps the tools per connection after `WebMcpPageToolValidator` checks them: at most 64, names matching `^[A-Za-z0-9_.-]{1,128}$`, descriptions up to 2,000 characters, object schemas up to 16 KB. `ClearPageTools()` and a closed connection forget them.
3. `AIChatResponseHandler` puts the connection id in `AICompletionContextKeys.ChatConnectionId`.
4. The page-tool registry provider adds a `WebMcpPageToolFunction` per tool when the session's profile has `WebMcpProfileSettings.AllowPageTools`; tools with `consequentialHint` also need `AllowConsequentialPageTools`.
5. A call invokes the client method `ExecutePageTool` (`WebMcpConstants.ExecutePageToolMethod`) with SignalR client results; the tab runs `executeTool()` and returns `{ text, isError }` (`WebMcpPageToolResult`) within `WebMcpPageToolOptions.ToolCallTimeout` (60 s).

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
- **Consequential tool missing**: turn on `AllowConsequentialPageTools`.
- **`400` on invoke**: the antiforgery header is missing or stale; render the config block per request.
- **`404` on invoke**: the tool is not in `WebMcpOptions`, is a system or hidden tool, or `IAIToolAccessEvaluator` denies it for the user.
- **No `document.modelContext`**: load `webmcp-polyfill.js` first and serve over HTTPS.
