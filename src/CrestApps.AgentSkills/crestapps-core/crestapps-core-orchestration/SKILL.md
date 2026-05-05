---
name: crestapps-core-orchestration
description: Skill for the default CrestApps.Core orchestration pipeline, tool calling, progressive scoping, retrieval, and streaming responses.
---

# CrestApps.Core Orchestration - Prompt Templates

## Configure the Default Orchestrator

You are a CrestApps.Core expert. Generate code and guidance for the default orchestration pipeline in CrestApps.Core.

### Guidelines

- Use the default orchestrator when the host needs tool calling, retrieval, streaming, and response routing in one pipeline.
- Register orchestration through `AddAISuite(...)` or `AddCoreAIOrchestration()`.
- Inject `IOrchestrator` for the active orchestrator and `IOrchestratorResolver` when the host chooses among named orchestrators.
- Let the orchestrator handle progressive tool scoping instead of manually injecting very large tool sets.

### Raw Registration

```csharp
builder.Services
    .AddCoreAIServices()
    .AddCoreAIOrchestration()
    .AddCoreAIOpenAI();
```

### Streaming Example

```csharp
public sealed class ChatService(IOrchestrator orchestrator)
{
    public async IAsyncEnumerable<string> StreamAsync(OrchestrationContext context)
    {
        await foreach (var update in orchestrator.ExecuteStreamingAsync(context))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }
}
```

### Important Services

| Service | Purpose |
|---|---|
| `IOrchestrator` | Main agentic execution loop |
| `IOrchestratorResolver` | Resolve named orchestrators |
| `IToolRegistry` | Merge tools from all providers |
| `IAIToolsService` | Tool metadata and access control |
| `IOrchestrationContextBuilder` | Build orchestration context through handlers |

### Default Scoping Guidance

- No scoping overhead below the configured threshold.
- Token-based relevance scoping for medium tool counts.
- LLM planning for very large tool sets or MCP-heavy catalogs.
- Let planning failures degrade gracefully to token-based scoping.
