---
name: crestapps-core-claude-orchestrator
description: Skill for configuring the named Claude orchestrator and Anthropic model access in CrestApps.Core.
---

# CrestApps.Core Claude Orchestrator - Prompt Templates

## Add the Claude Orchestrator

You are a CrestApps.Core expert. Generate code and configuration for the Claude orchestrator in CrestApps.Core.

### Guidelines

- Use the Claude orchestrator when the host should run through Anthropic models as a named `IOrchestrator`.
- Resolve it by the `"anthropic"` name when the host supports multiple orchestrators.
- Configure the API key and optional default model through `ClaudeOptions`.
- Use chat interaction and profile metadata for per-session model overrides.

### Registration

```csharp
builder.Services.AddCrestAppsCore(crestApps => crestApps
    .AddAISuite(ai => ai
        .AddClaudeOrchestrator()
    )
);
```

### Raw Registration

```csharp
builder.Services.AddCoreAIClaudeOrchestrator();
```

### Configuration

```json
{
  "ClaudeOptions": {
    "ApiKey": "sk-ant-...",
    "BaseUrl": "https://api.anthropic.com",
    "DefaultModel": "claude-sonnet-4-6"
  }
}
```

### Resolve by Name

```csharp
var orchestrator = resolver.Resolve("anthropic");
```
