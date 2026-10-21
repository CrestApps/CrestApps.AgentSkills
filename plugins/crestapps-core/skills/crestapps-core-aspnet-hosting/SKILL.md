---
name: crestapps-core-aspnet-hosting
description: Skill for composing CrestApps.Core 2.0 into ASP.NET Core MVC, Razor Pages, Blazor Web App, Minimal API, and .NET MAUI hybrid hosts. Covers Program.cs ordering, AddCrestAppsCore with a store and commit filters, middleware order (UseWebSockets before UseRouting, UseAIAnonymousVisitorCookie, rate limiting), chat hub classes derived from AIChatHubCore and ChatInteractionHubBase with ConfigureCrestAppsChatHubOptions, the WebRTC realtime transport, the document and knowledge-figure endpoints with their resource-based authorization handlers, MCP endpoint mapping, the @crestapps/ai-chat-ui browser assets, live site-settings options through IOptionsMonitor, and which helpers are sample-host code rather than package APIs. Use it when wiring CrestApps.Core into a new or existing web application or troubleshooting host startup.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core ASP.NET Hosting

You are a CrestApps.Core expert. Register host services first, compose CrestApps through `AddCrestAppsCore(...)`, then map only the hubs and endpoints for the features the host enables.

## Rules

- Packages go in the web project. Pick a provider package, the feature packages, and one store package (`CrestApps.Core.Data.EntityCore` or `CrestApps.Core.Data.YesSql`) when records must persist.
- The service registrations are identical for MVC, Razor Pages, Blazor and Minimal APIs; only the UI and endpoint layer differs.
- Add a commit boundary for every write path: `AddCrestAppsStoreCommitterFilter()` for MVC, `.AddSignalR(addStoreCommitterFilter: true)` for hubs, `StoreCommitterEndpointFilter` for Minimal API groups, and an explicit `IStoreCommitter.CommitAsync()` in background scopes.
- Call `UseWebSockets()` before `UseRouting()`. HTTP/2 WebSocket handshakes (extended CONNECT) are otherwise rejected with 405, which breaks the realtime WebSocket transport.
- Call `UseAIAnonymousVisitorCookie()` (from `CrestApps.Core.AI.Chat`) after routing and before authentication when anonymous visitors chat, so rate limiting and session reuse get a stable visitor key.
- Every hub type that is mapped needs `ConfigureCrestAppsChatHubOptions<THub>()` (or `ConfigureChatHubOptions<THub>()` on the Chat Interactions builder). `HubOptions<THub>` is per hub type; an unconfigured hub runs on SignalR defaults (one parallel invocation per client), which deadlocks the realtime WebRTC handshake.
- The document and figure endpoints authorize against resources through `IAuthorizationService`. Register `IAuthorizationHandler` implementations that grant the requirements, or every download and upload is forbidden.

## MVC Program.cs

```csharp
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Documents.Endpoints;
using CrestApps.Core.AI.Documents.OpenXml;
using CrestApps.Core.AI.Documents.Pdf;
using CrestApps.Core.AI.OpenAI;
using CrestApps.Core.Data.EntityCore;
using CrestApps.Core.SignalR;
using MyApp.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization();
builder.Services.AddControllersWithViews()
    .AddCrestAppsStoreCommitterFilter();
builder.Services.AddHttpContextAccessor();

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddEntityCoreStores()
        .AddOpenAI()
        .AddChatInteractions(chat => chat
            .AddEntityCoreStores()
            .ConfigureChatHubOptions<ChatInteractionHub>())
        .AddDocumentProcessing(documents => documents
            .AddEntityCoreStores()
            .AddOpenXml()
            .AddPdf())
        .AddSignalR(addStoreCommitterFilter: true))
    .AddIndexingServices(indexing => indexing.AddEntityCoreStores())
    .AddEntityCoreSqliteDataStore(
        $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "App_Data", "crestapps.db")}"));

builder.Services.ConfigureCrestAppsChatHubOptions<AIChatHub>();

var app = builder.Build();

await app.Services.InitializeEntityCoreSchemaAsync();

app.UseHttpsRedirection()
    .UseStaticFiles()
    .UseWebSockets()
    .UseRouting()
    .UseAIAnonymousVisitorCookie()
    .UseAuthentication()
    .UseAuthorization();

app.MapHub<AIChatHub>("/hubs/ai-chat");
app.MapHub<ChatInteractionHub>("/hubs/chat-interaction");

app.AddDownloadAIDocumentEndpoint()
    .AddDownloadAIDocumentFigureEndpoint()
    .AddDownloadKnowledgeFigureEndpoint()
    .AddUploadChatInteractionDocumentEndpoint()
    .AddRemoveChatInteractionDocumentEndpoint()
    .AddUploadChatSessionDocumentEndpoint()
    .AddRemoveChatSessionDocumentEndpoint();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();
```

For YesSql, swap the store calls for `.AddYesSqlStores()` and `.AddYesSqlDataStore(configuration => configuration.UseSqLite(...))`, then create the index tables before serving requests (see `crestapps-core-yessql-stores`).

## Chat Hubs

The packages ship hub base classes; the host owns the concrete hub, its route and its authorization.

```csharp
using CrestApps.Core.AI.Chat.Hubs;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Authorization;

namespace MyApp.Hubs;

[AllowAnonymous]
public sealed class AIChatHub : AIChatHubCore<IAIChatHubClient>
{
    public AIChatHub(IServiceProvider services, TimeProvider timeProvider, ILogger<AIChatHub> logger)
        : base(services, timeProvider, logger)
    {
    }

    protected override Task<bool> AuthorizeProfileAsync(IServiceProvider services, AIProfile profile)
    {
        return Task.FromResult(Context.User?.Identity?.IsAuthenticated == true);
    }
}

[Authorize]
public sealed class ChatInteractionHub : ChatInteractionHubBase
{
    public ChatInteractionHub(IServiceProvider services, TimeProvider timeProvider, ILogger<ChatInteractionHub> logger)
        : base(services, timeProvider, logger)
    {
    }
}
```

`.AddSignalR(...)` on the AI suite calls `AddCoreSignalR(pathPrefix)`, which registers `HubRouteManager`, sets host-wide `MaximumParallelInvocationsPerClient = 8` and `StreamBufferCapacity = 64`, and switches the JSON protocol to camelCase. Hub-level overrides and route management are covered in `crestapps-core-signalr`; realtime voice in `crestapps-core-realtime-voice`.

For speech-to-speech over WebRTC, also call `builder.Services.AddWebRtcRealtimeTransport()` (package `CrestApps.Core.AI.WebRTC`). It binds ICE servers from `CrestApps:AI:RealtimeTransport`; the browser falls back to WebSocket when WebRTC is unavailable.

## Document Endpoint Authorization

| Endpoint extension | Route | Requirement and resource |
|---|---|---|
| `AddDownloadAIDocumentEndpoint()` | `GET ai/documents/{documentId}/download` | `AIChatDocumentOperations.ManageDocuments` on `ChatInteraction`, `AIProfile`, or `AIChatSessionDocumentAuthorizationContext` |
| `AddDownloadAIDocumentFigureEndpoint()` | `GET ai/documents/{documentId}/figures/{figureId}` | same as above |
| `AddDownloadKnowledgeFigureEndpoint()` | `GET ai/knowledge/{dataSourceId}/figures/{canonicalId}` | `AIKnowledgeOperations.ViewFigures` on `AIDataSource` |
| `AddUploadChatInteractionDocumentEndpoint()`, `AddRemoveChatInteractionDocumentEndpoint()` | `POST ai/chat-interactions/upload-document`, `.../remove-document` | `ManageDocuments` on `ChatInteraction` |
| `AddUploadChatSessionDocumentEndpoint()`, `AddRemoveChatSessionDocumentEndpoint()` | `POST ai/chat-sessions/upload-document`, `.../remove-document` | `ManageDocuments` on `AIChatSessionDocumentAuthorizationContext` |

```csharp
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace MyApp.Security;

public sealed class ChatInteractionDocumentAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, ChatInteraction>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        ChatInteraction resource)
    {
        if (resource is not null &&
            requirement.Name == AIChatDocumentOperations.ManageDocuments.Name &&
            context.User.IsInRole("Administrator"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

```csharp
builder.Services.AddScoped<IAuthorizationHandler, ChatInteractionDocumentAuthorizationHandler>();
```

Write one handler per resource type the host serves, using whatever rule decides who may use that profile, interaction, session or data source.

## Other Application Models

- **Blazor Web App**: `AddRazorComponents().AddInteractiveServerComponents()`, the same `AddCrestAppsCore(...)` block, then `UseAntiforgery()` after authorization and `MapRazorComponents<App>().AddInteractiveServerRenderMode()`. Inject `IAICompletionService`, `IOrchestrator` or `IAIClientFactory` into components.
- **Razor Pages**: inject the same services into page models.
- **Minimal APIs**: `app.MapGroup("/api").AddEndpointFilter<StoreCommitterEndpointFilter>()` around handlers that write through a staged store. Do not add it when the store persists each write immediately.
- **MCP server**: register the SDK server with `services.AddMcpServer(...).WithHttpTransport().WithCrestAppsHandlers()`, keep `.AddMcpServer(...)` on the AI suite for CrestApps prompts and resources, and map `app.MapMcp("mcp")`. Authentication for `/mcp` is host middleware. See `crestapps-core-mcp`.
- **.NET MAUI or Blazor Hybrid**: register the same services in the app host; there is no hub or endpoint layer.

## Chat UI Assets

The chat scripts and styles (`ai-chat.js`, `ai-chat-widget.js`, `chat-interaction.js`, `chat-markers.js`, `realtime-audio.js`, `ai-chat.css`, `chat-widget.css`, and others) ship in the npm package `@crestapps/ai-chat-ui`. The sample hosts serve the same files from the `CrestApps.AI.Resources` project as `/_content/CrestApps.AI.Resources/scripts/...` and `/styles/...`; that Razor class library is not published to NuGet, so a package-based host serves the npm build from its own `wwwroot`.

- The 2.0 scripts are built with Vite for current browsers (Chrome and Edge 111, Firefox 114, Safari 16.4). They still load with a plain `<script>` tag and expose the same globals (`coreAIChatManager`, `CoreAIChatMarkers`, `CoreAIRealtime`).
- Pin the SignalR browser client to an exact version with an `integrity` hash, as the samples do with `@microsoft/signalr@10.0.11`.

## Live Site-Settings Options

When admin-managed settings feed options (for example `DefaultAIDeploymentSettings`, `GeneralAIOptions`, `PromptSecurityOptions`), consume `IOptionsMonitor<TOptions>` and register an `IOptionsChangeTokenSource<TOptions>` that fires when the settings store saves. The samples do this with `SiteSettingsStore` and `SiteSettingsOptionsChangeTokenSource<TOptions>`; copy the pattern rather than the sample types.

## Sample-Host Code, Not Package APIs

Do not tell users to call these from a package; they live in `src/Startup` and must be copied or rewritten: `AddSharedSampleHostDefaults()`, `AddSharedSiteSettings(...)`, `AddMvcSampleHostServices(...)`, `AddBlazorSampleHostServices(...)`, `AddSharedAIChatProtection()`, `UseSharedAIChatProtection()`, `AddA2AHost()`, `MapA2AHost()`, `AddChatApiEndpoints()`, `InitializeYesSqlSchemaAsync()`, `SeedArticlesAsync()`, and `SampleHostContentRootResolver`. `InitializeEntityCoreSchemaAsync()`, `ConfigureCrestAppsChatHubOptions<THub>()`, `UseAIAnonymousVisitorCookie()`, `AddWebRtcRealtimeTransport()` and the `Add*Endpoint()` document extensions are package APIs. See `crestapps-core-sample-hosts`.

## Upgrading from 1.x

- `AddSharedTemplateProviders()` is gone from the samples. `AIProfileSystemPromptTemplateProvider` moved into `CrestApps.Core.AI` and `AddCoreAITemplating()` registers it, so a package-based host renders saved system-prompt templates without extra code.
- The `ai/documents/{documentId}/download` endpoint now also authorizes AI Profile documents; register a handler for `AIProfile` resources if profile documents should be downloadable.
- Add `UseWebSockets()` before `UseRouting()` and `ConfigureCrestAppsChatHubOptions<THub>()` for every mapped chat hub when the host offers realtime voice.
- Chat assets lost their stylesheet source maps and the Babel helper globals; nothing else about the script URLs changed.
