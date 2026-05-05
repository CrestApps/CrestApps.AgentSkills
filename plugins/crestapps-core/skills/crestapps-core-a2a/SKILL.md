---
name: crestapps-core-a2a
description: Skill for A2A client and host setup, agent cards, remote skills, and deciding when to use A2A instead of MCP.
---

# CrestApps.Core A2A - Prompt Templates

## Add A2A Support

You are a CrestApps.Core expert. Generate code and guidance for Agent-to-Agent protocol support in CrestApps.Core.

### Guidelines

- Use A2A when the remote system is an AI agent that reasons independently.
- Use MCP when the remote system is exposing tools or resources rather than a full agent.
- Add the A2A client to discover remote agents and invoke their skills.
- Add the A2A host to expose local agents to remote clients.
- Configure authentication explicitly for host scenarios.

### Client Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddA2AClient()
    )
);
```

### Host Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddA2AHost()
    )
);

builder.Services.Configure<A2AHostOptions>(options =>
{
    options.AuthenticationType = A2AHostAuthenticationType.ApiKey;
    options.ApiKey = "your-secret-key";
});
```

### A2A vs MCP

| Use case | Prefer |
|---|---|
| Remote system should think and keep agent context | A2A |
| Remote system should expose tools or resources | MCP |
