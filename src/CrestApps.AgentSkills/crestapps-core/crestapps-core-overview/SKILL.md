---
name: crestapps-core-overview
description: Entry point for CrestApps.Core 2.0 in any ASP.NET Core or generic .NET host. Covers the full NuGet package map, the AddCrestAppsCore and AddAISuite builder chain, the CrestApps AI Connections and Deployments configuration layout, declaring deployment model capabilities (Features) and resolving deployments through slots with ResolveSlotAsync and AIDeploymentSlotNames, choosing a storage backend and sample host, the cross-cutting 2.0 breaking changes (AIDeploymentPurpose removal, Indexers renamed to FileSources, the new Ingestion package, consolidated FTP and SFTP packages, Vite-built chat assets), and an index of every crestapps-core skill. Use this skill first when a request mentions CrestApps.Core packages, getting started, which package or skill to use, or upgrading a CrestApps.Core 1.x host.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core Overview

You are a CrestApps.Core expert. Start here to pick packages, compose the builder chain, configure connections and deployments, and route the rest of the task to the right `crestapps-core-*` skill.

## Guidelines

- Compose everything through `services.AddCrestAppsCore(crestApps => crestApps.AddAISuite(ai => ...))`. `AddAISuite(...)` always registers `AddCoreServices()`, `AddCoreAIServices()` and `AddCoreAIOrchestration()`; every feature, provider and store is an opt-in call on a builder.
- Reference the smallest package set the feature list needs. Install the packages in the web or startup project.
- Keep provider credentials in `CrestApps:AI:Connections` and model choices in `CrestApps:AI:Deployments`.
- A deployment declares what its model can do (**capabilities**, the `Features` list). Code asks for a **slot** (`chat`, `utility`, `embedding`, `image`, `vision`, `speechToText`, `textToSpeech`, `realtime`) and lets `IAIDeploymentManager.ResolveSlotAsync(...)` pick the deployment. Never select deployments by a purpose or type; those APIs are gone in 2.0.
- Without a store package, connections and deployments come only from configuration, and nothing stores AI Profiles, chat sessions or Chat Interactions. Add `CrestApps.Core.Data.EntityCore` or `CrestApps.Core.Data.YesSql` (see `crestapps-core-data-storage`) before using those features.
- Use the MVC sample host (`CrestApps.Core.Mvc.Web`) as the broadest reference and the Blazor host (`CrestApps.Core.Blazor.Web`) for an Entity Framework Core composition.

## Smallest Useful Setup

```xml
<ItemGroup>
  <PackageReference Include="CrestApps.Core.AI.Chat" />
  <PackageReference Include="CrestApps.Core.AI.OpenAI" />
  <PackageReference Include="CrestApps.Core.Data.EntityCore" />
</ItemGroup>
```

`CrestApps.Core.AI.Chat` brings `CrestApps.Core`, `CrestApps.Core.AI` and their abstractions transitively; list them explicitly only when you prefer explicit references.

```csharp
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.OpenAI;
using CrestApps.Core.Data.EntityCore;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddEntityCoreStores()
        .AddOpenAI()
        .AddChatInteractions(chat => chat.AddEntityCoreStores())
    )
    .AddEntityCoreSqliteDataStore("Data Source=crestapps.db")
);

var app = builder.Build();
await app.Services.InitializeEntityCoreSchemaAsync();
```

A console or worker that only calls `IAICompletionService` needs no store: `AddAISuite(ai => ai.AddOpenAI())` and configured deployments are enough.

## Configuration with Capabilities

```json
{
  "CrestApps": {
    "AI": {
      "Connections": [
        {
          "Name": "primary-openai",
          "ClientName": "OpenAI",
          "ApiKey": "YOUR_API_KEY"
        }
      ],
      "Deployments": [
        {
          "Name": "gpt-4.1",
          "ConnectionName": "primary-openai",
          "ModelName": "gpt-4.1",
          "Features": [ "textGeneration", "toolCalling", "streaming", "imageInput" ]
        },
        {
          "Name": "gpt-4.1-mini",
          "ConnectionName": "primary-openai",
          "ModelName": "gpt-4.1-mini",
          "Features": [ "textGeneration", "toolCalling", "streaming" ]
        },
        {
          "Name": "text-embedding-3-small",
          "ConnectionName": "primary-openai",
          "ModelName": "text-embedding-3-small",
          "Features": [ "textEmbedding" ]
        }
      ]
    }
  }
}
```

- `Features` is shorthand for `Properties:AIDeploymentMetadata:Features`; both forms merge. Feature names are the constants in `AIDeploymentFeatureNames` (`textGeneration`, `toolCalling`, `textEmbedding`, `speechToText`, `textToSpeech`, `structuredOutputs`, `streaming`, `reasoning`, `imageInput`, `imageOutput`, `audioInput`, `audioOutput`, `videoInput`, `videoOutput`, `realtime`).
- `textGeneration` is opt-out. A deployment with no capability metadata at all is unconstrained and can fill `chat` and `utility`. Every other capability is opt-in, so an embedding, image, speech or realtime deployment must declare it.
- Once a deployment declares any `Features`, it fills only the slots whose required feature it lists. A deployment that declares `realtime` never fills `chat` or `utility`.
- Use `ConnectionName` to point at a shared connection. A deployment may instead carry contained connection settings (`ClientName`, `Endpoint`, `ApiKey`, and so on) directly.

## Resolve a Deployment by Slot

```csharp
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.AI;

namespace MyApp.Services;

public sealed class QuickAnswerService
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAICompletionService _completionService;

    public QuickAnswerService(
        IAIDeploymentManager deploymentManager,
        IAICompletionService completionService)
    {
        _deploymentManager = deploymentManager;
        _completionService = completionService;
    }

    public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        var deployment = await _deploymentManager.ResolveSlotAsync(
            AIDeploymentSlotNames.Chat,
            cancellationToken: cancellationToken);

        if (deployment is null)
        {
            return "No deployment can fill the chat slot. Check CrestApps:AI:Deployments.";
        }

        var response = await _completionService.CompleteAsync(
            deployment,
            [new ChatMessage(ChatRole.User, question)],
            new AICompletionContext(),
            cancellationToken);

        return response.Text;
    }
}
```

Resolution order for a slot is: the explicit deployment name, the slot's site default from `DefaultAIDeploymentSettings` (for example `DefaultChatDeploymentName`), the fallback slot's explicit name and default (`utility` falls back to `chat`), and only then the first capable deployment. Use `GetAllBySlotAsync(slotName)` to fill a picker, and `ResolveSlotOrThrowAsync(slotName)` from `AIDeploymentManagerExtensions` when a missing deployment is a configuration error. Details live in `crestapps-core-ai-model-capabilities`.

## Package Map

| Area | Packages | Skill |
|---|---|---|
| Foundation | `CrestApps.Core.Abstractions`, `CrestApps.Core`, `CrestApps.Core.Infrastructure.Abstractions`, `CrestApps.Core.Infrastructure`, `CrestApps.Core.Support` | `crestapps-core-foundations` |
| AI runtime | `CrestApps.Core.AI.Abstractions`, `CrestApps.Core.AI` | `crestapps-core-ai-runtime` |
| Chat and sessions | `CrestApps.Core.AI.Chat` | `crestapps-core-chat`, `crestapps-core-chat-interactions` |
| Realtime voice | `CrestApps.Core.AI.WebRTC` (plus `CrestApps.Core.AI.Chat`) | `crestapps-core-realtime-voice` |
| SignalR hubs | `CrestApps.Core.SignalR` | `crestapps-core-signalr` |
| Templates | `CrestApps.Core.Templates` | `crestapps-core-templates`, `crestapps-core-ai-templates` |
| Markdown | `CrestApps.Core.AI.Markdown` | `crestapps-core-ai-markdown` |
| Resilience | `CrestApps.Core.AI.Resilience` | `crestapps-core-resilience` |
| Ingestion | `CrestApps.Core.AI.Ingestion`, `CrestApps.Core.AI.Ingestion.Pdf`, `CrestApps.Core.Azure.DocumentIntelligence`, `CrestApps.Core.DataIngestion` | `crestapps-core-document-processing` |
| Documents and generated files | `CrestApps.Core.AI.Documents`, `CrestApps.Core.AI.Documents.OpenXml`, `CrestApps.Core.AI.Documents.Pdf`, `CrestApps.Core.AI.Documents.Word` | `crestapps-core-document-processing`, `crestapps-core-document-agents` |
| File sources | `CrestApps.Core.AI.FileSources`, `CrestApps.Core.AI.Ftp`, `CrestApps.Core.AI.Sftp` | `crestapps-core-file-sources`, `crestapps-core-mcp-file-resources` |
| Web crawlers | `CrestApps.Core.AI.WebCrawlers` | `crestapps-core-web-crawlers` |
| MCP | `CrestApps.Core.AI.Mcp`, `CrestApps.Core.AI.Mcp.Knowledge` | `crestapps-core-mcp` |
| A2A | `CrestApps.Core.AI.A2A` | `crestapps-core-a2a` |
| Providers | `CrestApps.Core.AI.OpenAI`, `CrestApps.Core.AI.OpenAI.Azure`, `CrestApps.Core.AI.AzureAIInference`, `CrestApps.Core.AI.Ollama`, `CrestApps.Core.Azure` | provider skills below |
| Orchestrators | `CrestApps.Core.AI.Claude`, `CrestApps.Core.AI.Copilot` | `crestapps-core-claude-orchestrator`, `crestapps-core-copilot-orchestrator` |
| Search backends | `CrestApps.Core.Elasticsearch` + `CrestApps.Core.AI.Elasticsearch`, `CrestApps.Core.Azure.AISearch` + `CrestApps.Core.AI.Azure.AISearch`, `CrestApps.Core.PostgreSQL` + `CrestApps.Core.AI.PostgreSQL` | `crestapps-core-elasticsearch`, `crestapps-core-azure-ai-search`, `crestapps-core-postgresql` |
| Storage | `CrestApps.Core.Data.EntityCore`, `CrestApps.Core.Data.YesSql` | `crestapps-core-entitycore-stores`, `crestapps-core-yessql-stores` |
| Chat UI assets | npm `@crestapps/ai-chat-ui` (the `CrestApps.AI.Resources` Razor class library is not published to NuGet) | `crestapps-core-aspnet-hosting` |

## Common Builder Calls

| Need | Builder call | Package |
|---|---|---|
| Chat sessions, Chat Interactions, hubs | `.AddChatInteractions(...)` | `CrestApps.Core.AI.Chat` |
| Uploaded documents and document RAG | `.AddDocumentProcessing(dp => dp.AddOpenXml().AddPdf().AddWord())` | `CrestApps.Core.AI.Documents*` |
| Ingestion only (no chat document stack) | `services.AddCoreAIDocumentIngestion(i => i.AddPlainTextReader().AddPdf())` | `CrestApps.Core.AI.Ingestion*` |
| Long-term user memory | `.AddAIMemory(...)` | `CrestApps.Core.AI` |
| Personas | `.AddPersonas(personas => personas.AddYesSqlStores())` | `CrestApps.Core.AI` |
| User-configured tool instances | `.AddToolInstances(...)` | `CrestApps.Core.AI` |
| MCP client or server | `.AddMcpClient(...)`, `.AddMcpServer(...)` | `CrestApps.Core.AI.Mcp` |
| Remote A2A agents | `.AddA2AClient(...)` | `CrestApps.Core.AI.A2A` |
| SignalR with commit filter | `.AddSignalR(addStoreCommitterFilter: true)` | `CrestApps.Core.SignalR` |
| Search backends | `.AddIndexingServices(i => i.AddElasticsearch(...))` on the root builder | search packages |
| Durable stores | `.AddEntityCoreStores()` or `.AddYesSqlStores()` per feature builder, plus `.AddEntityCoreSqliteDataStore(...)` or `.AddYesSqlDataStore(...)` on the root builder | store packages |

## Skill Index

Hosting and infrastructure

- `crestapps-core-foundations` - catalogs, catalog managers, handlers, `IStoreCommitter`, `IUserAccessor`, OData validation, utilities.
- `crestapps-core-aspnet-hosting` - MVC, Razor Pages, Blazor, Minimal API composition, middleware order, endpoints, chat assets.
- `crestapps-core-sample-hosts` - the MVC, Blazor, Aspire and protocol sample projects and how to copy from them.
- `crestapps-core-data-storage` - choosing a backend and per-feature store registration.
- `crestapps-core-entitycore-stores` - Entity Framework Core stores, schema and model extension.
- `crestapps-core-yessql-stores` - YesSql stores, collections, indexes and schema creation.
- `crestapps-core-extensible-entities` - `ExtensibleEntity.Properties` metadata and JSON options.
- `crestapps-core-templates` - the general Liquid template engine.
- `crestapps-core-signalr` - SignalR registration and hub routes.
- `crestapps-core-resilience` - retry middleware for AI clients.

AI runtime

- `crestapps-core-ai-runtime` - connections, deployments, client factory, completion services.
- `crestapps-core-ai-model-capabilities` - deployment capabilities, parameters and slots.
- `crestapps-core-ai-usage-metering` - usage records, labels and reports for every AI request.
- `crestapps-core-ai-profiles` - AI Profiles.
- `crestapps-core-ai-personas` - reusable personas linked from profiles and interactions.
- `crestapps-core-ai-templates` - AI prompt and profile templates on top of `crestapps-core-templates`.
- `crestapps-core-orchestration`, `crestapps-core-context-builders`, `crestapps-core-response-handlers` - the orchestration pipeline.
- `crestapps-core-custom-tools`, `crestapps-core-tool-instances`, `crestapps-core-ai-agents` - tools and agents.
- `crestapps-core-ai-memory`, `crestapps-core-prompt-security`, `crestapps-core-ai-markdown` - memory, safety and Markdown handling.

Chat and voice

- `crestapps-core-chat`, `crestapps-core-chat-interactions`, `crestapps-core-external-relays`, `crestapps-core-realtime-voice`.

Knowledge and documents

- `crestapps-core-document-processing`, `crestapps-core-document-agents`, `crestapps-core-file-sources`, `crestapps-core-web-crawlers`, `crestapps-core-data-sources`.
- Search backends: `crestapps-core-elasticsearch`, `crestapps-core-azure-ai-search`, `crestapps-core-postgresql`.

Protocols and providers

- `crestapps-core-mcp`, `crestapps-core-mcp-file-resources`, `crestapps-core-a2a`.
- `crestapps-core-openai`, `crestapps-core-azure-openai`, `crestapps-core-azure-ai-inference`, `crestapps-core-ollama`, `crestapps-core-claude-orchestrator`, `crestapps-core-copilot-orchestrator`.

For Orchard Core modules built on CrestApps.Core, use the `orchardcore-ai` family of skills instead.

## Upgrading from 1.x

- **Deployment purpose is gone.** `AIDeploymentPurpose`, `AIDeploymentType`, `AIDeployment.Purpose`, `AIDeployment.Type`, `SupportsPurpose`, `SupportsType` and `CanServeTextCompletion()` are removed. Replace `ResolveOrDefaultAsync(AIDeploymentPurpose.X, ...)` with `ResolveSlotAsync(AIDeploymentSlotNames.X, ...)`, `GetByPurposeAsync` and `GetAllByPurposeAsync` with `GetAllBySlotAsync`, and `ResolveAsync(purpose, ...)` with `ResolveSlotOrThrowAsync(slotName, ...)`. Declare `Features` on each configured deployment instead of a purpose or type key. Stored records and configuration that still carry the old keys are projected onto capabilities when read, so no data migration is needed.
- **`ChatMode.Realtime` and `AIProfile.RealtimeDeploymentName` are removed.** Realtime follows from a deployment declaring `realtime` and from the `realtime` slot; see `crestapps-core-realtime-voice`.
- **`CrestApps.Core.AI.Indexers` is renamed `CrestApps.Core.AI.FileSources`**, and its configuration section moves from `CrestApps:Indexers` to `CrestApps:AI:FileSources`. `AddCoreFileSources(builder.Configuration)` does not read `CrestApps:Indexers`: its legacy fallback is `CrestApps:File sources`, so move existing settings to `CrestApps:AI:FileSources`.
- **The ingestion pipeline moved into `CrestApps.Core.AI.Ingestion`.** `IDocumentFileStore`, `FileSystemFileStore`, `DocumentFileSystemFileStoreOptions`, `IImageAnalysisService`, `ChatDocumentsOptions` and `ExtractorExtension` now live in the `CrestApps.Core.AI.Ingestion` namespace; update the `using` directives. The PDF reader moved to `CrestApps.Core.AI.Ingestion.Pdf`.
- **`CrestApps.Core.AI.Mcp.Ftp` and `CrestApps.Core.AI.Mcp.Sftp` are replaced by `CrestApps.Core.AI.Ftp` and `CrestApps.Core.AI.Sftp`.** Registration method names are unchanged.
- **The `Ingested` data source type is renamed `File`** (`AIDataSourceSourceTypes.File`).
- **Chat scripts are built with Vite for current browsers** (Chrome and Edge 111, Firefox 114, Safari 16.4), and the `*.css.map` files are gone. Script URLs and globals are unchanged.
- **Tool authorization moved.** AI Sessions no longer filter tools per user; the AI Profile is the authorization boundary. `IAIToolAccessEvaluator` now governs Chat Interaction sends.
