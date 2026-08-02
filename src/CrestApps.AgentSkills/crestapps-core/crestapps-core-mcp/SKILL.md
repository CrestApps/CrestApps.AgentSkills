---
name: crestapps-core-mcp
description: Skill for CrestApps.Core MCP client and server registration, SDK transports, handlers, prompts, and resources.
---

# CrestApps.Core MCP - Prompt Templates

## Add MCP Support

Use CrestApps MCP client support to consume remote servers. Use the server support to expose CrestApps tools, prompts, and catalog-managed resources through an MCP C# SDK server.

`AddMcpClient()` registers the CrestApps client services with SSE and StdIO transport providers. `AddMcpServer(...)` registers CrestApps prompt and resource services, but an HTTP server also needs the MCP SDK server, its transport, and an endpoint mapping.

## HTTP MCP Server

The following combines the CrestApps server services with the pinned MCP SDK HTTP transport. `WithCrestAppsHandlers()` supplies the CrestApps tool, prompt, and resource protocol handlers.

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddMcpServer(mcpServer => mcpServer
            .AddYesSqlStores())
    )
);

_ = builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "My MCP Server",
        Version = "1.0",
    };
})
.WithHttpTransport()
.WithCrestAppsHandlers();

var app = builder.Build();

app.MapMcp("mcp");
```

`AddMcpServer(...)` in the first registration is the CrestApps builder call. `builder.Services.AddMcpServer()` and `app.MapMcp(...)` are MCP SDK calls. Use both for an HTTP server. The server endpoint above is `/mcp`; apply authorization or other host middleware before exposing it publicly.

## Client Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddMcpClient(mcpClient => mcpClient
            .AddYesSqlStores())
    )
);
```

Use SSE transport for a remote HTTP MCP server. The default client registration also includes StdIO transport for a local process. Register only the connection and store features that the host needs.

## Custom Resource Types

`AddCoreAIMcpResourceType<THandler>()` registers the type in `McpOptions`, registers the handler as scoped, and makes the handler available both through `IEnumerable<IMcpResourceTypeHandler>` and as a keyed service. The resource's `Source` selects that keyed handler at read time.

For raw service registration, add the CrestApps server services before the resource type:

```csharp
builder.Services
    .AddCoreAIMcpServer()
    .AddCoreAIMcpResourceType<MyDatabaseResourceHandler>("database");
```

`MyDatabaseResourceHandler` must implement `IMcpResourceTypeHandler`. A catalog-managed resource needs a URI in its `Resource` property and a `Source` value of `database`. URI templates are listed as MCP resource templates; fixed URIs are listed as resources.
