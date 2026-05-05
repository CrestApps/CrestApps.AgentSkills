---
name: crestapps-core-mcp
description: Skill for MCP client and server integration, transports, prompts, resources, and resource type handlers in CrestApps.Core.
---

# CrestApps.Core MCP - Prompt Templates

## Add MCP Support

You are a CrestApps.Core expert. Generate code and guidance for Model Context Protocol integration in CrestApps.Core.

### Guidelines

- Use MCP client support to consume remote tool servers.
- Use MCP server support to expose local tools, prompts, and resources to external clients.
- Use SSE for remote HTTP MCP servers and StdIO for local processes.
- Add custom resource type handlers when the host must expose domain-specific content as MCP resources.

### Client Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddMcpClient()
    )
);
```

### Server Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddMcpServer(mcpServer => mcpServer
            .AddYesSqlStores()
            .AddFtpResources()
        )
    )
);
```

### Custom Resource Type Example

```csharp
builder.Services
    .AddCoreAIMcpServer()
    .AddMcpResourceType<MyDatabaseResourceHandler>("database");
```
