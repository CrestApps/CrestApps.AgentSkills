---
name: crestapps-core-mcp
description: Skill for CrestApps.Core Model Context Protocol support in any ASP.NET Core host. Covers AddMcpClient and AddCoreAIMcpClient with SSE and StdIO transports, McpConnection stores and custom IMcpClientTransportProvider, AddMcpServer with the MCP C# SDK, WithCrestAppsHandlers and MapMcp, McpServerOptions authentication, opt-in tool exposure with ExposeAllTools and Tools, agents as MCP tools with ExposeAllAgents and Agents, rich tool results through McpToolResultMapper, IAIToolContentProvider and IMcpToolContentProvider, catalog prompts and resources, custom resource types with AddCoreAIMcpResourceType and McpResourceTypeHandlerBase, knowledge figure resources, and upgrading 1.x MCP hosts.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps.Core MCP

`CrestApps.Core.AI.Mcp` provides both sides of MCP: a client that turns remote MCP servers into orchestrator tools, and server handlers that expose CrestApps tools, tool instances, agents, prompts and resources through the official MCP C# SDK. FTP and SFTP resource types are covered in `crestapps-core-mcp-file-resources`.

## Client

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddMcpClient(mcpClient => mcpClient
            .AddEntityCoreStores()))   // or .AddYesSqlStores(); registers the McpConnection catalog
    .AddEntityCoreSqliteDataStore("Data Source=app.db"));
```

- `AddCoreAIMcpClient(includeStdIoTransport: true)` registers `McpService`, `IOAuth2TokenService`, metadata caching, `IMcpCapabilityResolver`, `McpToolRegistryProvider`, `McpAICompletionContextBuilderHandler` and the `sse` and `stdIo` transports. Pass `false`, or call `AddCoreAIMcpServices()` plus `AddCoreAISseMcpClientTransport()`, to leave out the local-process transport.
- An `McpConnection.Source` names the transport. Settings live in `SseMcpConnectionMetadata` or `StdioMcpConnectionMetadata` on its `Properties`; secrets are protected with Data Protection.
- Profiles select connections through `AIProfileMcpMetadata.ConnectionIds`. Tools appear as `mcp:{connectionId}:{toolName}` entries.
- Add a transport by registering an `IMcpClientTransportProvider` and `McpClientAIOptions.AddTransportType(...)`.

## Server

```csharp
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Knowledge;
using CrestApps.Core.AI.Mcp.Models;

builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddMcpServer(mcpServer => mcpServer
            .AddYesSqlStores()           // McpPrompt and McpResource catalogs
            .AddKnowledgeResources())));

builder.Services.Configure<McpServerOptions>(options =>
{
    options.AuthenticationType = McpServerAuthenticationType.ApiKey;
    options.ApiKey = builder.Configuration["Mcp:ServerApiKey"];
    options.Tools = ["crestapps-docs", "calculator"];
    options.Agents = ["researcher"];
});

builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new() { Name = "My MCP Server", Version = "1.0" };
})
.WithHttpTransport()
.WithCrestAppsHandlers();

var app = builder.Build();

app.MapMcp("mcp");
```

- The first `AddMcpServer(...)` is the CrestApps builder (`AddCoreAIMcpServer()` registers `IMcpServerPromptService` and `IMcpServerResourceService`). `builder.Services.AddMcpServer()`, `WithHttpTransport()` and `app.MapMcp(...)` come from the MCP SDK.
- `WithCrestAppsHandlers()` wires list and call handlers for tools, prompts, resources and resource templates.
- `McpServerOptions` is read through `IOptionsMonitor`, so hosts that back it with site settings change exposure without a redeploy.
- `McpServerOptions.AuthenticationType` is `OpenId` (default), `ApiKey` or `None` (development only), and `RequireAccessPermission` (default `true`) asks the host to require an access permission for OpenId callers. These are settings, not middleware: the framework does not enforce them on `/mcp`. The sample hosts add a `UseWhen` branch for `/mcp` that reads `IOptionsMonitor<McpServerOptions>`, compares the bearer or `ApiKey` header for `ApiKey` mode, challenges anonymous users for `OpenId`, and checks a role when `RequireAccessPermission` is set. Apply equivalent authentication before exposing the endpoint.

## What a Client May Call

Exposure is opt-in. Nothing is listed or callable until allowed.

| Option | Effect |
|---|---|
| `Tools` | allow-list of tool and tool instance names, case-insensitive |
| `ExposeAllTools` | every **selectable** tool and tool instance; system and hidden tools are never exposed |
| `Agents` | allow-list of agent profile names |
| `ExposeAllAgents` | every agent profile (`AIProfileType.Agent`) that has a description, including code-defined system agents |

- An agent is listed as one tool named after the agent, described by its description, taking a single `prompt`.
- Agents have their own switch so a host that already exposes tools does not start exposing agents on upgrade.
- An agent's internal tools, data sources and MCP connections are never filtered by `Tools`, and using them does not make them directly callable.
- An agent runs its own tools only when its profile sets `AgentMetadata.AllowToolInvocation = true`. Nested agent delegation stays suppressed. `AgentAvailability` is ignored for MCP.
- A name shared with an exposed tool resolves to the tool, and the agent is skipped with a warning.
- The call handler opens an `AIInvocationScope` for each call, so agents run at top-level depth and citation tools register their references.

## Tool Results

`McpToolResultMapper` maps a tool result onto MCP content blocks: a `string` is one text block, an `IMcpToolContentProvider` returns its own `ContentBlock` list, and an `IAIToolContentProvider` (for example `DataSourceRetrievalResult` or `KnowledgeObjectToolResult`) is mapped from its `AIContent` list, so a search that found a figure returns text plus a resource link. In chat completions the same results reach the model as text.

## Custom Resource Types

```csharp
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using Microsoft.Extensions.Localization;
using ModelContextProtocol.Protocol;

// IProductCatalog is the host's own service.
public sealed class ProductSheetResourceHandler : McpResourceTypeHandlerBase
{
    public const string TypeName = "product-sheet";

    private readonly IProductCatalog _catalog;

    public ProductSheetResourceHandler(IProductCatalog catalog)
        : base(TypeName)
    {
        _catalog = catalog;
    }

    protected override async Task<ReadResourceResult> GetResultAsync(
        McpResource resource,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        if (!variables.TryGetValue("sku", out var sku) || string.IsNullOrWhiteSpace(sku))
        {
            return CreateErrorResult(resource.Resource.Uri, "A product SKU is required.");
        }

        var markdown = await _catalog.GetSheetMarkdownAsync(sku, cancellationToken);

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = resource.Resource.Uri,
                    MimeType = "text/markdown",
                    Text = markdown,
                },
            ],
        };
    }
}

builder.Services
    .AddCoreAIMcpServer()
    .AddCoreAIMcpResourceType<ProductSheetResourceHandler>(ProductSheetResourceHandler.TypeName, entry =>
    {
        entry.DisplayName = new LocalizedString("ProductSheet", "Product sheet");
        entry.Description = new LocalizedString("ProductSheet Description", "Reads a product data sheet by SKU.");
        entry.SupportedVariables = [new McpResourceVariable("sku")];
    });
```

- `AddCoreAIMcpResourceType<THandler>(type, configure)` adds an `McpResourceTypeEntry` to `McpOptions`, registers the handler scoped, as `IMcpResourceTypeHandler`, and keyed by `type`. Some docs call it `AddMcpResourceType`; that method does not exist.
- A stored `McpResource` with `Source = "product-sheet"` and a URI template such as `product-sheet://catalog/{sku}` is published as a resource template; a fixed URI is published as a resource. `McpResourceUri.TryMatch` extracts the variables, and the last variable may span path segments.
- Use `SanitizePath(...)` from the base class for path variables, and `IMcpResourceHandler.Exporting` to strip secrets from exported resources.
- `AddKnowledgeResources()` (`CrestApps.Core.AI.Mcp.Knowledge`, type `datasource-figure`) publishes `crestapps://datasource/{dataSourceId}/figure/{figureId}` and serves only figures and charts that belong to that data source.

## Upgrading from 1.x

- Add `Agents` or `ExposeAllAgents` to expose agents; existing `Tools` and `ExposeAllTools` settings never expose agents.
- Tool results are no longer flattened to text over MCP. Implement `IAIToolContentProvider` or `IMcpToolContentProvider` on rich results.
- `CrestApps.Core.AI.Mcp.Ftp` and `CrestApps.Core.AI.Mcp.Sftp` are replaced by `CrestApps.Core.AI.Ftp` and `CrestApps.Core.AI.Sftp`; see `crestapps-core-mcp-file-resources`.
- The pinned MCP SDK moved from 2.0.0 to 2.2.0.
