---
name: crestapps-core-a2a
description: Skill for Agent-to-Agent (A2A) protocol support in CrestApps.Core on the A2A 1.0 SDK. Covers AddA2AClient and AddCoreAIA2AClient with Entity Framework Core or YesSql connection stores, A2AConnection and A2AConnectionMetadata authentication, AIProfileA2AMetadata, the agent card cache and A2AToolRegistryProvider, the listAvailableAgents, findAgentForTask and findToolsForTask tools, building an A2A host with IAgentHandler, A2AServer, ITaskStore and MapA2A, A2A 1.0 agent cards with supportedInterfaces published at /.well-known/agent-card.json, A2AHostOptions authentication and ExposeAgentsAsSkill, system agents exposed through the host, and upgrading from the 0.3 SDK.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core A2A

Use A2A when the remote system is an agent that reasons on its own (its own model, tools and context). Use MCP (`crestapps-core-mcp`) when the remote system exposes tools or resources. `CrestApps.Core.AI.A2A` ships the client; hosting is application code built on the `A2A` and `A2A.AspNetCore` 1.0 SDK packages, using `A2AHostOptions` from the framework.

## Client

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddA2AClient(a2a => a2a
            .AddEntityCoreStores()))   // or .AddYesSqlStores(); registers ICatalog<A2AConnection>
    .AddEntityCoreSqliteDataStore("Data Source=app.db"));
```

`AddCoreAIA2AClient()` registers a resilient named `HttpClient`, `IMemoryCache`, `A2AAICompletionContextBuilderHandler`, `A2AToolRegistryProvider`, `IA2AAgentCardCacheService` (15-minute cache), `IA2AConnectionAuthService`, and the system tools `listAvailableAgents`, `findAgentForTask` and `findToolsForTask`.

### Connections

```csharp
using CrestApps.Core;
using CrestApps.Core.AI.A2A;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Models;

var protector = dataProtectionProvider.CreateProtector(A2AConstants.DataProtectionPurpose); // "A2AClientConnection"

var connection = new A2AConnection
{
    DisplayText = "Legal Review Agent",
    Endpoint = "https://agents.example.com/",
};

connection.Put(new A2AConnectionMetadata
{
    AuthenticationType = ClientAuthenticationType.ApiKey,
    ApiKeyHeaderName = "Authorization",
    ApiKeyPrefix = "Bearer",
    ApiKey = protector.Protect(apiKey),
});

await connectionCatalog.CreateAsync(connection);

profile.Put(new AIProfileA2AMetadata { ConnectionIds = [connection.ItemId] });
```

- Authentication types are `Anonymous`, `ApiKey`, `Basic`, `OAuth2ClientCredentials`, `OAuth2PrivateKeyJwt`, `OAuth2Mtls` and `CustomHeaders`. Protect every secret with the `A2AClientConnection` purpose.
- The card is fetched with `A2ACardResolver` from `connection.Endpoint` (the SDK reads `/.well-known/agent-card.json` under it) and each skill becomes a tool `a2a:{connectionId}:{skillId}` whose name keeps only letters, digits and `_`.
- `A2AAgentProxyTool` sends a 1.0 `SendMessageRequest` with a `Message` (`Role.User`, `Part.FromText`, `ContextId`, metadata `agentName`) to `connection.Endpoint`, not to the URL in the card's `supportedInterfaces`. Set `Endpoint` to the address that accepts JSON-RPC messages and also serves the card.
- `A2ACardResolver` returns a single `AgentCard`. A host that publishes an array of cards (one per agent) is best consumed in skill mode (`ExposeAgentsAsSkill = true`).
- Replace `IA2AAgentCardCacheService` for a distributed cache, or `IA2AConnectionAuthService` for a custom auth scheme.

## Host

There is no packaged host extension. The MVC and Blazor sample hosts implement the pattern below internally.

```csharp
using A2A;
using A2A.AspNetCore;
using CrestApps.Core.AI.A2A.Models;

builder.Services.Configure<A2AHostOptions>(builder.Configuration.GetSection("A2AHostOptions"));
builder.Services.AddSingleton<IAgentHandler, ProfileAgentHandler>();
builder.Services.AddSingleton<ChannelEventNotifier>();
builder.Services.AddSingleton<ITaskStore, InMemoryTaskStore>();
builder.Services.AddSingleton<IA2ARequestHandler>(sp => new A2AServer(
    sp.GetRequiredService<IAgentHandler>(),
    sp.GetRequiredService<ITaskStore>(),
    sp.GetRequiredService<ChannelEventNotifier>(),
    sp.GetRequiredService<ILogger<A2AServer>>()));

var app = builder.Build();

app.MapGet("/.well-known/agent-card.json", WriteAgentCardsAsync);
app.MapA2A(app.Services.GetRequiredService<IA2ARequestHandler>(), "a2a")
    .RequireAuthorization("A2AHostPolicy");
```

```csharp
public sealed class ProfileAgentHandler : IAgentHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProfileAgentHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var updater = new TaskUpdater(eventQueue, context.TaskId, context.ContextId);
        var services = _httpContextAccessor.HttpContext.RequestServices;

        if (!context.IsContinuation)
        {
            await updater.SubmitAsync(cancellationToken: cancellationToken);
        }

        await updater.StartWorkAsync(cancellationToken: cancellationToken);

        // Resolve the agent profile (from ?agent= or the message metadata "agentName"),
        // build its completion context, stream IAICompletionService output with updater.AddArtifactAsync(...),
        // then finish with updater.CompleteAsync(message) or updater.FailAsync(message).
    }
}
```

### Agent Cards (A2A 1.0)

A 1.0 card no longer has a top-level `url`. The endpoint is declared in `supportedInterfaces`:

```csharp
var card = new AgentCard
{
    Name = profile.DisplayText ?? profile.Name,
    Description = profile.Description,
    Version = "1.0",
    SupportedInterfaces =
    [
        new AgentInterface
        {
            Url = $"{baseUrl}/a2a?agent={Uri.EscapeDataString(profile.Name)}",
            ProtocolBinding = "JSONRPC",
            ProtocolVersion = "1.0",
        },
    ],
    DefaultInputModes = ["text/plain"],
    DefaultOutputModes = ["text/plain"],
    Capabilities = new AgentCapabilities { Streaming = true },
    Skills = [new AgentSkill { Id = profile.Name, Name = profile.DisplayText ?? profile.Name, Description = profile.Description, Tags = ["agent"] }],
};
```

```json
{
  "name": "Legal Review Agent",
  "description": "Reviews contracts.",
  "version": "1.0",
  "supportedInterfaces": [
    { "url": "https://agents.example.com/a2a?agent=legal-review", "protocolBinding": "JSONRPC", "protocolVersion": "1.0" }
  ],
  "capabilities": { "streaming": true },
  "skills": [ { "id": "legal-review", "name": "Legal Review Agent", "tags": [ "agent" ] } ]
}
```

Advertise authentication with `SecuritySchemes` and `SecurityRequirements` (an `ApiKeySecurityScheme` on the `Authorization` header, or an `OpenIdConnectSecurityScheme` pointing at `/.well-known/openid-configuration`).

### A2AHostOptions

| Property | Default | Meaning |
|---|---|---|
| `AuthenticationType` | `OpenId` | `OpenId`, `ApiKey` or `None` (development only) |
| `ApiKey` | | shared secret for `ApiKey` mode, sent as `Authorization: Bearer {key}` |
| `RequireAccessPermission` | `true` | for `OpenId`, also require the host's A2A access permission |
| `ExposeAgentsAsSkill` | `false` | `false` publishes one card per agent (a JSON array); `true` publishes one card whose skills are the agents |

The options are settings only; the host enforces them in its authentication handler and authorization policy.

Cards are built from `IAIProfileManager.GetAsync(AIProfileType.Agent)`, which includes code-defined system agents such as `tabular-data-agent`, `presentation-agent`, `word-agent` and `pdf-agent`. They are hidden from UI pickers but discoverable by remote clients. Filter the list if they should stay private.

## Upgrading from 1.x

- The `A2A` and `A2A.AspNetCore` packages moved from `0.3.4-preview` to `1.0.0-preview2`.
- Host composition changes from `ITaskManager`/`TaskManager` callbacks to an `IAgentHandler` plus `A2AServer(handler, ITaskStore, ChannelEventNotifier, logger)`, mapped with `MapA2A(IA2ARequestHandler, "a2a")`.
- `AgentCard.Url` is replaced by `SupportedInterfaces` (`AgentInterface` with `Url`, `ProtocolBinding`, `ProtocolVersion`).
- Client-side message types are `Message`, `Role`, `Part.FromText` and `SendMessageRequest`. Custom code that used `AgentMessage`, `TextPart` or `MessageSendParams` must be updated.
- The well-known path is `/.well-known/agent-card.json`.
