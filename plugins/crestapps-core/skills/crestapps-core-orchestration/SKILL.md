---
name: crestapps-core-orchestration
description: Skill for the default CrestApps.Core orchestration pipeline, tool calling, threshold-based scoping, retrieval, and streaming responses.
---

# CrestApps.Core Orchestration - Prompt Templates

## Configure the Default Orchestrator

You are a CrestApps.Core expert. Generate code and guidance for the default orchestration pipeline in CrestApps.Core.

### Guidelines

- Use the default orchestrator when the host needs tool calling, retrieval, streaming, and response routing in one pipeline.
- Register orchestration through `AddAISuite(...)` or `AddCoreAIOrchestration()`.
- Inject `IOrchestratorResolver` and call `Resolve(name)` to obtain the configured orchestrator. Resolution falls back to the configured default when the name is empty or unknown.
- Let the orchestrator handle threshold-based tool scoping instead of manually injecting very large tool sets.

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
| `IOrchestratorResolver` | Resolve a named orchestrator with fallback to the configured default |
| `IToolRegistry` | Merge tools from all providers |
| `IAIToolsService` | Resolve a registered keyed `AITool` by name |
| `IOrchestrationContextBuilder` | Build orchestration context through handlers |

### Default Scoping Guidance

- At or below `ScopingThreshold`, all configured tools are passed through.
- Above that threshold, non-MCP catalogs at or below `PlanningThreshold` use relevance scoring without an LLM planning call.
- MCP tools or a count above `PlanningThreshold` trigger an LLM planning phase followed by relevance scoring.
- If planning fails, the orchestrator still scopes by the user message and recent conversation context.
