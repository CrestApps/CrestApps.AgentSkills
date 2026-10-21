---
name: crestapps-core-sample-hosts
description: Skill for the CrestApps.Core 2.0 sample hosts under src/Startup (CrestApps.Core.Mvc.Web with YesSql, CrestApps.Core.Blazor.Web with Entity Framework Core, CrestApps.Core.Aspire.AppHost with Ollama, Redis and pgvector, the MCP and A2A sample clients, and the shared CrestApps.Core.Startup.Shared library). Covers what each host composes, how to run them, App_Data layout, which helpers are sample-only and must be rewritten in production, how to translate a sample Program.cs into a package-based host, and how to rebuild the Vite-built chat assets with npm. Use it when a request mentions the CrestApps.Core samples, running the repository locally, copying sample code, or building the front-end assets.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Sample Hosts

You are a CrestApps.Core expert. Point developers at the right sample, then separate the framework calls they should copy from the sample plumbing they should replace.

## Sample Project Map

| Project | Path | Composition | Best use |
|---|---|---|---|
| MVC reference host | `src/Startup/CrestApps.Core.Mvc.Web` | Every feature on YesSql over SQLite (`App_Data/crestapps.db`) | Broadest end-to-end reference |
| Blazor reference host | `src/Startup/CrestApps.Core.Blazor.Web` | The same features on Entity Framework Core over SQLite (`App_Data/crestapps-blazor.db`) | Blazor Server and EF Core reference |
| Aspire app host | `src/Startup/CrestApps.Core.Aspire.AppHost` | Both web hosts plus Ollama, Redis, PostgreSQL with pgvector, and both protocol clients | Composed local environment |
| MCP sample client | `src/Startup/CrestApps.Core.Mvc.Samples.McpClient` | Razor Pages client using the MCP SDK, with a server selector | Exercising a host's `/mcp` endpoint |
| A2A sample client | `src/Startup/CrestApps.Core.Mvc.Samples.A2AClient` | Razor Pages client for agent cards and tasks, with a server selector | Exercising a host's A2A endpoints |
| Shared sample library | `src/Startup/CrestApps.Core.Startup.Shared` | Site settings store, option bridges, sample authorization handlers, article demo | Patterns to copy, not to reference |

Both web hosts register OpenAI, Azure OpenAI, Ollama, Azure AI Inference, Claude and Copilot orchestrators, Chat Interactions, document processing with OpenXml, PDF, Word and reference downloads, AI memory, personas, tool instances, A2A client and host, MCP client and server (with FTP, SFTP and knowledge resources), SignalR, Elasticsearch, Azure AI Search and PostgreSQL search, web crawlers, and file sources with file-system, FTP and SFTP connectors.

## Run the Samples

```bash
dotnet run --project ./src/Startup/CrestApps.Core.Mvc.Web/CrestApps.Core.Mvc.Web.csproj
dotnet run --project ./src/Startup/CrestApps.Core.Blazor.Web/CrestApps.Core.Blazor.Web.csproj
dotnet run --project ./src/Startup/CrestApps.Core.Aspire.AppHost/CrestApps.Core.Aspire.AppHost.csproj
```

- The MVC host listens on `https://localhost:5100` and the Blazor host on `https://localhost:5200` from their launch profiles. Under Aspire they use `https://localhost:7150` and `https://localhost:5201`, which the MCP and A2A clients target.
- `SampleHostContentRootResolver.ResolveContentRoot(...)` finds the project folder, so the commands work from the repository root.
- The sample admin account is configured under `CrestApps:Admin` in each host's `appsettings.json`.
- Put credentials in `App_Data/appsettings.json`; it is layered over the normal configuration chain. Set `CrestApps:AppDataPath` to move `App_Data` outside the source tree.
- Aspire needs a container runtime. It pulls `deepseek-v2:16b` into Ollama on first run and wires it through `CrestApps__AI__Providers__Ollama__Connections__Default__ChatDeploymentName`, so a chat deployment exists without manual configuration. Change `ollamaModelName` in the app host `Program.cs` for a smaller model.
- With your own Ollama, the deployment `ModelName` must match a pulled model exactly, or the provider returns 404.

## What Each Host Does at Startup

1. `builder.AddSharedSampleHostDefaults()` - NLog, `App_Data` creation, `App_Data/appsettings.json`, data-protection keys under `App_Data/DataProtection-Keys`, and `AddSharedSiteSettings(appDataPath)` (the `SiteSettingsStore` plus `IConfigureOptions` bridges for `GeneralAIOptions`, `DefaultAIDeploymentSettings`, `PromptSecurityOptions`, `AIVisitorIdentityOptions` and others, refreshed through `SiteSettingsOptionsChangeTokenSource<TOptions>`).
2. Host services, authentication with an `Admin` policy, `AddSharedAIChatProtection()` (endpoint rate limiting) and `AddWebRtcRealtimeTransport()`.
3. `AddMvcSampleHostServices(appDataPath)` or `AddBlazorSampleHostServices(appDataPath)` - the backing store (`AddCoreYesSqlDataStore(...)` or `AddCoreEntityCoreSqliteDataStore(...)`), the sample document and figure authorization handlers, article demo services, and option bridges for A2A, MCP, Copilot and Claude.
4. `AddCrestAppsCore(...)` with the full AI suite and indexing composition, then `ConfigureCrestAppsChatHubOptions<AIChatHub>()`, `AddCoreWebCrawlers()`, `AddCoreFileSources(builder.Configuration)` with connectors, and the web-crawler and file-source stores.
5. After `Build()`: `InitializeYesSqlSchemaAsync()` (MVC, defined in the sample) or `InitializeEntityCoreSchemaAsync()` (Blazor, a package API), then `SeedArticlesAsync()`.
6. Pipeline: HTTPS, static files, `UseWebSockets()`, routing, `UseSharedAIChatProtection()` (anonymous visitor cookie plus rate limiter), authentication, authorization, `/mcp` authentication middleware, the two hubs, `MapMcp("mcp")`, `MapA2AHost()`, and the chat and document endpoints.

## Translate a Sample into Production Code

Copy these package calls as they are: `AddCrestAppsCore`, `AddAISuite` and every feature builder call, `AddIndexingServices`, `AddEntityCoreSqliteDataStore` or `AddYesSqlDataStore`, `ConfigureCrestAppsChatHubOptions<THub>()`, `AddWebRtcRealtimeTransport()`, `AddCoreWebCrawlers()`, `AddCoreFileSources(builder.Configuration)`, the store registrations, `InitializeEntityCoreSchemaAsync()`, `UseAIAnonymousVisitorCookie()`, `WithCrestAppsHandlers()`, and the `Add*Endpoint()` document extensions.

Rewrite these for your host, because they exist only in `src/Startup`:

| Sample helper | Replace with |
|---|---|
| `AddSharedSampleHostDefaults()`, `AddSharedSiteSettings(...)` | Your logging, configuration, data protection, and a settings store that signals `IOptionsMonitor` |
| `AddMvcSampleHostServices(...)`, `AddBlazorSampleHostServices(...)` | Direct store registration plus your own `IAuthorizationHandler` implementations |
| `Sample*AuthorizationHandler` types | Handlers that apply your real access rules (the samples grant most document access to administrators only) |
| `InitializeYesSqlSchemaAsync()` | Your YesSql schema step using the package `Create*IndexSchemaAsync` extensions |
| `AddA2AHost()`, `MapA2AHost()`, A2A API key handlers | Your A2A host endpoints and authentication; see `crestapps-core-a2a` |
| `AddChatApiEndpoints()` | Your own session and profile endpoints |
| `AIChatHub`, `ChatInteractionHub` | Your hubs derived from `AIChatHubCore<IAIChatHubClient>` and `ChatInteractionHubBase` |
| `/mcp` authentication middleware | Your MCP authentication; see `crestapps-core-mcp` |
| `SeedArticlesAsync()`, article index profile | Nothing; the article catalog is demo data |

`crestapps-core-aspnet-hosting` shows a minimal package-based `Program.cs`.

## Front-End Assets

The chat scripts and stylesheets are sources under `src/Resources/CrestApps.AI.Resources/Assets/` built into its committed `wwwroot/` with Vite (Node.js 24, npm 11). Package consumers never build them; they use the `@crestapps/ai-chat-ui` npm package. Contributors changing a script use `npm install`, `npm run build`, `npm run lint` and `npm run check`, and commit the rebuilt `wwwroot` files. See [building-assets.md](references/building-assets.md) for commands, the `Assets.json` format, and troubleshooting.

## Upgrading from 1.x Samples

- The samples now register file sources (`AddCoreFileSources(builder.Configuration)` reading `CrestApps:AI:FileSources`, replacing `CrestApps:Indexers`), web crawlers, personas, tool instances, and PostgreSQL search. The manual **Upload files** action on a data source is gone; file sources replace it.
- `AddSharedTemplateProviders()` is removed; `AddCoreAITemplating()` now registers the system-prompt template provider.
- Deployment editors ask for model capabilities only; there is no purpose field.
- The front-end build moved from Gulp and Babel to Vite. `Assets.json` uses `action`, `name`, `source` and `dest`; the old `inputs`, `output` and `generateSourceMaps` keys are not read.
- Both hosts pin `@microsoft/signalr` to `10.0.11` with an `integrity` hash instead of `@latest`.
