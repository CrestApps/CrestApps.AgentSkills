---
name: crestapps-core-a2a
description: Skill for A2A client setup, agent cards, remote skills, and sample-host guidance in CrestApps.Core.
---

# CrestApps.Core A2A - Prompt Templates

## Add A2A Client Support

You are a CrestApps.Core expert. Generate code and guidance for Agent-to-Agent protocol support in CrestApps.Core.

### Guidelines

- Use A2A when the remote system is an AI agent that reasons independently.
- Use MCP when the remote system is exposing tools or resources rather than a full agent.
- Add the A2A client to discover remote agents and invoke their skills.
- Treat A2A host registration and endpoint mapping as application-specific work.
- Configure authentication explicitly when implementing a host.

### Client Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddOpenAI()
        .AddA2AClient()
    )
);
```

### Sample Host Endpoints and Options

The MVC and Blazor sample hosts implement their own internal host registrations. They publish agent cards at `/.well-known/agent-card.json` and map the A2A protocol endpoint at `a2a`, with an application-defined task manager and authorization policy. Those registrations and routes are not a public CrestApps.Core host-composition API.

Configure the shared host options in the application that implements those endpoints:

```csharp
builder.Services.Configure<A2AHostOptions>(options =>
{
    options.AuthenticationType = A2AHostAuthenticationType.ApiKey;
    options.ApiKey = "your-secret-key";
});
```

`A2AHostOptions` also controls `RequireAccessPermission` for OpenID authentication and `ExposeAgentsAsSkill` for combined agent cards. Custom hosts must supply their own protocol route mapping, authentication, authorization, and task processing.

### A2A vs MCP

| Use case | Prefer |
|---|---|
| Remote system should think and keep agent context | A2A |
| Remote system should expose tools or resources | MCP |
